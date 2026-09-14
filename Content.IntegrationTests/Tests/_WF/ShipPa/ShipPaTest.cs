#nullable enable
using System.Collections.Generic;
using Content.Server._WF.ShipPa;
using Content.Server.Power.Components;
using Content.Shared._WF.ShipPa;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._WF.ShipPa;

/// <summary>
/// Exercises the ship PA network end to end: speaker membership counting, one-shot broadcasts,
/// damage-driven distortion and breakage, repair, and the general quarters alarm loop.
/// </summary>
[TestFixture]
[TestOf(typeof(ShipPaSystem))]
public sealed class ShipPaTest
{
    private const string SpeakerProto = "WallmountShipPaSpeaker";
    private const string Chime = "/Audio/Announcements/attention.ogg";

    [Test]
    public async Task ShipPaLifecycleTest()
    {
        // Connected: false is enough - PlayPvs spawns its audio entity server-side unconditionally
        // (Robust.Server.Audio.AudioSystem.PlayPvs calls SetupAudio regardless of any player filter).
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var pa = entMan.System<ShipPaSystem>();
        var alerts = entMan.System<ShipAlertSystem>();
        var damageSys = entMan.System<DamageableSystem>();

        var gridUid = map.Grid.Owner;
        var speakerA = EntityUid.Invalid;
        var speakerB = EntityUid.Invalid;

        // Step 1: two speakers, each on their own tile, forced powered.
        await server.WaitAssertion(() =>
        {
            mapSys.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);

            speakerA = entMan.SpawnEntity(SpeakerProto, new EntityCoordinates(gridUid, 0.5f, 0.5f));
            speakerB = entMan.SpawnEntity(SpeakerProto, new EntityCoordinates(gridUid, 1.5f, 0.5f));

            entMan.GetComponent<ApcPowerReceiverComponent>(speakerA).NeedsPower = false;
            entMan.GetComponent<ApcPowerReceiverComponent>(speakerB).NeedsPower = false;

            var xformA = entMan.GetComponent<TransformComponent>(speakerA);
            var xformB = entMan.GetComponent<TransformComponent>(speakerB);

            Assert.Multiple(() =>
            {
                Assert.That(xformA.Anchored, Is.True);
                Assert.That(xformA.GridUid, Is.EqualTo(gridUid));
                Assert.That(xformB.Anchored, Is.True);
                Assert.That(xformB.GridUid, Is.EqualTo(gridUid));
            });
        });

        // Let the power update land and QueueRefresh flush (counts refresh every 0.25s = 15 ticks).
        await pair.RunTicksSync(20);

