# Review notes

## Scope

This is a client-side integration for XPortalNetworks 2.0.8. It late-binds to the internal `XPortalNetworks.KnownPortalsManager` and `KnownPortal` types so the project does not ship or directly reference the XPortalNetworks DLL.

## Known risks

- The dependency does not expose a public portal-list API. A future XPortalNetworks release that renames the internal manager, `GetList`, or portal properties will disable pin updates until the reflection adapter is updated.
- Map pins are local to each client. This is intentional: installing the same mod for all players produces the same view without writing shared map data.
- Private/personal portals are filtered by network owner and privacy flag by default. Enabling `IncludePrivatePortals` can reveal personal portal locations to the local player.
- No in-game multiplayer test has been performed in this workspace. Portal list synchronization and private filtering should be tested with two clients.
- Portal visibility is controlled by the client-only `ShowPortalPins` setting; no custom row is added to the vanilla map legend.

## Validation

Built against the local Valheim installation with `dotnet build -c Release`; the project currently compiles with zero errors after restore.
