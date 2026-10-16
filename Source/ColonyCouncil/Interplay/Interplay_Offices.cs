using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Offices in action: the keeper of days, the peacemaker, the physician, the reeve, the herdmaster, the marshal (and
    // the steward, at gatherings: Interplay_Gatherings.cs) doing their jobs in the colony's life.
    public static partial class Interplay
    {
        public const string Keeper = "Council_Office_Keeper";
        public const string Peacemaker = "Council_Office_Peacemaker";
        public const string Physician = "Council_Office_Physician";
        public const string Reeve = "Council_Office_Reeve";
        public const string Herdmaster = "Council_Office_Herdmaster";
        public const string Marshal = "Council_Office_Marshal";
        public const string Steward = "Council_Office_Steward";

        // ---------------- The keeper of days ----------------

        /// <summary>
        /// Who organises a calendar day's feast or games (CalendarKeeping): the keeper of days, when the day isn't about
        /// one person in particular (a death, a battle, a wedding, a birthday or an arrival keep their own) and the
        /// keeper is among those it's for. They write about it once. Null: the calendar's own choice.
        /// </summary>
        public static Pawn CalendarOrganizer(CalendarDay d, List<Pawn> who)
        {
            if (!On || d == null || who == null) return null;
            // A remembrance is the chaplain's to lead, when there is one (Interplay_Offices2.cs).
            if (d.way == DayWay.Remembrance) return RiteLeader(d, who);
            switch (d.kind)
            {
                case CalendarKind.Death:
                case CalendarKind.Battle:
                case CalendarKind.Wedding:
                case CalendarKind.Birthday:
                case CalendarKind.Arrival:
                    return null;
            }
            var keeper = Officer(Keeper);
            if (keeper == null || !who.Contains(keeper) || !Able(keeper) || Grieving(keeper)) return null;
            if (Ready("keeperday|" + d.id, 0.5f) && Rand.Chance(0.6f))
                Journal(keeper, "Council_Pool_Interplay_KeeperOrganised", null, (d.label ?? "").Named("NAME"));
            return keeper;
        }

        // ---------------- The peacemaker ----------------

        /// <summary>
        /// A spat between two colonists (a friction moment or social event, from MomentTones): with the peacemaker right
        /// there (up and about, within talking range of one of them: Together.Can), and now and then, they step in, and
        /// the spat is over in half the time (warm moments between the two come back sooner). The peacemaker steps in at
        /// most every few hours, and a pair once a day.
        /// </summary>
        public static void Notify_Spat(Pawn a, Pawn b, PairTone tone)
        {
            if (!On || tone == null || a == null || b == null || !S.frictions) return;
            try
            {
                var pm = Officer(Peacemaker);
                if (pm == null || pm == a || pm == b || !Able(pm) || pm.Map != a.MapHeld) return;
                if (!Together.Can(pm, a) && !Together.Can(pm, b)) return;
                int now = Now;
                int left = tone.soreUntil - now;
                if (left < GenDate.TicksPerHour * 2 || !Rand.Chance(0.5f)) return;
                if (!Ready("peacemaker|" + pm.thingIDNumber, 0.15f)) return;
                if (!Ready("spat|" + Mathf.Min(a.thingIDNumber, b.thingIDNumber) + "|" + Mathf.Max(a.thingIDNumber, b.thingIDNumber), 1f)) return;
                tone.soreUntil = now + left / 2;
                var to = Together.Can(pm, a) ? a : b;
                string said = Line("Council_Pool_Interplay_PeacemakerSays", pm, null, a.Named("A"), b.Named("B"));
                bool spoke = !said.NullOrEmpty() && Speech.Say(pm, said, to, SpeechColor);
                string text = Line("Council_Pool_Interplay_PeacemakerSteps", pm, null, a.Named("A"), b.Named("B"));
                if (!text.NullOrEmpty())
                {
                    News.Post(text, new LookTargets(pm, a, b), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                    // In the log either way; over their heads only if nothing was said for it already.
                    SocialLog.Add(pm, to, text, !spoke);
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("interplay: peacemaker", e);
            }
        }

        // ---------------- The physician ----------------

        /// <summary>
        /// The doctor's standing for the patients they tend (HealthTracker.Notify_Tended): the physician's office counts
        /// for two patients pulled through, so a physician who has saved one is already a trusted doctor.
        /// </summary>
        public static int DoctorLevel(Pawn doctor, HealerRecord rec)
        {
            int level = rec?.Level ?? 0;
            if (!On || rec == null || doctor == null || Officer(Physician) != doctor) return level;
            return Mathf.Max(level, HealthMath.DoctorLevel(rec.Saved + 2, rec.lost));
        }

        /// <summary>
        /// Once a day the physician does the rounds of everyone convalescing on their map: a visit to each, when the
        /// physician is beside them (Together), or walking over in their free time. A word, a look at the healing, a
        /// change of dressing: that patient's recovery is a little shorter (about two thirds of a day), and now and then
        /// they write about it. A patient not seen within the day is simply looked in on tomorrow.
        /// </summary>
        private static void PhysicianRounds(CouncilGameComponent comp)
        {
            if (!HealthTracker.ConvalescenceOn) return;
            var doc = Officer(Physician);
            if (doc == null || doc.Dead || doc.MapHeld == null || !doc.MapHeld.IsPlayerHome) return;
            foreach (var p in doc.MapHeld.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p == doc || !Ours(p) || HealthTracker.ConvalescenceOf(p) == null) continue;
                var patient = p;
                Together.When("rounds|" + doc.thingIDNumber + "|" + patient.thingIDNumber, doc, patient, () => LookIn(doc, patient), 20f, 1f);
            }
        }

        /// <summary>The physician beside a convalescing patient: the visit itself.</summary>
        private static void LookIn(Pawn doc, Pawn p)
        {
            // together: called once the physician is beside the patient (PhysicianRounds, through Together.When).
            if (!On || Officer(Physician) != doc) return;
            var h = HealthTracker.ConvalescenceOf(p);
            if (h == null) return;
            h.Severity = Mathf.Max(0.001f, h.Severity - PhysicianRoundsEase);
            if (Rand.Chance(0.3f)) Journal(p, "Council_Pool_Interplay_PhysicianRounds", null, doc.Named("OTHER"));
            string text = "CC_Together_PhysicianLooksIn".Translate(doc.Named("PAWN"), p.Named("OTHER")).Resolve();
            SocialLog.Add(doc, p, text);
            News.Post(text, new LookTargets(doc, p), NewsKind.Health, MessageTypeDefOf.SilentInput, Importance.Flavor);
        }

        /// <summary>How much of a convalescence the physician's daily rounds take off (recovery is 0.06 a day).</summary>
        public const float PhysicianRoundsEase = 0.04f;

        // ---------------- The reeve ----------------

        /// <summary>
        /// Who deals with a neighbour asking for goods (a delivery mission's A): the reeve, who knows the stores and the
        /// fields, when there is one at home. Null: the mission's own choice.
        /// </summary>
        public static Pawn MissionHead(Map map)
        {
            if (!On || map == null) return null;
            var reeve = Officer(Reeve);
            return reeve != null && reeve.Spawned && reeve.Map == map && !reeve.Downed ? reeve : null;
        }

        /// <summary>
        /// A delivery mission filled in full: when the reeve saw to it, the neighbours remember who did (a little more
        /// goodwill) and the reeve writes about it.
        /// </summary>
        public static void Notify_Delivered(Mission m)
        {
            if (!On || m?.a == null || m.faction == null) return;
            try
            {
                if (Officer(Reeve) != m.a) return;
                Missions.Goodwill(m, ReeveGoodwill);
                Journal(m.a, "Council_Pool_Interplay_ReeveDelivered", null, (m.place ?? m.faction.Name ?? "").Named("PLACE"), m.faction.Named("FACTION"));
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("interplay: reeve", e);
            }
        }

        /// <summary>The extra goodwill when the reeve sees a delivery through.</summary>
        public const int ReeveGoodwill = 3;

        // ---------------- The herdmaster ----------------

        /// <summary>Pets the herdmaster minded while their person was away: the person's ID, the pet and the herdmaster.</summary>
        private static readonly Dictionary<int, (Pawn pet, Pawn herdmaster)> minded = new Dictionary<int, (Pawn, Pawn)>();

        /// <summary>
        /// A named or bonded animal has died (PetTracker): the herdmaster, if it wasn't their own, comes to sit with the
        /// one who loved it: when they're together, or walking over in their free time, within the day. A friendship
        /// grows a little, and the grieving heart is a little calmer.
        /// </summary>
        public static void Notify_PetLost(CouncilGameComponent comp, Pawn pet, Pawn person)
        {
            if (!On || comp == null || pet == null || person == null || !S.petMoments) return;
            try
            {
                var hm = Officer(Herdmaster);
                if (hm == null || hm == person || hm.Dead || person.Dead || hm.MapHeld == null || hm.MapHeld != person.MapHeld) return;
                string petName = pet.LabelShort;
                Together.When("petlost|" + person.thingIDNumber, hm, person, () =>
                {
                    if (!On || Officer(Herdmaster) != hm) return;
                    if (S.socialWeb) comp.social.Bump(person, hm, TieKind.Friends, 4f, "CC_TieNote_Interplay_PetLost".Translate(petName), true);
                    if (Heart.Eligible(person)) Ease(person, HerdmasterComfort, StrainRelief.Friends);
                    string text = Line("Council_Pool_Interplay_HerdmasterComforts", hm, null, person.Named("OTHER"), pet.Named("PET"));
                    if (text.NullOrEmpty()) return;
                    News.Post(text, new LookTargets(hm, person), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                    SocialLog.Add(hm, person, text);
                }, 24f, 0f);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("interplay: herdmaster farewell", e);
            }
        }

        /// <summary>Strain eased when the herdmaster sits with someone whose animal died.</summary>
        public const float HerdmasterComfort = 4f;

        /// <summary>
        /// The herdmaster's day: a named animal whose person has been away two days or more is in the herdmaster's care;
        /// when its person comes home, they hear who looked after it, and like them a little more for it.
        /// </summary>
        private static void HerdmasterDay(CouncilGameComponent comp)
        {
            if (!S.petMoments) return;
            var hm = Officer(Herdmaster);
            // Home again: thanks.
            foreach (var id in minded.Keys.ToList())
            {
                var (pet, keeper) = minded[id];
                var person = pet != null && !pet.Dead ? PetTracker.PersonOf(pet) : null;
                if (person == null || person.thingIDNumber != id || keeper == null || keeper.Dead || !keeper.IsColonist)
                {
                    minded.Remove(id);
                    continue;
                }
                if (!(person.MapHeld?.IsPlayerHome ?? false)) continue;
                minded.Remove(id);
                // They hear who looked after it when they next meet (within two days).
                var (who, keptBy) = (person, keeper);
                Together.When("minded|" + who.thingIDNumber, who, keptBy, () =>
                {
                    if (S.socialWeb) comp.social.Bump(who, keptBy, TieKind.Friends, 3f, "CC_TieNote_Interplay_Minded".Translate(pet.LabelShort), true);
                    string text = Line("Council_Pool_Interplay_HerdmasterMinded", keptBy, null, who.Named("OTHER"), pet.Named("PET"));
                    if (text.NullOrEmpty()) return;
                    News.Post(text, new LookTargets(who, pet), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                    SocialLog.Add(keptBy, who, text);
                }, 48f);
            }
            if (hm == null || !hm.Spawned || hm.Map == null || !hm.Map.IsPlayerHome) return;
            foreach (var a in hm.Map.mapPawns.SpawnedColonyAnimals.ToList())
            {
                if (a?.Name == null || a.Dead) continue;
                var person = PetTracker.PersonOf(a);
                if (person == null || person == hm || minded.ContainsKey(person.thingIDNumber)) continue;
                if (person.MapHeld?.IsPlayerHome ?? false) continue;
                if (comp.AwayTicks(person) < Day * 2) continue;
                minded[person.thingIDNumber] = (a, hm);
            }
        }

        // ---------------- The marshal ----------------

        private static int battleStart = -1;

        /// <summary>
        /// After a fight at home, a marshal who fought in it walks the line and has a word with each of those who stood
        /// with them, face to face (Together: when they're beside them, or walking over in their free time, within half
        /// a day): their strain eases a little. Hourly, from the social web's own record of a fight going on.
        /// </summary>
        private static void MarshalWatch(CouncilGameComponent comp)
        {
            var social = comp.social;
            if (social == null) return;
            if (social.battleActive)
            {
                // Seen on the hour: the fight may have begun up to an hour before.
                if (battleStart < 0) battleStart = Now - GenDate.TicksPerHour;
                return;
            }
            if (battleStart < 0) return;
            int start = battleStart;
            battleStart = -1;
            if (!Heart.On) return;
            var marshal = Officer(Marshal);
            if (!Able(marshal) || marshal.Map == null || !marshal.Map.IsPlayerHome) return;
            if ((marshal.mindState?.lastAttackTargetTick ?? -1) < start) return;
            var fought = marshal.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != marshal && Ours(p) && !p.Downed && (p.mindState?.lastAttackTargetTick ?? -1) >= start && Heart.Eligible(p)).ToList();
            if (fought.Count == 0) return;
            bool told = false;
            foreach (var p in fought)
            {
                var them = p;
                Together.When("marshal|" + them.thingIDNumber, marshal, them, () =>
                {
                    if (!Heart.On || !Heart.Eligible(them)) return;
                    Ease(them, MarshalRelief, StrainRelief.Friends);
                    SocialLog.Add(marshal, them, "CC_Together_MarshalWord".Translate(marshal.Named("PAWN"), them.Named("OTHER")).Resolve());
                    // The colony hears of it once, with the first word.
                    if (told) return;
                    told = true;
                    string text = Line("Council_Pool_Interplay_MarshalWalks", marshal, null);
                    if (!text.NullOrEmpty()) News.Post(text, new LookTargets(marshal), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                }, 12f, 1f);
            }
        }

        /// <summary>Strain eased for those who fought beside the marshal.</summary>
        public const float MarshalRelief = 4f;
    }
}
