# AuraMove design

Target version: OttoAura 1.2.0. Sub-feature folder `AuraMove/`, namespace `OttoAura.AuraMove`.

## Premise

AuraPay already pays for healing and repair inside a ward. AuraMove sells one more service:
for a small fee the Merchant Guild picks a placed object up out of the world and sets it down a
few metres away, contents and state intact, instead of the player smashing it and building a new
one. The service exists only while AuraPay is on (`OttoPayBridge.IsAuraPayEnabled()`), the fee is
whole-or-nothing through `OttoPayBridge.TryWithdraw`, and the voice matches the rest of the mod:
short, plain, Merchant Bank flavoured, `MessageHud.MessageType.TopLeft`.

AuraMove is a client feature. It needs no server-side install, exactly like the aura. The only
network traffic is one routed RPC that carries the new position and rotation.

## UX flow

1. **Hover.** With no build tool out, the player looks at a placed object. A raycast from the
   camera (`Player.m_removeRayMask`, 50 m, hit inside `Player.m_maxPlaceDistance` of `m_eye`,
   `GetComponentInParent<Piece>()`) finds the piece. If the piece passes the eligibility rules a
   prompt is appended to the crosshair hover name: `The Guild will move this for N coins [key]`.
2. **Grab.** The AuraMove key (default keyboard `LeftAlt + V`, gamepad `JoyAltKeys` + `JoyButtonY`)
   starts the move. Nothing is charged yet. A placement ghost of the same prefab appears, the real
   object stays visible where it is, and a TopLeft message says the Guild has taken hold of it.
3. **Aim.** The ghost follows `Player.PieceRayTest` each frame, is rotated by the mouse wheel or the
   gamepad rotate buttons in `Player.m_placeRotationDegrees` steps, and is tinted invalid
   (`Piece.SetInvalidPlacementHeightlight`) wherever the game would refuse the placement or the
   destination is further than **Max Move Distance** from where the object stands now.
4. **Confirm.** The same key again. The fee is charged first. If the withdrawal fails, a message is
   shown and nothing moves. On success the RPC goes out.
5. **Shimmer.** On every client that has the object loaded, the object shrinks away at the old spot
   with a burst of VFX/SFX, is relocated, and grows back at the new spot with a second burst.
6. **Cancel.** `Escape` or gamepad B, or any interrupt (player dies, teleports, opens the build
   menu, walks out of range, the object is destroyed or unloaded). Nothing is charged on cancel.

AuraMove refuses all input while `Player.InPlaceMode()` is true. That single rule keeps it clear of
the vanilla hammer ghost, of `Hud.IsPieceSelectionVisible()`, and of OttoRedecorate's
`LeftControl + Mouse0`, which only ever fires with a hammer or Feaster in hand. Input is also
ignored while `Menu.IsVisible()`, `InventoryGui.IsVisible()`, `TextInput.IsVisible()`,
`Console.IsVisible()`, `Chat.instance.HasFocus()`, or the large map is open.

## Configuration

New section `5 - AuraMove`. Bound with the existing `config()` helper in `Plugin.Awake`, so every
entry is a ServerSync entry; the three input entries pass `synchronizedSetting: false` because a
key binding belongs to the client, the same split OttoRedecorate uses.

| Setting | Field | Type | Default | Range | Synced | Meaning |
| --- | --- | --- | --- | --- | --- | --- |
| Enabled | `AuraMoveEnabled` | Toggle | On | | yes | Turns the service off entirely. |
| Coins | `AuraMoveCoins` | int | 5 | 0-1000 | yes | Coins charged per completed move. 0 makes moving free, but AuraPay must still be on. |
| Max Move Distance | `AuraMoveMaxDistance` | float | 10 | 1-64 | yes | Metres the destination may sit from where the object stands now. |
| Support Is Immovable | `AuraMoveSupportImmovable` | Toggle | On | | yes | Nothing that carries load can move, except furniture. |
| Allowed Prefabs | `AuraMoveAllowedPrefabs` | string | `wood_fine_stack,blackwood_stack,bone_stack,piece_beehive` | | yes | Comma separated prefab names that skip every later rule. |
| Denied Prefabs | `AuraMoveDeniedPrefabs` | string | `fire_pit,bonfire,hearth,windmill` | | yes | Comma separated prefab names that can never move. |
| Shimmer Seconds | `AuraMoveShimmerSeconds` | float | 0.6 | 0-3 | yes | Length of the fade out and fade in together. 0 snaps and only plays the effects. |
| Effect Prefabs | `AuraMoveEffectPrefabs` | string | `vfx_Place_wood_pole,sfx_build_cultivator` | | yes | Fallback effect prefabs, used only when the moved piece has no place effect of its own. |
| Move Key | `AuraMoveKey` | KeyboardShortcut | `V + LeftAlt` | | no | Grab and confirm. |
| Gamepad Modifier | `AuraMoveGamepadModifier` | string | `JoyAltKeys` | | no | ZInput button held with the gamepad button. Empty means no modifier. |
| Gamepad Button | `AuraMoveGamepadButton` | string | `JoyButtonY` | | no | ZInput button that grabs and confirms. |

