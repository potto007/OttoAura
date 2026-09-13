# OttoAura

![OttoAura. Restore within the ward.](https://raw.githubusercontent.com/potto007/OttoAura/master/docs/images/ottoaura-title.png)

Your ward heals you and repairs your gear while you stand inside it.

There is nothing to build. Every vanilla ward you own, or are permitted on, gives off an aura once it is switched on, and while you stand inside it each tick restores some health and puts some durability back on the gear you wear. Point at the ward and its hover text tells you what the aura does and how far it reaches.

## How the aura works

Once a tick, one second by default, the mod looks for a player ward that is on, that contains you, and that lists you as creator or permitted, which is the same access a ward grants for building. If it finds one, it does two things:

1. It heals you for the Heal Per Second amount times the seconds since the last tick, up to your maximum health.
2. It repairs each worn item that can be repaired by the Repair Percent Per Tick share of its maximum durability, and an item that reaches full durability shows the vanilla repaired message and effect.

You can turn either part off by setting it to 0.

## Paying for repairs

By default a repair costs one coin per item per tick, paid from your OttoPay Merchant Bank balance, so you need AuraPay turned on in the inventory window. The aura takes the whole tick's cost or nothing, which means a balance that runs short stops repairs rather than paying for half of them. When the balance is empty the aura tells you so once every 30 seconds, and it keeps healing.

Set Coins Per Item Tick to 0 to make repairs free.

## AuraBoost

The magic of AuraPay makes you feel invigorated and lighter on your feet! As a result, you drain less stamina when you stay on the roads and trails. Once you have joined the Merchant Bank and turned AuraPay on in OttoPay, running on dirt paths, wood and metal drains half the usual stamina, and paved roads and stone drain none. A status icon shows while it is working. AuraBoost costs no coins, and it works anywhere, not only inside a ward.

Only things players make count. Terrain counts when it is clearly hoed into a path or paved. Built pieces count by material: stone, marble, ashstone and ancient pieces are roads, and wood, hardwood, timberwood and iron are trails.

## AuraMove

While AuraPay is on, the Merchant Guild offers one more service: for a small coin fee it picks a placed object up and sets it down a short distance away, contents and state intact, so you do not have to smash it and build a new one. Look at a chest, a lantern, a piece of furniture or a beehive with empty hands. If the Guild is willing to move it, the hover text says so and names the key.

Press that key to grab the object. A placement ghost appears where you are looking while the real object stays put, and you aim it the way you aim a build piece: the mouse wheel or the gamepad rotate buttons turn it, and the ghost goes red wherever the Guild will not set it down, including anywhere past Max Move Distance from where the object stands now. Press the key again to confirm. The fee comes out of your OttoPay Merchant Bank balance whole or nothing, the same way repairs do, so a balance that runs short moves nothing and charges nothing. Escape, or the gamepad B button, lets go at no cost, and so does dying, teleporting or taking out a build tool.

The object fades out where it stood and fades back in at the new spot. Other players on the server see the move too, because the new position goes through the object's ZDO. Chest contents and bed spawn points follow it.

Some things cannot be moved. The Guild will not touch vehicles, live wards, armed traps or active shield generators. Workbenches and stonecutters are load-bearing build infrastructure and stay where they are. Plants are rooted. Any chest that another player has open is off limits. By default, non-furniture pieces that carry structural load are immovable too, so the building itself cannot shift; you can turn that rule off with the Support Is Immovable setting. The Allowed Prefabs and Denied Prefabs lists let you grant or block particular objects by prefab name regardless of the other rules.

## Client and server

OttoAura is a client mod, so it works on a server that does not have it.

If you also install it on the server, the server config wins and the clients follow it. The file watcher picks up edits you make to the server's config file while it runs. A server with the mod also checks the version of each client that joins, and it disconnects any client that does not have the same version.

## Configuration

The config file is `potto007.OttoAura.cfg` in the BepInEx config folder. Every setting below is synced from the server except Show Heal Text and the three AuraMove input settings.

| Setting | Default | Range | Meaning |
| --- | --- | --- | --- |
| Lock Configuration | On | | Only server admins can change the config. |
| Heal Per Second | 1 | 0 to 50 | Health restored each second inside an active ward. 0 turns healing off. |
| Repair Percent Per Tick | 5 | 0 to 100 | Percent of an item's maximum durability restored each tick. 0 turns repair off. |
| Coins Per Item Tick | 1 | 0 to 1000 | Coins charged to the OttoPay balance for each item repaired in a tick. 0 makes repair free. |
| Tick Seconds | 1 | 0.25 to 30 | Seconds between aura ticks. |
| Show Heal Text | Off | | Show a floating number on each heal tick. This setting is not synced. |
| Prevent Crafting Station Repair | Off | | Hide the repair panel at crafting stations, so a ward aura is the only way to repair. |
| AuraBoost: Enabled | On | | Bank members with AuraPay on drain less stamina running on roads and trails. |
| AuraBoost: Stamina Usage Trail | 0.5 | 0 to 1 | Run stamina drain on dirt paths, wood and metal. 0 is none and 1 is vanilla. |
| AuraBoost: Stamina Usage Road | 0 | 0 to 1 | Run stamina drain on paved roads and stone. 0 is none and 1 is vanilla. |
| AuraBoost: Show Status Icon | On | | Show the AuraBoost icon in the status bar while the effect is active. |
| AuraMove: Enabled | On | | Turn the Guild move service off entirely. |
| AuraMove: Coins | 5 | 0 to 1000 | Coins charged to the Merchant Bank balance per completed move. 0 makes moving free, but AuraPay must still be on. |
| AuraMove: Max Move Distance | 10 | 1 to 64 | How far in metres the destination may sit from where the object stands now. |
| AuraMove: Support Is Immovable | On | | Non-furniture pieces that carry structural load cannot be moved. |
| AuraMove: Allowed Prefabs | wood_fine_stack,... | | Comma-separated prefab names that skip every eligibility restriction and can always be moved. |
| AuraMove: Denied Prefabs | fire_pit,... | | Comma-separated prefab names that can never be moved. |
| AuraMove: Shimmer Seconds | 0.6 | 0 to 3 | Total duration of the shrink and grow animation. 0 snaps and only plays the burst effects. |
| AuraMove: Effect Prefabs | vfx_Place_wood_pole,... | | Comma-separated fallback effect prefabs used when the moved piece has no place effect of its own. |
| AuraMove: Move Key | V + LeftAlt | | Keyboard shortcut to grab and confirm a move. Not synced - set per client. |
| AuraMove: Gamepad Modifier | JoyAltKeys | | ZInput button held with the gamepad button. Leave empty for no modifier. Not synced - set per client. |
| AuraMove: Gamepad Button | JoyButtonY | | ZInput button that grabs and confirms a move. Not synced - set per client. |

## Other mods

- OttoPay is required, and OttoAura does not load without it. Paid repairs and AuraBoost both work through AuraPay.
- AuraBoost does not stack with run stamina discounts that other mods add through their own status effects. When one is active, the bigger discount wins. Food, meads, Moder's power and item mods built on the game's own status effect types still stack with AuraBoost as usual.- Blacksmithing changes what happens after a repair. When an item reaches full durability, it stops losing durability for a while, and that time is 10 minutes times the player's Blacksmithing skill factor once the factor reaches 0.5. The item data keys are the same ones RepairStation used, so gear repaired by RepairStation keeps its time.

## Credits

OttoAura started from RepairStation 1.2.6 by Azumatt, under the MIT No Attribution license. The ward aura replaced the station, while the Blacksmithing hooks and the ServerSync plumbing came along unchanged. Bugs are mine, so report them at https://github.com/potto007/OttoAura.
