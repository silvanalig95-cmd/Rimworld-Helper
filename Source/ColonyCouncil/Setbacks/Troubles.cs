using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The quiet troubles' memories (Defs/ThoughtDefs/Rimfolk_Thoughts_GoodLife.xml).</summary>
    [DefOf]
    public static class TroubleDefOf
    {
        public static ThoughtDef Rimfolk_Trouble_Worry;
        public static ThoughtDef Rimfolk_Trouble_Anniversary;
        public static ThoughtDef Rimfolk_Trouble_FormerLife;
        public static ThoughtDef Rimfolk_Trouble_Restless;
        public static ThoughtDef Rimfolk_Trouble_Aches;
        public static ThoughtDef Rimfolk_Trouble_BadNight;
        public static ThoughtDef Rimfolk_Trouble_Envy;

        static TroubleDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(TroubleDefOf));
    }

    /// <summary>A quiet trouble's memory: a setback's, which also says who made it pass sooner.</summary>
    public class Thought_Trouble : Thought_CouncilMemory
    {
        /// <summary>A deep talk, a good time with someone close or a gathering has made it pass sooner (once).</summary>
        public bool eased;

        /// <summary>Who it was with (null for a gathering).</summary>
        public string easedBy;

        public override string Description
        {
            get
            {
                string text = base.Description;
                if (!eased) return text;
                string line = (easedBy.NullOrEmpty() ? "CC_Trouble_EasedGathering".Translate() : "CC_Trouble_Eased".Translate(easedBy)).Resolve();
                return text + "\n\n" + line.Colorize(ColoredText.SubtleGrayColor);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref eased, "eased");
            Scribe_Values.Look(ref easedBy, "easedBy");
        }
    }

    /// <summary>
    /// Quiet troubles. Someone content for days on end still has a life with things in it that can bring a low day: family
    /// out in the world they worry about, the day someone they loved died coming round again, the life they had before
    /// they came, days of the same work and the same recreation, an old injury or the years aching (more in the cold and
    /// the wet), a nightmare (the dreams' own), a rival who holds an honor they don't. Once a day, at the first hour they
    /// are up and about at home with no danger near, a colonist content three days or more may have one: 2% on the third
    /// day, a point more each day after, 12% at most, and never within ten days of the last. Which one is read from their
    /// life; with nothing there, nothing comes. Each is a memory of -6 to -9 (a rival's honor -5) for a day to two, felt
    /// as "Life's ups and downs" and their standards say, with a line said aloud and often one in their journal. A deep
    /// talk, a good time with a friend or partner, or a party makes it pass sooner. The chance is in GoodLifeMath.
    /// </summary>
    public static class Troubles
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.quietTroubles && Setbacks.On(SetbackGroup.Life);

        /// <summary>The cooldown's key in the setbacks' record.</summary>
        private const string Cooldown = "trouble";

        private static readonly Color SayColor = new Color(1f, 0.7f, 0.6f);

        /// <summary>The game day each colonist last had their chance (not saved: after a load, a second chance that day at most).</summary>
        private static readonly Dictionary<int, int> rolled = new Dictionary<int, int>();

        public static void Reset() => rolled.Clear();

        private class Option
        {
            public ThoughtDef def;
            public int stage;
            public float weight = 1f;
            public string cause, pool, journal;
            public float journalChance = 0.6f;
            public readonly List<string> tags = new List<string>();
            public readonly List<NamedArgument> args = new List<NamedArgument>();
            /// <summary>A bad night: the dream is had when it's chosen, and its words become the cause.</summary>
            public bool badNight;
        }

        // ---------------- Content days ----------------

        /// <summary>Once a day (SetbackTracker): who has been content how many days in a row.</summary>
        public static void Daily(SetbackTracker tracker)
        {
            var fresh = new Dictionary<string, int>();
            foreach (var p in Setbacks.Everyone())
            {
                float mood = p.needs?.mood?.CurLevelPercentage ?? 0f;
                fresh[p.ThingID] = GoodLifeMath.NextContentDays(tracker.ContentDays(p), mood);
            }
            tracker.SetContentDays(fresh);
        }

        // ---------------- The chance ----------------

        /// <summary>Once an hour (SetbackTracker): each colonist's chance for the day, at the first hour it can come.</summary>
        public static void Hourly(SetbackTracker tracker, int now)
        {
            if (!On || tracker == null) return;
            int day = now / GenDate.TicksPerDay;
            if (rolled.Count > 200) rolled.RemoveAll(kv => kv.Value != day);
            foreach (var p in Setbacks.Everyone())
            {
                if (!p.Spawned || p.Map == null || !p.Map.IsPlayerHome || p.DevelopmentalStage.Juvenile()) continue;
                if (rolled.TryGetValue(p.thingIDNumber, out int d) && d == day) continue;
                int hour = GenLocalDate.HourOfDay(p.Map);
                if (hour < 7 || hour > 21 || !p.Awake() || p.Downed || p.Drafted || p.InMentalState) continue;
                if (p.Map.dangerWatcher != null && p.Map.dangerWatcher.DangerRating != StoryDanger.None) continue;
                rolled[p.thingIDNumber] = day;
                if (!tracker.Rested(p, Cooldown, GoodLifeMath.TroubleGapDays)) continue;
                if (!Rand.Chance(GoodLifeMath.TroubleChance(tracker.ContentDays(p)))) continue;
                try
                {
                    Try(tracker, p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quiet troubles", e);
                }
            }
        }

        /// <summary>A trouble from their life, if their life holds one now; true when one came.</summary>
        private static bool Try(SetbackTracker tracker, Pawn p)
        {
            var options = new List<Option>();
            void Look(string what, Action<Pawn, List<Option>> find)
            {
                try
                {
                    find(p, options);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("quiet troubles: " + what, e);
                }
            }
            Look("worry", Worry);
            Look("anniversary", Anniversary);
            Look("former life", FormerLife);
            Look("restless", Restless);
            Look("aches", Aches);
            Look("bad night", BadNight);
            Look("envy", Envy);
            options.RemoveAll(o => o.def == null || !Setbacks.On(o.def));
            while (options.Count > 0)
            {
                var o = options.RandomElementByWeight(x => x.weight);
                options.Remove(o);
                if (Give(tracker, p, o)) return true;
            }
            return false;
        }

        private static bool Give(SetbackTracker tracker, Pawn p, Option o)
        {
            if (o.badNight)
            {
                string dream = CouncilGameComponent.Instance?.dreams?.BadNight(CouncilGameComponent.Instance, p);
                if (dream.NullOrEmpty()) return false;
                o.cause = "CC_Trouble_Cause_BadNight".Translate(dream.Named("DREAM")).Resolve();
            }
            if (!Setbacks.Give(p, o.def, null, o.cause, o.stage)) return false;
            tracker.Mark(p, Cooldown);
            Say(p, o);
            if (o.journal != null && Rand.Chance(o.journalChance))
            {
                var args = new List<NamedArgument>(o.args) { p.Named("PAWN") };
                CouncilGameComponent.Instance?.journals.WriteTagged(p, o.journal, o.tags.ToArray(), null, args.ToArray());
            }
            return true;
        }

        // apart: a line said alone, over their own head; whoever it names (family far away, the dead, a rival) is only
        // thought of, never spoken to.
        private static void Say(Pawn p, Option o)
        {
            if (o.pool == null || !p.Spawned) return;
            string line = Flavor.FromPool(o.pool, p, null, null, o.tags.ToArray());
            if (line.NullOrEmpty()) return;
            var args = new List<NamedArgument> { p.Named("PAWN") };
            args.AddRange(o.args);
            CouncilGameComponent.Speak(p, line.Formatted(args.ToArray()).Resolve(), SayColor);
        }

        // ---------------- What their life holds ----------------

        // apart: family out in the world, named in the cause and the line; nobody is spoken to.
        /// <summary>Near family (a partner, parents, brothers and sisters, children, grandparents) living out in the world.</summary>
        private static void Worry(Pawn p, List<Option> list)
        {
            if (p.relations == null || Setbacks.HasTrait(p, "Psychopath")) return;
            Pawn best = null;
            int bestRank = int.MaxValue;
            foreach (var o in p.relations.RelatedPawns)
            {
                if (o == null || o == p || o.Dead || o.RaceProps == null || !o.RaceProps.Humanlike) continue;
                int rank = int.MaxValue;
                foreach (var r in p.GetRelations(o))
                {
                    int k = FamilyMath.TreeRank(r.defName);
                    if (k >= 0 && k < rank) rank = k;
                }
                if (GoodLifeMath.WorryStage(rank == int.MaxValue ? -1 : rank) < 0) continue;
                var where = Family.WhereKind(p, o);
                if (where != KinWhere.WithFaction && where != KinWhere.World) continue;
                if (p.relations.OpinionOf(o) < 0) continue;
                if (rank < bestRank || (rank == bestRank && Rand.Bool))
                {
                    best = o;
                    bestRank = rank;
                }
            }
            if (best == null) return;
            string relation = Family.Relation(p, best);
            if (relation.NullOrEmpty()) return;
            var opt = new Option
            {
                def = TroubleDefOf.Rimfolk_Trouble_Worry, stage = GoodLifeMath.WorryStage(bestRank),
                cause = "CC_Trouble_Cause_Worry".Translate(best.Named("OTHER"), p.Named("PAWN"), relation.Named("RELATION"),
                    Family.WhereWords(p, best).Named("WHERE")).Resolve(),
                pool = "Rimfolk_Pool_Trouble_Worry", journal = "Rimfolk_Journal_Trouble_Worry"
            };
            opt.tags.Add(bestRank <= 2 ? "partner" : bestRank == 3 ? "parent" : bestRank == 6 ? "child" : "kin");
            opt.args.Add(best.Named("OTHER"));
            opt.args.Add(relation.Named("RELATION"));
            list.Add(opt);
        }

        /// <summary>The day someone they loved died (the colony's calendar keeps the date) comes round again.</summary>
        private static void Anniversary(Pawn p, List<Option> list)
        {
            var cal = CalendarTracker.Instance;
            var map = p.MapHeld;
            if (cal?.days == null || map == null) return;
            int today = GenLocalDate.DayOfYear(map), year = GenLocalDate.Year(map);
            CalendarDay pick = null;
            bool kin = false;
            foreach (var d in cal.days)
            {
                if (d == null || d.kind != CalendarKind.Death || d.day != today || year <= d.sinceYear || d.pawnName.NullOrEmpty()) continue;
                bool k = d.kin != null && d.kin.Contains(p.ThingID);
                if (!k && (d.knew == null || !d.knew.Contains(p.ThingID))) continue;
                if (pick == null || (k && !kin))
                {
                    pick = d;
                    kin = k;
                }
            }
            if (pick == null) return;
            int years = year - pick.sinceYear;
            var opt = new Option
            {
                def = TroubleDefOf.Rimfolk_Trouble_Anniversary, stage = kin ? 1 : 0,
                // On its day it's what's on their mind: likelier than anything else that day.
                weight = 3f,
                cause = (years == 1 ? "CC_Trouble_Cause_AnniversaryOne".Translate(pick.pawnName.Named("NAME"))
                    : "CC_Trouble_Cause_AnniversaryMany".Translate(pick.pawnName.Named("NAME"), years.Named("YEARS"))).Resolve(),
                pool = "Rimfolk_Pool_Trouble_Anniversary", journal = "Rimfolk_Journal_Trouble_Anniversary", journalChance = 0.9f
            };
            opt.tags.Add(kin ? "kin" : "friend");
            opt.tags.Add(years == 1 ? "firstyear" : "years");
            opt.args.Add(pick.pawnName.Named("NAME"));
            opt.args.Add(years.Named("YEARS"));
            list.Add(opt);
        }

        /// <summary>The life they had before they came (their backstory), for someone not born here.</summary>
        private static void FormerLife(Pawn p, List<Option> list)
        {
            if (p.story == null || p.ageTracker == null || p.records == null) return;
            // A hard week already has them thinking of home (Council_Setback_Homesick).
            if (p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(SetbackDefOf.Council_Setback_Homesick) != null) return;
            float here = p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal);
            // Born here, or here most of their life: this is the only life they've had.
            if (here >= p.ageTracker.AgeBiologicalTicks * 0.6f) return;
            string title = Pasts.Title(p);
            if (title.NullOrEmpty()) return;
            string withArticle = Find.ActiveLanguageWorker.WithIndefiniteArticle(title, p.gender);
            var opt = new Option
            {
                def = TroubleDefOf.Rimfolk_Trouble_FormerLife, weight = GoodLifeMath.FormerLifeWeight(here / GenDate.TicksPerDay),
                cause = "CC_Trouble_Cause_FormerLife".Translate(p.Named("PAWN"), TextUtil.DaysString((int)here).Named("TIME"), withArticle.Named("TITLE")).Resolve(),
                pool = "Rimfolk_Pool_Trouble_FormerLife", journal = "Rimfolk_Journal_Trouble_FormerLife"
            };
            foreach (var k in Pasts.Of(p)) opt.tags.Add("past:" + Pasts.Tag(k));
            opt.args.Add(withArticle.Named("TITLE"));
            list.Add(opt);
        }

        /// <summary>Days of the same work (the everyday pressures' count) and the same recreation (the game's tolerances).</summary>
        private static void Restless(Pawn p, List<Option> list)
        {
            var rec = Pressures.Tracker?.Peek(p);
            if (rec?.streakWork == null || rec.streakDays < GoodLifeMath.RestlessWorkDays) return;
            var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(rec.streakWork);
            if (work == null || Pressures.SparedSameWork(p, work)) return;
            var tolerances = p.needs?.joy?.tolerances;
            JoyKindDef top = null;
            float topValue = 0f;
            var values = new List<float>();
            if (tolerances != null)
                foreach (var k in DefDatabase<JoyKindDef>.AllDefsListForReading)
                {
                    float v = tolerances[k];
                    values.Add(v);
                    if (v > topValue)
                    {
                        topValue = v;
                        top = k;
                    }
                }
            bool same = top != null && GoodLifeMath.SameRecreation(values);
            if (!GoodLifeMath.Restless(rec.streakDays, same)) return;
            string workLabel = Pressures.WorkLabel(rec.streakWork);
            var opt = new Option
            {
                def = TroubleDefOf.Rimfolk_Trouble_Restless,
                cause = (same
                    ? "CC_Trouble_Cause_RestlessJoy".Translate(rec.streakDays.Named("DAYS"), workLabel.Named("WORK"), top.label.Named("JOY"))
                    : "CC_Trouble_Cause_RestlessWork".Translate(rec.streakDays.Named("DAYS"), workLabel.Named("WORK"))).Resolve(),
                pool = "Rimfolk_Pool_Trouble_Restless", journal = "Rimfolk_Journal_Trouble_Restless"
            };
            if (same) opt.tags.Add("samejoy");
            opt.args.Add(workLabel.Named("WORK"));
            opt.args.Add(rec.streakDays.Named("DAYS"));
            list.Add(opt);
        }

        /// <summary>The chronic complaints that ache: a bad back, and any mod's arthritis or rheumatism.</summary>
        private static bool Aching(HediffDef def) =>
            def != null && (def.defName == "BadBack" || def.defName.IndexOf("Arthritis", StringComparison.OrdinalIgnoreCase) >= 0
                            || (def.label ?? "").IndexOf("arthritis", StringComparison.OrdinalIgnoreCase) >= 0
                            || (def.label ?? "").IndexOf("rheumat", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>An old injury, a bad back or a body of fifty and more, aching; more readily in the cold and the wet.</summary>
        private static void Aches(Pawn p, List<Option> list)
        {
            var set = p.health?.hediffSet;
            if (set == null) return;
            // Real pain already has the game's own mood.
            if (set.PainTotal >= 0.15f) return;
            Hediff_Injury scar = null;
            Hediff chronic = null;
            foreach (var h in set.hediffs)
            {
                if (h == null || !h.Visible) continue;
                if (h is Hediff_Injury inj && inj.Part != null && inj.IsPermanent())
                {
                    if (scar == null || inj.Severity > scar.Severity) scar = inj;
                }
                else if (h.def.chronic && Aching(h.def)) chronic = h;
            }
            float age = Flavor.HumanAge(p);
            if (scar == null && chronic == null && age < GoodLifeMath.AchingAge) return;
            var weather = Flavor.TagsFor(p);
            bool cold = weather.Contains("cold"), wet = weather.Contains("rain") || weather.Contains("snow");
            var opt = new Option
            {
                def = TroubleDefOf.Rimfolk_Trouble_Aches, weight = GoodLifeMath.AchesWeight(cold || wet),
                pool = "Rimfolk_Pool_Trouble_Aches", journal = "Rimfolk_Journal_Trouble_Aches"
            };
            string cause;
            if (chronic != null)
            {
                string what = chronic.LabelBase.ToLowerInvariant();
                cause = "CC_Trouble_Cause_AchesChronic".Translate(p.Named("PAWN"), what.Named("WHAT")).Resolve();
                opt.tags.Add("chronic");
                opt.args.Add(what.Named("WHAT"));
            }
            else if (scar != null)
            {
                string what = scar.LabelBase.ToLowerInvariant(), part = scar.Part.Label;
                cause = "CC_Trouble_Cause_AchesInjury".Translate(p.Named("PAWN"), what.Named("WHAT"), part.Named("PART")).Resolve();
                opt.tags.Add("scar");
                opt.args.Add(what.Named("WHAT"));
                opt.args.Add(part.Named("PART"));
            }
            else
            {
                int years = Mathf.FloorToInt(p.ageTracker.AgeBiologicalYearsFloat);
                cause = "CC_Trouble_Cause_AchesAge".Translate(p.Named("PAWN"), years.Named("AGE")).Resolve();
                opt.tags.Add("age");
            }
            if (cold) cause += " " + "CC_Trouble_Weather_Cold".Translate().Resolve();
            else if (wet) cause += " " + "CC_Trouble_Weather_Wet".Translate().Resolve();
            opt.cause = cause;
            list.Add(opt);
        }

        /// <summary>Woke from a night's sleep this morning, with no dream told yet: a nightmare (Dreams/), if one fits.</summary>
        private static void BadNight(Pawn p, List<Option> list)
        {
            if (!S.dreams) return;
            var dreams = CouncilGameComponent.Instance?.dreams;
            if (dreams == null || !dreams.WokeFromNight(p)) return;
            list.Add(new Option { def = TroubleDefOf.Rimfolk_Trouble_BadNight, badNight = true, pool = "Rimfolk_Pool_Trouble_BadNight" });
        }

        // apart: a rival named in the cause and the line; the two needn't be together for one to envy the other.
        /// <summary>
        /// A rival who holds an honor they don't: a seat on the council, a name (not a dubious one), a royal title. Not
        /// while the setback of the rival's honor is still on their mind, and not for the kind.
        /// </summary>
        private static void Envy(Pawn p, List<Option> list)
        {
            if (!S.jealousy || (p.story?.traits?.HasTrait(TraitDefOf.Kind) ?? false)) return;
            var comp = CouncilGameComponent.Instance;
            var memories = p.needs?.mood?.thoughts?.memories?.Memories;
            foreach (var o in Setbacks.Everyone().InRandomOrder())
            {
                if (o == p || !Setbacks.Rival(p, o)) continue;
                if (memories != null && memories.Any(m => m?.def == SetbackDefOf.Council_Setback_RivalHonoured && m.otherPawn == o)) continue;
                string key = null, honor = null, tag = null;
                var office = S.offices ? OfficeTracker.HeldBy(o) : null;
                if (office != null && OfficeTracker.HeldBy(p) == null)
                {
                    key = "CC_Trouble_Cause_EnvySeat";
                    honor = office.label;
                    tag = "seat";
                }
                if (honor == null && S.epithets && comp?.epithets != null)
                {
                    var name = comp.epithets.Of(o).FirstOrDefault(e => !e.dubious);
                    if (name != null && !comp.epithets.Of(p).Any(e => !e.dubious))
                    {
                        key = "CC_Trouble_Cause_EnvyName";
                        honor = name.label;
                        tag = "name";
                    }
                }
                if (honor == null)
                {
                    var title = o.royalty?.MostSeniorTitle;
                    if (title != null && p.royalty?.MostSeniorTitle == null)
                    {
                        key = "CC_Trouble_Cause_EnvyTitle";
                        honor = title.Label;
                        tag = "title";
                    }
                }
                if (honor.NullOrEmpty()) continue;
                var opt = new Option
                {
                    def = TroubleDefOf.Rimfolk_Trouble_Envy,
                    cause = key.Translate(o.Named("OTHER"), honor.Named("HONOR")).Resolve(),
                    pool = "Rimfolk_Pool_Trouble_Envy", journal = "Rimfolk_Journal_Trouble_Envy"
                };
                opt.tags.Add(tag);
                opt.args.Add(o.Named("OTHER"));
                opt.args.Add(honor.Named("HONOR"));
                list.Add(opt);
                return;
            }
        }

        // ---------------- Company that helps ----------------

        /// <summary>The game's and Rimfolk's gatherings that help a trouble pass (a party, a concert, a day of remembrance kept).</summary>
        private static readonly HashSet<string> Gatherings = new HashSet<string> { "AttendedParty", "AttendedConcert", "Council_Cal_Remembered" };

        /// <summary>
        /// A memory they just gained (Patch_GoodTimesFade): a deep talk with anyone, a good memory about a friend, partner
        /// or family member (not small talk), or a party makes a quiet trouble on their mind pass sooner, once: half of
        /// what is left of it goes.
        /// </summary>
        public static void Notify_Memory(Pawn p, Thought_Memory m)
        {
            if (p == null || m?.def == null || m is Thought_Trouble || !RimfolkPatches.On) return;
            var memories = p.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return;
            bool any = false;
            for (int i = 0; i < memories.Count; i++)
                if (memories[i] is Thought_Trouble t && !t.eased && t.age < t.DurationTicks)
                {
                    any = true;
                    break;
                }
            if (!any) return;
            try
            {
                if (!Comforting(p, m, out string who)) return;
                for (int i = 0; i < memories.Count; i++)
                {
                    if (!(memories[i] is Thought_Trouble t) || t.eased || t.age >= t.DurationTicks) continue;
                    t.age = GoodLifeMath.EasedAge(t.age, t.DurationTicks);
                    t.eased = true;
                    t.easedBy = who;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quiet troubles: company", e);
            }
        }

        // together: a deep talk and the game's social memories come from the two being face to face; a party from being at it.
        private static bool Comforting(Pawn p, Thought_Memory m, out string who)
        {
            who = null;
            string name = m.def.defName;
            var o = m.otherPawn;
            if (name == "Chitchat") return false;
            if (name == "DeepTalk")
            {
                who = o?.LabelShort;
                return true;
            }
            if (Gatherings.Contains(name)) return true;
            if (o == null || o == p || o.Dead || Setbacks.Closeness(p, o) < 1) return false;
            float mood = m.MoodOffset();
            float opinion = m is Thought_MemorySocial s ? s.OpinionOffset() : 0f;
            if (mood < 0f || opinion < 0f || (mood <= 0f && opinion <= 0f)) return false;
            who = o.LabelShort;
            return true;
        }
    }
}
