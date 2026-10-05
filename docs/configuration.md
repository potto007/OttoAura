# OttoAura configuration

Settings live in `potto007.OttoAura.cfg` in the BepInEx config folder. With OttoAura on the server, the server's values win and clients follow them. The server picks up edits to its config file while it runs. Every setting is synced except ShowHealText and AuraMove's MoveKey, which each player sets for themselves.

Section and setting names lost their spaces and leading numbers in 1.3.0, so `5 - AuraMove` became `AuraMove` and `Max Move Distance` became `MaxMoveDistance`. An older config file is renamed in place the first time 1.3.0 or later loads, and your values come with it.

The [feature guide](features.md) explains what each feature does with these values.

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
| AuraMove: MoveKey | M + LeftAlt | | Takes the hammer out with Guild Move selected, and puts it away again. Not synced. |
| AuraTrade: Enabled | On | | Turn the Guild's valuables trade off entirely. |
| AuraTrade: FlatFee | 5 | 0 to 1000 | Coins the AuraPay network keeps from every trade, however large. |
| AuraTrade: PercentFee | 5 | 0 to 50 | Percent of a trade's gross worth kept on top of FlatFee, rounded up to whole coins. |
| AuraTrade: DeniedItems | | | Comma-separated item prefab names the Guild will not buy, for example Ruby,AmberPearl. |
| AuraTrade: ShimmerSeconds | 1.2 | 0 to 3 | How long a sold valuable takes to shrink away into the ward. 0 plays only the effects. |
| AuraTrade: DepartEffects | vfx_Potion_eitr_minor,... | | Comma-separated vanilla effect prefabs played where the sold valuable appears in front of the seller. |
| AuraTrade: TravelEffect | vfx_pick_wisp | | One vanilla effect prefab flown with the sold valuable into the ward. Blank flies nothing. |
| AuraTrade: ArriveEffects | fx_summon_spirit_spawn,... | | Comma-separated vanilla effect prefabs played at the ward as the valuable reaches it. |
| AuraDispel: Enabled | On | | A lit Wisp Torch also dispels the distance fog inside the radius where it clears the Mists. |
| AuraDispel: RadiusScale | 1 | 0.25 to 3 | Size of the fog-free area, as a multiple of the radius where the torch clears the Mists. |
