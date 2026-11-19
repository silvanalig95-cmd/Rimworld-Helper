using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 98. The lamp and the bell in a plague year (the hospitaller order and the Quiet Bell) =====================

    /// <summary>
    /// The lamp and the bell in a plague year (item 98): a town within reach lies under a plague (the region's world event, as the
    /// illness budget reads a town's sickness), and a hospitaller of the order and a sexton of the Quiet Bell come to the gate together:
    /// the order asks for the colony's doctor for its sickhouse there, the Bell for two of its diggers for the dead. Each colonist sent goes
    /// as a caravan of one (round 2's season away: the game's own roads there and back) and stays four to six days, the doctor on the
    /// order's wards and the diggers at the Bell's graves, each learning. The sickness may come home with them: each is lodged at the
    /// town for the illness budget's own sickness brought home (Harm/SickHome.cs, its chance and its letter). Home again, the order thinks
    /// better of the colony for its doctor and the Bell for its diggers, the plague's world event counts the colony's help, and sending
    /// both is remembered by both peoples as a lasting deed (<see cref="PeopleEvents10Math.PlagueDeed"/>). Each one away is a season away
    /// (kind "pev:away", key "plague", its extra "doctor" or "digger", with ":both" when both were sent).
    /// </summary>
    public static class PlagueYear
    {
        public const string EventDef = "Rimfolk_Evt_PlagueYear";
        public const string AwayKey = "plague";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>A running world event of plague that reaches this town (the illness budget's own reading).</summary>
        public static WorldSituation PlagueOf(Settlement s)
        {
            var ws = WorldSituations.Instance;
            if (s?.Faction == null || ws == null) return null;
            foreach (var w in ws.Running)
            {
                if (w?.def == null || !w.def.bad) continue;
                if (HarmTwoMath.KindOf(w.def.kind, w.def.defName == "Council_WS_Murrain") != SickTown.Plague) continue;
                if (HarmTwoMath.Reaches(w.ctx?.faction == s.Faction, SickHome.TilesTo(w, s))) return w;
            }
            return null;
        }

        /// <summary>The plague town nearest the home within reach: a town of a people at peace with the colony, under a plague.</summary>
        public static Settlement Town(Map map)
        {
            if (map == null || !map.Tile.Valid) return null;
            return Find.WorldObjects.Settlements
                .Where(s => s != null && !s.Destroyed && s.Faction != null && !s.Faction.IsPlayer && !s.Faction.HostileTo(Faction.OfPlayer) && s.Tile.Valid && s.Tile.Layer == map.Tile.Layer)
                .Select(s => (s, d: Find.WorldGrid.ApproxDistanceInTiles(map.Tile, s.Tile)))
                .Where(x => x.d <= PeopleEvents10Math.PlagueTiles && SickHome.TownSickness(x.s) == SickTown.Plague)
                .OrderBy(x => x.d).Select(x => x.s).FirstOrDefault();
        }

        /// <summary>The colony's best doctor who could go.</summary>
        public static Pawn Doctor(Map map) => PeoplesD.Best(map, SkillDefOf.Medicine, p => PeopleEvents2.MayLeave(p, map) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor));

        /// <summary>The colony's two best diggers who could go (Construction: the game's graves are dug as building), never the doctor.</summary>
        public static List<Pawn> Diggers(Map map, Pawn not) =>
            PeoplesD.Able(map).Where(p => p != not && PeopleEvents2.MayLeave(p, map) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0).Take(PeopleEvents10Math.Diggers).ToList();

        /// <summary>
        /// These may go now: never the colony's last two grown-ups (MayLeave), and at home at least one grown-up more than go.
        /// </summary>
        public static bool MayGo(List<Pawn> going, Map map) =>
            going.Count > 0 && going.All(p => PeopleEvents2.MayLeave(p, map)) && PeoplesD.Able(map).Count(x => !x.DevelopmentalStage.Juvenile()) > going.Count + 1;

        /// <summary>The answer taken: the doctor (and/or the diggers) set out for the plague town, each a caravan of one, for the same days.</summary>
        public static void Send(HappeningContext ctx, bool doctor, bool diggers)
        {
            var map = ctx?.map;
            var town = Find.WorldObjects.Settlements.FirstOrDefault(s => s != null && s.Label == ctx?.placeName) ?? Town(map);
            if (map == null || town == null) return;
            int days = ctx.variant > 0 ? ctx.variant : PeopleEvents10Math.PlagueDays(Rand.Value);
            var inc = ctx.incident ?? DefDatabase<IncidentDef>.GetNamedSilentFail(EventDef);
            string both = doctor && diggers ? ":both" : "";
            var went = new List<Pawn>();
            if (doctor && ctx.a != null && SeasonAway.Send(AwayKey, inc, ctx.a, null, ctx.faction, town.Tile, town.Label, days, map, "doctor" + both) != null) went.Add(ctx.a);
            if (diggers)
                foreach (var p in Pair(ctx))
                    if (SeasonAway.Send(AwayKey, inc, p, null, ctx.enemy, town.Tile, town.Label, days, map, "digger" + both) != null)
                        went.Add(p);
            if (went.Count == 0) return;
            if (doctor) PeoplesG.SayLine(Hospitaller(ctx), ctx.incident, "lamp");
            if (diggers) PeoplesG.SayLine(Sexton(ctx), ctx.incident, "bell");
            PeoplesG.Home(PeoplesG.Party(ctx));
        }

        /// <summary>The two diggers named on the card (kept in the event's group after its visitors).</summary>
        public static List<Pawn> Pair(HappeningContext ctx) =>
            new[] { ctx?.b, ctx?.pet }.Where(p => p != null && p.Faction == Faction.OfPlayer).ToList();

        public static Pawn Hospitaller(HappeningContext ctx) => ctx?.stranger;

        public static Pawn Sexton(HappeningContext ctx) => ctx?.group?.FirstOrDefault(p => p != null && p.Faction == ctx.enemy);

        /// <summary>The seasons at a plague town under way now.</summary>
        private static IEnumerable<Visit> There() =>
            CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && v.kind == SeasonAway.Kind && !v.done && SeasonAway.KeyOf(v) == AwayKey && v.stake == PeopleEvents2Math.There)
            ?? Enumerable.Empty<Visit>();

        /// <summary>The events' hour: each day they are there ends with what they learned on the wards or at the graves.</summary>
        public static void Days(int now)
        {
            foreach (var v in There().ToList())
            {
                var p = v.x;
                if (p == null || p.Dead || v.due < 0) continue;
                int arrived = v.due - Mathf.RoundToInt(SeasonAway.DaysOf(v) * GenDate.TicksPerDay);
                int since = now - arrived;
                if (since < GenDate.TicksPerDay || since % GenDate.TicksPerDay >= GenDate.TicksPerHour) continue;
                bool doctor = (SeasonAway.ExtraOf(v) ?? "").StartsWith("doctor");
                try
                {
                    p.skills?.Learn(doctor ? SkillDefOf.Medicine : SkillDefOf.Construction, doctor ? PeopleEvents10Math.DoctorXpPerDay : PeopleEvents10Math.DiggerXpPerDay, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 10: a day at the plague town", e);
                }
            }
        }
    }

    /// <summary>
    /// A season at a plague town: as they get there, the word and their lodging for the illness budget; at home, their people's thanks,
    /// the world event's count of the colony's help, and with both sent, the lasting deed in both peoples' memories.
    /// </summary>
    public class PlagueAway : AwayKind
    {
        public override string Key => PlagueYear.AwayKey;

        private static bool Doctor(Visit v) => (SeasonAway.ExtraOf(v) ?? "").StartsWith("doctor");

        private static bool Both(Visit v) => (SeasonAway.ExtraOf(v) ?? "").EndsWith(":both");

        private static Settlement TownOf(Visit v)
        {
            var tile = SeasonAway.TileOf(v);
            return tile.Valid ? Find.WorldObjects.SettlementAt(tile) : null;
        }

        public override void Arrived(Visit v, Map home, Faction f, string place)
        {
            var p = v.x;
            Tell.Message(NewsKind.World, Importance.Notable, home,
                (Doctor(v) ? "CC_PEv10_PlagueThereDoctor" : "CC_PEv10_PlagueThereDigger").Translate(p.Named("PAWN"), place.Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            // Lodged at the plague town for their days: the illness budget's own roll when they are home (Harm/SickHome.cs).
            var town = TownOf(v);
            if (town != null)
                SickHome.Lodged(p, town, SickTown.Plague, PeopleEvents10Math.LodgedHours(SeasonAway.DaysOf(v)), Doctor(v) && Peoples.TendsLodgers(f));
        }

        public override void Home(Visit v, Map home, Faction f, bool served)
        {
            var p = v.x;
            bool doctor = Doctor(v);
            if (served)
            {
                PeopleEvents10.Thought(p, "Rimfolk_Thought_PlagueTown");
                PeopleEvents10.Goodwill(f, doctor ? PeopleEvents10Math.DoctorGoodwill : PeopleEvents10Math.DiggerGoodwill, GoodwillWhy.Helped);
                // The plague's world event counts the colony's help: its people remember it when it ends (FactionMemory's hardship).
                var w = PlagueYear.PlagueOf(TownOf(v));
                if (w != null)
                {
                    w.help++;
                    w.deeds.Add("CC_PEv10_PlagueDeedText".Translate(p.Named("PAWN"), SeasonAway.PlaceOf(v).Named("PLACE")).Resolve());
                }
                if (Both(v) && f != null && !(FactionMemory.Instance?.Of(f).Any(d => d.kind == PeopleEvents10Math.PlagueDeed && PeopleEvents10.Now - d.tick < 30 * GenDate.TicksPerDay) ?? true))
                    KinMemory.Note(f, PeopleEvents10Math.PlagueDeed, p, SeasonAway.PlaceOf(v));
            }
            Tell.Message(NewsKind.World, Importance.Notable, home,
                (served ? doctor ? "CC_PEv10_PlagueHomeDoctor" : "CC_PEv10_PlagueHomeDigger" : "CC_PEv10_PlagueHomeEarly")
                .Translate(p.Named("PAWN"), SeasonAway.PlaceOf(v).Named("PLACE"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                p.Spawned ? new LookTargets(p) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>
    /// A hospitaller of the order ({FACTION}, {X}) and a sexton of the Quiet Bell ({ENEMY}) at the gate together: {PLACE} lies under a
    /// plague; the order asks for {A}, the colony's doctor, and the Bell for {B} and {PET}, two diggers, for {COUNT} days.
    /// </summary>
    public class HappeningWorker_PlagueYear : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeopleEvents10.Pair(PeoplesMath.Healers, PeoplesMath.Sextons, out _) != null && PlagueYear.Town(map) != null && PlagueYear.Doctor(map) != null
            && PlagueYear.Diggers(map, PlagueYear.Doctor(map)).Count == PeopleEvents10Math.Diggers && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeopleEvents10.Pair(PeoplesMath.Healers, PeoplesMath.Sextons, out var bell);
            ctx.enemy = bell;
            var town = PlagueYear.Town(ctx.map);
            if (ctx.faction == null || ctx.enemy == null || town == null) return false;
            ctx.a = PlagueYear.Doctor(ctx.map);
            var diggers = PlagueYear.Diggers(ctx.map, ctx.a);
            if (ctx.a == null || diggers.Count < PeopleEvents10Math.Diggers) return false;
            ctx.b = diggers[0];
            // {PET} names the second digger (the event has no beast of its own).
            ctx.pet = diggers[1];
            ctx.placeName = town.Label;
            ctx.variant = PeopleEvents10Math.PlagueDays(Rand.Value);
            ctx.count = ctx.variant;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Order_Healer"), ctx.faction, ext.ages);
            var sexton = Arrivals.Make(PeoplesD.KindNamed(ctx.enemy, "Rimfolk_QuietBell_Sexton"), ctx.enemy, ext.ages);
            if (ctx.stranger == null || sexton == null) return false;
            ctx.group.Add(sexton);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var gate = PeoplesD.GateSpot(ctx.map);
            float hours = ext.stayHours.RandomInRange;
            return Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, gate, hours) != null
                   && Arrivals.Visit(ctx.group.Where(p => p != null && p.Faction == ctx.enemy).ToList(), ctx.map, ctx.enemy, gate, hours) != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (Ages.Ours >= WorldAge.Industrial) tags.Add("modern");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var doctor = new List<Pawn> { ctx.a };
            var diggers = PlagueYear.Pair(ctx);
            switch (option)
            {
                case 0:
                    if (diggers.Count < PeopleEvents10Math.Diggers || !PlagueYear.MayGo(doctor.Concat(diggers).ToList(), ctx.map)) return "CC_PEv10_PlagueCantGo".Translate().Resolve();
                    break;
                case 1:
                    if (ctx.a == null || !PlagueYear.MayGo(doctor, ctx.map)) return "CC_PEv10_PlagueCantGo".Translate().Resolve();
                    break;
                case 2:
                    if (diggers.Count < PeopleEvents10Math.Diggers || !PlagueYear.MayGo(diggers, ctx.map)) return "CC_PEv10_PlagueCantGo".Translate().Resolve();
                    break;
            }
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault && option > 2) PeoplesG.Say(ctx.stranger, Ext, ctx, "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>Who goes to the plague town: <see cref="doctor"/> the colony's doctor with the order, <see cref="diggers"/> two diggers with the Bell.</summary>
    public class DilemmaEffect_PlagueYear : DilemmaEffect
    {
        public bool doctor, diggers;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.faction == null || hc.enemy == null) return;
            var going = new List<string>();
            if (doctor) going.Add(hc.a.LabelShort);
            if (diggers) going.AddRange(PlagueYear.Pair(hc).Select(p => p.LabelShort));
            string who = going.ToCommaList(true);
            sheet.Play("CC_PEv10_Impact_Plague".Translate(who, hc.placeName ?? "?", hc.variant).Resolve(), "CC_PEv10_Impact_PlagueBrief".Translate().Resolve());
            if (doctor)
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv10_Impact_PlagueDoctor".Translate(hc.a.LabelShort).Resolve(), "CC_PEv10_Impact_PlagueDoctorBrief".Translate().Resolve(), "pev10plaguedoc");
            if (diggers)
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv10_Impact_PlagueDiggers".Translate(string.Join(", ", PlagueYear.Pair(hc).Select(p => p.LabelShort))).Resolve(),
                    "CC_PEv10_Impact_PlagueDiggersBrief".Translate().Resolve(), "pev10plaguedig");
            if (Harm.On)
                sheet.Add(ImpactKind.Health, -1, "CC_PEv10_Impact_PlagueRisk".Translate(who).Resolve(), "CC_PEv10_Impact_PlagueRiskBrief".Translate().Resolve(), "pev10plaguerisk");
            if (doctor) sheet.Goodwill(hc.faction, PeopleEvents10Math.DoctorGoodwill);
            if (diggers) sheet.Goodwill(hc.enemy, PeopleEvents10Math.DiggerGoodwill * PeopleEvents10Math.Diggers);
            if (doctor && diggers)
                sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv10_Impact_PlagueDeed".Translate(hc.faction.Name, hc.enemy.Name).Resolve(), "CC_PEv10_Impact_PlagueDeedBrief".Translate().Resolve(), "pev10plaguedeed");
        }

        public override void Apply(DilemmaContext ctx) => PlagueYear.Send(ctx as HappeningContext, doctor, diggers);
    }
}
