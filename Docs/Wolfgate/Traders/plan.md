# NPC Traders (`_WF/Traders`)

Static humanoid NPCs, placed by mappers on Caelestinus Central, that front an existing
service (vending inventory, refuelling, later shipyard) through a dialogue menu.
Vending machines and consoles are untouched and keep working.

## Conventions
- All new code under `Content.{Shared,Server,Client}/_WF/Traders`, prototypes under
  `Resources/Prototypes/_WF/Traders`, locale in `Resources/Locale/en-US/_WF/traders.ftl`.
- Upstream edits: minimal, each marked `// WOLFGATE`.
- No licence headers. `/// <summary>` one-liners, sparse `//` notes.
  `[Dependency] private X _x = default!;` (no readonly). Reference style: `_WF/SafetyDepositBox`.
- Client/shared types must be sandbox-safe. Never name window controls
  CloseButton/ContentsContainer/TitleLabel/WindowHeader.
- Only one system may subscribe a given (component, event) pair — check before subscribing
  to events on upstream components.

## Decisions
- D1 ID ownership: `IdCardOwnerComponent` (shared, `_WF/Access`) stamped on the player's ID at
  spawn (`PlayerSpawnCompleteEvent`; find the ID via `IdCardSystem.TryFindIdCard`): `NetUserId UserId`,
  `string CharacterName`. A trader charges an ID only when the customer's session UserId matches
  the stamp (then `BankSystem.TryBankWithdraw(customerMob, amount)`). Otherwise
  "This isn't your ID." Unstamped IDs: cash only.
- D2 One customer at a time. Others get "One moment, please." Conversation ends on window close,
  customer > 3.5 tiles away, customer deleted/crit, or 60 s without input. On end, anything the
  trader is holding for the customer goes back (table, else customer's feet).
- D3 Table mode: at MapInit and at each conversation start the trader looks at the tile one step
  along `Transform.LocalRotation.GetCardinalDir()`; an anchored entity with
  `PlaceableSurfaceComponent` there becomes `TraderComponent.Table`. No table → `Table = null`.
- D4 Barter zone. Table mode: unanchored items intersecting the table's tile. No-table mode: items
  handed to the trader (`InteractUsing`) go into a `trader-offer` container on the trader.
  In table mode `InteractUsing` on the trader is refused with "Put it on the table, please."
- D5 Zone marker: server spawns `WFTraderBarterZone` (sprite-only, unshaded thin outline, draw depth
  just above tables, below items) at the table tile centre; deleted with the table/trader or when
  table mode is lost.
- D6 Reach: WOLFGATE edit in `SharedInteractionSystem.InRangeUnobstructed(Entity, Entity, ...)`:
  raise by-ref `InteractionRangeBonusEvent(User, Target) { float Bonus }` on `other`, add `Bonus`
  to `range` when `range > 0`. Shared `TraderReachSystem` answers with `TableReachBonus` (1 tile)
  only when `Table != null`. BUI `range` in YAML is 3.5 so windows survive across the table.
- D7 Payment (`TraderSystem.TryTakePayment`): cash in the zone first. If cash >= price the trader
  takes every cash stack and returns change as 2–3 stacks of uneven size (1 stack if change < 3).
  If cash < price and a valid ID (D1) is in the zone: take all cash, withdraw the rest from the bank.
  Otherwise refuse: "You're {$amount} short." Price 0 → no requirement.
  Cash = entities with `CashComponent` + `StackComponent` of stack type `Credit` (verify id); value
  = stack count.
- D8 Output: table mode → on the table tile (small random offset); else at the trader.
- D9 Dialogue is data (`traderDialogue` prototype). Picking an option: customer is forced to say
  the prompt (`ChatSystem.TrySendInGameICMessage` on the customer), trader replies ~1 s later, then
  the action runs. If the option's requirement is not in the zone the trader instead says
  "Please give me your {$thing}." and the menu stays open.
- D10 Shop mirrors one or more vending machine *entity prototypes* (`TraderShopComponent.Vendors`,
  e.g. `[ VendingMachineFuelVend ]`): pack, `requiresCash`, `MarketModifier` and `taxAccounts` are
  read from each prototype, prices computed exactly as `VendingMachineSystem.AuthorizedVend` does
  (GetEstimatedPrice, fallback 20, × modifier, overridden by GetEstimatedVendPrice > 0). The
  catalogue is the machines' starting inventories concatenated in listed order, de-duplicated by
  item id (first machine wins) and sorted by name within each machine's block; every line is priced
  by its own machine. Prices are sent in BUI state; the client does not re-derive them.
  The client keeps a basket (≤ 20 of one item, ≤ 12 distinct items, caps re-checked server-side) and
  checks out with `TraderShopCheckoutMessage(Dictionary<string,int>)`. One checkout = one
  `TryTakePayment` for the whole basket, taxes paid per machine on that machine's share of the
  total, `MarkAsPurchased` per item at its own unit price. A refused payment leaves the basket
  alone (`TraderShopCheckoutResultMessage(false)`); the window stays open either way and the
  trader thanks the customer on success.
- D10a Packaging: exactly one item entity in an order lands on the output spot bare, receipt beside
  it. More than one and the goods go into `WFTraderParcel` cartons (hand-carried item, plain
  `Container`, not storage), filled in order, `TraderParcelComponent.Capacity` (8) goods each, the
  receipt inside the first parcel and not counted against capacity. Parcels land on the output spot
  with the usual small offsets. Using a parcel in hand (or its "Open" verb) tips it out at the
  user's feet, puts the top item in a free hand, plays a cardboard sound and deletes the carton;
  a parcel that is deleted with goods inside spills them instead.
- D11 Receipts: `Paper` + `PaperSystem.SetContent`, stamped by the trader. Shop: one itemised
  receipt per checkout, lines carry a quantity. Services: one per transaction.
- D12 Trader body: invulnerable and inert. `TraderComponent` MapInit: `EnsureComp<GodmodeComponent>`
  (via GodmodeSystem), RemComp Pullable, Strippable, Cuffable, Buckle, Ensnareable, Hunger, Thirst,
  SSDIndicator, Carriable, PseudoItem, InputMover, MobMover, (verify each exists); body set static.
  No mind, no ghost role, no HTN. Fixed look via `humanoidProfile` prototype +
  `HumanoidAppearance.initial`; clothes via `Loadout`.
- D13 Refuel: needs the deed ID in the zone; ship = `ShuttleDeedComponent.ShuttleUid`, must be
  docked to the grid the trader stands on. For every `FuelGeneratorComponent` on that grid:
  solid adapter → fill `MaterialStorage` to its limit; chemical adapter → fill solution to max.
  Unit price from the mirrored vendor's items (`TraderRefuelComponent.Fuels`: material/reagent →
  item prototype; unit price = item vend price / units the item holds). Trader quotes
  ("{$count} generators, {$cost}.") and offers Yes / Never mind; on Yes: payment, fill, receipt
  listing each generator line, "All done." Nothing to fill → "Your tanks are already full."

