using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One at the colony with a tie to a people: who, and what ties them.</summary>
    public struct KinOfPeople
    {
        public Pawn pawn;
        public TieWay way;
        /// <summary>A colonist who came from that people (no tie of the ledger's: their own past).</summary>
        public bool cameFrom;
    }

    /// <summary>
    /// Kin hear of their town's hard year (the content update's B17, 20 November 2026). When a hard world event strikes a
    /// people, those at the colony with a tie to them (a ward or a hostage of theirs, one of theirs married into the colony,
    /// their envoy, a colonist who came from that people) hear of it by the next messenger to reach the colony (the
    /// couriers: CourierTracker calls <see cref="CourierCame"/>), or by the road within a few days (KinNewsMath.RoadDays):
    /// a letter from home tells it, and they are worried: a mood while it is fresh, and the worry on the heart's card while
    /// the event runs (<see cref="Worry"/>). One of the colonists among them may ask the council to send food or medicine home
    /// with the next courier, from the colony's stores, carried to the gate (Rimfolk_Dilemma_KinSendHome); what goes counts
    /// as the colony's help in the world event. The colony's own ward or envoy at the struck town writes of it, and may come
    /// home early from a plague town. Flavour and mood only: nothing here binds a family line (CLAUDE.md, the colony is the
    /// family). What has been told is kept on the world event itself (its flags), so nothing new is saved.
    /// </summary>
    public static class KinNews
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static bool On => (ColonyCouncilMod.Settings?.worldEvents ?? true) && WorldSituations.Instance != null;

        public const string WordFlag = "kinWord", AskDueFlag = "kinAskDue", AskedFlag = "kinAsked", AbroadFlag = "kinAbroad";

        // ================= Who has a tie to them =================

        /// <summary>Everyone at the colony's homes with a tie to this people, each once with the closest tie.</summary>
        public static List<KinOfPeople> KinOf(Faction f)
        {
            var list = new List<KinOfPeople>();
            if (f == null || f.IsPlayer) return list;
            void Add(Pawn p, TieWay w, bool came = false)
            {
                if (p == null || p.Dead || p.Discarded || !p.Spawned || !Homes.IsHome(p.Map) || p.RaceProps?.Humanlike != true) return;
                int i = list.FindIndex(k => k.pawn == p);
                if (i >= 0)
                {
                    if (w > list[i].way) list[i] = new KinOfPeople { pawn = p, way = w, cameFrom = list[i].cameFrom || came };
                    return;
                }
                list.Add(new KinOfPeople { pawn = p, way = w, cameFrom = came });
            }
            try
            {
                foreach (var w in Wards.With(f))
                    if (w != null && w.Theirs && w.stage == WardStage.Here) Add(w.pawn, w.Hostage ? TieWay.Hostage : TieWay.Ward);
                foreach (var t in Matches.With(f))
                {
                    var theirs = Matches.TheirsOf(t);
                    if (KinMemory.Ours(theirs)) Add(theirs, TieWay.Marriage);
                }
                var envoy = Envoys.With(f, out _);
                if (envoy?.envoy != null) Add(envoy.envoy, TieWay.Envoy);
                // Colonists who came from them: of one of Rimfolk's peoples' own kinds (a kind of theirs, theirs alone).
                if (Peoples.Of(f) != null && Find.FactionManager.AllFactionsListForReading.Count(x => x != null && x.def == f.def) == 1)
                    foreach (var home in Homes.All)
                        foreach (var p in home.mapPawns.FreeColonistsSpawned)
                            if (KinMemory.Ours(p) && p.kindDef?.defaultFactionDef == f.def)
                                Add(p, TieWay.None, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kin's news: who has a tie", e);
            }
            return list;
        }

        /// <summary>A hard year at a people: a bad world event running at one of theirs.</summary>
        public static bool Hard(WorldSituation s) => s != null && !s.ended && s.def != null && s.def.bad && s.ctx?.faction != null && !s.ctx.faction.IsPlayer;

        private static string TownOf(WorldSituation s) => !s.ctx.placeName.NullOrEmpty() ? s.ctx.placeName : s.ctx.faction?.Name ?? "?";

        // ================= The word comes =================

        /// <summary>A courier reached the colony (CourierTracker): it brings the word of each hard year not yet heard there.</summary>
        public static void CourierCame(CourierTrip t)
        {
            if (!On || t?.map == null || !Homes.IsHome(t.map)) return;
            try
            {
                foreach (var s in WorldSituations.Instance.Running.ToList())
                    if (Hard(s) && !s.Has(WordFlag)) Word(s, t.Bearers.FirstOrDefault());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kin's news: by the courier", e);
            }
        }

        /// <summary>
        /// The hour (Peoples/PeoplesTies.cs): word of a hard year that no courier brought comes by the road after a few
        /// days; an ask due is put to the council when it may be; the colony's own at a struck town write of it.
        /// </summary>
        public static void Hourly(int now)
        {
            if (!On) return;
            foreach (var s in WorldSituations.Instance.Running.ToList())
            {
                if (!Hard(s)) continue;
                try
                {
                    if (!s.Has(WordFlag) && KinNewsMath.ByRoad(s.DaysIn)) Word(s, null);
                    if (!s.Has(AbroadFlag)) Abroad(s);
                    if (s.Has(AskDueFlag) && !s.Has(AskedFlag)) TryAsk(s);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("kin's news: the hour", e);
                }
            }
        }

        /// <summary>
        /// The word of a hard year reaches those with a tie to its people: a letter from home at each home with any of them,
        /// a mood for each, and maybe an ask to send help (put to the council by the hour). Nothing when nobody has a tie.
        /// </summary>
        public static void Word(WorldSituation s, Pawn courier)
        {
            if (s == null || s.Has(WordFlag)) return;
            s.Set(WordFlag);
            var kin = KinOf(s.ctx.faction);
            if (kin.Count == 0) return;
            string town = TownOf(s);
            foreach (var home in kin.Select(k => k.pawn.Map).Distinct().ToList())
            {
                var here = kin.Where(k => k.pawn.Map == home).ToList();
                foreach (var k in here)
                    CouncilGameComponent.GiveThought(k.pawn, KinNewsDefOf.Rimfolk_KinWorried, null, -1f, "CC_Kin_Cause".Translate(town.Named("TOWN"), s.Label.Named("EVENT")).Resolve());
                var tags = new HashSet<string>();
                Flavor.AddEraTags(tags);
                tags.Add(courier != null ? "courier" : "road");
                tags.Add("kind:" + (s.def.kind ?? "other"));
                if (here.Count > 1) tags.Add("many");
                var first = here.OrderByDescending(k => k.way).First();
                tags.Add("tie:" + (first.cameFrom && first.way == TieWay.None ? "came" : first.way.ToString().ToLowerInvariant()));
                var lines = Lines("CC_Kin_Letter_");
                string text = Flavor.Pick(lines, tags) ?? Flavor.PickText(lines);
                if (text.NullOrEmpty()) continue;
                string names = TextUtil.JoinAnd(here.Select(k => k.pawn.LabelShort).ToList());
                text = text.Formatted(names.Named("NAMES"), first.pawn.Named("PAWN"), town.Named("TOWN"), (s.ctx.faction?.Name ?? "").Named("PEOPLE"), s.Label.Named("EVENT"),
                    (courier?.LabelShort ?? "").Named("COURIER")).Resolve();
                text = WorldSituationWorker.Sentences(text);
                Tell.Letter(NewsKind.World, Importance.Notable, home, "CC_Kin_Letter_Label".Translate(town.Named("TOWN")).Resolve(), text, LetterDefOf.NeutralEvent,
                    new LookTargets(here.Select(k => k.pawn).Take(3)), s.ctx.faction);
            }
            // One of the colonists among them may ask to send help home.
            if (KinNewsMath.SendFor(s.def.kind) != null && kin.Any(k => KinMemory.Ours(k.pawn)) && Rand.Chance(KinNewsMath.AskChance)) s.Set(AskDueFlag);
        }

        private static List<string> Lines(string prefix)
        {
            var lines = new List<string>();
            for (int i = 1; i <= 40; i++)
            {
                string key = prefix + i;
                if (key.CanTranslate()) lines.Add(key.Translate().RawText);
            }
            return lines;
        }

        // ================= The colony's own there =================

        /// <summary>The colony's ward or envoy at the struck town writes of it, and from a plague town may come home early.</summary>
        private static void Abroad(WorldSituation s)
        {
            if (s.place.kind != RefKind.Town) return;
            int town = s.place.id;
            var who = new List<(Pawn pawn, Action recall)>();
            foreach (var (h, w) in Wards.All())
                if (w != null && !w.Theirs && !w.Hostage && WardsAbroad.There(w) && w.town == town && w.pawn != null && !w.pawn.Dead)
                    who.Add((w.pawn, () => WardsAbroad.Recall(w)));
            foreach (var (h, a) in EnvoysAbroad.All())
                if (a != null && a.stage == EnvoyStage.There && a.town == town && a.colonist != null && !a.colonist.Dead)
                    who.Add((a.colonist, () => EnvoysAbroad.Recall(a)));
            if (who.Count == 0) return;
            // Written some days in, once they've seen it for themselves.
            if (s.DaysIn < 1f) return;
            s.Set(AbroadFlag);
            foreach (var (p, recall) in who)
            {
                bool home = KinNewsMath.Plague(s.def.kind) && Rand.Chance(KinNewsMath.ComeHomeChance);
                var tags = new HashSet<string>();
                Flavor.AddEraTags(tags);
                tags.Add("kind:" + (s.def.kind ?? "other"));
                tags.Add(home ? "comingHome" : "staying");
                var lines = Lines("CC_Kin_Abroad_");
                string text = Flavor.Pick(lines, tags) ?? Flavor.PickText(lines);
                if (text.NullOrEmpty()) continue;
                text = WorldSituationWorker.Sentences(text.Formatted(p.Named("PAWN"), TownOf(s).Named("TOWN"), (s.ctx.faction?.Name ?? "").Named("PEOPLE"), s.Label.Named("EVENT")).Resolve());
                if (home)
                {
                    try
                    {
                        recall();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("kin's news: home early", e);
                        continue;
                    }
                }
                Tell.Letter(NewsKind.World, Importance.Notable, Homes.Seat, "CC_Kin_Abroad_Label".Translate(p.Named("PAWN"), TownOf(s).Named("TOWN")).Resolve(), text,
                    home ? LetterDefOf.NeutralEvent : LetterDefOf.NeutralEvent, LookTargets.Invalid, s.ctx.faction);
            }
        }

        // ================= Help sent home =================

        /// <summary>The world event the council's ask is about, given just before the ask is cast; none at any other time.</summary>
        internal static WorldSituation asking;

        /// <summary>The council is asked, when it may be (the questions' own pace), by one of the colonists with a tie to them.</summary>
        private static void TryAsk(WorldSituation s)
        {
            var kin = KinOf(s.ctx.faction).Where(k => KinMemory.Ours(k.pawn) && !k.pawn.Downed).ToList();
            if (kin.Count == 0) return;
            var map = kin.OrderByDescending(k => k.way).First().pawn.Map;
            var tracker = DilemmaTracker.For(map);
            if (tracker == null) return;
            asking = s;
            try
            {
                if (tracker.TryFire(false, d => d.Worker is DilemmaWorker_KinSendHome)) s.Set(AskedFlag);
            }
            finally
            {
                asking = null;
            }
        }

        /// <summary>The help went home: it counts as the colony's part in the world event, and their people remember it.</summary>
        internal static void Helped(DilemmaContextKin k)
        {
            var s = WorldSituations.Instance?.Get(k.situationId);
            if (s != null && !s.ended)
            {
                s.help++;
                s.deeds.Add("CC_Kin_Deed".Translate(k.whatLabel.Named("WHAT"), (k.town ?? "?").Named("TOWN")).Resolve());
            }
            if (k.faction != null && FactionMemory.Remembers(k.faction)) FactionMemory.Note(k.faction, "hardship", k.town);
            if (k.a != null) CouncilGameComponent.GiveThought(k.a, KinNewsDefOf.Rimfolk_KinSentHelp);
        }

        // ================= On their card =================

        /// <summary>The worry on the heart's card (Heart/FeelingWorkers2.cs): a hard year heard of at a people they have a tie to, while it runs.</summary>
        public static void Worry(Pawn p, IFeelingSink sink)
        {
            if (!On || p == null) return;
            try
            {
                foreach (var s in WorldSituations.Instance.Running)
                {
                    if (!Hard(s) || !s.Has(WordFlag)) continue;
                    var k = KinOf(s.ctx.faction).FirstOrDefault(x => x.pawn == p);
                    if (k.pawn == null) continue;
                    sink.Add(KinNewsMath.Worry(k.way), "CC_Heart_Cause_KinTown".Translate(TownOf(s).Named("TOWN"), s.Label.Named("EVENT")));
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kin's news: the worry", e);
            }
        }

        /// <summary>For the dev tools: who at the colony has a tie to this people.</summary>
        public static string Describe(Faction f)
        {
            var kin = KinOf(f);
            return kin.Count == 0 ? "nobody" : string.Join(", ", kin.Select(k => k.pawn.LabelShort + " (" + (k.cameFrom && k.way == TieWay.None ? "came from them" : k.way.ToString()) + ")"));
        }
    }

    [DefOf]
    public static class KinNewsDefOf
    {
        public static ThoughtDef Rimfolk_KinWorried;
        public static ThoughtDef Rimfolk_KinSentHelp;

        static KinNewsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(KinNewsDefOf));
    }

    /// <summary>
    /// The council's question of help sent home (B17): A, one of the colony with a tie to the people of a hard year, asks to
    /// send food or medicine home with the next courier. {TOWN} the struck town, {EVENT} the world event, {WHAT} what would
    /// go, {COUNT} how much; tags kind:sickness and the rest, tie:marriage, tie:came. Saved with the letter.
    /// </summary>
    public class DilemmaContextKin : DilemmaContext
    {
        public int situationId = -1;
        public string town, eventLabel, what, whatLabel, kind, tie;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((town ?? "").Named("TOWN"));
            args.Add((eventLabel ?? "").Named("EVENT"));
            args.Add((whatLabel ?? "").Named("WHAT"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref situationId, "situationId", -1);
            Scribe_Values.Look(ref town, "town");
            Scribe_Values.Look(ref eventLabel, "eventLabel");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref whatLabel, "whatLabel");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref tie, "tie");
        }
    }

    /// <summary>Finds the hard year the ask is about (given by KinNews when it asks) and who asks: the colonist with the closest tie.</summary>
    public class DilemmaWorker_KinSendHome : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var s = KinNews.asking;
            if (s == null || !KinNews.Hard(s) || map == null) return false;
            var kin = KinNews.KinOf(s.ctx.faction).Where(k => KinMemory.Ours(k.pawn) && k.pawn.Map == map && !k.pawn.Downed).OrderByDescending(k => k.way).ToList();
            if (kin.Count == 0) return false;
            string what = KinNewsMath.SendFor(s.def.kind);
            if (what == null) return false;
            int stored = PlayCarry.Have(map, what, PlayCarry.DefOf(what, null));
            int count = KinNewsMath.Amount(what, stored);
            if (count <= 0) return false;
            var first = kin[0];
            ctx = new DilemmaContextKin
            {
                def = def, map = map, a = first.pawn, faction = s.ctx.faction, count = count, situationId = s.id,
                town = !s.ctx.placeName.NullOrEmpty() ? s.ctx.placeName : s.ctx.faction.Name, eventLabel = s.Label, what = what,
                whatLabel = PlayCarry.Label(what, PlayCarry.DefOf(what, null)), kind = s.def.kind,
                tie = first.cameFrom && first.way == TieWay.None ? "came" : first.way.ToString().ToLowerInvariant()
            };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is DilemmaContextKin k)) return;
            tags.Add("kind:" + (k.kind ?? "other"));
            tags.Add("tie:" + (k.tie ?? "came"));
            tags.Add(k.what == "medicine" ? "medicine" : "food");
        }
    }

    /// <summary>The help asked for, carried out of the stores to the gate for their courier to take home (B17).</summary>
    public class DilemmaEffect_KinSendHome : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static DilemmaEffect_CarryOut Carry(DilemmaContext ctx) =>
            new DilemmaEffect_CarryOut { what = (ctx as DilemmaContextKin)?.what ?? "rawfood", who = PlayWho.A, to = PlaySpot.Gate, label = "CC_Kin_CarryLabel".Translate().Resolve() };

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) => Carry(ctx).Describe(ctx, sheet);

        public override string Unavailable(DilemmaContext ctx) => Carry(ctx).Unavailable(ctx);

        public override void Apply(DilemmaContext ctx)
        {
            Carry(ctx).Apply(ctx);
            if (ctx is DilemmaContextKin k) KinNews.Helped(k);
        }
    }
}
