# OttoAura

![OttoAura. Restore within the ward.](https://raw.githubusercontent.com/potto007/OttoAura/main/docs/images/ottoaura-title.png)

**Version 1.5.1**, built and Harmony-checked against Valheim 1.0.16.

Your ward heals you and repairs your gear while you stand inside it. Turn on AuraPay in [OttoPay](https://thunderstore.io/c/valheim/p/potto007/OttoPay/) and the Merchant Guild throws in a few more services.

## What you get

- **Ward aura.** Stand in a ward you own or are permitted on. It heals you and repairs the gear you wear, 1 coin per item per tick.
- **AuraBoost.** Roads and trails drain less stamina. Half on dirt paths, wood and metal, none on paved roads and stone. Free.
- **AuraMove.** The Merchant Guild moves a chest, crafting station or piece of furniture a short way for 5 coins, contents intact.
- **AuraTrade.** Inside your ward, sell rubies, amber and other valuables straight into your Merchant Bank balance.
- **AuraDispel.** A lit Wisp Torch clears the fog around it, in any biome. Free.

Every service except AuraDispel needs AuraPay on in the inventory window. The [feature guide](https://github.com/potto007/OttoAura/blob/main/docs/features.md) has the full rules for each one.

## How to use it

### Ward aura

- Switch the ward on and stand inside it. Hover the ward to see what it does and how far it reaches.
- Repairs come out of your Merchant Bank balance. When it runs dry, repairs stop and healing keeps going.

### AuraBoost

- Turn AuraPay on, then run on a path, road or floor that players made.
- A status icon shows while it works. It works anywhere, not only in a ward.

### AuraMove

- Take out the hammer and pick Guild Move from the Merchant Guild tab, or press `LeftAlt + M`.
- Click an object, aim it like a build piece, and click again to set it down. Right click cancels for free.
- Chests, furniture, crafting stations and beehives move. Walls, floors and anything holding the building up stay put.

### AuraTrade

- Stand in your ward and open the inventory. Pick up a valuable and drop it on your Merchant Bank balance to sell it.
- To sell everything you carry, look at the ward and press `LeftShift + E`.
- The Guild keeps 5 coins plus 5% of each sale, so one big sale beats several small ones. Each valuable's tooltip shows what it pays.

### AuraDispel

- Light a Wisp Torch. The fog clears over the same ground where the torch clears the Mists, seen from inside or out.
- Ring a base with a few torches to keep it clear in foggy weather. Water, smoke, the Mists and the sky keep their haze.

## Configuration

Settings live in `BepInEx/config/potto007.OttoAura.cfg`. Change them in a text editor or in game with Configuration Manager. With OttoAura on the server, the server's values win and clients follow them. Every setting is synced except ShowHealText and MoveKey.

Each table below is one section of the file. The first covers three small ones, General, Aura and CraftingStations.

### General, Aura and CraftingStations

| Setting | Default | What it does |
| --- | --- | --- |
| LockConfiguration | On | Only server admins can change the config. |
| HealPerSecond | 1 | Health restored each second in an active ward, 0 to 50. 0 turns healing off. |
| RepairPercentPerTick | 5 | Percent of an item's durability restored each tick, 0 to 100. 0 turns repair off. |
| CoinsPerItemTick | 1 | Coins per item repaired each tick, 0 to 1000. 0 makes repairs free. |
| TickSeconds | 1 | Seconds between aura ticks, 0.25 to 30. |
| ShowHealText | Off | Show a floating number on each heal tick. Not synced. |
| PreventCraftingStationRepair | Off | Hide the repair panel at crafting stations, so the ward is the only place to repair. |

### AuraBoost

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | On | Turn AuraBoost on or off. |
| StaminaUsageTrail | 0.5 | Stamina drain on dirt paths, wood and metal. 0 is none, 1 is vanilla. |
| StaminaUsageRoad | 0 | Stamina drain on paved roads and stone. 0 is none, 1 is vanilla. |
| ShowStatusIcon | On | Show the AuraBoost icon while it works. |

### AuraMove

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | On | Turn AuraMove on or off. |
| Coins | 5 | Fee per move, 0 to 1000. 0 is free, but AuraPay must still be on. |
| MaxMoveDistance | 15 | How far an object can move, in metres, 1 to 64. |
| SupportIsImmovable | On | Pieces that hold the building up can't move. Furniture and crafting stations are exempt. |
| AllowedPrefabs | wood_fine_stack,... | Prefab names that can always move, comma separated. |
| DeniedPrefabs | fire_pit,... | Prefab names that can never move, comma separated. |
| MoveKey | M + LeftAlt | Take out the hammer with Guild Move selected, or put it away. Not synced. |
| ShimmerSeconds | 1.2 | Length of the shrink and grow animation, 0 to 3. 0 snaps. |

The effect settings, GrabEffects, DepartEffects, TravelEffect, ArriveEffects and FinishEffects, pick the vanilla effect prefabs played at each stage of a move. Leave them alone unless you want a different look.

### AuraTrade

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | On | Turn AuraTrade on or off. |
| FlatFee | 5 | Coins kept from every sale, 0 to 1000. |
| PercentFee | 5 | Percent of each sale kept on top of FlatFee, 0 to 50. Rounded up. |
| DeniedItems | | Item prefab names the Guild won't buy, comma separated, for example `Ruby,AmberPearl`. |
| ShimmerSeconds | 1.2 | How long a sold valuable takes to shrink into the ward, 0 to 3. |

DepartEffects, TravelEffect and ArriveEffects pick the effect prefabs for a sale, the same way they do for AuraMove.

### AuraDispel

| Setting | Default | What it does |
| --- | --- | --- |
| Enabled | On | Turn AuraDispel on or off. |
| RadiusScale | 1 | Size of the fog-free area, as a multiple of the torch's Mist radius, 0.25 to 3. |

## Client and server

OttoAura works as a client-only mod. Install it on the server too and the server's settings win. Every client with OttoAura must run the same version as the server, or the server turns it away with a message naming both versions.

## Other mods

- **OttoPay** is required. OttoAura charges your Merchant Bank balance through it. AuraTrade needs OttoPay 1.6.0 or newer.
- **Blacksmithing.** Fully repaired gear stops wearing for a while, longer with higher Blacksmithing skill.
- **Other stamina mods.** AuraBoost doesn't stack with run stamina discounts from other mods. The bigger discount wins.

## Credits

OttoAura is maintained by **Paul Otto**. Report bugs at https://github.com/potto007/OttoAura.
