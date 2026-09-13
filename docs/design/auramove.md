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

AuraMove is a build piece, not a hotkey mode. The service lives in the hammer as a build category
of its own, "Merchant Guild", holding one pseudo-piece, "Guild Move". Selecting it puts the player
in the game's own placement mode, so aiming, rotation, snapping, ward checks and the red invalid
tint are all vanilla behaviour rather than a private ghost.

1. **Open.** Take the hammer out and pick Guild Move, either from the Merchant Guild tab in the
   build menu or with the AuraMove key (default `LeftAlt + M`), which takes the hammer out, selects
   Guild Move, and puts the hammer away again when Guild Move is already selected. The tab and the
   piece only exist while `AuraMoveController.IsAvailable`; with the feature off, or AuraPay off,
   the category is removed from the hammer's piece table entirely.
2. **Hover.** With Guild Move selected the ghost is the pseudo-piece, which has no renderers, so
   the placement marker is all the player sees. `Player.GetHoveringPiece` supplies what the player
   is pointing at, and if it passes the eligibility rules the crosshair says
   `Left click and the Guild will move this for N coins`.
3. **Grab.** Left click. Nothing is charged yet. The source object stays where it is, and
   `MoveCarry` remembers it, so `PieceTable.GetSelectedPrefab` starts answering with the source
   object's own prefab and `Player.SetupPlacementGhost` builds a real ghost of it.
4. **Aim.** `Player.UpdatePlacementGhost` drives everything from here: the ghost follows the ray,
   the mouse wheel and the gamepad rotate buttons turn it, and it goes red wherever the game would
   refuse the placement. AuraMove adds one rule of its own in a postfix, **Max Move Distance** from
   where the object stands now, which sets `m_placementStatus` to `Invalid` and tints the ghost.
5. **Confirm.** Left click again. `Player.TryPlacePiece` is intercepted: nothing is instantiated,
   the fee is charged first, and on success the RPC goes out. A destination the game refuses gets
   vanilla's own message; a destination that is only too far gets the Guild's.
6. **Shimmer.** On every client that has the object loaded, the object shrinks away at the old
   spot as that spot flares, a wisp carries it across, and it grows back at the new spot over a
   spirit summon and a chime.
7. **Cancel.** Right click, which the Guild reads as "let go" instead of the hammer's remove, so a
   cancel can never smash what the player is pointing at. The carry is also dropped by choosing a
   different piece or category, putting the hammer away, dying, teleporting, logging out, walking
   out of range, switching AuraPay or AuraMove off, and the source object being destroyed or
   unloaded. Nothing is charged on any of these.

Vanilla's own input gates do the work that the old `InPlaceMode` refusal used to do: with the
hammer out there is no crosshair-mode conflict left to avoid. The AuraMove key alone is still
ignored while `Menu.IsVisible()`, `InventoryGui.IsVisible()`, `TextInput.IsVisible()`,
`Console.IsVisible()`, `Chat.instance.HasFocus()`, the build menu is open, or the large map is up.

## Hammer integration

The pieces of vanilla this rests on, and why each one is touched.

**A category past `Piece.PieceCategory.Max`.** `Max` is 9 and `All` is 100, so 10 is the first free
value, and it is the value Jotunn hands its first custom category, which keeps the two lined up
rather than fighting. `Max` itself cannot be used: `PieceTable.GetSelectedCategory` treats both
`Max` and `All` as "nothing selected" and resets the selection, so a piece parked there could never
stay selected. Vanilla sizes `m_availablePiecesByCategory` to 9 lists and both selection arrays to
9 entries, so `GuildMove.EnsureCategoryLists` grows all three to 11 before anything indexes them.

**The piece is not in `PieceTable.m_pieces`.** Vanilla only offers a piece from `m_pieces` once its
name is in the player's known-recipe list, and that list is saved into the character file. Adding
the pseudo-piece to `m_availablePieces`, `m_enabledPieces` and the category list in a postfix on
`PieceTable.UpdateAvailable` keeps the service out of the save entirely and makes availability a
pure function of the config and of AuraPay: the same postfix takes the piece and the category away
again the moment either is switched off, and `AuraMoveController.Tick` calls
`Player.UpdateAvailablePiecesList` on the frame availability changes so the tab appears and
disappears without waiting for an equipment change.

