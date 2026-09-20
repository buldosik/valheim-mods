# XPortal Shared Map Pins

Standalone companion mod for Vapok XPortalNetworks 2.0.8.

It reads the portal list maintained by XPortalNetworks and creates matching `Portal` pins on each player's local map. The pins are refreshed automatically, and pins for renamed, moved, or destroyed portals are removed.

## Behavior

- Public/global XPortalNetworks portals are pinned automatically.
- Private and personal-network portals are excluded by default.
- Set `IncludePrivatePortals = true` in the generated config to include them locally.
- Pins are client-local map data. Install this mod for every player who should see the pins; no world or ZDO data is changed.
- The mod does not alter portal routing, ownership, permissions, or XPortalNetworks settings.
- `Actions/RestoreXPortalPins`: set to `true` to recreate only XPortal-generated portal pins, then it resets. Vanilla player pins are not touched.
- `General/PinColor`: a `Color` setting with a Configuration Manager color picker.
- `General/ShowPortalPins`: toggle all portal pins from the configuration. Disabled mode removes only this mod's pins.
- `General/ShowNetworkInPinName`: when enabled, map pins use a name such as `[Trade Hub] North Base`; disabled by default.
- Portal pins use a separate internal category with the native portal icon when available. Visibility is controlled through configuration, not the map legend.

## Build

From this directory:

```powershell
.\build.ps1 -ValheimDir 'D:\Steam\steamapps\common\Valheim'
```

Copy `dist/XPortalSharedMapPins.dll` into `BepInEx/plugins` alongside XPortalNetworks.

## Requirements

- Valheim 1.0.15 references
- BepInEx 5.4.2350
- XPortalNetworks 2.0.8
