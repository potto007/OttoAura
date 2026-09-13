using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OttoAura.AuraMove;

// The "Merchant Guild" build category and the single pseudo-piece that lives in it, "Guild Move".
//
// AuraMove is driven from the hammer instead of a hotkey ghost: the player opens the hammer's
// Merchant Guild tab, picks Guild Move, and the pseudo-piece stands in for the service. Nothing
// is ever built from it - Player.TryPlacePiece is intercepted while it is selected (MoveCarry).
//
// The pseudo-piece is deliberately NOT added to PieceTable.m_pieces. Vanilla only offers a piece
// from m_pieces once its name is in the player's known-recipe list, which is saved into the
// character file; injecting the piece into the available lists after PieceTable.UpdateAvailable
// has run keeps the service out of the save and makes availability a pure function of the config
// and of AuraPay.
internal static class GuildMove
{
    internal const string PrefabName = "OttoAura_GuildMove";
    internal const string PieceLabel = "Guild Move";
    internal const string CategoryLabel = "Merchant Guild";

    // Piece.PieceCategory.Max is 9 and All is 100. Max cannot be used as a real category:
    // PieceTable.GetSelectedCategory treats both Max and All as "nothing selected" and resets the
    // selection to the first category, so a piece parked on Max could never stay selected. 10 is
    // the first free value, and it is the same value Jotunn hands out to the first custom
    // category, so a world running both mods lines up rather than fighting.
    internal const Piece.PieceCategory Category = (Piece.PieceCategory)10;

    // One list per category index, 0 through 10 inclusive.
    private const int CategoryListCount = (int)Category + 1;

    private static GameObject? _prefab;
    private static Piece? _pseudoPiece;
    private static PieceTable? _hammerTable;
    private static bool _warnedNoHammer;
    private static string _describedFor = "";
    private static int _describedCoins = -1;
    private static float _describedDistance = -1f;

    internal static Piece? PseudoPiece => _pseudoPiece;
    internal static PieceTable? HammerTable => _hammerTable;

    // True for the pseudo-piece itself and for a placement ghost cloned from it.
    internal static bool IsPseudoPiece(Piece? piece) =>
        piece != null && _pseudoPiece != null && piece.m_name == PieceLabel && piece.m_category == Category;

    // True when the local player has the hammer out with Guild Move selected.
    internal static bool IsSelectedBy(Player? player)
    {
        if (player == null || _hammerTable == null || player.m_buildPieces != _hammerTable)
        {
            return false;
        }

        return IsPseudoPiece(_hammerTable.GetSelectedPiece());
    }

    // Build the pseudo-piece once the item database is loaded. Called from ObjectDB.Awake and
    // ObjectDB.CopyOtherDB; the main menu ObjectDB has no items, so a missing hammer is normal
    // and simply means "not yet".
    internal static void Setup(ObjectDB db)
    {
        if (db == null)
        {
            return;
        }

        PieceTable? table = ResolveHammerTable(db);
        if (table == null)
        {
            return;
        }

        _hammerTable = table;

        if (_prefab != null)
        {
            return;
        }

        GameObject prefab = new(PrefabName);
        // Inactive: the pseudo-piece is a template, and the placement ghost vanilla clones from
        // it stays inactive too, so it renders nothing and runs no component.
        prefab.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(prefab);

        Piece piece = prefab.AddComponent<Piece>();
        piece.m_name = PieceLabel;
        piece.m_description = BuildDescription();
        piece.m_icon = ResolveIcon(db);
        piece.m_category = Category;
        // Misc is the usage tag the modern build menu files it under, so the piece is reachable
        // from the tag list as well as from the category tab.
        piece.m_usage = Piece.UsageTagFlags.Misc;
        piece.m_canBeRemoved = false;
        piece.m_enabled = true;
        piece.m_canRotate = true;
        piece.m_resources = Array.Empty<Piece.Requirement>();

        _prefab = prefab;
        _pseudoPiece = piece;
    }

    internal static void Shutdown()
    {
        // Take the piece and the category back out of the hammer before the prefab goes. Nothing
        // runs InjectInto again once _pseudoPiece is null, so a category left behind would sit in
        // the build menu for the rest of the session as a tab with nothing in it.
        if (_hammerTable != null && _pseudoPiece != null)
        {
            _hammerTable.m_enabledPieces.Remove(_pseudoPiece);
            _hammerTable.m_availablePieces.Remove(_pseudoPiece);
            if (_hammerTable.m_availablePiecesByCategory.Count > (int)Category)
            {
                _hammerTable.m_availablePiecesByCategory[(int)Category].Remove(_pseudoPiece);
            }

            SyncCategory(_hammerTable, available: false);
        }

        if (_prefab != null)
        {
            UnityEngine.Object.Destroy(_prefab);
        }

        _prefab = null;
        _pseudoPiece = null;
        _hammerTable = null;
        _describedFor = "";
        _describedCoins = -1;
        _describedDistance = -1f;
    }

