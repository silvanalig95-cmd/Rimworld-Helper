using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // A camp with more to say: what its people ask of the colony (a request by letter, CampAsk.theyAsk: the drovers would
    // water their herd at the colony's water, the healers' apprentice would see the colony's sick), the errands they walk
    // on into the colony when an ask, a request or a council answer sends them (to the gate, the water, the fields, the
    // graves, a colonist's side), and the effects the asks and the camp's dilemmas use: goods handed over from the camp's
    // own stores, one of the kind's good turns done when asked, one of them taken into the colony, the herd that strayed
    // into the colony's fields, and the camp moving on early in peace.
    public partial class Camp
    {
        /// <summary>The day they last asked something of the colony (one request every other day at most).</summary>
        public int lastRequestDay = -99;

        /// <summary>The chance on a day's event that they ask one of their requests, when one is open.</summary>
        public const float RequestChance = 0.35f;

        // An errand of theirs into the colony (DilemmaEffect_CampComes): who goes, where, what they do there, when.
        public List<Pawn> errandPeople = new List<Pawn>();
        public string errandTo;
        public string errandDoing;
        public IntVec3 errandCell = IntVec3.Invalid;
        /// <summary>The colonist they go to (to "A": the sick one the healer sees).</summary>
        public Pawn errandFor;
        public bool errandHerd;
        public int errandFrom = -1, errandUntil = -1;
        public bool errandArrived, errandDone;
        /// <summary>What they say there, filled in when it was set going.</summary>
        public List<string> errandLines = new List<string>();
        /// <summary>Done when the errand is over, if they got there: one of the kind's good turns ("milk", "survey"), goods set out ("found").</summary>
        public string errandHelp, errandGive;
        public int errandGiveCount;

        public IEnumerable<CampAsk> Requests => kind?.asks?.Where(a => a != null && a.theyAsk) ?? Enumerable.Empty<CampAsk>();

        public CampAsk AskByKey(string key) => key == null ? null : kind?.asks?.FirstOrDefault(a => a?.key == key);

        /// <summary>A line of an ask's (a request, its refusal), tagged like the camp's other lines, with {X}, {A} and {COUNT}.</summary>
        public string PickAsk(List<string> lines, Pawn a, int count)
        {
            if (lines.NullOrEmpty()) return null;
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            CampWords.AddTags(tags, kind, faction, map);
            LetterTags.Add(tags, a, null, Speaker, faction, false);
            return Format(Flavor.Pick(lines, tags) ?? Flavor.PickText(lines), Speaker, a, count);
        }

        // ---------------- Requests ----------------

        /// <summary>
        /// The day's request: one their kind makes that can be granted now, asked by letter when the colony has had no
        /// question for a while. False if none came about today.
        /// </summary>
        private bool TryRequest()
        {
            if (GenDate.DaysPassed - lastRequestDay < 2 || DaysHere < 0.5f || !LetterGap.Open) return false;
            if (Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Camp c && c.campId == id && !c.resolved)) return false;
            var open = Requests.Where(a => !a.askTexts.NullOrEmpty() && AskBlocked(a) == null).ToList();
            if (open.Count == 0) return false;
            var ask = open.RandomElement();
            lastRequestDay = GenDate.DaysPassed;
            var who = AskColonist(ask);
            int cost = AskCost(ask);
            string text = PickAsk(ask.askTexts, who, cost);
            if (text.NullOrEmpty()) return false;
            if (!ask.tip.NullOrEmpty()) text += "\n\n" + Format(ask.tip, Speaker, who, cost);
            Ask("request", Text("CC_CampAsk_RequestTitle"), text, who, 1f, ask.key);
            return true;
        }

        /// <summary>The colony says no to a request (or let its letter go unanswered: a smaller slight). Returns what's told.</summary>
        public string Refuse(CampAsk a, bool byDefault)
        {
            if (a == null || !CanTalk) return null;
            string mark = a.once ? a.key : a.key + "@" + GenDate.DaysPassed;
            if (!asksMade.Contains(mark)) asksMade.Add(mark);
            float t = byDefault ? a.refuseTension * 0.5f : a.refuseTension;
            if (t != 0f) AddTension(t);
            if (a.refuseGoodwill > 0 && !byDefault) LoseGoodwill(a.refuseGoodwill);
            var who = AskColonist(a);
            return PickAsk(a.refused, who, AskCost(a)) ?? Text("CC_CampAsk_Refused");
        }

        // ---------------- Errands into the colony ----------------

        public bool ErrandSet => errandUntil > 0;

        public bool OnErrand(Pawn p) => ErrandSet && p != null && errandPeople.Contains(p);

        /// <summary>Under way now (or about to be): its hours have come and it isn't over.</summary>
        public bool ErrandLive => ErrandSet && Now >= errandFrom && Now <= errandUntil;

        /// <summary>Who goes: the one who speaks for them, their healer, their trader, their best hand, or all the grown-ups.</summary>
        public List<Pawn> ErrandPeople(string who, int count)
        {
            var here = Here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile() && !p.InMentalState).ToList();
            if (here.Count == 0) return here;
            IEnumerable<Pawn> order;
            switch (who)
            {
                case "healer":
                    order = here.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0);
                    break;
                case "trader":
                    order = here.OrderByDescending(p => p == trader ? 1 : 0).ThenByDescending(p => p == Speaker ? 1 : 0);
                    break;
                case "crafter":
                    order = here.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0);
                    break;
                case "all":
                    return here;
                default:
                    order = here.OrderByDescending(p => p == Speaker ? 1 : 0).ThenBy(p => p == trader ? 1 : 0);
                    break;
            }
            return order.Take(Mathf.Clamp(count, 1, 8)).ToList();
        }

        /// <summary>Where an errand goes, or an invalid cell when there's nowhere to go (no water near the colony, no graves).</summary>
        public IntVec3 ErrandCell(string to, Pawn a, Pawn walker)
        {
            if (map == null) return IntVec3.Invalid;
            try
            {
                switch (to)
                {
                    case "gate": return PlaySpots.GateCell(map, walker);
                    case "water":
                    {
                        // A well of the colony's own where a mod adds one, else shallow water near the colony.
                        var well = Avail.ColonyBuildings(map, d => d.defName.Contains("Well") && !d.defName.Contains("Wellness") && !d.defName.Contains("Wellbeing"))
                            .OrderBy(b => b.Position.DistanceToSquared(center)).FirstOrDefault();
                        if (well != null)
                        {
                            var c = PlaySpots.FreeCellNear(well.InteractionCell.IsValid ? well.InteractionCell : well.Position, map, walker);
                            if (c.IsValid) return c;
                        }
                        return PlaySpots.WaterCell(map, walker);
                    }
                    case "fields": return PlaySpots.FieldCell(map, walker);
                    case "graves":
                    {
                        var grave = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(g => g.Spawned && g.HasCorpse)
                            .OrderBy(g => g.Position.DistanceToSquared(center)).FirstOrDefault();
                        return grave == null ? IntVec3.Invalid : PlaySpots.FreeCellNear(grave.Position, map, walker);
                    }
                    case "colony": return PlaySpots.OpenCell(map, walker);
                    case "A": return a != null && a.Spawned && a.Map == map && !a.Dead ? a.Position : IntVec3.Invalid;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp errand place " + to, e);
            }
            return IntVec3.Invalid;
        }

        /// <summary>Sets an errand going (one at a time: a new one takes the place of the old). False when it can't go.</summary>
        public bool StartErrand(DilemmaEffect_CampComes e, Pawn a)
        {
            if (e == null || map == null || !Settled) return false;
            var people = ErrandPeople(e.who, e.count);
            if (people.Count == 0) return false;
            var cell = ErrandCell(e.to, a, people[0]);
            if (!cell.IsValid) return false;
            EndErrand();
            errandPeople = people;
            errandCell = cell;
            errandTo = e.to;
            errandDoing = e.doing;
            errandFor = e.to == "A" ? a : null;
            errandHerd = e.herd && herd.Count > 0;
            int hour = GenLocalDate.HourOfDay(map);
            int wait = hour >= e.when.min && hour < e.when.max ? 0 : (e.when.min - hour + 24) % 24 * GenDate.TicksPerHour;
            errandFrom = Now + wait;
            // Their hours there, and the walk there and back.
            errandUntil = errandFrom + Mathf.RoundToInt((e.hours.RandomInRange + 1f) * GenDate.TicksPerHour);
            errandLines = e.lines?.Where(l => !l.NullOrEmpty()).Select(l => Format(l, people[0], a)).ToList() ?? new List<string>();
            errandHelp = e.help;
            errandGive = e.give;
            errandGiveCount = e.give.NullOrEmpty() ? 0 : Mathf.RoundToInt(e.giveCount * (e.give == "found" || e.give == "silver" ? LivingEvents.WealthFactor(map) : 1f));
            errandArrived = false;
            errandDone = false;
            return true;
        }

        public void EndErrand()
        {
            errandPeople.Clear();
            errandCell = IntVec3.Invalid;
            errandFor = null;
            errandHerd = false;
            errandFrom = errandUntil = -1;
            errandLines.Clear();
            errandHelp = errandGive = null;
            errandGiveCount = 0;
        }

        /// <summary>Every hour: an errand whose time is up (or a camp no longer settled) is over.</summary>
        private void WatchErrand()
        {
            if (!ErrandSet) return;
            errandPeople.RemoveAll(p => p == null || p.Dead || !Here.Contains(p));
            bool strayed = errandDoing == "stray";
            if (Now > errandUntil && Settled && errandArrived && !strayed) ErrandOver();
            if (Now > errandUntil || !Settled || (errandPeople.Count == 0 && !strayed)) EndErrand();
        }

        /// <summary>Their errand done: what it promised (a share of what they dug, the herd's milk, a seam marked in the rock).</summary>
        private void ErrandOver()
        {
            try
            {
                if (!errandGive.NullOrEmpty() && errandGiveCount > 0)
                {
                    int n = GiveGoods(errandGive, errandGiveCount, null);
                    if (n > 0)
                    {
                        string key = "CC_CampErrand_Gave_" + (errandDoing ?? "visit");
                        if (!key.CanTranslate()) key = "CC_CampErrand_Gave_visit";
                        Note(Format(key.Translate().RawText, errandPeople.FirstOrDefault(), errandFor, n), MessageTypeDefOf.PositiveEvent, Importance.Notable);
                    }
                }
                if (!errandHelp.NullOrEmpty() && CanHelpNow(errandHelp)) HelpNow(errandHelp);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp errand over", e);
            }
            errandHelp = errandGive = null;
        }

        /// <summary>One of them on the errand: off to its place, or at the colonist's side, while its hours last.</summary>
        public Job ErrandJob(Pawn p)
        {
            if (!OnErrand(p) || Now < errandFrom) return null;
            if (Now > errandUntil || MapFacts.ThreatOn(map))
            {
                EndErrand();
                return null;
            }
            if (errandTo == "A")
            {
                var c = errandFor;
                if (c == null || c.Dead || !c.Spawned || c.Map != map || (errandDone && errandDoing == "tend"))
                {
                    EndErrand();
                    return null;
                }
                if (!p.CanReach(c, PathEndMode.Touch, Danger.Some)) return null;
                return JobMaker.MakeJob(CampDefOf.Council_CampErrandTo, c);
            }
            // Each of them a little apart, round the place.
            int i = Mathf.Max(0, errandPeople.IndexOf(p));
            var spot = i == 0 ? errandCell : PlaySpots.RingCell(map, errandCell, i, p, null);
            if (!spot.IsValid || !p.CanReach(spot, PathEndMode.OnCell, Danger.Some)) spot = errandCell;
            if (!p.CanReach(spot, PathEndMode.OnCell, Danger.Some))
            {
                EndErrand();
                return null;
            }
            return JobMaker.MakeJob(CampDefOf.Council_CampErrand, spot, errandCell);
        }

        /// <summary>A spell of the errand done: the first arrival is told, a line now and then, a healer's look at the sick.</summary>
        public void ErrandWorked(Pawn p, Job job)
        {
            if (!OnErrand(p)) return;
            if (!errandArrived)
            {
                errandArrived = true;
                string key = "CC_CampErrand_Arrived_" + (errandDoing ?? "visit");
                if (!key.CanTranslate()) key = "CC_CampErrand_Arrived_visit";
                Note(Format(key.Translate().RawText, p, errandFor), MessageTypeDefOf.NeutralEvent, Importance.Notable, p);
            }
            if (errandLines.Count > 0 && Rand.Chance(0.35f)) Speech.Say(p, errandLines.RandomElement());
            if (errandTo != "A" || errandDone || !(job?.targetA.Thing is Pawn c) || c.Dead || !p.Position.InHorDistOf(c.Position, 3f)) return;
            // together: the errand's job walked them to the colonist's side, and it ends if the colonist walks off.
            errandDone = true;
            Warm(c, 2);
            if (errandDoing == "prayover")
            {
                // A prayer at their side: no cure, but an everyday ailment a little easier, and kindly meant.
                var eased = HappeningWorker_Setting.Ailment(c);
                if (eased != null) eased.Severity = Mathf.Max(0.01f, eased.Severity * 0.8f);
                CouncilGameComponent.GiveThought(c, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Camp_PilgrimBlessing"), p);
                return;
            }
            if (errandDoing != "tend") return;
            try
            {
                var med = p.skills?.GetSkill(SkillDefOf.Medicine);
                bool healer = med != null && !med.TotallyDisabled && med.Level >= 3 && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor);
                if (!healer) return;
                if (c.health.HasHediffsNeedingTend()) TendUtility.DoTend(p, c, null);
                var ill = LifeChecks.Illness(c);
                if (ill != null) ill.Severity = Mathf.Max(Mathf.Min(ill.Severity, 0.02f), ill.Severity - 0.08f);
                var ail = HappeningWorker_Setting.Ailment(c);
                if (ail != null) ail.Severity = Mathf.Max(0.01f, ail.Severity * 0.6f);
                helps++;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp errand tend", e);
            }
        }

        /// <summary>For the week's page: what their errand is, while it's still to come or under way.</summary>
        public string ErrandText()
        {
            if (!ErrandSet) return null;
            string key = "CC_CampErrand_Week_" + (errandDoing ?? "visit");
            if (!key.CanTranslate()) key = "CC_CampErrand_Week_visit";
            return Format(key.Translate().RawText, errandPeople.FirstOrDefault(), errandFor);
        }

        // ---------------- Goods, good turns, joining, moving on ----------------

        /// <summary>
        /// Some of the camp's own goods set out for the colony at the spot nearest it: meat, wood, leather, food, whatever
        /// they found digging (silver, jade, gold), silver from their purse, or a thing by name. What they're short of comes
        /// as <paramref name="fallback"/> when given. Returns how many.
        /// </summary>
        public int GiveGoods(string what, int count, string fallback)
        {
            if (map == null || count <= 0) return 0;
            int given = 0;
            if (what == "found")
            {
                var jade = DefDatabase<ThingDef>.GetNamedSilentFail("Jade");
                var gold = DefDatabase<ThingDef>.GetNamedSilentFail("Gold");
                float roll = Rand.Value;
                var def = roll < 0.2f && gold != null ? gold : roll < 0.45f && jade != null ? jade : ThingDefOf.Silver;
                int n = def == ThingDefOf.Silver ? count : Mathf.Max(2, count / 6);
                return Place(def, n);
            }
            if (what == "silver")
            {
                int have = trader?.inventory?.innerContainer?.TotalStackCountOfDef(ThingDefOf.Silver) ?? 0;
                if (have > 0)
                {
                    trader.inventory.RemoveCount(ThingDefOf.Silver, Mathf.Min(have, count));
                    traderSilver = -1;
                }
                return Place(ThingDefOf.Silver, count);
            }
            Func<Thing, bool> match;
            switch (what)
            {
                case "meat": match = t => t.def.IsMeat; break;
                case "wood": match = t => t.def == ThingDefOf.WoodLog; break;
                case "leather": match = t => t.def.IsLeather; break;
                case "food": match = IsRawFood; break;
                default:
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(what ?? "");
                    match = t => def != null && t.def == def;
                    break;
                }
            }
            var mine = Goods().Where(match).ToList();
            if (mine.Count > 0) given = GiveAway(mine, count);
            if (given < count && !fallback.NullOrEmpty())
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(fallback);
                if (def != null) given += Place(def, count - given);
            }
            return given;
        }

        private int Place(ThingDef def, int n)
        {
            int placed = 0;
            var spot = share.IsValid ? share : FireCell;
            while (n > 0)
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                t.stackCount = Mathf.Min(n, def.stackLimit);
                n -= t.stackCount;
                if (!GenPlace.TryPlaceThing(t, spot, map, ThingPlaceMode.Near, out var put)) break;
                put?.SetForbidden(false, false);
                placed += t.stackCount;
            }
            return placed;
        }

        /// <summary>Whether one of the kind's good turns could be done now when asked (something to mend, ground not yet surveyed).</summary>
        public bool CanHelpNow(string h)
        {
            if (map == null || !Settled) return false;
            switch (h)
            {
                case "repair":
                    return !RepairPending && map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer)
                        .Any(t => t is Building b && !(b is Frame) && b.def.useHitPoints && b.HitPoints < b.MaxHitPoints * CampLookMath.RepairBelow);
                case "survey":
                    return surveys == 0;
                default:
                    return true;
            }
        }

        /// <summary>One of the kind's good turns, done because the colony asked (it's told as the good turn always is).</summary>
        public bool HelpNow(string h)
        {
            try
            {
                if (!DoHelp(h)) return false;
                helps++;
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp help asked " + h, e);
                return false;
            }
        }

        /// <summary>One of them taken into the colony by the council's answer (not hired, not by their own letter).</summary>
        public bool Welcome(Pawn p)
        {
            if (p == null || p.Dead || !CanTalk || !Here.Contains(p)) return false;
            if (p == trader)
            {
                p.mindState.wantsToTradeWithColony = false;
                if (p.trader != null) p.trader.traderKind = null;
                trader = null;
            }
            errandPeople.Remove(p);
            p.ownership?.UnclaimAll();
            if (!Arrivals.Join(p, map)) return false;
            willing.Remove(p);
            if (!joined.Contains(p)) joined.Add(p);
            return true;
        }

        /// <summary>
        /// Some of their herd wandered into the colony's fields (a dilemma): they stay there, among the crops, until
        /// someone sends them back or a day goes by. Uses the errand's place for the herd alone.
        /// </summary>
        public bool StrayHerd(IntVec3 field)
        {
            if (herd.Count == 0 || !field.IsValid || ErrandSet) return false;
            errandPeople.Clear();
            errandCell = field;
            errandTo = "fields";
            errandDoing = "stray";
            errandFor = null;
            errandHerd = true;
            errandFrom = Now;
            errandUntil = Now + GenDate.TicksPerDay;
            errandLines.Clear();
            errandArrived = true;
            errandDone = false;
            return true;
        }

        public bool Strayed => ErrandSet && errandDoing == "stray";

        /// <summary>The crops the strayed herd ate (or already had): the ripest in the field where they stand. Returns how many.</summary>
        public int EatCrops(int count)
        {
            if (map == null || count <= 0 || !errandCell.IsValid) return 0;
            var crops = GenRadial.RadialCellsAround(errandCell, 9f, true)
                .Where(c => c.InBounds(map) && c.GetZone(map) is Zone_Growing)
                .Select(c => c.GetPlant(map)).Where(p => p != null && p.def.plant != null && p.def.plant.Harvestable && p.Growth > 0.2f)
                .Distinct().OrderByDescending(p => p.Growth).Take(count).ToList();
            if (crops.Count == 0) return 0;
            try
            {
                Setbacks.Notify_CropLost(crops[0], false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp crops lost", e);
            }
            foreach (var p in crops) p.Destroy();
            return crops.Count;
        }

        // ---------------- Saving ----------------

        private void ExposeRequests()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                errandPeople.RemoveAll(p => p == null || p.Discarded || p.Dead);
                if (errandFor != null && (errandFor.Discarded || errandFor.Dead)) errandFor = null;
            }
            Scribe_Values.Look(ref lastRequestDay, "lastRequestDay", -99);
            Scribe_Collections.Look(ref errandPeople, "errandPeople", LookMode.Reference);
            Scribe_Values.Look(ref errandTo, "errandTo");
            Scribe_Values.Look(ref errandDoing, "errandDoing");
            Scribe_Values.Look(ref errandCell, "errandCell", IntVec3.Invalid);
            Scribe_References.Look(ref errandFor, "errandFor");
            Scribe_Values.Look(ref errandHerd, "errandHerd");
            Scribe_Values.Look(ref errandFrom, "errandFrom", -1);
            Scribe_Values.Look(ref errandUntil, "errandUntil", -1);
            Scribe_Values.Look(ref errandArrived, "errandArrived");
            Scribe_Values.Look(ref errandDone, "errandDone");
            Scribe_Collections.Look(ref errandLines, "errandLines", LookMode.Value);
            Scribe_Values.Look(ref errandHelp, "errandHelp");
            Scribe_Values.Look(ref errandGive, "errandGive");
            Scribe_Values.Look(ref errandGiveCount, "errandGiveCount");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                errandPeople ??= new List<Pawn>();
                errandLines ??= new List<string>();
                errandPeople.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>The camp on the colony's map that an answer is about: settled, at peace, here to deal with.</summary>
    public static class CampOnMap
    {
        public static Camp Of(Map map)
        {
            if (map == null || !CampTracker.Enabled) return null;
            var c = CampTracker.Instance?.camp;
            return c != null && c.map == map && c.Settled && c.CanTalk ? c : null;
        }
    }

    /// <summary>
    /// Some of the camp's people walk into the colony on an errand (an ask, a request, an answer): <see cref="who"/>
    /// (the one who speaks for them unless given: "healer", "trader", "crafter", "all") and up to <see cref="count"/>
    /// of them go <see cref="to"/> (the gate, the water, the fields, the graves, the open ground in the middle of the
    /// colony, or A's side) in the hours <see cref="when"/>, stay <see cref="hours"/>, and walk back; with
    /// <see cref="herd"/>, their animals come along (watered at the colony's water). <see cref="doing"/> names what
    /// they're about there (CC_CampErrand_Arrived_* tells it when they arrive; "tend" at a sick colonist's side has their
    /// healer see to them), and <see cref="lines"/> are said there now and then.
    /// </summary>
    public class DilemmaEffect_CampComes : DilemmaEffect
    {
        public string to = "gate";
        public string who;
        public int count = 1;
        public bool herd;
        public FloatRange hours = new FloatRange(2f, 3f);
        public IntRange when = new IntRange(8, 17);
        public string doing = "visit";
        [MustTranslate] public List<string> lines;
        /// <summary>When it's over (if they got there): one of the kind's good turns ("milk", "survey"), and goods set out ("found", 40).</summary>
        public string help;
        public string give;
        public int giveCount;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null) return "CC_CampAsk_NotNow".Translate().ToString();
            if (herd && c.herd.Count == 0) return "CC_CampErrand_NoHerd".Translate().ToString();
            if (c.Strayed) return "CC_CampErrand_Busy".Translate().ToString();
            if (!help.NullOrEmpty() && !c.CanHelpNow(help))
            {
                string why = "CC_CampHelp_No_" + help;
                return (why.CanTranslate() ? why : "CC_CampHelp_No").Translate().ToString();
            }
            var who = c.ErrandPeople(this.who, count).FirstOrDefault();
            if (who == null) return "CC_CampAsk_NotNow".Translate().ToString();
            if (c.ErrandCell(to, ctx.a, who).IsValid) return null;
            string key = "CC_CampErrand_No_" + to;
            return (key.CanTranslate() ? key : "CC_CampErrand_No_gate").Translate().ToString();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            // apart: an answer to the camp; their people walk over themselves, and what's done at someone's side waits for them to be there.
            c.StartErrand(this, ctx.a);
        }
    }

    /// <summary>
    /// The camp hands over some of its own goods, set out at the spot nearest the colony (see <see cref="Camp.GiveGoods"/>):
    /// <see cref="what"/> ("meat", "wood", "leather", "food", "found", "silver", or a thing's defName), <see cref="count"/>
    /// of it, made up with <see cref="fallback"/> when their stores are short.
    /// </summary>
    public class DilemmaEffect_CampGives : DilemmaEffect
    {
        public string what = "meat";
        public int count = 20;
        public string fallback;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            try
            {
                c.GiveGoods(what, Mathf.RoundToInt(count * (what == "silver" || what == "found" ? LivingEvents.WealthFactor(ctx.map) : 1f)), fallback);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp gives " + what, e);
            }
        }
    }

    /// <summary>One of the kind's good turns, done now because the colony asked: "repair" (the smith mends a building), "survey", "milk"...</summary>
    public class DilemmaEffect_CampHelp : DilemmaEffect
    {
        public string help;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null) return "CC_CampAsk_NotNow".Translate().ToString();
            if (c.CanHelpNow(help)) return null;
            string key = "CC_CampHelp_No_" + help;
            return (key.CanTranslate() ? key : "CC_CampHelp_No").Translate().ToString();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null || help.NullOrEmpty() || (chance < 1f && !Rand.Chance(chance))) return;
            c.HelpNow(help);
        }
    }

    /// <summary>The stranger (X), one of the camp's people, joins the colony with the answer (their camp's books keep it).</summary>
    public class DilemmaEffect_CampJoin : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            var x = ctx?.stranger;
            return c == null || x == null || x.Dead || !c.Here.Contains(x) ? "CC_Stranger_Gone".Translate(x?.LabelShort ?? "?").ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null || ctx.stranger == null || (chance < 1f && !Rand.Chance(chance))) return;
            if (c.Welcome(ctx.stranger)) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World,
                "CC_CampDlm_Joined".Translate(ctx.stranger.Named("X"), c.Label.Named("NAME"), (c.faction?.Name ?? "").Named("FACTION")).Resolve(), ctx.stranger));
        }
    }

    /// <summary>
    /// The camp's herd that strayed into the colony's fields: <see cref="eaten"/> crops are lost (the ripest where they
    /// stand), and with <see cref="home"/> the beasts are walked back to their pasture now.
    /// </summary>
    public class DilemmaEffect_CampHerd : DilemmaEffect
    {
        public int eaten;
        public bool home = true;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null || !c.Strayed || (chance < 1f && !Rand.Chance(chance))) return;
            int n = c.EatCrops(eaten);
            if (n > 0) Messages.Message("CC_CampDlm_CropsEaten".Translate(n, c.Label).Resolve(), new LookTargets(new TargetInfo(c.errandCell, c.map)), MessageTypeDefOf.NegativeEvent, false);
            if (home) c.EndErrand();
            else c.errandUntil = Mathf.Min(c.errandUntil, Find.TickManager.TicksGame + GenDate.TicksPerHour * 4);
        }
    }

    /// <summary>The camp moves on early, of its own accord and in peace (no gifts left behind: their stay was cut short).</summary>
    public class DilemmaEffect_CampMovesOn : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null || (chance < 1f && !Rand.Chance(chance))) return;
            c.Leave("moved");
        }
    }

    /// <summary>The camp's fee is settled another way (a beast, a day's work, forgiven): the next falls due five days on.</summary>
    public class DilemmaEffect_CampFee : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.Of(ctx?.map);
            if (c == null || !c.termFee) return;
            c.feeLate = false;
            c.nextFeeTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 5;
        }
    }
}