- D14 Shipyard dealer (`TraderShipyardComponent.Consoles`, e.g. `[ ComputerShipyard,
  ComputerShipyardExpedition, ComputerShipyardSr ]`). The trader *is* the console: on `BuyShip` the
  chosen console prototype's `ShipyardConsole`, `ShipyardListing`, `AccessReader` and
  `CompanyAccessReader` components are copied onto the trader (`ShipyardSystem.TryHostConsole`, in
  the `_WF` partial because those components are `Access`-restricted), the customer's ID goes into
  the console's real `ShipyardConsole-targetId` slot and the slot is locked, the conversation window
  is hidden and the console's own `ShipyardConsoleUiKey` BUI is opened for the customer. Every
  upstream purchase rule then runs unmodified, including `_station.GetOwningStation(trader)` (the
  trader stands on the station grid) and the radio announcements. The option says which console it
  wants through a new `TraderDialogueOption.Argument` (`argument:`), carried on `TraderActionEvent`.
  No `ActivatableUI` on the trader, so nobody can open the listing by clicking it; the Sell and
  Unassign buttons are refused server-side (`ShipyardConsoleActionAttemptEvent`) with
  `trader-shipyard-no-selling` and hidden client-side. A `ShipyardShuttlePurchaseEvent` that lands
  while a trader has a listing open prints a receipt. The listing closing, the customer leaving,
  the timeout or the trader dying all end the conversation, which hands every held item back
  (`TraderComponent.Held` + `TraderSystem.ReturnHeldItems`, force-removed from whatever container
  they are in) before the hosted components are stripped. Trader UI keys cannot be added at
  runtime, so `WFBaseTraderShipyard` in `base.yml` declares every `ShipyardConsoleUiKey`.
- D15 Used ship market. `UsedShipMarketComponent` on a station (Caelestinus Central) makes every
  ship sold anywhere on it get copied. One marked upstream hook in `ShipyardSystem.TrySellShuttle`
  raises `ShipSoldEvent(shuttle, console, station, appraisal)` after `CleanGrid` and before
  `QueueDel`, while the grid still carries its own `ShuttleDeed` and `Vessel` components.
  `UsedShipMarketSystem` strips the old owner's state (deed, ship ownership, linked lifecycle,
  station membership, company, in-flight FTL) and serialises the grid to YAML in memory
  (`MapLoaderSystem.TrySaveGrid` with `MissingEntityBehaviour.Ignore`, so the station's docks do not
  come with it). `SaleValue` is the post-sale-rate, pre-tax bill the console booked;
  `Price = ceil(SaleValue × (1 + UsedShipMarkup))`, available at `SoldAt + RelistDelay` (0.05 and
  5 min, data fields on `TraderUsedShipsComponent`). Listings live for the round only and every
  salesman calls out a new arrival (`trader-used-new-stock`). Buying: `UsedShips` opens
  `TraderUiKey.UsedShips`, the hull is loaded onto the shipyard map and FTL-docked exactly as
  `TryPurchaseShuttle` does (`TryAddSavedShip` + `TryDockLoadedShuttle`) *before* payment is taken,
  so a failed load costs nothing and no refund has to be handed back as cash; then `TryAssignDeed`
  with the recorded `VesselPrototype` (null-safe) and a name override, so a resold ship comes back
  in a freshly-bought state but keeps the name it was sold under. Selling: the salesman hosts its
  own `console:` prototype (default `ComputerShipyard`), quotes `CalculateShipResaleValue` and asks
  for confirmation, then drives the console's own `OnSellMessage` with the customer as the actor -
  docking, organics, `PreserveOnSale`, taxes and the bank deposit are all upstream's. Selling a ship
  is only possible through the salesman.

