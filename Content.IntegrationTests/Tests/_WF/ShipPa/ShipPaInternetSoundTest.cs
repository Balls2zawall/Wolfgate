#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server._WF.Audio.InternetSound;
using Content.Server._WF.ShipPa;
using Content.Server.Power.Components;
using Content.Shared._WF.Audio.InternetSound;
using Content.Shared._WF.ShipPa;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._WF.ShipPa;

/// <summary>
/// Internet sounds are mounted as ordinary resources so the PA can play them like anything else. Fetching
/// needs yt-dlp, so these tests mount a shipped Ogg by hand and exercise everything downstream of that.
/// </summary>
[TestFixture]
[TestOf(typeof(InternetSoundSystem))]
public sealed class ShipPaInternetSoundTest
{
    /// <summary>A short shipped sound, standing in for a fetched track.</summary>
    private const string SourceSound = "/Audio/Effects/Arcade/newgame.ogg";

    private const string SpeakerProto = "WallmountShipPaSpeaker";

    [Test]
    public async Task AMountedTrackIsAnOrdinarySound()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resourceManager = server.ResolveDependency<IResourceManager>();
            var resources = InternetSoundResources.For(resourceManager);
            var audio = server.System<SharedAudioSystem>();

            const int id = 9001;
            resources.Store(id, Read(resourceManager, SourceSound));

            var path = InternetSoundResources.PathFor(id);

            Assert.That(resourceManager.ContentFileExists(new ResPath(path)), Is.True,
                "A stored track should resolve through the mounted content root.");

            // This is the whole point of mounting it: the server can read it like any shipped sound, so
            // PlayPvs and the PA's phase maths work without knowing it arrived at runtime.
            Assert.That(audio.GetAudioLength(path), Is.GreaterThan(TimeSpan.Zero),
                "The server should be able to read the length of a track mounted at runtime.");

            resources.Remove(id);

            Assert.That(resourceManager.ContentFileExists(new ResPath(path)), Is.False,
                "Removing a track should take it back out of the content root.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ATrackPlaysOnEverySpeakerAndClearsItself()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var pa = entMan.System<ShipPaSystem>();
        var grid = map.Grid.Owner;

        var speakers = new List<EntityUid>();
        var path = string.Empty;

        await server.WaitAssertion(() =>
        {
            var resourceManager = server.ResolveDependency<IResourceManager>();
            var resources = InternetSoundResources.For(resourceManager);

            const int id = 9002;
            resources.Store(id, Read(resourceManager, SourceSound));
            path = InternetSoundResources.PathFor(id);

            var mapSys = entMan.System<SharedMapSystem>();

            for (var i = 0; i < 3; i++)
            {
                mapSys.SetTile(map.Grid, new Vector2i(i, 0), map.Tile.Tile);

                var speaker = entMan.SpawnEntity(SpeakerProto, new EntityCoordinates(grid, i + 0.5f, 0.5f));
                entMan.GetComponent<ApcPowerReceiverComponent>(speaker).NeedsPower = false;
                speakers.Add(speaker);
            }

        });

        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            Assert.That(pa.StartTrack(grid, InternetSoundSystem.TrackKey, new SoundPathSpecifier(path)), Is.True,
                "A mounted track should start on the ship's PA.");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var playing = StreamsFor(entMan, path);
            Assert.That(playing, Has.Count.EqualTo(speakers.Count),
                "Every working speaker should carry its own copy of the track.");

            Assert.That(playing.Select(uid => entMan.GetComponent<ShipPaAudioComponent>(uid).BroadcastId).Distinct().Count(),
                Is.EqualTo(1),
                "The copies should share one broadcast id so the client mesh can duck all but the nearest.");
        });

        // The sound is a couple of seconds long; the PA notices it has finished on its next reconcile.
        await pair.RunTicksSync(240);

        await server.WaitAssertion(() =>
        {
            Assert.That(StreamsFor(entMan, path), Is.Empty,
                "A track should stop itself once it has played out, rather than looping like an alarm.");

            // Clearing the key is what tells the sound system the audio is safe to free; a looping alarm
            // in the same slot would still be active here.
            Assert.That(pa.IsAlarmActive(grid, InternetSoundSystem.TrackKey), Is.False,
                "A finished track should leave its PA slot free for the next one.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ABigShipStaysInsideTheClientSourceBudget()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var pa = entMan.System<ShipPaSystem>();
        var grid = map.Grid.Owner;

        // Comfortably more speakers than the engine will give one client sources for.
        const int speakerCount = 25;
        var path = string.Empty;

        await server.WaitAssertion(() =>
        {
            var resourceManager = server.ResolveDependency<IResourceManager>();
            var resources = InternetSoundResources.For(resourceManager);

            const int id = 9003;
            resources.Store(id, Read(resourceManager, SourceSound));
            path = InternetSoundResources.PathFor(id);

            var mapSys = entMan.System<SharedMapSystem>();

            for (var i = 0; i < speakerCount; i++)
            {
                mapSys.SetTile(map.Grid, new Vector2i(i, 0), map.Tile.Tile);

                var speaker = entMan.SpawnEntity(SpeakerProto, new EntityCoordinates(grid, i + 0.5f, 0.5f));
                entMan.GetComponent<ApcPowerReceiverComponent>(speaker).NeedsPower = false;
            }
        });

        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            Assert.That(pa.CountSpeakers(grid).Online, Is.EqualTo(speakerCount),
                "Every speaker should still be part of the ship's PA.");

            Assert.That(pa.StartTrack(grid, InternetSoundSystem.TrackKey, new SoundPathSpecifier(path)), Is.True);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var playing = StreamsFor(entMan, path);

            // Past the engine's per-file cap a speaker gets an entity but no audio, so the far end of a
            // long hull would caption the track in silence. Fewer, audible streams beats more, mute ones.
            Assert.That(playing, Has.Count.LessThan(speakerCount),
                "A ship with more speakers than the source budget should not stream to all of them.");

            Assert.That(playing, Is.Not.Empty,
                "It should still come out of the speakers nearest the crew.");
        });

        await pair.CleanReturnAsync();
    }

    private static byte[] Read(IResourceManager resources, string path)
    {
        using var stream = resources.ContentFileRead(path);
        using var memory = new System.IO.MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static List<EntityUid> StreamsFor(IEntityManager entMan, string path)
    {
        var found = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<AudioComponent>();

        while (query.MoveNext(out var uid, out var audio))
        {
            if (audio.FileName == path)
                found.Add(uid);
        }

        return found;
    }
}
