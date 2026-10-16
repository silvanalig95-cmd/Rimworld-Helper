using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What a decree takes from the stores each morning: a share of what's there, within bounds.</summary>
    public class DecreeTake
    {
        /// <summary>"silver", "rawfood", "medicine", or a ThingDef defName.</summary>
        public string what;
        /// <summary>The share of what's in store (0.1 = a tenth).</summary>
        public float share = 0.1f;
        public int min = 1;
        public int max = 9999;
    }

    /// <summary>Who a decree's goodwill goes to.</summary>
    public enum DecreeGoodwillTo
    {
        /// <summary>The people of the nearest friendly settlement.</summary>
        Neighbour,
        /// <summary>A friendly people of the colony's own faith (Ideology), else the neighbours.</summary>
        Faith,
        /// <summary>The people at the heart of the world event the decree answers (a famine), else the neighbours.</summary>
        Situation
    }

    /// <summary>
    /// The decrees that do more than a hediff can: when they can be declared at all (the colony's era, its research, its
    /// faith, a world event running in the region, a DLC or mod), and what they do each day and hour while they hold (a
    /// tithe or alms taken from the stores and goodwill for it, the faith's certainty, a curfew's night hours, a work
    /// song's recreation, a quarantine's closed doors, a market day's traders). Everything here is read from the decree's
    /// own fields (DecreeDef), so a new decree of these kinds needs no code.
    /// </summary>
    public static class DecreeWork
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static Map Home => HappeningTracker.HomeMap();

        // ---------------- The colony's faith ----------------

        /// <summary>The colony's own faith (Ideology), or null.</summary>
        public static Ideo ColonyFaith
        {
            get
            {
                if (!ModsConfig.IdeologyActive) return null;
                try
                {
                    return Faction.OfPlayerSilentFail?.ideos?.PrimaryIdeo;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("decree faith", e);
                    return null;
                }
            }
        }

        /// <summary>Of the colony's own faith.</summary>
        public static bool Believer(Pawn p)
        {
            var faith = ColonyFaith;
            return faith != null && p?.Ideo == faith;
        }

        /// <summary>Of a faith that isn't the colony's.</summary>
        public static bool OtherFaith(Pawn p)
        {
            var faith = ColonyFaith;
            return faith != null && p?.Ideo != null && p.Ideo != faith;
        }

        /// <summary>The colony's faith holds one of these memes.</summary>
        public static bool FaithHas(List<string> memes)
        {
            var faith = ColonyFaith;
            if (faith?.memes == null || memes.NullOrEmpty()) return false;
            foreach (var m in faith.memes)
                if (m != null && memes.Contains(m.defName)) return true;
            return false;
        }

        // ---------------- When it can be declared ----------------

        private static readonly Dictionary<DecreeDef, (float at, bool hidden)> hiddenCache = new Dictionary<DecreeDef, (float, bool)>();

        /// <summary>
        /// Never possible in this game as it stands: another era, a DLC or mod that isn't there, another faith. Asked by
        /// the decrees page every frame, so worked out every couple of seconds.
        /// </summary>
        public static bool Hidden(DecreeDef d)
        {
            if (d == null) return true;
            float now = Time.realtimeSinceStartup;
            if (hiddenCache.TryGetValue(d, out var c) && now - c.at < 2f && now >= c.at) return c.hidden;
            bool hidden;
            try
            {
                hidden = HiddenNow(d);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree hidden " + d.defName, e);
                hidden = true;
            }
            hiddenCache[d] = (now, hidden);
            return hidden;
        }

        private static bool HiddenNow(DecreeDef d)
        {
            if (!d.mods.NullOrEmpty() && !d.mods.Any(People.ModActive)) return true;
            if (!WishParams.EraFits(d.eras)) return true;
            if ((d.needsIdeology || !d.memes.NullOrEmpty()) && ColonyFaith == null) return true;
            if (!d.memes.NullOrEmpty() && !FaithHas(d.memes)) return true;
            return false;
        }

        private static readonly Dictionary<DecreeDef, (float at, string why)> blockedCache = new Dictionary<DecreeDef, (float, string)>();

        /// <summary>
        /// Why this decree can't be declared right now beyond the usual (cooldown, too many in force), or null. Asked by the
        /// decrees page every frame, so worked out about once a second.
        /// </summary>
        public static string Blocked(DecreeDef d)
        {
            float now = Time.realtimeSinceStartup;
            if (blockedCache.TryGetValue(d, out var c) && now - c.at < 1f && now >= c.at) return c.why;
            string why = null;
            try
            {
                why = Why(d);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree gate " + d.defName, e);
            }
            blockedCache[d] = (now, why);
            return why;
        }

        private static string Why(DecreeDef d)
        {
            if (Hidden(d)) return "CC_Decree_NotForUs".Translate();
            if (!d.research.NullOrEmpty())
            {
                var known = d.research.Select(r => DefDatabase<ResearchProjectDef>.GetNamedSilentFail(r)).Where(r => r != null).ToList();
                if (!known.Any(r => r.IsFinished))
                    return known.Count == 0 ? "CC_Decree_NotForUs".Translate().Resolve() : "CC_Decree_NeedsResearch".Translate(known[0].LabelCap).Resolve();
            }
            var map = Home;
            if (d.minColonists > 0 && (map == null || RoomUtil.ColonistCount(map) < d.minColonists)) return "CC_Decree_NeedsPeople".Translate(d.minColonists).Resolve();
            if (!d.situationKinds.NullOrEmpty() && SituationRunning(d) == null && !OrWhenNow(d, map)) return "CC_Decree_NeedsSituation".Translate().Resolve();
            if (d.callsTraders && MarketFactions().Count == 0) return "CC_Decree_NoTraders".Translate().Resolve();
            if (d.take != null && (map == null || Have(map, d.take) < Mathf.Max(1, d.take.min))) return "CC_Decree_NothingToGive".Translate(Label(d.take)).Resolve();
            return null;
        }

        /// <summary>A running world event of one of the decree's kinds (a plague for a quarantine), or null.</summary>
        public static WorldSituation SituationRunning(DecreeDef d)
        {
            if (d?.situationKinds == null || d.situationKinds.Count == 0 || S == null || !S.worldSituations) return null;
            var ws = WorldSituations.Instance;
            if (ws == null) return null;
            foreach (var s in ws.Running)
                if (s?.def != null && !s.def.kind.NullOrEmpty() && d.situationKinds.Contains(s.def.kind)) return s;
            return null;
        }

        private static bool OrWhenNow(DecreeDef d, Map map)
        {
            if (d.orWhen.NullOrEmpty() || map == null) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            return d.orWhen.Any(m => m != DecreeMoment.Situation && DecreeTracker.MomentNow(m, map, pawns, d));
        }

        // ---------------- Declared, and over ----------------

        public static void Enacted(DecreeTracker t, DecreeDef def, Pawn asker)
        {
            try
            {
                var map = Home;
                if (def.nightSleep) ApplyNight(t);
                if (def.callsTraders && map != null) CallTraders(def, map);
                if (!def.enactEffects.NullOrEmpty() && map != null)
                {
                    var a = asker != null && asker.Spawned && asker.Map == map ? asker : map.mapPawns.FreeColonistsSpawned.Where(PlayOuts.OurPerson).RandomElementWithFallback();
                    PlayOuts.Run(def.enactEffects, new DilemmaContext { map = map, a = a, faction = GoodwillFaction(def), why = def.LabelCap }, "decree " + def.defName);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree declared " + def.defName, e);
            }
        }

        public static void Ended(DecreeTracker t, DecreeDef def)
        {
            if (def.nightSleep && !t.active.Any(a => a.def != null && a.def.nightSleep)) RestoreNight(t);
        }

        // ---------------- The hour ----------------

        public static void Hourly(DecreeTracker t)
        {
            try
            {
                bool night = t.active.Any(a => a.def != null && a.def.nightSleep);
                if (night) ApplyNight(t);
                else if (t.nightHours.Count > 0) RestoreNight(t);
                foreach (var a in t.active)
                {
                    if (a.def == null || a.def.joyWhileWorking <= 0f) continue;
                    foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
                    {
                        if (p.needs?.joy == null || p.Downed || p.InMentalState || !p.Awake() || p.IsQuestLodger()) continue;
                        var job = p.CurJob;
                        if (job?.workGiverDef == null) continue;
                        if (a.def.ReactionOf(p) < DecreeReaction.Neutral) continue;
                        p.needs.joy.GainJoy(a.def.joyWhileWorking, JoyKindDefOf.Social);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree hour", e);
            }
        }

        // ---------------- The day ----------------

        public static void Daily(DecreeTracker t, ActiveDecree a)
        {
            var def = a?.def;
            if (def == null) return;
            try
            {
                var map = Home;
                int given = 0;
                bool gave = def.take == null;
                if (def.take != null && map != null)
                {
                    given = Take(map, def.take);
                    gave = given > 0;
                }
                Faction to = null;
                if (def.goodwillPerDay != 0 && gave)
                {
                    to = GoodwillFaction(def);
                    to?.TryAffectGoodwillWith(Faction.OfPlayer, def.goodwillPerDay, false, false);
                }
                if (def.certaintyPerDay > 0f && ColonyFaith != null)
                {
                    float amount = def.certaintyPerDay * Interplay.FaithFactor;
                    foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
                        if (Believer(p) && !p.IsQuestLodger()) p.ideo?.OffsetCertainty(amount);
                }
                if (given > 0)
                {
                    string text = to != null
                        ? "CC_Decree_GaveTo".Translate(def.LabelCap, given, Label(def.take), to.Name).Resolve()
                        : "CC_Decree_Gave".Translate(def.LabelCap, given, Label(def.take)).Resolve();
                    News.Post(text, null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree day " + def.defName, e);
            }
        }

        // ---------------- The stores ----------------

        private static int Have(Map map, DecreeTake t)
        {
            switch (t.what)
            {
                case "silver": return map.resourceCounter.Silver;
                case "rawfood": return DilemmaEffect_Consume.RawFood(map);
                case "medicine": return DilemmaEffect_Consume.Medicine(map).Sum(x => x.stackCount);
                default:
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(t.what);
                    return def == null ? 0 : map.resourceCounter.GetCount(def);
                }
            }
        }

        private static string Label(DecreeTake t)
        {
            switch (t?.what)
            {
                case null: return "";
                case "silver": return ThingDefOf.Silver.label;
                case "rawfood": return "CC_Dilemma_RawFood".Translate();
                case "medicine": return "CC_Dilemma_Medicine".Translate();
                default: return DefDatabase<ThingDef>.GetNamedSilentFail(t.what)?.label ?? t.what;
            }
        }

        /// <summary>Takes the day's share (a quartermaster stretches it, as for any answer that costs the stores). Returns how much went.</summary>
        private static int Take(Map map, DecreeTake t)
        {
            int have = Have(map, t);
            if (have <= 0) return 0;
            int want = Mathf.Clamp(Mathf.RoundToInt(have * t.share), Mathf.Max(1, t.min), Mathf.Max(1, t.max));
            want = Mathf.Min(want, have);
            if (want < Mathf.Max(1, t.min)) return 0;
            var consume = new DilemmaEffect_Consume { what = t.what, count = want };
            var ctx = new DilemmaContext { map = map };
            if (consume.Unavailable(ctx) != null) return 0;
            consume.Apply(ctx);
            return Mathf.Max(1, Mathf.RoundToInt(want * (CouncilGameComponent.Instance?.offices.CostFactor ?? 1f)));
        }

        // ---------------- Goodwill ----------------

        private static bool Usable(Faction f) =>
            f != null && !f.IsPlayer && !f.defeated && !f.Hidden && !f.temporary && f.HasGoodwill && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer);

        /// <summary>Who a decree's goodwill goes to today (see <see cref="DecreeGoodwillTo"/>), or null when there's nobody.</summary>
        public static Faction GoodwillFaction(DecreeDef def)
        {
            try
            {
                switch (def.goodwillTo)
                {
                    case DecreeGoodwillTo.Situation:
                    {
                        var f = SituationRunning(def)?.ctx?.faction;
                        if (Usable(f)) return f;
                        break;
                    }
                    case DecreeGoodwillTo.Faith:
                    {
                        var faith = ColonyFaith;
                        if (faith != null)
                        {
                            var f = Nearest(x => x.ideos?.PrimaryIdeo == faith);
                            if (f != null) return f;
                        }
                        break;
                    }
                }
                return Nearest(null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree goodwill", e);
                return null;
            }
        }

        /// <summary>The people of the nearest friendly settlement (that fits <paramref name="pred"/>).</summary>
        private static Faction Nearest(Func<Faction, bool> pred)
        {
            var map = Home;
            if (map == null) return null;
            Faction best = null;
            float bestDist = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                var f = s.Faction;
                if (!Usable(f) || (pred != null && !pred(f))) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(map.Tile, s.Tile);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = f;
                }
            }
            return best;
        }

        // ---------------- A market day ----------------

        /// <summary>Friendly peoples who send trade caravans.</summary>
        private static List<Faction> MarketFactions() =>
            Find.FactionManager.AllFactionsListForReading.Where(f => Usable(f) && !f.def.caravanTraderKinds.NullOrEmpty()).ToList();

        /// <summary>Word goes out, and a trade caravan comes: the game's own, from a friendly people, the nearest and friendliest first.</summary>
        private static void CallTraders(DecreeDef def, Map map)
        {
            var inc = IncidentDefOf.TraderCaravanArrival;
            if (inc?.Worker == null) return;
            var tech = Avail.EffectiveTech;
            foreach (var f in MarketFactions().OrderByDescending(f => Arrivals.TechWeight(f, tech) * (f.PlayerGoodwill + 120)).Take(4))
            {
                try
                {
                    var parms = StorytellerUtility.DefaultParmsNow(inc.category, map);
                    parms.faction = f;
                    parms.forced = true;
                    if (!inc.Worker.CanFireNow(parms) || !inc.Worker.TryExecute(parms)) continue;
                    return;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("market day traders", e);
                }
            }
            Messages.Message("CC_Decree_MarketNobody".Translate(def.label).Resolve(), MessageTypeDefOf.NeutralEvent, false);
        }

        // ---------------- A curfew ----------------

        private static readonly int[] NightHours = { 22, 23, 0, 1, 2, 3, 4, 5 };

        private static TraitDef nightOwl;
        private static bool nightOwlLooked;

        private static bool NightOwl(Pawn p)
        {
            if (!nightOwlLooked)
            {
                nightOwlLooked = true;
                nightOwl = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            }
            return nightOwl != null && (p.story?.traits?.HasTrait(nightOwl) ?? false);
        }

        /// <summary>Who a curfew sends to bed: not night owls (they keep the watch), not the sleepless, not babies or lodgers.</summary>
        public static bool CurfewApplies(Pawn p) =>
            p?.timetable != null && p.needs?.rest != null && !p.IsQuestLodger() && !p.DevelopmentalStage.Baby() && !NightOwl(p);

        /// <summary>Everyone the curfew applies to (and anyone new since) has their night hours turned to sleep.</summary>
        private static void ApplyNight(DecreeTracker t)
        {
            var sleep = TimeAssignmentDefOf.Sleep;
            if (sleep == null) return;
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
            {
                if (t.nightHours.ContainsKey(p.ThingID) || !CurfewApplies(p)) continue;
                var changed = new List<string>();
                foreach (int h in NightHours)
                {
                    var a = p.timetable.GetAssignment(h);
                    if (a == null || a == sleep) continue;
                    changed.Add(h + "=" + a.defName);
                    p.timetable.SetAssignment(h, sleep);
                }
                // Kept even when nothing changed, so they aren't looked at again every hour.
                t.nightHours[p.ThingID] = string.Join(";", changed);
            }
        }

        /// <summary>The curfew is over: each night hour still set to sleep goes back to what it was.</summary>
        private static void RestoreNight(DecreeTracker t)
        {
            try
            {
                var sleep = TimeAssignmentDefOf.Sleep;
                var people = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList();
                foreach (var kv in t.nightHours)
                {
                    if (kv.Value.NullOrEmpty()) continue;
                    var p = people.FirstOrDefault(x => x.ThingID == kv.Key);
                    if (p?.timetable == null) continue;
                    foreach (var part in kv.Value.Split(';'))
                    {
                        int eq = part.IndexOf('=');
                        if (eq <= 0 || !int.TryParse(part.Substring(0, eq), out int h) || h < 0 || h >= GenDate.HoursPerDay) continue;
                        var def = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(part.Substring(eq + 1));
                        if (def != null && p.timetable.GetAssignment(h) == sleep) p.timetable.SetAssignment(h, def);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("curfew over", e);
            }
            finally
            {
                t.nightHours.Clear();
            }
        }

        // ---------------- A quarantine ----------------

        /// <summary>The factor on illnesses passing between people sharing a bedroom, from the decrees in force (1: none).</summary>
        public static float ContagionFactor
        {
            get
            {
                var active = CouncilGameComponent.Instance?.decrees?.active;
                if (active == null || active.Count == 0) return 1f;
                float f = 1f;
                for (int i = 0; i < active.Count; i++)
                    if (active[i]?.def != null) f *= active[i].def.contagionFactor;
                return Mathf.Clamp(f, 0f, 1f);
            }
        }

        /// <summary>The factor on Rimfolk's visitor events, from the decrees in force (1: none).</summary>
        public static float VisitorFactor
        {
            get
            {
                var active = CouncilGameComponent.Instance?.decrees?.active;
                if (active == null || active.Count == 0) return 1f;
                float f = 1f;
                for (int i = 0; i < active.Count; i++)
                    if (active[i]?.def != null) f *= active[i].def.visitorFactor;
                return Mathf.Clamp(f, 0.05f, 2f);
            }
        }
    }
}
