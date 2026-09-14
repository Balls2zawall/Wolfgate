#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Queries;
using Content.Shared._WF.PlanetCracker.Planets;
using Content.Shared.Decals;
using Content.Shared.Salvage.Expeditions;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._WF.PlanetCracker;

/// <summary>
/// Everything F8's data promises that only a loaded server can check: that the four growth-stage decals index and name
/// real fissure.rsi states, that both one-shot effects' RSIs carry the state their prototype names and despawn on the
/// animation's own length, that the NPC targeting pair indexes, and that Asclepiu's two salvage factions and every mob
/// they can roll resolve.
/// A mistyped decal id here is not a load failure: DecalSystem.SetDecalId THROWS ArgumentOutOfRangeException on an
/// unknown prototype (Content.Server/Decals/DecalSystem.cs:427-430), so the growth promoter would surface it as a
/// server exception in the middle of a drill instead. That is why all four are asserted.
/// </summary>
[TestFixture]
public sealed class FissurePrototypeTest
{
    /// <summary>The four growth stages, in the order WFFissureSpawnerSystem.Rings.cs promotes them.</summary>
    private static readonly string[] FissureDecals =
    {
        "WFFissure1",
        "WFFissure2",
        "WFFissure3",
        "WFFissure4",
    };

    /// <summary>The decal sheet, which also carries the burst effect's state; see Resources/Prototypes/_WF/PlanetCracker/fissures.yml.</summary>
    private const string FissureRsi = "/Textures/_WF/PlanetCracker/Decals/fissure.rsi";

    /// <summary>The emerge sheet; see the same yml.</summary>
    private const string EmergeRsi = "/Textures/_WF/PlanetCracker/Effects/mob_emerge.rsi";

    /// <summary>The state WFEffectFissureBurst names, which deliberately lives in the Decals rsi rather than Effects/.</summary>
    private const string BurstState = "burst";

    /// <summary>The state WFEffectMobEmerge names.</summary>
    private const string EmergeState = "emerge";

    /// <summary>Six frames at 0.08 s, as both meta.json files declare; the yml mirrors it as a TimedDespawn lifetime.</summary>
    private const float EffectLifetime = 0.48f;

    /// <summary>The two one-shot effects and the RSI each of them draws out of.</summary>
    private static readonly (string Proto, string Rsi, string State)[] Effects =
    {
        ("WFEffectFissureBurst", FissureRsi, BurstState),
        ("WFEffectMobEmerge", EmergeRsi, EmergeState),
    };

    /// <summary>The crackable world whose faction table the fissures roll from.</summary>
    private const string Surface = "WFSurfaceAsclepiu";

    /// <summary>
    /// The utility query the stamped threats score the anchor with. A const rather than a literal for RA0033, and
    /// deliberately not a static ProtoId: UtilityQueryPrototype is server-only, and Content.YAMLLinter's
    /// ValidateStaticFields runs over this assembly on the CLIENT instance too, where that kind does not exist.
    /// </summary>
    private const string TargetQuery = "WFFissureTargets";

    /// <summary>The compound every stamped threat is re-rooted onto; a const for the same two reasons as TargetQuery.</summary>
    private const string ThreatCompound = "WFFissureThreatCompound";

    /// <summary>
    /// A decal prototype names its state as a bare SpriteSpecifier and DecalOverlay only ever draws frame zero, so a
    /// renamed or mistyped state is a silently missing fissure and nothing else in the tree would notice.
    /// </summary>
    [Test]
    public async Task FissureDecalsResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var protoMan = client.ResolveDependency<IPrototypeManager>();
        var cache = client.ResolveDependency<IResourceCache>();

