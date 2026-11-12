using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 14. A pilgrimage (the pilgrim fellowship) =====================

    /// <summary>A shrine a file of pilgrims is bound for: a fellowship town, a town that keeps a relic, an old shrine at a landmark.</summary>
    public class Shrine
    {
        /// <summary>"town", "relic" or "old" (PeopleEvents2Math.ShrineKinds).</summary>
        public string kind;
        public PlanetTile tile;
        public string name;
        public float road;
    }

    /// <summary>
    /// The pilgrimage (item 14): a file of the fellowship bound for a shrine asks whether anyone of the colony would walk with
    /// them. The one who goes is away ten to thirty days as a caravan of their own (SeasonAway): the roads, the weather and
    /// the pilgrims' cough are the world's and the budget's own. Home, they bring a shell (and with Vanilla Ideology Expanded
    /// - Relics and Artifacts a small token of a relic's shrine), a lasting memory of it, steadier faith (Ideology's
    /// certainty), and a grief or guilt eased; the fellowship thinks better of the colony. On foot in every age.
    /// </summary>
    public static class Pilgrimage
    {
        public const string GiftKind = "pilgrimage";
        public const string AwayKey = "pilgrimage";

        public static ThingDef ShellDef => DefDatabase<ThingDef>.GetNamedSilentFail("Rimfolk_PilgrimShell");

        public static ThoughtDef Memory => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_Pilgrimage");

        /// <summary>The shrines within reach of a walk there and back in thirty days, nearest first.</summary>
        public static List<Shrine> Shrines(Map map, Faction fellowship)
        {
            var list = new List<Shrine>();
            if (map == null) return list;
            try
            {
                foreach (var s in Find.WorldObjects.Settlements)
                {
                    if (s == null || s.Destroyed || s.Faction == null || s.Faction.IsPlayer || s.Faction.HostileTo(Faction.OfPlayer) || s.Tile.Layer != map.Tile.Layer) continue;
                    float road = SeasonAway.RoadDays(map, s.Tile);
                    if (!PeopleEvents2Math.ShrineInReach(road)) continue;
                    if (s.Faction == fellowship) list.Add(new Shrine { kind = "town", tile = s.Tile, name = s.Name, road = road });
                    else if (ModsConfig.IdeologyActive && RelicOf(s.Faction) is Precept_Relic relic)
                        list.Add(new Shrine { kind = "relic", tile = s.Tile, name = "CC_PEv2_RelicShrine".Translate(s.Name.Named("TOWN"), relic.Label.Named("RELIC")).Resolve(), road = road });
                }
                var marks = Find.World?.landmarks?.landmarks;
                if (marks != null)
                    foreach (var kv in marks)
                    {
                        if (kv.Value == null || kv.Value.name.NullOrEmpty() || !kv.Key.Valid || kv.Key.Layer != map.Tile.Layer || kv.Key == map.Tile) continue;
                        float road = SeasonAway.RoadDays(map, kv.Key);
                        if (road < 1.5f || !PeopleEvents2Math.ShrineInReach(road)) continue;
                        list.Add(new Shrine { kind = "old", tile = kv.Key, name = "CC_PEv2_OldShrine".Translate(kv.Value.name.Named("NAME")).Resolve(), road = road });
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the shrines in reach", e);
            }
            return list.OrderBy(x => x.road).ToList();
        }

        /// <summary>A relic a people's faith keeps (Ideology), or null.</summary>
        public static Precept_Relic RelicOf(Faction f)
        {
            if (f?.ideos == null) return null;
            foreach (var ideo in f.ideos.AllIdeos)
            {
                var r = ideo?.GetAllPreceptsOfType<Precept_Relic>().FirstOrDefault(p => !p.Label.NullOrEmpty());
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>The colony's people who could walk with them: grown, up, free to leave, never had a pilgrimage.</summary>
        public static IEnumerable<Pawn> Takers(Map map) => PeopleEvents.Takers(map, GiftKind).Where(p => PeopleEvents2.MayLeave(p, map));

        /// <summary>Their heaviest grief or guilt now (a memory that feeds the heart's grief or shame), or null.</summary>
        public static Thought_Memory Burden(Pawn p, out string which)
        {
            which = null;
            var mems = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (mems == null) return null;
            Thought_Memory best = null;
            float worst = 0f;
            foreach (var m in mems)
            {
                if (m?.def == null || m.CurStage == null) continue;
                float off = m.MoodOffset();
                if (off >= 0f || off >= worst) continue;
                string k = Feelings.Feeds(m.def, "Grief") ? "grief" : Feelings.Feeds(m.def, "Shame") ? "guilt" : null;
                if (k == null) continue;
                worst = off;
                best = m;
                which = k;
            }
            return best;
        }

        /// <summary>Who would most want to go: one carrying a grief or a guilt, else one whose faith is shaken, else anyone.</summary>
        public static Pawn Pick(List<Pawn> takers, Pawn not = null) =>
            takers.Where(p => p != not).OrderByDescending(p => Burden(p, out _) != null ? 2 : 0).ThenBy(p => Faiths.Holds(p) ? Faiths.Certainty(p) : 1f).ThenBy(p => Rand.Value).FirstOrDefault();

        /// <summary>A second one who might go: the colony's most faithful, else another.</summary>
        public static Pawn Second(List<Pawn> takers, Pawn first) =>
            takers.Where(p => p != first).OrderByDescending(p => Faiths.Holds(p) ? Faiths.Certainty(p) : 0f).ThenBy(p => Rand.Value).FirstOrDefault();
    }

    /// <summary>
    /// A file of {FACTION}'s pilgrims at the gate, bound for {PLACE} ({TALE} there and back), asks whether anyone of the colony
    /// would walk with them: {A_nameDef} (who carries a grief or a guilt, or whose faith is shaken), or {B_nameDef}.
    /// </summary>
    public class HappeningWorker_Pilgrimage : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Pilgrims);
            return f != null && SeasonAway.Running(Pilgrimage.AwayKey) == null && Pilgrimage.Takers(map).Any() && Pilgrimage.Shrines(map, f).Count > 0 && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Pilgrims);
            if (ctx.faction == null) return false;
            var shrine = Pilgrimage.Shrines(ctx.map, ctx.faction).Take(4).RandomElementWithFallback();
            var takers = Pilgrimage.Takers(ctx.map).ToList();
            ctx.a = Pilgrimage.Pick(takers);
            if (shrine == null || ctx.a == null) return false;
            ctx.b = Pilgrimage.Second(takers, ctx.a);
            float days = PeopleEvents2Math.ShrineDays(shrine.road, Rand.Value);
            ctx.count = Mathf.RoundToInt(PeopleEvents2Math.AwayDays(shrine.road, days));
            ctx.tale = TextUtil.DaysString(ctx.count * GenDate.TicksPerDay);
            ctx.placeName = shrine.name;
            ctx.note = shrine.kind + "|" + shrine.tile + "|" + days.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Pilgrims_Keeper"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 0; i < Rand.RangeInclusive(2, 3); i++) ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Pilgrims_Pilgrim"), ctx.faction, ext.ages));
            ctx.group.RemoveAll(p => p == null);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            string kind = (ctx.note ?? "").Split('|')[0];
            if (!kind.NullOrEmpty()) tags.Add(kind);
            if (Pilgrimage.Burden(ctx.a, out string which) != null) tags.Add(which);
            else if (Faiths.Holds(ctx.a) && Faiths.Certainty(ctx.a) < 0.5f) tags.Add("doubt");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            var who = option == 0 ? ctx.a : ctx.b;
            if (who == null) return "CC_PEv2_NoSecond".Translate().Resolve();
            if (!PeopleEvents2.MayLeave(who, ctx.map)) return "CC_PEv2_CantLeave".Translate(who.LabelShort.Named("PAWN")).Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "walk" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>{A} (or {B} with <see cref="second"/>) walks out with the file and sets out as a caravan of their own for the shrine.</summary>
    public class DilemmaEffect_Pilgrimage : DilemmaEffect
    {
        public bool second;

        public override bool ChanceEach => false;

        private Pawn Who(DilemmaContext ctx) => second ? ctx?.b : ctx?.a;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            var who = Who(ctx);
            if (hc == null || who == null) return;
            sheet.Add(ImpactKind.People, -1, "CC_PEv2_Impact_Away".Translate(who.LabelShort, hc.tale ?? "?", hc.placeName ?? "?").Resolve(), "CC_PEv2_Impact_AwayBrief".Translate().Resolve(), "pev2away");
            sheet.Play("CC_PEv2_Impact_WalkOut".Translate(who.LabelShort, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv2_Impact_WalkOutBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Health, -1, "CC_PEv2_Impact_Road".Translate(who.LabelShort).Resolve(), "CC_PEv2_Impact_RoadBrief".Translate().Resolve(), "pev2road");
            if (Harm.On) sheet.Add(ImpactKind.Health, -1, "CC_PEv2_Impact_Cough".Translate(who.LabelShort, Mathf.RoundToInt(PeopleEvents2Math.PilgrimCough * 100f)).Resolve(), "CC_PEv2_Impact_CoughBrief".Translate().Resolve(), "pev2cough");
            string burden = Pilgrimage.Burden(who, out string which) is Thought_Memory m ? m.LabelCap.ToString() : null;
            sheet.Add(ImpactKind.Mood, 1, (burden != null ? "CC_PEv2_Impact_HomeEased" : "CC_PEv2_Impact_Home").Translate(who.LabelShort, burden ?? "").Resolve(), "CC_PEv2_Impact_HomeBrief".Translate().Resolve(), "pev2home");
            if (hc.faction != null) sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv2_Impact_Fellowship".Translate(hc.faction.Name, PeopleEvents2Math.PilgrimGoodwill).Resolve(), "CC_PEv2_Impact_FellowshipBrief".Translate().Resolve(), "pev2fellow");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var who = Who(ctx);
            if (hc?.map == null || who == null || hc.faction == null) return;
            var bits = (hc.note ?? "").Split('|');
            if (bits.Length < 3 || !PlanetTile.TryParse(bits[1], out var tile)) return;
            float days = float.TryParse(bits[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 3f;
            var v = SeasonAway.Send(Pilgrimage.AwayKey, hc.incident, who, null, hc.faction, tile, (bits[0] == "relic" ? "relic:" : "") + hc.placeName, days, hc.map);
            if (v == null) return;
            PeopleEventsBook.Gave(Pilgrimage.GiftKind, who);
            // The file goes on its way at the same time, toward the same road.
            Arrivals.SendAway(PeoplesG.Party(hc));
        }
    }

    /// <summary>The pilgrimage's season: the letter at the shrine, and what they bring home.</summary>
    public class PilgrimAway : AwayKind
    {
        public override string Key => Pilgrimage.AwayKey;

        private static string Shrine(string place) => place != null && place.StartsWith("relic:", StringComparison.Ordinal) ? place.Substring(6) : place;

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            Tell.Letter(NewsKind.World, Importance.Notable, home, "CC_PEv2_ShrineLabel".Translate(p.LabelShort).Resolve(),
                "CC_PEv2_Shrine".Translate(p.Named("PAWN"), Shrine(place).Named("PLACE"), (f?.Name ?? "?").Named("FACTION"), Mathf.RoundToInt(SeasonAway.DaysOf(v)).Named("DAYS")).Resolve(),
                LetterDefOf.NeutralEvent, LookTargets.Invalid, f);
        }

        public override void Home(Visit v, Map home, Faction f, bool served)
        {
            var p = v.x;
            string place = SeasonAway.PlaceOf(v);
            bool relic = place != null && place.StartsWith("relic:", StringComparison.Ordinal);
            place = Shrine(place);
            var things = new List<string>();
            // The shell, and with Relics and Artifacts a token of the relic's shrine: set down at their feet as they come in.
            var shell = Pilgrimage.ShellDef;
            if (shell != null && served && p.Spawned)
            {
                var t = ThingMaker.MakeThing(shell);
                if (GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near)) things.Add(t.LabelNoCount);
            }
            if (relic && served && p.Spawned)
            {
                var token = PeopleEvents2Math.RelicTokens.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).RandomElementWithFallback();
                if (token != null)
                {
                    var t = ThingMaker.MakeThing(token, token.MadeFromStuff ? GenStuff.DefaultStuffFor(token) : null);
                    if (GenPlace.TryPlaceThing(t, p.Position, p.Map, ThingPlaceMode.Near)) things.Add(t.LabelNoCount);
                }
            }
            string eased = null;
            if (served)
            {
                // A lasting memory of the shrine; faith steadied; the heaviest grief or guilt eased.
                var memory = Pilgrimage.Memory;
                if (memory != null) CouncilGameComponent.GiveThought(p, memory);
                Faiths.Steady(p, PeopleEvents2Math.PilgrimCertainty);
                var burden = Pilgrimage.Burden(p, out _);
                if (burden != null)
                {
                    eased = burden.LabelCap;
                    burden.age = PeopleEvents2Math.EasedAge(burden.age, burden.DurationTicks);
                }
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents2Math.PilgrimGoodwill);
                FactionMemory.Note(f, "gift", p.LabelShort);
            }
            string text = (served ? eased != null ? "CC_PEv2_PilgrimHomeEased" : "CC_PEv2_PilgrimHome" : "CC_PEv2_PilgrimHomeEarly").Translate(p.Named("PAWN"), place.Named("PLACE"),
                TextUtil.JoinAnd(things).Named("THINGS"), (eased ?? "").Named("EASED"), (f?.Name ?? "?").Named("FACTION")).Resolve();
            Tell.Message(NewsKind.People, Importance.Notable, home, text, p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            if (served) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, "CC_PEv2_PilgrimChronicle".Translate(p.Named("PAWN"), place.Named("PLACE")).Resolve(), p));
            // The pilgrims' cough, caught on the crowded road: through the harm budget, and shown.
            var cold = DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_HeadCold");
            if (cold != null && AilmentTracker.On && p.Spawned && Harm.Roll(p, HarmKinds.PilgrimCough, PeopleEvents2Math.PilgrimCough, HurtClass.Mild)
                && Harm.Ail(p, cold, Mathf.Max(0.1f, cold.initialSeverity), HarmKinds.PilgrimCough, HurtClass.Mild) != null)
                Tell.Message(NewsKind.Health, Importance.Notable, home, "CC_PEv2_PilgrimCough".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
        }
    }
}
