# OfflineMod

Punish players who already **disconnected** from a CS2 server. Tracks recent leavers and exposes
`!offline` → a menu to **ban / mute / gag** them via AdminCommands' offline-by-SteamID path
(persists to the shared admin DB; enforced on their next connect everywhere).

- `!offline` — admin command (perm `@offlinemod/punish`), opens a MenuManager menu of the most
  recent disconnects on this server → pick player → pick punishment + duration.
- Reuses **AdminCommands** (apply), **AdminManager** (perm gate), **MenuManager** (UI). No DB of its own.

## Build / deploy
```
dotnet build OfflineMod.slnx -c Release
modsharp-deploy /path/to/OfflineMod <server-profile>
```
Grant `@offlinemod/punish` (collection `offlinemod`) to a mod role, or root (`*`) admins get it automatically.