**The tab.** `Hud.UpdateBuild` shows tab `i` for category `i`, so a table with more categories than
the HUD has tabs never draws the last one. `GuildMove.EnsureTabs` clones tab 0 when that happens,
re-attaches `Hud.OnLeftClickCategory` (an `Action` field, which `Instantiate` does not copy), and
positions the clone itself only where the tab parent has no layout group. A HUD with no category
tabs at all is left alone: the modern build menu lists `PieceTable.m_availablePieces` and ignores
categories, so the piece is still reachable there through its `Misc` usage tag.

**The label.** `m_categoryLabels` entries go through `Localization.Localize`, which leaves a string
without a `$` token untouched, so "Merchant Guild" needs no localization registration.

**The description.** `Hud.SetupPieceInfo` reads `Piece.m_description` every frame, so
`GuildMove.RefreshDescription` rewrites it whenever the fee, the distance or the carried object
changes. That is how the panel names the live fee and what the Guild is holding.

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
| Shimmer Seconds | `AuraMoveShimmerSeconds` | float | 1.2 | 0-3 | yes | Length of the shrink and the grow together. 0 snaps and only plays the stage effects. |
| Grab Effects | `AuraMoveGrabEffects` | string | `fx_summon_start,sfx_staffspiritcaller_cast` | | yes | Played for the grabbing client alone, at the source object's feet. |
| Depart Effects | `AuraMoveDepartEffects` | string | `vfx_Potion_eitr_minor,sfx_OpenPortal` | | yes | Played on every client at the old spot as the object starts to shrink. |
| Travel Effect | `AuraMoveTravelEffect` | string | `vfx_pick_wisp` | | yes | One prefab, flown along an arc from the old spot to the new one. Blank flies nothing. |
| Arrive Effects | `AuraMoveArriveEffects` | string | `fx_summon_spirit_spawn,sfx_runestone_activate` | | yes | Played on every client at the new spot as the object grows back in. |
| Finish Effects | `AuraMoveFinishEffects` | string | `sfx_dverger_heal_finish` | | yes | Played on every client at the new spot once the object is whole again. |
| Move Key | `AuraMoveKey` | KeyboardShortcut | `M + LeftAlt` | | no | Takes the hammer out with Guild Move selected, and puts it away again. |

`Gamepad Modifier` and `Gamepad Button` were dropped in the hammer rework. The grab and the
confirm are the build tool's own place button now, which is already bound on both devices, and the
build menu's Merchant Guild tab is the gamepad route to the piece, so a second binding of our own
would only be a way to get the two out of step.

Defaults chosen against the vanilla binding table in `ZInput.Reset` (verified in
`assembly_utils_publicized.dll`): `M` is vanilla's Map button (ZInput line ~2997:
`AddButton("Map", KeyToPath(Key.M), altKey: false, ...)`). `Minimap.Update` calls
`SetMapMode(Large)` on `ZInput.GetButtonDown("Map")` regardless of whether Alt is held, so
`Alt+M` would open the large map as a side effect of grabbing a piece. A Harmony prefix on
`Minimap.SetMapMode(Minimap.MapMode mode)` skips the `Large` transition for exactly the frame
that `MoveTargeting.KeyboardShortcutDown` is true (shortcut down, AuraMove available, input not
blocked). `Small` and `None` are never blocked, so the map can always close. The shortcut is the only key AuraMove reads directly; everything else comes through the build
tool.

## Eligibility rules

A direct port of `OttoRedecorate.Redecorate.CanMove`
(`/home/potto/src/valheim/mods/OttoRedecorate/OttoRedecorate/Redecorate.cs:408-525`), minus the
hammer/Feaster tool rules, which the Guild Move piece replaces. Checks run
in order and the first match decides. Every deny path returns a `MoveDenial` value so the caller
can say why.

