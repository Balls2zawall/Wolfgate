#nullable enable
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Shared._WF.Wolfmed.Sounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee;
using NUnit.Framework;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._WF.Wolfmed;

/// <summary>Playtest 4, second round: fists punch, the fire axe and spear chop, crowbars clang, all from Bob.</summary>
[TestFixture]
public sealed class WolfmedBobMeleeSoundsTest : GameTest
{
    [TestCase("FireAxe", "WFWolfmedChop")]
    [TestCase("Spear", "WFWolfmedChop")]
    [TestCase("SpearBone", "WFWolfmedChop")]
    [TestCase("Crowbar", "WFWolfmedCrowbarHit")]
    [TestCase("CrowbarRed", "WFWolfmedCrowbarHit")]
    public async Task WeaponCarriesTheBobCollectionTest(string proto, string collection)
    {
        await Server.WaitAssertion(() =>
        {
            var prototype = SProtoMan.Index<EntityPrototype>(proto);
            Assert.That(prototype.Components.TryGetValue("MeleeWeapon", out var entry), Is.True, $"{proto} has no MeleeWeapon.");
            var melee = (MeleeWeaponComponent) entry.Component;
            Assert.That(melee.HitSound, Is.TypeOf<SoundCollectionSpecifier>());
            Assert.That(((SoundCollectionSpecifier) melee.HitSound!).Collection, Is.EqualTo(collection));
        });
    }

    [Test]
    public async Task OwnerChosenCollectionBeatsTheStabTest()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var attacker = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var spear = SEntMan.SpawnEntity("Spear", map.GridCoords);
            var melee = SEntMan.GetComponent<MeleeWeaponComponent>(spear);
            var damage = new DamageSpecifier { DamageDict = { [new ProtoId<DamageTypePrototype>("Piercing")] = FixedPoint2.New(45) } };
            Assert.That(SEntMan.System<WolfmedOrganicSoundSystem>().GetHitSound(human, damage, spear, attacker, melee), Is.Null,
                "the spear's chop was overridden by the stab.");
            Assert.That(SProtoMan.Index<SoundCollectionPrototype>("WFWolfmedPunch").PickFiles.Count, Is.EqualTo(3));
        });
    }
}
