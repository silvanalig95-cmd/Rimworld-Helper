using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The council and the colony's life: setbacks that keep coming turn into wishes and stories, strain into wishes for
    // rest, promises taken back and earned names into votes, traditions into the colony's ways and the ways into who
    // minds an answer, and a confided secret into a remark at the council table.
    public static partial class Interplay
    {
        // ---------------- Setbacks that keep coming ----------------

        /// <summary>What kind of trouble a setback is, for what it makes someone want.</summary>
        public enum Trouble
        {
            Work,
            Sleep,
            Company,
            Food,
            Care,
            Council
        }

        private static readonly Dictionary<string, Trouble> TroubleOf = new Dictionary<string, Trouble>
        {
            ["Council_Setback_WorkWrecked"] = Trouble.Work,
            ["Council_Setback_WorkTornDown"] = Trouble.Work,
            ["Council_Setback_BotchedBuild"] = Trouble.Work,
            ["Council_Setback_ShoddyWork"] = Trouble.Work,
            ["Council_Setback_MakingDo"] = Trouble.Work,
            ["Council_Setback_BotchedSurgery"] = Trouble.Work,
            ["Council_Setback_ArtSold"] = Trouble.Work,
            ["Council_Setback_NoisyNeighbour"] = Trouble.Sleep,
            ["Council_Setback_MissedGathering"] = Trouble.Company,
            ["Council_Setback_DriftedApart"] = Trouble.Company,
            ["Council_Setback_QuietDays"] = Trouble.Company,
            ["Council_Setback_Homesick"] = Trouble.Company,
            ["Council_Setback_NoPlate"] = Trouble.Food,
            ["Council_Setback_CropLost"] = Trouble.Food,
            ["Council_Setback_LeftWaiting"] = Trouble.Care,
            ["Council_Setback_SickFriend"] = Trouble.Care,
            ["Council_Setback_StillWaiting"] = Trouble.Council,
            ["Council_Setback_IdeaPassedOver"] = Trouble.Council,
            ["Council_Setback_NotAgreed"] = Trouble.Council,
            ["Council_Setback_ToldThemSo"] = Trouble.Council,
            ["Council_Setback_DecreeDrags"] = Trouble.Council
        };

        /// <summary>The wishes a run of each kind of trouble makes likelier.</summary>
        private static readonly Dictionary<Trouble, string[]> WishesFor = new Dictionary<Trouble, string[]>
        {
            [Trouble.Work] = new[] { "Council_Wish_ToolCabinet", "Council_Wish_PassionSpace", "Council_Wish_Lessons", "Council_Wish_LetMeWork" },
            [Trouble.Sleep] = new[] { "Council_Wish_OwnRoom", "Council_Wish_Separate", "Council_Wish_Spacious" },
            [Trouble.Company] = new[] { "Council_Wish_Party", "Council_Wish_Music", "Council_Wish_RecreationVariety", "Council_Wish_CampEvening", "Council_Wish_Meeting" },
            [Trouble.Food] = new[] { "Council_Wish_Freezer", "Council_Wish_FoodThatKeeps", "Council_Wish_BetterFood", "Council_Wish_FreshFood" },
            [Trouble.Care] = new[] { "Council_Wish_Infirmary", "Council_Wish_Sickbed", "Council_Wish_Healroot", "Council_Wish_SickbedVisit" },
            [Trouble.Council] = new[] { "Council_Wish_Meeting" }
        };

        /// <summary>Wishes for rest and comfort, likelier for the worn out.</summary>
        private static readonly HashSet<string> RestWishes = new HashSet<string>
        {
            "Council_Wish_DayOff", "Council_Wish_Bath", "Council_Wish_Sauna", "Council_Wish_Tea", "Council_Wish_GardenSeat", "Council_Wish_Armchair",
            "Council_Wish_Music", "Council_Wish_HotWater", "Council_Wish_Outside"
        };

        /// <summary>The days (of the game) each colonist had a setback of each kind lately, by their ID. Not saved.</summary>
        private static readonly Dictionary<int, List<(int day, Trouble t)>> setbackCounts = new Dictionary<int, List<(int, Trouble)>>();

        /// <summary>A run of trouble: this many setbacks of one kind...</summary>
        public const int RunCount = 2;

        /// <summary>...within this many days.</summary>
        public const int RunDays = 10;

        /// <summary>Once a day: the setbacks each colonist had since yesterday, by kind (from their memories).</summary>
        private static void CountSetbacks()
        {
            int today = Now / Day;
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
            {
                var memories = p.needs?.mood?.thoughts?.memories?.Memories;
                if (memories == null || !Ours(p)) continue;
                List<(int, Trouble)> list = null;
                foreach (var m in memories)
                {
                    if (m?.def == null || m.age >= Day || !TroubleOf.TryGetValue(m.def.defName, out var t)) continue;
                    if (list == null && !setbackCounts.TryGetValue(p.thingIDNumber, out list)) setbackCounts[p.thingIDNumber] = list = new List<(int, Trouble)>();
                    list.Add((today, t));
                }
            }
            foreach (var id in setbackCounts.Keys.ToList())
            {
                var list = setbackCounts[id];
                list.RemoveAll(x => today - x.day > RunDays);
                if (list.Count == 0) setbackCounts.Remove(id);
            }
        }

        /// <summary>Whether this colonist has had a run of this kind of trouble lately.</summary>
        public static bool HadRun(Pawn p, Trouble t)
        {
            if (p == null || !setbackCounts.TryGetValue(p.thingIDNumber, out var list)) return false;
            int n = 0;
            foreach (var x in list)
                if (x.t == t && ++n >= RunCount)
                    return true;
            return false;
        }

        /// <summary>
        /// How much likelier a wish is for this colonist (CouncilGameComponent.TryGenerateWish), from the rest of their
        /// life: a run of setbacks of one kind makes the wishes that answer it likelier (work wrecked twice: a proper
        /// workshop; kept awake twice: a room of their own), and a worn-out heart makes wishes for rest likelier. At most
        /// two fifths again as likely; never less.
        /// </summary>
        public static float WishFactor(Pawn p, WishDef def)
        {
            if (!On || p == null || def == null) return 1f;
            float f = 1f;
            if (setbackCounts.Count > 0 && setbackCounts.ContainsKey(p.thingIDNumber))
                foreach (var kv in WishesFor)
                    if (System.Array.IndexOf(kv.Value, def.defName) >= 0 && HadRun(p, kv.Key))
                        f = Mathf.Max(f, 1.4f);
            if (Heart.On && RestWishes.Contains(def.defName) && Heart.BandOf(p) >= StrainBand.Worn) f = Mathf.Max(f, 1.3f);
            return Mathf.Clamp(f, 1f, 1.4f);
        }

        /// <summary>
        /// How much likelier a personal story is to begin for this colonist (ArcTracker.Candidates): work that keeps
        /// going wrong makes a story of mastering a craft likelier, the council letting them down again and again one of
        /// second chances, and missing the colony's company one of finding a place or a companion. Twice as likely.
        /// </summary>
        public static float ArcWeight(ArcDef def, Pawn p)
        {
            if (!On || def == null || p == null || setbackCounts.Count == 0 || !setbackCounts.ContainsKey(p.thingIDNumber)) return 1f;
            var w = def.Worker;
            if ((w is ArcWorker_Apprentice || w is ArcWorker_Calling) && HadRun(p, Trouble.Work)) return 2f;
            if (w is ArcWorker_Doubt && HadRun(p, Trouble.Council)) return 2f;
            if ((w is ArcWorker_Companion || w is ArcWorker_Newcomer) && HadRun(p, Trouble.Company)) return 2f;
            return 1f;
        }

        // ---------------- The elections ----------------

        /// <summary>
        /// How a voter leans toward a candidate beyond skill and liking (OfficeTracker.Score): someone whose granted
        /// wish was taken back in the last year holds it against everyone sitting on the council; the names the colony
        /// gave a candidate count for them (a dubious one against).
        /// </summary>
        public static float VoteLean(Pawn voter, Pawn candidate)
        {
            if (!On || voter == null || candidate == null) return 0f;
            var comp = Comp;
            if (comp == null) return 0f;
            float s = 0f;
            try
            {
                if (voter != candidate && KeptTracker.On && comp.offices.OfficeOf(candidate) != null && TookBackFrom(comp, voter)) s -= TakenBackVote;
                if (S.epithets && comp.epithets != null)
                {
                    int good = 0, bad = 0;
                    foreach (var e in comp.epithets.Of(candidate))
                        if (e.dubious) bad++;
                        else good++;
                    s += Mathf.Min(good, 2) - Mathf.Min(bad, 2);
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("interplay: vote", e);
            }
            return s;
        }

        /// <summary>How much a promise taken back counts against a sitting officer (a vote is worth about 3 to 15).</summary>
        public const float TakenBackVote = 3f;

        private static bool TookBackFrom(CouncilGameComponent comp, Pawn voter)
        {
            var watches = comp.kept?.watches;
            if (watches == null) return false;
            int now = Now;
            foreach (var w in watches)
                if (w != null && w.pawn == voter && w.phase == KeptPhase.TakenBack && w.takenBackTick >= 0 && now - w.takenBackTick < GenDate.TicksPerYear)
                    return true;
            return false;
        }

        // ---------------- Traditions and the colony's ways ----------------

        /// <summary>
        /// A council tradition (a dilemma's answer the colony keeps giving) was kept, founded, broken or let lapse
        /// (DilemmaTracker.Tradition): the colony is that much more traditional, or restless. <paramref name="step"/>:
        /// 2 founded, 1 kept, -1 broken, -2 lapsed.
        /// </summary>
        public static void Notify_CouncilTradition(string label, int step)
        {
            if (!On || !S.colonyCharacter || step == 0) return;
            Comp?.character?.Shift(new WayShift { tradition = step }, "CC_Interplay_Ways_Tradition".Translate(label ?? "?").Resolve());
        }

        /// <summary>
        /// A calendar day became a tradition, or a tradition was kept again (CalendarKeeping.Tradition, which already makes
        /// the colony more traditional): how it is kept says something too. A feast every year makes the colony a little
        /// more generous, a day of rest a little more easygoing.
        /// </summary>
        public static void Notify_DayTradition(CalendarDay d, bool founded)
        {
            if (!On || d == null || !S.colonyCharacter) return;
            float size = founded ? 1f : 0.5f;
            WayShift shift = null;
            if (d.way == DayWay.Feast) shift = new WayShift { generosity = size };
            else if (d.way == DayWay.Rest) shift = new WayShift { toil = -size };
            if (shift != null) Comp?.character?.Shift(shift, "CC_Cal_Way_Why".Translate(d.label ?? "?").Resolve());
        }

        /// <summary>
        /// How much a colonist cares about one of the colony's ways when an answer goes with or against it (SetbackCouncil's
        /// "not my choice"): their nature, and for someone who has lived here a year or more, a little of the colony's own
        /// name. An old hand of a merciful colony minds a harsh answer even if mercy isn't in their nature, and minds a
        /// merciful one less even if it is.
        /// </summary>
        public static float WayAffinity(Pawn p, Way w, float nature)
        {
            if (!On || p == null || !S.colonyCharacter) return nature;
            var character = Comp?.character;
            if (character == null) return nature;
            int tier = character.Tier(w);
            if (tier == 0) return nature;
            float years = (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerYear;
            if (years < 1f) return nature;
            return Mathf.Clamp(nature + OldHandsLean * tier, -1.5f, 1.5f);
        }

        /// <summary>How much of the colony's name an old hand takes on, per tier (known 1, renowned 2).</summary>
        public const float OldHandsLean = 0.25f;

        // ---------------- A secret at the council table ----------------

        /// <summary>Meetings that already had their pointed remark (by when they were called). Not saved.</summary>
        private static readonly HashSet<int> meetingsHinted = new HashSet<int>();

        private static readonly Color MeetingColor = new Color(0.7f, 0.85f, 1f);

        /// <summary>
        /// While the meeting argues (CouncilMeeting.Talk), now and then: someone who was told a secret, and has since
        /// come to dislike the one who told them, says something pointed with them sitting there. It goes in the
        /// meeting's remarks; the one with the secret holds it against them; and a rival may say too much. At most once
        /// a meeting. True when it took this turn of the talk.
        /// </summary>
        public static bool MeetingSecret(CouncilMeeting m, List<Pawn> here)
        {
            // together: only those at the meeting's gathering (CouncilMeeting.Talk passes who is there).
            if (!On || m == null || here == null || here.Count < 3 || !S.secrets) return false;
            if (meetingsHinted.Contains(m.calledTick) || !Rand.Chance(0.04f)) return false;
            var comp = Comp;
            if (comp?.secrets?.secrets == null) return false;
            try
            {
                foreach (var s in comp.secrets.secrets.InRandomOrder())
                {
                    if (s?.def == null || s.revealed || s.def.good || s.owner == null || !here.Contains(s.owner)) continue;
                    var k = s.knowers.Where(x => x != null && x != s.owner && here.Contains(x) && x.relations != null && x.relations.OpinionOf(s.owner) < 0)
                        .RandomElementWithFallback();
                    if (k == null) continue;
                    string line = Line("Council_Pool_Interplay_MeetingSecret", k, null, s.owner.Named("OTHER"));
                    if (line.NullOrEmpty()) return false;
                    meetingsHinted.Add(m.calledTick);
                    if (meetingsHinted.Count > 50) meetingsHinted.Clear();
                    CouncilGameComponent.Speak(k, line, MeetingColor);
                    if (m.remarks.Count < 5) m.remarks.Add("CC_Meeting_Remark".Translate(k.LabelShort, line).Resolve());
                    bool rivals = S.socialWeb && comp.social.Strength(k, s.owner, TieKind.Rivals) >= 25f;
                    if (S.socialWeb) comp.social.Bump(s.owner, k, TieKind.Rivals, 6f, "CC_TieNote_Interplay_MeetingHint".Translate());
                    if (rivals && Rand.Chance(0.3f))
                    {
                        comp.secrets.Reveal(comp, s, k);
                        return true;
                    }
                    News.Post("CC_Interplay_MeetingHint".Translate(k.Named("KNOWER"), s.owner.Named("OWNER")).Resolve(), new LookTargets(k, s.owner), NewsKind.People,
                        MessageTypeDefOf.SilentInput, Importance.Flavor);
                    return true;
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("interplay: meeting secret", e);
            }
            return false;
        }

        // ---------------- Quirks in who someone is ----------------

        /// <summary>
        /// A colonist's quirks as tags (PeopleTags): "quirk:Hums", "quirk:SleepTalker"... so social events, dilemma roles,
        /// flavour lines and dreams can ask for them like a trait.
        /// </summary>
        public static void AddQuirkTags(Pawn p, HashSet<string> tags)
        {
            if (!On || p == null || tags == null || !S.quirks) return;
            var list = Comp?.quirks?.quirks;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var q = list[i];
                if (q?.def == null || q.pawn != p) continue;
                string name = q.def.defName;
                tags.Add("quirk:" + (name.StartsWith("Council_Quirk_") ? name.Substring("Council_Quirk_".Length) : name));
            }
        }
    }
}
