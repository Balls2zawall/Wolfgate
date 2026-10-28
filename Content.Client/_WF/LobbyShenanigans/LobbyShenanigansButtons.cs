using Robust.Client.UserInterface.Controls;

namespace Content.Client._WF.LobbyShenanigans;

/// <summary>
/// A button for each shenanigan, in the lobby's bottom bar. Set <see cref="Layer"/> to the layer they play on.
/// </summary>
public sealed class LobbyShenanigansButtons : BoxContainer
{
    // The bar's LobbyLinks style reaches buttons one row down, as in the link banner.
    private readonly BoxContainer _row = new() { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 4 };
    private LobbyShenanigansLayer? _layer;

    public LobbyShenanigansButtons()
    {
        Orientation = LayoutOrientation.Horizontal;
        AddChild(_row);
    }

    public LobbyShenanigansLayer? Layer
    {
        get => _layer;
        set
        {
            _layer = value;
            _row.RemoveAllChildren();
            if (value == null)
                return;
            foreach (var shenanigan in value.Shenanigans)
            {
                var button = new Button
                {
                    Text = Loc.GetString(shenanigan.Name),
                    ToolTip = Loc.GetString(shenanigan.Description),
                };
                button.OnPressed += _ => value.Trigger(shenanigan);
                _row.AddChild(button);
            }
        }
    }
}
