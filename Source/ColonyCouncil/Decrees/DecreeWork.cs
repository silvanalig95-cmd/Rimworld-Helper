using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
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

        /// <summary>
        /// The form of its idea a decree takes now (DecreeMath.FormNow): the drills by the colony's age, rationing by
        /// whether a famine lies over the region while the colony's own stores aren't low. Only that form is listed.
        /// </summary>
        public static string FormNow(DecreeDef d, Map home)
        {
            if (d == null || DecreeMath.OtherForm(d.defName) == null) return d?.defName;
            var age = Ages.Of(home);
            bool lowTech = age == WorldAge.Tribal || age == WorldAge.Medieval;
            var bread = DefDatabase<DecreeDef>.GetNamedSilentFail(DecreeMath.FamineBread);
            bool famine = bread != null && SituationRunning(bread) != null;
            bool foodLow = home != null && MapFacts.Of(home).FoodDays < 8f;
            return DecreeMath.FormNow(d.defName, lowTech, famine, foodLow);
        }

        private static bool HiddenNow(DecreeDef d)
        {
            // One idea, one decree: the form of it not taken now isn't listed (decision 13 of the review).
            if (FormNow(d, Home) != d.defName) return true;
            if (!d.mods.NullOrEmpty() && !d.mods.Any(People.ModActive)) return true;
            if (!WishParams.EraFits(d.eras)) return true;
            if ((d.needsIdeology || !d.memes.NullOrEmpty()) && ColonyFaith == null) return true;
            if (!d.memes.NullOrEmpty() && !FaithHas(d.memes)) return true;
            if ((d.oneFaith || d.everyFaith) && DecreeFaith.Hidden) return true;
            return false;
        }

        private static readonly Dictionary<(DecreeDef, int), (float at, string why)> blockedCache = new Dictionary<(DecreeDef, int), (float, string)>();

        /// <summary>
        /// Why this decree can't be declared right now beyond the usual (cooldown, too many in force), or null, in the colony
        /// of <paramref name="t"/> (the seat's without one). Asked by the decrees page every frame, so worked out about once a second.
        /// </summary>
        public static string Blocked(DecreeDef d, DecreeTracker t = null)
        {
            float now = Time.realtimeSinceStartup;
            var key = (d, t?.colony?.home ?? HomesMath.None);
            if (blockedCache.TryGetValue(key, out var c) && now - c.at < 1f && now >= c.at) return c.why;
            string why = null;
            try
            {
                why = Why(d, t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree gate " + d.defName, e);
            }
            blockedCache[key] = (now, why);
            return why;
        }

        /// <summary>The colony's home on the ground for these decrees (the seat's without a tracker).</summary>
        private static Map HomeOf(DecreeTracker t) => t != null ? t.Ground : Home;

        private static string Why(DecreeDef d, DecreeTracker t)
        {
            if (Hidden(d)) return "CC_Decree_NotForUs".Translate();
            if (!d.research.NullOrEmpty())
            {
                var known = d.research.Select(r => DefDatabase<ResearchProjectDef>.GetNamedSilentFail(r)).Where(r => r != null).ToList();
                if (!known.Any(r => r.IsFinished))
                    return known.Count == 0 ? "CC_Decree_NotForUs".Translate().Resolve() : "CC_Decree_NeedsResearch".Translate(known[0].LabelCap).Resolve();
            }
            // The colony's own home: its people, its stores.
            var map = HomeOf(t);
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
                var map = HomeOf(t);
                if (def.nightSleep) ApplyNight(t);
                if (def.callsTraders && map != null) CallTraders(def, map);
                if (!def.enactEffects.NullOrEmpty() && map != null)
                {
                    var a = asker != null && asker.Spawned && asker.Map == map ? asker : map.mapPawns.FreeColonistsSpawned.Where(PlayOuts.OurPerson).RandomElementWithFallback();
                    PlayOuts.Run(def.enactEffects, new DilemmaContext { map = map, a = a, faction = GoodwillFaction(def, map), why = def.LabelCap }, "decree " + def.defName);
                }
                if (def.oneFaith) DecreeFaith.Enacted(def);
                if (def.everyFaith && map != null) EveryFaithEvening(map);
                if (!def.daily.NullOrEmpty() && map != null) RunDaily(t, def, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree declared " + def.defName, e);
            }
        }

        public static void Ended(DecreeTracker t, DecreeDef def)
        {
            if (def.nightSleep && !t.active.Any(a => a.def != null && a.def.nightSleep)) RestoreNight(t);
            if (def.oneFaith) DecreeFaith.Ended(def);
            if (def.lightsOut && !t.active.Any(a => a.def != null && a.def.lightsOut)) LampsOn(t, HomeOf(t));
        }

        // ---------------- The hour ----------------

        public static void Hourly(DecreeTracker t)
        {
            try
            {
                bool night = t.active.Any(a => a.def != null && a.def.nightSleep);
                if (night) ApplyNight(t);
                else if (t.nightHours.Count > 0) RestoreNight(t);
                // Saving the power: the lamps out from eleven to five, on again as they were.
                var home = HomeOf(t);
                bool lamps = t.active.Any(a => a.def != null && a.def.lightsOut);
                if (lamps && home != null && YearFourMath.LightsOut(GenLocalDate.HourOfDay(home))) LampsOut(t, home);
                else if (t.lampsOff.Count > 0) LampsOn(t, home);
                foreach (var a in t.active)
                {
                    if (a.def == null || a.def.joyWhileWorking <= 0f) continue;
                    foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
                    {
                        if (p.needs?.joy == null || p.Downed || p.InMentalState || !p.Awake() || p.IsQuestLodger() || !t.Ours(p)) continue;
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
                // The colony's own stores, and its nearest neighbours.
                var map = HomeOf(t);
                int given = 0;
                bool gave = def.take == null;
                if (def.take != null && map != null)
                {
                    given = Take(map, def.take, t?.CostFactor ?? 1f);
                    gave = given > 0;
                }
                Faction to = null;
                if (def.goodwillPerDay != 0 && gave)
                {
                    to = GoodwillFaction(def, map);
                    to?.TryAffectGoodwillWith(Faction.OfPlayer, def.goodwillPerDay, false, false);
                }
                if (def.certaintyPerDay > 0f && ColonyFaith != null)
                {
                    float amount = def.certaintyPerDay * Interplay.FaithFactor(map);
                    foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
                        if (Believer(p) && !p.IsQuestLodger() && (t == null || t.Ours(p))) p.ideo?.OffsetCertainty(amount);
                }
                if (def.oneFaith) DecreeFaith.Daily(t, def);
                if (def.everyFaith) EveryFaithDaily(t, a);
                if (!def.daily.NullOrEmpty() && map != null) RunDaily(t, def, map);
                if (given > 0)
                {
                    string text = to != null
                        ? "CC_Decree_GaveTo".Translate(def.LabelCap, given, Label(def.take), to.Name).Resolve()
                        : "CC_Decree_Gave".Translate(def.LabelCap, given, Label(def.take)).Resolve();
                    Tell.Minor(NewsKind.Colony, Importance.Flavor, map, text, null, MessageTypeDefOf.NeutralEvent);
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

        /// <summary>
        /// Takes the day's share (the colony's quartermaster stretches it by <paramref name="costFactor"/>, as for any answer
        /// that costs the stores). Returns how much went.
        /// </summary>
        private static int Take(Map map, DecreeTake t, float costFactor)
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
            return Mathf.Max(1, Mathf.RoundToInt(want * costFactor));
        }

        // ---------------- Goodwill ----------------

        private static bool Usable(Faction f) =>
            f != null && !f.IsPlayer && !f.defeated && !f.Hidden && !f.temporary && f.HasGoodwill && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer);

        /// <summary>
        /// Who a decree's goodwill goes to today (see <see cref="DecreeGoodwillTo"/>), or null when there's nobody; the
        /// nearest measured from <paramref name="from"/>, the colony's home (the seat's without one).
        /// </summary>
        public static Faction GoodwillFaction(DecreeDef def, Map from = null)
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
                            var f = Nearest(x => x.ideos?.PrimaryIdeo == faith, from);
                            if (f != null) return f;
                        }
                        break;
                    }
                }
                return Nearest(null, from);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree goodwill", e);
                return null;
            }
        }

        /// <summary>The people of the nearest friendly settlement (that fits <paramref name="pred"/>) to the colony's home <paramref name="from"/>.</summary>
        private static Faction Nearest(Func<Faction, bool> pred, Map from)
        {
            var map = from ?? Home;
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
            Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_Decree_MarketNobody".Translate(def.label).Resolve(), null, MessageTypeDefOf.NeutralEvent);
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
                // The colony's own people: another colony keeps its own hours.
                if (t.nightHours.ContainsKey(p.ThingID) || !CurfewApplies(p) || !t.Ours(p)) continue;
                var changed = new List<string>();
                foreach (int h in NightHours)
                {
                    var a = p.timetable.GetAssignment(h);
                    if (a == null || a == sleep) continue;
                    // An hour today's rest day turned to recreation goes back to what it was before that, when the curfew ends.
                    string was = ColonyCalendar.Of(p)?.TakeRestHour(p, h) ?? a.defName;
                    changed.Add(h + "=" + was);
                    p.timetable.SetAssignment(h, sleep);
                }
                // Kept even when nothing changed, so they aren't looked at again every hour.
                t.nightHours[p.ThingID] = string.Join(";", changed);
            }
        }

        /// <summary>
        /// The curfew is over: each night hour still set to sleep goes back to what it was. Someone of the colony's away
        /// where its lists don't reach (carried off, say) keeps their note, and is put back when they come home (the hour
        /// checks again); the dead, those no longer the colony's and those the game no longer knows are let go.
        /// </summary>
        private static void RestoreNight(DecreeTracker t)
        {
            List<string> away = null;
            try
            {
                var sleep = TimeAssignmentDefOf.Sleep;
                var people = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList();
                List<Pawn> everyone = null;
                foreach (var kv in t.nightHours)
                {
                    if (kv.Value.NullOrEmpty()) continue;
                    var p = people.FirstOrDefault(x => x.ThingID == kv.Key);
                    if (p == null)
                    {
                        everyone ??= PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList();
                        var q = everyone.FirstOrDefault(x => x.ThingID == kv.Key);
                        // Not found anywhere: off at an outpost or aboard something the lists don't reach, else let go
                        // (a pawn the game no longer knows would otherwise be kept for ever).
                        bool keep = q == null ? CouncilGameComponent.KnownPawnIds().Contains(kv.Key) : !q.Dead && !q.Discarded && q.Faction == Faction.OfPlayer;
                        if (keep) (away ??= new List<string>()).Add(kv.Key);
                        continue;
                    }
                    if (p.timetable == null) continue;
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
                if (away == null) t.nightHours.Clear();
                else
                    foreach (var k in t.nightHours.Keys.Where(k => !away.Contains(k)).ToList())
                        t.nightHours.Remove(k);
            }
        }

        // ---------------- The land's year: what's marked each morning ----------------

        /// <summary>
        /// A decree's morning work marked (<see cref="DecreeDef.daily"/>: the hunters' game, trees for the cutters, wild plants
        /// for the pickers), at the colony's home on the ground; the day it's declared as well. Someone of the colony at home is
        /// A, for the effects that need one.
        /// </summary>
        private static void RunDaily(DecreeTracker t, DecreeDef def, Map map)
        {
            var a = map.mapPawns.FreeColonistsSpawned.Where(p => PlayOuts.OurPerson(p) && (t == null || t.Ours(p))).RandomElementWithFallback();
            if (a == null) return;
            PlayOuts.Run(def.daily, new DilemmaContext { map = map, a = a, why = def.LabelCap }, "decree daily " + def.defName);
        }

        // ---------------- Every faith its evening ----------------

        /// <summary>Declared: the largest faith at home that isn't the colony's own holds an evening of faith, when one can be held now.</summary>
        private static void EveryFaithEvening(Map map)
        {
            if (!FaithEvenings.On) return;
            var own = ColonyFaith;
            foreach (var f in Faiths.Census())
            {
                if (f?.ideo == null || f.ideo == own || !FaithEvenings.CanCall(map, f.ideo, out _)) continue;
                FaithEvenings.Call(map, f.ideo);
                return;
            }
        }

        /// <summary>
        /// The decree's morning: those of other faiths than the colony's trust the council a little more, the more open the
        /// more (<see cref="YearFourMath.ToleranceTrust"/>); the devout of the colony's own faith a little less. The trust log
        /// says why on the first morning and every fifth.
        /// </summary>
        private static void EveryFaithDaily(DecreeTracker t, ActiveDecree a)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !Faiths.On) return;
            var own = ColonyFaith;
            int day = a == null ? 1 : Mathf.Max(1, (Find.TickManager.TicksGame - a.startTick) / GenDate.TicksPerDay + 1);
            string why = day % 5 == 1 ? "CC_Log_EveryFaith".Translate().Resolve() : null;
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && (t == null || t.Ours(p))).ToList())
            {
                if (!Faiths.Holds(p)) continue;
                if (Faiths.Of(p) != own) comp.standings.Change(p, YearFourMath.ToleranceTrust(Persona.Get(p, PersonaAxis.Openness)), why);
                else if (Faiths.Devout(p)) comp.standings.Change(p, YearFourMath.DevoutMinds, why);
            }
        }

        // ---------------- Saving the power ----------------

        private static AccessTools.FieldRef<CompFlickable, bool> wantOn;
        private static bool wantLooked;

        /// <summary>The switch's wanted state, set with it so the game sends nobody to flick it back (null when the game keeps it elsewhere).</summary>
        private static AccessTools.FieldRef<CompFlickable, bool> WantOn
        {
            get
            {
                if (wantLooked) return wantOn;
                wantLooked = true;
                try
                {
                    wantOn = AccessTools.FieldRefAccess<CompFlickable, bool>("wantSwitchOn");
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("decree lamps switch", e);
                    wantOn = null;
                }
                return wantOn;
            }
        }

        /// <summary>A lamp of the colony's: a light on the grid that draws little (not a sun lamp over a field), with a switch.</summary>
        public static bool Lamp(Thing t)
        {
            if (!(t is ThingWithComps tw) || t.Faction != Faction.OfPlayer || !t.Spawned || t is Building_Turret) return false;
            if (tw.GetComp<CompGlower>() == null || tw.GetComp<CompFlickable>() == null) return false;
            var power = tw.GetComp<CompPowerTrader>();
            return power?.Props != null && power.Props.PowerConsumption > 0f && power.Props.PowerConsumption <= YearFourMath.LampWatts;
        }

        /// <summary>A patient lies abed in the lamp's room: that one stays lit.</summary>
        private static bool Sickroom(Thing t)
        {
            var room = t.GetRoom();
            return room != null && !room.PsychologicallyOutdoors && room.ContainedBeds.Any(b => b.Medical && b.CurOccupants.Any());
        }

        private static void Switch(CompFlickable f, bool on)
        {
            var want = WantOn;
            if (want == null || f.SwitchIsOn == on) return;
            want(f) = on;
            f.SwitchIsOn = on;
        }

        /// <summary>The night hours: each lamp of the colony's that is on (and not in a sickroom) is switched off and noted.</summary>
        private static void LampsOut(DecreeTracker t, Map map)
        {
            if (WantOn == null) return;
            try
            {
                int n = 0;
                foreach (var b in map.listerBuildings.allBuildingsColonist.ToList())
                {
                    if (!Lamp(b) || t.lampsOff.Contains(b.ThingID)) continue;
                    var f = b.GetComp<CompFlickable>();
                    if (!f.SwitchIsOn || Sickroom(b)) continue;
                    Switch(f, false);
                    t.lampsOff.Add(b.ThingID);
                    n++;
                }
                if (n > 0) Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_Decree_LampsOut".Translate(n).Resolve(), null, MessageTypeDefOf.SilentInput);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree lamps out", e);
            }
        }

        /// <summary>Five in the morning, or the decree over: each lamp switched off for the night and still off is switched on again.</summary>
        private static void LampsOn(DecreeTracker t, Map map)
        {
            try
            {
                if (map != null && WantOn != null)
                {
                    var ids = new HashSet<string>(t.lampsOff);
                    foreach (var b in map.listerBuildings.allBuildingsColonist.ToList())
                    {
                        if (!ids.Contains(b.ThingID)) continue;
                        var f = b.GetComp<CompFlickable>();
                        if (f != null && !f.SwitchIsOn) Switch(f, true);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree lamps on", e);
            }
            finally
            {
                t.lampsOff.Clear();
            }
        }

        // ---------------- A quarantine ----------------

        /// <summary>The factor on illnesses passing between people sharing a bedroom, from the decrees in force in the sick one's colony (1: none).</summary>
        public static float ContagionFactorFor(Pawn sick)
        {
            var active = (DecreeTracker.Of(sick) ?? CouncilGameComponent.Instance?.decrees)?.active;
            if (active == null || active.Count == 0) return 1f;
            float f = 1f;
            for (int i = 0; i < active.Count; i++)
                if (active[i]?.def != null) f *= active[i].def.contagionFactor;
            return Mathf.Clamp(f, 0f, 1f);
        }

        /// <summary>
        /// The factor on Rimfolk's visitor events at the colony of <paramref name="map"/> (the seat's for a map that isn't a
        /// home), from its decrees in force (1: none). A quarantine shuts its own gates, not another colony's.
        /// </summary>
        public static float VisitorFactor(Map map)
        {
            var active = ColonyState.For(map)?.decrees?.active;
            if (active == null || active.Count == 0) return 1f;
            float f = 1f;
            for (int i = 0; i < active.Count; i++)
                if (active[i]?.def != null) f *= active[i].def.visitorFactor;
            return Mathf.Clamp(f, 0.05f, 2f);
        }
    }
}
