#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._WF.Wolfmed.Gore;
using Content.Server.Body.Systems;
using Content.Server.Decals;
using Content.Shared._Onyx.Wounds;
using Content.Shared._WF.Wolfmed.CCVar;
using Content.Shared.Body.Part;
using Content.Shared.Decals;
using Content.Shared.Humanoid;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests._WF.Wolfmed.Scenarios;

/// <summary>
/// Playtest 4 (GORE): extreme trauma leaves gib decals around the body, the blood layer in the blood colour, the
/// skin-coloured bits in the skin colour; a limb off leaves a few, a gibbed body leaves them all; a machine none.
/// </summary>
[TestFixture]
public sealed class WolfmedGibDecalTest : GameTest
{
    private int Count(EntityUid body, string? suffix = null)
    {
        var xform = SEntMan.GetComponent<TransformComponent>(body);
        var grid = xform.GridUid!.Value;
        var position = SEntMan.System<SharedTransformSystem>().GetMoverCoordinates(body, xform).Position;
        return SEntMan.System<DecalSystem>().GetDecalsInRange(grid, position, 4f)
            .Count(d => d.Decal.Id.StartsWith(WolfmedGibDecalSystem.Prefix) &&
                        (suffix == null ? !d.Decal.Id.EndsWith("_meat") && !d.Decal.Id.EndsWith("_flesh") : d.Decal.Id.EndsWith(suffix)));
    }

    [Test]
    public async Task LimbOffAndGibLeaveGibsTest()
    {
        await OverrideCVar(Side.Server, WolfmedCVars.GibDecals, true);
        await OverrideCVar(Side.Server, WolfmedCVars.GibSpread, 1f);
        await OverrideCVar(Side.Server, WolfmedCVars.GibsDismemberment, 2);
        await OverrideCVar(Side.Server, WolfmedCVars.GibsGib, 7);
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        EntityUid a = default;
        await Server.WaitPost(() =>
        {
            s.SetAir(map.MapUid, true);
            a = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
        });
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Count(a), Is.EqualTo(0), "gibs before any trauma.");
            Assert.That(SEntMan.System<AmputationSystem>().TryAmputate(a, s.Part(a, BodyPartType.Arm, BodyPartSymmetry.Left)), Is.True);
            Assert.That(Count(a), Is.EqualTo(2), "a limb off did not leave two gibs.");

            var blood = SEntMan.System<WolfmedGoreSystem>().GetBloodColor(a)!.Value;
            var xform = SEntMan.GetComponent<TransformComponent>(a);
            var decals = SEntMan.System<DecalSystem>().GetDecalsInRange(xform.GridUid!.Value, xform.Coordinates.Position, 4f)
                .Select(d => d.Decal).Where(d => d.Id.StartsWith(WolfmedGibDecalSystem.Prefix)).ToList();
            Assert.That(decals.Where(d => !d.Id.EndsWith("_meat") && !d.Id.EndsWith("_flesh")).All(d => d.Color == blood), Is.True,
                "a blood layer is not the blood colour.");
            var skin = SEntMan.GetComponent<HumanoidAppearanceComponent>(a).SkinColor;
            Assert.That(decals.Where(d => d.Id.EndsWith("_flesh")).All(d => d.Color == skin), Is.True, "a flesh layer is not the skin colour.");
            Assert.That(decals.All(d => d.Cleanable), Is.True, "a gib is not cleanable.");

            SEntMan.System<BodySystem>().GibBody(a);
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var grid = map.Grid.Owner;
            var all = SEntMan.System<DecalSystem>().GetDecalsInRange(grid, map.GridCoords.Position, 6f)
                .Count(d => d.Decal.Id.StartsWith(WolfmedGibDecalSystem.Prefix) && !d.Decal.Id.EndsWith("_meat") && !d.Decal.Id.EndsWith("_flesh"));
            Assert.That(all, Is.EqualTo(9), "the gib did not add seven more gibs.");
        });
    }

    [Test]
    public async Task MachinesLeaveNoGibsTest()
    {
        await OverrideCVar(Side.Server, WolfmedCVars.GibDecals, true);
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        EntityUid ipc = default;
        await Server.WaitPost(() =>
        {
            s.SetAir(map.MapUid, true);
            ipc = SEntMan.SpawnEntity("MobIPC", map.GridCoords);
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<AmputationSystem>().TryAmputate(ipc, s.Part(ipc, BodyPartType.Arm, BodyPartSymmetry.Left)), Is.True);
            Assert.That(Count(ipc), Is.EqualTo(0), "a chassis left gibs.");
        });
    }
}
