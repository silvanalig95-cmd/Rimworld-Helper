using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Setbacks set off by something happening right now: the game's hooks (SetbackHooks.cs) and the council's own code call these.</summary>
    public static partial class Setbacks
    {
        private static bool Live => RimfolkPatches.On && (S?.MoodEffects ?? false);

        // ---------------- Work ----------------

        /// <summary>A colonist finished building something: remembered if it's of note, and minded if it came out shoddy.</summary>
        public static void Notify_Built(Pawn worker, Thing t)
        {
            if (!Live || worker == null || t == null) return;
            Tracker?.Notify_Built(worker, t);
            Shoddy(worker, t, SkillDefOf.Construction);
        }

        /// <summary>A building is being destroyed (wrecked, or taken down).</summary>
        public static void Notify_Gone(Building b, DestroyMode mode)
        {
            if (!Live || b == null || (mode != DestroyMode.KillFinalize && mode != DestroyMode.Deconstruct)) return;
            Tracker?.Notify_Gone(b, mode);
        }

        /// <summary>A construction failed and its materials were wasted.</summary>
        public static void Notify_BuildFailed(Pawn worker, Frame frame)
        {
            if (!Live || worker == null || !Eligible(worker) || !(Tracker?.Ready(worker, "botched", 0.5f) ?? false)) return;
            var sk = worker.skills?.GetSkill(SkillDefOf.Construction);
            string what = frame?.def?.entityDefToBuild?.label ?? "?";
            Give(worker, SetbackDefOf.Council_Setback_BotchedBuild, null, "CC_Setback_Cause_BotchedBuild".Translate(what).Resolve(), sk != null && People.Passionate(sk) ? 1 : 0);
        }

        /// <summary>Something made (crafted, cooked, sculpted) came out of the bench.</summary>
        public static void Notify_Made(Pawn worker, Thing product, SkillDef skill)
        {
            if (!Live || worker == null || product == null || skill == null) return;
            Shoddy(worker, product, skill);
        }

        /// <summary>
        /// A poor or awful result in their own passion: poor from someone who should know better (skill 8 or more), awful
        /// from anyone past a beginner. Perfectionists (Vanilla Traits Expanded) have their own mood for it.
        /// </summary>
        private static void Shoddy(Pawn worker, Thing product, SkillDef skill)
        {
            if (!Eligible(worker) || !QualityUtility.TryGetQuality(product, out var q) || q > QualityCategory.Poor) return;
            var sk = worker.skills?.GetSkill(skill);
            if (sk == null || sk.TotallyDisabled || !People.Passionate(sk) || HasTrait(worker, "VTE_Perfectionist")) return;
            bool awful = q == QualityCategory.Awful;
            if (sk.Level < (awful ? 4 : 8)) return;
            if (!(Tracker?.Ready(worker, "shoddy", 0.5f) ?? false)) return;
            string label = (product is MinifiedThing m ? m.InnerThing : product)?.LabelNoCount ?? product.LabelNoCount;
            Give(worker, SetbackDefOf.Council_Setback_ShoddyWork, null, "CC_Setback_Cause_Shoddy".Translate(label, QualityUtility.GetLabel(q)).Resolve(), awful ? 1 : 0);
        }

        /// <summary>
        /// A job done at a makeshift spot (a crafting spot, a butcher spot, a campfire) when the colony could already build
        /// a proper bench for it: someone who cares about the work minds.
        /// </summary>
        public static void Notify_BillDone(Pawn worker, RecipeDef recipe, Thing bench)
        {
            if (!Live || worker == null || recipe == null || bench?.def == null || !bench.Spawned || Pressures.Gentle) return;
            if (!Makeshift(bench.def) || !Eligible(worker)) return;
            var sk = recipe.workSkill != null ? worker.skills?.GetSkill(recipe.workSkill) : null;
            if (sk == null || sk.TotallyDisabled || (!People.Passionate(sk) && sk.Level < 10)) return;
            var map = bench.Map;
            ThingDef better = null;
            foreach (var u in Avail.RecipeUsers(recipe))
            {
                if (u == null || u == bench.def || Makeshift(u) || u.category != ThingCategory.Building || !Avail.CanBuild(u, map)) continue;
                better = u;
                break;
            }
            if (better == null || !(Tracker?.Ready(worker, "makingDo", 1f) ?? false)) return;
            Give(worker, SetbackDefOf.Council_Setback_MakingDo, null, "CC_Setback_Cause_MakingDo".Translate(bench.def.label, better.label).Resolve());
        }

        /// <summary>A spot or a campfire rather than a bench.</summary>
        private static bool Makeshift(ThingDef d) =>
            d.defName.EndsWith("Spot", StringComparison.Ordinal) || d.defName == "Campfire";

        /// <summary>An operation went wrong in their hands.</summary>
        public static void Notify_SurgeryFailed(Pawn surgeon, Pawn patient)
        {
            if (!Live || surgeon == null || patient == null || !Eligible(surgeon)) return;
            // together: the surgeon at the patient's bedside, right after the operation.
            if (HasTrait(surgeon, "Psychopath") || !(Tracker?.Ready(surgeon, "surgery", 0.5f) ?? false)) return;
            var sk = surgeon.skills?.GetSkill(SkillDefOf.Medicine);
            int stage = sk != null && People.Passionate(sk) ? 1 : 0;
            if (!Give(surgeon, SetbackDefOf.Council_Setback_BotchedSurgery, null, "CC_Setback_Cause_Surgery".Translate(patient.LabelShort).Resolve(), stage)) return;
            Say(surgeon, "Council_Pool_Setback_Surgery", patient.Named("OTHER"));
            Write(surgeon, "Council_Journal_Setback_Surgery", 0.7f, patient.Named("OTHER"));
        }

        // ---------------- Home ----------------

        /// <summary>A proper meal eaten out in the open in freezing cold or rain.</summary>
        public static void Notify_Ate(Pawn p, Thing food)
        {
            if (!Live || p == null || food?.def?.ingestible == null || !p.Spawned || p.Map == null || !p.Map.IsPlayerHome) return;
            if (food.def.ingestible.preferability < FoodPreferability.MealSimple || !Eligible(p) || HasTrait(p, "Ascetic")) return;
            var room = p.GetRoom();
            if ((room != null && !room.PsychologicallyOutdoors) || p.Position.Roofed(p.Map)) return;
            var map = p.Map;
            bool cold = map.mapTemperature.OutdoorTemp < 0f;
            bool wet = map.weatherManager.RainRate > 0.3f || map.weatherManager.SnowRate > 0.3f;
            if (!cold && !wet) return;
            if (!(Tracker?.Ready(p, "coldMeal", 0.5f) ?? false)) return;
            Give(p, SetbackDefOf.Council_Setback_ColdMeal, null, (cold ? "CC_Setback_Cause_ColdMeal" : "CC_Setback_Cause_WetMeal").Translate(food.LabelNoCount).Resolve());
        }

        private static readonly HashSet<int> soldThisTick = new HashSet<int>();
        private static int soldTick = -1;

        /// <summary>Sold to a trader: a work of art by one of ours, or an animal one of ours looked after.</summary>
        public static void Notify_Sold(Thing t, ITrader trader)
        {
            if (!Live || t == null) return;
            int now = Find.TickManager.TicksGame;
            if (now != soldTick)
            {
                soldTick = now;
                soldThisTick.Clear();
            }
            if (!soldThisTick.Add(t.thingIDNumber)) return;
            string to = trader?.TraderName ?? "CC_Setback_ATrader".Translate().ToString();
            if (t is Pawn animal)
            {
                if (animal.RaceProps == null || !animal.RaceProps.Animal) return;
                var master = animal.playerSettings?.Master;
                if (master == null || !Eligible(master) || HasTrait(master, "Psychopath")) return;
                // A bond is the game's own grief.
                if (animal.relations != null && animal.relations.DirectRelationExists(PawnRelationDefOf.Bond, master)) return;
                Give(master, SetbackDefOf.Council_Setback_AnimalSold, null, "CC_Setback_Cause_AnimalSold".Translate(animal.LabelShort, to).Resolve());
                return;
            }
            var inner = t is MinifiedThing m ? m.InnerThing : t;
            var art = inner?.TryGetComp<CompArt>();
            if (art == null || !art.Active) return;
            var author = Author(art, t.MapHeld);
            if (author == null || HasTrait(author, "Greedy")) return;
            string title = art.Title.NullOrEmpty() ? inner.LabelNoCount : art.Title;
            if (Give(author, SetbackDefOf.Council_Setback_ArtSold, null, "CC_Setback_Cause_ArtSold".Translate(title, to).Resolve()))
                Write(author, "Council_Journal_Setback_ArtSold", 0.5f, title.Named("TITLE"));
        }

        /// <summary>The colonist who made a work of art, by the name it carries.</summary>
        private static Pawn Author(CompArt art, Map map)
        {
            string name;
            try
            {
                name = ColoredText.StripTags(art.AuthorName.RawText ?? "");
            }
            catch
            {
                return null;
            }
            if (name.NullOrEmpty()) return null;
            foreach (var p in Everyone())
            {
                string full = p.Name?.ToStringFull;
                if (name == full || name == ColoredText.StripTags(p.NameFullColored.RawText ?? "")) return p;
            }
            return null;
        }

        // ---------------- Crops ----------------

        /// <summary>A colonist sowed (from the records hook): remembered for the field.</summary>
        public static void Notify_Sown(Pawn p)
        {
            if (!Live || p?.CurJob == null || p.CurJob.def != JobDefOf.Sow || p.Map == null) return;
            Tracker?.Notify_Sown(p, p.Map, p.CurJob.targetA.Cell, p.CurJob.plantDefToSow);
        }

        public static void Notify_CropLost(Plant plant, bool blight)
        {
            if (!Live || plant == null || !plant.sown || !plant.Spawned || !plant.Map.IsPlayerHome) return;
            Tracker?.Notify_CropLost(plant, blight);
        }
    }
}
