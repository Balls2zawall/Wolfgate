using System.Numerics;
using Content.Client._WF.LobbyShenanigans;
using NUnit.Framework;

namespace Content.Tests._WF.LobbyShenanigans;

/// <summary>Dropped moproaches come to rest on the floor, inside the screen, without sinking into each other.</summary>
[TestFixture]
[TestOf(typeof(MoproachPile))]
public sealed class MoproachPileTest
{
    private static readonly Vector2 Bounds = new(800, 600);

    private static MoproachPile Drop(int count)
    {
        var pile = new MoproachPile();
        var random = new System.Random(7);
        for (var i = 0; i < count; i++)
        {
            pile.Bodies.Add(new MoproachPile.Body
            {
                Pos = new Vector2(20 + random.NextSingle() * 760, -40 - i * 30),
                Vel = new Vector2((random.NextSingle() - 0.5f) * 300, 0),
                Radius = 20,
            });
        }
        for (var t = 0f; t < 10f; t += 1f / 60f)
            pile.Step(1f / 60f, Bounds);
        return pile;
    }

    [Test]
    public void OneLandsOnTheFloorAndStops()
    {
        var body = Drop(1).Bodies[0];
        Assert.That(body.Pos.Y, Is.EqualTo(Bounds.Y - body.Radius).Within(0.5f));
        Assert.That(body.Vel.Length(), Is.LessThan(5f));
    }

    [Test]
    public void ManySettleIntoAPileInsideTheScreen()
    {
        var pile = Drop(60);
        foreach (var body in pile.Bodies)
        {
            Assert.That(body.Pos.X, Is.InRange(body.Radius - 0.5f, Bounds.X - body.Radius + 0.5f));
            Assert.That(body.Pos.Y, Is.InRange(0f, Bounds.Y - body.Radius + 0.5f));
            Assert.That(body.Vel.Length(), Is.LessThan(40f), "settled, not jittering");
        }
        for (var i = 0; i < pile.Bodies.Count; i++)
        for (var j = i + 1; j < pile.Bodies.Count; j++)
        {
            var a = pile.Bodies[i];
            var b = pile.Bodies[j];
            Assert.That((a.Pos - b.Pos).Length(), Is.GreaterThan((a.Radius + b.Radius) * 0.8f), "no deep overlaps");
        }
    }
}
