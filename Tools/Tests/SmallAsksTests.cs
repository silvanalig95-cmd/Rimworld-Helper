using ColonyCouncil;

/// <summary>
/// The player's small asks of 12 November 2026: builders leave the trees in the way to the plant cutters, the medics carry a
/// few medicines, and the rating on LWM's Deep Storage contents (decrees' rests are in DecreeMathTests).
/// </summary>
public static class SmallAsksTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "small asks: " + what);
        }

        // Builders and trees.
        C(BuildersTreesMath.Leave(false, true, true, true, 0), "a tree in the way, another build free and a cutter at home: left to the cutters");
        C(!BuildersTreesMath.Leave(true, true, true, true, 0), "a build the player ordered: the builder cuts it");
        C(!BuildersTreesMath.Leave(false, false, true, true, 0), "only trees are left to the cutters");
        C(!BuildersTreesMath.Leave(false, true, false, true, 0), "nothing else free to build: the builder cuts it");
        C(!BuildersTreesMath.Leave(false, true, true, false, 0), "nobody cuts plants: the builder cuts it");
        C(BuildersTreesMath.Leave(false, true, true, true, BuildersTreesMath.WaitTicks - 1) && !BuildersTreesMath.Leave(false, true, true, true, BuildersTreesMath.WaitTicks),
            "marked half a day with nobody taking it: the builder cuts it");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string trees = File.ReadAllText(Path.Combine(src, "Building", "BuildersTrees.cs"));
        C(trees.Contains("[HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.HandleBlockingThingJob))]") && trees.Contains("__result.def != JobDefOf.CutPlant")
          && trees.Contains("DesignationDefOf.CutPlant") && trees.Contains("constructible is Blueprint_Build") && trees.Contains("RimfolkPatches.On"),
            "the hook marks the tree with the game's own cut mark, on builds only, behind the deep hooks setting");

        // Medics' medicine.
        C(MedicKitsMath.Medics(0) == 0 && MedicKitsMath.Medics(3) == 1 && MedicKitsMath.Medics(6) == 2 && MedicKitsMath.Medics(20) == 3, "one medic carries for a small home, three at most");
        C(MedicKitsMath.Reserve(3, 0) == 4 && MedicKitsMath.Reserve(10, 2) == 14, "the sick's share stays home: one a colonist, four at least, two more for each one sick");
        C(MedicKitsMath.Each(30, 2, 10) == 3 && MedicKitsMath.Each(16, 2, 10) == 2 && MedicKitsMath.Each(12, 2, 10) == 1 && MedicKitsMath.Each(11, 2, 10) == 0,
            "three each with plenty, two with enough, one with a little, none when the sick would go short");
        C(MedicKitsMath.Parse(MedicKitsMath.Record(42, "MedicineIndustrial", 2), out int id, out string def, out int count) && id == 42 && def == "MedicineIndustrial" && count == 2
          && MedicKitsMath.Parse(MedicKitsMath.Record(7, null, -1), out _, out _, out int taken) && taken == -1, "what Rimfolk set, and who the player took over, read back");
        string kits = File.ReadAllText(Path.Combine(src, "Health", "MedicKits.cs"));
        C(kits.Contains("SetCountForGroup(group, each)") && kits.Contains("ThingDefOf.MedicineUltratech") && kits.Contains("records[p.thingIDNumber] = (null, -1)")
          && kits.Contains("SetCountForGroup(group, 0)"), "the game's own carry medicine, the finest kept home, the player's own left alone, put back when no longer a medic");

        // The rating on Deep Storage's contents.
        string rack = File.ReadAllText(Path.Combine(src, "Weapons", "StorageContentsRatings.cs"));
        C(rack.Contains("\"LWM.DeepStorage.ITab_DeepStorage_Inventory\"") && rack.Contains("\"DrawThingRow\"") && rack.Contains("StandsDownWhy") && rack.Contains("WeaponGearTab.Bits(")
          && rack.Contains("ApparelGearTab.Bits("), "the weapon and apparel badge on Deep Storage's contents rows, by name, standing down without the mod");
        // A guest joins when the council says so, Hospitality or not (the player, 12 November 2026).
        string join = File.ReadAllText(Path.Combine(src, "Strangers", "StrangerEffects.cs"));
        C(join.Contains("GuestWill.RecruitNow(x, 0, false)") && !join.Contains("CC_Stranger_HospitalityRecruit")
          && !join.Contains("x.Faction == Faction.OfPlayer || Strangers.IsHospitalityGuest(x)"),
            "an event's join takes a Hospitality guest in through Hospitality's own recruiting, never greyed for being a guest");
        return n;
    }
}
