using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._WF.LobbyShenanigans;

/// <summary>
/// One bit of lobby fun with a button of its own. A subclass is found by reflection and gets its button in the
/// lobby's bottom bar; it moves and draws on a layer over the whole lobby that never takes a click.
/// Only the player who pressed it sees it.
/// </summary>
public abstract class LobbyShenanigan
{
    /// <summary>Fluent ID of the button's text.</summary>
    public abstract string Name { get; }

    /// <summary>Fluent ID of the button's tooltip.</summary>
    public abstract string Description { get; }

    /// <summary>Lower comes first in the bar.</summary>
    public virtual int Order => 0;

    /// <summary>The button was pressed.</summary>
    public abstract void Trigger(Vector2 bounds, float scale);

    /// <summary>Moves on by <paramref name="dt"/> seconds in a layer of <paramref name="bounds"/> pixels.</summary>
    public abstract void Update(float dt, Vector2 bounds, float scale);

    /// <summary>Draws in the layer's pixels.</summary>
    public abstract void Draw(DrawingHandleScreen handle, float scale);
}
