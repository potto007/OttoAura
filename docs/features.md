# OttoAura feature guide

The full rules behind each feature. The [README](../README.md) has the short version, and the [configuration reference](configuration.md) lists every setting named here.

## Ward aura

A ward counts when it is switched on, contains you, and lists you as creator or permitted. That's the same access a ward grants for building.

Each tick, one second by default, the aura does two things:

1. It heals you by HealPerSecond times the seconds since the last tick, up to your maximum health.
2. It repairs each worn item that can be repaired by RepairPercentPerTick of its maximum durability. An item that reaches full durability shows the game's repaired message and effect.

Set either value to 0 to turn that part off.

### Paying for repairs

A repair costs CoinsPerItemTick coins per item per tick, 1 by default, from your OttoPay Merchant Bank balance. AuraPay has to be on in the inventory window. The aura takes the whole tick's cost or nothing, so a short balance stops repairs instead of paying for half of them. With an empty balance the aura says so once every 30 seconds and keeps healing.

Set CoinsPerItemTick to 0 to make repairs free. PreventCraftingStationRepair hides the repair panel at crafting stations, which leaves the ward as the only place to repair.

## AuraBoost

You need to be a Merchant Bank member with AuraPay on. Running on dirt paths, wood and metal then drains StaminaUsageTrail of the usual stamina, half by default. Paved roads and stone drain StaminaUsageRoad, nothing by default. A status icon shows while it works. AuraBoost costs no coins and works anywhere.

Only things players make count. Terrain counts when it's clearly hoed into a path or paved. Built pieces count by material. Stone, marble, ashstone and ancient pieces are roads. Wood, hardwood, timberwood and iron are trails.

AuraBoost doesn't stack with run stamina discounts other mods add through their own status effects. The bigger discount wins. Food, meads, Moder's power and item mods built on the game's own status effect types stack as usual.

## AuraMove

AuraMove needs AuraPay on. The Merchant Guild picks a placed object up and sets it down a short way off, contents and state intact, so you don't have to smash it and build it again.

### Using it

Take out the hammer. A Merchant Guild build category holds one entry, Guild Move. Pick it from the build menu, or press MoveKey, `LeftAlt + M` by default, which takes the hammer out with Guild Move selected. The same key puts the hammer away. With the service off, or AuraPay off, the category disappears.

Look at a chest, a lantern, a piece of furniture or a beehive. If the Guild will move it, the crosshair says so and names the fee. Left click and the Guild takes hold. The object stays put while you aim a ghost of it the way you aim any build piece, with the same rotation, snapping and red invalid tint. The Guild adds one rule of its own. The new spot can't be further than MaxMoveDistance from where the object stands now.

Left click again to set it down. The fee, Coins, comes out of your Merchant Bank balance whole or not at all. A short balance moves nothing and charges nothing. Right click lets go for free, and so does picking another piece, putting the hammer away, dying, teleporting or walking off.

### The effect

A summoning ring lights up at the object's feet when the Guild takes hold. On the confirming click the old spot flares, the object shrinks away, a wisp carries it across, and it grows back at the new spot over a spirit summon and a runestone chime. Other players see the same thing, because the effects travel in the same message as the new position. Chest contents and bed spawn points move with the object. Set ShimmerSeconds to 0 to snap instead. The GrabEffects, DepartEffects, TravelEffect, ArriveEffects and FinishEffects settings pick the effect prefabs for each stage.

### What can't move

The Guild won't touch vehicles, live wards, armed traps or active shield generators. Walls, floors and beams stay where they are, but every crafting station can move, from the workbench to the forge and the cauldron. Plants are rooted. A chest another player has open is off limits.

By default other pieces that carry structural load can't move either, so the building itself never shifts. SupportIsImmovable turns that rule off. AllowedPrefabs and DeniedPrefabs grant or block particular objects by prefab name, whatever the other rules say.

## AuraTrade

The Merchant Guild buys valuables, anything with a coin value except coins. You need AuraPay on, OttoPay 1.6.0 or newer, and you must stand inside a ward that is on and lists you as creator or permitted.

### Selling

Open your inventory and pick up a valuable, a whole stack, or part of a stack after splitting it. Inside such a ward, the Merchant Bank balance icon turns into a deposit arrow, and its tooltip shows what the sale pays. Drop the valuables on the arrow to sell them.

To sell everything you carry at once, look at the ward and press AltPlace plus Use, `LeftShift + E` by default. The ward's hover text shows what that pays. Plain Use still switches the ward on and off. Outside a ward, or with AuraPay off, the arrow doesn't appear and nothing sells.

### Fees

The AuraPay network takes a cut, the way a card processor does. Each sale pays FlatFee plus PercentFee of its worth, rounded up. With the defaults of 5 coins and 5%:

| Sale | Worth | Fee | You get |
| --- | --- | --- | --- |
| One valuable worth 20 | 20 | 6 | 14 |
| Twelve of them together | 240 | 17 | 223 |

The flat part is charged once per sale, so fewer, larger sales keep more. Every valuable's tooltip shows worth, fee and net for one item and for the whole stack. A sale the fee would swallow whole is refused, and nothing leaves your inventory.

The valuables leave your inventory before the coins arrive. If the Merchant Bank refuses the deposit, they come straight back. A sold valuable appears in front of you, shrinks away as a wisp carries it into the ward, and the ward answers with a spirit summon. Nearby players with OttoAura see it too. DeniedItems lists item prefab names the Guild won't buy.

## AuraDispel

A lit Wisp Torch clears the Mists inside its radius. AuraDispel clears the distance fog over the same ground. The fog thins inside the radius and comes back as you look past it, so a torch-lit base is clear from inside and stands out from outside. It works in every biome and any foggy weather. The 16 lit Wisp Torches nearest you count, wherever you stand. It costs no coins and doesn't need AuraPay. A Wisplight carried by a player doesn't count.

It changes only what each player sees. Water, glass, smoke and the Mists themselves keep their own haze, and the sky looks the same as anywhere else. RadiusScale makes the clear area bigger or smaller.

The effect needs a shader built with the game's Unity version, and it ships inside the mod. If your graphics setup can't run it, the game's fog stays as it is and the BepInEx log says so once.

## Blacksmithing

With Blacksmithing installed, an item that reaches full durability stops losing durability for a while. Once the player's Blacksmithing skill factor reaches 0.5, that time is 10 minutes times the factor. The item data keys are the ones the RepairStation mod uses, so gear it repaired keeps its time.
