using Content.Shared._WF.LobbyShenanigans;

namespace Content.Client._WF.LobbyShenanigans;

/// <summary>Passes on the shenanigans the server sets off to the lobby's layer.</summary>
public sealed class LobbyShenanigansSystem : EntitySystem
{
    public event Action<string>? Triggered;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LobbyShenaniganTriggerEvent>(ev => Triggered?.Invoke(ev.Id));
    }
}
