using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What Rimfolk keeps about one pair of colonists, by thingIDNumber (the lower first): how well they know each other,
    /// their hours side by side lately, the fights they fought near each other, the seats one lost to the other, and an
    /// awkward spell after one turned the other down or they broke up. Saved as one short line (<see cref="PairBook"/>).
    /// </summary>
    public class PairRecord
    {
        public int a, b;
        /// <summary>How well they know each other, 0 to 100 (BondsMath).</summary>
        public float fam;
        /// <summary>Hours side by side lately: each day keeps BondsMath.RecentKeep of the last.</summary>
        public float recent;
        /// <summary>Fights fought near each other, and the tick of the last one (-1: none).</summary>
        public int battles, lastBattle = -1;
        /// <summary>Council seats a lost to b, and b to a.</summary>
        public int lostAB, lostBA;
        /// <summary>Until when things are awkward between them, and why (<see cref="AwkwardRebuffed"/> and the others).</summary>
        public int awkwardUntil = -1, awkwardKind;
        /// <summary>Who of the two last turned the other down or left them (thingIDNumber, -1: nobody), and when.</summary>
        public int turnedDownBy = -1, turnedDownTick = -1;
        /// <summary>When both were last among the colony's people (records of pairs gone a while are dropped).</summary>
        public int seen;

        public const int AwkwardNone = 0, AwkwardRebuffed = 1, AwkwardProposal = 2, AwkwardBreakup = 3;

        public bool Awkward(int now) => awkwardKind != AwkwardNone && now < awkwardUntil;

        public int LossesOf(Pawn loser) => loser == null ? 0 : loser.thingIDNumber == a ? lostAB : lostBA;

        /// <summary>One more seat lost by <paramref name="loser"/> to the other; the count with this one.</summary>
        public int AddLoss(Pawn loser) => loser.thingIDNumber == a ? ++lostAB : ++lostBA;

        private static string F(float x) => x.ToString("0.##", CultureInfo.InvariantCulture);

        public string Save() => string.Join(",", a, b, F(fam), F(recent), battles, lastBattle, lostAB, lostBA, awkwardUntil, awkwardKind, turnedDownBy, turnedDownTick, seen);

        public static PairRecord Parse(string line)
        {
            var p = line?.Split(',');
            if (p == null || p.Length < 13) return null;
            int I(int i) => int.TryParse(p[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
            float Fl(int i) => float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
            var r = new PairRecord
            {
                a = I(0), b = I(1), fam = Mathf.Clamp(Fl(2), 0f, BondsMath.KnowMax), recent = Mathf.Max(0f, Fl(3)), battles = I(4), lastBattle = I(5),
                lostAB = I(6), lostBA = I(7), awkwardUntil = I(8), awkwardKind = I(9), turnedDownBy = I(10), turnedDownTick = I(11), seen = I(12)
            };
            return r.a == r.b ? null : r;
        }
    }

    /// <summary>Every pair's record (Social/Bonds.cs), on the social web. Saved as a list of short lines.</summary>
    public class PairBook : IExposable
    {
        private List<string> pairs = new List<string>();
        [Unsaved] private Dictionary<long, PairRecord> byKey = new Dictionary<long, PairRecord>();
        /// <summary>Pairs already together when this began were given what they had already shared (once, on the first day).</summary>
        public bool seeded;

        /// <summary>The pair's record; with <paramref name="create"/>, a new one if there is none.</summary>
        public PairRecord Get(Pawn x, Pawn y, bool create)
        {
            if (x == null || y == null || x == y) return null;
            long key = PersonaMath.PairKey(x.thingIDNumber, y.thingIDNumber);
            if (byKey.TryGetValue(key, out var r) || !create) return r;
            r = new PairRecord
            {
                a = Mathf.Min(x.thingIDNumber, y.thingIDNumber), b = Mathf.Max(x.thingIDNumber, y.thingIDNumber), seen = Find.TickManager?.TicksGame ?? 0
            };
            byKey[key] = r;
            return r;
        }

        public IEnumerable<PairRecord> All => byKey.Values;

        public int Count => byKey.Count;

        /// <summary>Records of pairs not both among the colony's people for <paramref name="days"/> days are dropped.</summary>
        public void Prune(int now, int days)
        {
            foreach (var key in byKey.Where(kv => now - kv.Value.seen > days * GenDate.TicksPerDay).Select(kv => kv.Key).ToList()) byKey.Remove(key);
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) pairs = byKey.Values.Select(r => r.Save()).ToList();
            Scribe_Collections.Look(ref pairs, "pairs", LookMode.Value);
            Scribe_Values.Look(ref seeded, "seeded");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                byKey = new Dictionary<long, PairRecord>();
                foreach (var line in pairs ?? new List<string>())
                {
                    var r = PairRecord.Parse(line);
                    if (r != null) byKey[PersonaMath.PairKey(r.a, r.b)] = r;
                }
                pairs = new List<string>();
            }
        }
    }

    /// <summary>
    /// How bonds come about between colonists (the arithmetic is in BondsMath): how well two know each other, from time
    /// actually spent side by side; how two get on, from the game's compatibility, faith, views, shared passions and temper
    /// (the limits it sets on words and friendship); what people did to each other (harm, cruel words, a botched
    /// operation, an affair, envy, a doctor who pulled them through); and love's own troubles (jealousy, an awkward
    /// spell, losing out to someone else). Everything here comes of something that happened in the game, and shows with
    /// its cause. The social web (SocialWeb) holds the ties; this keeps the pairs' records and reads the game's events.
    /// </summary>
    public static class Bonds
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Friendships take time: how well two know each other counts (the setting).</summary>
        public static bool TakeTime => S != null && S.socialWeb && S.bondsTakeTime;

        /// <summary>How two get on sets limits (the setting): faith, views, passions and temper count with the game's compatibility.</summary>
        public static bool Limits => S != null && S.socialWeb && S.chemistryLimits;

        /// <summary>Comrades only from fighting near each other (the setting).</summary>
        public static bool Danger => S != null && S.socialWeb && S.comradesByDanger;

        /// <summary>What people did to each other starts grudges and earns gratitude (the setting).</summary>
        public static bool DeedsOn => S != null && S.socialWeb && S.deedsRemembered;

        /// <summary>Love's own troubles: jealousy, an awkward spell, losing out to someone else (the setting).</summary>
        public static bool Love => S != null && S.socialWeb && S.loveFeelings;

        public static PairBook Book => CouncilGameComponent.Instance?.social?.bonds;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>One of the colony's people with a social life (as the social web has them).</summary>
        public static bool Eligible(Pawn p) =>
            p != null && !p.Dead && People.HasInnerLife(p) && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby();

        // ---------------- How two get on ----------------

        /// <summary>How two get on, in parts, for the arithmetic and for the Social tab's explanation.</summary>
        public struct Reading
        {
            public float game, faith, views, interests, temper;
            public int agree, odds, shared, kind, abrasive;
            public bool ageGap;
            public float Total => game + faith + views + interests + temper;
        }

        private static readonly Dictionary<long, (int tick, Reading r)> readings = new Dictionary<long, (int, Reading)>();

        public static void ResetCaches()
        {
            readings.Clear();
            incidents.Clear();
            kinCache.Clear();
        }

        /// <summary>How two get on: the game's compatibility alone with the setting off. Worked out at most once an hour a pair.</summary>
        public static Reading Read(Pawn a, Pawn b)
        {
            var r = new Reading { game = Chemistry.Of(a, b) };
            if (!Limits || a == null || b == null || a == b) return r;
            long key = PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber);
            int now = Now;
            if (readings.TryGetValue(key, out var c) && now - c.tick >= 0 && now - c.tick < GenDate.TicksPerHour) return c.r;
            try
            {
                if (ModsConfig.IdeologyActive && a.Ideo != null && b.Ideo != null) r.faith = BondsMath.FaithPart(a.Ideo == b.Ideo ? 1 : -1);
                if (Persona.On)
                {
                    var pa = Persona.Of(a);
                    var pb = Persona.Of(b);
                    if (pa != null && pb != null)
                        foreach (var kv in pa.stances)
                        {
                            if (Mathf.Abs(kv.Value) < TalkMath.SideAt || !pb.stances.TryGetValue(kv.Key, out float vb) || Mathf.Abs(vb) < TalkMath.SideAt) continue;
                            if (Mathf.Sign(kv.Value) == Mathf.Sign(vb)) r.agree++;
                            else r.odds++;
                        }
                    r.views = BondsMath.ViewsPart(r.agree, r.odds);
                }
                if (a.skills != null && b.skills != null)
                    foreach (var sa in a.skills.skills)
                        if (People.Burning(sa) && People.Burning(b.skills.GetSkill(sa.def))) r.shared++;
                r.interests = BondsMath.InterestsPart(r.shared);
                r.kind = (Has(a, TraitDefOf.Kind) ? 1 : 0) + (Has(b, TraitDefOf.Kind) ? 1 : 0);
                r.abrasive = (Has(a, TraitDefOf.Abrasive) ? 1 : 0) + (Has(b, TraitDefOf.Abrasive) ? 1 : 0);
                r.temper = BondsMath.TemperPart(r.kind, r.abrasive);
                r.ageGap = a.ageTracker != null && b.ageTracker != null && Mathf.Abs(a.ageTracker.AgeBiologicalYearsFloat - b.ageTracker.AgeBiologicalYearsFloat) >= 15f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("how two get on: the parts", e);
            }
            if (readings.Count > 2000) readings.Clear();
            readings[key] = (now, r);
            return r;
        }

        public static float Affinity(Pawn a, Pawn b) => Read(a, b).Total;

        private static bool Has(Pawn p, TraitDef t) => t != null && (p.story?.traits?.HasTrait(t) ?? false);

        /// <summary>A temper that holds on to a grudge: abrasive, volatile or jealous.</summary>
        private static bool Hot(Pawn p) => Has(p, TraitDefOf.Abrasive) || Has(p, TraitDefOf.Jealous) || PeopleTags.Has(p, "trait:Volatile");

        /// <summary>
        /// How much a grudge between the two grows from what happened: as their tempers take it (a kind soul less, a hot
        /// one more) and as they get on (more between two who don't click).
        /// </summary>
        public static float GrudgeFactor(Pawn x, Pawn y)
        {
            int kind = (Has(x, TraitDefOf.Kind) ? 1 : 0) + (Has(y, TraitDefOf.Kind) ? 1 : 0);
            int hot = (Hot(x) ? 1 : 0) + (Hot(y) ? 1 : 0);
            return BondsMath.GrudgeGrowth(kind, hot) * ChemistryMath.SoreFactor(Affinity(x, y));
        }

        /// <summary>A day of a grudge between the two (held a while after it was renewed, then fading as their tempers let it).</summary>
        public static float GrudgeDay(SocialTie t)
        {
            int kind = (Has(t.a, TraitDefOf.Kind) ? 1 : 0) + (Has(t.b, TraitDefOf.Kind) ? 1 : 0);
            int hot = (Hot(t.a) ? 1 : 0) + (Hot(t.b) ? 1 : 0);
            int days = t.renewedTick < 0 ? 999 : (Now - t.renewedTick) / GenDate.TicksPerDay;
            return BondsMath.GrudgeDay(t.strength, days, kind, hot);
        }

        // ---------------- Knowing each other ----------------

        /// <summary>
        /// How well the two know each other (0 to 100); as well as can be with the setting off. Family and partners know
        /// each other well whatever the record says.
        /// </summary>
        public static float Familiarity(Pawn a, Pawn b)
        {
            if (!TakeTime) return BondsMath.KnowMax;
            float fam = Book?.Get(a, b, false)?.fam ?? 0f;
            return fam < BondsMath.KinKnow && Kin(a, b) ? BondsMath.KinKnow : fam;
        }

        private static readonly Dictionary<long, (int tick, bool kin)> kinCache = new Dictionary<long, (int, bool)>();

        /// <summary>Family by blood or partners (worked out at most once an hour a pair).</summary>
        private static bool Kin(Pawn a, Pawn b)
        {
            if (a?.relations == null || b == null || a == b) return false;
            long key = PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber);
            int now = Now;
            if (kinCache.TryGetValue(key, out var c) && now - c.tick >= 0 && now - c.tick < GenDate.TicksPerHour) return c.kin;
            bool kin = false;
            try
            {
                kin = LovePartnerRelationUtility.LovePartnerRelationExists(a, b) || a.GetRelations(b).Any(r => r.familyByBloodRelation);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bonds: family", e);
            }
            if (kinCache.Count > 2000) kinCache.Clear();
            kinCache[key] = (now, kin);
            return kin;
        }

        /// <summary>They came to know each other a little better (a talk, something gone through together, a fight side by side).</summary>
        public static void Know(Pawn a, Pawn b, float amount)
        {
            if (!TakeTime || amount <= 0f || !Eligible(a) || !Eligible(b) || a == b) return;
            var r = Book?.Get(a, b, true);
            if (r != null) r.fam = Mathf.Min(BondsMath.KnowMax, r.fam + amount);
        }

        /// <summary>How close a friendship between the two can get now: how they get on, and how well they know each other.</summary>
        public static float FriendCap(Pawn a, Pawn b)
        {
            float cap = 100f;
            if (Limits) cap = Mathf.Min(cap, BondsMath.FriendCapByChemistry(Affinity(a, b)));
            if (TakeTime) cap = Mathf.Min(cap, BondsMath.FriendCapByKnowing(Familiarity(a, b)));
            return cap;
        }

        /// <summary>A friendship can form between the two, at this mutual opinion (the lower of the two).</summary>
        public static bool FriendsForm(Pawn a, Pawn b, int low)
        {
            float aff = Limits ? Affinity(a, b) : 0f;
            if (low < (Limits ? BondsMath.FormOpinion(aff) : 45f)) return false;
            if (TakeTime && Familiarity(a, b) < BondsMath.FormKnowing(aff)) return false;
            return FriendCap(a, b) >= BondsMath.AnnouncedAt;
        }

        /// <summary>Where a friendship between the two drifts to: how they feel about each other, no further than it can go.</summary>
        public static float FriendTarget(Pawn a, Pawn b, int low) => Mathf.Min(low - 25f, FriendCap(a, b));

        /// <summary>
        /// Once an hour: every two of the colony's people up and about and side by side (close, and in the same room or in
        /// sight) come to know each other a little better, more over a meal or recreation together.
        /// </summary>
        public static void Hourly()
        {
            var book = Book;
            if (!TakeTime || book == null) return;
            int now = Now;
            foreach (var map in Find.Maps)
            {
                var people = new List<Pawn>();
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (Eligible(p) && !p.Downed && !p.Drafted && p.Awake()) people.Add(p);
                if (people.Count < 2) continue;
                for (int i = 0; i < people.Count; i++)
                    for (int j = i + 1; j < people.Count; j++)
                    {
                        var x = people[i];
                        var y = people[j];
                        // together: the two are side by side right now (SocialLog.Near).
                        if (!x.Position.InHorDistOf(y.Position, SocialLog.TalkRange) || !SocialLog.Near(x, y)) continue;
                        var r = book.Get(x, y, true);
                        bool sharing = Sharing(x) && Sharing(y);
                        r.fam = Mathf.Min(BondsMath.KnowMax, r.fam + BondsMath.HourGain(r.fam, sharing, Affinity(x, y), r.Awkward(now)));
                        r.recent += 1f;
                    }
            }
        }

        /// <summary>At a meal or in their recreation: time that counts for more between two together.</summary>
        private static bool Sharing(Pawn p)
        {
            var job = p.CurJobDef;
            return job != null && (job == JobDefOf.Ingest || job.joyKind != null);
        }

        /// <summary>
        /// Once a day: the pairs' recent hours settle, two who no longer see each other drift apart, records of pairs gone
        /// a while are dropped. On the first day, pairs already together are given what they have shared so far.
        /// </summary>
        public static void Daily(SocialWeb web, List<Pawn> pawns)
        {
            var book = web.bonds;
            if (book == null) return;
            int now = Now;
            if (!book.seeded)
            {
                book.seeded = true;
                Seed(web, pawns, now);
            }
            var ids = new HashSet<int>();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
                if (Eligible(p)) ids.Add(p.thingIDNumber);
            foreach (var r in book.All)
            {
                if (!ids.Contains(r.a) || !ids.Contains(r.b)) continue;
                r.seen = now;
                if (!TakeTime) continue;
                r.recent *= BondsMath.RecentKeep;
                r.fam = Mathf.Max(0f, r.fam + BondsMath.DayDrift(r.fam, r.recent));
            }
            book.Prune(now, 60);
            if (DeedsOn) Envy(web, pawns);
        }

        /// <summary>
        /// The first day on a save from before: two who have lived side by side know each other by now (from their days
        /// together, a little more for a friendship), and comradeships count the battles their strength shows at least,
        /// as of today. Nothing jumps: what follows fades or grows from there.
        /// </summary>
        private static void Seed(SocialWeb web, List<Pawn> pawns, int now)
        {
            var book = web.bonds;
            for (int i = 0; i < pawns.Count; i++)
                for (int j = i + 1; j < pawns.Count; j++)
                {
                    var r = book.Get(pawns[i], pawns[j], true);
                    r.fam = Mathf.Max(r.fam, BondsMath.Seed(Chemistry.DaysTogether(pawns[i], pawns[j]), web.Strength(pawns[i], pawns[j], TieKind.Friends)));
                }
            foreach (var t in web.ties)
            {
                if (t.kind == TieKind.Comrades && Eligible(t.a) && Eligible(t.b))
                {
                    var r = book.Get(t.a, t.b, true);
                    r.battles = Mathf.Max(r.battles, BondsMath.SeedBattles(t.strength));
                    r.lastBattle = now;
                }
                if (t.kind == TieKind.Rivals && t.renewedTick < 0) t.renewedTick = now;
            }
        }

        // ---------------- Envy ----------------

        /// <summary>
        /// A jealous colonist envies whoever has a much better bedroom; the jealous and the greedy envy one who holds a
        /// royal title above theirs. A little each day while it lasts, never toward a partner, family or a good friend.
        /// </summary>
        private static void Envy(SocialWeb web, List<Pawn> pawns)
        {
            // apart: envy of what someone has (their room, their title), noted as a grudge with its cause.
            var rooms = new Dictionary<Pawn, float>();
            foreach (var p in pawns) rooms[p] = BedroomImpressiveness(p);
            foreach (var p in pawns)
            {
                bool jealous = Has(p, TraitDefOf.Jealous), greedy = Has(p, TraitDefOf.Greedy);
                if (!jealous && !greedy) continue;
                if (jealous)
                {
                    var best = pawns.Where(o => o != p && rooms[o] - rooms[p] >= BondsMath.EnvyRoomGap && Envies(web, p, o)).OrderByDescending(o => rooms[o]).FirstOrDefault();
                    if (best != null) Envied(web, p, best, "CC_TieNote_EnvyRoom".Translate(best.LabelShort).Resolve());
                }
                if (ModsConfig.RoyaltyActive)
                {
                    int mine = p.royalty?.MostSeniorTitle?.def?.seniority ?? -1;
                    var titled = pawns.Where(o => o != p && (o.royalty?.MostSeniorTitle?.def?.seniority ?? -1) > mine && Envies(web, p, o))
                        .OrderByDescending(o => o.royalty.MostSeniorTitle.def.seniority).FirstOrDefault();
                    if (titled != null) Envied(web, p, titled, "CC_TieNote_EnvyTitle".Translate(titled.LabelShort, titled.royalty.MostSeniorTitle.def.GetLabelFor(titled)).Resolve());
                }
            }
        }

        private static bool Envies(SocialWeb web, Pawn p, Pawn o) =>
            !LovePartnerRelationUtility.LovePartnerRelationExists(p, o) && !p.GetRelations(o).Any(r => r.familyByBloodRelation) && web.Strength(p, o, TieKind.Friends) < 45f;

        /// <summary>A day's envy: the note goes into the tie's history once, not every day.</summary>
        private static void Envied(SocialWeb web, Pawn p, Pawn o, string note)
        {
            // apart: envy of what the other has, felt from afar.
            var t = web.Get(p, o, TieKind.Rivals);
            bool noted = t != null && (t.cause == note || t.history.Any(h => h.EndsWith(note, StringComparison.Ordinal)));
            web.Bump(p, o, TieKind.Rivals, BondsMath.EnvyPerDay, noted ? null : note);
        }

        private static float BedroomImpressiveness(Pawn p)
        {
            var bed = p.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned) return 0f;
            var room = bed.GetRoom();
            if (room == null || room.PsychologicallyOutdoors || room.Role != RoomRoleDefOf.Bedroom) return 0f;
            return room.GetStat(RoomStatDefOf.Impressiveness);
        }

        // ---------------- The game's events between two ----------------

        /// <summary>The same harm between the same two within a while is one incident (a fight's many blows, a burst of fire).</summary>
        private static readonly Dictionary<string, int> incidents = new Dictionary<string, int>();

        private static bool Fresh(string key, int hours)
        {
            int now = Now;
            if (incidents.TryGetValue(key, out int at) && now - at >= 0 && now - at < hours * GenDate.TicksPerHour) return false;
            if (incidents.Count > 400) incidents.Clear();
            incidents[key] = now;
            return true;
        }

        private static string PairTag(Pawn x, Pawn y, string what) => PersonaMath.PairKey(x.thingIDNumber, y.thingIDNumber) + "|" + what;

        /// <summary>
        /// A memory the game (or a mod) gave <paramref name="holder"/> about <paramref name="other"/>: talk makes them know
        /// each other better; harm, cross words, a botched operation and an affair start grudges; a rejection or a breakup
        /// leaves an awkward spell. Called after the memory was given (the game's own checks passed).
        /// </summary>
        public static void Notify_Memory(Pawn holder, Thought_Memory m, Pawn other)
        {
            // together: the game's own interaction, fight or deed between the two, as it happens (its memory says so).
            if (holder == null || other == null || holder == other || m?.def == null || S == null || !S.socialWeb) return;
            if (!Eligible(holder) || !Eligible(other)) return;
            var web = CouncilGameComponent.Instance?.social;
            if (web == null) return;
            switch (m.def.defName)
            {
                case "Insulted":
                case "Slighted":
                    if (DeedsOn)
                        web.Bump(holder, other, TieKind.Rivals, m.def.defName == "Insulted" ? BondsMath.Insult : BondsMath.Slight, "CC_TieNote_CrossWords".Translate().Resolve());
                    return;
                case "HadAngeringFight":
                    if (DeedsOn && Fresh(PairTag(holder, other, "blows"), BondsMath.IncidentHours))
                        web.Bump(holder, other, TieKind.Rivals, BondsMath.CameToBlows, "CC_TieNote_Fight".Translate().Resolve());
                    return;
                case "HarmedMe":
                    if (DeedsOn) Harmed(web, holder, other);
                    return;
                case "BotchedMySurgery":
                    if (DeedsOn) web.Bump(holder, other, TieKind.Rivals, BondsMath.BotchedSurgery, "CC_TieNote_Botched".Translate().Resolve());
                    return;
                case "CheatedOnMe":
                    if (Love) Cheated(web, holder, other);
                    return;
                case "RebuffedMyRomanceAttempt":
                    TurnedDown(holder, other, PairRecord.AwkwardRebuffed, 10);
                    return;
                case "RejectedMyProposal":
                    TurnedDown(holder, other, PairRecord.AwkwardProposal, 10);
                    return;
                case "BrokeUpWithMe":
                case "DivorcedMe":
                    TurnedDown(holder, other, PairRecord.AwkwardBreakup, 20);
                    return;
            }
            // A warm word between them: they come to know each other a little better (a deep talk about a point a side).
            if (TakeTime && Chemistry.Spoken(m.def))
            {
                float offset = m.def.stages != null && m.def.stages.Count > 0 && m.def.stages[0] != null ? m.def.stages[0].baseOpinionOffset : 0f;
                Know(holder, other, BondsMath.TalkGain(offset, Familiarity(holder, other)));
            }
        }

        /// <summary>Hurt by one of their own: in a rage, or in a fight (friendly fire). A social fight is the fight's own memory.</summary>
        private static void Harmed(SocialWeb web, Pawn holder, Pawn other)
        {
            // together: the blow itself was struck on the map (the game's memory of it).
            var state = other.MentalStateDef;
            if (state == MentalStateDefOf.SocialFighting) return;
            if (other.InMentalState)
            {
                if (Fresh(PairTag(holder, other, "rage"), BondsMath.IncidentHours))
                    web.Bump(holder, other, TieKind.Rivals, BondsMath.AttackedInRage, "CC_TieNote_Rage".Translate().Resolve());
                return;
            }
            var map = other.MapHeld ?? holder.MapHeld;
            if ((other.Drafted || (map != null && MapFacts.ThreatOn(map))) && Fresh(PairTag(holder, other, "fire"), BondsMath.IncidentHours))
                web.Bump(holder, other, TieKind.Rivals, BondsMath.HitInFight, "CC_TieNote_FriendlyFire".Translate().Resolve());
        }

        /// <summary>A partner cheated: a grudge toward them, and toward the one they were with (the partner of their lovin, if it shows).</summary>
        private static void Cheated(SocialWeb web, Pawn holder, Pawn cheat)
        {
            // apart: the game's own news of an affair (its memory), held against the two who had it.
            if (!Fresh(PairTag(holder, cheat, "cheat"), 24)) return;
            web.Bump(holder, cheat, TieKind.Rivals, BondsMath.CheatedOn, "CC_TieNote_Cheated".Translate().Resolve());
            var with = cheat.CurJobDef == JobDefOf.Lovin ? cheat.CurJob?.targetA.Thing as Pawn : null;
            if (with != null && with != holder && with != cheat && Eligible(with))
                web.Bump(holder, with, TieKind.Rivals, BondsMath.TookUpWith, "CC_TieNote_Affair".Translate(cheat.LabelShort).Resolve());
        }

        /// <summary><paramref name="by"/> turned <paramref name="asker"/> down (or left them): an awkward spell between them, and who it was.</summary>
        private static void TurnedDown(Pawn asker, Pawn by, int kind, int days)
        {
            var r = Book?.Get(asker, by, true);
            if (r == null) return;
            int now = Now;
            r.turnedDownBy = by.thingIDNumber;
            r.turnedDownTick = now;
            if (!Love) return;
            r.awkwardKind = kind;
            r.awkwardUntil = Mathf.Max(r.awkwardUntil, now + days * GenDate.TicksPerDay);
        }

        /// <summary>
        /// Two of the colony became lovers (or engaged, or wed): anyone the new partner turned down or left in the last
        /// month lost out to the other one, and minds it.
        /// </summary>
        public static void Notify_RelationAdded(Pawn pawn, PawnRelationDef def, Pawn other)
        {
            // apart: news of a new couple, felt by someone who lost out; noted as a grudge with its cause.
            if (!Love || !DeedsOn || def == null || !LovePartnerRelationUtility.IsLovePartnerRelation(def) || !Eligible(pawn) || !Eligible(other)) return;
            var book = Book;
            var web = CouncilGameComponent.Instance?.social;
            if (book == null || web == null) return;
            int now = Now;
            foreach (var (beloved, winner) in new[] { (pawn, other), (other, pawn) })
                foreach (var x in PawnsFinder.AllMaps_FreeColonists.ToList())
                {
                    if (x == pawn || x == other || !Eligible(x)) continue;
                    var r = book.Get(x, beloved, false);
                    if (r == null || r.turnedDownBy != beloved.thingIDNumber || now - r.turnedDownTick > 30 * GenDate.TicksPerDay) continue;
                    if (!Fresh(PairTag(x, winner, "heart" + beloved.thingIDNumber), 24 * 30)) continue;
                    web.Bump(x, winner, TieKind.Rivals, BondsMath.WonTheirHeart, "CC_TieNote_WonHeart".Translate(beloved.LabelShort).Resolve());
                }
        }

        /// <summary>
        /// A friendship was told (or became close): a partner of one of them who minds it (the jealous, or one who doesn't
        /// much like the friend and could see why their partner would) is jealous of the friend.
        /// </summary>
        public static void Notify_FriendTie(SocialTie t, bool close)
        {
            // apart: news of a friendship, felt by a partner; a memory and a grudge with its cause.
            if (!Love || t?.a == null || t.b == null) return;
            var web = CouncilGameComponent.Instance?.social;
            if (web == null) return;
            foreach (var (friend, other) in new[] { (t.a, t.b), (t.b, t.a) })
            {
                if (friend.relations == null || !Eligible(friend) || !Eligible(other)) continue;
                foreach (var rel in friend.relations.DirectRelations.ToList())
                {
                    var partner = rel.otherPawn;
                    if (!LovePartnerRelationUtility.IsLovePartnerRelation(rel.def) || partner == other || !Eligible(partner) || partner.relations == null) continue;
                    if (Has(partner, TraitDefOf.Psychopath)) continue;
                    bool minds = Has(partner, TraitDefOf.Jealous)
                                 || (!Has(partner, TraitDefOf.Kind) && partner.relations.OpinionOf(other) < 15 && friend.relations.SecondaryLovinChanceFactor(other) > 0.25f);
                    if (!minds || !Fresh(PairTag(partner, other, close ? "close" : "jealous"), 24 * 20)) continue;
                    CouncilGameComponent.GiveThought(partner, CouncilDefOf.Council_JealousOf, other, -1f, null, close ? 1 : 0);
                    if (DeedsOn) web.Bump(partner, other, TieKind.Rivals, close ? BondsMath.JealousClose : BondsMath.JealousFormed, "CC_TieNote_Jealous".Translate(friend.LabelShort).Resolve());
                }
            }
        }

        // ---------------- Gratitude ----------------

        /// <summary>A doctor of the colony pulled <paramref name="patient"/> through a dangerous illness, or did a major operation well.</summary>
        public static void Notify_Healed(Pawn doctor, Pawn patient, bool surgery)
        {
            // apart: a patient grateful to the doctor who saw them through (the care itself was given at the bedside).
            if (!DeedsOn || !Eligible(doctor) || !Eligible(patient) || doctor == patient) return;
            CouncilGameComponent.GiveThought(patient, CouncilDefOf.Council_GratefulTo, doctor, -1f, null, surgery ? 1 : 0);
            Know(doctor, patient, 2f);
        }

        // ---------------- The Social tab ----------------

        /// <summary>
        /// A line or two about the pair for the game's Social tab: how well they know each other, how they get on and why
        /// (and what that allows), and an awkward spell. Null if there's nothing to say.
        /// </summary>
        public static string PairLines(Pawn a, Pawn b)
        {
            if (S == null || !S.socialWeb || !Eligible(a) || !Eligible(b)) return null;
            var parts = new List<string>();
            int now = Now;
            if (TakeTime)
            {
                var r = Book?.Get(a, b, false);
                int stage = BondsMath.KnowStage(Familiarity(a, b));
                parts.Add(("CC_Bonds_Know_" + stage).Translate().Resolve());
                if (r != null && r.Awkward(now)) parts.Add(("CC_Bonds_Awkward_" + r.awkwardKind).Translate().Resolve());
            }
            if (Limits && !LovePartnerRelationUtility.LovePartnerRelationExists(a, b))
            {
                var read = Read(a, b);
                float aff = read.Total;
                var why = Reasons(a, b, read, aff < 0f);
                string because = why.Count > 0 ? " (" + string.Join(", ", why) + ")" : "";
                if (aff <= BondsMath.WorseAt) parts.Add("CC_Bonds_Grate".Translate(because.Named("WHY")).Resolve());
                else if (aff <= BondsMath.PoorAt) parts.Add("CC_Bonds_NoClick".Translate(because.Named("WHY")).Resolve());
                else if (aff < 0f) parts.Add("CC_Bonds_Mismatch".Translate(because.Named("WHY")).Resolve());
                else if (aff >= BondsMath.GoodAt) parts.Add("CC_Bonds_Click".Translate(because.Named("WHY")).Resolve());
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        /// <summary>What pushes the two apart (<paramref name="apart"/>) or together: only the parts that point that way.</summary>
        private static List<string> Reasons(Pawn a, Pawn b, Reading r, bool apart)
        {
            var why = new List<string>();
            float sign = apart ? -1f : 1f;
            if (r.game * sign >= 0.6f) why.Add((apart ? (r.ageGap ? "CC_Bonds_Why_Years" : "CC_Bonds_Why_Little") : "CC_Bonds_Why_Natural").Translate().Resolve());
            if (r.faith * sign > 0f) why.Add((apart ? "CC_Bonds_Why_Faiths" : "CC_Bonds_Why_Faith").Translate().Resolve());
            if (r.views * sign > 0f) why.Add((apart ? "CC_Bonds_Why_Odds" : "CC_Bonds_Why_Agree").Translate().Resolve());
            if (!apart && r.interests > 0f) why.Add("CC_Bonds_Why_Passion".Translate().Resolve());
            if (apart && r.abrasive > 0)
                foreach (var p in new[] { a, b }.Where(p => Has(p, TraitDefOf.Abrasive))) why.Add("CC_Bonds_Why_Tongue".Translate(p.Named("PAWN")).Resolve());
            if (!apart && r.kind > 0)
                foreach (var p in new[] { a, b }.Where(p => Has(p, TraitDefOf.Kind))) why.Add("CC_Bonds_Why_Kind".Translate(p.Named("PAWN")).Resolve());
            return why;
        }

        /// <summary>For a comradeship's tooltip: the fights the two fought near each other.</summary>
        public static int BattlesTogether(Pawn a, Pawn b) => Book?.Get(a, b, false)?.battles ?? 0;
    }

    /// <summary>
    /// A memory given between two of the colony (Social/Bonds.cs): what passed between them makes them know each other,
    /// what one did to the other starts a grudge, a rejection leaves an awkward spell. After the game gave it.
    /// </summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_BondsMemory
    {
        public static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
        {
            if (!RimfolkPatches.On || newThought == null || __instance?.pawn == null) return;
            // Refused by the game's own checks (a trait that spares them, a stage filter): it was never given.
            if (newThought.pawn != __instance.pawn) return;
            try
            {
                Bonds.Notify_Memory(__instance.pawn, newThought, otherPawn ?? newThought.otherPawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bonds: a memory", e);
            }
        }
    }

    /// <summary>Two of the colony became lovers, engaged or wed (Social/Bonds.cs): whoever lost out minds it.</summary>
    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation))]
    public static class Patch_BondsCouple
    {
        public static void Postfix(Pawn ___pawn, PawnRelationDef def, Pawn otherPawn)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                Bonds.Notify_RelationAdded(___pawn, def, otherPawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bonds: a couple", e);
            }
        }
    }
}
