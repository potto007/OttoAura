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

By default a repair costs one coin per item per tick, paid from your OttoPay Merchant Bank balance, so you need OttoPay installed and AuraPay turned on in the inventory window. The aura takes the whole tick's cost or nothing, which means a balance that runs short stops repairs rather than paying for half of them. When the balance is empty the aura tells you so once every 30 seconds, and it keeps healing.

Set Coins Per Item Tick to 0 to make repairs free, and then OttoPay is not needed at all.

## Client and server

Install OttoAura on the server and on every client. The server checks the version of each client that joins and disconnects any client that does not have the same version. The server sends its config to the clients, and the file watcher picks up edits you make to the server's config file while it runs.

## Configuration

The config file is `potto007.OttoAura.cfg` in the BepInEx config folder. Every setting below is synced from the server except Show Heal Text.

| Setting | Default | Range | Meaning |
| --- | --- | --- | --- |
| Lock Configuration | On | | Only server admins can change the config. |
| Heal Per Second | 1 | 0 to 50 | Health restored each second inside an active ward. 0 turns healing off. |
| Repair Percent Per Tick | 5 | 0 to 100 | Percent of an item's maximum durability restored each tick. 0 turns repair off. |
| Coins Per Item Tick | 1 | 0 to 1000 | Coins charged to the OttoPay balance for each item repaired in a tick. 0 makes repair free. |
| Tick Seconds | 1 | 0.25 to 30 | Seconds between aura ticks. |
| Show Heal Text | Off | | Show a floating number on each heal tick. This setting is not synced. |
| Prevent Crafting Station Repair | Off | | Hide the repair panel at crafting stations, so a ward aura is the only way to repair. |

## Other mods

- OttoPay is needed for paid repairs, and it is not needed when Coins Per Item Tick is 0.
- Blacksmithing changes what happens after a repair. When an item reaches full durability, it stops losing durability for a while, and that time is 10 minutes times the player's Blacksmithing skill factor once the factor reaches 0.5. The item data keys are the same ones RepairStation used, so gear repaired by RepairStation keeps its time.

## Credits

OttoAura started from RepairStation 1.2.6 by Azumatt, under the MIT No Attribution license. The ward aura replaced the station, while the Blacksmithing hooks and the ServerSync plumbing came along unchanged. Bugs are mine, so report them at https://github.com/potto007/OttoAura.
