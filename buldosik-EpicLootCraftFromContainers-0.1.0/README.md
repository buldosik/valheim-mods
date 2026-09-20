# EpicLootCraftFromContainers

A small compatibility bridge between **EpicLoot** and **CraftFromContainers**.

EpicLoot's Enchanting Table can count and consume materials from the same nearby containers that **CraftFromContainers** already supports.

## Features

- Adds CraftFromContainers storage support to the EpicLoot Enchanting Table.
- Uses CraftFromContainers for nearby-container discovery instead of maintaining a separate container list.
- Therefore works with any container that CraftFromContainers can expose through the normal container inventory path.
- Tested locally with vanilla containers and **RossItemDrawers**.
- Respects CraftFromContainers' enabled/prevent-key state and `LeaveOne` behavior.

## Tested with

- Valheim 1.0.14
- EpicLoot 0.14.8
- CraftFromContainers plugin 4.0.3 (UnderHeiz package)
- RossItemDrawers 1.0.10

## Installation

Install through Thunderstore/r2modman, or place `EpicLootCraftFromContainers.dll` in your `BepInEx/plugins` folder.

Both **EpicLoot** and **CraftFromContainers** are required.

## How it works

The bridge registers CraftFromContainers as an EpicLoot inventory provider. Container discovery is delegated to CraftFromContainers, so this mod does not implement its own list of supported chest/container types.

If CraftFromContainers can use a nearby container, EpicLoot should be able to use its materials through this bridge as well.

## Notes

- Local/single-player functionality has been tested.
- Simultaneous multiplayer access to the same container has not yet been extensively tested.
- If you find a container that works with CraftFromContainers but not with this bridge, please report it with a BepInEx log.

## AI disclosure

A significant portion of the initial compatibility bridge code and package documentation was created with AI assistance.
