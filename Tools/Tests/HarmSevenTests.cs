using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Harm, part 7 (Harm/HarmSevenMath.cs; docs/harm-review-proposals.md 1.1.2, 1.3.1, 1.3.2, 1.5.1, 3.5.2, 3.7.1, 3.9.1, 3.9.2,
/// 3.10.1, 3.13.1, 4.1.3, 4.7.3, 3.12.2; 1.2.4, 2.2.2, 2.5.3, 2.6.2, 2.8.2, 2.8.3, 2.9.1, 2.9.2, 3.8.1, 3.13.2, 2.3.2, 2.3.3;
/// 2.10.1, 3.10.2, 3.10.3, 4.8.1, 2.6.1, 2.7.1): each rate set against the review's, each prevention easing it; the three
/// that hurt nobody of the colony outside the budget; and the mod's side: every hurt through the one service with its kind,
/// hooked where its system lives, named before the choice, seen on the map, said in one sentence. After it the review is
/// built in full.
/// </summary>
public static class HarmSevenTests
{
    private const int D = HarmSevenMath.DayTicks, H = HarmSevenMath.HourTicks;

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "harm seven: " + what);
        }

        bool Near(float a, float b, float eps = 0.0001f) => Math.Abs(a - b) < eps;

        // ================= Tempers in the colony =================

        C(Near(HarmSevenMath.GameFightAt(0), 0.04f) && Near(HarmSevenMath.GameFightAt(-100), 0.16f) && Near(HarmSevenMath.GameFightAt(100), 0.024f)
          && Near(HarmSevenMath.GameFightAt(-20), 0.064f), "the game's own insult chance by the pair's opinion (4 at -100, 0.6 at 100)");
        C(Near(HarmSevenMath.SitDown(70f, 5, true), 0.1f) && HarmSevenMath.SitDown(30f, 5, true) == 0f && HarmSevenMath.SitDown(50f, 5, true) < 0.1f
          && HarmSevenMath.SitDown(70f, 15, true) < HarmSevenMath.SitDown(70f, 5, true) && Near(HarmSevenMath.SitDown(70f, 5, false), 0.05f),
            "the peacemaker's sit-down: one in ten for the worst pairs, nothing once cooled, less with Social, half without a hot temper (1.1.2)");
        C(Near(HarmSevenMath.PeacemakerPunch, 1f / 3f) && HarmSevenMath.PunchHigh <= 3f, "the peacemaker stepping between them takes a punch one time in three");
        C(Near(HarmSevenMath.SpeakerWords(true, true, true), 0.2f) && HarmSevenMath.SpeakerWords(false, true, true) == 0f && HarmSevenMath.SpeakerWords(true, false, true) == 0f
          && HarmSevenMath.SpeakerWords(true, true, false) == 0f, "words with the speaker: a fifth, only the bitter and hot-tempered, never under gentler stakes (1.3.1)");
        C(HarmSevenMath.TalksDown(0) == 0f && HarmSevenMath.TalksDown(10) > HarmSevenMath.TalksDown(4) && HarmSevenMath.TalksDown(20) <= 0.75f,
            "a speaker with good Social talks them down more often");
        C(Near(HarmSevenMath.PassedOverChance, HarmSevenMath.GameFightAt(-20)) && HarmSevenMath.PassedOverWords == 2 && HarmSevenMath.PassedOverNature(true, false)
          && HarmSevenMath.PassedOverNature(false, true) && !HarmSevenMath.PassedOverNature(false, false), "what went to someone else: the jealous or abrasive, the game's chance at -20 (1.3.2)");
        C(HarmSevenMath.PassedOverKept(0, 6 * D) && !HarmSevenMath.PassedOverKept(0, 7 * D), "for a week");
        C(HarmSevenMath.Meeting(true, false, 5) < 0.1f && HarmSevenMath.Meeting(true, false, 5) < 1f / 8f && HarmSevenMath.Meeting(false, false, 5) == 0f
          && Near(HarmSevenMath.Meeting(true, true, 5), HarmSevenMath.Meeting(true, false, 5) / 2f), "a meeting that boils over: rarer than the hustings, half with the peacemaker (1.5.1)");
        C(Near(HarmSevenMath.FriendSteps, 1f / 6f) && HarmSevenMath.FriendHigh <= 3f, "friends stand by: one fight in six, a small blow (3.5.2)");
        C(Near(HarmSevenMath.Face(true, 2), 0.1f) && HarmSevenMath.Face(false, 3) == 0f && HarmSevenMath.Face(true, 1) == 0f, "said to their face: one telling in ten, a hot temper (3.7.1)");
        C(Near(HarmSevenMath.Wronged(true, false, false), 0.25f) && Near(HarmSevenMath.Wronged(true, false, true), 0.125f) && HarmSevenMath.Wronged(true, true, false) == 0f
          && HarmSevenMath.Wronged(false, false, false) == 0f, "found out by the wronged one: one in four, half with the peacemaker, never one told by its owner (3.9.1)");
        C(Near(HarmSevenMath.LeakChance, 0.2f) && Near(HarmSevenMath.LeakBlows, 0.5f), "told by the wrong friend: one leak in five, blows half the time (3.9.2)");
        C(Near(HarmSevenMath.LoveRivalChance, 0.25f), "a rival in love: one story in four (3.10.1)");
        C(Near(HarmSevenMath.AltarChance, 1f / 3f), "words at the altar: one time in three, once after the decree (3.13.1)");
        C(Near(HarmSevenMath.FeastFists(true), 0.25f) && HarmSevenMath.FeastFists(false) == 0f, "the feast's fistfight: one quarrel in four with drink, none without (4.1.3)");
        C(Near(HarmSevenMath.SecondRoundChance, 1f / 3f) && HarmSevenMath.Plenty(8, 4) && !HarmSevenMath.Plenty(7, 4) && !HarmSevenMath.Plenty(9, 0),
            "the toast's second round: one toast in three, only with strong drink in plenty (4.7.3)");
        C(Near(HarmSevenMath.SoldierChance, 0.05f) && HarmSevenMath.StrikeHigh <= 3f, "the soldier's nightmare: one in twenty, the mildest bruise (3.12.2)");
        C(HarmSevenMath.SoldierPast("Mercenary") && HarmSevenMath.SoldierPast("Child soldier") && HarmSevenMath.SoldierPast("gang member") && HarmSevenMath.SoldierPast("Space marines")
          && !HarmSevenMath.SoldierPast("Farm boy") && !HarmSevenMath.SoldierPast("Soldering tech") && !HarmSevenMath.SoldierPast(null), "a soldier's or an outlaw's past, by whole words");

        // ================= Visitors and outsiders =================

        C(Near(HarmSevenMath.Gates(false, false), 0.5f) && Near(HarmSevenMath.Gates(true, false), 0.25f) && Near(HarmSevenMath.Gates(true, true), 0.125f),
            "open gates: about half the time, half with the warden, half again on guard (1.2.4)");
        C(HarmSevenMath.StrangerOf(0.1f) == GateStranger.Thief && HarmSevenMath.StrangerOf(0.9f) == GateStranger.Drunk, "a thief or a drunk");
        C(Near(HarmSevenMath.GamblerChance, 1f / 6f) && HarmSevenMath.Evening(20) && !HarmSevenMath.Evening(10), "the gambler's game: one in six, of an evening (2.2.2)");
        C(Near(HarmSevenMath.Barber(false, false), 1f / 30f) && Near(HarmSevenMath.Barber(true, false), 2f / 30f) && HarmSevenMath.Barber(false, true) == 0f && HarmSevenMath.NickHigh <= 1f,
            "the barber's nick: one cut in 30, more for a poor barber, never a spacer's clippers, a cut of one (2.5.3)");
        C(Near(HarmSevenMath.RankEnemyChance, 1f / 6f), "the guest of rank's enemy: one visit in six (2.6.2)");
        C(Near(HarmSevenMath.Crowd(false), 0.2f) && Near(HarmSevenMath.Crowd(true), 0.1f) && HarmSevenMath.CrowdJoins(3, 3) == 1 && HarmSevenMath.CrowdJoins(0, 3) == 0
          && 2 + 2 * HarmSevenMath.CrowdJoins(9, 9) <= HarmSevenMath.CrowdMax, "a crowd: one fistfight in five, up to four in all, half with the warden near (2.8.2)");
        C(Near(HarmSevenMath.PastBlood(true), 0.05f) && HarmSevenMath.PastBlood(false) == 0f, "first blood not stopped: one duel in 20 with bloodlust (2.8.3)");
        C(Near(HarmSevenMath.ChallengeHold, 0.25f) && HarmSevenMath.LetterTicks(0f) == 10 * D && HarmSevenMath.LetterTicks(1f) == 30 * D && HarmSevenMath.ComesTicks(0.5f) == (int)(3.5f * D),
            "a challenge by letter: rare, the letter some days on, the challenger after it (2.9.1)");
        C(Near(HarmSevenMath.HeirChance, 1f / 3f) && HarmSevenMath.HeirWronged < 0f && Near(HarmSevenMath.HeirVisitFight, 0.2f), "the heir's grudge: one heir in three, a fistfight now and then (2.9.2)");
        C(Near(HarmSevenMath.HatedChance, 0.25f), "a trader of the hated people: one visit in four (3.8.1)");
        C(Near(HarmSevenMath.Zeal(false), 0.1f) && Near(HarmSevenMath.Zeal(true), 0.05f), "zeal at the gate: one visit in ten, half under the best-behavior decree (3.13.2)");
        C(Near(HarmSevenMath.Hungry(false, false, false), 1f / 15f) && HarmSevenMath.Hungry(true, false, false) == 0f && Near(HarmSevenMath.Hungry(false, true, true), 1f / 60f)
          && HarmSevenMath.HungryHour(23) && HarmSevenMath.HungryHour(1) && !HarmSevenMath.HungryHour(12), "hungry hands: once or twice a famine at night, never while fed, less watched and lit (2.3.2)");
        C(Near(HarmSevenMath.HungryNightChance * 20f, 4f / 3f), "about once or twice in a famine of a few weeks");
        C(Near(HarmSevenMath.CreekChance, 0.5f) && HarmSevenMath.CreekCatches(true, false) && !HarmSevenMath.CreekCatches(true, true) && !HarmSevenMath.CreekCatches(false, false),
            "a brawl at the creek: half the time, one of ours too near, the marshal there first (2.3.3)");

        // ================= Personal stories =================

        C(HarmSevenMath.Seeks(true, false, false) && !HarmSevenMath.Seeks(true, true, false) && !HarmSevenMath.Seeks(false, false, false) && !HarmSevenMath.Seeks(true, false, true),
            "the nemesis seeks the one it hurt: on the field, not behind cover, once a return (2.10.1)");
        C(Near(HarmSevenMath.WorstNightChance, 0.5f) && HarmSevenMath.WorstHour(0) && !HarmSevenMath.WorstHour(12), "the worst night alone: half the time (3.10.2)");
        C(Near(HarmSevenMath.FirstStepsChance, 1f / 3f) && HarmSevenMath.StepsHigh <= 4f, "first steps alone: one in three (3.10.3)");
        C(Near(HarmSevenMath.Working(WorkingSlip.Fire, false), 0.04f) && Near(HarmSevenMath.Working(WorkingSlip.Ice, true), 0.02f) && HarmSevenMath.Working(WorkingSlip.None, false) == 0f,
            "a working gone wrong: one in 25, half with the teacher (4.8.1)");
        C(HarmSevenMath.SlipOf(true, false, false) == WorkingSlip.Fire && HarmSevenMath.SlipOf(false, true, false) == WorkingSlip.Ice && HarmSevenMath.SlipOf(false, false, true) == WorkingSlip.Storm
          && HarmSevenMath.SlipOf(false, false, false) == WorkingSlip.None, "by the mage's element");
        C(HarmSevenMath.NightPassed(10, 20f) && !HarmSevenMath.NightPassed(10, 10f) && HarmSevenMath.NightPassed(2, 3f) && !HarmSevenMath.NightPassed(2, 1f),
            "the lost child: found after a night out (2.6.1)");
        C(HarmSevenMath.LostWhich(-5f, false) == 0 && HarmSevenMath.LostWhich(5f, false) == 1 && HarmSevenMath.LostWhich(15f, true) == 1 && HarmSevenMath.LostWhich(20f, false) == 2,
            "frostnip in a frost, a chill in the wet or cold, else an ankle");
        C(Near(HarmSevenMath.LostHurtChance, 0.5f) && HarmSevenMath.ScrapeHigh <= 2f, "about half of late finds; a child only the mildest");
        C(Near(HarmSevenMath.SetUponShare, 0.5f) && HarmSevenMath.SetUponLow == 3f && HarmSevenMath.SetUponHigh == 6f, "set upon at the edge: half of the waylaid, the cut they always came in with (2.7.1)");

        // ================= The registry =================

        var built = new[] { "1.1.2", "1.3.1", "1.3.2", "1.5.1", "3.5.2", "3.7.1", "3.9.1", "3.9.2", "3.10.1", "3.13.1", "4.1.3", "4.7.3", "3.12.2", "1.2.4", "2.2.2", "2.5.3",
            "2.6.2", "2.8.2", "2.8.3", "2.9.1", "2.9.2", "3.8.1", "3.13.2", "2.3.2", "2.3.3", "3.10.2", "3.10.3", "4.8.1" };
        foreach (var id in built)
            C(HarmKinds.All.Count(k => !k.founding && k.label.Contains("(" + id + ")")) == 1, $"the proposal {id} is a kind of its own");
        foreach (var id in new[] { "2.10.1", "2.6.1", "2.7.1" })
            C(!HarmKinds.All.Any(k => k.label.Contains("(" + id + ")")), $"{id} hurts nobody of the colony and stays outside the budget");
        // Part 7's: from the sit-down to the working gone wrong (what a camp left comes after, Camps/CampPack.cs).
        int sevenFrom = Array.IndexOf(HarmKinds.All, HarmKinds.SitDown);
        var sevenKinds = HarmKinds.All.Skip(sevenFrom).Take(Array.IndexOf(HarmKinds.All, HarmKinds.WorkingWrong) - sevenFrom + 1).ToList();
        C(sevenKinds.Count == 28 && sevenKinds.All(k => !k.founding && k.weight > 0f && k.weight < 0.025f), "the twenty-eight come after the rest, none founding, each a small share");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var mine = new Dictionary<string, string>
        {
            ["HarmSeven"] = Code("Harm/HarmSeven.cs"), ["Tempers"] = Code("Harm/Tempers.cs"), ["GateTroubles"] = Code("Harm/GateTroubles.cs"), ["Stories"] = Code("Harm/Stories.cs")
        };
        foreach (var kv in mine)
        {
            C(!Regex.IsMatch(kv.Value, @"TakeDamage\(|AddHediff\(|StartSocialFight\(|MakeHediff\("), $"{kv.Key}: nobody is hurt but through the harm service");
            C(!Regex.IsMatch(kv.Value, @"Sched\.(Rare|Hourly|Every)"), $"{kv.Key}: no clock of its own (the harm book's hour and slices)");
            C(!kv.Value.Contains("—") && !kv.Value.Contains("LetterStack"), $"{kv.Key}: no em dashes");
        }
        string tempers = mine["Tempers"], gate = mine["GateTroubles"], stories = mine["Stories"], hub = mine["HarmSeven"], harm = Code("Harm/Harm.cs");
        // Letters only where the proposal is itself a letter: the challenge.
        C(!tempers.Contains("Tell.Letter(") && !stories.Contains("Tell.Letter(") && Regex.Matches(gate, @"Tell\.Letter\(").Count == 1 && gate.Contains("CC_HarmSeven_ChallengeTitle"),
            "a letter only for the challenge by letter");
        C(!tempers.Contains("Tell.Message(") && !gate.Contains("Tell.Message(") && Regex.Matches(stories, @"Tell\.Message\(").Count == 1 && stories.Contains("CC_HarmSeven_SetUpon"),
            "a message only for the enemies at the edge, as the game warns of enemies on the land");
        C(harm.Contains("HarmSeven.Hourly(this, now);") && harm.Contains("HarmSeven.Slice(this, slice, now);") && harm.Contains("HarmSeven.Reset();"), "on the harm book's hour and its slices");
        C(Code("Harm/HarmSix.cs").Contains("default: return HarmSeven.Due(x, now);") && Code("Harm/HarmSix.cs").Contains("default: return HarmSeven.Lapsed(x, now);")
          && Code("Harm/HarmFour.cs").Contains("HarmSeven.Unpeopled(what)"), "what part 7 keeps waiting goes in the harm book's own list, no new saved collection");
        foreach (var system in new[] { "sitDown", "speakerWords", "passedOver", "meetingBoils", "friendsStand", "saidToFace", "wrongedOne", "toldByFriend", "loveRivals", "altarWords",
                     "feastFists", "secondRound", "soldierNight", "openGates", "gamblerTurns", "barberNick", "rankEnemy", "crowdFight", "pastFirstBlood", "letterChallenge", "heirGrudge",
                     "hatedTrader", "gateZeal", "hungryHands", "creekBrawl", "worstNight", "firstSteps", "workingWrong" })
            C(harm.Contains("case \"" + system + "\":"), $"{system} sits under its own system's switch");
        C(Regex.IsMatch(harm, @"case ""speakerWords"": return [^;]*s\.Harsh;"), "words with the speaker only under demanding stakes");
        C(Regex.IsMatch(harm, @"case ""secondRound"": return [^;]*Tempers\.BrewingLoaded") && Regex.IsMatch(harm, @"case ""workingWrong"": return [^;]*Contests\.MagicLoaded"),
            "the soft ones only with their mods");
        foreach (var k in sevenKinds)
            C(Regex.IsMatch(tempers + gate + stories, @"HarmKinds\." + Regex.Escape(Pascal(k.key)) + @"\b"), $"{k.key} is named in its calls");
        // Hooked where each system lives, one line each.
        C(Code("Offices/Offices.cs").Contains("if (Tempers.SitDown(peacemaker, pair.a, pair.b, pair.strength)) return;"), "the sit-down: the peacemaker's own mediation");
        C(Code("CouncilDynamics.cs").Contains("Tempers.PromiseBroken(w.pawn);") && Code("CouncilDynamics.cs").Contains("Tempers.PassedOver(p, who);"), "a broken promise; one passed over");
        C(Code("Ambitions/Ambitions.cs").Contains("if (gathered) Tempers.MeetingDecided(m);") && !Code("Ambitions/CouncilMeeting.cs").Contains("nobody gets hurt over a meeting"),
            "the meeting: decided at the table, its comment saying so now");
        C(tempers.Contains("MentalState_SocialFighting") && tempers.Contains("RecoverFromState"), "friends stand by: any social fight, pulled apart");
        C(Code("Social/Gossip/Gossip.cs").Contains("Tempers.SaidToFace(teller, subject, against);"), "said to their face: gossip told in front");
        C(Code("Secrets/Secrets.cs").Contains("Tempers.SecretHeard(s, speaker, heard, confession);") && Code("Secrets/Secrets.cs").Contains("Tempers.Betrayed(owner, leaker);")
          && Code("Secrets/Secrets.cs").Contains("public bool wrongsColony;"), "the secrets: let slip, betrayed, and which wronged the colony");
        int wrongs = Directory.GetFiles(Path.Combine(root, "Defs", "SecretDefs"), "*.xml").Sum(f => Regex.Matches(File.ReadAllText(f), "<wrongsColony>true</wrongsColony>").Count);
        C(wrongs >= 5, $"the secrets that wronged the colony are marked ({wrongs})");
        C(Code("Arcs/ArcWorkers_Lives.cs").Contains("if (arc.stage == 1) Tempers.RivalsInLove(arc);") && File.Exists(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_HarmSeven.xml")),
            "a rival in love: its rivals' stage, the one they like thinking less of the starter");
        C(tempers.Contains("DecreeFaith.FaithOfColony") && File.ReadAllText(Path.Combine(root, "Defs", "DecreeDefs", "Rimfolk_Decrees_Faith.xml")).Contains("<hurts>OneFaith</hurts>"),
            "words at the altar: under the one-faith decree, named on its card");
        C(Code("Setbacks/SetbackTimes.cs").Contains("Tempers.TableQuarrel(starter, other);") && Code("PlayOut/PlayOutLord.cs").Contains("Tempers.SecondRound(lord, Map, spot, words);")
          && !Code("Integrations/ToastDrinks.cs").Contains("Tempers"), "the table's quarrel and the toast's second round, the toast's own picking left alone");
        C(tempers.Contains("Council_Dream_Shaken") && tempers.Contains("SoldierPast"), "the soldier's nightmare: a nightmare that shook them, a soldier's past");
        C(gate.Contains("DecreeHurt.Gates") && File.ReadAllText(Path.Combine(root, "Defs", "DecreeDefs", "Council_Decrees4.xml")).Contains("<hurts>Gates</hurts>"),
            "open gates: the decree, named on its card");
        C(Regex.Matches(File.ReadAllText(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_Neighbors.xml")), "DilemmaEffect_GamblerTurns").Count == 2,
            "the gambler: under letting anyone play, named on its sheet");
        C(Code("Visitors/Callers.cs").Contains("GateTroubles.Cut(by, p);") && gate.Contains("TechLevel.Spacer"), "the barber: each cut, never a spacer's clippers");
        C(gate.Contains("\"dignitary\"") && gate.Contains("Outsiders.Challenge(c, ch, HarmKinds.RankEnemy)"), "the guest of rank: a dignitary's visit, the outsiders' own duel");
        string outcomes = Code("Outsiders/OutsiderOutcomes.cs");
        C(outcomes.Contains("GateTroubles.Crowd(c, x);") && outcomes.Contains("GateTroubles.PastFirstBlood(bout)") && outcomes.Contains("GateTroubles.Slighted(c, x, false);")
          && outcomes.Contains("GateTroubles.Slighted(c, x, true);"), "the outsiders' hard words, duels and first blood");
        C(Code("World/Notables.cs").Contains("GateTroubles.HeirSucceeded(n, heir);") && Code("Harm/Quarrels.cs").Contains("HarmSeven.Challenge || w.what == HarmSeven.Heir"),
            "the heir's grudge at the succession; a good word or a gift settles a grudge");
        C(gate.Contains("LetterPurpose.Greetings") && gate.Contains("LetterPurpose.Condolences"), "a letter of greetings or condolences first settles it");
        C(gate.Contains("v.faction.HostileTo(Faction.OfPlayer)") && gate.Contains("Faiths.Devout(o)"), "the hated people at peace; a devout of ours");
        C(gate.Contains("\"hunger\"") && gate.Contains("Council_Decree_FamineBread") && gate.Contains("\"prospect\""), "the famine's hungry, fed by famine bread; the prospectors");
        C(stories.Contains("comp.nemeses.Open") && !stories.Contains("Harm.Note(x") && !stories.Contains("HarmKinds.Nemesis"), "the nemesis: the game's own attack, aimed, no hurt of its own");
        C(stories.Contains("\"Rimfolk_Arc_GettingClean\"") && stories.Contains("Binging_DrugExtreme") && stories.Contains("IsForbidden(p)"), "the worst night: the game's own binge, a drug in reach");
        C(stories.Contains("\"Rimfolk_Arc_NewLeg\"") && stories.Contains("PlaySpots.GateCell"), "first steps by the gate");
        C(stories.Contains("\"Council_Arc_PM_Discipline\"") && stories.Contains("\"InnerFire\"") && stories.Contains("\"HeartOfFrost\"") && stories.Contains("\"StormBorn\"")
          && stories.Contains("HurtKind.Frostbite"), "the working: the discipline's practice, A RimWorld of Magic's mages by name, the game's frostbite");
        C(Code("Missions/Workers_MissionRoad.cs").Contains("Stories.LostFound(m, x, finder);") && stories.Contains("HurtClass.Mild"), "the lost child: found, a child only the mildest");
        C(Code("Couriers/CourierTracker.cs").Contains("!Stories.SetUpon(t, people[0], map, entry)") && stories.Contains("LordJob_ExitMapBest"), "the courier: set upon at the edge in place of the cut on the road");
        string dev = Code("Harm/HarmSevenDebug.cs");
        C(!Regex.IsMatch(dev, @"TakeDamage\(|AddHediff\(|StartSocialFight\(|Harm\.(Note|Injure|Ail|Fight)\(") && Regex.Matches(dev, @"\[DebugAction\(""Rimfolk"", ""Harm seven: ").Count >= 31,
            "dev tools for each, through the feature itself, never past the cap");
        var jobs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_HarmSeven.xml"));
        foreach (var j in new[] { "Rimfolk_PracticeWalk", "Rimfolk_StoresSneak" })
            C(jobs.Contains("<defName>" + j + "</defName>"), $"the job {j}");
        C(Regex.Matches(jobs, "ColonyCouncil.JobDriver_HarmWalk").Count == 2 && Code("Harm/HarmSix.cs").Contains("else HarmSeven.Arrived(p, def);"), "each a walk out, a while there, an end");

        // The words.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_HarmSeven.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes or exclamation marks");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_Impact_")).All(kv => !Regex.IsMatch(kv.Value, @"\d")), "the sheets' lines are words, no numbers");
        foreach (var a in new[] { "Tribal", "Medieval", "Industrial", "Spacer" })
            C(keyed.ContainsKey("CC_HarmSeven_Challenge_" + a), $"the challenge by the people's means: {a}");
        foreach (var w in new[] { "Fire", "Ice", "Storm" })
            C(keyed.ContainsKey("CC_HarmSeven_Mote_Working_" + w), $"the working's words: {w}");
        foreach (var t in new[] { "frost", "chill", "ankle", "scrape" })
            C(keyed.ContainsKey("CC_HarmSeven_Mote_Lost_" + t), $"the lost one's words: {t}");
        var allKeys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                allKeys.Add(el.Name.LocalName);
        foreach (var kv in mine)
            foreach (Match m in Regex.Matches(kv.Value, "\"(CC_[A-Za-z_]+)\""))
            {
                string k = m.Groups[1].Value;
                if (k.EndsWith("_")) continue;
                C(allKeys.Contains(k), $"{kv.Key}: {k} exists");
            }
        C(allKeys.Contains("CC_Harm_Decree_Gates") && allKeys.Contains("CC_Harm_Decree_OneFaith"), "the two decrees' lines");

        // Said over a head: one short sentence each, and every pool asked for with lines.
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[d.Element("defName")!.Value] = d.Descendants("li").Select(li => li.Value).ToList();
        string linesFile = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_HarmSeven_Lines.xml"));
        var asked = mine.Values.SelectMany(v => Regex.Matches(v, "\"(Rimfolk_Pool_[A-Za-z]+)\"").Select(m => m.Groups[1].Value)).Distinct()
            .Where(p => linesFile.Contains("<defName>" + p + "</defName>")).ToList();
        C(asked.Count >= 50, $"the new pools are asked for ({asked.Count})");
        foreach (Match m in Regex.Matches(linesFile, "<defName>(Rimfolk_Pool_[A-Za-z]+)</defName>"))
            C(asked.Contains(m.Groups[1].Value), $"{m.Groups[1].Value} is said somewhere");
        foreach (var pool in asked)
        {
            C(pools.TryGetValue(pool, out var lines) && lines.Count >= 6, $"{pool} exists with six lines or more");
            if (lines == null) continue;
            foreach (var line in lines)
            {
                string body = Regex.Replace(line, @"^\s*\[[^\]]*\]\s*", "").Trim();
                int cut = -1;
                for (int i = 12; i < body.Length; i++)
                    if (body[i] == '.' || body[i] == '!' || body[i] == '?')
                    {
                        cut = i;
                        break;
                    }
                C(cut < 0 || cut == body.Length - 1, $"{pool}: '{body}' is one sentence");
                C(body.Length <= 90 && !body.Contains("!") && !body.Contains("—"), $"{pool}: '{body}' is short, with no exclamation mark or em dash");
            }
        }

        string review = File.ReadAllText(Path.Combine(root, "docs", "harm-review-proposals.md"));
        C(Regex.Matches(review, @"\*Built \(harm, part 7").Count == 31, "the review marks the thirty-one built");
        var proposals = Regex.Matches(review, @"^\d+\. \*\*", RegexOptions.Multiline).Count;
        C(proposals == 94 && Regex.Matches(review, @"\*Built \(harm, parts? \d").Count == 94, $"every one of the review's proposals is built ({proposals})");
        C(File.ReadAllText(Path.Combine(root, "DESIGN.md")).Contains("### Harm, part 7"), "DESIGN.md has them");
        return n;
    }

    private static string Pascal(string key) => char.ToUpperInvariant(key[0]) + key.Substring(1);

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
