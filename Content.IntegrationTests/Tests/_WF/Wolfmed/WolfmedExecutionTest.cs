#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Client._WF.Wolfmed.Life;
using Content.Client.ContextMenu.UI;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests._WF.Wolfmed.Scenarios;
using Content.Server._WF.Wolfmed.Life;
using Content.Server.Chat;
using Content.Shared._Onyx.Wounds;
using Content.Shared._WF.Wolfmed.CCVar;
using Content.Shared._WF.Wolfmed.Consciousness;
using Content.Shared._WF.Wolfmed.Life;
using Content.Shared._WF.Wolfmed.Wounds;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Execution;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Players;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._WF.Wolfmed;

/// <summary>
/// Executions and weapon suicides (owner, 2026-09-30): how a weapon is measured, what each strength does to the head,
/// who can be executed, and the "are you sure?" a player is asked first. An executor with no player is not asked:
/// the Execute verbs start their do-after directly.
/// </summary>
[TestFixture]
[TestOf(typeof(WolfmedExecutionSystem))]
public sealed class WolfmedExecutionTest : GameTest
{
    private const string Artery = "WFWolfmedArterialBleedWound";
    private const string Gunshot = "WFWolfmedGunshotWound";
    private const string Stump = "DismembermentWound";

    /// <summary>One gun of each kind and tier, as it spawns. Numbers are one round's localized damage.</summary>
    private static readonly (string Gun, WolfmedKillKind Kind, WolfmedKillTier Tier)[] Guns =
    {
        ("WeaponPistolViper", WolfmedKillKind.Ballistic, WolfmedKillTier.Weak), // 9x19 mm, Piercing 24
        ("WeaponPistolMk58", WolfmedKillKind.Ballistic, WolfmedKillTier.Weak), // .45 ACP, Piercing 20; its Structural 20 is not counted
        // .357 FMJ is a cartridge that fires a hitscan: the old gun execution read it as zero. 35 x 1.2.
        ("WeaponRevolverPython", WolfmedKillKind.Ballistic, WolfmedKillTier.Medium),
        ("WeaponRevolverMateba", WolfmedKillKind.Ballistic, WolfmedKillTier.Heavy), // the same round x 3
        ("WeaponSniperRepeater", WolfmedKillKind.Ballistic, WolfmedKillTier.Medium), // and x 1.5 from a lever action
        ("WeaponShotgunSawn", WolfmedKillKind.Ballistic, WolfmedKillTier.Heavy), // buckshot: heavy by its pellets
        ("WeaponLaserGun", WolfmedKillKind.Energy, WolfmedKillTier.Weak), // Heat 22
        ("WeaponDEWCalico", WolfmedKillKind.Energy, WolfmedKillTier.Medium), // Heat 35 x 1.2
        ("UllmanWeaponPulseSniper", WolfmedKillKind.Energy, WolfmedKillTier.Heavy), // Heat 75, its Structural 300 not counted
        ("WeaponEnergyShotgun", WolfmedKillKind.Energy, WolfmedKillTier.Heavy), // a spread of laser bolts
        ("WeaponDisabler", WolfmedKillKind.NonLethal, WolfmedKillTier.Weak), // Heat 5 and stamina
        ("WeaponShotgunDoubleBarreledRubber", WolfmedKillKind.NonLethal, WolfmedKillTier.Weak), // beanbag: Blunt 10 and stamina
        ("WeaponShotgunSawnEmpty", WolfmedKillKind.NonLethal, WolfmedKillTier.Weak), // nothing to fire
    };

    /// <summary>Loose rounds, as a gun with no damage modifier of its own would fire them.</summary>
    private static readonly (string Round, WolfmedKillKind Kind, WolfmedKillTier Tier)[] Rounds =
    {
        ("ShellShotgun12_gaugeSlug", WolfmedKillKind.Ballistic, WolfmedKillTier.Medium), // Piercing 34; its Structural 200 is not counted
        ("Cartridge145x114mm", WolfmedKillKind.Ballistic, WolfmedKillTier.Heavy), // carries stamina damage and is still Piercing 105
        ("Cartridge9x19mmRubber", WolfmedKillKind.NonLethal, WolfmedKillTier.Weak),
        ("ShellShotgun12_gaugePractice", WolfmedKillKind.NonLethal, WolfmedKillTier.Weak), // six pellets of nothing
        ("CartridgeRocket", WolfmedKillKind.NonLethal, WolfmedKillTier.Weak), // the shell carries the blast; it is not a bullet
        // Heat 15 and a token blast: a bullet that burns, not a carrier shell.
        ("Cartridge68x52mmCaselessPlasma", WolfmedKillKind.Energy, WolfmedKillTier.Weak),
        // Six pellets of Piercing 7 are 42, under the heavy line: heavy by the pellets alone.
        ("ShellShotgun12_gaugeFlechette", WolfmedKillKind.Ballistic, WolfmedKillTier.Heavy),
    };

    private static readonly (string Blade, WolfmedKillTier Tier)[] Blades =
    {
        ("KitchenKnife", WolfmedKillTier.Weak), // Slash 16
        ("CombatKnife", WolfmedKillTier.Medium), // Slash 22, the arterial rule's own line
        ("Claymore", WolfmedKillTier.Heavy), // Slash 40
        ("FireAxe", WolfmedKillTier.Weak), // Slash 20 in one hand
    };

    private async Task Pin()
    {
        await OverrideCVar(Side.Server, WolfmedCVars.Consciousness, true);
    }

    /// <summary>
    /// <c>Measure</c> on real weapons: each ballistic and energy tier, a load that is heavy by its pellet count, a
    /// hitscan cartridge, less-lethal and empty guns, and blades in each tier with the wield bonus counted.
    /// </summary>
    [Test]
    public async Task MeasureTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var execution = SEntMan.System<WolfmedExecutionSystem>();
        var weapons = new Dictionary<string, EntityUid>();
        EntityUid user = default;

