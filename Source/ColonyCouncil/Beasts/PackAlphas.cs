using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A wild pack led by an alpha (Beasts/PackAlphas.cs): the alpha, its name, its kind (plural, for the words), the home
    /// whose land it was on, the pack as it was when the colony named it, and how it went: still wild, tamed (its pack
    /// following it in one by one), slain, dead of something else, gone off the land, or done.
    /// </summary>
    public class PackAlpha : IExposable
    {
        public Pawn alpha;
        public string name, kind, one;
        public int home = HomesMath.None;
        public List<Pawn> pack = new List<Pawn>();
        public int madeTick = -1, endTick = -1;
        /// <summary>wild, tamed (open), slain, died, gone, done.</summary>
        public string state = "wild";
        /// <summary>A pack of the game's manhunters (its letter told the threat already), and whether a threat letter went out.</summary>
        public bool manhunters, threatTold;
        /// <summary>Tamed: which of the pack comes in next, when, the one walking in now and since when, how many came.</summary>
        public int followIndex, nextFollow = -1, walkSince = -1, followed;
        public Pawn walking;
        public int lastSight = -1;

        public bool Open => state == "wild" || state == "tamed";

        public void End(string how, int now)
        {
            state = how;
            endTick = now;
            walking = null;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                pack?.RemoveAll(p => p == null || p.Discarded);
                if (walking != null && walking.Discarded) walking = null;
            }
            Scribe_References.Look(ref alpha, "alpha");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref one, "one");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Collections.Look(ref pack, "pack", LookMode.Reference);
            Scribe_Values.Look(ref madeTick, "madeTick", -1);
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref state, "state", "wild");
            Scribe_Values.Look(ref manhunters, "manhunters");
            Scribe_Values.Look(ref threatTold, "threatTold");
            Scribe_Values.Look(ref followIndex, "followIndex");
            Scribe_Values.Look(ref nextFollow, "nextFollow", -1);
            Scribe_Values.Look(ref walkSince, "walkSince", -1);
            Scribe_Values.Look(ref followed, "followed");
            Scribe_References.Look(ref walking, "walking");
            Scribe_Values.Look(ref lastSight, "lastSight", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pack ??= new List<Pawn>();
                pack.RemoveAll(p => p == null);
                if (state.NullOrEmpty()) state = "wild";
            }
        }
    }

    /// <summary>
    /// Pack alphas (the player's pick "alphas and bosses", 7 November 2026). A wild pack of a hunting kind on a home's land
    /// (three or more of one kind together: wolves, wargs, the raptors and great cats of the player's animal mods) may turn
    /// out to be led by an alpha: the biggest in its prime, named by someone of the colony and shown by its name on the
    /// map, with the scarred alpha's hediff (a little harder to hurt, harder-hitting, more meat and hide). One home has one
    /// at a time, a month apart at the setting's 100%; a pack of the game's own manhunters is led by one three times in ten.
    /// The colony hears of a plain pack in the news; a letter comes only when an alpha-led pack threatens it (manhunting,
    /// or hunting one of the colony or its animals). Killed, the alpha's pack scatters for the edges (manhunters come to
    /// their senses first) and the one who brought it down earns a name (Packbreaker); tamed, its pack follows it in over
    /// the next days, one walking up to it at a time, each the colony's once it reaches it. No new harm of its own: the
    /// pack's danger is the game's, the alpha a little worse, and its fall ends the pack's fight.
    /// </summary>
    public static class PackAlphas
    {
        public const string NamePool = "Rimfolk_Pool_AlphaNames";
        public const string SightPool = "Rimfolk_Pool_AlphaSighting";

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && BeastsMath.Rate(S.packAlphas) > 0f && Current.Game != null;

        private const int Day = BeastsMath.DayTicks;

        // ---------------- Who leads ----------------

        public static bool IsManhunter(MentalStateDef d) => d != null && (d == MentalStateDefOf.Manhunter || d == MentalStateDefOf.ManhunterPermanent);

        /// <summary>Of a hunting kind that runs in packs.</summary>
        public static bool PackKind(Pawn p) =>
            p?.RaceProps != null && BeastsMath.PackKind(p.RaceProps.Animal, p.RaceProps.predator, p.RaceProps.Insect, p.RaceProps.Dryad, p.RaceProps.baseBodySize);

        /// <summary>An alpha whose story is open, or one of its pack: the beast that stays never takes one up (Animals/ResidentBeast.cs).</summary>
        public static bool InPack(Pawn p)
        {
            var book = BeastBook.Instance;
            return p != null && book?.alphas != null && book.alphas.Any(r => r != null && r.Open && (r.alpha == p || (r.pack?.Contains(p) ?? false)));
        }

        /// <summary>
        /// A story already, for the colony's animals choosing their prey (Animals/PetHunts.cs): as <see cref="Taken"/> (an
        /// alpha or of its pack, a nemesis, the beast the colony watches, a legend, named by anyone), or a beast nemesis that fled.
        /// </summary>
        public static bool Storied(Pawn p)
        {
            if (p == null) return false;
            var book = BeastBook.Instance;
            return book == null ? p.Name != null : Taken(book, p) || BeastNemeses.Fled(p);
        }

        /// <summary>A story already: an alpha or of its pack, a nemesis, the beast the colony watches, a legend, named by anyone.</summary>
        private static bool Taken(BeastBook book, Pawn p)
        {
            if (p.Name != null) return true;
            if (book.alphas.Any(r => r.Open && (r.alpha == p || r.pack.Contains(p)))) return true;
            var comp = CouncilGameComponent.Instance;
            if (comp?.nemeses?.For(p) != null) return true;
            if (comp != null && Homes.All.Any(m => comp.BeastOf(m)?.animal == p)) return true;
            return WorldSituations.Holds(p);
        }

        /// <summary>
        /// The biggest pack of one hunting kind on the map: wild, up and seen, three or more within reach of each other,
        /// all manhunting (the game's own pack) or none of them in any mental state. Null when there is none.
        /// </summary>
        public static List<Pawn> FindPack(BeastBook book, Map map, bool manhunters)
        {
            if (map == null) return null;
            var cands = new List<Pawn>();
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || p.Faction != null || !PackKind(p) || !BeastBook.Up(p) || p.Position.Fogged(map)) continue;
                bool mh = p.InMentalState && IsManhunter(p.MentalStateDef);
                if (manhunters ? !mh : p.InMentalState) continue;
                if (Taken(book, p)) continue;
                cands.Add(p);
            }
            List<Pawn> best = null;
            foreach (var g in cands.GroupBy(p => p.kindDef))
            {
                var list = g.ToList();
                if (list.Count < BeastsMath.PackMin) continue;
                foreach (var seed in list)
                {
                    var cluster = list.Where(q => q.Position.InHorDistOf(seed.Position, BeastsMath.PackRadius)).ToList();
                    if (cluster.Count >= BeastsMath.PackMin && (best == null || cluster.Count > best.Count)) best = cluster;
                }
            }
            return best;
        }

        private static float AgeShare(Pawn p)
        {
            float life = p.RaceProps?.lifeExpectancy ?? 0f;
            return life > 0f ? (p.ageTracker?.AgeBiologicalYearsFloat ?? 0f) / life : 0.5f;
        }

        /// <summary>
        /// The pack gets its alpha: the biggest in its prime, given a name by someone of the colony (seeing it, if anyone
        /// does) and the scarred alpha's hediff. The news, or (for manhunters) a letter. Null when nobody is there to name it.
        /// </summary>
        public static PackAlpha Make(BeastBook book, Map map, List<Pawn> pack, bool manhunters, int now)
        {
            if (book == null || map == null || pack == null || pack.Count < BeastsMath.PackMin) return null;
            var alpha = pack.Where(BeastBook.Up).OrderByDescending(p => BeastsMath.AlphaScore(p.BodySize, AgeShare(p), p.gender == Gender.Male)).FirstOrDefault();
            if (alpha == null) return null;
            var voice = BeastBook.Voice(map, alpha);
            string name = BeastBook.NameFrom(NamePool, voice, alpha.BodySize >= 1.5f ? "big" : "small", manhunters ? "manhunters" : "wild");
            if (name == null) return null;
            alpha.Name = new NameSingle(name);
            if (BeastsDefOf.Rimfolk_PackAlpha != null && alpha.health.hediffSet.GetFirstHediffOfDef(BeastsDefOf.Rimfolk_PackAlpha) == null)
                alpha.health.AddHediff(BeastsDefOf.Rimfolk_PackAlpha);
            var r = new PackAlpha
            {
                alpha = alpha, name = name, kind = alpha.kindDef.GetLabelPlural(), one = alpha.KindLabel, home = Homes.KeyOf(map), pack = pack.ToList(), madeTick = now,
                manhunters = manhunters, threatTold = manhunters
            };
            book.alphas.Add(r);
            var bh = book.HomeOf(map);
            if (bh != null)
            {
                if (manhunters) bh.lastManhunterAlpha = now;
                else bh.lastAlpha = now;
            }
            var args = new[] { name.Named("NAME"), r.kind.Named("KIND"), r.one.Named("ONE"), pack.Count.Named("COUNT"), voice.Named("PAWN") };
            if (manhunters)
            {
                // The game's own letter told of the pack: this one says who leads it, and what its fall will do.
                Tell.Letter(NewsKind.Land, Importance.Important, map, "CC_Alpha_ThreatTitle".Translate(name.Named("NAME")).Resolve(),
                    "CC_Alpha_LedManhunters".Translate(args).Resolve(), LetterDefOf.ThreatSmall, new LookTargets(alpha));
            }
            else News.Post("CC_Alpha_Led".Translate(args).Resolve(), new LookTargets(alpha), NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, "CC_Alpha_LedChronicle".Translate(args).Resolve(), voice));
            return r;
        }

        // ---------------- The day and the threats ----------------

        /// <summary>The day: a pack on the land with no alpha yet may turn out to have one (one a home at a time, a month apart at 100%).</summary>
        public static void Daily(BeastBook book, Map map, int now)
        {
            if (!On || map == null) return;
            int key = Homes.KeyOf(map);
            if (book.alphas.Any(r => r.Open && r.home == key)) return;
            var bh = book.HomeOf(map);
            if (bh == null || !BeastsMath.Waited(S.packAlphas, BeastsMath.AlphaGapDays, now, bh.lastAlpha)) return;
            var pack = FindPack(book, map, false);
            if (pack == null || !Rand.Chance(BeastsMath.Chance(S.packAlphas, BeastsMath.AlphaDayChance))) return;
            Make(book, map, pack, false, now);
        }

        /// <summary>
        /// A big threat just came to a home (the storyteller's): if it is a pack of the game's own manhunters of a hunting
        /// kind, three in ten of them run behind an alpha. Looked at once a threat, within two hours of it.
        /// </summary>
        public static void WatchThreats(BeastBook book, int now)
        {
            if (!On) return;
            foreach (var map in Homes.All)
            {
                if (map?.storyState == null) continue;
                int threat = map.storyState.LastThreatBigTick;
                if (threat <= 0 || now - threat > BeastsMath.HourTicks * 2) continue;
                var bh = book.HomeOf(map);
                if (bh == null || bh.threatLooked >= threat) continue;
                bh.threatLooked = threat;
                int key = Homes.KeyOf(map);
                if (book.alphas.Any(r => r.Open && r.home == key && r.state == "wild")) continue;
                if (!BeastsMath.Waited(S.packAlphas, BeastsMath.ManhunterAlphaGapDays, now, bh.lastManhunterAlpha)) continue;
                var pack = FindPack(book, map, true);
                if (pack == null || !Rand.Chance(BeastsMath.Chance(S.packAlphas, BeastsMath.ManhunterAlphaChance))) continue;
                Make(book, map, pack, true, now);
            }
        }

        /// <summary>The pack threatens the colony: manhunting, or one of it hunting one of the colony's people or animals.</summary>
        private static bool Threatens(PackAlpha r, out Pawn prey)
        {
            prey = null;
            foreach (var m in r.pack.Concat(new[] { r.alpha }))
            {
                if (!BeastBook.Up(m) || m.Faction != null || !Homes.IsHome(m.Map)) continue;
                if (m.InMentalState && IsManhunter(m.MentalStateDef)) return true;
                if (m.CurJobDef == JobDefOf.PredatorHunt && m.CurJob?.targetA.Thing is Pawn t && t.Faction == Faction.OfPlayer)
                {
                    prey = t;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The hour: a letter once the pack threatens the colony, and now and then someone in sight of the alpha says so.</summary>
        public static void Hourly(BeastBook book, int now)
        {
            foreach (var r in book.alphas.Where(r => r.state == "wild").ToList())
            {
                var a = r.alpha;
                if (!BeastBook.Up(a) || !Homes.IsHome(a.Map)) continue;
                if (!r.threatTold && Threatens(r, out var prey))
                {
                    r.threatTold = true;
                    string text = prey != null
                        ? "CC_Alpha_Hunting".Translate(r.name.Named("NAME"), r.kind.Named("KIND"), prey.Named("PREY")).Resolve()
                        : "CC_Alpha_Manhunting".Translate(r.name.Named("NAME"), r.kind.Named("KIND")).Resolve();
                    Tell.Letter(NewsKind.Land, Importance.Important, a.Map, "CC_Alpha_ThreatTitle".Translate(r.name.Named("NAME")).Resolve(), text, LetterDefOf.ThreatSmall,
                        new LookTargets(prey != null && prey.Spawned ? new[] { a, prey } : new[] { a }));
                }
                if (now - r.lastSight < BeastsMath.HourTicks * 6 || !Rand.Chance(0.4f)) continue;
                var p = a.Map.mapPawns.FreeColonistsSpawned
                    .Where(c => People.HasInnerLife(c) && !c.Downed && !c.Drafted && c.Awake() && c.Position.InHorDistOf(a.Position, 18f) && GenSight.LineOfSight(c.Position, a.Position, a.Map))
                    .RandomElementWithFallback();
                if (p == null) continue;
                var tags = new List<string> { r.threatTold ? "threat" : "calm" };
                if (p.DevelopmentalStage.Juvenile()) tags.Add("child");
                string line = Flavor.FromPool(SightPool, p, null, null, tags.ToArray());
                if (line.NullOrEmpty()) continue;
                // apart: a remark on seeing the alpha, said to nobody in particular; the alpha is where they look.
                if (Speech.Say(p, line.Formatted(r.name.Named("NAME"), r.one.Named("KIND")).Resolve())) r.lastSight = now;
            }
        }

        // ---------------- The short pass ----------------

        /// <summary>Every 250 ticks: an alpha down (its pack scatters), tamed (its pack starts to follow), gone; the pack walking in.</summary>
        public static void Rare(BeastBook book, int now)
        {
            foreach (var r in book.alphas.Where(r => r.Open).ToList())
            {
                var a = r.alpha;
                if (a == null || a.Discarded)
                {
                    r.End("gone", now);
                    continue;
                }
                if (r.state == "tamed")
                {
                    if (a.Dead || a.Faction != Faction.OfPlayer) r.End("done", now);
                    else Follow(r, now);
                    continue;
                }
                if (a.Dead)
                {
                    Fell(r, now);
                    continue;
                }
                if (a.Faction == Faction.OfPlayer)
                {
                    Tamed(r, now);
                    continue;
                }
                if (a.Faction != null || !a.Spawned)
                {
                    if (a.Faction == null && !a.Downed)
                        News.Post("CC_Alpha_Gone".Translate(r.name.Named("NAME"), r.kind.Named("KIND")).Resolve(), null, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                    r.End("gone", now);
                }
            }
        }

        /// <summary>The alpha is dead: its pack scatters for the edges, and whoever of the colony brought it down earns a name.</summary>
        private static void Fell(PackAlpha r, int now)
        {
            var a = r.alpha;
            var map = a.Corpse?.MapHeld ?? a.MapHeld;
            var slayer = BeastBook.Slayer(a);
            int scattered = Scatter(r, map);
            r.End(slayer != null ? "slain" : "died", now);
            var comp = CouncilGameComponent.Instance;
            string text = slayer != null
                ? "CC_Alpha_SlainBy".Translate(r.name.Named("NAME"), r.kind.Named("KIND"), slayer.Named("SLAYER")).Resolve()
                : "CC_Alpha_Died".Translate(r.name.Named("NAME"), r.kind.Named("KIND")).Resolve();
            if (scattered > 0) text += " " + "CC_Alpha_Scatter".Translate(r.kind.Named("KIND")).Resolve();
            var look = slayer != null && slayer.Spawned ? new LookTargets(slayer) : a.Corpse != null && a.Corpse.Spawned ? new LookTargets(a.Corpse) : LookTargets.Invalid;
            if (r.threatTold) Tell.Letter(NewsKind.Land, Importance.Notable, map, "CC_Alpha_FellTitle".Translate(r.name.Named("NAME")).Resolve(), text, LetterDefOf.PositiveEvent, look);
            else News.Post(text, look, NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, slayer));
            if (slayer == null || !slayer.IsColonist || comp == null) return;
            using var _cause = Causes.Push("CC_Cause_Alpha".Translate(r.name.Named("NAME")).Resolve());
            CouncilGameComponent.GiveThought(slayer, BeastsDefOf.Rimfolk_Alpha_Slain);
            comp.journals.Write(slayer, "Rimfolk_Journal_AlphaSlain", null, r.name.Named("NAME"), r.one.Named("KIND"));
            if (BeastsDefOf.Rimfolk_Epithet_Packbreaker != null) comp.epithets.Bestow(BeastsDefOf.Rimfolk_Epithet_Packbreaker, slayer, comp);
        }

        /// <summary>
        /// The pack loses its nerve: each of it still up and nobody's, and with manhunters every manhunter of its kind on the
        /// map, comes to its senses and runs for the nearest edge on its own. Returns how many scatter.
        /// </summary>
        public static int Scatter(PackAlpha r, Map map)
        {
            if (map == null) return 0;
            var runners = r.pack.Where(m => m != r.alpha && BeastBook.Up(m) && m.Faction == null && m.Map == map).ToList();
            if (r.manhunters)
                foreach (var m in map.mapPawns.AllPawnsSpawned)
                    if (m != r.alpha && m.kindDef == r.alpha.kindDef && m.Faction == null && BeastBook.Up(m) && m.InMentalState && IsManhunter(m.MentalStateDef) && !runners.Contains(m))
                        runners.Add(m);
            int n = 0;
            foreach (var m in runners)
            {
                try
                {
                    if (m.InMentalState) m.MentalState?.RecoverFromState();
                    Lords.Leave(m);
                    LordMaker.MakeNewLord(null, new LordJob_ExitMapNear(m.Position, LocomotionUrgency.Jog, 12f, false, false), map, new List<Pawn> { m });
                    n++;
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("alpha's pack scatters", e);
                }
            }
            return n;
        }

        /// <summary>Tamed: the colony's now, and its old pack will follow it in.</summary>
        private static void Tamed(PackAlpha r, int now)
        {
            var a = r.alpha;
            r.state = "tamed";
            r.followIndex = 0;
            r.nextFollow = now + Mathf.RoundToInt(BeastsMath.FollowAfterDays(0, Rand.Value) * Day);
            var tamer = a.playerSettings?.Master ?? a.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
            bool anyLeft = r.pack.Any(m => m != a && m != null && !m.Dead && !m.Discarded && m.Faction == null);
            string text = "CC_Alpha_Tamed".Translate(r.name.Named("NAME"), r.kind.Named("KIND"), (tamer?.LabelShort ?? "").Named("TAMER")).Resolve();
            if (anyLeft) text += " " + "CC_Alpha_TamedPack".Translate(r.name.Named("NAME")).Resolve();
            News.Post(text, new LookTargets(a), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            var comp = CouncilGameComponent.Instance;
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, tamer));
            if (tamer != null && tamer.IsColonist) CouncilGameComponent.GiveThought(tamer, BeastsDefOf.Rimfolk_Alpha_Tamed);
        }

        /// <summary>
        /// The pack following its tamed alpha in, one at a time: the next of it (on the map, or back from the wild at the
        /// edge) walks up to the alpha and is the colony's once it reaches it; one that doesn't get there in two days gives up.
        /// While the alpha is away (a caravan), they wait.
        /// </summary>
        private static void Follow(PackAlpha r, int now)
        {
            var a = r.alpha;
            if (!BeastBook.Up(a) || !Homes.IsHome(a.Map)) return;
            var m = r.walking;
            if (m != null)
            {
                if (m.Dead || m.Destroyed || m.Faction != null || m.Downed || (!m.Spawned && now - r.walkSince > BeastsMath.FollowGiveUpDays * Day))
                {
                    Next(r, now);
                    return;
                }
                if (!m.Spawned) return;
                if (m.Map == a.Map && m.Position.InHorDistOf(a.Position, BeastsMath.JoinRange))
                {
                    Join(r, m);
                    Next(r, now);
                    return;
                }
                if (m.Map != a.Map || now - r.walkSince > BeastsMath.FollowGiveUpDays * Day)
                {
                    Next(r, now);
                    return;
                }
                if (!m.InMentalState && m.CurJobDef != JobDefOf.Goto) WalkTo(m, a);
                return;
            }
            if (now < r.nextFollow) return;
            while (r.followIndex < r.pack.Count && r.followed < BeastsMath.FollowMax)
            {
                var next = r.pack[r.followIndex];
                if (next == null || next == a || next.Dead || next.Discarded || next.Faction != null || next.Downed || next.InMentalState)
                {
                    r.followIndex++;
                    continue;
                }
                if (!next.Spawned)
                {
                    if (!Find.WorldPawns.Contains(next) || !AtEdge(next, a.Map))
                    {
                        r.followIndex++;
                        continue;
                    }
                }
                else if (next.Map != a.Map)
                {
                    r.followIndex++;
                    continue;
                }
                r.walking = next;
                r.walkSince = now;
                WalkTo(next, a);
                return;
            }
            r.End("done", now);
        }

        private static void Next(PackAlpha r, int now)
        {
            r.walking = null;
            r.followIndex++;
            r.nextFollow = now + Mathf.RoundToInt(Rand.Range(0.6f, 1f) * Day);
        }

        /// <summary>One of the pack back from the wild, at the map's edge.</summary>
        private static bool AtEdge(Pawn m, Map map)
        {
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal)) return false;
            try
            {
                Find.WorldPawns.RemovePawn(m);
                GenSpawn.Spawn(m, cell, map);
                return true;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("alpha's pack back at the edge", e);
                return false;
            }
        }

        /// <summary>It walks up to its old alpha (the game's own going-to, re-given while it isn't there yet).</summary>
        private static void WalkTo(Pawn m, Pawn a)
        {
            if (m.jobs == null || !a.Spawned) return;
            if (!CellFinder.TryFindRandomCellNear(a.Position, a.Map, 3, c => c.Standable(a.Map) && m.CanReach(c, PathEndMode.OnCell, Danger.Deadly), out var cell)) cell = a.Position;
            Lords.Leave(m);
            var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            job.expiryInterval = BeastsMath.HourTicks;
            m.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>It has reached its alpha: the colony's now, with the alpha's master for its own.</summary>
        private static void Join(PackAlpha r, Pawn m)
        {
            try
            {
                InteractionWorker_RecruitAttempt.DoRecruit(null, m, false);
                var master = r.alpha.playerSettings?.Master;
                if (master != null && m.playerSettings != null) m.playerSettings.Master = master;
                r.followed++;
                News.Post("CC_Alpha_Followed".Translate(r.name.Named("NAME"), m.KindLabel.Named("ONE")).Resolve(), new LookTargets(m), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("alpha's pack comes in", e);
            }
        }
    }
}
