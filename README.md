# OttoAura

![OttoAura. Restore within the ward.](https://raw.githubusercontent.com/potto007/OttoAura/master/docs/images/ottoaura-title.png)

Your ward heals you and repairs your gear while you stand inside it.

There is nothing to build. Every vanilla ward you own, or are permitted on, gives off an aura once it is switched on, and while you stand inside it each tick restores some health and puts some durability back on the gear you wear. Point at the ward and its hover text tells you what the aura does and how far it reaches.

## How the aura works

Once a tick, one second by default, the mod looks for a player ward that is on, that contains you, and that lists you as creator or permitted, which is the same access a ward grants for building. If it finds one, it does two things:

1. It heals you for the HealPerSecond amount times the seconds since the last tick, up to your maximum health.
2. It repairs each worn item that can be repaired by the RepairPercentPerTick share of its maximum durability, and an item that reaches full durability shows the vanilla repaired message and effect.

You can turn either part off by setting it to 0.

## Paying for repairs

By default a repair costs one coin per item per tick, paid from your OttoPay Merchant Bank balance, so you need AuraPay turned on in the inventory window. The aura takes the whole tick's cost or nothing, which means a balance that runs short stops repairs rather than paying for half of them. When the balance is empty the aura tells you so once every 30 seconds, and it keeps healing.

Set CoinsPerItemTick to 0 to make repairs free.

## AuraBoost

The magic of AuraPay makes you feel invigorated and lighter on your feet! As a result, you drain less stamina when you stay on the roads and trails. Once you have joined the Merchant Bank and turned AuraPay on in OttoPay, running on dirt paths, wood and metal drains half the usual stamina, and paved roads and stone drain none. A status icon shows while it is working. AuraBoost costs no coins, and it works anywhere, not only inside a ward.

Only things players make count. Terrain counts when it is clearly hoed into a path or paved. Built pieces count by material: stone, marble, ashstone and ancient pieces are roads, and wood, hardwood, timberwood and iron are trails.

## AuraMove

While AuraPay is on, the Merchant Guild offers one more service: for a small coin fee it picks a placed object up and sets it down a short distance away, contents and state intact, so you do not have to smash it and build a new one.

The service lives in your hammer. Take the hammer out and you have a new build category, Merchant Guild, holding one entry, Guild Move. Pick it from the build menu, or press MoveKey (`LeftAlt + M` by default) to take the hammer out with Guild Move already selected. The same key puts the hammer away again. The AuraPay panel in the inventory reminds you of the key. With the service switched off, or AuraPay off, the category is not there at all.

With Guild Move selected, look at a chest, a lantern, a piece of furniture or a beehive. If the Guild is willing to move it, the crosshair says so and names the fee. Left click and the Guild takes hold: the real object stays where it stands, and you aim a ghost of it exactly the way you aim a build piece, with the same rotation, snapping and red invalid tint the hammer gives you, plus one rule of the Guild's own, nothing further than MaxMoveDistance from where the object stands now. Left click again to set it down. The fee comes out of your OttoPay Merchant Bank balance whole or nothing, the same way repairs do, so a balance that runs short moves nothing and charges nothing. Right click and the Guild lets go at no cost, and so does picking another piece, putting the hammer away, dying, teleporting or walking off.

The move is a small conjuring. A summoning ring lights up at the object's feet the moment the Guild takes hold. On the confirming click the old spot flares and the object shrinks away, a wisp carries it across, and it grows back in at the new spot over a spirit summon and a runestone chime. Other players on the server see the same thing, because the effects are played from the same message that carries the new position, and the position itself goes through the object's ZDO. Chest contents and bed spawn points follow it. Set ShimmerSeconds to 0 if you would rather it just snapped.

Some things cannot be moved. The Guild will not touch vehicles, live wards, armed traps or active shield generators. Building pieces such as walls, floors and beams stay where they are, but every crafting station, from the workbench to the forge and the cauldron, can be moved. Plants are rooted. Any chest that another player has open is off limits. By default, other pieces that carry structural load are immovable too, so the building itself cannot shift; you can turn that rule off with the SupportIsImmovable setting. The AllowedPrefabs and DeniedPrefabs lists let you grant or block particular objects by prefab name regardless of the other rules.

## AuraTrade

The Merchant Guild buys valuables, anything with a coin value except coins themselves. You need AuraPay on in OttoPay, OttoPay 1.6.0 or newer, and you have to stand inside a ward that is switched on and lists you as creator or permitted, the same ward whose aura heals you.

Open your inventory and pick up a valuable, a whole stack, or part of one after splitting it. While you are in such a ward, the Merchant Bank balance icon turns into a deposit arrow, and its tooltip shows what the sale would pay. Drop the valuables on the arrow to sell them. To sell everything you carry in one go, look at the ward and press `LeftShift + E` (AltPlace and Use, if you have rebound them); the ward's hover text tells you what that would pay. Plain `E` still switches the ward on and off. Outside a ward, or with AuraPay off, the arrow does not appear for valuables and nothing can be sold.

The AuraPay network takes a fee for the magic, the way a card processor does: a flat FlatFee per sale plus PercentFee of what the sale is worth, rounded up. With the defaults of 5 coins and 5%, a valuable worth 20 sold on its own pays 6 in fees and puts 14 in your balance, while twelve of them sold together are worth 240, pay 17 and put 223 in your balance. The flat part is charged once however much you sell, so fewer, larger sales keep more. Every valuable's tooltip shows the gross worth, the fee and the net, for one item and for the whole stack. A sale the fee would take entirely is refused, and nothing leaves your inventory.

The valuables leave your inventory before the coins are credited, and if the Merchant Bank refuses the deposit they come straight back. A sold valuable disappears the way AuraMove objects do: it appears in front of you, shrinks away as a wisp carries it into the ward, and the ward answers with a spirit summon. Other players nearby who have OttoAura see it too. DeniedItems lists item prefab names the Guild will not buy.

## Client and server

OttoAura is a client mod, so it works on a server that does not have it.

If you also install it on the server, the server config wins and the clients follow it. The file watcher picks up edits you make to the server's config file while it runs. A client without OttoAura can still join a server that has it. A client with OttoAura must run the same version as the server, or it is turned away with a message naming both versions.

## Configuration

The config file is `potto007.OttoAura.cfg` in the BepInEx config folder. Every setting below is synced from the server except ShowHealText and the AuraMove MoveKey.

Section and setting names lost their spaces and their leading numbers in 1.3.0, so `5 - AuraMove` is now `AuraMove` and `Max Move Distance` is now `MaxMoveDistance`. A config file from an earlier version is renamed in place the first time 1.3.0 loads, and the values you chose come with it. Nothing to do by hand.

| Setting | Default | Range | Meaning |
| --- | --- | --- | --- |
| LockConfiguration | On | | Only server admins can change the config. |
| HealPerSecond | 1 | 0 to 50 | Health restored each second inside an active ward. 0 turns healing off. |
| RepairPercentPerTick | 5 | 0 to 100 | Percent of an item's maximum durability restored each tick. 0 turns repair off. |
| CoinsPerItemTick | 1 | 0 to 1000 | Coins charged to the OttoPay balance for each item repaired in a tick. 0 makes repair free. |
| TickSeconds | 1 | 0.25 to 30 | Seconds between aura ticks. |
| ShowHealText | Off | | Show a floating number on each heal tick. This setting is not synced. |
| PreventCraftingStationRepair | Off | | Hide the repair panel at crafting stations, so a ward aura is the only way to repair. |
| AuraBoost: Enabled | On | | Bank members with AuraPay on drain less stamina running on roads and trails. |
| AuraBoost: StaminaUsageTrail | 0.5 | 0 to 1 | Run stamina drain on dirt paths, wood and metal. 0 is none and 1 is vanilla. |
| AuraBoost: StaminaUsageRoad | 0 | 0 to 1 | Run stamina drain on paved roads and stone. 0 is none and 1 is vanilla. |
| AuraBoost: ShowStatusIcon | On | | Show the AuraBoost icon in the status bar while the effect is active. |
| AuraMove: Enabled | On | | Turn the Guild move service off entirely. |
| AuraMove: Coins | 5 | 0 to 1000 | Coins charged to the Merchant Bank balance per completed move. 0 makes moving free, but AuraPay must still be on. |
| AuraMove: MaxMoveDistance | 15 | 1 to 64 | How far in metres the destination may sit from where the object stands now. |
| AuraMove: SupportIsImmovable | On | | Pieces that carry structural load cannot be moved. Furniture and crafting stations are exempt. |
| AuraMove: AllowedPrefabs | wood_fine_stack,... | | Comma-separated prefab names that skip every eligibility restriction and can always be moved. |
| AuraMove: DeniedPrefabs | fire_pit,... | | Comma-separated prefab names that can never be moved. |
| AuraMove: ShimmerSeconds | 1.2 | 0 to 3 | Total duration of the shrink and grow animation. 0 snaps and only plays the stage effects. |
| AuraMove: GrabEffects | fx_summon_start,... | | Comma-separated vanilla effect prefabs played for you alone when the Guild takes hold of an object. |
| AuraMove: DepartEffects | vfx_Potion_eitr_minor,... | | Comma-separated vanilla effect prefabs played at the old spot as the object leaves it. |
| AuraMove: TravelEffect | vfx_pick_wisp | | One vanilla effect prefab flown along an arc from the old spot to the new one during the shimmer. Blank flies nothing. |
| AuraMove: ArriveEffects | fx_summon_spirit_spawn,... | | Comma-separated vanilla effect prefabs played at the new spot as the object grows back in, alongside the piece's own place effect. |
| AuraMove: FinishEffects | sfx_dverger_heal_finish | | Comma-separated vanilla effect prefabs played at the new spot once the object is whole again. |
| AuraMove: MoveKey | M + LeftAlt | | Takes the hammer out with Guild Move selected, and puts it away again. Not synced - set per client. |
| AuraTrade: Enabled | On | | Turn the Guild's valuables trade off entirely. |
| AuraTrade: FlatFee | 5 | 0 to 1000 | Coins the AuraPay network keeps from every trade, however large. |
| AuraTrade: PercentFee | 5 | 0 to 50 | Percent of a trade's gross worth kept on top of FlatFee, rounded up to whole coins. |
| AuraTrade: DeniedItems | | | Comma-separated item prefab names the Guild will not buy, for example Ruby,AmberPearl. |
| AuraTrade: ShimmerSeconds | 1.2 | 0 to 3 | How long a sold valuable takes to shrink away into the ward. 0 plays only the effects. |
| AuraTrade: DepartEffects | vfx_Potion_eitr_minor,... | | Comma-separated vanilla effect prefabs played where the sold valuable appears in front of the seller. |
| AuraTrade: TravelEffect | vfx_pick_wisp | | One vanilla effect prefab flown with the sold valuable into the ward. Blank flies nothing. |
| AuraTrade: ArriveEffects | fx_summon_spirit_spawn,... | | Comma-separated vanilla effect prefabs played at the ward as the valuable reaches it. |

## Other mods

- OttoPay 1.5.0 is required, and OttoAura does not load without it. Paid repairs and AuraBoost both work through AuraPay. AuraTrade needs OttoPay 1.6.0 and stays off with anything older.
- AuraBoost does not stack with run stamina discounts that other mods add through their own status effects. When one is active, the bigger discount wins. Food, meads, Moder's power and item mods built on the game's own status effect types still stack with AuraBoost as usual.- Blacksmithing changes what happens after a repair. When an item reaches full durability, it stops losing durability for a while, and that time is 10 minutes times the player's Blacksmithing skill factor once the factor reaches 0.5. The item data keys are the same ones RepairStation used, so gear repaired by RepairStation keeps its time.

## Credits

OttoAura started from RepairStation 1.2.6 by Azumatt, under the MIT No Attribution license. The ward aura replaced the station, while the Blacksmithing hooks and the ServerSync plumbing came along unchanged. Bugs are mine, so report them at https://github.com/potto007/OttoAura.