1. No local player or dead player -> deny. Build mode is no longer a bar: every check now runs
   with the hammer out.
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

**Placement ghost.** There is no private ghost any more. While the Guild has hold of something,
`PieceTable.GetSelectedPrefab` answers with the source object's own prefab, so
`Player.SetupPlacementGhost` and `Player.UpdatePlacementGhost` build and drive the ghost with all
of the game's own rules, and `Player.m_placementGhost` is the one the whole HUD already knows
about. The swap is guarded on the vanilla result being the Guild Move pseudo-piece, so no other
piece table and no other selection can see it. `MovePlacement.cs`, the hand-rolled port of
`SetupPlacementGhost`, is gone with it.

## Effect design

The move should read as a conjuring rather than a teleport, and it is built entirely from effect
prefabs the game already ships, so the mod carries no assets. Five stages, each one a
comma-separated config entry of prefab names, so the whole choreography is overridable without a
rebuild. Only the first is local; the other four run from inside the RPC handler, so every client
that has the object loaded sees the same thing, not only the player who paid.

| Stage | When | Scope | Default |
| --- | --- | --- | --- |
| Grab | the player takes hold | local | `fx_summon_start`, `sfx_staffspiritcaller_cast` |
| Depart | the object starts to shrink, at the old spot | every client | `vfx_Potion_eitr_minor`, `sfx_OpenPortal` |
| Travel | across the shrink, arriving as the object reappears | every client | `vfx_pick_wisp` |
| Arrive | the relocation is applied, at the new spot | every client | `fx_summon_spirit_spawn`, `sfx_runestone_activate` |
| Finish | the object is whole again, at the new spot | every client | `sfx_dverger_heal_finish` |

Why each default: `fx_summon_start` is the summoning circle that opens a spirit summon, which is
exactly the "the Guild is taking hold of this" beat, and `sfx_staffspiritcaller_cast` is its soft
cast. `vfx_Potion_eitr_minor` is the eitr-coloured flash the game uses for magic being spent, over
`sfx_OpenPortal` for the sense of somewhere being opened. `vfx_pick_wisp` is a single small mote,
which is what a thing in transit should look like. `fx_summon_spirit_spawn` is the far end of the
same summon the grab opened, and `sfx_runestone_activate` gives the arrival its weight.
`sfx_dverger_heal_finish` is a short chime that says the work is done without another bang.

**Shimmer.** A uniform `transform.localScale` tween: the object shrinks to near zero over the
first half of **Shimmer Seconds**, the relocation is applied at the midpoint, and it grows back
over the second half. Scale was chosen over an alpha fade deliberately. Valheim's piece shaders are
opaque and there is no reliable `_Color` alpha to drive; a scale tween needs no shader assumptions,
reverts exactly, and reads as a dematerialise. `localScale` is never serialised, so nothing about it
can leak into the save. The default is 1.2 s, which is long enough for the departure, the crossing
and the arrival to read as three separate beats. Shimmer Seconds 0 skips the tween and the travel
wisp, and fires Depart, Arrive and Finish on the same frame.

**The piece's own place effect** still plays, as one more arrival effect at the new spot only. It is
the exact sound and sparkle the game ships for placing that piece, so where a piece has one it
grounds the arrival in the object's own material. It is no longer used at the old spot, and it is no
longer a fallback: the config stages are the choreography, and the place effect rides along with
them. It goes out through `MoveEffects.PlayPlaceEffect`, which spawns it under
`ZNetView.m_forceDisableInit` and tracks the instances like every other stage. Vanilla plays a
place effect on the placing client alone; this one plays inside the RPC handler on every client
that has the object loaded, and `EffectList.Create` instantiates plainly, so without the flag a
place effect carrying a `ZNetView` would claim one ZDO per client for the same puff of smoke. The
effect is skipped altogether unless every enabled entry in the list carries a prefab: `Create`
walks them all and instantiates each without a null check, so one blank entry would throw inside
the RPC handler and take the rest of the arrival with it.

