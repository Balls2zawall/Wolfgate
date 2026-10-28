using System.Collections.Generic;
using System.Numerics;

namespace Content.Client._WF.LobbyShenanigans;

/// <summary>
/// Circles that fall, bounce off the screen's sides and each other, and settle into a pile on the floor.
/// Pure maths in pixels, so it runs in a unit test without a client.
/// </summary>
public sealed class MoproachPile
{
    /// <summary>One falling moproach: where its middle is, how it moves and how it spins.</summary>
    public sealed class Body
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Radius;
        public float Angle;
        public float Spin;
        public float Age;
        public int Look;
        public bool Touching;
    }

    public const float Gravity = 1800f;
    private const float Bounce = 0.35f;
    private const float FloorFriction = 4f;
    private const float AirSpin = 0.6f;
    // Slower than this and a knock does not bounce: what keeps a resting pile from buzzing.
    private const float Rest = 120f;
    private const float ContactDamping = 6f;
    private const float MaxStep = 1f / 120f;
    private const int Iterations = 4;

    public readonly List<Body> Bodies = new();

    /// <summary>Moves everything on by <paramref name="dt"/> seconds inside a box of <paramref name="bounds"/> pixels.</summary>
    public void Step(float dt, Vector2 bounds, float scale = 1f)
    {
        // Small steps keep a tall pile from sinking into itself.
        while (dt > 0)
        {
            var h = MathF.Min(dt, MaxStep);
            dt -= h;
            Integrate(h, scale);
            for (var i = 0; i < Iterations; i++)
            {
                Collide();
                foreach (var body in Bodies)
                    Walls(body, bounds);
            }
            Settle(h);
        }
    }

    private void Integrate(float h, float scale)
    {
        foreach (var body in Bodies)
        {
            body.Age += h;
            body.Touching = false;
            body.Vel.Y += Gravity * scale * h;
            body.Pos += body.Vel * h;
            body.Angle += body.Spin * h;
            body.Spin *= 1f - AirSpin * h;
        }
    }

    private static void Walls(Body body, Vector2 bounds)
    {
        var r = body.Radius;
        if (body.Pos.X < r)
        {
            body.Pos.X = r;
            body.Vel.X = MathF.Abs(body.Vel.X) * Bounce;
        }
        else if (body.Pos.X > bounds.X - r)
        {
            body.Pos.X = bounds.X - r;
            body.Vel.X = -MathF.Abs(body.Vel.X) * Bounce;
        }

        if (body.Pos.Y > bounds.Y - r)
        {
            body.Pos.Y = bounds.Y - r;
            if (body.Vel.Y > 0)
                body.Vel.Y = body.Vel.Y < Rest ? 0 : -body.Vel.Y * Bounce;
            body.Touching = true;
            // On the floor it rolls the way it slides.
            body.Spin = body.Vel.X / r;
        }
    }

    /// <summary>Anything touching something slows down, so a pile comes to rest instead of creeping.</summary>
    private void Settle(float h)
    {
        var keep = MathF.Max(0f, 1f - ContactDamping * h);
        var slide = MathF.Max(0f, 1f - FloorFriction * h);
        foreach (var body in Bodies)
        {
            if (!body.Touching)
                continue;
            body.Vel.X *= slide;
            if (body.Vel.LengthSquared() < Rest * Rest)
                body.Vel *= keep;
        }
    }

    /// <summary>Pushes overlapping pairs apart and trades their speed along the line between them.</summary>
    private void Collide()
    {
        for (var i = 0; i < Bodies.Count; i++)
        {
            var a = Bodies[i];
            for (var j = i + 1; j < Bodies.Count; j++)
            {
                var b = Bodies[j];
                var d = b.Pos - a.Pos;
                var reach = a.Radius + b.Radius;
                var distSq = d.LengthSquared();
                if (distSq >= reach * reach)
                    continue;

                var dist = MathF.Sqrt(distSq);
                // Two in the very same spot: split them sideways.
                var n = dist > 0.0001f ? d / dist : new Vector2(i % 2 == 0 ? 1 : -1, 0);
                var push = (reach - dist) / 2f;
                a.Pos -= n * push;
                b.Pos += n * push;
                a.Touching = b.Touching = true;

                var vn = Vector2.Dot(b.Vel - a.Vel, n);
                if (vn >= 0)
                    continue;
                var impulse = -(1f + (vn > -Rest ? 0 : Bounce)) * vn / 2f;
                a.Vel -= n * impulse;
                b.Vel += n * impulse;

                // A glancing knock sets them spinning against each other.
                var tangent = new Vector2(-n.Y, n.X);
                var slide = Vector2.Dot(b.Vel - a.Vel, tangent);
                a.Spin += slide / a.Radius * 0.1f;
                b.Spin -= slide / b.Radius * 0.1f;
            }
        }
    }
}