## Types (names are fixed — YAML depends on them)

Shared `_WF/Traders`:
- `TraderComponent` (networked, auto state): `ProtoId<TraderDialoguePrototype> Dialogue`;
  `float TableReachBonus = 1f`; `EntityUid? Table` (networked); `EntityUid? Customer`;
  `float MaxCustomerDistance = 3.5f`; `TimeSpan IdleTimeout = 60 s`; `TimeSpan ReplyDelay = 1 s`;
  `EntProtoId ReceiptPrototype = "Paper"`; `EntProtoId ZoneMarker = "WFTraderBarterZone"`;
  `string StampState = "paper_stamp-generic"`; `Color StampColor`.
- `TraderShopComponent`: `List<EntProtoId> Vendors`; `EntProtoId Parcel = "WFTraderParcel"`;
  consts `MaxPerLine = 20`, `MaxLines = 12`.
- `TraderParcelComponent`: `string ContainerId = "parcel-contents"`; `int Capacity = 8`;
  `SoundSpecifier OpenSound`.
- `TraderRefuelComponent`: `EntProtoId Vendor`; `List<TraderFuelEntry> Fuels`
  (`string? Material`, `string? Reagent`, `EntProtoId Item`); `float ServiceFee = 0f` (fraction).
- `TraderDialoguePrototype` (`traderDialogue`): `LocId Greeting`; `LocId Farewell`;
  `List<TraderDialogueOption> Options`.
- `TraderDialogueOption` (DataDefinition): `LocId Prompt`; `LocId Response`;
  `TraderAction Action = None`; `TraderRequirement Requires = None`.
- `enum TraderAction { None, OpenShop, Refuel, BuyShip, SellShip, UsedShips }`
- `enum TraderRequirement { None, Payment, Id, DeedId }` (Payment = any cash or any ID in zone).
- `enum TraderUiKey { Dialogue, Shop, UsedShips }`.
- `TraderShipyardComponent`: `List<TraderShipyardListing> Consoles` (`console: <EntProtoId>`).
- `TraderUsedShipsComponent`: `EntProtoId Console`; `float UsedShipMarkup`; `TimeSpan RelistDelay`.
Server `_WF/Shipyard`: `UsedShipMarketComponent` (station), `UsedShipMarketSystem` + `UsedShipListing`,
`ShipSoldEvent`, `ShipyardConsoleActionAttemptEvent`, `ShipyardSystem.WolfgateTrader` partial.
- Messages: `TraderDialogueSelectMessage(int Index)`, `TraderConfirmMessage(bool Accepted)`,
  `TraderShopCheckoutMessage(Dictionary<string, int> Items)`,
  `TraderShopCheckoutResultMessage(bool Success)` (server to client).
- States: `TraderDialogueState(string Line, List<string> Options, bool Confirming)`;
  `TraderShopState(List<TraderShopEntry> Entries, int CashInZone, string? IdName, int Balance)`;
  `TraderShopEntry(string Item, int Price)`.
- `InteractionRangeBonusEvent` (by-ref) in `Content.Shared/_WF/Interaction`.
- `IdCardOwnerComponent` in `Content.Shared/_WF/Access`.

Server `_WF/Traders`: `TraderSystem` (conversation, zone, payment, change, speech queue, receipts,
inert body), `TraderShopSystem`, `TraderParcelSystem`, `TraderRefuelSystem`,
`_WF/Access/IdCardOwnerSystem`.
Service systems listen for a server event `TraderActionEvent(Trader, Customer, Action)` raised by
`TraderSystem` after the reply line.

Client `_WF/Traders`: `TraderBoundUserInterface` (both keys or one each), `TraderDialogueWindow`
(SpriteView of the trader, name, current line, numbered option buttons), `TraderShopWindow`
(left: catalogue rows with sprite/name/unit price, clicking adds one; right: the basket with
− / + per line, running total, the zone/balance footer, Clear and Purchase). The basket is
client-side only.

## Stages
1. Core (Opus): everything above except D13. Builds Shared/Server/Client.
2. Data (Sonnet, parallel with 1, no builds): base trader prototype, fuel technician (profile, gear,
   dialogue, entity), zone marker + RSI, locale.
3. Refuel + tests + gate (Opus): D13, integration fixture, Release YAML lint, headless server start.
4. Ship traders (D14, D15): `WFTraderShipyard` (Corwin Ashgrove) and `WFTraderUsedShips`
   (Benny Sokolov). Done.
