using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The newer seats in action: the chaplain tends the faith and leads the colony's remembrances, the warden of guests
    // keeps the peace with the camp and sees visitors welcomed, and the council of elders sit with the young. Like the
    // other offices (Interplay_Offices.cs), each rides on the council's day, answers to "Systems talk to each other" and
    // prefers what isn't mood: certainty, strain eased, a camp's tension, goodwill, a skill passed on, a friendship.
    public static partial class Interplay
    {
        public const string Chaplain = "Council_Office_Chaplain";
        public const string Warden = "Council_Office_Warden";
        public const string Elder = "Council_Office_Elder";

        /// <summary>The faith's certainty (from decrees and customs) goes half again as far with a chaplain at home.</summary>
        public static float FaithFactor
        {
            get
            {
                if (!On) return 1f;
                var c = Officer(Chaplain);
                return c != null && !c.Dead && (c.MapHeld?.IsPlayerHome ?? false) ? ChaplainFaithFactor : 1f;
            }
        }

        public const float ChaplainFaithFactor = 1.5f;

        /// <summary>
        /// Who leads a remembrance on the calendar (CalendarKeeping): the chaplain, when they are among those keeping it
        /// and not deep in grief themselves. Null: the calendar's own choice.
        /// </summary>
        public static Pawn RiteLeader(CalendarDay d, List<Pawn> who)
        {
            if (!On || d == null || who == null || d.way != DayWay.Remembrance) return null;
            var c = Officer(Chaplain);
            if (c == null || !who.Contains(c) || !Able(c) || Grieving(c)) return null;
            if (Ready("chaplainrite|" + d.id, 0.5f) && Rand.Chance(0.6f))
                Journal(c, "Council_Pool_Interplay_ChaplainRite", null, (d.label ?? "").Named("NAME"));
            return c;
        }

        // ---------------- The chaplain ----------------

        /// <summary>Strain eased when the chaplain sits with someone grieving.</summary>
        public const float ChaplainComfort = 4f;

        /// <summary>Certainty the chaplain's word gives the believer least sure of the faith.</summary>
        public const float ChaplainCertainty = 0.06f;

        /// <summary>
        /// The chaplain's day: a word with someone grieving (their strain eases), or else with the believer least sure of
        /// the colony's faith (their certainty rises). One of the two, now and then, never the same person twice in a few
        /// days, and face to face (Together: when they're beside each other, or the chaplain walking over in their free time).
        /// </summary>
        private static void ChaplainDay(CouncilGameComponent comp)
        {
            var c = Officer(Chaplain);
            if (!Able(c) || c.Map == null || !c.Map.IsPlayerHome || DecreeWork.ColonyFaith == null || !Rand.Chance(0.6f)) return;
            var people = c.Map.mapPawns.FreeColonistsSpawned.Where(p => p != c && Ours(p) && !p.DevelopmentalStage.Baby()).ToList();
            var grieving = people.Where(p => Grieving(p) && Heart.Eligible(p)).InRandomOrder().FirstOrDefault();
            if (grieving != null && Ready("chaplain|grief|" + grieving.thingIDNumber, 2f))
            {
                var them = grieving;
                Together.When("chaplain|grief|" + them.thingIDNumber, c, them, () => SitWithGrieving(c, them), 24f, 0f);
                return;
            }
            var doubter = people.Where(p => DecreeWork.Believer(p) && p.ideo != null && p.ideo.Certainty < 0.85f).OrderBy(p => p.ideo.Certainty).FirstOrDefault();
            if (doubter == null || !Ready("chaplain|doubt|" + doubter.thingIDNumber, 3f)) return;
            var who = doubter;
            Together.When("chaplain|doubt|" + who.thingIDNumber, c, who, () => WordOfFaith(c, who), 24f, 4f);
        }

        /// <summary>The chaplain beside someone grieving.</summary>
        private static void SitWithGrieving(Pawn c, Pawn grieving)
        {
            // together: called once the chaplain is beside them (ChaplainDay, through Together.When).
            if (!On || Officer(Chaplain) != c || !Heart.Eligible(grieving)) return;
            Ease(grieving, ChaplainComfort, StrainRelief.Friends);
            string text = Line("Council_Pool_Interplay_ChaplainComforts", c, null, grieving.Named("OTHER"));
            if (text.NullOrEmpty()) return;
            News.Post(text, new LookTargets(c, grieving), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
            SocialLog.Add(c, grieving, text);
        }

        /// <summary>The chaplain beside a believer whose faith is wavering.</summary>
        private static void WordOfFaith(Pawn c, Pawn doubter)
        {
            // together: called once the chaplain is beside them (ChaplainDay, through Together.When).
            if (!On || Officer(Chaplain) != c || doubter.ideo == null || !DecreeWork.Believer(doubter)) return;
            doubter.ideo.OffsetCertainty(ChaplainCertainty);
            string said = Line("Council_Pool_Interplay_ChaplainTends", c, null, doubter.Named("OTHER"));
            if (!said.NullOrEmpty())
            {
                News.Post(said, new LookTargets(c, doubter), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                SocialLog.Add(c, doubter, said);
            }
            if (Rand.Chance(0.35f)) Journal(doubter, "Council_Pool_Interplay_ChaplainTendsJournal", null, c.Named("OTHER"));
        }

        // ---------------- The warden of guests ----------------

        /// <summary>Tension at the camp eased each day the warden spends an hour at their fire.</summary>
        public const float WardenTension = 4f;

        /// <summary>Goodwill when the warden sees a people's visitors welcomed (once every few days for each people).</summary>
        public const int WardenGoodwill = 2;

        /// <summary>
        /// The warden's day: an hour at the fire of the camp on the colony's land (tension eases, and the campers know the
        /// warden a little better), and a proper welcome for any visitors on the map (their people remember it). Both
        /// happen where the people are: the warden walks over to a camper or a guest in their free time, or meets them by chance.
        /// </summary>
        private static void WardenDay(CouncilGameComponent comp)
        {
            var w = Officer(Warden);
            if (!Able(w) || w.Map == null || !w.Map.IsPlayerHome) return;
            var camp = CampTracker.Enabled ? CampTracker.Instance?.camp : null;
            if (camp != null && camp.map == w.Map && camp.Settled && camp.CanTalk && Ready("warden|camp", 0.9f))
            {
                var camper = camp.Here.Where(m => Together.Able(m)).InRandomOrder().FirstOrDefault();
                if (camper != null)
                    Together.When("warden|camp", w, camper, () =>
                    {
                        // together: the warden is at the camp, beside one of them.
                        if (!On || Officer(Warden) != w || !camp.Settled || !camp.CanTalk) return;
                        camp.AddTension(-WardenTension);
                        camp.Warm(w, 1);
                        string text = Line("Council_Pool_Interplay_WardenCamp", w, null, (camp.Label ?? "").Named("PLACE"));
                        if (!text.NullOrEmpty()) News.Post(text, new LookTargets(w, camper), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                    }, 20f, 2f);
            }
            var visitors = Strangers.On(w.Map, StrangerKind.Visitor).Where(v => v.Faction != null && !v.Faction.IsPlayer && v.Faction.HasGoodwill && !v.Faction.HostileTo(Faction.OfPlayer) && Together.Able(v)).ToList();
            foreach (var f in visitors.Select(v => v.Faction).Distinct().ToList())
            {
                if (!Ready("warden|guests|" + f.loadID, 4f)) continue;
                var guest = visitors.First(v => v.Faction == f);
                var people = f;
                Together.When("warden|guests|" + people.loadID, w, guest, () =>
                {
                    // together: the warden is beside one of the visitors, welcoming them.
                    if (!On || Officer(Warden) != w || people.HostileTo(Faction.OfPlayer)) return;
                    people.TryAffectGoodwillWith(Faction.OfPlayer, WardenGoodwill, false, false);
                    string text = Line("Council_Pool_Interplay_WardenGuests", w, null, guest.Named("OTHER"), people.Name.Named("FACTION"));
                    if (!text.NullOrEmpty()) News.Post(text, new LookTargets(w, guest), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                }, 12f, 0f);
            }
        }

        // ---------------- The council of elders ----------------

        /// <summary>Practice the young one gets in what the elder knows best, an afternoon's worth.</summary>
        public const float ElderLessonXp = 400f;

        /// <summary>
        /// The elders' day: each elder at home, every couple of days, sits with someone ten years younger or more and
        /// passes on what they know best (practice in that skill), and the two grow a little closer. Face to face: when
        /// the two are together, or the elder walking over in their free time; a sitting that doesn't come within the day
        /// is simply let go.
        /// </summary>
        private static void EldersDay(CouncilGameComponent comp)
        {
            if (!S.offices) return;
            var elders = comp.offices.HoldersOf(Elder);
            if (elders.Count == 0) return;
            foreach (var e in elders)
            {
                if (!Able(e) || e.Map == null || !e.Map.IsPlayerHome || e.skills == null || !Ready("elder|" + e.thingIDNumber, 2f)) continue;
                float age = Flavor.HumanAge(e);
                var young = e.Map.mapPawns.FreeColonistsSpawned
                    .Where(p => p != e && Ours(p) && !elders.Contains(p) && !p.DevelopmentalStage.Baby() && Able(p) && Flavor.HumanAge(p) < age - 10f)
                    .InRandomOrder().FirstOrDefault();
                if (young == null) continue;
                var (elder, them) = (e, young);
                Together.When("elder|" + elder.thingIDNumber + "|" + them.thingIDNumber, elder, them, () => ElderSits(comp, elder, them), 20f, 3f);
            }
        }

        /// <summary>An elder sitting with someone younger: the lesson, or the stories.</summary>
        private static void ElderSits(CouncilGameComponent comp, Pawn e, Pawn young)
        {
            // together: called once the elder is beside them (EldersDay, through Together.When).
            if (!On || !S.offices || comp?.offices == null || !comp.offices.HoldersOf(Elder).Contains(e) || e.skills == null) return;
            var skill = e.skills.skills.Where(s => !s.TotallyDisabled && s.Level >= 6).OrderByDescending(s => s.Level).Select(s => s.def)
                .FirstOrDefault(d => young.skills?.GetSkill(d) is SkillRecord r && !r.TotallyDisabled);
            if (skill != null) young.skills.Learn(skill, ElderLessonXp, true);
            if (S.socialWeb && comp.social != null) comp.social.Bump(young, e, TieKind.Friends, 2f, "CC_TieNote_Interplay_Elder".Translate(), true);
            string text = skill != null
                ? Line("Council_Pool_Interplay_ElderCounsel", e, null, young.Named("OTHER"), skill.label.Named("SKILL"))
                : Line("Council_Pool_Interplay_ElderStories", e, null, young.Named("OTHER"));
            if (!text.NullOrEmpty())
            {
                News.Post(text, new LookTargets(e, young), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                SocialLog.Add(e, young, text);
            }
            if (Rand.Chance(0.35f)) Journal(young, "Council_Pool_Interplay_ElderCounselJournal", null, e.Named("OTHER"));
        }
    }
}
