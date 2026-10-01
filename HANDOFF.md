# Desktop Handoff

## Playable Full Island Update - October 1, 2026

The saved scene `Assets/Scenes/ValetIslandCity.unity` now contains the playable
valet setup as well as the expanded island layout. The gameplay root uses the
existing player, car, buyer, guest, HUD, hotel, and bus mechanics on this scene.

- Added routed hotel arrivals on the hotel island, city-wide buyer locations, a
  baked island navigation surface, five island bus stops, and water/stuck-car
  recovery (`R` while driving).
- Expanded the city toward the supplied full-island reference with outer roads,
  larger buildings, wooded hills, ponds/streams, a southern marina, and additional
  residential/warehouse districts. The layout remains an editable saved scene.
- The lower city road now ends in a literal jump ramp over the water.
- The hotel arrival road is a one-way loop. The bridge connection splits into a
  dedicated entry and exit branch with a divider and one-way arrows.
- Added the supplied full-island image to `SourceAssets/Design/FullIslandReference.png`.

Validation status: the scene build and gameplay setup passed. Automated playtesting
confirmed physical bridge driving and jump-ramp launch. The remaining automated
loop test has a stale phase-state failure after the ramp check, so full buyer-sale-
bus replacement validation is still pending manual play in the Unity Game view.
Do not describe the complete gameplay loop as fully validated until that manual
check or a corrected harness passes.

## Road, Bridge, and Building Fixes - October 1, 2026

Last night's work was committed and pushed first as `1443d4d` on `main` at
https://github.com/Aren20004/valet-getaway. This project folder now has the repository's
Git history and origin configured; future commits can be made directly here.

The updated scene is still **Assets/Scenes/ValetIslandCity.unity**. Open it from
**Valet Getaway > Open Island City** in Unity Editor. The original ValetTestCity
gameplay scene remains separate; the island map is still a visual layout.

- Trimmed sidewalk and asphalt meshes at road intersections, forks, and the hotel
  loop's self-intersection. Overlapping surfaces have a single owner while retaining
  the imported Broken Vector road mesh and texture atlas.
- Corrected the bridge to span its actual local X axis with uniform scaling. Its
  original deck is the driving surface; the duplicate carriageway was removed.
- Replaced roof/window building details with plain colored boxes: sage houses and
  apartments, blue shops, cream offices, and a sandstone hotel. Each building has
  roof and front/back labels. The label shader respects depth and backface culling.
- Added IslandRoadJunctions.cs, BuildingLabel.cs, and BuildingLabel.shader. All
  geometry remains saved, editable scene objects; label materials bind the font
  atlas when scenes load in the editor or at runtime.
- Builder backups now include the generated meshes/materials as well as the scene.
  The pre-fix local backup is Logs/SceneBackups/IslandCity_20261001_before_fixes.

Fresh Unity 6000.6.3f1 build and saved-scene checks passed in the validation copy:
393 persistent meshes, zero missing materials before save, 279 building labels,
4,098 clear driving-lane raycasts, and 180 bridge deck/vehicle-clearance checks.
Rendered previews after reopening the saved scene were inspected, including hotel,
bridge, intersections, and the city overview. The copied scene's SHA-256 matches the
validated file. Results and previews are under Logs/IslandValidation.txt and
Logs/Previews. No manual driving test or gameplay migration was performed.

Lane markings at junctions remain a visual polish opportunity. Any future gameplay
migration must address hard-coded hotel/guest/validation coordinates first.

## Island City Layout - September 30, 2026

User requested a large city island, a bridge to a hotel island, and street shapes based on SourceAssets/Design/IslandCityReference.png, using Broken Vector Low Poly Road Pack (asset 67288). The user imported the pack into Assets/BrokenVector/LowPolyRoadPack.

Open the saved layout with **Valet Getaway > Open Island City**, or open Assets/Scenes/ValetIslandCity.unity. All scene geometry is saved and editable in the Hierarchy. This is a visual layout scene; player/guest/buyer/bus gameplay remains in ValetTestCity and has NOT been migrated into this larger map.

