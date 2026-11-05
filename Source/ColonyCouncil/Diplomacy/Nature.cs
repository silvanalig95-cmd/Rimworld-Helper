using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Stress against one's nature (docs/design/stage2-diplomacy.md, item 13; release 2d.2; the player's decision 19): the
    /// service each release of stage 2 calls where a colonist does, or is made to do, one of its acts (NatureAct). It works
    /// out what the act brings them (NatureMath: by the sides of them it goes against) and, with the "Against their nature"
    /// setting and the heart on, puts it through the heart: strain under its own source (StrainSource.Nature), within that
    /// source's daily cap and the day's cap for everything, scaled as the pressures' bad side is (the "Life's ups and downs"
    /// setting and hard times); relief under its own kind (StrainRelief.Nature) when the act suits them. One act a day per
    /// colonist (the last kept on their heart, with its words for the heart's page). Told in the colony's news; the second
    /// time within a year the same act goes against the same side, or an oath sworn by a proud one, it may leave a mark
    /// (Defs/PersonaDefs/Rimfolk_PersonaMarks_Nature.xml). No mood of its own: the heart does the rest as it does for
    /// everything (its bands, its episodes, its reliefs).
    /// </summary>
    public static class Nature
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Switched on (round d), the setting on, and the heart on.</summary>
        public static bool On => NatureMath.SwitchedOn && (S?.diplomacyNature ?? true) && Heart.On;

        /// <summary>A felt act is told in the news, and shows on the heart's page, from this much.</summary>
        public const float Told = 1f;

        /// <summary>The person's eight sides (PersonaAxis order, each -100..100), or null when Rimfolk keeps none for them.</summary>
        public static float[] Sides(Pawn p)
        {
            if (p == null) return null;
            try
            {
                return Persona.Values(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("nature: a person's sides", e);
                return null;
            }
        }

        /// <summary>Someone an act can weigh on: one of the colony's own with a heart the heart layer looks after.</summary>
        public static bool Feels(Pawn p) => p != null && KinMemory.Ours(p) && Heart.Eligible(p);

        /// <summary>
        /// What an act would bring them before it is done, unscaled (the council's questions name it): above 0 strain, below 0
        /// relief, 0 nothing (or the setting off).
        /// </summary>
        public static float Would(Pawn p, NatureAct act) => On && Feels(p) ? NatureMath.Felt(act, Sides(p)) : 0f;

        /// <summary>
        /// <paramref name="p"/> did (or was made to do) <paramref name="act"/>, concerning <paramref name="people"/> (their
        /// people, for the words) and <paramref name="other"/> (someone else in it, by name). Returns what was put through the
        /// heart: the strain added (above 0), the strain eased (below 0), or 0.
        /// </summary>
        public static float Felt(Pawn p, NatureAct act, Faction people = null, string other = null, string role = null) => Felt(p, new[] { act }, people, other, role);

        /// <summary>
        /// Two acts at once (a hostage given is also a season away): one act a day, so the one that weighs most counts
        /// (NatureMath.Pick). <paramref name="role"/> picks the act's own words for this role where it has them (the envoy's
        /// season, the hostage's).
        /// </summary>
        public static float Felt(Pawn p, NatureAct[] acts, Faction people, string other, string role)
        {
            if (!On || acts == null || acts.Length == 0 || !Feels(p)) return 0f;
            try
            {
                return Apply(p, acts, people, other, role);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("nature: an act felt", e);
                return 0f;
            }
        }

        private static float Apply(Pawn p, NatureAct[] acts, Faction people, string other, string role)
        {
            var h = Heart.Of(p) ?? HeartTracker.Instance?.For(p, true);
            if (h == null) return 0f;
            int now = Find.TickManager.TicksGame;
            if (!NatureMath.MayFeel(h.natureTick, now)) return 0f;
            var sides = Sides(p);
            float felt = NatureMath.Pick(acts, sides, out var act);
            if (felt == 0f) return 0f;
            bool eased = felt < 0f;
            float applied = eased
                ? -HeartLogic.Relieve(p, -felt, StrainRelief.Nature)
                : HeartLogic.Build(p, StrainSource.Nature, NatureMath.Scale(felt, S?.lifePressure ?? LifePressure.Normal, Pressures.Hard(p)));
            if (applied == 0f) return 0f;
            int lastAct = h.natureAct, lastTick = h.natureTick;
            string why = Why(p, act, sides, people, other, role, eased);
            h.natureTick = now;
            h.natureAct = (int)act;
            h.natureEased = eased;
            h.natureWhy = why;
            HeartView.Refresh(p, h);
            if (Mathf.Abs(applied) >= Told && !why.NullOrEmpty())
            {
                // apart: something done at the gate, in a letter or by the council; how it sits with them is told in the news.
                string text = DipWords.T(eased ? "CC_Nature_Told_Eased" : "CC_Nature_Told", p.Named("PAWN"), why.Named("WHY"));
                Tell.Minor(NewsKind.People, Importance.Notable, p.MapHeld, text, new LookTargets(p), eased ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
            }
            Mark(p, act, !eased, lastAct, lastTick, now, people?.Name ?? other);
            return applied;
        }

        /// <summary>
        /// "swore the colony's oath to the Lenlia, against her pride": what they did (the act's words for this role where it has
        /// them) and the side of them it went against or suited, by the game's own pronouns.
        /// </summary>
        public static string Why(Pawn p, NatureAct act, float[] sides, Faction people, string other, string role, bool eased)
        {
            if (p == null || !NatureMath.RuleOf(act, sides, out var rule)) return null;
            string actKey = NatureMath.ActKey(act, role);
            if (!actKey.CanTranslate()) actKey = NatureMath.ActKey(act);
            var pawn = p.Named("PAWN");
            string did = DipWords.P(actKey, pawn, DipWords.People(people).Named("PEOPLE"), (other ?? "").Named("OTHER"));
            string side = DipWords.P(NatureMath.SideKey(rule, eased), pawn);
            return DipWords.P(eased ? "CC_Nature_WhyEased" : "CC_Nature_Why", did.Named("ACT"), side.Named("SIDE"));
        }

        /// <summary>
        /// A mark the act may leave (NatureMath.MarkFamily): an oath sworn by a proud one; the same act against the same side
        /// twice within a year. The mark's own conditions and chance decide, and the persona's limits keep it rare.
        /// </summary>
        private static void Mark(Pawn p, NatureAct act, bool strained, int lastAct, int lastTick, int now, string label)
        {
            string family = NatureMath.MarkFamily(act, strained, lastAct, lastTick, now);
            if (family == null) return;
            try
            {
                PersonaMarks.Give(p, family, null, label.NullOrEmpty() ? null : label);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("nature: a mark", e);
            }
        }

        // ---------------- The council's questions ----------------

        /// <summary>
        /// The council's question names what an answer asks of someone's nature (a line on its sheet, beside the strain the
        /// heart's own effects show), when it would weigh on them.
        /// </summary>
        public static void Impact(ImpactSheet sheet, Pawn p, NatureAct act, string key, string brief, Faction people = null, string other = null)
        {
            if (sheet == null || p == null || Would(p, act) <= 0f) return;
            var args = new[] { p.Named("PAWN"), DipWords.People(people).Named("PEOPLE"), (other ?? "").Named("OTHER") };
            sheet.Add(ImpactKind.Needs, -1, DipWords.P(key, args), DipWords.P(brief, args));
        }

        /// <summary>A challenge turned down weighs on the proud and the hot-tempered: the words for the side it goes against.</summary>
        public static void ImpactRefused(ImpactSheet sheet, Pawn champion)
        {
            if (sheet == null || champion == null || Would(champion, NatureAct.RefusedChallenge) <= 0f) return;
            if (!NatureMath.RuleOf(NatureAct.RefusedChallenge, Sides(champion), out var rule)) return;
            Impact(sheet, champion, NatureAct.RefusedChallenge, rule.axis == PersonaAxis.Temper ? "CC_Impact_NatureRefuse_Temper" : "CC_Impact_NatureRefuse_Pride",
                "CC_Impact_NatureRefuseBrief");
        }

        // ---------------- Who it touches ----------------

        /// <summary>The colony's own close kin of someone (NatureMath.CloseKin), alive, grown past babyhood.</summary>
        public static List<Pawn> CloseKinOf(Pawn p)
        {
            var list = new List<Pawn>();
            if (p?.relations == null) return list;
            foreach (var r in p.relations.DirectRelations)
            {
                var o = r?.otherPawn;
                if (o == null || o == p || list.Contains(o) || !NatureMath.CloseKin(r.def?.defName) || !Feels(o)) continue;
                list.Add(o);
            }
            return list;
        }

        /// <summary>The colony's own parents of a child (the child's Parent relations).</summary>
        public static List<Pawn> ParentsOf(Pawn child)
        {
            var list = new List<Pawn>();
            if (child?.relations == null) return list;
            foreach (var r in child.relations.DirectRelations)
            {
                var o = r?.otherPawn;
                if (o == null || o == child || list.Contains(o) || !NatureMath.WardParent(r.def?.defName) || !Feels(o)) continue;
                list.Add(o);
            }
            return list;
        }

        /// <summary>A hostage of the colony's sets out: the hostage (the timid given, the closed sent away, the open eased), and their close kin if warm.</summary>
        public static void HostageGoes(Pawn hostage, Faction people)
        {
            if (!On || hostage == null) return;
            Felt(hostage, new[] { NatureAct.GivenHostage, NatureAct.SeasonAway }, people, null, "Hostage");
            foreach (var kin in CloseKinOf(hostage)) Felt(kin, NatureAct.KinHostage, people, hostage.LabelShort);
        }

        /// <summary>A youth of the colony's sets out to live with them as a ward: their parents, if warm.</summary>
        public static void WardGoes(Pawn ward, Faction people)
        {
            if (!On || ward == null) return;
            foreach (var parent in ParentsOf(ward)) Felt(parent, NatureAct.WardSent, people, ward.LabelShort);
        }

        /// <summary>A truce sworn with a people: each of the colony's at this home sworn to vengeance on them feels it, always.</summary>
        public static void TruceWithFoe(Faction people, Map home)
        {
            var comp = CouncilGameComponent.Instance;
            if (!On || people == null || comp?.vendettas?.vendettas == null) return;
            var seen = new HashSet<Pawn>();
            foreach (var v in comp.vendettas.vendettas)
            {
                if (v == null || !v.Active || v.faction != people || v.avenger == null || !seen.Add(v.avenger)) continue;
                if (v.avenger.DevelopmentalStage.Juvenile() || home != null && Homes.Of(v.avenger) != home) continue;
                Felt(v.avenger, NatureAct.TruceWithFoe, people, v.victim);
            }
        }
    }
}