Defaults chosen against the vanilla binding table in `ZInput.Reset` (verified in
`assembly_utils_publicized.dll`): no vanilla action binds `V`, and `JoyAltKeys` is vanilla's own
alternate-layer modifier, so the pair only fires when the player asks for it.

## Eligibility rules

A direct port of `OttoRedecorate.Redecorate.CanMove`
(`/home/potto/src/valheim/mods/OttoRedecorate/OttoRedecorate/Redecorate.cs:408-525`), minus the
hammer/Feaster tool rules, which do not apply because AuraMove works with empty hands. Checks run
in order and the first match decides. Every deny path returns a `MoveDenial` value so the caller
can say why.

1. No local player, dead player, or `Player.InPlaceMode()` -> deny.
2. No `Piece`, no `m_nview`, `!m_nview.IsValid()`, or `!piece.IsPlacedByPlayer()` -> deny.
3. Prefab name (`Utils.GetPrefabName(piece.gameObject.name)`) in **Allowed Prefabs** -> allow, skip the rest.
4. Prefab name in **Denied Prefabs** -> deny.
5. `piece.m_category` is `BuildingWorkbench` (2) or `BuildingStonecutter` (3) -> deny. These are the
   load-bearing build pieces; `Furniture` is 4 and passes.
6. `!PrivateArea.CheckAccess(piece.transform.position, 0f, flashWard, false)` -> deny, warded ground.
7. `Vagon` -> deny (carts). `Ship` -> deny.
8. `piece.m_inCeilingOnly` -> allow, skip the rest.
9. `Plant` -> deny.
10. `Bed` whose `GetOwner()` is 0 or the local player id -> allow, skip the rest.
11. `PrivateArea` component with `IsEnabled()` -> deny (a live ward).
12. `Trap` with `IsArmed()` -> deny.
13. `ShieldGenerator` with `m_radius > 0f` -> deny.
14. `Aoe` in children with `m_useAttackSettings` and no `Fireplace` on the piece -> deny (stakes).
15. `Container`: `!CheckAccess(localPlayer.GetPlayerID())` -> deny; `IsInUse()` or
    `m_open && m_open.activeSelf` -> deny (someone has it open).
16. **Support Is Immovable** on, `m_category != Furniture`, `WearNTear.m_supports` -> deny.
17. Otherwise allow.

## Relocation mechanism

Mirrors OttoRedecorate's proven path, which was read directly rather than trusted from notes.

- The initiating client calls `piece.m_nview.ClaimOwnership()`, then
  `ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "OttoAura_MovePiece", zdo.m_uid, position, rotation)`.
- The handler on each client looks the ZDO up with `ZDOMan.instance.GetZDO(zdoID)`, drops out on a
  null or invalid ZDO, and if `zdo.IsOwner()` writes `SetRotation` then `SetPosition`. Only the new
  owner writes; everyone else just re-orients the instance they hold.
- `ZNetScene.instance.FindInstance(zdo)` gives the local `ZNetView`, or null when the object is not
  in a loaded zone. Null is normal, not an error: that client will build the object at the new
  place from the ZDO when the zone loads.