    // Keep the build panel's description honest: it names the live fee and, while the Guild has
    // hold of something, what it is holding. Hud.SetupPieceInfo reads m_description every frame,
    // so refreshing the string is enough; the cache key keeps it from rebuilding needlessly.
    internal static void RefreshDescription()
    {
        if (_pseudoPiece == null)
        {
            return;
        }

        int coins = OttoAuraPlugin.AuraMoveCoins.Value;
        float distance = OttoAuraPlugin.AuraMoveMaxDistance.Value;
        string carried = MoveCarry.CarriedName;

        // Compared field by field rather than through a composed key: this runs every frame.
        if (coins == _describedCoins && distance == _describedDistance && carried == _describedFor)
        {
            return;
        }

        _describedCoins = coins;
        _describedDistance = distance;
        _describedFor = carried;
        _pseudoPiece.m_description = BuildDescription();
    }

    private static string BuildDescription()
    {
        int coins = Mathf.Max(0, OttoAuraPlugin.AuraMoveCoins.Value);
        string fee = coins > 0 ? $"for {coins} coins" : "at no charge";
        int metres = Mathf.RoundToInt(OttoAuraPlugin.AuraMoveMaxDistance.Value);

        if (MoveCarry.IsCarrying)
        {
            return $"The Merchant Guild has hold of the {MoveCarry.CarriedName}. Set it down within "
                   + $"{metres} metres {fee}. Right click and the Guild lets go.";
        }

        return $"The Merchant Guild lifts a placed object and sets it down within {metres} metres {fee}, "
               + "contents and state intact. Point at it and left click to take hold.";
    }

    // ---- Piece table integration -------------------------------------------------------------

    // Called after PieceTable.UpdateAvailable. Adds the pseudo-piece and the Merchant Guild
    // category to the hammer's table while the service is available, and takes both away again
    // when it is not, so nothing of AuraMove shows in the build menu with the feature off.
    internal static void InjectInto(PieceTable table)
    {
        if (table == null || _pseudoPiece == null || table != _hammerTable)
        {
            return;
        }

        EnsureCategoryLists(table);

        bool available = AuraMoveController.IsAvailable;
        if (available)
        {
            table.m_enabledPieces.Add(_pseudoPiece);
            table.m_availablePieces.Add(_pseudoPiece);

            List<Piece> pieces = table.m_availablePiecesByCategory[(int)Category];
            if (!pieces.Contains(_pseudoPiece))
            {
                pieces.Add(_pseudoPiece);
            }
        }

        SyncCategory(table, available);
        EnsureTabs(table);
    }

    // Vanilla sizes m_availablePiecesByCategory to Piece.PieceCategory.Max lists and both
    // selection arrays to 9 entries. A category past Max needs its own slot in all three, or
    // GetPiece and GetSelectedIndex read the wrong list.
    private static void EnsureCategoryLists(PieceTable table)
    {
        while (table.m_availablePiecesByCategory.Count < CategoryListCount)
        {
            table.m_availablePiecesByCategory.Add(new List<Piece>());
        }

        if (table.m_selectedPiece.Length < CategoryListCount)
        {
            Vector2Int[] selected = table.m_selectedPiece;
            Array.Resize(ref selected, CategoryListCount);
            table.m_selectedPiece = selected;
        }

        if (table.m_lastSelectedPiece.Length < CategoryListCount)
        {
            Vector2Int[] lastSelected = table.m_lastSelectedPiece;
            Array.Resize(ref lastSelected, CategoryListCount);
            table.m_lastSelectedPiece = lastSelected;
        }
    }

    private static void SyncCategory(PieceTable table, bool available)
    {
        int index = table.m_categories.IndexOf(Category);

        if (available)
        {
            if (index < 0)
            {
                // Hud.UpdateBuild reads m_categoryLabels[i] for category i, so the label has to
                // land on the same index the category takes, not merely at the end of its own
                // list. The two lists match on the vanilla hammer; padding covers a table where
                // they do not, and keeps the tab from showing somebody else's label.
                int slot = table.m_categories.Count;
                table.m_categories.Add(Category);
                while (table.m_categoryLabels.Count <= slot)
                {
                    table.m_categoryLabels.Add("");
                }

                table.m_categoryLabels[slot] = CategoryLabel;
            }

            return;
        }

        if (index < 0)
        {
            return;
        }

        table.m_categories.RemoveAt(index);
        if (index < table.m_categoryLabels.Count)
        {
            table.m_categoryLabels.RemoveAt(index);
        }

        // The tab the player was standing on has just gone; move them somewhere that exists.
        if (table.m_selectedCategory == Category)
        {
            table.m_selectedCategory = table.m_categories.Count > 0
                ? table.m_categories[0]
                : Piece.PieceCategory.Misc;
        }
    }