**Travel path.** The wisp is instantiated at the old spot and its transform is written each frame
along `Vector3.Lerp(from, to, t)` plus a sine hump, so it leaves and lands on the ground and rides
over what is between. The arc height is a quarter of the distance covered, capped at 3 m. The lerp
allocates nothing per frame: it writes `transform.position` from stack values only.

**Prefab resolution.** Names are looked up in `ZNetScene.instance.m_namedPrefabs` by stable hash
rather than through `GetPrefab`, so a name that is not in this game version warns once instead of
spamming `GetPrefab`'s error log on every move.

**Networked prefabs.** Several of the magic-flavoured vanilla effects, the summon and guardstone
families in particular, carry a `ZNetView`, and instantiating one as-is would create a networked
object once per client. Rather than skip them, which would rule out most of the interesting
effects, every effect is instantiated with `ZNetView.m_forceDisableInit` set: `ZNetView.Awake` then
destroys itself instead of claiming a ZDO, so the instance is purely local, silent on the wire, and
identical to look at. The flag is saved and restored around the `Instantiate` call.

**Lifetime.** `MoveEffects` owns the lifetime of everything it spawns and does not trust the
prefab's own `TimedDestruction`: that component caches its `ZNetView` in `Awake`, and with the
`ZNetView` destroyed under it the timer can end up doing nothing. Every instance is tracked and
destroyed after at most 5 s by `MoveEffects.Tick`, which `AuraMoveController.Tick` calls before its
own local-player guard so cleanup keeps running while the player is torn down. The travel wisp is
destroyed on arrival. The grab ring is destroyed by `MoveCarry.Drop`, so a cancel or a confirm takes
it with the carry. `StopAll` destroys every tracked instance and restores every tweened scale, and
runs on `Game.Logout` and on plugin shutdown.

**Nothing is parented to the moved object.** An attached effect would be dragged through the
relocation and would inherit the shimmer's near-zero scale, so every instance is a free-standing
world object at a fixed position. That also keeps effects clear of the ZDO position write.

A tween whose object is destroyed mid-flight aborts on the next frame, and `MoveEffects.StopAll`
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
AuraMove/MoveTargeting.cs       the AuraMove key, input gates, crosshair prompt patch
AuraMove/GuildMove.cs           pseudo-piece, Merchant Guild category, piece table and tab patches
AuraMove/MoveCarry.cs           carry state and the vanilla placement patches
AuraMove/MoveRelocation.cs      RPC register/send/handle, ZDO write, UpdateOrientation
AuraMove/MoveEffects.cs         the five effect stages, their lifetimes, and the shimmer coroutine
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
internal static ConfigEntry<string> AuraMoveGrabEffects = null!;
internal static ConfigEntry<string> AuraMoveDepartEffects = null!;
internal static ConfigEntry<string> AuraMoveTravelEffect = null!;
internal static ConfigEntry<string> AuraMoveArriveEffects = null!;
internal static ConfigEntry<string> AuraMoveFinishEffects = null!;
internal static ConfigEntry<KeyboardShortcut> AuraMoveKey = null!;

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
    internal static bool InputBlocked { get; }
    internal static bool ShortcutDown { get; }
    internal static string KeyLabel { get; }
}

internal static class GuildMove
{
    internal const Piece.PieceCategory Category = (Piece.PieceCategory)10;
    internal static Piece? PseudoPiece { get; }
    internal static PieceTable? HammerTable { get; }
    internal static bool IsPseudoPiece(Piece? piece);
    internal static bool IsSelectedBy(Player? player);
    internal static void Setup(ObjectDB db);
    internal static void Shutdown();
    internal static void RefreshDescription();
    internal static void InjectInto(PieceTable table);
    internal static void EnsureTabs(PieceTable? table);
}