Added:
- Two finite islands and surrounding water, with sand shores.
- 21 named road routes using adapted meshes from the requested road pack. Street centerlines were traced from the supplied screenshot; the pack's actual demo scene has a different layout. This is a reference-based reconstruction, not a pixel-exact copy.
- The pack's truss bridge with a continuous collidable carriageway between islands.
- 90 district-coordinated city buildings/houses, including pitched roofs along curved streets.
- Hotel building, entrance canopy, valet bays, arrival loop, fountain, landscaping, and parking on the opposite island. This side extends beyond what is visible in the cropped reference.
- URP materials in Assets/ValetGetaway/IslandCity/Materials. Original pack materials/assets remain intact.
- Editor builder: Assets/ValetGetaway/Scripts/Editor/IslandCityBuilder.cs. Open Island City is for viewing; Build Reference Island City regenerates the layout and geometry. Back up manual changes before regenerating.

Verified using Unity 6000.6.3f1 in a separate build workspace:
- Fresh compilation and saved-scene reopening succeeded.
- 1,073 persistent mesh renderers; zero missing materials.
- Raycasts confirmed a continuous bridge carriageway collider.
- Overhead and hotel previews were rendered and inspected. No manual driving test or full intersection collision audit has been performed. Junction lane markings remain a polish item.
- Copied scene hash matches the verified scene; the original ValetTestCity file hash is unchanged.

Original scene backup: Logs/SceneBackups/ValetTestCity_BeforeIslandLayout.unity. Temporary survey script was removed, and its request was consumed. No auto-build requests remain.

Next: review the layout with the user, polish intersections/terrain/building detail, then migrate the hotel gameplay into this scene with relative coordinates and city-wide buyer/bus navigation. Do not simply move the old gameplay root: HotelGuestCar and validation currently use hard-coded coordinates.

## Desktop Update - September 30, 2026

Implemented the design proposal's vehicle damage/value system:
- Impacts above 3 m/s into a surface reduce condition; gentle bumps and upward-facing road contacts do not.
- Condition loss is 4.5 percentage points per m/s above that threshold, with a 0.35-second contact cooldown.
- Sale value scales from 100% at pristine condition to a 20% salvage floor. These are initial tuning choices, adjustable on VehicleInteraction in the Inspector.
- HUD shows condition, current sale offer, and driving speed. Crash notifications show value lost; sale messages show final condition.
- Guest arrival remains protected. Damage persists after exiting/re-entering a claimed car. New guest handovers and prototype resets restore condition.
- Existing buyer discovery, stopped-car, nearby-car, and on-foot conversation gates are retained.

Verified in Unity 6000.6.3f1 on this desktop:
- Fresh script compilation succeeded; no C# compile errors found in the run log.
- `VALET DAMAGE VALIDATION PASSED`: gentle/invalid impacts, depreciation, contact cooldown, salvage floor, replacement reset.
- `VALET HOTEL VALIDATION PASSED`: arrival, keys, driving, discovery, sale gates, damaged-car payout, duplicate prevention, bus return, replacement arrival and restored condition.
- Tests are available under Valet Getaway > Validate Vehicle Damage and Validate Hotel Gameplay.
- Evidence was in Logs/Editor.log; logs are ignored and may rotate. A compact validation record is saved in this chat's outputs.

Pending: manually drive into walls at different speeds, inspect HUD at desktop and narrow WebGL resolutions, and tune damage economics. The automated damage checks exercise impact/value logic; physical contact feel and HUD appearance are not visually verified. Kenney visual cleanup remains pending; no scene rebuild or scene save was performed for this change. Debt/timer/win/lose states, traffic and police are still future design work.

The original scene file was preserved. A temporary desktop test runner was removed after validation and its request was consumed. The design PDF is now in SourceAssets/Design. This Downloads folder appears to be an extracted project without a .git directory: these changes are local, not pushed to GitHub. Unity remains open with ValetTestCity after the tests.
## Start Here

This repository contains the project files and this handoff, not a copy of the
original chat. A desktop chat must be given access to the locally cloned folder.
The desktop cannot open the old laptop path just by opening the conversation.

1. Install GitHub Desktop, Unity Hub, and Unity Editor **6000.6.3f1**.
2. Clone this private repository while signed into the owning GitHub account.
   Prefer a local folder such as `C:\Projects\ValetGetaway` on Windows or
   `~/Projects/ValetGetaway` on macOS, outside OneDrive/iCloud synced folders.
