using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The weapon you can see (the player's words, 9 November 2026: "always have an image of the item included ... it can have a
/// prominent spot", "where do i see the weapon recommendation in this ui mod?"): the item's own picture (UI/ItemPortraitMath.cs,
/// UiKit.ItemPortrait: the qualities' colours, the card's tile), and the suggestions in Nice Inventory Tab's equipment box
/// (Weapons/WeaponNiceMath.cs, WeaponNiceTab.cs: its widths at every tab width it allows, the room the box makes, where the
/// row goes, which buttons fit), and the seams (the hooks by name and soft, the game's own icon drawing, the card's head,
/// the game's gear tab alike, the setting, the apparel card's note).
/// </summary>
public static class WeaponShowTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the weapon you can see: " + what);
        }
        bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) <= eps;
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string Read(string rel) => File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        // ---------- The qualities' colours ----------
        var q = ItemPortraitMath.QualityColours;
        C(q.Length == 7, "a colour for each of the game's seven qualities");
        C(q.Distinct().Count() == 7, "each its own");
        const uint niceRow = 0x0e1013, niceTile = 0x15191d;
        for (int i = 0; i < q.Length; i++)
        {
            C(SkinMath.Contrast(q[i], SkinMath.WindowGround) >= SkinMath.TextContrast, $"quality {i}: its word reads on the window's ground ({SkinMath.Contrast(q[i], SkinMath.WindowGround):0.0}:1)");
            C(SkinMath.Contrast(q[i], niceRow) >= SkinMath.TextContrast && SkinMath.Contrast(q[i], niceTile) >= SkinMath.TextContrast, $"quality {i}: and on Nice Inventory Tab's grounds");
            C(SkinMath.Contrast(q[i], SkinMath.Raised) >= 3f, $"quality {i}: its frame shows on a raised card ({SkinMath.Contrast(q[i], SkinMath.Raised):0.0}:1)");
        }
        C(ItemPortraitMath.QualityColour(-1) == 0u && ItemPortraitMath.QualityColour(7) == 0u, "no quality, no colour");
        C(ItemPortraitMath.CardTile(400f) == 96f && ItemPortraitMath.CardTile(564f) == 113f && ItemPortraitMath.CardTile(900f) == 128f, "the card's picture: 96 to 128 pixels, a fifth of the card");
        C(ItemPortraitMath.CardTile(564f) >= ItemPortraitMath.LargeTile && 20f < ItemPortraitMath.LargeTile, "the card's picture is large (framed twice, its quality named); the lists' small ones aren't");

        // ---------- Nice Inventory Tab's widths ----------
        C(Near(WeaponNiceMath.RowWidth(WeaponNiceMath.TabDefault, false), 363f) && Near(WeaponNiceMath.RowWidth(WeaponNiceMath.TabDefault, true), 349f),
            "at its first width (760) a row of the equipment box is 363 pixels, 349 with its scroll bar");
        // Button widths as the row measures them (a word in the tiny font, about 6 pixels a letter, and 16 around it).
        float Btn(string word) => Math.Max(48f, word.Length * 6f + 16f);
        float equipSidearm = Btn("Equip") + 4f + Btn("As sidearm"), trade = Btn("Trade with Bautista");
        foreach (float tab in new[] { WeaponNiceMath.TabMin, WeaponNiceMath.TabDefault, WeaponNiceMath.TabMax })
        {
            float text = WeaponNiceMath.TextWidth(WeaponNiceMath.RowWidth(tab, true));
            C(equipSidearm + 4f + WeaponNiceMath.CrossW <= text && trade + 4f + WeaponNiceMath.CrossW <= text,
                $"tab {tab}: the widest acts (Equip and As sidearm, or Trade with a long name) and Not now's cross fit the row ({text:0} pixels of text)");
            C(text - equipSidearm >= 0f, $"tab {tab}: nothing runs past the row");
        }
        float atFirst = WeaponNiceMath.TextWidth(WeaponNiceMath.RowWidth(WeaponNiceMath.TabDefault, true));
        C(WeaponNiceMath.NotNowAsWord(atFirst, equipSidearm, Btn("Not now")), "at its first width, Not now is a word");
        C(!WeaponNiceMath.NotNowAsWord(WeaponNiceMath.TextWidth(WeaponNiceMath.RowWidth(WeaponNiceMath.TabMin, true)), equipSidearm, Btn("Not now")),
            "at its narrowest, a cross with the words on its tooltip");
        C(Near(WeaponNiceMath.TileW, 70f) && WeaponNiceMath.TileH > 50f, "the picture's tile is as Nice Inventory Tab's rows have theirs: 1.2 as wide as high, and larger");

        // ---------- The room the box makes ----------
        C(WeaponNiceMath.Height(new List<bool>(), false) == 0f, "nothing to show: no room taken");
        C(WeaponNiceMath.Height(null, true) == WeaponNiceMath.QuietH, "the quiet line alone");
        C(Near(WeaponNiceMath.Height(new List<bool> { false }, true), WeaponNiceMath.RowH), "one row (the quiet line never with it)");
        C(Near(WeaponNiceMath.Height(new List<bool> { false, true }, false), 2f * WeaponNiceMath.RowH + WeaponNiceMath.WarnH + WeaponNiceMath.Gap),
            "two rows, one with a warning, and the gap between: the box lays out that much, so nothing overlaps or is cut");

        // ---------- Where the row goes ----------
        var H = WeaponNiceMath.Child.Held;
        var O = WeaponNiceMath.Child.Other;
        var U = WeaponNiceMath.Child.Ours;
        C(WeaponNiceMath.Place(new List<WeaponNiceMath.Child> { H, H, O, O }) == 2, "under the last thing held, above the inventory's cells");
        C(WeaponNiceMath.Place(new List<WeaponNiceMath.Child> { O, O }) == 0, "first when nothing is held");
        C(WeaponNiceMath.InPlace(new List<WeaponNiceMath.Child> { H, U, O }) && !WeaponNiceMath.InPlace(new List<WeaponNiceMath.Child> { U, H, O }),
            "Nice Inventory Tab adds its rows back after a change: the row is seen out of place and moved");
        C(WeaponNiceMath.Place(new List<WeaponNiceMath.Child> { U, H, O }) == 1 && !WeaponNiceMath.InPlace(new List<WeaponNiceMath.Child> { H, O }), "moved under the weapon; missing is not in place");

        // ---------- The seams ----------
        string kit = Read("Source/ColonyCouncil/UI/UiKitItems.cs");
        string card = Read("Source/ColonyCouncil/Weapons/WeaponCardWindow.cs");
        string nice = Read("Source/ColonyCouncil/Weapons/WeaponNiceTab.cs");
        string ui = Read("Source/ColonyCouncil/Weapons/WeaponPickUi.cs");
        string gear = Read("Source/ColonyCouncil/Weapons/WeaponGearTab.cs");
        C(kit.Contains("public static void ItemPortrait(") && kit.Contains("Widgets.ThingIcon(pic, thing)") && kit.Contains("Widgets.DefIcon(pic, def,"),
            "the picture is the game's own drawing (its stuff, style, unique and random graphics, any mod's)");
        C(kit.Contains("pictureFailed") && kit.Contains("catch (Exception e)"), "a graphic a mod can't give leaves the tile empty, once, quietly");
        C(card.Contains("UiKit.ItemPortrait(pic, thing, def, stuff, true)") && card.Contains("ItemPortraitMath.CardTile(w)"), "the card's head: the picture large, its quality named");
        C(card.Contains("UiKit.ItemPortrait(new Rect(92f, y, 20f, 20f), t)") && card.Contains("UiKit.ItemPortrait(new Rect(202f, y + 2f, 20f, 20f), held)"),
            "small pictures where the card lists other weapons: what a colonist holds and carries, who should carry it");
        C(ui.Contains("UiKit.ItemPortrait(tile, t, ground: look.tile") && ui.Contains("UiKit.DashedBox(r, look.frame)") && ui.Contains("CC_WeaponPick_TagSuggested"),
            "a suggestion's row: the picture, a dashed frame and its tag, never read as a weapon held");
        C(ui.Contains("Rows(new Rect(0f, y, width, 9999f), p, GameLook)") && nice.Contains("WeaponPickGearTab.Rows(g, sp.pawn, Look)"),
            "the game's gear tab and Nice Inventory Tab's box draw the same rows");
        foreach (var name in new[] { "\"NiceInventoryTab.ITab_Pawn_Gear_Patch\"", "\"equipment_alt_groupbox\"", "\"equipment_legacy_groupbox\"", "\"Childs\"", "\"Visible\"", "\"Geometry\"",
                     "\"SetFixedHeight\"", "\"Update\"", "\"NiceInventoryTab.LabelWidget\"", "\"Recache\"", "\"Draw\"" })
            C(nice.Contains(name), $"Nice Inventory Tab's {name} read by name");
        C(!nice.Contains("using NiceInventoryTab"), "no hard reference to Nice Inventory Tab");
        C(nice.Contains("if (changed) UpdateLayout.Invoke(box, null);"), "the box is laid out again only when the row moved or changed height");
        C(gear.Contains("WeaponGearTab.Click(badge, thing, pawn);"), "on Nice Inventory Tab's rows (no room for a word more) the badge opens the card to compare");
        string apparel = Read("docs/design/apparel.md");
        C(apparel.Contains("UiKit.ItemPortrait"), "the apparel card's plan uses the same picture");
        return n;
    }
}
