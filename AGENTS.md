# Valet Getaway

This is a Unity game project. Read `HANDOFF.md` when resuming work on another
computer; it records the last verified state and unfinished work.

- Use the Unity version in `ProjectSettings/ProjectVersion.txt` (6000.6.3f1).
- Main scene: `Assets/Scenes/ValetTestCity.unity`.
- Gameplay scripts: `Assets/ValetGetaway/Scripts`; editor tools are in its `Editor` folder.
- The user wants editable, saved scene objects in the Hierarchy, not a city created
  only at runtime. Preserve their scene work and take a backup before rebuilding.
- Keep `.meta` files with assets. Do not commit Unity caches, logs, local credentials,
  or machine-specific paths. Do not upgrade Unity/packages just to open the project.
- Existing gameplay assets use Kenney City Kit (Roads), Car Kit, and City Kit (Commercial).
  The user-requested island city uses Broken Vector Low Poly Road Pack at
  Assets/BrokenVector/LowPolyRoadPack. Preserve all asset licenses.
- New visual layout: Assets/Scenes/ValetIslandCity.unity. It is not yet a gameplay
  replacement for ValetTestCity; see the latest HANDOFF.md entry.
- Island buildings should remain plain district-colored boxes with purpose labels;
  do not add building textures, windows, or detailed roofs without a new request.
- IslandRoadJunctions trims intersecting sidewalks. The imported bridge spans local
  X, not local Z; preserve its proportions and do not add a duplicate road deck.
- Buyers wander, remain undisclosed until seen, and then receive directions. A sale
  requires a stopped car nearby and an on-foot conversation; retain these conditions.
- Do not claim gameplay validation passed without a fresh Console result. The
  laptop stalled repeatedly; the latest visual fixes and hotel test remain pending.
- Explain progress in plain language. Do not start more game implementation when
  the user is only asking to open, transfer, or inspect the project.

To open Unity on the desktop, prefer the included `Open Unity.cmd` (Windows) or
`Open Unity.command` (macOS), after checking the required editor is installed.
These launchers open the local checkout, never the old laptop directory.
