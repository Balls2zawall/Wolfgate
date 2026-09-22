#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Server._NF.Shipyard.Systems;
using Content.Server._WF.Shipyard;
using Content.Server._WF.Traders;
using Content.Server.Station.Systems;
using Content.Shared._Mono.Ships.Components;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared._WF.Traders;
using Content.Shared.Access.Components;
using Content.Shared.Stacks;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._WF.Traders;

/// <summary>
/// Covers the ship-selling traders: the dialogue arguments that pick a console listing, the card a
/// trader holds while a listing is open, and the used ship round trip through YAML and back.
/// </summary>
[TestFixture]
[TestOf(typeof(UsedShipMarketSystem))]
public sealed class TraderShipTest
{
    private const string DealerProto = "WFTraderShipyard";
    private const string SalesmanProto = "WFTraderUsedShips";
    private const string DealerDialogue = "WFTraderShipyardDialogue";
    private const string ConsoleProto = "ComputerShipyard";
    private const string TableProto = "Table";
    private const string IdProto = "PassengerIDCard";
    private const string CashProto = "SpaceCash";
    private const string VesselProto = "Guppy";
    private const string ShipName = "Test Barge";
    private const string MarkerName = "Wolfgate resale marker";

    /// <summary>
    /// Every BuyShip option names a console the trader is actually allowed to front.
    /// </summary>
    [Test]
    public async Task DialogueArgumentsMatchConsoles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var compFactory = server.ResolveDependency<IComponentFactory>();

            var dealer = protoMan.Index<EntityPrototype>(DealerProto);
            Assert.That(dealer.TryGetComponent<TraderShipyardComponent>(out var shipyard, compFactory), Is.True,
                $"{DealerProto} should front shipyard listings.");

            var allowed = shipyard!.Consoles.Select(listing => listing.Console.Id).ToList();
            Assert.That(allowed, Is.Not.Empty, "The dealer should have at least one console.");

            var dialogue = protoMan.Index<TraderDialoguePrototype>(DealerDialogue);
            var buyOptions = dialogue.Options.Where(o => o.Action == TraderAction.BuyShip).ToList();
            Assert.That(buyOptions, Is.Not.Empty, "The dealer should offer ships.");

            foreach (var option in buyOptions)
            {
                Assert.That(option.Argument, Is.Not.Null,
                    "A BuyShip option has to say which console it opens.");
                Assert.That(allowed, Does.Contain(option.Argument),
                    $"'{option.Argument}' is not one of the dealer's consoles.");
                Assert.That(protoMan.HasIndex<EntityPrototype>(option.Argument!), Is.True,
                    $"'{option.Argument}' is not an entity prototype.");
            }

            // Every listed console has to be a real shipyard console the trader can host.
            foreach (var id in allowed)
            {
                var console = protoMan.Index<EntityPrototype>(id);
                Assert.That(console.TryGetComponent<ShipyardConsoleComponent>(out _, compFactory), Is.True,
                    $"{id} is not a shipyard console.");
            }

