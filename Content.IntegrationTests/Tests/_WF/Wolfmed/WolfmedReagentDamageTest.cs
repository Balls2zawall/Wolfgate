#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Server._WF.Wolfmed.Life;
using Content.Server._WF.Wolfmed.Medical;
using Content.Server.EntityEffects.Effects;
using Content.Shared._Onyx.Wounds;
using Content.Shared._WF.Wolfmed.CCVar;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._WF.Wolfmed;

/// <summary>
/// Playtest 5, "radiation medicine causes lots of body damage that the new medical system takes to overdrive": the
/// brute a metabolising reagent deals (arithrazine, 1.5 a tick) is toxin load on a wound host, never a wound, while the
/// same effect as a reaction on the skin still wounds.
/// </summary>
[TestFixture]
[TestOf(typeof(WolfmedReagentDamageSystem))]
public sealed class WolfmedReagentDamageTest : GameTest
{
    [Test]
    public async Task MetabolisedBruteIsToxinNotWoundsTest()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var toxin = SEntMan.System<WolfmedToxinSystem>();
            var factor = Server.CfgMan.GetCVar(WolfmedCVars.ReagentToxinFactor);
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var before = WoundCount(body);

            // Arithrazine's side effect, twenty metabolism ticks: a 10-unit pill.
            var arithrazine = new HealthChange { Damage = Brute(FixedPoint2.New(1.5)) };
            for (var tick = 0; tick < 20; tick++)
                arithrazine.Effect(Args(body, method: null));

            var damage = SEntMan.GetComponent<DamageableComponent>(body).Damage.DamageDict;
            var brute = damage.GetValueOrDefault("Blunt") + damage.GetValueOrDefault("Slash") + damage.GetValueOrDefault("Piercing");
            Assert.Multiple(() =>
            {
                Assert.That(WoundCount(body), Is.EqualTo(before), "a metabolising reagent wounded the body.");
                Assert.That(brute, Is.EqualTo(FixedPoint2.Zero), "the reagent's brute reached the body.");
                Assert.That(toxin.GetLoad(body), Is.EqualTo(20 * 1.5f * factor).Within(0.01f), "the reagent's damage did not become toxin load.");
            });

            // The same effect as a reaction on the skin is a wound, as before.
            var load = toxin.GetLoad(body);
            new HealthChange { Damage = Brute(FixedPoint2.New(15)) }.Effect(Args(body, ReactionMethod.Touch));
            Assert.Multiple(() =>
            {
                Assert.That(WoundCount(body), Is.GreaterThan(before), "a reaction on the skin no longer wounds.");
                Assert.That(toxin.GetLoad(body), Is.EqualTo(load).Within(0.01f), "a reaction on the skin became toxin load.");
            });
        });
    }

    private EntityEffectReagentArgs Args(EntityUid body, ReactionMethod? method) =>
        new(body, SEntMan, null, null, FixedPoint2.New(0.5), null, method, FixedPoint2.New(1));

    private static DamageSpecifier Brute(FixedPoint2 amount)
    {
        var third = amount / FixedPoint2.New(3);
        return new DamageSpecifier { DamageDict = { ["Blunt"] = third, ["Slash"] = third, ["Piercing"] = third } };
    }

    private int WoundCount(EntityUid body)
    {
        var wounds = SEntMan.System<WoundSystem>();
        return SEntMan.System<SharedBodySystem>().GetBodyChildren(body).Sum(part => wounds.GetWounds(part.Id).Count());
    }
}
