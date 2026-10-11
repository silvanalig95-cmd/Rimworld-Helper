using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A small thing that happens between two colonists: a late-night talk, a quarrel over chores, a prank, war
    /// stories, a lesson. Picked from who they are to each other (their tie, or how they feel when there is none)
    /// and their natures. Effects reuse the dilemma effect classes (A and B are the two colonists).
    /// </summary>
    public class SocialEventDef : Def
    {
        public float weight = 1f;
        /// <summary>Needs a tie of this kind between them (A is the mentor for Mentor ties). Null = any pair.</summary>
        public TieKind tie = TieKind.None;
        public float minStrength;
        /// <summary>Without a tie requirement: their opinions of each other must lie in this range (both ways).</summary>
        public int minOpinion = -100;
        public int maxOpinion = 100;
        public DilemmaRole roleA;
        public DilemmaRole roleB;
        public float minMood;
        [MustTranslate] public List<string> texts = new List<string>();
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        /// <summary>What it does to their bond.</summary>
        public TieKind changeTie = TieKind.None;
        public float change;
        /// <summary>Short note for the tie's history ({A}, {B}).</summary>
        [MustTranslate] public string note;
        public bool notable;
        /// <summary>Children can take part (without roles, events are for adults by default).</summary>
        public bool children;
        /// <summary>B keeps a secret A confided (not yet out).</summary>
        public bool bKeepsSecretOfA;
        /// <summary>A and B have sworn vengeance on the same faction ({FACTION}).</summary>
        public bool sharedVendetta;
        /// <summary>A has a quirk ({QUIRK}).</summary>
        public bool aHasQuirk;
        /// <summary>A is pursuing a life goal ({GOAL}).</summary>
        public bool aHasGoal;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (texts.NullOrEmpty()) yield return "social event has no texts";
        }
    }

    public static class SocialEvents
    {
        /// <summary>A social event or two most days, more in bigger colonies.</summary>
        public static void Daily(CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.socialWeb || !ColonyCouncilMod.Settings.socialEvents) return;
            var map = Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
            if (map == null) return;
            if (comp.social.battleActive || GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Baby() && !p.Downed && !p.InMentalState && !p.Drafted).ToList();
            if (pawns.Count < 2) return;
            float expected = UnityEngine.Mathf.Clamp(pawns.Count / 5f, 0.4f, 2f) * ColonyCouncilMod.Settings.socialEventFrequency;
            int count = (int)expected + (Rand.Chance(expected - (int)expected) ? 1 : 0);
            for (int i = 0; i < count; i++) TryOne(comp, map, pawns);
        }

        public static bool TryOne(CouncilGameComponent comp, Map map, List<Pawn> pawns)
        {
            foreach (var def in DefDatabase<SocialEventDef>.AllDefsListForReading.InRandomOrder().OrderByDescending(d => d.weight * Rand.Value))
            {
                Pawn a = null, b = null;
                SocialTie tie = null;
                try
                {
                    if (!Cast(def, comp, pawns, out a, out b, out tie)) continue;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("social event " + def.defName, e);
                    continue;
                }
                Fire(def, comp, map, a, b, tie);
                return true;
            }
            return false;
        }

        private static bool Cast(SocialEventDef def, CouncilGameComponent comp, List<Pawn> pawns, out Pawn a, out Pawn b, out SocialTie chosen)
        {
            a = b = null;
            chosen = null;
            if (def.tie != TieKind.None)
            {
                var tie = comp.social.ties.Where(t => t.kind == def.tie && t.announced && t.strength >= def.minStrength && pawns.Contains(t.a) && pawns.Contains(t.b))
                    .InRandomOrder().FirstOrDefault(t => Fits(def, t.a, t.b) || (def.tie != TieKind.Mentor && Fits(def, t.b, t.a)));
                if (tie == null) return false;
                chosen = tie;
                if (Fits(def, tie.a, tie.b)) { a = tie.a; b = tie.b; }
                else { a = tie.b; b = tie.a; }
                return true;
            }
            foreach (var x in pawns.InRandomOrder())
            {
                foreach (var y in pawns.InRandomOrder())
                {
                    if (x == y || x.relations == null || y.relations == null) continue;
                    int xy = x.relations.OpinionOf(y), yx = y.relations.OpinionOf(x);
                    if (xy < def.minOpinion || yx < def.minOpinion || xy > def.maxOpinion || yx > def.maxOpinion) continue;
                    if (!Fits(def, x, y)) continue;
                    a = x;
                    b = y;
                    return true;
                }
            }
            return false;
        }

        private static bool Fits(SocialEventDef def, Pawn a, Pawn b)
        {
            if (def.roleA != null ? !def.roleA.Allows(a, null) : !def.children && a.DevelopmentalStage.Juvenile()) return false;
            if (def.roleB != null ? !def.roleB.Allows(b, a) : !def.children && b.DevelopmentalStage.Juvenile()) return false;
            if (def.minMood > 0f && ((a.needs?.mood?.CurLevelPercentage ?? 0f) < def.minMood || (b.needs?.mood?.CurLevelPercentage ?? 0f) < def.minMood)) return false;
            var comp = CouncilGameComponent.Instance;
            if (def.bKeepsSecretOfA && !(comp.secrets.Of(a) is Secret s && !s.revealed && s.knowers.Contains(b))) return false;
            if (def.sharedVendetta && SharedVendetta(comp, a, b) == null) return false;
            if (def.aHasQuirk && !comp.quirks.Of(a).Any()) return false;
            if (def.aHasGoal && comp.aspirations.Active(a) == null) return false;
            return true;
        }

        private static Faction SharedVendetta(CouncilGameComponent comp, Pawn a, Pawn b) =>
            comp.vendettas.Of(a).Select(v => v.faction).FirstOrDefault(f => comp.vendettas.Of(b).Any(v => v.faction == f));

        private static void Fire(SocialEventDef def, CouncilGameComponent comp, Map map, Pawn a, Pawn b, SocialTie tie)
        {
            var ctx = new DilemmaContext { map = map, a = a, b = b, skill = tie?.skill, why = def.label };
            string text = ctx.Format(Flavor.PickText(def.texts));
            ctx.cause = text;
            if (def.sharedVendetta) ctx.faction = SharedVendetta(comp, a, b);
            foreach (var e in def.effects)
            {
                try
                {
                    e.Apply(ctx);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("social event effect " + def.defName, ex);
                }
            }
            string note = def.note.NullOrEmpty() ? null : ctx.Format(def.note);
            if (def.changeTie != TieKind.None && def.change != 0f)
            {
                // The bond the event was about (a mentorship in particular, when there are two between them).
                if (tie != null && tie.kind == def.changeTie && def.change > 0f)
                {
                    tie.strength = UnityEngine.Mathf.Min(100f, tie.strength + def.change);
                    tie.Note(note);
                }
                else if (def.change > 0f) comp.social.Bump(a, b, def.changeTie, def.change, note);
                else comp.social.Weaken(a, b, def.changeTie, -def.change, note);
            }
            Notify.Message(text, new LookTargets(a, b), MessageTypeDefOf.SilentInput, def.notable ? Importance.Notable : Importance.Flavor);
            if (def.notable) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Bond, text, a));
        }
    }
}
