using System;
using System.IO;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// What makes a camp go when it is harmed or in danger (Camps/CampHarmMath; the player's report of 10 November 2026:
/// "camps packed up after an animal taking revenge after being attacked, animal and other more arbitrary attacks should
/// not get the camp to leave"). Real attacks (the colony meaning it, raiders, a people at war with theirs) send them off as
/// before; a beast taking revenge, a manhunter, a predator, an insect, a wild man, a fit of madness, a stray shot or an
/// accident don't: those come at the camp are fought off by its fire and kept clear of by the rest. Then the mod's side:
/// the camp's group, the hourly reading of danger and the band check all ask the rule.
/// </summary>
public static class CampHarmTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp harm: " + what);
        }

        var colonist = new CampHarmFacts { instigator = true, colony = true };
        var turret = colonist;
        var colonyDog = new CampHarmFacts { instigator = true, colony = true, animal = true };
        var revenge = new CampHarmFacts { instigator = true, animal = true, noFaction = true, mad = true, hostileToColony = false };
        var manhunter = new CampHarmFacts { instigator = true, animal = true, noFaction = true, mad = true };
        var predator = new CampHarmFacts { instigator = true, animal = true, noFaction = true };
        var hiveSpider = new CampHarmFacts { instigator = true, animal = true, insect = true, hive = true, hostileToCamp = true, hostileToColony = true };
        var wildInsect = new CampHarmFacts { instigator = true, animal = true, insect = true, noFaction = true };
        var raider = new CampHarmFacts { instigator = true, hostileToColony = true };
        var raidersDog = new CampHarmFacts { instigator = true, animal = true, hostileToColony = true };
        var foe = new CampHarmFacts { instigator = true, hostileToCamp = true };
        var mech = new CampHarmFacts { instigator = true, hostileToCamp = true, hostileToColony = true };
        var wildMan = new CampHarmFacts { instigator = true, noFaction = true };
        var accident = new CampHarmFacts();

        // ---------------- Real attacks ----------------

        C(CampHarmMath.By(colonist) == CampHarmBy.Colony && CampHarmMath.RealAttack(colonist) && CampHarmMath.Scatters(colonist),
            "a colonist, a turret or a trap of the colony's meaning it: they scatter at once, as before");
        C(CampHarmMath.Scatters(turret) && CampHarmMath.Scatters(colonyDog), "the colony's animal set on them too");
        C(CampHarmMath.By(raider) == CampHarmBy.Raiders && CampHarmMath.RealAttack(raider) && !CampHarmMath.Scatters(raider),
            "raiders: a real danger, read by the camp each hour (they defend their fire or go), not a scatter on the first hit");
        C(CampHarmMath.By(raidersDog) == CampHarmBy.Raiders && CampHarmMath.RealAttack(raidersDog), "the raiders' own beasts come with them, and count as the raid");
        C(CampHarmMath.By(foe) == CampHarmBy.Foes && CampHarmMath.RealAttack(foe) && !CampHarmMath.Scatters(foe), "a people at war with the camp's own");
        C(CampHarmMath.RealAttack(mech), "machines at war with everyone");

        // ---------------- Arbitrary harm ----------------

        C(CampHarmMath.By(revenge) == CampHarmBy.Beast && !CampHarmMath.RealAttack(revenge) && !CampHarmMath.Scatters(revenge),
            "a beast taking revenge for a hunt is no reason to go (the player's report)");
        C(!CampHarmMath.RealAttack(manhunter) && !CampHarmMath.RealAttack(predator), "nor a manhunter or a predator");
        C(CampHarmMath.By(hiveSpider) == CampHarmBy.Insect && !CampHarmMath.RealAttack(hiveSpider) && CampHarmMath.By(wildInsect) == CampHarmBy.Insect,
            "nor an insect, wild or of a hive, though hives are at war with everyone");
        C(CampHarmMath.By(new CampHarmFacts { instigator = true, colony = true, animal = true, mad = true }) == CampHarmBy.Beast,
            "the colony's own beast gone manhunter is a beast, not the colony");
        C(CampHarmMath.By(wildMan) == CampHarmBy.Other && !CampHarmMath.RealAttack(wildMan), "a wild man of no people");
        C(CampHarmMath.By(accident) == CampHarmBy.Accident && !CampHarmMath.RealAttack(accident) && !CampHarmMath.FendOff(CampHarmBy.Accident),
            "an accident: nobody to fight, nothing to go for");
        var stray = colonist;
        stray.crossfire = true;
        C(CampHarmMath.By(stray) == CampHarmBy.Stray && !CampHarmMath.Scatters(stray) && !CampHarmMath.FendOff(CampHarmBy.Stray),
            "the colony's shot meant for an enemy of both (the game's own crossfire rule) hits one of them: a stray, they stay");
        var berserk = colonist;
        berserk.mad = true;
        C(!CampHarmMath.Scatters(berserk), "a colonist out of their mind, or one of theirs gone berserk, is no attack of the colony's");
        C(CampHarmMath.By(new CampHarmFacts { instigator = true }) == CampHarmBy.Other, "someone of a people at peace with both (a quarrel, a fit)");

        bool onlyColonyScatters = true, scattersAreReal = true;
        for (int bits = 0; bits < 1 << 10; bits++)
        {
            var f = new CampHarmFacts
            {
                instigator = (bits & 1) != 0, colony = (bits & 2) != 0, crossfire = (bits & 4) != 0, mad = (bits & 8) != 0, animal = (bits & 16) != 0,
                insect = (bits & 32) != 0, noFaction = (bits & 64) != 0, hive = (bits & 128) != 0, hostileToCamp = (bits & 256) != 0, hostileToColony = (bits & 512) != 0
            };
            if (CampHarmMath.Scatters(f) && (!f.colony || f.crossfire || f.mad)) onlyColonyScatters = false;
            if (CampHarmMath.Scatters(f) && !CampHarmMath.RealAttack(f)) scattersAreReal = false;
            if (CampHarmMath.RealAttack(f) && CampHarmMath.FendOff(CampHarmMath.By(f))) scattersAreReal = false;
        }
        C(onlyColonyScatters && scattersAreReal, "of every mix of facts, only the colony meaning it scatters them, and what they fight off is never a real attack");

        // ---------------- Fought off, kept clear of ----------------

        C(CampHarmMath.FendOff(CampHarmBy.Beast) && CampHarmMath.FendOff(CampHarmBy.Insect) && CampHarmMath.FendOff(CampHarmBy.Other), "beasts, insects and wild men are fought off");
        C(CampHarmMath.Fights(true, true, true) && !CampHarmMath.Fights(false, true, true) && !CampHarmMath.Fights(true, false, true) && !CampHarmMath.Fights(true, true, false),
            "those who stand by the fire: grown, able to fight, armed");
        C(CampHarmMath.Runs(false, CampHarmMath.KeepClear) && !CampHarmMath.Runs(false, CampHarmMath.KeepClear + 1f) && !CampHarmMath.Runs(true, 1f),
            "the rest run when it comes near; the fighters hold");
        C(CampHarmMath.FendRadius >= 20f && CampHarmMath.RunFrom > CampHarmMath.KeepClear, "fought off within reach of the fire; they run well clear");

        // ---------------- The mod's side ----------------

        string camps = Path.Combine(root, "Source", "ColonyCouncil", "Camps");
        string Code(string f) => Regex.Replace(File.ReadAllText(Path.Combine(camps, f)), @"//[^\n]*", "");
        string lord = Code("CampLord.cs"), harm = Code("CampHarm.cs"), life = Code("CampLife.cs"), war = Code("WarCamp.cs"), raid = Code("CampRaid.cs");
        C(lord.Contains("scatter.AddTrigger(new Trigger_CampHarmed());") && !lord.Contains("Trigger_PawnHarmed"),
            "the camp's group scatters by the rule's trigger, no longer on any harm with a faction behind it");
        C(Regex.IsMatch(harm, @"class Trigger_CampHarmed : Trigger\s*\{[^}]*Trigger_PawnHarmed\.SignalIsHarm\(signal\)[\s\S]*?CampHarmMath\.Scatters\(CampHarm\.Facts\(by, signal\.Pawn, lord\?\.faction, signal\.dinfo\.IntendedTarget\)\)"),
            "the trigger takes the game's own harm signals and asks the rule, with who did it, who was hurt and what was aimed at");
        C(war.Contains("scatter.AddTrigger(new Trigger_CampHarmed());") && !war.Contains("Trigger_PawnHarmed"), "a war party come to help scatters by the same rule");
        C(lord.Contains("!CampHarm.RealThreatOn(lord.Map, lord.faction)") && !lord.Contains("MapFacts.ThreatOn"), "back to their fire from a fight once no real danger is left");
        C(Regex.IsMatch(harm, @"RealThreatOn\(Map map, Faction camp\)[\s\S]*?MapFacts\.ThreatOn\(map\)[\s\S]*?CampHarmMath\.RealAttack\(Facts\(t\.Thing, null, camp\)\)"),
            "danger on the land is read through the rule, after the game's own threat test");
        C(life.Contains("bool danger = CampHarm.RealThreatOn(map, faction);") && life.Contains("if (danger)") && life.Contains("if (fighting && !danger) fighting = false;")
            && !Regex.IsMatch(life, @"if \(MapFacts\.ThreatOn\(map\)\)\s*\{"),
            "the hour's reading of danger (defend or go) is the real attacks alone");
        C(life.Contains("(!CampHarm.RealThreatOn(map, faction) || CampRaids.OnlyBandsOn(map))"), "packing goes on with a beast about");
        C(life.Contains("CampHarmMath.RealAttack(CampHarm.ByOf(p, faction))"), "they defend their fire against raiders near, by the rule");
        C(raid.Contains("CampHarmMath.RealAttack(CampHarm.ByOf(p, null))"), "a beast about doesn't count as a danger beside a band");
        C(Regex.IsMatch(harm, @"private void Fend\(\)[\s\S]*?CampHarm\.Menace\(map, faction, FireCell, here\)[\s\S]*?CampHarmMath\.Fights\([\s\S]*?CampHarmMath\.Runs\([\s\S]*?FleeUtility\.FleeJob[\s\S]*?LordJob_Camp\.MemoGuard"),
            "a beast at the camp: fighters stand by the fire (the guard), the rest run from it, the game's own flight");
        C(Regex.IsMatch(harm, @"Menace\([\s\S]*?CampHarmMath\.FendOff\(ByOf\(p, camp\)\)"), "only what the rule says to fight off is fought off");
        C(life.Contains("Fend();") && life.Contains("if (fending && !(toil is LordToil_CampGuard)) fending = false;"), "watched every few seconds, and let go with the guard");
        C(Code("Camp.cs").Contains("ExposeHarm();") && harm.Contains("Scribe_Values.Look(ref fending, \"fending\")"), "saved");
        return n;
    }
}
