# EpicLoot CraftFromContainers Bridge

Target tested against the DLLs supplied in chat:

- UnderHeiz CraftFromContainers **4.0.3**
- RandyKnapp EpicLoot **0.14.8**

## Design

EpicLoot 0.14.8 exposes a public inventory-provider API:

`RegisterInventoryProvider(string, Func<List<ItemData>>, Func<string,int>, Func<string,int,int>, Func<ItemData,int,int>)`

This bridge registers **CraftFromContainers itself** as that provider.

Container discovery is deliberately delegated to:

`CraftFromContainers.BepInExPlugin.GetNearbyContainers(...)`

So the bridge does not know or care whether a container is vanilla, a cart, a ship, RossItemDrawers, or another custom container. If CraftFromContainers includes it and `Container.GetInventory()` exposes it, EpicLoot can use it.

The bridge also mirrors CFC's `modEnabled`, activation/prevent key state, and `LeaveOne` behavior.

## Build

1. Put the exact `CraftFromContainers.dll` 4.0.3 in `lib/CraftFromContainers.dll`.
2. Set environment variable `VALHEIM_DIR` to your Valheim folder.
3. Run:

   `dotnet build -c Release`

4. Copy `bin/Release/net48/EpicLootCraftFromContainers.dll` into `BepInEx/plugins`.

The bridge directly uses CFC 4.0.3's public `modEnabled`, `leaveOne`, and `AllowByKey()` API. EpicLoot registration remains reflection-based: the installed EpicLoot 0.14.8 targets .NET Framework 4.8.1, while this bridge targets 4.8, and a direct reference fails framework compatibility validation. Reflection also remains for the private Valheim `Container.Save()` method.

See [REVIEW.md](REVIEW.md) for the settings audit and known correctness, multiplayer, and compatibility risks.

## Expected log

On startup:

`Registered CraftFromContainers as an EpicLoot enchanting material provider.`

Then the EpicLoot enchanting table should count and consume materials from the same nearby containers that CFC uses.
