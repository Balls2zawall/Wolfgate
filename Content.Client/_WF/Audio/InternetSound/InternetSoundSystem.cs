using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client.Audio;
using Content.Shared._WF.Audio.InternetSound;
using Content.Shared._WF.CCVar;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Shared.Asynchronous;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Timing;

namespace Content.Client._WF.Audio.InternetSound;

/// <summary>
/// Receives admin internet sounds and mounts them as resources, so the server can then play them as ordinary
/// sounds. Also runs the radio for sounds played to everyone, and frees a track's decoded audio once the
/// server says it's done with it.
/// </summary>
public sealed partial class InternetSoundSystem : EntitySystem
{
    [Dependency] private ITransferManager _transfer = default!;
    [Dependency] private ITaskManager _task = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ContentAudioSystem _contentAudio = default!;
    [Dependency] private ClientGlobalSoundSystem _globalSound = default!;

    /// <summary>
    /// How long to keep waiting for a released track's streams to disappear before giving up on freeing it.
    /// Deleting an OpenAL buffer a source still holds silently does nothing but loses our handle to it, so
    /// running out of patience has to mean leaving the audio alone, not freeing it anyway.
    /// </summary>
    private static readonly TimeSpan FreeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The transfer key outlives entity systems, which are rebuilt on reconnect, so it routes to the live instance.
    /// </summary>
    private static readonly Dictionary<ITransferManager, InternetSoundSystem?> Receivers = new();

    private sealed record Header(int Id, string Title, string Requester, bool IsPa);

    private InternetSoundResources _resources = default!;

    /// <summary>
    /// Tracks the server has released, waiting for their streams to go away before the audio is freed.
    /// </summary>
    private readonly List<(int Id, TimeSpan Deadline)> _releasing = new();

    /// <summary>
    /// Id of a track still downloading, or 0.
    /// </summary>
    private int _loadingId;

    private InternetSoundPopup? _popup;

    /// <summary>Track the radio is showing, so another one ending doesn't close it.</summary>
    private int _popupId;

    /// <summary>
    /// Status messages for the admin who requested a sound. Also written to the console.
    /// </summary>
    public event Action<string, bool>? StatusReceived;

    /// <summary>
    /// Server state changes, for admins only.
    /// </summary>
    public event Action<InternetSoundStateEvent>? StateReceived;

