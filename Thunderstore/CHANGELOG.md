# Changelog

## 1.4.0

- AuraTrade. The Merchant Guild buys valuables. Stand inside a ward you may use, with AuraPay on, and the Merchant Bank balance in your inventory turns into a deposit arrow when you pick up a valuable. Drop a valuable, a stack or a split on it to sell. `LeftShift + E` on the ward sells every valuable you carry at once. Coins are never bought.
- The coins go to your Merchant Bank balance, less the AuraPay network's fee for the magic: a flat charge per sale plus a percent of what the sale is worth, 5 coins plus 5% by default. The flat charge is paid once per sale, so one big sale keeps more than several small ones. A sale the fee would swallow whole is refused and nothing is taken.
- A sold valuable leaves the way AuraMove objects do: it appears in front of the seller, shrinks away as a wisp carries it into the ward, and the ward answers with a spirit summon. Every nearby player with OttoAura sees it.
- Every valuable's tooltip shows what AuraTrade would pay for one and for the whole stack: gross worth, the fee, and the net. The deposit arrow's tooltip shows the same for what you are holding, and the ward's hover text shows it for everything you carry.
- New server-synced settings under `AuraTrade`: `Enabled`, `FlatFee`, `PercentFee`, `DeniedItems`, `ShimmerSeconds`, `DepartEffects`, `TravelEffect` and `ArriveEffects`.
- AuraTrade needs OttoPay 1.6.0. With an older OttoPay it stays off and everything else works as before.
- The version check is ServerSync's alone. A client without OttoAura can join a server that has it, and a client with OttoAura only has to match the server's version. Before, the server also compared the DLL file itself and turned away every client without that exact file, OttoAura or not. A server still on 1.3.0 keeps doing that, so update the server first.

## 1.3.0

- Changing a synced setting on a server no longer throws, and clients now receive the new value. The bundled ServerSync read `ZRoutedRpc.Everybody` as a field, and Valheim 1.0.12 made it a constant, so every broadcast died before it left the server.
- Config sections and settings are PascalCase: `5 - AuraMove` is now `AuraMove`, and `Max Move Distance` is now `MaxMoveDistance`. An older config file is renamed in place the first time 1.3.0 loads and keeps the values you chose. Settings the mod no longer defines are left alone.
- `MaxMoveDistance` now defaults to 15 metres instead of 10.

## 1.2.1

- Every crafting station can be moved, from the workbench to the forge, the cauldron and their extensions. Building pieces such as walls, floors and beams still stay put.
- The Thunderstore description no longer mentions the mod OttoAura started from; the credit stays in the README.

## 1.2.0

- AuraMove. While AuraPay is on, the Merchant Guild picks a placed object up and sets it down a short distance away for a small coin fee, contents and state intact, so you do not have to smash it and rebuild.
- The fee is whole-or-nothing from your Merchant Bank balance. If the balance runs short, nothing moves and nothing is charged.
- AuraMove lives in the hammer. A new Merchant Guild build category holds one entry, Guild Move: pick it, left click what you want moved, aim the ghost the way you aim any build piece, and left click again to set it down. Right click and the Guild lets go.
- `M + LeftAlt` takes the hammer out with Guild Move already selected, and puts it away again. It is configurable per client. There is no separate gamepad binding: the build menu's Merchant Guild tab is the gamepad route, and the place button does the rest.
- The move is a conjuring, not a jump: a summoning ring at the object's feet when the Guild takes hold, an eitr flare and a portal sound as it leaves, a wisp arcing across to the new spot, and a spirit summon with a runestone chime as it grows back in. All vanilla effects, all client-side, none of them saved.
- New server-synced settings under `5 - AuraMove`: `Enabled`, `Coins`, `Max Move Distance`, `Support Is Immovable`, `Allowed Prefabs`, `Denied Prefabs`, `Shimmer Seconds`, `Grab Effects`, `Depart Effects`, `Travel Effect`, `Arrive Effects`, and `Finish Effects`. Every effect stage takes comma-separated vanilla prefab names, so the whole choreography can be swapped without a rebuild. `Move Key` is per-client only.
- Other players see the move because the new position goes through the object's ZDO. Chest contents and bed spawn points follow it.
- Requires OttoPay 1.5.0; the AuraPay tooltip in the inventory now lists AuraBoost and AuraMove with the current key.

## 1.1.0

- AuraBoost. The magic of AuraPay makes you feel invigorated and lighter on your feet! Merchant Bank members with AuraPay on in OttoPay drain less stamina while running on roads and trails.
- OttoPay is now a required dependency. OttoAura does not load without it, even when repairs are free.
- New server synced settings under `4 - AuraBoost`: `Enabled`, `Stamina Usage Trail`, `Stamina Usage Road` and `Show Status Icon`.
- AuraBoost does not stack with run stamina discounts from other mods' own status effects. When one is active, the bigger discount wins.

## 1.0.1

- Player facing text now says Merchant Bank balance instead of coin pouch, because OttoPay dropped the pouch.
- The README describes the ward aura instead of the old repair station.
- New icon and title art.

## 1.0.0

- First release, from RepairStation 1.2.6 by Azumatt, for Valheim 1.0.7.
- The repair station is gone. Every vanilla ward you have access to now heals you and repairs your worn gear while you stand inside it.
- Repairs cost coins from the OttoPay balance when AuraPay is on, and you can set the cost to 0 for free repairs.