        await Server.WaitPost(() =>
        {
            new WolfmedScenario(SEntMan).SetAir(map.MapUid, true);
            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            foreach (var id in Guns.Select(gun => gun.Gun).Concat(Blades.Select(blade => blade.Blade)))
                weapons[id] = SEntMan.SpawnEntity(id, map.GridCoords);
        });
        await RunSeconds(1);

        await Server.WaitAssertion(() =>
        {
            var wrong = new List<string>();
            foreach (var (id, kind, tier) in Guns)
            {
                var strength = execution.Measure(weapons[id], user);
                TestContext.Out.WriteLine($"{id}: {strength}");
                if (strength.Kind != kind || strength.Lethal && strength.Tier != tier)
                    wrong.Add($"{id} measured {strength}, expected {kind} {tier}");
            }

            foreach (var (id, tier) in Blades)
            {
                var strength = execution.Measure(weapons[id], user);
                TestContext.Out.WriteLine($"{id}: {strength}");
                if (strength.Kind != WolfmedKillKind.Blade || strength.Tier != tier)
                    wrong.Add($"{id} measured {strength}, expected Blade {tier}");
            }

            var pistol = SEntMan.GetComponent<GunComponent>(weapons["WeaponPistolViper"]);
            foreach (var (id, kind, tier) in Rounds)
            {
                var strength = execution.MeasureRound((weapons["WeaponPistolViper"], pistol), SProtoMan.Index<EntityPrototype>(id));
                TestContext.Out.WriteLine($"{id}: {strength}");
                if (strength.Kind != kind || strength.Lethal && strength.Tier != tier)
                    wrong.Add($"{id} measured {strength}, expected {kind} {tier}");
            }

            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));

            // The flechette shell is what pins "a spread is heavy whatever it sums to": buckshot reaches the line anyway.
            var flechette = execution.MeasureRound((weapons["WeaponPistolViper"], pistol),
                SProtoMan.Index<EntityPrototype>("ShellShotgun12_gaugeFlechette"));
            Assert.Multiple(() =>
            {
                Assert.That(flechette.Pellets, Is.EqualTo(6));
                Assert.That(flechette.Damage, Is.EqualTo(42f).Within(0.01f));
                Assert.That(flechette.Damage, Is.LessThan(Server.CfgMan.GetCVar(WolfmedCVars.ExecutionHeavy)),
                    "the flechette fixture reaches the heavy line by its sum, so it pins nothing.");
            });

            // Buckshot is six pellets and is summed; the Structural on a .45 and the pulse sniper is left out.
            Assert.Multiple(() =>
            {
                Assert.That(execution.Measure(weapons["WeaponShotgunSawn"], user).Pellets, Is.GreaterThan(1));
                Assert.That(execution.Measure(weapons["WeaponPistolMk58"], user).Damage, Is.EqualTo(20f).Within(0.01f));
                Assert.That(execution.Measure(weapons["WeaponRevolverPython"], user).Damage, Is.EqualTo(42f).Within(0.01f));
                Assert.That(execution.Measure(weapons["UllmanWeaponPulseSniper"], user).Damage, Is.EqualTo(75f).Within(0.01f));
            });