        await client.WaitAssertion(() =>
        {
            var rsi = cache.GetResource<RSIResource>(new ResPath(FissureRsi)).RSI;

            using (Assert.EnterMultipleScope())
            {
                foreach (var id in FissureDecals)
                {
                    Assert.That(protoMan.TryIndex<DecalPrototype>(id, out var decal), Is.True,
                        $"{id} is not a decal prototype at all, so the growth promoter would throw on it.");
                    Assert.That(decal!.Sprite, Is.InstanceOf<SpriteSpecifier.Rsi>(),
                        $"{id} does not name an RSI state, so the fissure has nothing to draw.");

                    var state = ((SpriteSpecifier.Rsi)decal.Sprite).RsiState;

                    Assert.That(rsi.TryGetState(state, out _), Is.True,
                        $"{id} names state '{state}', which fissure.rsi does not have.");

                    // True makes DecalOverlay snap the decal to the eye's cardinal and SUBTRACT that from the
                    // per-decal Angle (Content.Client/Decals/Overlays/DecalOverlay.cs:103-109), which would throw
                    // away the ring tangent the stamp computes.
                    Assert.That(decal.SnapCardinals, Is.False,
                        $"{id} snaps to cardinals, which cancels the per-instance ring rotation.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The two one-shot effects' art, read with NO live entity anywhere.
    /// Deliberately not the EverySpriteStateExists shape: that spawns and then runs ticks before reading the client
    /// sprite, and both of these carry a 0.48 s lifetime, so the read would race the despawn exactly as
    /// PlanetCrackerPrototypeTest already documents for WFEffectSurveyPulse. Resolving the RSI through the resource
    /// cache proves the same thing and cannot race anything.
    /// </summary>
    [Test]
    public async Task FissureEffectRsisDeclareTheirStates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var cache = client.ResolveDependency<IResourceCache>();

        await client.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var (proto, path, state) in Effects)
                {
                    var rsi = cache.GetResource<RSIResource>(new ResPath(path)).RSI;

                    Assert.That(rsi.TryGetState(state, out _), Is.True,
                        $"{path} has no '{state}' state for {proto} to name.");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The despawn length, read INSIDE the spawning callback so not one tick can elapse between the spawn and the read.
    /// Anything that runs ticks first is reading a corpse: 0.48 s is fifteen ticks at the default rate.
    /// </summary>
    [Test]
    public async Task FissureEffectsDespawnOnTheAnimationLength()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var (proto, _, _) in Effects)
                {
                    var uid = entMan.SpawnEntity(proto, new MapCoordinates(Vector2.Zero, map.MapId));

                    Assert.That(entMan.TryGetComponent(uid, out TimedDespawnComponent? despawn), Is.True,
                        $"{proto} is not a one-shot effect at all; it would sit on the ground forever.");
                    Assert.That(despawn!.Lifetime, Is.EqualTo(EffectLifetime).Within(0.001f),
                        $"{proto} does not despawn on its animation's own length.");

                    entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The targeting pair the site threats are re-rooted onto. A missing compound would leave every stamped mob with a
    /// RootTask pointing at nothing, which surfaces as an HTN plan failure per mob rather than as a load error.
    /// </summary>
    [Test]
    public async Task FissureTargetingPrototypesResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        await pair.Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(protoMan.HasIndex<UtilityQueryPrototype>(TargetQuery), Is.True,
                    "WFFissureTargets is not a utility query, so the threats have nothing to pick the anchor with.");
                Assert.That(protoMan.HasIndex<HTNCompoundPrototype>(ThreatCompound), Is.True,
                    "WFFissureThreatCompound is not an HTN compound, so every stamped mob's root task dangles.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Asclepiu's two faction ids and everything they can roll. A faction whose entries do not resolve is not a load
    /// error either: the roll simply comes back with a prototype CreateEntityUninitialized then throws on, mid-drill.
    /// </summary>
    [Test]
    public async Task TheSurfaceFactionsAndTheirMobsResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var surface = protoMan.Index<WFPlanetSurfacePrototype>(Surface);
            var ids = new List<ProtoId<SalvageFactionPrototype>?> { surface.Faction, surface.UnsanctionedFaction };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(surface.Faction, Is.Not.Null, "The crackable world names no sanctioned faction at all.");
                Assert.That(surface.UnsanctionedFaction, Is.Not.Null, "The crackable world names no unsanctioned faction.");

                foreach (var id in ids)
                {
                    if (id is not { } value)
                        continue;

                    Assert.That(protoMan.TryIndex(value, out var faction), Is.True,
                        $"{value} is not a salvage faction, so the fissures would spawn nothing at all.");

                    foreach (var group in faction!.MobGroups)
                    foreach (var entry in group.Entries)
                    {
                        Assert.That(entry.PrototypeId, Is.Not.Null,
                            $"{value} has a mob entry with no prototype id.");
                        Assert.That(protoMan.HasIndex<EntityPrototype>(entry.PrototypeId!.Value), Is.True,
                            $"{value} can roll '{entry.PrototypeId}', which is not an entity prototype.");
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
