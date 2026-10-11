using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Something a dead colonist carried, kept by the person who loved them most.</summary>
    public class Heirloom : IExposable
    {
        public Thing thing;
        public string ofName;
        public Pawn heir;
        public int tick;
        public bool lostNoticed;

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref ofName, "ofName");
            Scribe_References.Look(ref heir, "heir");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref lostNoticed, "lostNoticed");
        }
    }

    /// <summary>
    /// What outlives people, and what marks the stages of a life: heirlooms passed on at death, and children
    /// coming of age, when the council helps choose their path.
    /// </summary>
    public class LegacyTracker : IExposable
    {
        public List<Heirloom> heirlooms = new List<Heirloom>();
        private HashSet<string> children = new HashSet<string>();
        /// <summary>Who came of age recently (for the dilemma about their path).</summary>
        public List<Pawn> cameOfAge = new List<Pawn>();
        private Dictionary<Pawn, int> cameOfAgeTick = new Dictionary<Pawn, int>();
        private List<Pawn> k1;
        private List<int> v1;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static bool Eligible(Pawn p) => p != null && !p.Dead && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike;

        public Heirloom HeirloomCarriedBy(Pawn p)
        {
            if (p == null) return null;
            foreach (var h in heirlooms)
            {
                if (h.thing == null || h.thing.Destroyed) continue;
                if (p.equipment?.Primary == h.thing || (p.apparel?.WornApparel.Contains(h.thing as Apparel) ?? false)) return h;
                // Carried as a sidearm (Simple Sidearms keeps those in the inventory).
                if (h.thing.def.IsWeapon && (p.inventory?.innerContainer?.Contains(h.thing) ?? false)) return h;
            }
            return null;
        }

        // ---------------- Heirlooms ----------------

        /// <summary>Called when a colonist has died: their weapon or hat goes to the one who loved them most.</summary>
        public void Notify_Death(CouncilGameComponent comp, Pawn dead)
        {
            if (!S.heirlooms || dead == null) return;
            Thing thing = dead.equipment?.Primary;
            if (thing == null)
                thing = dead.apparel?.WornApparel.FirstOrDefault(a => a.def.apparel?.layers?.Contains(ApparelLayerDefOf.Overhead) ?? false);
            if (thing == null || thing.Destroyed) return;
            var heir = PawnsFinder.AllMaps_FreeColonists.Where(Eligible).Where(p => p.relations != null && !p.DevelopmentalStage.Baby())
                .OrderByDescending(p => p.relations.OpinionOf(dead) + comp.social.Warmth(p, dead) + (p.GetRelations(dead).Any() ? 40f : 0f)).FirstOrDefault();
            if (heir == null || heir.relations.OpinionOf(dead) < 20 && comp.social.Warmth(heir, dead) < 30f && !heir.GetRelations(dead).Any()) return;
            heirlooms.Add(new Heirloom { thing = thing, ofName = dead.LabelShort, heir = heir, tick = Find.TickManager.TicksGame });
            string text = "CC_Heirloom_Passed".Translate(heir.Named("PAWN"), dead.Named("OTHER"), thing.def.label.Named("ITEM")).Resolve();
            Notify.Message(text, new LookTargets(heir, thing), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, text, heir));
            comp.journals.Write(heir, "Council_Journal_Heirloom", null, dead.Named("OTHER"), thing.def.label.Named("ITEM"));
        }

        private void CheckHeirlooms(CouncilGameComponent comp)
        {
            foreach (var h in heirlooms.ToList())
            {
                if (h.heir == null || h.heir.Dead)
                {
                    heirlooms.Remove(h);
                    continue;
                }
                if (h.thing != null && !h.thing.Destroyed) continue;
                // Gone: destroyed, burnt, broken, sold off the map.
                if (!h.lostNoticed && h.heir.IsColonist)
                {
                    h.lostNoticed = true;
                    CouncilGameComponent.GiveThought(h.heir, CouncilDefOf.Council_HeirloomLost);
                    comp.journals.Write(h.heir, "Council_Journal_HeirloomLost", null, h.ofName.Named("LOST"));
                }
                heirlooms.Remove(h);
            }
        }

        // ---------------- Coming of age ----------------

        private void CheckChildren(CouncilGameComponent comp)
        {
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.Where(Eligible))
            {
                if (p.DevelopmentalStage.Juvenile())
                {
                    // A newborn: something for the adults to think about.
                    if (children.Add(p.ThingID) && (p.ageTracker?.AgeBiologicalTicks ?? long.MaxValue) < GenDate.TicksPerDay * 3)
                        foreach (var o in PawnsFinder.AllMaps_FreeColonists.Where(o => o != p && Eligible(o) && !o.DevelopmentalStage.Juvenile()))
                            comp.aspirations.Notify_Event(o, "birth");
                    continue;
                }
                if (!children.Remove(p.ThingID)) continue;
                ComingOfAge(comp, p);
            }
            foreach (var p in cameOfAge.Where(p => p == null || p.Dead || !cameOfAgeTick.TryGetValue(p, out int t) || Find.TickManager.TicksGame - t > GenDate.TicksPerDay * 6).ToList())
            {
                cameOfAge.Remove(p);
                if (p != null) cameOfAgeTick.Remove(p);
            }
        }

        private void ComingOfAge(CouncilGameComponent comp, Pawn p)
        {
            if (!S.comingOfAge) return;
            string text = "CC_ComingOfAge_Text".Translate(p.Named("PAWN")).Resolve();
            Find.LetterStack.ReceiveLetter("CC_ComingOfAge_Label".Translate(p.LabelShort), text, LetterDefOf.PositiveEvent, p);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, p));
            comp.journals.Write(p, "Council_Journal_ComingOfAge");
            foreach (var o in PawnsFinder.AllMaps_FreeColonists.Where(o => o != p && Eligible(o) && o.relations != null && !o.DevelopmentalStage.Juvenile()))
            {
                if (o.GetRelations(p).Any(r => r == PawnRelationDefOf.Parent))
                {
                    CouncilGameComponent.GiveThought(o, CouncilDefOf.Council_ChildGrown, p);
                    comp.journals.Write(o, "Council_Journal_ChildGrown", null, p.Named("OTHER"));
                }
                else if (o.relations.OpinionOf(p) >= 20) CouncilGameComponent.GiveThought(o, CouncilDefOf.Council_SawThemGrow, p);
            }
            cameOfAge.Add(p);
            cameOfAgeTick[p] = Find.TickManager.TicksGame;
            // The council is asked what path they should take.
            if (S.enableDilemmas) comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_ComingOfAge);
        }

        public void Daily(CouncilGameComponent comp)
        {
            try
            {
                CheckHeirlooms(comp);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heirlooms", e);
            }
            CheckChildren(comp);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref heirlooms, "heirlooms", LookMode.Deep);
            Scribe_Collections.Look(ref children, "children", LookMode.Value);
            Scribe_Collections.Look(ref cameOfAge, "cameOfAge", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.Saving)
                foreach (var k in cameOfAgeTick.Keys.Where(k => k == null || k.Dead || k.Destroyed).ToList()) cameOfAgeTick.Remove(k);
            Scribe_Collections.Look(ref cameOfAgeTick, "cameOfAgeTick", LookMode.Reference, LookMode.Value, ref k1, ref v1, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (heirlooms == null) heirlooms = new List<Heirloom>();
                heirlooms.RemoveAll(h => h == null || h.heir == null);
                if (children == null) children = new HashSet<string>();
                if (cameOfAge == null) cameOfAge = new List<Pawn>();
                cameOfAge.RemoveAll(p => p == null);
                if (cameOfAgeTick == null) cameOfAgeTick = new Dictionary<Pawn, int>();
                cameOfAgeTick.Remove(null);
            }
        }
    }

    /// <summary>"Carrying their sword": comfort for whoever carries an heirloom, more for the heir.</summary>
    public class ThoughtWorker_Heirloom : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.legacy == null || !ColonyCouncilMod.Settings.heirlooms) return ThoughtState.Inactive;
            var h = comp.legacy.HeirloomCarriedBy(p);
            if (h == null) return ThoughtState.Inactive;
            return ThoughtState.ActiveAtStage(h.heir == p ? 0 : 1);
        }
    }

    public class Thought_Heirloom : Thought_Situational
    {
        public override string LabelCap
        {
            get
            {
                var h = CouncilGameComponent.Instance?.legacy.HeirloomCarriedBy(pawn);
                string label = base.LabelCap;
                return h?.thing != null ? label.Formatted(h.ofName.Named("LOST"), h.thing.def.label.Named("ITEM")).Resolve().CapitalizeFirst() : label;
            }
        }
    }

    /// <summary>A dilemma about someone who has just come of age: A is them, B (if any) a possible teacher.</summary>
    public class DilemmaWorker_ComingOfAge : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.comingOfAge || !CanHappenNow(map)) return false;
            foreach (var a in comp.legacy.cameOfAge.Where(p => p != null && p.Spawned && p.Map == map).InRandomOrder())
            {
                if (!def.roleA.Allows(a, null)) continue;
                Pawn b = null;
                if (def.roleB != null)
                {
                    var options = map.mapPawns.FreeColonistsSpawned.Where(p => def.roleB.Allows(p, a)).ToList();
                    if (def.roleB.bestAt != null) options = options.OrderByDescending(p => p.skills?.GetSkill(def.roleB.bestAt)?.Level ?? 0).Take(1).ToList();
                    b = options.RandomElementWithFallback();
                    if (b == null) continue;
                }
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = def.thingCount };
                if (!Extra(ctx)) continue;
                comp.legacy.cameOfAge.Remove(a);
                return true;
            }
            return false;
        }
    }

    /// <summary>B takes A on as a student in a skill (a real mentorship in the social web).</summary>
    public class DilemmaEffect_Mentorship : DilemmaEffect
    {
        public SkillDef skill;

        public override void Apply(DilemmaContext ctx)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || ctx.a == null || ctx.b == null || skill == null) return;
            social.StartMentorship(ctx.b, ctx.a, skill);
        }
    }
}
