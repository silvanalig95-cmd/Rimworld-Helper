using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    public enum CampPhase
    {
        /// <summary>Walking in to their spot.</summary>
        Coming,
        /// <summary>Putting the camp up.</summary>
        Building,
        Living,
        Leaving,
        /// <summary>At war with the colony, holding their camp.</summary>
        Fighting,
        Over
    }

    /// <summary>
    /// A group camped on the colony's map: who they are, where, for how long, what they've built and taken, the terms
    /// the colony set, and how tense things are. One at a time (see <see cref="CampTracker"/>). Their everyday work is
    /// done by the pawns themselves (<see cref="CampWork"/>); this keeps the books, tells the story and decides when
    /// they go.
    /// </summary>
    public partial class Camp : IExposable
    {
        public int id;
        public CampKindDef kind;
        public Map map;
        public Faction faction;
        public Lord lord;
        public List<Pawn> members = new List<Pawn>();
        public Pawn leader;
        public Pawn trader;
        public IntVec3 center = IntVec3.Invalid;
        public IntVec3 stash = IntVec3.Invalid;
        public IntVec3 share = IntVec3.Invalid;
        public IntVec3 colonyCenter = IntVec3.Invalid;
        public string place;
        public List<CampPlan> plans = new List<CampPlan>();

        public CampPhase phase;
        public int startTick;
        public int arrivedTick = -1;
        public int stayTicks;
        public int untilTick = int.MaxValue;
        public int buildDeadline = -1;
        public int builtTick = -1;
        public int leftTick = -1;

        public float tension;
        /// <summary>Highest tension mark the colony was told about (40, 70).</summary>
        public int warned;
        /// <summary>Dried food they brought, in meals.</summary>
        public int provisions;

        // Terms the colony set (and they took).
        public bool termFee, termShare, termBoundary, termLimit;
        public int fee;
        public int nextFeeTick = -1;
        /// <summary>Today they bend a term: work on the colony's side, keep the meat, pay late.</summary>
        public bool bendBoundary, skipShare, feeLate;

        public bool asked;
        public int refusals;
        public bool extendAsked;
        public bool extended;

        // What they did.
        public int treesFelled, animalsHunted, tradeDays, gifts, helps, timesTaken;
        public int dayKey = -1, dayHunts, dayChops, huntsToday, chopsToday, eventHour = 12, lastEventDay = -1, lastCookDay = -1, lastTradeDay = -1, lastJoinAskTick = -999999;
        public int traderSilver = -1;
        /// <summary>Hours in a row none of them could reach their camp.</summary>
        public int stuckHours;
        public List<Pawn> willing = new List<Pawn>();
        public List<Pawn> joined = new List<Pawn>();
        public List<Pawn> arrested = new List<Pawn>();
        /// <summary>Animals being hunted, and those killed whose bodies they still mean to fetch.</summary>
        public List<Pawn> prey = new List<Pawn>();
        public List<Pawn> kills = new List<Pawn>();
        public List<Thing> trees = new List<Thing>();
        public List<IntVec3> felled = new List<IntVec3>();
        public List<int> felledTicks = new List<int>();
        /// <summary>The camp's own goods on the map, by thing id: only these are ever theirs to use.</summary>
        public List<int> owned = new List<int>();
        /// <summary>Why it ended (time, asked, forced, raid, weather, hunger, crowded, fallout, harmed, arrest, fight, empty).</summary>
        public string ending;
        public bool told;

        [Unsaved] private HashSet<int> ownedSet;

        private static int Now => Find.TickManager.TicksGame;

        // ---------------- Who and where ----------------

        public string Label => kind?.label ?? "camp";

        public bool Settled => phase == CampPhase.Building || phase == CampPhase.Living;

        public bool Active => phase != CampPhase.Over;

        public bool LordAlive => lord != null && map != null && Find.Maps.Contains(map) && map.lordManager.lords.Contains(lord);

        /// <summary>Those of them still with the group on the map.</summary>
        public List<Pawn> Here => members.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map && lord != null && p.GetLord() == lord).ToList();

        public bool Has(Pawn p) => p != null && members.Contains(p);

        public float DaysHere => arrivedTick < 0 ? 0f : (Now - arrivedTick) / (float)GenDate.TicksPerDay;

        public IntVec3 FireCell => plans.FirstOrDefault(p => p.role == "fire")?.cell ?? center;

        public Building Fire => plans.FirstOrDefault(p => p.role == "fire")?.Built(map, faction);

        public bool AnyTerms => termFee || termShare || termBoundary || termLimit;

        private HashSet<int> OwnedSet => ownedSet ??= new HashSet<int>(owned);

        public bool Owns(Thing t) => t != null && OwnedSet.Contains(t.thingIDNumber);

        /// <summary>It's theirs now: kept out of the colony's hands (forbidden to colonists).</summary>
        public void Own(Thing t)
        {
            if (t == null) return;
            if (OwnedSet.Add(t.thingIDNumber)) owned.Add(t.thingIDNumber);
            if (t.Spawned) t.SetForbidden(true, false);
        }

        public void Disown(Thing t)
        {
            if (t == null) return;
            if (OwnedSet.Remove(t.thingIDNumber)) owned.Remove(t.thingIDNumber);
        }

        /// <summary>The camp's goods lying at the camp.</summary>
        public List<Thing> Goods()
        {
            var list = new List<Thing>();
            if (map == null || owned.Count == 0) return list;
            float r = CampSite.CoreRadius + 6;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
                if (t.Spawned && OwnedSet.Contains(t.thingIDNumber) && t.Position.InHorDistOf(center, r)) list.Add(t);
            return list;
        }

        /// <summary>Their blueprints and half-built frames.</summary>
        public List<Thing> Unfinished()
        {
            var list = new List<Thing>();
            if (map == null || faction == null) return list;
            float r = CampSite.CoreRadius + 4;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
                if (t.Faction == faction && t.Position.InHorDistOf(center, r)) list.Add(t);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
                if (t.Faction == faction && t.Position.InHorDistOf(center, r)) list.Add(t);
            return list;
        }

        public List<Building> Buildings() => plans.Where(p => p.placed).Select(p => p.Built(map, faction)).Where(b => b != null).Distinct().ToList();

        /// <summary>Is this cell on the colony's side of the camp (closer to the colony than the camp is)?</summary>
        public bool ColonySide(IntVec3 c) => colonyCenter.IsValid && c.DistanceToSquared(colonyCenter) < center.DistanceToSquared(colonyCenter);

        /// <summary>Somewhere they may take wood or game: near the camp, well clear of the colony's home area and zones.</summary>
        public bool MayWork(IntVec3 c)
        {
            if (map == null || !c.InBounds(map)) return false;
            float radius = termBoundary && !bendBoundary ? CampSite.WorkRadius * 0.6f : CampSite.WorkRadius;
            if (!c.InHorDistOf(center, radius)) return false;
            if (termBoundary && !bendBoundary && ColonySide(c)) return false;
            if (map.zoneManager.ZoneAt(c) != null) return false;
            var home = map.areaManager.Home;
            if (home != null)
                foreach (var x in GenRadial.RadialCellsAround(c, CampSite.HomeMargin, true))
                    if (x.InBounds(map) && home[x]) return false;
            return true;
        }

        // ---------------- Words ----------------

        public string DaysText => TextUtil.DaysString(Mathf.Max(GenDate.TicksPerHour, (untilTick == int.MaxValue ? stayTicks : untilTick - Now)));

        /// <summary>Camp text with {X} (whoever speaks for them, unless given), {A}, {FACTION}, {NAME}, {PLACE}, {DAYS}, {COUNT} and {THING}.</summary>
        public string Format(string s, Pawn x = null, Pawn a = null, int count = 0, string thing = null)
        {
            if (s.NullOrEmpty()) return "";
            var speaker = x ?? (leader != null && !leader.Dead ? leader : Here.FirstOrDefault() ?? members.FirstOrDefault());
            var colonist = a ?? (map != null ? IncidentWorker_Happening.Colonists(map).FirstOrDefault() : null);
            var args = new List<NamedArgument>
            {
                speaker != null ? speaker.Named("X") : "?".Named("X"),
                colonist != null ? colonist.Named("A") : "?".Named("A"),
                (faction?.Name ?? "").Named("FACTION"),
                Label.Named("NAME"),
                (place ?? "").Named("PLACE"),
                DaysText.Named("DAYS"),
                count.Named("COUNT"),
                (thing ?? "").Named("THING")
            };
            return s.Formatted(args.ToArray()).Resolve();
        }

        public string Text(string key, Pawn x = null, Pawn a = null, int count = 0, string thing = null) => Format(key.Translate().RawText, x, a, count, thing);

        public string Pick(List<string> lines, Pawn x = null, Pawn a = null)
        {
            if (lines.NullOrEmpty()) return null;
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            return Format(Flavor.Pick(lines, tags) ?? Flavor.PickText(lines), x, a);
        }

        public LookTargets Look => map != null && center.IsValid ? new LookTargets(new TargetInfo(center, map)) : LookTargets.Invalid;

        /// <summary>A letter about the camp; it also goes in the chronicle when it matters.</summary>
        public void Tell(string label, string text, LetterDef def, bool chronicle = true, Pawn about = null)
        {
            if (text.NullOrEmpty()) return;
            Find.LetterStack.ReceiveLetter(label, text, def, Look, faction);
            if (chronicle) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, label + ": " + FirstSentence(text), about));
        }

        public void Note(string text, MessageTypeDef type, Importance importance = Importance.Notable, Thing look = null)
        {
            if (text.NullOrEmpty()) return;
            Notify.Message(text, look != null && look.Spawned ? new LookTargets(look) : Look, type, importance);
        }

        private static string FirstSentence(string text)
        {
            if (text.NullOrEmpty()) return "";
            int cut = text.IndexOfAny(new[] { '.', '!', '?' });
            if (cut > 0 && cut < 220) return text.Substring(0, cut + 1);
            return text.Length > 220 ? text.Substring(0, 220) + "..." : text;
        }

        /// <summary>A choice for the colony, by letter (see <see cref="ChoiceLetter_Camp"/>).</summary>
        public void Ask(string what, string label, string text, Pawn pawn = null, float days = 1f)
        {
            var letter = (ChoiceLetter_Camp)LetterMaker.MakeLetter(label, text, CampDefOf.Council_CampLetter, Look, faction);
            letter.campId = id;
            letter.what = what;
            letter.pawn = pawn;
            int ticks = Mathf.RoundToInt(days * GenDate.TicksPerDay);
            letter.Text = text + "\n\n" + "CC_Camp_Deadline".Translate(TextUtil.DaysString(ticks), CampAnswers.DefaultLabel(this, what, pawn)).Resolve();
            letter.StartTimeout(ticks);
            Find.LetterStack.ReceiveLetter(letter);
        }

        // ---------------- Tension ----------------

        public void AddTension(float amount)
        {
            tension = Mathf.Clamp(tension + amount, 0f, 100f);
        }

        public string TensionWord => ("CC_Camp_Tension_" + (tension >= 70f ? "High" : tension >= 40f ? "Raised" : tension >= 15f ? "Some" : "Low")).Translate();

        private void CheckTension()
        {
            if (!Settled) return;
            if (tension >= 100f)
            {
                faction?.TryAffectGoodwillWith(Faction.OfPlayer, -8, true, false);
                Leave("fallout");
                return;
            }
            if (tension >= 70f && warned < 70)
            {
                warned = 70;
                Ask("tension", Text("CC_Camp_TensionHighTitle"), Text("CC_Camp_TensionHigh") + TookLine());
            }
            else if (tension >= 40f && warned < 40)
            {
                warned = 40;
                Ask("tension", Text("CC_Camp_TensionTitle"), Text("CC_Camp_Tension") + TookLine());
            }
        }

        /// <summary>What they've taken so far, for the letters.</summary>
        public string TookLine()
        {
            if (treesFelled == 0 && animalsHunted == 0) return "";
            return "\n\n" + "CC_Camp_Took".Translate(treesFelled, animalsHunted).Resolve();
        }

        // ---------------- Save ----------------

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Only what can still be found again after loading.
                members.RemoveAll(p => p == null || p.Discarded);
                willing.RemoveAll(p => p == null || p.Discarded || p.Dead);
                joined.RemoveAll(p => p == null || p.Discarded);
                arrested.RemoveAll(p => p == null || p.Discarded);
                prey.RemoveAll(p => p == null || p.Discarded || p.Dead);
                kills.RemoveAll(p => p == null || p.Discarded || p.Corpse == null || p.Corpse.Destroyed);
                trees.RemoveAll(t => t == null || t.Destroyed);
                if (leader != null && leader.Discarded) leader = null;
                if (trader != null && (trader.Discarded || trader.Dead)) trader = null;
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Defs.Look(ref kind, "kind");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref lord, "lord");
            Scribe_Collections.Look(ref members, "members", LookMode.Reference);
            Scribe_References.Look(ref leader, "leader");
            Scribe_References.Look(ref trader, "trader");
            Scribe_Values.Look(ref center, "center", IntVec3.Invalid);
            Scribe_Values.Look(ref stash, "stash", IntVec3.Invalid);
            Scribe_Values.Look(ref share, "share", IntVec3.Invalid);
            Scribe_Values.Look(ref colonyCenter, "colonyCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref place, "place");
            Scribe_Collections.Look(ref plans, "plans", LookMode.Deep);
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref arrivedTick, "arrivedTick", -1);
            Scribe_Values.Look(ref stayTicks, "stayTicks");
            Scribe_Values.Look(ref untilTick, "untilTick", int.MaxValue);
            Scribe_Values.Look(ref buildDeadline, "buildDeadline", -1);
            Scribe_Values.Look(ref builtTick, "builtTick", -1);
            Scribe_Values.Look(ref leftTick, "leftTick", -1);
            Scribe_Values.Look(ref tension, "tension");
            Scribe_Values.Look(ref warned, "warned");
            Scribe_Values.Look(ref provisions, "provisions");
            Scribe_Values.Look(ref termFee, "termFee");
            Scribe_Values.Look(ref termShare, "termShare");
            Scribe_Values.Look(ref termBoundary, "termBoundary");
            Scribe_Values.Look(ref termLimit, "termLimit");
            Scribe_Values.Look(ref fee, "fee");
            Scribe_Values.Look(ref nextFeeTick, "nextFeeTick", -1);
            Scribe_Values.Look(ref bendBoundary, "bendBoundary");
            Scribe_Values.Look(ref skipShare, "skipShare");
            Scribe_Values.Look(ref feeLate, "feeLate");
            Scribe_Values.Look(ref asked, "asked");
            Scribe_Values.Look(ref refusals, "refusals");
            Scribe_Values.Look(ref extendAsked, "extendAsked");
            Scribe_Values.Look(ref extended, "extended");
            Scribe_Values.Look(ref treesFelled, "treesFelled");
            Scribe_Values.Look(ref animalsHunted, "animalsHunted");
            Scribe_Values.Look(ref tradeDays, "tradeDays");
            Scribe_Values.Look(ref gifts, "gifts");
            Scribe_Values.Look(ref helps, "helps");
            Scribe_Values.Look(ref timesTaken, "timesTaken");
            Scribe_Values.Look(ref dayKey, "dayKey", -1);
            Scribe_Values.Look(ref dayHunts, "dayHunts");
            Scribe_Values.Look(ref dayChops, "dayChops");
            Scribe_Values.Look(ref huntsToday, "huntsToday");
            Scribe_Values.Look(ref chopsToday, "chopsToday");
            Scribe_Values.Look(ref eventHour, "eventHour", 12);
            Scribe_Values.Look(ref lastEventDay, "lastEventDay", -1);
            Scribe_Values.Look(ref lastCookDay, "lastCookDay", -1);
            Scribe_Values.Look(ref lastTradeDay, "lastTradeDay", -1);
            Scribe_Values.Look(ref lastJoinAskTick, "lastJoinAskTick", -999999);
            Scribe_Values.Look(ref traderSilver, "traderSilver", -1);
            Scribe_Values.Look(ref stuckHours, "stuckHours");
            Scribe_Collections.Look(ref willing, "willing", LookMode.Reference);
            Scribe_Collections.Look(ref joined, "joined", LookMode.Reference);
            Scribe_Collections.Look(ref arrested, "arrested", LookMode.Reference);
            Scribe_Collections.Look(ref prey, "prey", LookMode.Reference);
            Scribe_Collections.Look(ref kills, "kills", LookMode.Reference);
            Scribe_Collections.Look(ref trees, "trees", LookMode.Reference);
            Scribe_Collections.Look(ref felled, "felled", LookMode.Value);
            Scribe_Collections.Look(ref felledTicks, "felledTicks", LookMode.Value);
            Scribe_Collections.Look(ref owned, "owned", LookMode.Value);
            Scribe_Values.Look(ref ending, "ending");
            Scribe_Values.Look(ref told, "told");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                members ??= new List<Pawn>();
                plans ??= new List<CampPlan>();
                willing ??= new List<Pawn>();
                joined ??= new List<Pawn>();
                arrested ??= new List<Pawn>();
                prey ??= new List<Pawn>();
                kills ??= new List<Pawn>();
                trees ??= new List<Thing>();
                felled ??= new List<IntVec3>();
                felledTicks ??= new List<int>();
                owned ??= new List<int>();
                members.RemoveAll(p => p == null);
                willing.RemoveAll(p => p == null);
                joined.RemoveAll(p => p == null);
                arrested.RemoveAll(p => p == null);
                prey.RemoveAll(p => p == null);
                kills.RemoveAll(p => p == null);
                trees.RemoveAll(t => t == null);
                plans.RemoveAll(p => p == null || p.def == null);
                while (felledTicks.Count < felled.Count) felledTicks.Add(Now);
                ownedSet = null;
            }
        }
    }
}
