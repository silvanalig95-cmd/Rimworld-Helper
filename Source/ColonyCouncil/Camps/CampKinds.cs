using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Words a camp's texts can use beyond its own: {IDEO}, the faith of the people camped (with Ideology), and
    /// {SITUATION}, the world event that put them on the road (a famine, a plague) when one is running that the camp
    /// kind lists. And the tags its lines can ask for: [ideology] and [sameFaith] (they share the colony's faith),
    /// [ws:hunger], [ws:sickness]... (a world event of that kind is running), and the season.
    /// </summary>
    public static class CampWords
    {
        /// <summary>The faith of a people, by name (with Ideology), or "".</summary>
        public static string Ideo(Faction f)
        {
            if (!ModsConfig.IdeologyActive || f == null) return "";
            try
            {
                return f.ideos?.PrimaryIdeo?.name ?? "";
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp ideo", e);
                return "";
            }
        }

        /// <summary>A people shares the colony's own faith.</summary>
        public static bool SameFaith(Faction f)
        {
            if (!ModsConfig.IdeologyActive || f == null) return false;
            try
            {
                var theirs = f.ideos?.PrimaryIdeo;
                return theirs != null && theirs == Faction.OfPlayer?.ideos?.PrimaryIdeo;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The running world event that brings this kind of camp (the first listed kind that runs), or null.</summary>
        public static WorldSituation Situation(CampKindDef kind)
        {
            if (kind == null || kind.situationKinds.NullOrEmpty()) return null;
            var inst = WorldSituations.Instance;
            if (inst == null) return null;
            return inst.Running.FirstOrDefault(s => s.def != null && kind.situationKinds.Contains(s.def.kind));
        }

        /// <summary>The name the texts use for a world event: "the gray cough", or the event itself.</summary>
        public static string SituationName(WorldSituation s) => s == null ? "" : !s.ctx.name.NullOrEmpty() ? s.ctx.name : s.def.label;

        /// <summary>{IDEO} and {SITUATION} filled in (before the usual placeholders).</summary>
        public static string Fill(string s, CampKindDef kind, Faction f)
        {
            if (s.NullOrEmpty()) return s;
            if (s.Contains("{IDEO}")) s = s.Replace("{IDEO}", Ideo(f));
            if (s.Contains("{SITUATION}")) s = s.Replace("{SITUATION}", SituationName(Situation(kind)));
            return s;
        }

        /// <summary>The camp's own tags: faith, the world event behind it, the season.</summary>
        public static void AddTags(HashSet<string> tags, CampKindDef kind, Faction f, Map map)
        {
            if (tags == null) return;
            if (!Ideo(f).NullOrEmpty()) tags.Add("ideology");
            if (SameFaith(f)) tags.Add("sameFaith");
            var s = Situation(kind);
            if (s?.def?.kind != null) tags.Add("ws:" + s.def.kind);
            if (map != null && !tags.Contains("spring") && !tags.Contains("summer") && !tags.Contains("fall") && !tags.Contains("winter"))
            {
                switch (GenLocalDate.Season(map))
                {
                    case Season.Spring: tags.Add("spring"); break;
                    case Season.Summer:
                    case Season.PermanentSummer: tags.Add("summer"); break;
                    case Season.Fall: tags.Add("fall"); break;
                    case Season.Winter:
                    case Season.PermanentWinter: tags.Add("winter"); break;
                }
            }
        }
    }

    public partial class Camp
    {
        /// <summary>How many times surveyors have shown the colony a seam (once is enough).</summary>
        public int surveys;

        /// <summary>
        /// A help line of the camp's own kind (its helpTexts tagged with the help: [heal], [show]...), or the general
        /// one from the translations. {X} is whoever helped, {A} the colonist it was for, {COUNT} and {THING} what.
        /// </summary>
        private string HelpText(string help, Pawn x = null, Pawn a = null, int count = 0, string thing = null)
        {
            var lines = kind?.helpTexts?.Where(l => HasTag(l, help)).ToList();
            if (!lines.NullOrEmpty())
            {
                var tags = new HashSet<string> { help };
                Flavor.AddEraTags(tags);
                CampWords.AddTags(tags, kind, faction, map);
                string line = Flavor.Pick(lines, tags) ?? Flavor.PickText(lines);
                if (!line.NullOrEmpty()) return Format(line, x, a, count, thing);
            }
            return Text("CC_Camp_Help_" + help, x, a, count, thing);
        }

        private static bool HasTag(string line, string tag)
        {
            Flavor.Strip(line, out var t);
            return t != null && t.Any(x => x.Split('|').Any(a => a.Trim() == tag));
        }

        /// <summary>The good turns of the newer kinds of camp. False if none came about today.</summary>
        private bool MoreHelp(string h)
        {
            var colonists = IncidentWorker_Happening.Colonists(map);
            var here = Here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile()).ToList();
            if (here.Count == 0 || colonists.Count == 0) return false;
            switch (h)
            {
                case "heal":
                {
                    // A colonist with an everyday ailment who has come by the camp is seen to, by the best healer of
                    // those beside them (Together); failing that, herbs left for the colony's stores.
                    int Medicine(Pawn p) => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                    var patient = colonists.Where(p => HappeningWorker_Setting.Ailment(p) != null && here.Any(h => Together.Can(h, p))).RandomElementWithFallback();
                    var healer = patient != null ? here.Where(h => Together.Can(h, patient)).OrderByDescending(Medicine).First() : here.OrderByDescending(Medicine).First();
                    if (patient != null)
                    {
                        var ail = HappeningWorker_Setting.Ailment(patient);
                        string what = ail.LabelBase;
                        ail.Severity = Mathf.Max(0.01f, ail.Severity * 0.4f);
                        Warm(patient, 2);
                        CouncilGameComponent.GiveThought(patient, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Camp_Healed"), healer);
                        Tell(Text("CC_Camp_HelpTitle"), HelpText("heal", healer, patient, 0, what), LetterDefOf.PositiveEvent, false);
                        return true;
                    }
                    int n = Rand.RangeInclusive(2, 4);
                    var med = ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);
                    med.stackCount = n;
                    if (!GenPlace.TryPlaceThing(med, share, map, ThingPlaceMode.Near)) return false;
                    Tell(Text("CC_Camp_HelpTitle"), HelpText("herbs", healer, null, n), LetterDefOf.PositiveEvent, false);
                    return true;
                }
                case "show":
                {
                    // An evening performance at their fire: everyone at home who can come, comes.
                    int hour = GenLocalDate.HourOfDay(map);
                    if (hour < 16 || hour > 21) return false;
                    var star = here.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Artistic)?.Level ?? 0).First();
                    var show = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Camp_Show");
                    if (PlayOuts.On)
                    {
                        // The colony walks over to their fire for it: a gathering at the camp (answers on the map), and
                        // the evening is remembered by those who came. With nobody free tonight, everyone it was for
                        // still has the memory, as answers that can't play out always do.
                        var effects = new List<DilemmaEffect>
                        {
                            new DilemmaEffect_Gathering
                            {
                                music = true, organizer = PlayWho.None, spots = new List<PlaySpot> { PlaySpot.Camp, PlaySpot.Stranger, PlaySpot.Outdoors },
                                label = "CC_Together_CampShowLabel".Translate().Resolve(), hours = new IntRange(hour, 23), waitHours = 2f,
                                duration = new FloatRange(1.5f, 2.5f)
                            }
                        };
                        if (show != null) effects.Add(new DilemmaEffect_Thought { thought = show, target = DilemmaTarget.Colony });
                        PlayOuts.Run(effects, new DilemmaContext { map = map, stranger = star, why = Label }, "camp show");
                    }
                    else
                    {
                        foreach (var c in colonists.Where(c => !c.Downed && !c.InMentalState))
                        {
                            c.needs?.joy?.GainJoy(0.2f, JoyKindDefOf.Social);
                            CouncilGameComponent.GiveThought(c, show);
                        }
                        if (!(kind?.lines).NullOrEmpty()) Arrivals.Say(star, Pick(kind.lines, star));
                    }
                    Tell(Text("CC_Camp_HelpTitle"), HelpText("show", star, colonists.RandomElement()), LetterDefOf.PositiveEvent, false);
                    return true;
                }
                case "blessing":
                {
                    var elder = here.OrderByDescending(p => p.ageTracker?.AgeBiologicalYears ?? 0).First();
                    var blessed = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Camp_Blessing");
                    foreach (var c in colonists.Where(c => !(c.story?.traits?.HasTrait(TraitDefOf.Psychopath) ?? false)))
                        CouncilGameComponent.GiveThought(c, blessed);
                    Tell(Text("CC_Camp_HelpTitle"), HelpText("blessing", elder, colonists.RandomElement()), LetterDefOf.PositiveEvent, false);
                    return true;
                }
                case "survey":
                {
                    // Once per camp: their sightings turn up a seam of ore in the colony's own rock.
                    if (surveys > 0 || !StorytellerEvents.TryRockFace(map, 10f, out var cell)) return false;
                    var ore = StorytellerEvents.Ore(false);
                    if (ore == null) return false;
                    var seam = StorytellerEvents.Seam(map, cell, ore, Rand.RangeInclusive(4, 7));
                    if (seam.Count == 0) return false;
                    surveys++;
                    var surveyor = here.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Mining)?.Level ?? 0).First();
                    Find.LetterStack.ReceiveLetter(Text("CC_Camp_HelpTitle"), HelpText("survey", surveyor, colonists.RandomElement(), 0, ore.label),
                        LetterDefOf.PositiveEvent, new LookTargets(seam[0]), faction);
                    return true;
                }
            }
            return false;
        }
    }
}