        // Step 2: grid PA membership counted.
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent(gridUid, out ShipAlertComponent? alert), Is.True);
            Assert.That(alert!.SpeakersTotal, Is.EqualTo(2));
            Assert.That(alert.SpeakersOnline, Is.EqualTo(2));

            var (online, total, _) = pa.CountSpeakers(gridUid);
            Assert.That(online, Is.EqualTo(2));
            Assert.That(total, Is.EqualTo(2));
        });

        // Step 3: a one-shot broadcast plays a tagged copy from every working speaker.
        var firstId = 0;

        await server.WaitAssertion(() =>
        {
            var id = pa.Broadcast(gridUid, new SoundPathSpecifier(Chime));
            Assert.That(id, Is.Not.Null);
            firstId = id!.Value;

            var streams = CollectBroadcast(entMan, firstId);
            Assert.That(streams, Has.Count.EqualTo(2));

            var speakers = new HashSet<EntityUid>();

            foreach (var (uid, audio, xform) in streams)
            {
                Assert.That(audio.BroadcastId, Is.EqualTo(firstId));
                Assert.That(audio.Speaker, Is.Not.Null);
                Assert.That(xform.ParentUid, Is.EqualTo(audio.Speaker!.Value));
                Assert.That(audio.IsOverlay, Is.False);
                Assert.That(entMan.HasComponent<AudioComponent>(uid), Is.True);
                speakers.Add(audio.Speaker.Value);
            }

            Assert.That(speakers, Is.EquivalentTo(new[] { speakerA, speakerB }));
        });

        // Step 4: listener filter is well-formed with nobody around, and Announce still fires.
        await server.WaitAssertion(() =>
        {
            Filter? listeners = null;
            Assert.DoesNotThrow(() => listeners = pa.GetListeners(gridUid));
            Assert.That(listeners, Is.Not.Null);
            Assert.That(listeners!.Count, Is.EqualTo(0));

            Assert.That(pa.Announce(gridUid, "test"), Is.True);
        });

        // Step 5: damage drives distortion, breakage silences a speaker, rebroadcast skips it.
        var secondId = 0;

        await server.WaitAssertion(() =>
        {
            var blunt = protoManager.Index<DamageTypePrototype>("Blunt");
            var speakerCompA = entMan.GetComponent<ShipPaSpeakerComponent>(speakerA);

            damageSys.TryChangeDamage(speakerA, new DamageSpecifier(blunt, FixedPoint2.New(30)), ignoreResistances: true);
            Assert.That(speakerCompA.Distortion, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(speakerCompA.Broken, Is.False);

            damageSys.TryChangeDamage(speakerA, new DamageSpecifier(blunt, FixedPoint2.New(40)), ignoreResistances: true);
            Assert.That(speakerCompA.Broken, Is.True);
            Assert.That(speakerCompA.Distortion, Is.EqualTo(1f).Within(0.01f));

            var id = pa.Broadcast(gridUid, new SoundPathSpecifier(Chime));
            Assert.That(id, Is.Not.Null);
            Assert.That(id, Is.Not.EqualTo(firstId));
            secondId = id!.Value;

            // The earlier broadcast's audio entities may still exist or have despawned; filter by the new id.
            var streams = CollectBroadcast(entMan, secondId);
            Assert.That(streams, Has.Count.EqualTo(1));
            Assert.That(streams[0].Audio.Speaker, Is.EqualTo(speakerB));
            Assert.That(streams[0].Xform.ParentUid, Is.EqualTo(speakerB));
        });

        // Step 6: full repair clears both breakage and distortion.
        await server.WaitAssertion(() =>
        {
            var damageableA = entMan.GetComponent<DamageableComponent>(speakerA);
            var speakerCompA = entMan.GetComponent<ShipPaSpeakerComponent>(speakerA);

            damageSys.SetAllDamage(speakerA, damageableA, FixedPoint2.Zero);

            Assert.That(speakerCompA.Broken, Is.False);
            Assert.That(speakerCompA.Distortion, Is.EqualTo(0f).Within(0.01f));
        });

        // Step 7a: situation code and general quarters loop start on every working speaker.
        await server.WaitAssertion(() =>
        {
            var alert = entMan.GetComponent<ShipAlertComponent>(gridUid);

            alerts.SetCode(gridUid, "ShipCodeRed");
            Assert.That(alert.Code.Id, Is.EqualTo("ShipCodeRed"));

            alerts.SetGeneralQuarters(gridUid, true);
            Assert.That(alert.GeneralQuarters, Is.True);
            Assert.That(pa.IsAlarmActive(gridUid, ShipAlertSystem.GeneralQuartersAlarm), Is.True);
        });

        await pair.RunTicksSync(5);

        // Step 7b: both speakers loop the alarm; breaking one should drop it out.
        await server.WaitAssertion(() =>
        {
            Assert.That(CountLoopingStreams(entMan), Is.EqualTo(2));

            var blunt = protoManager.Index<DamageTypePrototype>("Blunt");
            damageSys.TryChangeDamage(speakerA, new DamageSpecifier(blunt, FixedPoint2.New(70)), ignoreResistances: true);

            Assert.That(entMan.GetComponent<ShipPaSpeakerComponent>(speakerA).Broken, Is.True);
        });

        // Alarms reconcile every 0.5s (30 ticks); Stop() queues deletion, so give it room.
        await pair.RunTicksSync(40);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountLoopingStreams(entMan), Is.EqualTo(1));

            alerts.SetGeneralQuarters(gridUid, false);
            Assert.That(pa.IsAlarmActive(gridUid, ShipAlertSystem.GeneralQuartersAlarm), Is.False);
        });

        // Step 7c: securing from general quarters stops every remaining loop (queued deletion again).
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountLoopingStreams(entMan), Is.EqualTo(0));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>Every ShipPaAudioComponent tagged with the given broadcast id, with its transform.</summary>
    private static List<(EntityUid Uid, ShipPaAudioComponent Audio, TransformComponent Xform)> CollectBroadcast(
        EntityManager entMan, int broadcastId)
    {
        var result = new List<(EntityUid, ShipPaAudioComponent, TransformComponent)>();
        var query = entMan.EntityQueryEnumerator<ShipPaAudioComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var audio, out var xform))
        {
            if (audio.BroadcastId == broadcastId)
                result.Add((uid, audio, xform));
        }

        return result;
    }

    /// <summary>Count of live PA audio streams currently flagged to loop (i.e. active alarms).</summary>
    private static int CountLoopingStreams(EntityManager entMan)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<ShipPaAudioComponent, AudioComponent>();

        while (query.MoveNext(out _, out _, out var audio))
        {
            if (audio.Params.Loop)
                count++;
        }

        return count;
    }
}
