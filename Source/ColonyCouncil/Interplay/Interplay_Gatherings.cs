using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The heart and health at the colony's gatherings, and the colony's animals with those who grieve.
    public static partial class Interplay
    {
        // ---------------- Grief and gatherings ----------------

        /// <summary>
        /// A calendar day's feast or games (CalendarKeeping.PlayDay): those deep in grief sit it out, unless the day is
        /// about them. They aren't asked, so they can't miss it; a few write about it. A remembrance at a grave keeps
        /// them all. Returns the list without them.
        /// </summary>
        public static List<Pawn> WithoutMourners(CalendarDay d, PlayKind kind, bool atGrave, List<Pawn> who)
        {
            if (!On || d == null || who == null || atGrave || d.way == DayWay.Remembrance) return who;
            if (kind != PlayKind.Feast && kind != PlayKind.Gathering && kind != PlayKind.Bonfire) return who;
            try
            {
                var kept = new List<Pawn>(who.Count);
                foreach (var p in who)
                {
                    bool theirDay = p != null && (p.ThingID == d.pawnId || p.ThingID == d.otherId);
                    if (p != null && !theirDay && Grieving(p))
                    {
                        if (Rand.Chance(0.5f)) Journal(p, "Council_Pool_Interplay_SatOut", null, (d.label ?? "").Named("NAME"));
                        continue;
                    }
                    kept.Add(p);
                }
                return kept;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("interplay: mourners", e);
                return who;
            }
        }

        /// <summary>
        /// Someone missed a gathering an answer called (SetbackCouncil.Notify_GatheringOver): grief excuses them, and
        /// they don't mind having missed it.
        /// </summary>
        public static bool Excused(Pawn p) => On && p != null && Grieving(p);

        // ---------------- When a gathering is over ----------------

        /// <summary>
        /// A gathering an answer or a calendar day called is over (LordJob_PlayOut.Finish): the convalescents who came
        /// recover a little faster for the company; at a feast, when the stores hold fruit or vegetables, the table had
        /// some, and those who ate count as having had fresh food (against scurvy); and with the steward there, everyone
        /// else's strain eases a little more.
        /// </summary>
        public static void Notify_GatheringOver(PlayOut play, List<Pawn> attended, List<Pawn> fed, PlayKind kind, bool early, Map map)
        {
            GroupHabits.Notify_GatheringOver(play, attended, early);
            // An evening of faith is judged and does its work (Faith/FaithEvenings.cs), whatever this setting says.
            FaithEvenings.Notify_GatheringOver(play, attended, early);
            if (!On || attended.NullOrEmpty() || early) return;
            Safe("interplay: convalescents at a gathering", () => Convalescents(attended));
            if (kind == PlayKind.Feast && !fed.NullOrEmpty()) Safe("interplay: the feast's table", () => FeastFresh(fed, map));
            Safe("interplay: the steward", () => StewardThere(attended));
        }

        /// <summary>How much of a convalescence a gathering takes off for those who came.</summary>
        public const float GatheringEase = 0.03f;

        private static void Convalescents(List<Pawn> attended)
        {
            if (!HealthTracker.ConvalescenceOn) return;
            foreach (var p in attended)
            {
                if (!Ours(p)) continue;
                var h = HealthTracker.ConvalescenceOf(p);
                if (h == null) continue;
                h.Severity = Mathf.Max(0.001f, h.Severity - GatheringEase);
                if (Rand.Chance(0.3f)) Journal(p, "Council_Pool_Interplay_ConvalescentGathering", null);
            }
        }

        private static void FeastFresh(List<Pawn> fed, Map map)
        {
            var ailments = AilmentTracker.Instance;
            if (ailments == null || map == null || !AilmentTracker.On || !AilmentTracker.DietKnown) return;
            int fresh = 0;
            foreach (var kv in map.resourceCounter.AllCountedAmounts)
            {
                var d = kv.Key;
                if (kv.Value <= 0 || d?.ingestible == null || !d.ingestible.HumanEdible || d.IsCorpse) continue;
                if ((d.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) == 0) continue;
                fresh += kv.Value;
                if (fresh >= fed.Count) break;
            }
            if (fresh < fed.Count) return;
            foreach (var p in fed)
                if (p != null && !p.Dead)
                    ailments.MarkFresh(p);
        }

        /// <summary>Strain eased for the others at a gathering the steward came to.</summary>
        public const float StewardRelief = 2f;

        private static void StewardThere(List<Pawn> attended)
        {
            // together: only those who were at the gathering with the steward (the play-out's own count of who came).
            var steward = Officer(Steward);
            if (steward == null || !attended.Contains(steward) || !Heart.On) return;
            foreach (var p in attended)
                if (p != null && p != steward && Heart.Eligible(p))
                    Ease(p, StewardRelief, StrainRelief.Gathering);
        }

        // ---------------- Pets and grief ----------------

        /// <summary>Strain eased when a grieving colonist spends time with their own animal (once a day).</summary>
        public const float PetComfort = 3f;

        /// <summary>
        /// Hourly: someone grieving with their own animal close by (bonded to them, or theirs to command) has a calmer
        /// hour of it, once a day.
        /// </summary>
        private static void PetsComfort(CouncilGameComponent comp)
        {
            // together: the animal close by its person, both up (a few steps apart at most).
            if (!S.petMoments || !Heart.On) return;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var a in map.mapPawns.SpawnedColonyAnimals.ToList())
                {
                    if (a == null || a.Dead || a.Downed || a.Name == null) continue;
                    var person = PetTracker.PersonOf(a);
                    if (person == null || !person.Spawned || person.Map != map || !person.Awake() || !Near(a, person, 6f)) continue;
                    if (!Heart.Eligible(person) || !Grieving(person) || !Ready("petcomfort|" + person.thingIDNumber, 1f)) continue;
                    Ease(person, PetComfort, StrainRelief.Friends);
                    if (!Rand.Chance(0.5f)) continue;
                    string text = Line("Council_Pool_Interplay_PetComfort", person, null, a.Named("PET"));
                    if (!text.NullOrEmpty()) News.Post(text, new LookTargets(person, a), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                }
            }
        }
    }
}