internal static class MoveCarry
{
    internal static bool IsCarrying { get; }
    internal static Piece? Source { get; }
    internal static GameObject? CarriedPrefab { get; }
    internal static string CarriedName { get; }
    internal static bool TooFar { get; }
    internal static bool Grab(Player player, Piece piece);
    internal static void Drop(bool rebuildGhost);
    internal static void ApplyDistanceRule(Player player);
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
    internal static void Tick();
    internal static void PlayGrab(Piece source);
    internal static void StopGrab();
    internal static void PlayRelocation(GameObject movedObject, Vector3 fromPosition,
        Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, System.Action apply);
    internal static void StopAll();
}

internal static class AuraMoveController
{
    internal static bool IsAvailable { get; }
    internal static void Init();
    internal static void Tick();
    internal static void Shutdown();
    internal static bool HandlePlaceClick(Player player);
    internal static void CancelByPlayer(Player player);
}
```

`AuraMoveController.IsAvailable` is
`OttoAuraPlugin.AuraMoveEnabled.Value == OttoAuraPlugin.Toggle.On && OttoPayBridge.IsAuraPayEnabled()`,
re-read every frame because the server can switch the setting and the player can switch AuraPay at
any moment, exactly as `AuraBoostEffect.IsActive` does.

## Harmony hooks

| Hook | Kind | Why |
| --- | --- | --- |
| `ObjectDB.Awake` | postfix | Build the pseudo-piece; the hammer's piece table and the Coins icon live in the item database. |
| `ObjectDB.CopyOtherDB` | postfix | Joining a server rebuilds the database, so resolve the hammer table again. |
| `PieceTable.UpdateAvailable` | postfix | Put the pseudo-piece and the Merchant Guild category back into the hammer table, or take them away when the service is off. |
| `PieceTable.GetSelectedPrefab` | postfix | While carrying, hand vanilla the source object's prefab so it builds and drives the ghost. |
| `Player.GetBuildSelection` | postfix | Put the pseudo-piece back for the build panel, so it names the Guild's fee and not the carried object's build cost. |
| `Player.UpdatePlacementGhost` | postfix | Apply Max Move Distance as one more invalid condition, on top of every vanilla rule. |
| `Player.TryPlacePiece` | prefix | The left click: take hold, or charge and send the move. Never builds anything. |
| `Player.PlacePiece` | prefix | Belt and braces: the pseudo-piece can never be instantiated. |
| `Player.RemovePiece` | prefix | While carrying, right click means "let go" rather than the hammer's remove. |
| `Player.SetSelectedPiece(Vector2Int)` | prefix | Choosing another piece drops the carry before the ghost is rebuilt. |
| `Player.SetBuildCategory(int)` / `(PieceCategory)` | prefix | Same, for the category tabs. |
| `Player.SetPlaceMode` | prefix | Any equipment change, including putting the hammer away and dying, drops the carry. |
| `Hud.UpdateCrosshair` | postfix | The prompt that says what a click will do. |
| `Minimap.SetMapMode` | prefix | Alt+M would also open the large map, because vanilla's Map button ignores Alt. |
| `Game.Logout` | prefix | Leaving the world clears the carry and stops any shimmer. |
| `Game.Start` | postfix | Register the relocation RPC (unchanged). |

`Menu.Show` is no longer patched. Escape was the old cancel key and had to be kept away from the
pause menu; right click is the cancel now, so Escape does what it always did.

## Known limitations

- A destination that overlaps the source object, or another piece, is accepted wherever vanilla
  would accept it. The Guild is exactly as fussy as the hammer, no more.
- Taking hold costs the hammer's attack stamina check, so a click with an empty stamina bar
  flashes the bar instead of grabbing. Nothing is charged.
- The right click cancel sits behind the same stamina check, because `Player.UpdatePlacement`
  gates its whole remove branch on it before `Player.RemovePiece` is ever called. With an empty
  bar the Guild keeps hold until stamina comes back. Every other way out of a carry - picking
  another piece, another tab, stowing the hammer, walking away - is unaffected, and nothing is
  charged either way.
- Where the HUD prefab carries no category tabs, the Merchant Guild tab cannot be drawn. The piece
  is still in the modern build menu under the Misc usage tag.
