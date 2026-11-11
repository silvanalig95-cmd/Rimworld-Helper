using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a god on the map leaves behind (docs/design/gods.md, "On the map"): the colony's standing with it, a find where
    /// it stood, the kindest colonist's lasting gift (a passion, a trait, A RimWorld of Magic's spell or class of its domain,
    /// or its favour), its answer in the weather, and its curse: on one who struck its stranger, a fever through the
    /// shared harm cap and illness budget (Harm.May, IllnessBudget) at the game's own rate (Harm.Scale); on the colony, its
    /// domain withheld (the rain kept away, dry thunder, the game gone from the land, the fires guttering). Everything is
    /// the game's own things, told and shown.
    /// </summary>
    public static class GodGifts
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The god in words at the realness step: named plainly in Walking, else by name only once the colony knows it.</summary>
        private static NamedArgument[] A(God g) => GodsOnMapWords.Args(g, Gods.Realness == GodRealness.Walking);

        /// <summary>
        /// The stranger's god judges the colony: favour moved by the treatment, and the blessing, the gift or the curse
        /// that goes with it. Returns what it left, in words, for the card.
        /// </summary>
        public static string Outcome(God god, Map map, IntVec3 at, Treatment t, Pawn kindest, Pawn harmer, StrangerWelcome welcome)
        {
            if (god == null || map == null) return null;
            var lines = new List<string>();
            try
            {
                string why = ("CC_GodM_Why_" + t).Translate(GodStranger.Traveller(GodStranger.Age).Named("TRAVELLER")).Resolve();
                Gods.AddFavour(god, map, GodsOnMapMath.FavourFor(t), why, GodsOnMapMath.SourceOf(t), GodsOnMapMath.OffenceOf(t));
                if (GodsOnMapMath.Blesses(t))
                {
                    string found = LeaveFind(god, map, at, t == Treatment.Generous ? 2 : 1);
                    if (found != null) lines.Add(found);
                }
                if (GodsOnMapMath.Gives(t) && kindest != null)
                {
                    string gift = Give(kindest, god, map);
                    if (gift != null) lines.Add(gift);
                }
                if (t >= Treatment.Kind && GodWeather.Answer(god, map, GodsOnMapMath.FavourFor(t), true)) lines.Add("CC_GodM_Out_Weather".Translate(A(god)).Resolve());
                if (GodsOnMapMath.Curses(t))
                {
                    if (t == Treatment.Harmed && harmer != null)
                    {
                        string fever = Fever(harmer, map, god);
                        if (fever != null) lines.Add(fever);
                    }
                    string withheld = Withhold(god, map);
                    if (withheld != null) lines.Add(withheld);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: what the stranger left", e);
            }
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        // ================= A find =================

        /// <summary>
        /// A find where a god stood or led someone (GodsOnMapMath.Find: its domain's things in the colony's age), the
        /// first that exists, <paramref name="share"/> times the usual count. Its words, or null.
        /// </summary>
        public static string LeaveFind(God god, Map map, IntVec3 at, int share = 1)
        {
            if (god == null || map == null || !at.IsValid) return null;
            var (names, count) = GodsOnMapMath.Find(god.domain, GodStranger.Age);
            var def = names.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && !d.MadeFromStuff);
            if (def == null) return null;
            int left = Math.Max(1, count * Math.Max(1, share));
            int placed = 0;
            while (left > 0)
            {
                var th = ThingMaker.MakeThing(def);
                int n = Math.Min(left, Math.Max(1, def.stackLimit));
                th.stackCount = n;
                if (!GenPlace.TryPlaceThing(th, at, map, ThingPlaceMode.Near)) break;
                placed += n;
                left -= n;
            }
            if (placed == 0) return null;
            FleckMaker.ThrowLightningGlow(at.ToVector3Shifted(), map, 2f);
            return "CC_GodM_Out_Find".Translate(A(god).Concat(new[] { placed.Named("COUNT"), def.label.Named("THING") }).ToArray()).Resolve();
        }

        // ================= The kindest colonist's gift =================

        private static bool Magic => ModsConfig.IsActive("torann.arimworldofmagic") || DefDatabase<TraitDef>.GetNamedSilentFail("TM_Gifted") != null;

        /// <summary>
        /// The lasting gift of a god pleased with <paramref name="p"/>: a spell of its domain for one who casts, sometimes a
        /// class of its domain for one with none, a passion for its work, a trait, or its favour (GodsOnMapMath.Gift). Told,
        /// with a light over them, and written in their journal. Its words, or null.
        /// </summary>
        public static string Give(Pawn p, God god, Map map)
        {
            if (p == null || god == null || p.Dead || p.story == null) return null;
            var d = god.domain;
            var spell = Magic && MagicClasses.Level(p, false) >= 0 ? GodsOnMapMath.Spells(d).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(x => x != null) : null;
            var cls = Magic && !HasClass(p) ? GodsOnMapMath.Classes(d).Select(n => DefDatabase<TraitDef>.GetNamedSilentFail(n)).FirstOrDefault(x => x != null && TraitFits(p, x, Degree(x)) && ClassOn(x.defName)) : null;
            var skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(GodsOnMapMath.Skill(d));
            var skill = skillDef != null ? p.skills?.GetSkill(skillDef) : null;
            bool passionOpen = skill != null && !skill.TotallyDisabled && skill.passion != Passion.Major;
            var (traitName, degree) = GodsOnMapMath.Trait(d);
            var trait = DefDatabase<TraitDef>.GetNamedSilentFail(traitName);
            bool traitOpen = trait != null && TraitFits(p, trait, degree) && p.story.traits.allTraits.Count < 5;
            var gift = GodsOnMapMath.Gift(spell != null, cls != null, passionOpen, traitOpen, Rand.Value);
            string what = null;
            try
            {
                switch (gift)
                {
                    case GodGift.Spell:
                        var scroll = ThingMaker.MakeThing(spell);
                        if (p.Spawned && GenPlace.TryPlaceThing(scroll, p.Position, p.Map, ThingPlaceMode.Near)) what = "CC_GodM_Gift_Spell".Translate(spell.label.Named("THING")).Resolve();
                        break;
                    case GodGift.Class:
                        p.story.traits.GainTrait(new Trait(cls, Degree(cls), false));
                        Renew(p);
                        what = "CC_GodM_Gift_Class".Translate(cls.DataAtDegree(Degree(cls)).GetLabelFor(p).Named("CLASS")).Resolve();
                        break;
                    case GodGift.Passion:
                        skill.passion = Passion.Major;
                        what = "CC_GodM_Gift_Passion".Translate(skillDef.label.Named("SKILL")).Resolve();
                        break;
                    case GodGift.Trait:
                        p.story.traits.GainTrait(new Trait(trait, degree, false));
                        what = "CC_GodM_Gift_Trait".Translate(trait.DataAtDegree(degree).GetLabelFor(p).Named("TRAIT")).Resolve();
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's gift", e);
                what = null;
            }
            if (what == null)
            {
                // Its favour: the colony stands better with it for their sake, and a spark of inspiration.
                Gods.AddFavour(god, map, GodsOnMapMath.FavourGift, "CC_GodM_Why_Favour".Translate(p.LabelShort.Named("PAWN")).Resolve());
                try
                {
                    var insp = p.mindState?.inspirationHandler?.GetRandomAvailableInspirationDef();
                    if (insp != null) p.mindState.inspirationHandler.TryStartInspiration(insp, "CC_GodM_InspiredBy".Translate(A(god)).Resolve());
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a god's favour", e);
                }
                what = "CC_GodM_Gift_Favour".Translate(A(god)).Resolve();
            }
            if (p.Spawned) FleckMaker.ThrowLightningGlow(p.DrawPos, p.Map, 1.6f);
            string line = "CC_GodM_Out_Gift".Translate(A(god).Concat(new[] { p.Named("PAWN"), what.Named("WHAT") }).ToArray()).Resolve();
            CouncilGameComponent.Instance?.journals?.WriteText(p, "CC_GodM_Journal_Gift".Translate(A(god).Concat(new[] { what.Named("WHAT") }).ToArray()).Resolve());
            return line;
        }

        private static int Degree(TraitDef d) => d.degreeDatas.NullOrEmpty() ? 0 : d.degreeDatas[0].degree;

        /// <summary>They could have this trait: not already, nothing they have conflicts with it, their past doesn't forbid it.</summary>
        private static bool TraitFits(Pawn p, TraitDef def, int degree)
        {
            var traits = p.story?.traits;
            if (traits == null || def == null || traits.HasTrait(def)) return false;
            if (def.degreeDatas != null && !def.degreeDatas.Any(x => x.degree == degree)) return false;
            if (traits.allTraits.Any(t => t?.def != null && (t.def.ConflictsWith(def) || def.ConflictsWith(t.def)))) return false;
            return !p.story.AllBackstories.Any(b => b != null && b.DisallowsTrait(def, degree));
        }

        /// <summary>One of A RimWorld of Magic's classes already (a class keeps every other off, as the mod keeps them).</summary>
        private static bool HasClass(Pawn p) =>
            p.story.traits.allTraits.Any(t => t?.def != null && (PeoplesGearMath.MagicSetting(t.def.defName) != null || t.def.defName == "Necromancer" || t.def.defName == "DeathKnight"));

        private static Type settingsType;
        private static bool settingsLooked;

        /// <summary>The class is switched on in A RimWorld of Magic's own settings (read by name; on when it can't be read).</summary>
        private static bool ClassOn(string trait)
        {
            string setting = PeoplesGearMath.MagicSetting(trait) ?? trait;
            try
            {
                if (!settingsLooked)
                {
                    settingsLooked = true;
                    settingsType = AccessTools.TypeByName("TorannMagic.ModOptions.Settings");
                }
                var settings = settingsType == null ? null : AccessTools.Field(settingsType, "Instance")?.GetValue(null);
                if (settings == null) return true;
                return !(AccessTools.Field(settingsType, setting)?.GetValue(settings) is bool on) || on;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>A RimWorld of Magic's components look again whether the pawn casts (as the peoples' gifts do: Peoples/PeopleGifts.cs).</summary>
        private static void Renew(Pawn p)
        {
            if (p.AllComps == null) return;
            foreach (var c in p.AllComps)
            {
                if (c == null) continue;
                var type = c.GetType();
                string flag = type.Name == "CompAbilityUserMagic" ? "initializedIsMagicUser" : type.Name == "CompAbilityUserMight" ? "initializedIsMightUser" : null;
                if (flag == null) continue;
                try
                {
                    AccessTools.Field(type, flag)?.SetValue(c, false);
                    var index = AccessTools.Field(type, "customIndex");
                    if (index != null && index.FieldType == typeof(int)) index.SetValue(c, -2);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: waking a class of A RimWorld of Magic", e);
                }
            }
        }

        // ================= The curse =================

        /// <summary>
        /// The one who struck a god's stranger or beast falls ill with a fever: only under the shared cap (Harm.May) and
        /// within the illness budget (IllnessBudget.Allows, Owe), at a chance by the setting and the storyteller
        /// (Harm.Scale); told, at once. Its words, or null.
        /// </summary>
        public static string Fever(Pawn p, Map map, God god)
        {
            var flu = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            if (p == null || p.Dead || flu == null || p.health?.hediffSet == null || p.health.hediffSet.HasHediff(flu)) return null;
            if (!Harm.On || !Harm.May(p) || !Rand.Chance(Mathf.Min(0.9f, 0.8f * Harm.Scale)) || !IllnessBudget.Allows(map, flu, p)) return null;
            try
            {
                var h = HediffMaker.MakeHediff(flu, p);
                h.Severity = 0.2f;
                p.health.AddHediff(h);
                if (!p.health.hediffSet.HasHediff(flu)) return null;
                IllnessBudget.Owe(map, flu, p);
                string text = "CC_GodM_Out_Fever".Translate(A(god).Concat(new[] { p.Named("PAWN") }).ToArray()).Resolve();
                Tell.Message(NewsKind.People, Importance.Important, map, text, new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
                return text;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's fever", e);
                return null;
            }
        }

        /// <summary>
        /// A god's domain withheld from the colony for a while: the rain kept away (the harvest, the waters, the wild), dry
        /// thunder where the land has it (storms), the wild game leaving the land (the hunt), the colony's lights guttering
        /// for a night (the hearth, the night). Nothing for the rest but the god's displeasure. Its words, or null.
        /// </summary>
        public static string Withhold(God god, Map map)
        {
            if (god == null || map == null) return null;
            try
            {
                switch (god.domain)
                {
                    case GodDomain.Harvest:
                    case GodDomain.Waters:
                    case GodDomain.Wild:
                        map.weatherDecider.DisableRainFor(GenDate.TicksPerDay * 5);
                        return "CC_GodM_Out_NoRain".Translate(A(god)).Resolve();
                    case GodDomain.Storm:
                        return GodWeather.Storm(map, true) ? "CC_GodM_Out_DryThunder".Translate(A(god)).Resolve() : null;
                    case GodDomain.Hunt:
                        return GodOmens.Leave(map, a => !a.RaceProps.predator, 10) > 0 ? "CC_GodM_Out_GameGone".Translate(A(god)).Resolve() : null;
                    case GodDomain.Hearth:
                    case GodDomain.Night:
                        return GodOmens.Gutter(map, 4, GenDate.TicksPerHour * 8) > 0 ? "CC_GodM_Out_Fires".Translate(A(god)).Resolve() : null;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's domain withheld", e);
            }
            return null;
        }
    }
}
