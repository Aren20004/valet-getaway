# Valet Getaway

Unity prototype with third-person movement, driving, buyer discovery and car sales,
hotel valet guests, bus return points, and Kenney city assets.

Moving from the laptop? Start with **[HANDOFF.md](HANDOFF.md)**. It includes a
desktop-chat prompt, setup instructions, and the unfinished validation checklist.
The included **Open Unity.cmd** (Windows) and **Open Unity.command** (macOS)
launch this checkout with the correct editor and main scene once Unity is installed.

## Open on another computer

1. Clone this repository using GitHub Desktop or Git.
2. Install Unity Hub and Unity Editor **6000.6.3f1** (the version recorded in
   `ProjectSettings/ProjectVersion.txt`).
3. In Unity Hub, choose Add project from disk and select this repository's folder.
4. Open the project and allow Unity to restore packages and import assets. The first
   import can take several minutes because generated caches are not stored in Git.
5. Open `Assets/Scenes/ValetTestCity.unity`.

## Prototype Controls

- WASD: walk or drive; Shift: run; mouse: camera.
- E: talk to guests, enter/exit a car, sell to a buyer, or use a bus stop.
- Space: brake while driving.
- Buyers must first be seen to reveal their directions. Stop the car near the buyer,
  exit, and speak to them to sell it.

## Current Status

The initial Kenney city layout is saved. The latest prefab-visibility corrections
and full hotel gameplay validation still need to be run in a responsive editor.
The laptop editor repeatedly stalled during importing and startup; the project is
not being presented as fully play-tested.

After opening the scene outside Play mode, use **Valet Getaway > Apply Kenney Roads
and Cars** to finish the saved model replacements, then **Valet Getaway > Validate
Hotel Gameplay** to run the automated prototype check. This check enters Play mode,
exercises interactions, reports its result in the Console, and exits Play mode.

Scene backups are in `Assets/Scenes/ValetTestCity_BeforeHotel.unity` and
`Assets/Scenes/ValetTestCity_BeforeKenney.unity`.

## Project Files

- `Assets/ValetGetaway`: gameplay scripts, prefabs, and prototype materials.
- `Assets/Kenney`: imported City Kit (Roads), Car Kit, and City Kit (Commercial).
- `SourceAssets/Kenney`: original downloaded asset archives.
- `Packages` and `ProjectSettings`: dependencies and shared Unity configuration.

Keep Unity `.meta` files with their corresponding assets when committing changes.
`Library`, `Temp`, `Logs`, and other machine-specific generated files are excluded.

## Asset Licenses

Kenney packs are CC0. Each imported pack includes its original `License.txt`.
See https://kenney.nl/assets for the asset catalogue.
