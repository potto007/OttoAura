# OttoAura

![OttoAura. Restore within the ward.](https://raw.githubusercontent.com/potto007/OttoAura/main/docs/images/ottoaura-title.png)

**Version 1.5.1**, built and Harmony-checked against Valheim 1.0.16.

Your ward heals you and repairs your gear while you stand inside it. Turn on AuraPay in OttoPay and the Merchant Guild throws in a few more services.

The [feature guide](https://github.com/potto007/OttoAura/blob/main/docs/features.md) has the full rules for everything below.

## Ward aura

Switch on a ward you own or are permitted on and stand inside it. Every second it heals you a little and puts some durability back on the gear you wear. Hover the ward to see what it does and how far it reaches.

Repairs cost 1 coin per item per tick from your OttoPay balance, so AuraPay has to be on. When the coins run out, repairs stop and healing keeps going.

## AuraBoost

With AuraPay on, running on roads and trails costs less stamina. Dirt paths, wood and metal cost half. Paved roads and stone cost nothing. It's free and works anywhere, not only in a ward. Only paths, roads and floors that players made count.

## AuraMove

The Merchant Guild moves a placed object a short distance for 5 coins, with its contents intact. Take out the hammer and pick Guild Move from the Merchant Guild tab, or press `LeftAlt + M`. Click the object, aim it like a build piece, and click again to set it down. Right click cancels for free.

Chests, furniture, crafting stations and beehives move. Walls, floors and anything holding the building up stay put.

## AuraTrade

Inside a ward you can use, with AuraPay on, the Merchant Guild buys valuables. Drop a valuable on your Merchant Bank balance in the inventory to sell it, or press `LeftShift + E` on the ward to sell everything you carry. Each valuable's tooltip shows what it pays.

The Guild keeps 5 coins plus 5% of every sale, so one big sale beats several small ones. AuraTrade needs OttoPay 1.6.0.

## AuraDispel

A lit Wisp Torch clears the distance fog over the same ground where it clears the Mists, in any biome. Ring your base with a few and it stays clear in foggy weather, seen from inside or out. It's free and doesn't need AuraPay.

Only the view changes. Water, smoke, the Mists and the sky keep their haze.

## Client and server

OttoAura works as a client-only mod. Install it on the server too and the server's settings win. Every client with OttoAura must run the same version as the server.

## Configuration

Settings live in `BepInEx/config/potto007.OttoAura.cfg`, and the server pushes them to clients. AuraBoost, AuraMove, AuraTrade and AuraDispel each have an `Enabled` switch, and costs, ranges and effects can all be changed. The [configuration reference](https://github.com/potto007/OttoAura/blob/main/docs/configuration.md) lists every setting.

## Other mods

- OttoAura needs OttoPay 1.5.0. AuraTrade needs OttoPay 1.6.0.
- AuraBoost doesn't stack with run stamina discounts from other mods. The bigger discount wins.
- With Blacksmithing installed, fully repaired gear stops wearing for a while. Higher Blacksmithing skill means longer.

## Credits

OttoAura is maintained by **Paul Otto**. Report bugs at https://github.com/potto007/OttoAura.
