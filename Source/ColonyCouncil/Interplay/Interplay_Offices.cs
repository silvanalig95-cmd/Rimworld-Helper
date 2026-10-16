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
                Journal(keeper, "Council_Pool_Interplay_KeeperOrganised", null, (d.label ?? "").Named("DAY"));
            return keeper;
        }

        // ---------------- The peacemaker ----------------

        /// <summary>
        /// A spat between two colonists (a friction moment or social event, from MomentTones): with the peacemaker close
        /// by, and now and then, they step in, and the spat is over in half the time (warm moments between the two come
        /// back sooner). The peacemaker steps in at most every few hours, and a pair once a day.
        /// </summary>
        public static void Notify_Spat(Pawn a, Pawn b, PairTone tone)
        {
            if (!On || tone == null || a == null || b == null || !S.frictions) return;
            try
            {
                var pm = Officer(Peacemaker);
                if (pm == null || pm == a || pm == b || !Able(pm) || pm.Map != a.MapHeld) return;
                if (!Near(pm, a) && !Near(pm, b)) return;
                int now = Now;
                int left = tone.soreUntil - now;
                if (left < GenDate.TicksPerHour * 2 || !Rand.Chance(0.5f)) return;
                if (!Ready("peacemaker|" + pm.thingIDNumber, 0.15f)) return;
                if (!Ready("spat|" + Mathf.Min(a.thingIDNumber, b.thingIDNumber) + "|" + Mathf.Max(a.thingIDNumber, b.thingIDNumber), 1f)) return;
                tone.soreUntil = now + left / 2;
                string said = Line("Council_Pool_Interplay_PeacemakerSays", pm, null, a.Named("A"), b.Named("B"));
                if (!said.NullOrEmpty()) CouncilGameComponent.Speak(pm, said, SpeechColor);
                string text = Line("Council_Pool_Interplay_PeacemakerSteps", pm, null, a.Named("A"), b.Named("B"));
                if (!text.NullOrEmpty())
                {
                    News.Post(text, new LookTargets(pm, a, b), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                    SocialLog.Add(pm, a, text);
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
        /// Once a day the physician looks in on everyone convalescing on their map: a word, a look at the healing, a
        /// change of dressing. Their recovery is a little shorter (about two thirds of a day), and now and then a
        /// patient writes about it.
        /// </summary>
        private static void PhysicianRounds(CouncilGameComponent comp)
        {
            if (!HealthTracker.ConvalescenceOn) return;
            var doc = Officer(Physician);
            if (!Able(doc) || doc.Map == null || !doc.Map.IsPlayerHome) return;
            var seen = new List<Pawn>();
            foreach (var p in doc.Map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p == doc || !Ours(p)) continue;
                var h = HealthTracker.ConvalescenceOf(p);
                if (h == null) continue;
                h.Severity = Mathf.Max(0.001f, h.Severity - PhysicianRoundsEase);
                seen.Add(p);
                if (Rand.Chance(0.3f)) Journal(p, "Council_Pool_Interplay_PhysicianRounds", null, doc.Named("OTHER"));
            }
            if (seen.Count == 0) return;
            string names = TextUtil.JoinAnd(seen.Select(p => p.LabelShort).ToList());
            News.Post("CC_Interplay_PhysicianRounds".Translate(doc.Named("PAWN"), names.Named("NAMES")).Resolve(), new LookTargets(doc), NewsKind.Health,
                MessageTypeDefOf.SilentInput, Importance.Flavor);
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
        /// one who loved it. A friendship grows a little, and the grieving heart is a little calmer.
        /// </summary>
        public static void Notify_PetLost(CouncilGameComponent comp, Pawn pet, Pawn person)
        {
            if (!On || comp == null || pet == null || person == null || !S.petMoments) return;
            try
            {
                var hm = Officer(Herdmaster);
                if (hm == null || hm == person || !Able(hm) || !person.Spawned || hm.Map != person.Map) return;
                if (S.socialWeb) comp.social.Bump(person, hm, TieKind.Friends, 4f, "CC_TieNote_Interplay_PetLost".Translate(pet.LabelShort), true);
                if (Heart.Eligible(person)) HeartLogic.Change(person, -HerdmasterComfort, StrainRelief.Friends);
                string text = Line("Council_Pool_Interplay_HerdmasterComforts", hm, null, person.Named("OTHER"), pet.Named("PET"));
                if (!text.NullOrEmpty()) News.Post(text, new LookTargets(hm, person), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
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
                if (S.socialWeb) comp.social.Bump(person, keeper, TieKind.Friends, 3f, "CC_TieNote_Interplay_Minded".Translate(pet.LabelShort), true);
                string text = Line("Council_Pool_Interplay_HerdmasterMinded", keeper, null, person.Named("OTHER"), pet.Named("PET"));
                if (!text.NullOrEmpty()) News.Post(text, new LookTargets(person, pet), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
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
        /// with them: their strain eases a little. Hourly, from the social web's own record of a fight going on.
        /// </summary>
        private static void MarshalWatch(CouncilGameComponent comp)
        {
            var social = comp.social;
            if (social == null) return;
            if (social.battleActive)
            {
                if (battleStart < 0) battleStart = Now;
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
            foreach (var p in fought) HeartLogic.Change(p, -MarshalRelief, StrainRelief.Friends);
            string text = Line("Council_Pool_Interplay_MarshalWalks", marshal, null);
            if (!text.NullOrEmpty()) News.Post(text, new LookTargets(marshal), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
        }

        /// <summary>Strain eased for those who fought beside the marshal.</summary>
        public const float MarshalRelief = 4f;
    }
}
