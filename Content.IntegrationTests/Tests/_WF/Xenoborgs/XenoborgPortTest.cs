using Content.Server._Mono.Atmos.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._WF.Xenoborgs;

[TestFixture]
public sealed class XenoborgPortTest
{
    [TestCase("Xenon")]
    [TestCase("utero")]
    [TestCase("termina")]
    public async Task ShuttleLoads(string file)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            entities.DeleteEntity(map.Grid);
            var path = new ResPath($"/Maps/_Mono/ShuttleEvent/Xeno/{file}.yml");
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(map.MapId, path, out var grid), Is.True);
            Assert.That(entities.HasComponent<ShuttleComponent>(grid!.Value), Is.True);
            entities.DeleteEntity(grid.Value);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task JetpackRechargesAndStopsAtPressureCap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.ResolveDependency<IEntityManager>();
        await pair.Server.WaitAssertion(() =>
        {
            var uid = entities.SpawnEntity("JetpackXenoborgRechargable", map.GridCoords);
            var tank = entities.GetComponent<GasTankComponent>(uid);
            var recharge = entities.GetComponent<SelfRechargingGasTankComponent>(uid);
            var system = entities.System<SelfRechargingGasTankSystem>();
            Assert.That(tank.Air.GetMoles(Gas.Plasma), Is.GreaterThan(0));
            Assert.That(recharge.RechargeRate, Is.EqualTo(0.01f));
            tank.Air = new GasMixture(5) { Temperature = recharge.GasTemperature };

            system.Update(1f);
            Assert.That(tank.Air.GetMoles(Gas.Plasma), Is.GreaterThan(0));
            Assert.That(tank.Air.Pressure, Is.LessThan(recharge.MaxPressure));

            // A long update must clamp regeneration rather than overfill or rupture the tank.
            system.Update(1000f);
            Assert.That(tank.Air.Pressure, Is.EqualTo(recharge.MaxPressure).Within(0.01f));
            var fullMoles = tank.Air.TotalMoles;
            system.Update(1f);
            Assert.That(tank.Air.TotalMoles, Is.EqualTo(fullMoles).Within(0.0001f));

            // Empty/disabled recharge and non-positive mixture entries must not generate gas.
            tank.Air = new GasMixture(5) { Temperature = recharge.GasTemperature };
            recharge.RechargeRate = 0;
            system.Update(1f);
            Assert.That(tank.Air.TotalMoles, Is.Zero);
            recharge.RechargeRate = 0.01f;
            recharge.Gases.Clear();
            system.Update(1f);
            Assert.That(tank.Air.TotalMoles, Is.Zero);
            recharge.Gases[Gas.Oxygen] = 1;
            recharge.Gases[Gas.Nitrogen] = 3;
            recharge.Gases[Gas.Plasma] = -1;
            system.Update(1f);
            Assert.That(tank.Air.GetMoles(Gas.Nitrogen),
                Is.EqualTo(tank.Air.GetMoles(Gas.Oxygen) * 3).Within(0.00001f));
            Assert.That(tank.Air.GetMoles(Gas.Plasma), Is.Zero);
            entities.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}
