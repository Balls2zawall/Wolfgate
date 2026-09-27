#nullable enable
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Shared._WF.Wolfmed.Consciousness;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._WF.Wolfmed.Scenarios;

/// <summary>Playtest 4: a Downed body fires a sidearm from the floor and nothing bigger.</summary>
[TestFixture]
public sealed class WolfmedDownedSidearmTest : GameTest
{
    [TestCase("WeaponPistolMk58", false)]
    [TestCase("WeaponRevolverInspector", false)]
    [TestCase("WeaponRifleAk", true)]
    [TestCase("WeaponShotgunKammerer", true)]
    public async Task DownedShotTest(string gunProto, bool cancelled)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.EnsureComponent<WolfmedDownedComponent>(body);
            var gun = SEntMan.SpawnEntity(gunProto, map.GridCoords);
            var ev = new ShotAttemptedEvent { User = body, Used = (gun, SEntMan.GetComponent<GunComponent>(gun)) };
            SEntMan.EventBus.RaiseLocalEvent(body, ref ev);
            Assert.That(ev.Cancelled, Is.EqualTo(cancelled), cancelled
                ? $"{gunProto} fired from the floor."
                : $"{gunProto} would not fire from the floor.");
        });
    }
}
