# Desktop Handoff

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

The original game-design PDF was outside the project and is not in this repository.
Ask the user to attach it again if full design details are needed.

## Switching Computers

Save the Unity scene and assets, commit, and push before leaving a computer. Pull
on the other computer before editing. Do not edit the same scene simultaneously
on both machines. GitHub does not automatically upload uncommitted local changes.

Documentation: https://learn.chatgpt.com/docs/agent-configuration/agents-md