            var salesman = protoMan.Index<EntityPrototype>(SalesmanProto);
            Assert.That(salesman.TryGetComponent<TraderUsedShipsComponent>(out var used, compFactory), Is.True,
                $"{SalesmanProto} should deal in used ships.");
            Assert.That(used!.UsedShipMarkup, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(used.RelistDelay, Is.EqualTo(TimeSpan.FromMinutes(5)));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A card taken for a listing goes into the console's slot, is locked in, and comes back to the
    /// table when the conversation ends - even after the listing has been torn down.
    /// </summary>
    [Test]
    public async Task HeldIdComesBack()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var mapSys = entMan.System<SharedMapSystem>();
        var traderSys = entMan.System<TraderSystem>();
        var shipyardSys = entMan.System<ShipyardSystem>();

        var gridUid = map.Grid.Owner;

        await server.WaitAssertion(() =>
        {
            mapSys.SetTile(gridUid, map.Grid.Comp, new Vector2i(0, -1), map.Tile.Tile);

            var trader = entMan.SpawnEntity(DealerProto, new EntityCoordinates(gridUid, 0.5f, 0.5f));
            entMan.SpawnEntity(TableProto, new EntityCoordinates(gridUid, 0.5f, -0.5f));

            var comp = entMan.GetComponent<TraderComponent>(trader);
            traderSys.RefreshTable((trader, comp));
            Assert.That(comp.Table, Is.Not.Null, "The trader should have found its table.");

            var idCard = entMan.SpawnEntity(IdProto, new EntityCoordinates(gridUid, 0.5f, -0.5f));
            Assert.That(traderSys.GetZoneItems((trader, comp)), Does.Contain(idCard),
                "The card should start on the table.");

            // No owner stamp, so the trader is allowed to pick it up for the customer.
            Assert.That(traderSys.TryHoldItem((trader, comp), idCard), Is.True);
            Assert.That(traderSys.GetZoneItems((trader, comp)), Does.Not.Contain(idCard),
                "The trader should be holding the card.");

            Assert.That(shipyardSys.TryHostConsole(trader, ConsoleProto, out var uiKey), Is.True,
                "The trader should be able to front a shipyard console.");
            Assert.That(uiKey, Is.EqualTo(ShipyardConsoleUiKey.Shipyard));
            Assert.That(entMan.HasComponent<ShipyardConsoleComponent>(trader), Is.True,
                "The console's own component should be on the trader.");

            Assert.That(shipyardSys.TryInsertHostedId(trader, idCard), Is.True,
                "The card should go into the console's slot.");

            var console = entMan.GetComponent<ShipyardConsoleComponent>(trader);
            Assert.That(console.TargetIdSlot.Item, Is.EqualTo(idCard));
            Assert.That(console.TargetIdSlot.Locked, Is.True,
                "The customer must not be able to pull the card back out of the NPC.");

            comp.Customer = trader;
            traderSys.EndConversation((trader, comp), farewell: false);

            Assert.That(entMan.Deleted(idCard), Is.False, "The card must never be lost.");
            Assert.That(entMan.GetComponent<TransformComponent>(idCard).ParentUid, Is.EqualTo(gridUid),
                "The card should be back out on the table.");
            Assert.That(traderSys.GetZoneItems((trader, comp)), Does.Contain(idCard),
                "The card should be back in the barter zone.");
            Assert.That(entMan.HasComponent<ShipyardConsoleComponent>(trader), Is.False,
                "The hosted listing should have been torn down with the conversation.");

            // The salesman hosts a console too, for the sale rules it buys under.
            var salesman = entMan.SpawnEntity(SalesmanProto, new EntityCoordinates(gridUid, 1.5f, 0.5f));
            var used = entMan.GetComponent<TraderUsedShipsComponent>(salesman);
            Assert.That(shipyardSys.TryHostConsole(salesman, used.Console, out _), Is.True,
                "The salesman should be able to host the console it buys under.");
            shipyardSys.ClearHostedConsole(salesman);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A ship is bought back, kept as YAML, put on the lot after the delay, and comes back out with
    /// its cargo and its name.
    /// </summary>
    [Test]
    public async Task UsedShipRoundTrip()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.EntMan;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var mapLoader = entMan.System<MapLoaderSystem>();
        var metaSys = entMan.System<MetaDataSystem>();
        var stackSys = entMan.System<SharedStackSystem>();
        var shipyardSys = entMan.System<ShipyardSystem>();
        var marketSys = entMan.System<UsedShipMarketSystem>();

        var gridUid = map.Grid.Owner;
        var shuttle = EntityUid.Invalid;
        var console = EntityUid.Invalid;
        var station = EntityUid.Invalid;
        UsedShipListing? listing = null;
        var appraisal = 0;

        // Load a real vessel, brand it as a bought ship and drop a marker aboard.
        await server.WaitAssertion(() =>
        {
            var vessel = protoMan.Index<VesselPrototype>(VesselProto);

            Assert.That(mapLoader.TryLoadGrid(map.MapId, vessel.ShuttlePath, out var grid), Is.True,
                $"Could not load {vessel.ShuttlePath}.");

            shuttle = grid!.Value.Owner;
            metaSys.SetEntityName(shuttle, ShipName);
            entMan.EnsureComponent<VesselComponent>(shuttle).VesselId = VesselProto;

            // Somewhere over the hull, or grid traversal drops the marker onto the map instead.
            var deck = entMan.GetComponent<MapGridComponent>(shuttle).LocalAABB.Center;
            var marker = entMan.SpawnEntity(CashProto, new EntityCoordinates(shuttle, deck));
            metaSys.SetEntityName(marker, MarkerName);
            Assert.That(MarkersAboard(entMan, shuttle), Is.EqualTo(1),
                "The marker should be aboard before the ship is bought back.");

            console = entMan.SpawnEntity(ConsoleProto, new EntityCoordinates(gridUid, 0.5f, 0.5f));
            appraisal = shipyardSys.GetShipAppraisal(shuttle);
            Assert.That(appraisal, Is.GreaterThan(0), "The vessel should appraise for something.");
        });

        // Capture it the way a sale at a marked station would, without the docking rules.
        await server.WaitAssertion(() =>
        {
            Assert.That(marketSys.TryCapture(shuttle, console, appraisal,
                    UsedShipMarketSystem.DefaultMarkup, UsedShipMarketSystem.DefaultRelistDelay, out listing),
                Is.True, "The sold ship should have been copied.");

            Assert.That(listing!.ShipName, Is.EqualTo(ShipName), "The listing should keep the ship's name.");
            Assert.That(listing.DesignId?.Id, Is.EqualTo(VesselProto), "The listing should remember the design.");
            Assert.That(listing.DesignName, Is.EqualTo(protoMan.Index<VesselPrototype>(VesselProto).Name));
            Assert.That(listing.Data, Is.Not.Empty, "The grid should have been serialised.");

            Assert.That(listing.SaleValue, Is.EqualTo(shipyardSys.GetPostSaleRateBill(console, appraisal)),
                "The listing should record the post-rate, pre-tax bill.");
            Assert.That(listing.Price,
                Is.EqualTo((int) Math.Ceiling(listing.SaleValue * (1f + UsedShipMarketSystem.DefaultMarkup))),
                "The lot price should be the sale value plus the markup.");

            Assert.That(listing.AvailableAt - listing.SoldAt, Is.EqualTo(UsedShipMarketSystem.DefaultRelistDelay),
                "A bought ship should sit out back for the relist delay.");
            Assert.That(marketSys.GetAvailable(), Does.Not.Contain(listing),
                "It should not be on the lot yet.");

            // The old owner's bindings must not survive the copy.
            Assert.That(entMan.HasComponent<ShuttleDeedComponent>(shuttle), Is.False,
                "The deed should have been stripped before the copy.");

            // Stand in for five minutes passing.
            listing.AvailableAt = listing.SoldAt;
            Assert.That(marketSys.GetAvailable(), Does.Contain(listing), "It should be on the lot now.");

            entMan.DeleteEntity(shuttle);
        });

        await pair.RunTicksSync(2);

        // Buy it: load the copy and dock it to a station, then take it off the lot.
        var bought = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            station = entMan.Spawn();
            entMan.EnsureComponent<StationDataComponent>(station);
            entMan.System<StationSystem>().AddGridToStation(station, gridUid);

            Assert.That(marketSys.TryLoadListing(listing!, station, out var loaded), Is.True,
                "The saved ship should load back in.");

            bought = loaded!.Value;
            Assert.That(entMan.Deleted(bought), Is.False);
            Assert.That(entMan.HasComponent<MapGridComponent>(bought), Is.True,
                "What came back should be a grid.");
            Assert.That(entMan.GetComponent<MetaDataComponent>(bought).EntityName, Is.EqualTo(ShipName),
                "The resold ship should keep its name.");

            Assert.That(MarkersAboard(entMan, bought), Is.EqualTo(1),
                "The marker should still be aboard the resold ship.");

            marketSys.RemoveListing(listing!);
            Assert.That(marketSys.Listings, Does.Not.Contain(listing), "A sold listing should be off the lot.");
            Assert.That(marketSys.GetAvailable(), Is.Empty);

            entMan.DeleteEntity(bought);
            entMan.DeleteEntity(station);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// How many marker items are somewhere aboard a grid.
    /// </summary>
    private static int MarkersAboard(IEntityManager entMan, EntityUid grid)
    {
        return Descendants(entMan, grid)
            .Count(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityName == MarkerName);
    }

    /// <summary>
    /// Everything parented to an entity, however deeply.
    /// </summary>
    private static List<EntityUid> Descendants(IEntityManager entMan, EntityUid root)
    {
        var found = new List<EntityUid>();
        var pending = new Queue<EntityUid>();
        pending.Enqueue(root);

        while (pending.TryDequeue(out var uid))
        {
            var enumerator = entMan.GetComponent<TransformComponent>(uid).ChildEnumerator;
            while (enumerator.MoveNext(out var child))
            {
                found.Add(child);
                pending.Enqueue(child);
            }
        }

        return found;
    }
}