- `UpdateOrientation(GameObject, Vector3, Quaternion)` does the visible work:
  `transform.SetPositionAndRotation`, then the vanilla bookkeeping that a moved piece needs.
  - `WearNTear`: `OnPlaced()`, `ClearCachedSupport()`, `m_colliders = null`, and re-subscribe
    `ClearCachedSupport` to the heightmap at the new position, null-guarded on both sides because
    `Heightmap.FindHeightmap` returns null where no zone is loaded.
  - `EffectArea` children: remove and re-add the cached bounds in `EffectArea.s_BurningAreas`,
    `s_noMonsterAreas` and `s_noMonsterCloseToAreas` with the collider's new centre.
  - `Bed` that is the local player's active spawn point: re-set the custom spawn point.

**Rotation sync trap.** `ZDO.Serialize` (verified in the publicized assembly) only writes the
rotation when `m_rotation != Quaternion.identity.eulerAngles`, so a piece rotated back to exactly
identity never syncs its rotation and remote clients keep the stale one. OttoRedecorate patches
`ZDO.Serialize` with a transpiler to fix this. AuraMove does not patch anything: it nudges the
target yaw by 0.01 degrees whenever the euler angles round to exactly zero, which sets the flag
and is invisible in game. `MoveRelocation.AvoidIdentity` owns that rule.

**Placement ghost.** `Player.SetupPlacementGhost` cannot be reused: it reads
`m_buildPieces.GetSelectedPrefab()`, and so does one branch of `Player.UpdatePlacementGhost`, both
of which are null outside build mode. AuraMove builds and drives its own ghost, a trimmed port of
`SetupPlacementGhost` (verified against `Player.cs:3671-3824` in the decompiled game assembly), and
never touches `Player.m_placementGhost`. It reuses the parts of the game that are safe outside
build mode: `Player.PieceRayTest`, `Player.TestGhostClipping`, `Location.IsInsideNoBuildLocation`,
`PrivateArea.CheckAccess`, `Piece.SetInvalidPlacementHeightlight`.

## Effect design

Two layers, both driven from inside the RPC handler so every client that has the object loaded runs
them, not only the player who paid.

1. **Burst.** The moved piece's own `Piece.m_placeEffect.Create(position, rotation)` at the old spot
   and again at the new spot. That is the sound and the sparkle the game already ships for placing
   that exact piece, so nothing has to be guessed and nothing can be missing. Only if the piece has
   no enabled entry in `m_placeEffect` does the code fall back to **Effect Prefabs**, resolved
   through `ZNetScene.instance.m_namedPrefabs` by stable hash so a missing name warns once instead
   of spamming `GetPrefab`'s error. The two fallback defaults, `vfx_Place_wood_pole` and
   `sfx_build_cultivator`, are names already used against this game version by
   Advize_PlantEverything. Effect prefabs must be instantiated locally only; a prefab carrying a
   `ZNetView` would be spawned once per client, so any such prefab is skipped with a warning.
2. **Shimmer.** A uniform `transform.localScale` tween: the object shrinks to near zero over the
   first half of **Shimmer Seconds**, the relocation is applied at the midpoint, and it grows back
   over the second half. Scale was chosen over an alpha fade deliberately. Valheim's piece shaders
   are opaque and there is no reliable `_Color` alpha to drive; a scale tween needs no shader
   assumptions, reverts exactly, and reads as a dematerialise. Because it runs inside the RPC
   handler it is network-visible: remote clients see the shimmer too, not a snap. `localScale` is
   never serialised, so nothing about it can leak into the save.

Shimmer Seconds 0 skips the tween and applies the move immediately, still with both bursts. A
tween whose object is destroyed mid-flight aborts on the next frame, and `MoveEffects.StopAll`
restores every tracked original scale on shutdown.

## Multiplayer semantics

- The RPC is registered once per session, on `Game.Start`, and only when `!ZNet.instance.IsDedicated()`,
  because a dedicated server holds no instances to move. `ZRoutedRpc.Register` uses
  `Dictionary.Add`, so registration is guarded on `m_functions.ContainsKey(hash)` to survive a
  second `Game.Start` in one process.
- Clients without OttoAura ignore the unknown RPC name and pick the move up from the ZDO. They see
  the object jump rather than shimmer; state, contents and spawn points still follow, because the
  authority for the move is the ZDO, not the visual.
- The fee is charged once, on the initiating client, before the RPC is sent. `TryWithdraw` is
  whole-or-nothing, so a short balance moves nothing.
- Eligibility, ward access and container access are all evaluated for the local player only, which
  is the same trust model the rest of OttoAura and OttoRedecorate use.

## File layout