3. Add the cloned folder as a local project in the desktop coding/chat app.
4. Start a chat with that project selected and send:

   > Read AGENTS.md and HANDOFF.md. This is my Valet Getaway Unity project moved
   > from my laptop. Find Unity 6000.6.3f1 on this desktop and open this local
   > project and Assets/Scenes/ValetTestCity.unity. Check for import or compile
   > errors, then help finish the pending city visuals and hotel gameplay tests.

5. Alternatively, double-click `Open Unity.cmd` on Windows or `Open Unity.command`
   on macOS. They find the default Unity Hub editor installation. A custom editor
   executable can be provided through the `UNITY_EDITOR_PATH` environment variable.
   If launching is blocked by local security policy, add/open the folder in Unity
   Hub instead; no system security changes are required.

Unity still needs its normal Hub sign-in/license setup. Allow the initial asset
import to complete. No Unity installation or account credentials are in Git.

## Implemented, But Not Fully Revalidated

- Third-person player movement, driving, follow camera, and enter/exit interaction.
- Random walking buyers, first-sighting discovery, persistent directions afterward,
  and a nearby stopped-car/on-foot sale requirement.
- Hotel, valet guests arriving by car, key handover, five bus return points, money
  display, sale payouts, and guest replacement logic.
- Saved 160-by-160 test city with 81 Kenney road tiles, three Kenney guest cars,
  and sixteen commercial buildings. Original packs are in `SourceAssets/Kenney`.
- Building prefabs are under `Assets/ValetGetaway/Prefabs/Buildings`.

## Resume Checks

1. Open `Assets/Scenes/ValetTestCity.unity` outside Play mode. Preserve any new work.
2. Run **Valet Getaway > Apply Kenney Roads and Cars**. Despite the menu name,
   `KenneySceneSetup` also handles commercial buildings. On the existing city it
   records the original building renderers as hidden prefab overrides, adjusts
   collision bounds, reduces oversized signs, saves, and captures previews.
3. Confirm that old cubes no longer obscure the buildings, road tiles line up,
   car models face forward, and the hotel entrance/valet bays remain usable.
   Previews are saved to `Logs/Previews` (not tracked).
4. Run **Valet Getaway > Validate Hotel Gameplay**. It enters/exits Play mode and
   should log `VALET HOTEL VALIDATION PASSED`. It checks arrival, key handover,
   driving, buyer discovery, sale distance/speed gates, payment, duplicate
   prevention, bus teleport, and replacement guest arrival.
5. Earlier tests failed/timed out. A replacement-car approach lane fix was added
   to `HotelGuestCar.cs`, and the test now enables background running temporarily.
   Neither change has a confirmed full successful run. Diagnose actual failures
   rather than assuming the laptop alone caused them.
6. Manually play the loop and check the enlarged HUD at the desktop's resolution.

No automatic scene-setup request should be needed on a fresh clone. Request files
used on the laptop lived under ignored `Library`/`Temp` folders and were canceled
when work paused. Use the editor menu commands deliberately.

## Important Files

- `HotelGameSession.cs`: E-key interaction routing, money, bus travel, guest resets.
- `HotelGuestCar.cs`: arriving/waiting/handed-over/sold guest-car states.
- `BuyerDeliveryManager.cs` and `BuyerWander.cs`: discovery, wandering, sale gates.
- `VehicleInteraction.cs`: entry, exit, delivery integration.
- `PrototypeHud.cs`: money/objective/prompts and destination overlay.
- `Editor/KenneySceneSetup.cs`: city model placement and preview capture.
- `Editor/HotelGameplayValidation.cs`: automated hotel-loop checks.

Scene backups: `ValetTestCity_BeforeHotel.unity` and `ValetTestCity_BeforeKenney.unity`
under `Assets/Scenes`. Do not replace the main scene with a backup without checking
what would be lost. Money currently lasts only for the current play session.

The game-design PDF is now included at SourceAssets/Design/Valet Getaway - Game Design Proposal.pdf.

## Switching Computers

Save the Unity scene and assets, commit, and push before leaving a computer. Pull
on the other computer before editing. Do not edit the same scene simultaneously
on both machines. GitHub does not automatically upload uncommitted local changes.

Documentation: https://learn.chatgpt.com/docs/agent-configuration/agents-md

