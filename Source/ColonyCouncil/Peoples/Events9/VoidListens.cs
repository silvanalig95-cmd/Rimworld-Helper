using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 88. The void listens back (the Choir of the Spire) =====================

    /// <summary>
    /// The void listens back (item 88, Anomaly): a warden of the quiet of the Choir comes for a colonist shaken by the void (with Vanilla
    /// Anomaly Expanded - Insanity, their sanity below half; else a memory of the void on them: the inhuman rambling heard, the darkness,
    /// a ritual done on them) and stays at their side through the day (eight hours, following them about; round 9's sitting with). Some of
    /// their sanity returns (Insanity's own need, three tenths of its whole) and the void's memories on them are lifted. At the end the
    /// warden says what kind of threat the void will send next, read from what the game could send to the colony now; the word holds:
    /// the void's next threat within the month is that one (the storyteller's own, steered through the mast's hook on Storyteller.TryFire,
    /// never one more), unless it can't come then. Or the warden only listens an hour at the colony's edge and gives the warning. One of
    /// the events' visits (kind "pev:void"): x the warden, a the colonist, its text "people|sit" or "people|edge".
    /// </summary>
    public static class VoidListens
    {
        public const string Kind = "pev:void";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Anomaly's memories of the void, lifted by a day with the warden (the ones that are memories, not a room or a weather).</summary>
        public static readonly string[] VoidMemories =
        {
            "HeardInhumanRambling", "SpokeToDisturbing", "SwallowedByDarkness", "HarbingerScream", "PsychicRitualVictim", "DrainedMySkills", "ObeliskAbduction", "UsedMeForPsychicRitual"
        };

        public static NeedDef SanityDef => DefDatabase<NeedDef>.GetNamedSilentFail("VAEI_Sanity");

        /// <summary>Their sanity now (0 to 1 of its whole), -1 without Insanity's need.</summary>
        public static float Sanity(Pawn p)
        {
            var def = SanityDef;
            var need = def == null ? null : p?.needs?.TryGetNeed(def);
            return need == null || need.MaxLevel <= 0f ? -1f : need.CurLevel / need.MaxLevel;
        }

        private static List<Thought_Memory> Memories(Pawn p) =>
            p?.needs?.mood?.thoughts?.memories?.Memories?.Where(m => m?.def != null && VoidMemories.Contains(m.def.defName)).ToList() ?? new List<Thought_Memory>();

        public static bool Shaken(Pawn p) => p != null && PeopleEvents9Math.Shaken(Sanity(p), Memories(p).Count > 0);

        public static Pawn Pick(Map map) =>
            PeoplesG.Grown(map).Where(p => Shaken(p) && OrderOf(p) == null).OrderBy(p => Sanity(p) < 0f ? 1f : Sanity(p)).ThenByDescending(p => Memories(p).Count).FirstOrDefault();

        public static Visit OrderOf(Pawn on) => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done && v.a == on);

        /// <summary>One of Anomaly's own threats, the void's sending.</summary>
        public static bool VoidThreat(IncidentDef d) =>
            d?.category != null && (d.category == IncidentCategoryDefOf.ThreatBig || d.category == IncidentCategoryDefOf.ThreatSmall)
            && string.Equals(d.modContentPack?.PackageId, ModContentPack.AnomalyModPackageId, StringComparison.OrdinalIgnoreCase);

        /// <summary>The void's next threat as the warden hears it: one of Anomaly's the game could send to this home now, by its own weight.</summary>
        public static IncidentDef Next(Map map)
        {
            if (!ModsConfig.AnomalyActive || map == null) return null;
            var can = new List<IncidentDef>();
            foreach (var d in DefDatabase<IncidentDef>.AllDefsListForReading)
            {
                if (!VoidThreat(d) || d.baseChance <= 0f && d.baseChanceWithRoyalty <= 0f) continue;
                try
                {
                    var parms = StorytellerUtility.DefaultParmsNow(d.category, map);
                    if (d.Worker.CanFireNow(parms)) can.Add(d);
                }
                catch
                {
                    // One that can't be asked here is not heard.
                }
            }
            return can.RandomElementByWeightWithFallback(d => Mathf.Max(0.05f, d.baseChance));
        }

        /// <summary>The answer taken: the warden to the colonist's side through the day, or to the colony's edge an hour; at once with the play-outs off.</summary>
        public static void Begin(HappeningContext ctx, bool sit)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var x = ctx?.stranger;
            if (map == null || t == null || x == null || ctx.faction == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, x, sit ? ctx.b : null, null, Now + (sit ? 12 : 4) * GenDate.TicksPerHour);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + (sit ? "sit" : "edge");
            if (PlayOuts.On)
            {
                if (sit && PeoplesG.Up(ctx.b, map))
                {
                    // The warden's way to the colonist's side counts from now, in a room visitors otherwise keep out of (KeepOut,
                    // PeopleErrands). Still no way to them: the warden waits at the gate and the visit's hour tries again, never the
                    // day's good done unseen.
                    KeepOut.Refresh(map);
                    PeopleEvents9.Errand(x, map, ctx.b, PeopleEvents9.SitWithJob, PeopleEvents9Math.VoidSitTicks, 11f);
                    return;
                }
                var edge = Edge(map);
                if (!sit && edge.IsValid && PeopleEvents9.Errand(x, map, edge, PeopleEvents9.ListenEdgeJob, PeopleEvents9Math.VoidListenTicks, 3f)) return;
            }
            End(v);
        }

        /// <summary>The colony's edge: open ground beyond its home area, a walk from its middle.</summary>
        public static IntVec3 Edge(Map map)
        {
            var home = map.areaManager.Home;
            var mid = CampSite.ColonyCenter(map);
            for (int i = 0; i < 10; i++)
            {
                var c = mid + IntVec3.FromVector3(Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0f, 360f)) * Rand.Range(18f, 28f));
                if (!c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.Roofed(map) || home != null && home[c]) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var x = v.x;
            bool sit = Mode(v) == "sit";
            var job = sit ? PeopleEvents9.SitWithJob : PeopleEvents9.ListenEdgeJob;
            bool working = x != null && x.Spawned && x.CurJobDef == job;
            if (sit && (v.a == null || v.a.Dead || !v.a.Spawned || v.a.Map != v.map))
            {
                GiveUp(v);
                return false;
            }
            if (working && now <= v.until) return true;
            if (now > v.until || !PeoplesG.Up(x, v.map))
            {
                GiveUp(v);
                return false;
            }
            // Pulled away (a fight, a meal): back to it.
            if (!v.map.dangerWatcher.DangerRating.Equals(StoryDanger.None)) return true;
            if (sit) PeopleEvents9.Send(x, job, v.a, PeopleEvents9Math.VoidSitTicks / 2);
            return true;
        }

        private static string Mode(Visit v)
        {
            var p = (v?.text ?? "").Split('|');
            return p.Length > 1 ? p[1] : "sit";
        }

        /// <summary>The day at their side done (SitWith, one not of a mind that shouts).</summary>
        public static void Sat(Pawn warden, Pawn on)
        {
            var v = PeopleEvents9.Of(Kind, warden);
            if (v != null && v.a == on) End(v);
        }

        /// <summary>The hour at the colony's edge done.</summary>
        public static void Listened(Pawn warden)
        {
            var v = PeopleEvents9.Of(Kind, warden);
            if (v != null) End(v);
        }

        /// <summary>
        /// The visit's end: with a day at their side, their sanity part returned and the void's memories lifted; either way the warning, told
        /// and kept; the warden home.
        /// </summary>
        private static void End(Visit v)
        {
            if (v.done) return;
            v.done = true;
            var x = v.x;
            var map = v.map;
            var f = x?.Faction ?? PeopleEvents9.PeopleOf(v);
            var on = v.a;
            if (Mode(v) == "sit" && on != null && !on.Dead)
            {
                try
                {
                    var need = SanityDef == null ? null : on.needs?.TryGetNeed(SanityDef);
                    if (need != null) need.CurLevel = PeopleEvents9Math.SanityAfter(need.CurLevel, need.MaxLevel);
                    foreach (var m in Memories(on)) on.needs.mood.thoughts.memories.RemoveMemory(m);
                    PeopleEvents9.Thought(on, "Rimfolk_Thought_QuietWarden");
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 9: the warden's day", e);
                }
                PeoplesG.SayLine(x, v.incident, "quiet");
                Tell.Message(NewsKind.Health, Importance.Notable, map, "CC_PEv9_VoidSat".Translate((x?.LabelShort ?? "?").Named("WARDEN"), on.Named("PAWN")).Resolve(),
                    on.Spawned ? new LookTargets(on) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            Warn(x, map, f, v.incident);
            PeopleEvents9.Goodwill(f, PeopleEvents9Math.VoidGoodwill, GoodwillWhy.TheirWork);
            if (x != null) PeoplesG.Home(new List<Pawn> { x });
        }

        /// <summary>The warden's word: the void's next threat to this home, kept until it comes or the month is out; or the void quiet.</summary>
        public static void Warn(Pawn warden, Map map, Faction f, IncidentDef incident)
        {
            if (map == null) return;
            var next = Next(map);
            string who = warden?.LabelShort ?? f?.Name ?? "?";
            if (next == null)
            {
                Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv9_VoidQuiet".Translate(who.Named("WARDEN")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            PeopleEvents9Book.Instance?.Foretell(next.defName, who, Now + PeopleEvents9Math.ForetoldDays * GenDate.TicksPerDay, map);
            if (warden != null && warden.Spawned) PeoplesG.SayLine(warden, incident, "warn");
            Tell.Letter(NewsKind.World, Importance.Important, map, "CC_PEv9_VoidWarnLabel".Translate().Resolve(),
                "CC_PEv9_VoidWarn".Translate(who.Named("WARDEN"), next.label.Named("KIND"), (f?.Name ?? "?").Named("FACTION")).Resolve(), LetterDefOf.NeutralEvent, LookTargets.Invalid, f);
        }

        private static void GiveUp(Visit v)
        {
            if (v.done) return;
            v.done = true;
            if (v.map != null)
                Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv9_VoidGaveUp".Translate((v.x?.LabelShort ?? "?").Named("WARDEN")).Resolve(), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
            if (v.x != null) PeoplesG.Home(new List<Pawn> { v.x });
        }

        /// <summary>The hour: the warden's word let go once the month is out.</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents9Book.Instance;
            if (b?.foretold != null && (now > b.foretoldUntil || b.foretoldMap == null || !Find.Maps.Contains(b.foretoldMap))) b.ClearForetold();
        }

        /// <summary>
        /// The storyteller fires a threat at a home (Patch_LineMastWarns, before anything else is asked of it): while the warden's word
        /// holds for that home and the threat is one of the void's, it is the one named, when that one can come now. Nothing is added: the
        /// storyteller's own threat only takes the form the warden heard. Told as it comes.
        /// </summary>
        public static void Steer(FiringIncident fi)
        {
            var b = PeopleEvents9Book.Instance;
            var map = fi?.parms?.target as Map;
            if (b?.foretold == null || map == null || map != b.foretoldMap || Now > b.foretoldUntil || !VoidThreat(fi.def)) return;
            var named = DefDatabase<IncidentDef>.GetNamedSilentFail(b.foretold);
            if (named == null)
            {
                b.ClearForetold();
                return;
            }
            if (fi.def != named)
            {
                bool can;
                try
                {
                    can = named.Worker.CanFireNow(fi.parms);
                }
                catch
                {
                    can = false;
                }
                if (!can) return;
                fi.def = named;
            }
            string who = b.foretoldBy;
            b.ClearForetold();
            Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv9_VoidCame".Translate((who ?? "?").Named("WARDEN"), named.label.Named("KIND")).Resolve(), LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>
    /// A warden of the quiet of {FACTION} ({X}) at the gate: {B} has been shaken by the void, and {X} would stay at {B}'s side through the
    /// day; at the end, the warden's word on what the void will send next. Anomaly only.
    /// </summary>
    public class HappeningWorker_VoidListens : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            ModsConfig.AnomalyActive && PeoplesD.Friendly(PeoplesMath.Listeners) != null && VoidListens.Pick(map) != null && !MapFacts.ThreatOn(map) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Listeners);
            ctx.b = VoidListens.Pick(ctx.map);
            if (ctx.faction == null || ctx.b == null) return false;
            ctx.variant = VoidListens.Sanity(ctx.b) >= 0f ? 1 : 0;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Choir_QuietWarden"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = ctx.b;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.variant > 0) tags.Add("sanity");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option != 0) return null;
            if (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || ctx.b.Map != ctx.map) return "CC_PEv9_ShakenGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "sit" : option == 1 ? "edge" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The warden at {B}'s side through the day (<see cref="sit"/>), or only an hour at the colony's edge; the void's next threat named at the end.</summary>
    public class DilemmaEffect_VoidListens : DilemmaEffect
    {
        public bool sit = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.b == null) return;
            if (sit)
            {
                if (PlayOuts.On) sheet.Play("CC_PEv9_Impact_VoidSit".Translate(hc.stranger?.LabelShort ?? "?", hc.b.LabelShort).Resolve(), "CC_PEv9_Impact_VoidSitBrief".Translate().Resolve());
                sheet.Add(ImpactKind.Health, 1, (hc.variant > 0 ? "CC_PEv9_Impact_Sanity" : "CC_PEv9_Impact_Memories").Translate(hc.b.LabelShort).Resolve(), "CC_PEv9_Impact_SanityBrief".Translate().Resolve(),
                    "pev9sanity");
            }
            else if (PlayOuts.On) sheet.Play("CC_PEv9_Impact_VoidEdge".Translate(hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv9_Impact_VoidEdgeBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, 1, "CC_PEv9_Impact_Foretold".Translate(PeopleEvents9Math.ForetoldDays).Resolve(), "CC_PEv9_Impact_ForetoldBrief".Translate().Resolve(), "pev9foretold");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents9Math.VoidGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => VoidListens.Begin(ctx as HappeningContext, sit);
    }
}