```
AuraMove/MoveEligibility.cs     MoveDenial enum, CanMove/Evaluate, denial text
AuraMove/MoveTargeting.cs       hover raycast, input, rotation input, crosshair prompt patch
AuraMove/MovePlacement.cs       ghost build/update/destroy, placement validity
AuraMove/MoveRelocation.cs      RPC register/send/handle, ZDO write, UpdateOrientation
AuraMove/MoveEffects.cs         bursts and the shimmer coroutine
AuraMove/AuraMoveController.cs  state machine, fee, player messages, Init/Tick/Shutdown
Plugin.cs                       config entries, using, Init/Tick/Shutdown wiring (integration only)
```

## Shared contract

All types are `internal static` in `namespace OttoAura.AuraMove` unless noted. Config entries live
on `OttoAuraPlugin` next to the AuraBoost entries, which is where every other OttoAura setting
lives; there is no settings shim. The `AuraMove/` files reference those fields before they exist,
so the project does not compile until the integration slice adds them.

```csharp
// Plugin.cs, ConfigOptions region (integration slice owns these)
internal static ConfigEntry<Toggle> AuraMoveEnabled = null!;
internal static ConfigEntry<int> AuraMoveCoins = null!;
internal static ConfigEntry<float> AuraMoveMaxDistance = null!;
internal static ConfigEntry<Toggle> AuraMoveSupportImmovable = null!;
internal static ConfigEntry<string> AuraMoveAllowedPrefabs = null!;
internal static ConfigEntry<string> AuraMoveDeniedPrefabs = null!;
internal static ConfigEntry<float> AuraMoveShimmerSeconds = null!;
internal static ConfigEntry<string> AuraMoveEffectPrefabs = null!;
internal static ConfigEntry<KeyboardShortcut> AuraMoveKey = null!;
internal static ConfigEntry<string> AuraMoveGamepadModifier = null!;
internal static ConfigEntry<string> AuraMoveGamepadButton = null!;

internal enum MoveDenial
{
    None, NoPlayer, NotPlacedByPlayer, DeniedPrefab, BuildingPiece, Warded, Vehicle,
    Plant, Ward, ArmedTrap, ShieldGenerator, Stake, PrivateContainer, ContainerInUse, Supporting,
}

internal static class MoveEligibility
{
    internal static bool CanMove(Piece? piece, bool flashWard = false);
    internal static MoveDenial Evaluate(Piece? piece, bool flashWard = false);
    internal static string DenialMessage(MoveDenial denial);
    internal static bool IsListed(string csv, string prefabName);
}

internal static class MoveTargeting
{
    internal static Piece? Hovered { get; }
    internal static bool InputBlocked { get; }
    internal static string KeyLabel { get; }
    internal static void UpdateHover();
    internal static void ClearHover();
    internal static bool ActivatePressed();
    internal static bool CancelPressed();
    internal static int ConsumeRotationSteps();
}

internal static class MovePlacement
{
    internal static bool Active { get; }
    internal static bool IsValid { get; }
    internal static Vector3 Position { get; }
    internal static Quaternion Rotation { get; }
    internal static bool Begin(Piece piece);
    internal static void UpdateGhost(Piece piece);
    internal static void Cancel();
}

internal static class MoveRelocation
{
    internal const string RpcName = "OttoAura_MovePiece";
    internal static void Register();
    internal static bool Request(Piece piece, Vector3 position, Quaternion rotation);
    internal static void UpdateOrientation(GameObject movedObject, Vector3 position, Quaternion rotation);
    internal static Quaternion AvoidIdentity(Quaternion rotation);
}

internal static class MoveEffects
{
    internal static void PlayRelocation(GameObject movedObject, Vector3 fromPosition,
        Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, System.Action apply);
    internal static void StopAll();
}

internal static class AuraMoveController
{
    internal static bool IsAvailable { get; }
    internal static bool IsMoving { get; }
    internal static Piece? Selected { get; }
    internal static void Init();
    internal static void Tick();
    internal static void Shutdown();
}
```

`AuraMoveController.IsAvailable` is
`OttoAuraPlugin.AuraMoveEnabled.Value == OttoAuraPlugin.Toggle.On && OttoPayBridge.IsAuraPayEnabled()`,
re-read every frame because the server can switch the setting and the player can switch AuraPay at
any moment, exactly as `AuraBoostEffect.IsActive` does.