    // Hud.UpdateBuild walks m_pieceCategoryTabs and shows tab i for category i, so a table with
    // more categories than the HUD has tabs simply never draws the last one. Clone a tab when
    // that happens. A HUD with no tabs at all is left alone: the piece is still reachable from
    // the modern build menu, which lists PieceTable.m_availablePieces and ignores categories.
    internal static void EnsureTabs(PieceTable? table)
    {
        Hud? hud = Hud.instance;
        if (hud == null || table == null || hud.m_pieceCategoryTabs == null || hud.m_pieceCategoryTabs.Length == 0)
        {
            return;
        }

        while (hud.m_pieceCategoryTabs.Length < table.m_categories.Count)
        {
            GameObject? tab = CreateTab(hud, hud.m_pieceCategoryTabs.Length);
            if (tab == null)
            {
                return;
            }

            GameObject[] tabs = hud.m_pieceCategoryTabs;
            Array.Resize(ref tabs, tabs.Length + 1);
            tabs[tabs.Length - 1] = tab;
            hud.m_pieceCategoryTabs = tabs;
        }
    }

    private static GameObject? CreateTab(Hud hud, int index)
    {
        GameObject template = hud.m_pieceCategoryTabs[0];
        if (template == null)
        {
            return null;
        }

        GameObject tab = UnityEngine.Object.Instantiate(template, template.transform.parent);
        tab.name = $"{PrefabName}_Tab";
        tab.SetActive(false);

        // Instantiate copies serialized data only, and Hud.Awake assigns m_onLeftDown at runtime,
        // so the clone comes back with no click handler.
        UIInputHandler handler = tab.GetComponent<UIInputHandler>();
        if (handler == null)
        {
            handler = tab.AddComponent<UIInputHandler>();
        }
        handler.m_onLeftDown = (Action<UIInputHandler>)Delegate.Combine(handler.m_onLeftDown,
            new Action<UIInputHandler>(hud.OnLeftClickCategory));

        // A layout group parents the vanilla tabs in most HUD versions and will place the clone
        // itself. Where there is none, put the tab one tab-width to the right of the first one.
        if (template.transform.parent != null
            && template.transform.parent.GetComponent<LayoutGroup>() == null
            && template.transform is RectTransform first
            && tab.transform is RectTransform rect)
        {
            rect.anchoredPosition = first.anchoredPosition + new Vector2(first.rect.width * index, 0f);
        }

        return tab;
    }

    private static PieceTable? ResolveHammerTable(ObjectDB db)
    {
        GameObject? hammer = db.GetItemPrefab("Hammer");
        if (hammer == null)
        {
            return null;
        }

        PieceTable? table = hammer.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
        if (table == null && !_warnedNoHammer)
        {
            _warnedNoHammer = true;
            OttoAuraPlugin.OttoAuraLogger.LogWarning("AuraMove: the Hammer item has no piece table, so the Merchant Guild tab cannot be added.");
        }

        return table;
    }

    // The Coins icon is the Merchant Guild's own currency, so it needs no art of its own.
    private static Sprite? ResolveIcon(ObjectDB db)
    {
        GameObject? coins = db.GetItemPrefab("Coins");
        ItemDrop.ItemData.SharedData? shared = coins == null ? null : coins.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
        if (shared != null && shared.m_icons != null && shared.m_icons.Length > 0)
        {
            return shared.m_icons[0];
        }

        OttoAuraPlugin.OttoAuraLogger.LogWarning("AuraMove: the Coins icon was not found, so Guild Move shows without one.");
        return null;
    }
}

// The item database is where the hammer's piece table and the Coins icon live. Awake covers a
// single player world, CopyOtherDB covers joining a server, which rebuilds the database.
[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
static class ObjectDB_Awake_GuildMove_Patch
{
    static void Postfix(ObjectDB __instance) => GuildMove.Setup(__instance);
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
static class ObjectDB_CopyOtherDB_GuildMove_Patch
{
    static void Postfix(ObjectDB __instance) => GuildMove.Setup(__instance);
}

// UpdateAvailable rebuilds the whole availability picture from PieceTable.m_pieces, so the
// pseudo-piece has to be put back every time it runs.
[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
static class PieceTable_UpdateAvailable_Patch
{
    static void Postfix(PieceTable __instance) => GuildMove.InjectInto(__instance);
}