    /// <summary>
    /// Last state the server sent, so windows opened later know what's playing.
    /// </summary>
    public InternetSoundStateEvent? State { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        _resources = InternetSoundResources.For(_resourceCache);

        // The engine starts new music on its own audio frame; run after it so we can re-pause in the same frame.
        UpdatesAfter.Add(typeof(AudioSystem));

        lock (Receivers)
        {
            if (!Receivers.ContainsKey(_transfer))
            {
                var transfer = _transfer;
                transfer.RegisterTransferMessage(InternetSoundProtocol.TransferKey, ev => Route(transfer, ev));
            }

            Receivers[_transfer] = this;
        }

        SubscribeNetworkEvent<InternetSoundPlayingEvent>(OnPlaying);
        SubscribeNetworkEvent<InternetSoundReleaseEvent>(OnRelease);
        SubscribeNetworkEvent<InternetSoundStopEvent>(OnStop);
        SubscribeNetworkEvent<InternetSoundStatusEvent>(OnStatus);
        SubscribeNetworkEvent<InternetSoundStateEvent>(OnState);

        Subs.CVar(_cfg, InternetSoundCVars.Volume, OnVolumeChanged);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        ClosePopup();
        ResumeMusic();

        // Disconnecting took every audio entity with it, so everything still mounted is safe to free.
        foreach (var id in _resources.StoredIds().ToList())
        {
            Free(id);
        }

        _releasing.Clear();
        _resources.Clear();

        lock (Receivers)
        {
            if (Receivers.TryGetValue(_transfer, out var receiver) && receiver == this)
                Receivers[_transfer] = null;
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        UpdateReleases();
        UpdateGlobalSound();
    }

    /// <summary>
    /// Frees released tracks once nothing is playing them any more.
    /// </summary>
    private void UpdateReleases()
    {
        for (var i = _releasing.Count - 1; i >= 0; i--)
        {
            var (id, deadline) = _releasing[i];

            if (IsPlaying(id))
            {
                if (_timing.RealTime < deadline)
                    continue;

                // Leaving it mounted wastes memory; freeing it now would waste it permanently.
                Log.Warning($"Internet sound {id} still had streams running when it was released; leaving its audio loaded.");
                _releasing.RemoveAt(i);
                continue;
            }

            Free(id);
            _releasing.RemoveAt(i);
        }
    }

    /// <summary>
    /// Holds a sound played to everyone at the listener's own volume, and keeps music out of its way.
    /// The engine rewrites gain on its audio frames, so this is done every frame rather than once.
    /// </summary>
    private void UpdateGlobalSound()
    {
        var volume = _cfg.GetCVar(CCVars.AdminSoundsEnabled) ? _cfg.GetCVar(InternetSoundCVars.Volume) : 0f;
        var playing = false;

        var query = EntityQueryEnumerator<InternetSoundAudioComponent, AudioComponent>();

        while (query.MoveNext(out _, out _, out var audio))
        {
            audio.Gain = SharedAudioSystem.VolumeToGain(audio.Params.Volume) * volume;

            if (audio.Playing)
                playing = true;
        }

        if (!playing)
        {
            ResumeMusic();
            return;
        }

        // Music loops keep starting tracks while paused; hold them until the sound finishes.
        _contentAudio.PauseMusicWolfgate();
        _globalSound.PauseEventMusicWolfgate();
        _contentAudio.EnforceMusicPauseWolfgate();
        _globalSound.EnforceEventMusicPauseWolfgate();
    }

    private void ResumeMusic()
    {
        _contentAudio.ResumeMusicWolfgate();
        _globalSound.ResumeEventMusicWolfgate();
    }

    /// <summary>
    /// True while any audio entity is still playing this track.
    /// </summary>
    private bool IsPlaying(int id)
    {
        var path = InternetSoundResources.PathFor(id);
        var query = EntityQueryEnumerator<AudioComponent>();

        while (query.MoveNext(out var audio))
        {
            if (audio.FileName == path)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Drops a track's decoded audio. The resource cache has no way to evict an audio resource, so the file
    /// is swapped for a moment of silence and reloaded: that path frees the old OpenAL buffer and leaves a
    /// harmless stub behind. Only safe once <see cref="IsPlaying"/> is false.
    /// </summary>
    private void Free(int id)
    {
        if (!_resources.Has(id))
            return;

        _resources.Silence(id);

        try
        {
            _resourceCache.ReloadResource<AudioResource>(InternetSoundResources.PathFor(id));
        }
        catch (Exception e)
        {
            Log.Warning($"Couldn't unload internet sound {id}: {e.Message}");
        }

        _resources.Remove(id);
    }

    /// <summary>
    /// Reads the header first so the radio shows while audio is still arriving, then mounts the track and
    /// tells the server it's ready. Nothing plays until the server says so.
    /// </summary>
    private static async void Route(ITransferManager transfer, TransferReceivedEvent ev)
    {
        InternetSoundSystem? receiver;
        lock (Receivers)
        {
            Receivers.TryGetValue(transfer, out receiver);
        }

        Header? header = null;
        try
        {
            await using var stream = ev.DataStream;

            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes);
            var headerLength = lengthBytes[0] | lengthBytes[1] << 8 | lengthBytes[2] << 16 | lengthBytes[3] << 24;
            if (headerLength is <= 0 or > InternetSoundProtocol.MaxHeaderBytes)
                throw new IOException($"Bad internet sound header length {headerLength}.");

            var headerBytes = new byte[headerLength];
            await stream.ReadExactlyAsync(headerBytes);
            var loaded = header = ReadHeader(headerBytes);
            receiver?._task.RunOnMainThread(() => receiver.BeginLoading(loaded));

            // Capped so a bad or oversized transfer can't exhaust client memory.
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                if (buffer.Length + read > InternetSoundProtocol.MaxPayloadBytes)
                    throw new IOException("Internet sound is larger than the client limit.");

                buffer.Write(chunk, 0, read);
            }

            var audio = buffer.ToArray();
            receiver?._task.RunOnMainThread(() => receiver.FinishLoading(loaded, audio));
        }
        catch (Exception e)
        {
            var failed = header;
            receiver?._task.RunOnMainThread(() => receiver.FailLoading(failed, e));
        }
    }

    private static Header ReadHeader(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        return new Header(reader.ReadInt32(), reader.ReadString(), reader.ReadString(), reader.ReadBoolean());
    }

    private void BeginLoading(Header header)
    {
        _loadingId = header.Id;

        // A ship's PA is diegetic: it comes out of speakers you can walk away from, so there's no radio and
        // nothing to turn down beyond the ordinary volume sliders.
        if (!header.IsPa)
            ShowPopup(header);
    }

    /// <summary>
    /// Mounts the track so the engine can resolve it, then confirms to the server.
    /// </summary>
    private void FinishLoading(Header header, byte[] audio)
    {
        if (_loadingId != header.Id)
            return;

        _loadingId = 0;
        _resources.Store(header.Id, audio);
        RaiseNetworkEvent(new InternetSoundReadyEvent(header.Id, false));
    }

    private void FailLoading(Header? header, Exception e)
    {
        Log.Error($"Failed to receive internet sound: {e}");

        if (header == null)
            return;

        if (_loadingId == header.Id)
        {
            _loadingId = 0;
            ClosePopup();
        }

        // Tell the server anyway, so one bad client doesn't hold the track up until the deadline.
        RaiseNetworkEvent(new InternetSoundReadyEvent(header.Id, true));
    }

    private void ShowPopup(Header header)
    {
        ClosePopup();

        // Players who muted admin sounds opt out of the radio too. A ship's PA is diegetic and always shows
        // nothing, so there's no case here for a sound they'd hear but have no window for.
        if (!_cfg.GetCVar(CCVars.AdminSoundsEnabled))
            return;

        var popup = new InternetSoundPopup(header.Title, header.Requester, _cfg.GetCVar(InternetSoundCVars.Volume));
        popup.VolumeChanged += volume => _cfg.SetCVar(InternetSoundCVars.Volume, volume);
        popup.VolumeReleased += () => _cfg.SaveToFile();

        // Stopping is just turning it down; the sound belongs to the server now.
        popup.StopPressed += () =>
        {
            _cfg.SetCVar(InternetSoundCVars.Volume, 0f);
            _cfg.SaveToFile();
            ClosePopup();
        };

        popup.OnClose += () =>
        {
            if (_popup != popup)
                return;

            _popup = null;
            _popupId = 0;
        };

        _popup = popup;
        _popupId = header.Id;

        // Middle top; the window keeps itself on screen.
        popup.OpenCenteredAt(new Vector2(0.5f, 0f));
    }

    private void ClosePopup()
    {
        if (_popup is not { } popup)
            return;

        _popup = null;
        _popupId = 0;
        popup.Close();
    }

    private void OnPlaying(InternetSoundPlayingEvent ev)
    {
        if (_popupId == ev.Id)
            _popup?.SetPlaying();
    }

    private void OnRelease(InternetSoundReleaseEvent ev)
    {
        if (_releasing.Any(entry => entry.Id == ev.Id))
            return;

        _releasing.Add((ev.Id, _timing.RealTime + FreeTimeout));
    }

    private void OnStop(InternetSoundStopEvent ev)
    {
        if (ev.Id == 0 || ev.Id == _loadingId)
            _loadingId = 0;

        // Several tracks can be in play, so one ending mustn't close another's radio.
        if (ev.Id == 0 || ev.Id == _popupId)
            ClosePopup();
    }

    private void OnStatus(InternetSoundStatusEvent ev)
    {
        if (ev.IsError)
            _console.WriteError(null, ev.Message);
        else
            _console.WriteLine(null, ev.Message);

        StatusReceived?.Invoke(ev.Message, ev.IsError);
    }

    /// <summary>
    /// Asks the server what's playing. Admin windows call this when they open.
    /// </summary>
    public void RequestState()
    {
        RaiseNetworkEvent(new InternetSoundStateRequestEvent());
    }

    private void OnState(InternetSoundStateEvent ev)
    {
        State = ev;
        StateReceived?.Invoke(ev);
    }

    private void OnVolumeChanged(float volume)
    {
        _popup?.SetVolume(volume);
    }
}