            // A wielded fire axe is measured with its wield bonus: Slash 20 becomes 45.
            var axe = weapons["FireAxe"];
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(user, axe), Is.True);
            Assert.That(SEntMan.System<SharedWieldableSystem>().TryWield(axe, SEntMan.GetComponent<WieldableComponent>(axe), user),
                Is.True, "the axe would not wield.");
            var wielded = execution.Measure(axe, user);
            Assert.That(wielded.Tier, Is.EqualTo(WolfmedKillTier.Heavy), $"a wielded fire axe measured {wielded}.");

            // Every strength has its line for the bystanders.
            var locale = Server.ResolveDependency<ILocalizationManager>();
            foreach (var kind in new[] { "ballistic", "energy", "blade" })
            {
                foreach (var tier in new[] { "weak", "medium", "heavy" })
                    Assert.That(locale.HasString($"wolfmed-execution-{kind}-{tier}"), Is.True, $"no popup for {kind} {tier}.");
            }
        });
    }

    /// <summary>
    /// A gun execution of an Unconscious human kills at every ballistic tier: weak leaves a gunshot wound and an open
    /// artery in the head, medium throws the brain out, heavy destroys the head and leaves a stump.
    /// </summary>
    // The medium gun is the .357 repeater, not a revolver: the test pool runs with PVS off, and there a revolver's first
    // shot sends a field delta for a cartridge spawned the same tick, which the client's state assert rejects.
    [Test]
    public async Task GunExecutionTiersTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var mobState = SEntMan.System<MobStateSystem>();
        var body = SEntMan.System<SharedBodySystem>();
        var containers = SEntMan.System<SharedContainerSystem>();
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        var weak = await Execute(map, s, "MobHuman", "WeaponPistolViper");
        await Server.WaitAssertion(() =>
        {
            var brain = s.Life.GetBrainOrgan(weak.Victim);
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(weak.Victim), Is.True, "a pistol execution did not kill.");
                Assert.That(brain, Is.Not.Null, "a weak round took the brain out.");
                Assert.That(brain?.Comp.Health, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(Wounds(weak.Head), Does.Contain(Artery), "no arterial bleed in the head.");
                Assert.That(Wounds(weak.Head), Does.Contain(Gunshot), "no gunshot wound in the head.");
                Assert.That(body.GetBodyChildrenOfType(weak.Victim, BodyPartType.Head), Is.Not.Empty);
            });
        });

        var medium = await Execute(map, s, "MobHuman", "WeaponSniperRepeater");
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(medium.Victim), Is.True, "a .357 execution did not kill.");
                Assert.That(s.Life.HasBrain(medium.Victim), Is.False, "the brain is still in the body.");
                Assert.That(SEntMan.Deleted(medium.Brain), Is.False, "the brain was deleted.");
                Assert.That(containers.IsEntityInContainer(medium.Brain), Is.False, "the brain is not loose.");
                Assert.That(SEntMan.GetComponent<TransformComponent>(medium.Brain).MapID, Is.EqualTo(map.MapId));
                Assert.That(body.GetBodyChildrenOfType(medium.Victim, BodyPartType.Head), Is.Not.Empty,
                    "a medium round took the head off.");
                Assert.That(Severity(medium.Head, Gunshot), Is.GreaterThanOrEqualTo(FixedPoint2.New(50)),
                    "the hole in the head is not a severe wound.");
            });
        });

        var heavy = await Execute(map, s, "MobHuman", "WeaponShotgunSawn");
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(heavy.Victim), Is.True, "a shotgun execution did not kill.");
                Assert.That(body.GetBodyChildrenOfType(heavy.Victim, BodyPartType.Head), Is.Empty, "the head is still on.");
                Assert.That(SEntMan.Deleted(heavy.Head), Is.True, "the head was left whole on the floor.");
                Assert.That(HeadStump(heavy.Victim), Is.Not.Null, "no stump on the torso.");
                Assert.That(SEntMan.Deleted(heavy.Brain), Is.False, "the brain was deleted with the head.");
                Assert.That(containers.IsEntityInContainer(heavy.Brain), Is.False, "the brain is not loose.");
            });
        });

        // A disabler is not a way to kill anybody: the old head hit runs and nothing else does.
        var stunned = await Execute(map, s, "MobHuman", "WeaponDisabler");
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(stunned.Victim), Is.False, "a disabler execution killed.");
                Assert.That(s.Life.GetBrainOrgan(stunned.Victim)?.Comp.Health, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(Wounds(stunned.Head), Does.Not.Contain(Artery));
            });
        });
    }

    /// <summary>A heavy energy weapon burns the head to ash: no head, ash where it was, and a neck that does not bleed.</summary>
    [Test]
    public async Task EnergyHeavyAshesTheHeadTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        var scene = await Execute(map, s, "MobHuman", "UllmanWeaponPulseSniper");
        await Server.WaitAssertion(() =>
        {
            var stump = HeadStump(scene.Victim);
            var ash = 0;
            var query = SEntMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var meta, out var xform))
            {
                if (meta.EntityPrototype?.ID == "Ash" && xform.MapID == map.MapId)
                    ash++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<MobStateSystem>().IsDead(scene.Victim), Is.True, "the pulse execution did not kill.");
                Assert.That(SEntMan.System<SharedBodySystem>().GetBodyChildrenOfType(scene.Victim, BodyPartType.Head), Is.Empty,
                    "the head is still on.");
                Assert.That(SEntMan.Deleted(scene.Head), Is.True, "the head was left on the floor.");
                Assert.That(ash, Is.EqualTo(1), "no ash where the head was.");
                Assert.That(stump, Is.Not.Null, "no stump on the torso.");
                Assert.That(SEntMan.Deleted(scene.Brain), Is.False, "the brain was deleted with the head.");
            });

            Assert.That(SEntMan.System<WoundBleedingSystem>().GetPartRate(s.Part(scene.Victim, BodyPartType.Torso)), Is.Zero,
                "the burned neck is bleeding.");
            if (SEntMan.TryGetComponent(stump, out WoundBleedingComponent? bleeding))
                Assert.That(bleeding.Treatment, Is.EqualTo(BleedingTreatment.Cauterized));
        });
    }

    /// <summary>
    /// Blades: a kitchen knife opens the artery, a combat knife cuts deep as well, a claymore takes the head off and
    /// leaves it whole on the floor.
    /// </summary>
    [Test]
    public async Task BladeTiersTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var mobState = SEntMan.System<MobStateSystem>();
        var body = SEntMan.System<SharedBodySystem>();
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        var weak = await Execute(map, s, "MobHuman", "KitchenKnife");
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(weak.Victim), Is.True, "a knife execution did not kill.");
                Assert.That(Wounds(weak.Head), Does.Contain(Artery), "the throat was not opened.");
                Assert.That(Wounds(weak.Head), Does.Not.Contain("SlashWound"));
                Assert.That(s.Life.HasBrain(weak.Victim), Is.True);
            });
        });

        var medium = await Execute(map, s, "MobHuman", "CombatKnife");
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(medium.Victim), Is.True, "a combat knife execution did not kill.");
                Assert.That(Wounds(medium.Head), Does.Contain(Artery));
                Assert.That(Severity(medium.Head, "SlashWound"), Is.GreaterThanOrEqualTo(FixedPoint2.New(50)),
                    "the cut is not a severe wound.");
            });
        });

        var heavy = await Execute(map, s, "MobHuman", "Claymore");
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(heavy.Victim), Is.True, "a claymore execution did not kill.");
                Assert.That(body.GetBodyChildrenOfType(heavy.Victim, BodyPartType.Head), Is.Empty, "the head is still on.");
                Assert.That(SEntMan.Deleted(heavy.Head), Is.False, "a blade destroyed the head instead of severing it.");
                Assert.That(SEntMan.GetComponent<BodyPartComponent>(heavy.Head).Body, Is.Null);
                Assert.That(SEntMan.System<SharedContainerSystem>().IsEntityInContainer(heavy.Head), Is.False);
                Assert.That(SEntMan.GetComponent<TransformComponent>(heavy.Head).MapID, Is.EqualTo(map.MapId));
                Assert.That(HeadStump(heavy.Victim), Is.Not.Null, "no stump on the torso.");
            });
        });
    }

    /// <summary>
    /// A Downed body, awake and past its fall stun, offers Execute to a knife and to a gun; a standing one does not.
    /// </summary>
    [Test]
    public async Task DownedIsExecutableTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var hands = SEntMan.System<SharedHandsSystem>();
        EntityUid downed = default, standing = default, knifer = default, gunner = default;

        await Server.WaitPost(() =>
        {
            s.SetAir(map.MapUid, true);
            downed = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            standing = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            knifer = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            gunner = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(knifer, SEntMan.SpawnEntity("KitchenKnife", map.GridCoords)), Is.True);
            Assert.That(hands.TryPickupAnyHand(gunner, SEntMan.SpawnEntity("WeaponPistolViper", map.GridCoords)), Is.True);
        });
        await RunSeconds(1);

        await Server.WaitPost(() => s.Consciousness.SetExternalPressure(downed, "wolfmed-test", 0.71f));
        // Past the fall's short stun, which made any body executable before this change.
        await RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            Assert.That(s.State(downed), Is.EqualTo(WolfmedConsciousness.Downed), "the fixture is not Downed.");
            Assert.That(SEntMan.System<ActionBlockerSystem>().CanInteract(downed, null), Is.True,
                "the Downed body cannot act at all, so this proves nothing about the Downed rule.");

            Assert.Multiple(() =>
            {
                Assert.That(ExecuteVerb(downed, knifer), Is.Not.Null, "a knife cannot execute a Downed body.");
                Assert.That(ExecuteVerb(downed, gunner), Is.Not.Null, "a gun cannot execute a Downed body.");
                Assert.That(ExecuteVerb(standing, knifer), Is.Null, "a knife can execute a standing, unrestrained body.");
                Assert.That(ExecuteVerb(standing, gunner), Is.Null, "a gun can execute a standing, unrestrained body.");
            });
        });
    }

    /// <summary>
    /// The plan does not fit every species and the kill still happens: a chassis and a slime keep their core in the
    /// torso and die at every strength with nothing thrown, and a diona's brain is never torn out.
    /// </summary>
    [Test]
    public async Task SpeciesTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var execution = SEntMan.System<WolfmedExecutionSystem>();
        var mobState = SEntMan.System<MobStateSystem>();
        var kinds = new[] { WolfmedKillKind.Ballistic, WolfmedKillKind.Energy, WolfmedKillKind.Blade };
        var tiers = new[] { WolfmedKillTier.Weak, WolfmedKillTier.Medium, WolfmedKillTier.Heavy };
        var victims = new List<(string Species, WolfmedKillStrength Strength, EntityUid Body)>();
        EntityUid attacker = default, weapon = default, diona = default;

        await Server.WaitPost(() =>
        {
            s.SetAir(map.MapUid, true);
            attacker = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            weapon = SEntMan.SpawnEntity("KitchenKnife", map.GridCoords);
            diona = SEntMan.SpawnEntity("MobDiona", map.GridCoords);
            foreach (var species in new[] { "MobIPC", "MobSlimePerson" })
            {
                foreach (var kind in kinds)
                {
                    foreach (var tier in tiers)
                        victims.Add((species, new WolfmedKillStrength(kind, tier, 50f), SEntMan.SpawnEntity(species, map.GridCoords)));
                }
            }
        });
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            foreach (var (species, strength, victim) in victims)
            {
                Assert.That(mobState.IsDead(victim), Is.False, $"{species} fixture started dead.");
                var core = s.Life.GetBrainOrgan(victim)?.Owner;
                Assert.That(execution.Apply(victim, attacker, weapon, strength, WolfmedEnding.Execution), Is.True,
                    $"{species} was not Wolfmed's to kill.");
                Assert.Multiple(() =>
                {
                    Assert.That(mobState.IsDead(victim), Is.True, $"{species} survived {strength.Kind} {strength.Tier}.");
                    Assert.That(core, Is.Not.Null, $"{species} has no core.");
                    Assert.That(s.Life.GetBrainOrgan(victim)?.Owner, Is.EqualTo(core),
                        $"{species} lost its core to {strength.Kind} {strength.Tier}: it is in the torso.");
                });
            }

            // A diona: the medium round wounds and kills, and the brain stays where it is.
            Assert.That(execution.Apply(diona, attacker, weapon,
                new WolfmedKillStrength(WolfmedKillKind.Ballistic, WolfmedKillTier.Medium, 40f), WolfmedEnding.Execution), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(diona), Is.True, "the diona survived.");
                Assert.That(s.Life.HasBrain(diona), Is.True, "a diona's brain was torn out: it becomes a living nymph.");
            });

            // Not a wound host, or not lethal: nothing happens and the caller keeps its old behaviour.
            Assert.That(execution.Apply(weapon, attacker, weapon,
                new WolfmedKillStrength(WolfmedKillKind.Blade, WolfmedKillTier.Heavy, 50f), WolfmedEnding.Execution), Is.False);
            Assert.That(execution.Apply(attacker, attacker, weapon, WolfmedKillStrength.None, WolfmedEnding.Suicide), Is.False);
            Assert.That(mobState.IsDead(attacker), Is.False);
        });

        // The deferred head deletions and thrown organs settle without an error.
        await RunSeconds(2);
    }

    /// <summary>The Execute verb on yourself kills a wound host with a gun as it does with a knife, with the same gore.</summary>
    [Test]
    public async Task OnYourselfKillsTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        foreach (var weaponProto in new[] { "WeaponPistolViper", "KitchenKnife" })
        {
            EntityUid body = default, weapon = default, head = default;
            await Server.WaitPost(() =>
            {
                body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                weapon = SEntMan.SpawnEntity(weaponProto, map.GridCoords);
            });
            await RunSeconds(2);

            await Server.WaitAssertion(() =>
            {
                head = s.Part(body, BodyPartType.Head);
                Arm(body, weapon);
            });
            await RunSeconds(3);

            await Server.WaitAssertion(() =>
            {
                var verb = ExecuteVerb(body, body);
                Assert.That(verb?.Act, Is.Not.Null, $"no Execute verb on yourself with {weaponProto}.");
                verb!.Act!.Invoke();
            });
            await RunSeconds(7);

            await Server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.System<MobStateSystem>().IsDead(body), Is.True, $"{weaponProto} on yourself did not kill.");
                    Assert.That(s.Life.GetBrainOrgan(body)?.Comp.Health, Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(Wounds(head), Does.Contain(Artery), $"{weaponProto} on yourself left no artery open.");
                    // The weak tier's own severity: applied twice, the wound merges to double.
                    Assert.That(Severity(head, Artery), Is.EqualTo(FixedPoint2.New(20)),
                        $"{weaponProto} on yourself did not apply its tier exactly once.");
                });
            });
        }
    }

    /// <summary>
    /// A player's blade Execute on themselves, once confirmed, applies the blade's tier once, after the ghost has left:
    /// a kitchen knife opens the artery at the weak tier's severity, a claymore takes the head off, and each leaves the
    /// player in a ghost that cannot return, never in the brain of their own severed head.
    /// </summary>
    [Test]
    public async Task BladeOnYourselfGhostsTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var execution = SEntMan.System<WolfmedExecutionSystem>();
        var mobState = SEntMan.System<MobStateSystem>();
        var bodies = SEntMan.System<SharedBodySystem>();
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        // The client predicts the end of its own blade execution, which flips combat mode and closes the context menu.
        var contextMenu = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ContextMenuUIController>();
        await Client.WaitPost(contextMenu.Setup);

        var knifed = await ConfirmOnYourself(s, map, execution, "KitchenKnife");
        await Server.WaitAssertion(() =>
        {
            AssertSuicided(s, knifed.Body, "the knife Execute on yourself");
            Assert.Multiple(() =>
            {
                Assert.That(Severity(knifed.Head, Artery), Is.EqualTo(FixedPoint2.New(20)),
                    "the knife's tier was not applied exactly once.");
                Assert.That(Wounds(knifed.Head), Does.Not.Contain("SlashWound"), "a weak blade left the medium tier's cut.");
                Assert.That(bodies.GetBodyChildrenOfType(knifed.Body, BodyPartType.Head), Is.Not.Empty);
            });
        });
        await RunSeconds(1);

        var cut = await ConfirmOnYourself(s, map, execution, "Claymore");
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(mobState.IsDead(cut.Body), Is.True, "the claymore Execute on yourself did not kill.");
                Assert.That(bodies.GetBodyChildrenOfType(cut.Body, BodyPartType.Head), Is.Empty, "the head is still on.");
                Assert.That(SEntMan.Deleted(cut.Head), Is.False, "a blade destroyed the head instead of severing it.");
                Assert.That(HeadStump(cut.Body), Is.Not.Null, "no stump on the torso.");
                Assert.That(ServerSession!.AttachedEntity, Is.Not.EqualTo(cut.Brain), "the player is in the brain of the severed head.");
                Assert.That(SEntMan.GetComponent<MindContainerComponent>(cut.Brain).Mind, Is.Null,
                    "the mind rode the brain out: the head came off before the ghost left.");
            });
            AssertGhosted(cut.Body, "the claymore Execute on yourself");
        });
        await RunSeconds(2);
        await Client.WaitPost(contextMenu.Shutdown);
    }

    /// <summary>A fresh player body uses Execute on itself with this weapon, says yes, and the do-after runs out.</summary>
    private async Task<(EntityUid Body, EntityUid Head, EntityUid Brain)> ConfirmOnYourself(
        WolfmedScenario s,
        TestMapData map,
        WolfmedExecutionSystem execution,
        string weaponProto)
    {
        var armed = await PossessArmed(s, map, weaponProto);
        EntityUid brain = default;
        await Server.WaitAssertion(() =>
        {
            brain = s.Life.GetBrainOrgan(armed.Body)!.Value.Owner;
            Invoke(armed.Body, armed.Body);
            Assert.That(execution.GetPending(armed.Body), Is.EqualTo((armed.Body, armed.Weapon)),
                $"Execute on yourself with {weaponProto} did not ask.");
            Assert.That(execution.Confirm(armed.Body), Is.True, $"a yes to your own ending with {weaponProto} was refused.");
        });
        await RunSeconds(7);
        return (armed.Body, armed.Head, brain);
    }

    /// <summary>
    /// "Are you sure?": a player's Execute opens a dialog and starts nothing. A no starts nothing, a yes after the
    /// weapon was dropped starts nothing, and a yes with everything still in place starts the do-after and the victim
    /// dies, for a knife and for a gun. A second Execute replaces the question, and going Unconscious withdraws it.
    /// </summary>
    [Test]
    public async Task ConfirmationTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var execution = SEntMan.System<WolfmedExecutionSystem>();
        var hands = SEntMan.System<SharedHandsSystem>();
        var mobState = SEntMan.System<MobStateSystem>();
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        // The client predicts the end of its own knife execution, which flips combat mode and closes the context menu.
        // The pooled client is not in the gameplay state, so that menu has to be set up by hand.
        var contextMenu = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ContextMenuUIController>();
        await Client.WaitPost(contextMenu.Setup);

        // A pooled client can still show a dialog an earlier fixture answered on the server: count from what is there.
        var open = await ClientWindows<WolfmedChoiceWindow>();
        var executor = await Possess("MobHuman", map);
        EntityUid first = default, second = default, knife = default, pistol = default, head = default;
        await Server.WaitPost(() =>
        {
            first = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            second = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            knife = SEntMan.SpawnEntity("KitchenKnife", map.GridCoords);
            pistol = SEntMan.SpawnEntity("WeaponPistolViper", map.GridCoords);
        });
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            head = s.Part(second, BodyPartType.Head);
            s.Consciousness.SetExternalPressure(first, "test", 1f);
            s.Consciousness.SetExternalPressure(second, "test", 1f);
            Arm(executor, knife);
        });
        await RunSeconds(1);

        // Asked, and nothing started.
        await Server.WaitAssertion(() =>
        {
            Invoke(first, executor);
            Assert.That(execution.GetPending(executor), Is.EqualTo((first, knife)), "Execute did not ask the player.");
            Assert.That(Executing(executor), Is.False, "the do-after started before the answer.");
        });
        await RunTicksSync(30);
        Assert.That(await ClientWindows<WolfmedChoiceWindow>(), Is.EqualTo(open + 1), "the executor's client shows no dialog.");

        // A second Execute replaces the question; it does not stack a second window.
        await Server.WaitAssertion(() =>
        {
            Invoke(second, executor);
            Assert.That(execution.GetPending(executor), Is.EqualTo((second, knife)));
        });
        await RunTicksSync(30);
        Assert.That(await ClientWindows<WolfmedChoiceWindow>(), Is.EqualTo(open + 1), "the first question was left open.");

        // No.
        await Server.WaitAssertion(() =>
        {
            execution.Decline(executor);
            Assert.That(execution.GetPending(executor), Is.Null);
            Assert.That(Executing(executor), Is.False, "a no started the do-after.");
        });
        await RunSeconds(7);
        Assert.That(await ClientWindows<WolfmedChoiceWindow>(), Is.EqualTo(open), "the dialog stayed open after a no.");
        await Server.WaitAssertion(() =>
            Assert.That(mobState.IsDead(first) || mobState.IsDead(second), Is.False, "somebody died after a no."));

        // Yes, but the knife is on the floor by then.
        await Server.WaitAssertion(() =>
        {
            Invoke(first, executor);
            Assert.That(hands.TryDrop(executor, knife), Is.True);
            Assert.That(execution.Confirm(executor), Is.False, "a yes with the weapon dropped went ahead.");
            Assert.That(execution.GetPending(executor), Is.Null);
            Assert.That(Executing(executor), Is.False, "the do-after started with the weapon dropped.");
        });
        await RunSeconds(7);
        await Server.WaitAssertion(() => Assert.That(mobState.IsDead(first), Is.False, "the victim died with the weapon dropped."));

        // Yes.
        await Server.WaitAssertion(() =>
        {
            Arm(executor, knife);
            Invoke(first, executor);
            Assert.That(execution.Confirm(executor), Is.True, "a yes with everything in place was refused.");
            Assert.That(execution.GetPending(executor), Is.Null);
            Assert.That(Executing(executor), Is.True, "a yes did not start the do-after.");
        });
        await RunSeconds(7);
        Assert.That(await ClientWindows<WolfmedChoiceWindow>(), Is.EqualTo(open), "the dialog stayed open after a yes.");
        await Server.WaitAssertion(() => Assert.That(mobState.IsDead(first), Is.True, "the confirmed knife execution did not kill."));

        // The gun verb asks the same way.
        await Server.WaitAssertion(() =>
        {
            Assert.That(hands.TryDrop(executor, knife), Is.True);
            Arm(executor, pistol);
        });
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Invoke(second, executor);
            Assert.That(execution.GetPending(executor), Is.EqualTo((second, pistol)), "the gun's Execute did not ask the player.");
            Assert.That(Executing(executor), Is.False, "the gun's do-after started before the answer.");
        });
        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            Assert.That(mobState.IsDead(second), Is.False, "the gun execution went ahead unanswered.");
            Assert.That(execution.Confirm(executor), Is.True, "a yes to the gun's question was refused.");
            Assert.That(Executing(executor), Is.True, "a yes did not start the gun's do-after.");
        });
        await RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            Assert.That(mobState.IsDead(second), Is.True, "the confirmed gun execution did not kill.");
            Assert.That(Wounds(head), Does.Contain(Artery));
        });
        await RunSeconds(3);

        // An executor who goes out is no longer asked. The gun verb lets a corpse be shot, so there is still a question.
        await Server.WaitAssertion(() =>
        {
            Invoke(second, executor);
            Assert.That(execution.GetPending(executor), Is.Not.Null);
            s.Consciousness.SetExternalPressure(executor, "test", 1f);
        });
        await RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(mobState.IsCritical(executor), Is.True, "the executor fixture did not go Unconscious.");
            Assert.That(execution.GetPending(executor), Is.Null, "an Unconscious executor still has the question open.");
        });
        Assert.That(await ClientWindows<WolfmedChoiceWindow>(), Is.EqualTo(open), "the dialog stayed open on an Unconscious executor.");
        await Client.WaitPost(contextMenu.Shutdown);
    }

    /// <summary>A weapon that will not kill is turned away: no dialog, no do-after, nobody dead.</summary>
    [Test]
    public async Task NonLethalIsRefusedTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var execution = SEntMan.System<WolfmedExecutionSystem>();
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        var open = await ClientWindows<WolfmedChoiceWindow>();
        var executor = await Possess("MobHuman", map);
        EntityUid victim = default, disabler = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            disabler = SEntMan.SpawnEntity("WeaponDisabler", map.GridCoords);
        });
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            s.Consciousness.SetExternalPressure(victim, "test", 1f);
            Arm(executor, disabler);
        });
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(execution.Measure(disabler, executor).Lethal, Is.False, "the fixture's disabler measures lethal.");
            Invoke(victim, executor);
            Assert.That(execution.GetPending(executor), Is.Null, "a disabler's Execute asked for confirmation.");
            Assert.That(Executing(executor), Is.False, "a disabler's Execute started the do-after.");
        });
        await RunSeconds(8);

        Assert.That(await ClientWindows<WolfmedChoiceWindow>(), Is.EqualTo(open), "a disabler's Execute opened a dialog.");
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<MobStateSystem>().IsDead(victim), Is.False, "a disabler execution killed.");
            Assert.That(s.Life.GetBrainOrgan(victim)?.Comp.Health, Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    /// <summary>
    /// A player's gun Execute on themselves, once confirmed, spends the round and leaves the body dead with the
    /// tier's gore and a ghost that cannot return.
    /// </summary>
    [Test]
    public async Task GunOnYourselfGhostsTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var execution = SEntMan.System<WolfmedExecutionSystem>();
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        var body = await Possess("MobHuman", map);
        EntityUid pistol = default, head = default;
        await Server.WaitPost(() => pistol = SEntMan.SpawnEntity("WeaponPistolViper", map.GridCoords));
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            head = s.Part(body, BodyPartType.Head);
            Arm(body, pistol);
        });
        await RunSeconds(3);

        var loaded = 0;
        await Server.WaitAssertion(() =>
        {
            loaded = RoundsLeft(pistol);
            Invoke(body, body);
            Assert.That(execution.GetPending(body), Is.EqualTo((body, pistol)), "Execute on yourself did not ask.");
            Assert.That(execution.Confirm(body), Is.True, "a yes to your own ending was refused.");
        });
        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            AssertSuicided(s, body, "the gun Execute on yourself");
            Assert.Multiple(() =>
            {
                Assert.That(RoundsLeft(pistol), Is.EqualTo(loaded - 1), "the round was not spent.");
                Assert.That(Wounds(head), Does.Contain(Artery), "no arterial bleed in the head.");
                Assert.That(Wounds(head), Does.Contain(Gunshot), "no gunshot wound in the head.");
            });
        });
    }

    /// <summary>
    /// The suicide command with a weapon in the active hand: a loaded gun is fired into the head, an empty one falls
    /// back to the default, a blade leaves its tier's gore. Each kills and leaves a ghost that cannot return.
    /// </summary>
    [Test]
    public async Task SuicideCommandWeaponTest()
    {
        await Pin();
        var map = await Pair.CreateTestMap();
        var s = new WolfmedScenario(SEntMan);
        var suicide = SEntMan.System<SuicideSystem>();
        var bodies = SEntMan.System<SharedBodySystem>();
        await Server.WaitPost(() => s.SetAir(map.MapUid, true));

        // A loaded pistol: the round is spent and the head carries the weak tier.
        var shot = await PossessArmed(s, map, "WeaponPistolViper");
        await Server.WaitAssertion(() =>
        {
            var loaded = RoundsLeft(shot.Weapon);
            Assert.That(suicide.Suicide(shot.Body), Is.True, "the suicide was refused.");
            AssertSuicided(s, shot.Body, "the suicide command with a pistol");
            Assert.Multiple(() =>
            {
                Assert.That(RoundsLeft(shot.Weapon), Is.EqualTo(loaded - 1), "the gun was not fired.");
                Assert.That(Wounds(shot.Head), Does.Contain(Gunshot), "no gunshot wound in the head.");
                Assert.That(Wounds(shot.Head), Does.Contain(Artery), "no arterial bleed in the head.");
            });
        });
        await RunSeconds(1);

        // An empty gun: the command's default runs, and it still kills.
        var empty = await PossessArmed(s, map, "WeaponShotgunSawnEmpty");
        await Server.WaitAssertion(() =>
        {
            Assert.That(suicide.Suicide(empty.Body), Is.True, "the suicide was refused.");
            AssertSuicided(s, empty.Body, "the suicide command with an empty gun");
            Assert.Multiple(() =>
            {
                Assert.That(Wounds(empty.Head), Does.Not.Contain(Gunshot), "an empty gun left a gunshot wound.");
                Assert.That(bodies.GetBodyChildrenOfType(empty.Body, BodyPartType.Head), Is.Not.Empty);
            });
        });
        await RunSeconds(1);

        // A claymore: the head comes off, which no single hit's damage can do.
        var cut = await PossessArmed(s, map, "Claymore");
        await Server.WaitAssertion(() =>
        {
            Assert.That(suicide.Suicide(cut.Body), Is.True, "the suicide was refused.");
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<MobStateSystem>().IsDead(cut.Body), Is.True, "the suicide command with a claymore did not kill.");
                Assert.That(bodies.GetBodyChildrenOfType(cut.Body, BodyPartType.Head), Is.Empty, "the head is still on.");
                Assert.That(SEntMan.Deleted(cut.Head), Is.False, "a blade destroyed the head instead of severing it.");
                Assert.That(HeadStump(cut.Body), Is.Not.Null, "no stump on the torso.");
            });
            AssertGhosted(cut.Body, "the suicide command with a claymore");
        });
        await RunSeconds(2);
    }

    /// <summary>Puts the test player into a fresh body of this prototype.</summary>
    private async Task<EntityUid> Possess(string prototype, TestMapData map)
    {
        Assert.That(ServerSession, Is.Not.Null, "These tests need a connected pair.");
        var session = ServerSession!;
        var minds = SEntMan.System<SharedMindSystem>();
        EntityUid body = default;

        await Server.WaitPost(() =>
        {
            minds.WipeMind(session.ContentData()?.Mind);
            body = SEntMan.SpawnEntity(prototype, map.GridCoords);
            minds.TransferTo(minds.CreateMind(session.UserId).Owner, body);
        });
        await RunTicksSync(30);
        Assert.That(session.AttachedEntity, Is.EqualTo(body), "the player did not attach to the new body.");
        return body;
    }

    /// <summary>A fresh player body with this weapon in hand, ready to use.</summary>
    private async Task<(EntityUid Body, EntityUid Weapon, EntityUid Head)> PossessArmed(WolfmedScenario s, TestMapData map, string weaponProto)
    {
        var body = await Possess("MobHuman", map);
        EntityUid weapon = default, head = default;
        await Server.WaitPost(() => weapon = SEntMan.SpawnEntity(weaponProto, map.GridCoords));
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            head = s.Part(body, BodyPartType.Head);
            Arm(body, weapon);
        });
        await RunSeconds(3);
        return (body, weapon, head);
    }

    /// <summary>Dead through the brain, and the player is in a ghost that cannot come back.</summary>
    private void AssertSuicided(WolfmedScenario s, EntityUid body, string what)
    {
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.System<MobStateSystem>().IsDead(body), Is.True, $"{what} did not kill.");
            Assert.That(s.Life.GetBrainOrgan(body)?.Comp.Health, Is.EqualTo(FixedPoint2.Zero), $"{what} left the brain whole.");
        });
        AssertGhosted(body, what);
    }

    private void AssertGhosted(EntityUid body, string what)
    {
        var ghost = ServerSession!.AttachedEntity;
        Assert.Multiple(() =>
        {
            Assert.That(ghost, Is.Not.EqualTo(body), $"{what} left the player in the body.");
            Assert.That(SEntMan.TryGetComponent(ghost, out GhostComponent? ghostComp), Is.True, $"{what} did not ghost.");
            Assert.That(ghostComp?.CanReturnToBody, Is.False, $"the ghost of {what} can return.");
        });
    }

    private async Task<int> ClientWindows<T>() where T : Control
    {
        var count = 0;
        await Client.WaitPost(() => count = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
            .OfType<T>().Count(window => window.Visible));
        return count;
    }

    private void Invoke(EntityUid victim, EntityUid attacker)
    {
        var verb = ExecuteVerb(victim, attacker);
        Assert.That(verb?.Act, Is.Not.Null, "no Execute verb.");
        verb!.Act!.Invoke();
    }

    /// <summary>An Execute do-after is running for this executor.</summary>
    private bool Executing(EntityUid executor)
    {
        if (!SEntMan.TryGetComponent(executor, out DoAfterComponent? comp))
            return false;

        var doAfters = comp.DoAfters;
        return doAfters.Values.Any(doAfter => doAfter.Args.Event is ExecutionDoAfterEvent && !doAfter.Cancelled && !doAfter.Completed);
    }

    /// <summary>The rounds the gun still holds.</summary>
    private int RoundsLeft(EntityUid gun)
    {
        var count = new GetAmmoCountEvent();
        SEntMan.EventBus.RaiseLocalEvent(gun, ref count);
        return count.Count;
    }

    private sealed record Scene(EntityUid Victim, EntityUid Attacker, EntityUid Weapon, EntityUid Head, EntityUid Brain);

    /// <summary>Spawns a helpless victim and an armed attacker, invokes Execute and runs the do-after out.</summary>
    private async Task<Scene> Execute(TestMapData map, WolfmedScenario s, string victimProto, string weaponProto)
    {
        EntityUid victim = default, attacker = default, weapon = default, head = default, brain = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(victimProto, map.GridCoords);
            attacker = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            weapon = SEntMan.SpawnEntity(weaponProto, map.GridCoords);
        });
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            head = s.Part(victim, BodyPartType.Head);
            brain = s.Life.GetBrainOrgan(victim)!.Value.Owner;
            s.Consciousness.SetExternalPressure(victim, "test", 1f);
            Assert.That(SEntMan.System<MobStateSystem>().IsCritical(victim), Is.True, "the victim is not helpless.");
            Arm(attacker, weapon);
        });
        // A gun just taken in hand cannot fire for a moment, and offers no verb until it can.
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var verb = ExecuteVerb(victim, attacker);
            Assert.That(verb?.Act, Is.Not.Null, $"no Execute verb for {weaponProto}.");
            verb!.Act!.Invoke();
        });
        await RunSeconds(8);
        return new Scene(victim, attacker, weapon, head, brain);
    }

    /// <summary>Puts the weapon in the holder's hand, racked: a pistol spawns with its bolt open and only clicks.</summary>
    private void Arm(EntityUid holder, EntityUid weapon)
    {
        Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(holder, weapon), Is.True);
        if (SEntMan.TryGetComponent(weapon, out ChamberMagazineAmmoProviderComponent? chamber))
            SEntMan.System<SharedGunSystem>().SetBoltClosed(weapon, chamber, true);
    }

    private Verb? ExecuteVerb(EntityUid victim, EntityUid attacker)
    {
        return SEntMan.System<SharedVerbSystem>().GetLocalVerbs(victim, attacker, typeof(UtilityVerb), force: true)
            .FirstOrDefault(verb => verb.Text == Loc.GetString("execution-verb-name"));
    }

    private string[] Wounds(EntityUid part)
    {
        return SEntMan.System<WoundSystem>().GetWounds(part).Select(wound => wound.Comp.Prototype.Id).ToArray();
    }

    private FixedPoint2 Severity(EntityUid part, string prototype)
    {
        return SEntMan.System<WoundSystem>().GetWounds(part)
            .Where(wound => wound.Comp.Prototype == prototype)
            .Select(wound => wound.Comp.Severity)
            .DefaultIfEmpty(FixedPoint2.Zero)
            .Max();
    }

    /// <summary>The stump a lost head leaves on the torso, if there is one.</summary>
    private EntityUid? HeadStump(EntityUid body)
    {
        var torso = SEntMan.System<SharedBodySystem>().GetBodyChildrenOfType(body, BodyPartType.Torso).First().Id;
        foreach (var wound in SEntMan.System<WoundSystem>().GetWounds(torso))
        {
            if (wound.Comp.Prototype == Stump &&
                SEntMan.TryGetComponent(wound, out WolfmedStumpComponent? stump) && stump.PartType == BodyPartType.Head)
                return wound;
        }

        return null;
    }
}
