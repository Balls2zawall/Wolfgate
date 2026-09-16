using System.Buffers.Binary;
using System.Text;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Shared._WF.Audio.InternetSound;

/// <summary>
/// A content root holding active internet sounds on the server and clients. Each downloaded track has
/// an ordinary resource path, usable by positional PA playback and by replay recordings.
/// </summary>
/// <remarks>
/// Roots can only ever be added to <see cref="IResourceManager"/>, never removed, so this mounts once per
/// process and is held in a static. Entity systems are rebuilt on reconnect; the root outlives them.
/// </remarks>
public sealed class InternetSoundResources
{
    /// <summary>
    /// Where the root is mounted. Nothing ships under this path, so it can't shadow a real resource.
    /// </summary>
    public static readonly ResPath Prefix = ResPath.Root / "WFInternetSound";

    private static readonly Dictionary<IResourceManager, InternetSoundResources> Mounted = new();

    private readonly MemoryContentRoot _root = new();

    private InternetSoundResources(IResourceManager resources)
    {
        resources.AddRoot(Prefix, _root);
    }

    /// <summary>
    /// The root for this process, mounting it on first use.
    /// </summary>
    public static InternetSoundResources For(IResourceManager resources)
    {
        lock (Mounted)
        {
            if (!Mounted.TryGetValue(resources, out var existing))
                Mounted[resources] = existing = new InternetSoundResources(resources);

            return existing;
        }
    }

    /// <summary>
    /// Full path of the sound with this id. Ids are never reused, because the server caches audio length
    /// by filename forever and would hand a later track the wrong duration.
    /// </summary>
    public static string PathFor(int id)
    {
        return (Prefix / $"{id}.ogg").ToString();
    }

    private static ResPath RelativeFor(int id)
    {
        return new ResPath($"{id}.ogg");
    }

    public event Action<int, byte[]>? Stored;

    public IEnumerable<InternetSoundReplayAsset> ReplayAssets()
    {
        foreach (var (path, bytes) in _root.GetAllFiles())
        {
            if (int.TryParse(path.FilenameWithoutExtension, out var id))
                yield return new InternetSoundReplayAsset(id, bytes);
        }
    }

    public void Store(int id, byte[] audio)
    {
        _root.AddOrUpdateFile(RelativeFor(id), audio);
        Stored?.Invoke(id, audio);
    }

    public bool Has(int id)
    {
        return _root.FileExists(RelativeFor(id));
    }

    public void Remove(int id)
    {
        _root.RemoveFile(RelativeFor(id));
    }

    /// <summary>
    /// Swaps a track's bytes for a moment of silence, so reloading its resource frees the decoded audio
    /// without the load failing on a missing file.
    /// </summary>
    public void Silence(int id)
    {
        _root.AddOrUpdateFile(RelativeFor(id), SilentWav);
    }

    public IEnumerable<int> StoredIds()
    {
        foreach (var path in _root.GetAllFiles())
        {
            if (int.TryParse(path.relPath.FilenameWithoutExtension, out var id))
                yield return id;
        }
    }

    public void Clear()
    {
        _root.Clear();
    }

    /// <summary>
    /// A few samples of 8 kHz mono silence as a PCM wav, under a hundred bytes. The client sniffs audio by
    /// magic bytes rather than extension, so this loads fine from a path that used to hold an Ogg.
    /// </summary>
    private static readonly byte[] SilentWav = BuildSilentWav();

    private static byte[] BuildSilentWav()
    {
        const int sampleRate = 8000;
        const int samples = 16;
        const int dataBytes = samples * sizeof(short);

        var wav = new byte[44 + dataBytes];
        var span = wav.AsSpan();

        Encoding.ASCII.GetBytes("RIFF", span[..4]);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataBytes);
        Encoding.ASCII.GetBytes("WAVE", span[8..12]);

        Encoding.ASCII.GetBytes("fmt ", span[12..16]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16); // Chunk size.
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1); // PCM.
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], 1); // Mono.
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * sizeof(short)); // Byte rate.
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], sizeof(short)); // Block align.
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16); // Bits per sample.

        Encoding.ASCII.GetBytes("data", span[36..40]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataBytes);

        // The samples themselves are already zero, which is silence.
        return wav;
    }
}
