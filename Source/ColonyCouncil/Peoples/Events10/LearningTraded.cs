using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 99. Learning traded through the colony (the learned society and the Sunken Vault) =====================

    /// <summary>
    /// Learning traded through the colony (item 99): a copyist of the learned society and a technician of the Sunken Vault meet at the
    /// colony's table, hosted three days. Each morning the copyist copies the vault's records there four hours with the technician beside,
    /// reading them out; each afternoon the colony's best crafter teaches them both two hours at the colony's bench. They eat at the
    /// colony's table and sleep in its beds (else on the floor), as hosted guests do (Missions/Hosting.cs). When they go, the society has
    /// its copy (whole after three mornings, and then a book of it left for the colony) and the vault its craft learned again (supply that
    /// holds its age, round G's); both peoples remember one of theirs who learned at the colony, which moves their ages by the teaching
    /// rule (Peoples/PeopleAges.cs). Or the colony hosts them for the copying only, the crafter kept at their own work. One of the events'
    /// visits (kind "pev:learning"): x the copyist, group the technician, a the crafter, place the table, stake the mornings copied, due the
    /// afternoons taught, text "society|vault|teach or copy|the tick they began|the map's day of the last copying|of the last lesson".
    /// </summary>
    public static class LearningTraded
    {
        public const string Kind = "pev:learning";

        private static int Now => Find.TickManager.TicksGame;

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        private static int Int(Visit v, int i, int otherwise = -1) => int.TryParse(Parts(v).ElementAtOrDefault(i), out int n) ? n : otherwise;

        private static void SetInt(Visit v, int i, int value)
        {
            var p = Parts(v).ToList();
            while (p.Count <= i) p.Add("");
            p[i] = value.ToString();
            v.text = string.Join("|", p);
        }

        public static Faction Society(Visit v) => PeopleEvents10.PeopleById(Parts(v).ElementAtOrDefault(0));

        public static Faction Vault(Visit v) => PeopleEvents10.PeopleById(Parts(v).ElementAtOrDefault(1));

        public static bool Teaching(Visit v) => Parts(v).ElementAtOrDefault(2) == "teach";

        public static Visit Open() => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done);

        /// <summary>The colony's best crafter who could teach.</summary>
        public static Pawn Crafter(Map map) => PeoplesD.Best(map, SkillDefOf.Crafting, p => !p.WorkTagIsDisabled(WorkTags.Crafting) && !p.InMentalState);

        /// <summary>The colony's table where they sit (the one nearest the gate), or null.</summary>
        public static Thing Table(Map map, Pawn by) => by == null ? null : ConsoleTalk.Table(map, by) is var t && t.HasThing ? t.Thing : null;

        /// <summary>The answer taken: they stay three days at the table, a lord of their own each there for the stay.</summary>
        public static void Begin(HappeningContext ctx, bool teach)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var tech = ctx?.group?.FirstOrDefault(p => p != null && p.Faction == ctx.enemy);
            if (map == null || t == null || ctx.faction == null || ctx.enemy == null || ctx.stranger == null) return;
            // Three mornings at the table (and three afternoons at the bench): a day more at most, should they come in late in the day.
            int stay = (PeopleEvents10Math.HostDays + 1) * GenDate.TicksPerDay + 12 * GenDate.TicksPerHour;
            var v = t.AddVisit(Kind, ctx.incident, map, ctx.stranger, ctx.a, tech != null ? new List<Pawn> { tech } : null, Now + stay);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + ctx.enemy.GetUniqueLoadID() + "|" + (teach ? "teach" : "copy") + "|" + Now + "|-1|-1";
            v.place = ctx.place ?? Table(map, ctx.a);
            v.stake = 0;
            v.due = 0;
            if (!PlayOuts.On)
            {
                v.stake = PeopleEvents10Math.HostDays;
                if (teach) v.due = PeopleEvents10Math.HostDays;
                Finish(v);
                return;
            }
            var spot = v.place != null && v.place.Spawned ? v.place.Position : PeoplesD.GateSpot(map) ?? ctx.stranger.Position;
            foreach (var p in PeopleEvents10.Up(v))
            {
                try
                {
                    Lords.Leave(p);
                    LordMaker.MakeNewLord(p.Faction, new LordJob_VisitOnErrand(p.Faction, spot, stay), map, new List<Pawn> { p });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 10: the stay at the table", e);
                }
            }
            PeoplesG.SayLine(ctx.stranger, ctx.incident, "stay");
        }

        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var map = v.map;
            var here = PeopleEvents10.Up(v);
            int day = map == null ? -1 : GenLocalDate.Year(map) * GenDate.DaysPerYear + GenLocalDate.DayOfYear(map);
            bool all = PeopleEvents10Math.CopyWhole(v.stake) && (!Teaching(v) || v.due >= PeopleEvents10Math.HostDays);
            if (map == null || here.Count == 0 || now >= v.until || all)
            {
                Finish(v);
                return false;
            }
            Tend(v, map);
            if (MapFacts.ThreatOn(map)) return true;
            int hour = GenLocalDate.HourOfDay(map);
            var copyist = v.x;
            var tech = v.group.FirstOrDefault();
            bool copying = copyist != null && copyist.CurJobDef == PeopleEvents10.CopyJob;
            // The morning's copying: the copyist at the table, the technician beside reading the records out.
            if (PeopleEvents10Math.CopyHour(hour) && v.stake < PeopleEvents10Math.HostDays && Int(v, 4) != day && !copying && PeoplesG.Up(copyist, map) && v.place != null && v.place.Spawned
                && PeopleEvents10.Send(copyist, PeopleEvents10.CopyJob, v.place, PeopleEvents10Math.CopyTicks))
            {
                SetInt(v, 4, day);
                if (PeoplesG.Up(tech, map)) PeopleEvents10.Send(tech, PeopleEvents10.ReadOutJob, v.place, PeopleEvents10Math.CopyTicks);
                PeoplesG.SayLine(tech, v.incident, "readout");
            }
            // The afternoon's lesson at the crafter's bench.
            if (Teaching(v) && PeopleEvents10Math.LessonHour(hour) && v.due < PeopleEvents10Math.HostDays && Int(v, 5) != day && !copying)
            {
                var crafter = v.a;
                var bench = WanderYear.Bench(map, crafter);
                if (bench != null && PeoplesG.Up(crafter, map) && !crafter.Drafted && crafter.CurJobDef != PeopleEvents10.TeachJob
                    && PeopleEvents10.Send(crafter, PeopleEvents10.TeachJob, bench, PeopleEvents10Math.LessonTicks))
                {
                    SetInt(v, 5, day);
                    foreach (var p in here) PeopleEvents10.Send(p, PeopleEvents10.LearnJob, bench, PeopleEvents10Math.LessonTicks);
                    PeoplesG.SayLine(copyist, v.incident, "lesson");
                }
            }
            return true;
        }

        /// <summary>
        /// The hosted guests' needs, as a feast's guests are seen to: the hungry eat the colony's meals at its table (else their packs), and
        /// at night each lies down in a bed of the colony's they may have (else on the floor) and gets up once rested.
        /// </summary>
        private static void Tend(Visit v, Map map)
        {
            bool night = GenLocalDate.HourOfDay(map) >= 22 || GenLocalDate.HourOfDay(map) < 6;
            foreach (var x in PeopleEvents10.Up(v))
            {
                if (x.CurJobDef == PeopleEvents10.CopyJob || x.CurJobDef == PeopleEvents10.ReadOutJob || x.CurJobDef == PeopleEvents10.LearnJob) continue;
                float food = x.needs?.food?.CurLevelPercentage ?? 1f;
                bool sleepy = night && Hosting.WantsSleep(x);
                if (Hosting.LyingAwake(x) && !sleepy)
                {
                    Hosting.GetUp(x);
                    continue;
                }
                if (!Hosting.Free(x)) continue;
                if (food < Hosting.EatBelow || sleepy && food < Hosting.SupperBelow)
                {
                    var meal = Hosting.Meal(x, false) ?? Hosting.Raw(x);
                    if (meal != null && Hosting.Eat(x, meal, null)) continue;
                    if (food < Hosting.PackBelow || sleepy)
                    {
                        var pack = Hosting.Pack(x);
                        if (pack != null && Hosting.Eat(x, pack, null)) continue;
                    }
                }
                if (!sleepy) continue;
                bool Ours(Pawn o) => o == x || v.group.Contains(o) || o == v.x;
                var bed = x.ownership?.OwnedBed is Building_Bed owned && owned.Spawned && owned.Map == map
                    ? owned
                    : Hosting.Beds(map, Ours, x).OrderBy(b => b.Position.DistanceToSquared(x.Position))
                        .FirstOrDefault(b => Hosting.CanShare(b, x) && (b.OwnersForReading.Contains(x) || b.OwnersForReading.Count < b.SleepingSlotsCount));
                if (bed != null && Hosting.Sleep(x, bed)) continue;
                Hosting.SleepRough(x);
            }
        }

        /// <summary>A morning's copying done (round 9's work at a place): counted and told.</summary>
        public static void Copied(Pawn copyist)
        {
            var v = PeopleEvents10.Of(Kind, copyist);
            if (v == null) return;
            v.stake++;
            Tell.Message(NewsKind.People, Importance.Flavor, v.map,
                "CC_PEv10_Copied".Translate(copyist.Named("COPYIST"), (Vault(v)?.Name ?? "?").Named("VAULT"), Mathf.Min(v.stake, PeopleEvents10Math.HostDays).Named("COUNT"),
                    PeopleEvents10Math.HostDays.Named("OF")).Resolve(), new LookTargets(copyist), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>An afternoon's lesson done: the crafter the better teacher, the visitors at the bench the better at it.</summary>
        public static void Taught(Pawn crafter)
        {
            var v = PeopleEvents10.OfColonist(Kind, crafter);
            if (v == null) return;
            v.due++;
            crafter.skills?.Learn(SkillDefOf.Social, PeopleEvents10Math.LessonSocialXp, true);
            crafter.skills?.Learn(SkillDefOf.Crafting, PeopleEvents10Math.LessonCraftXp, true);
            foreach (var p in PeopleEvents10.Up(v))
                if (p.Position.InHorDistOf(crafter.Position, 12f))
                    p.skills?.Learn(SkillDefOf.Crafting, PeopleEvents10Math.VisitorCraftXp, true);
            Tell.Message(NewsKind.People, Importance.Flavor, v.map,
                "CC_PEv10_Taught".Translate(crafter.Named("CRAFTER"), (Society(v)?.Name ?? "?").Named("SOCIETY"), (Vault(v)?.Name ?? "?").Named("VAULT")).Resolve(),
                new LookTargets(crafter), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// Their stay over: the society's copy (and the book for the colony when it is whole), the vault's craft as supply, both peoples'
        /// memory of one of theirs who learned here, their thanks, and home.
        /// </summary>
        public static void Finish(Visit v)
        {
            v.done = true;
            var map = v.map;
            var society = Society(v);
            var vault = Vault(v);
            var copyist = v.x;
            var tech = v.group.FirstOrDefault();
            int mornings = v.stake, lessons = v.due;
            Thing book = null;
            if (PeopleEvents10Math.CopyWhole(mornings) && map != null)
            {
                var def = HappeningWorker_ScholarCopyist.Book();
                try
                {
                    book = def == null ? null : ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                    if (book != null)
                    {
                        book.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
                        var at = v.place != null && v.place.Spawned ? v.place.Position : copyist != null && copyist.Spawned ? copyist.Position : PeoplesD.GateSpot(map) ?? map.Center;
                        GenPlace.TryPlaceThing(book, at, map, ThingPlaceMode.Near);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 10: the copy's book", e);
                    book = null;
                }
            }
            if (lessons > 0)
            {
                if (society != null) FactionMemory.Note(society, "learnedHere", copyist?.LabelShort);
                if (vault != null)
                {
                    FactionMemory.Note(vault, "learnedHere", tech?.LabelShort);
                    VaultSupply.Noted(vault, PeopleEvents10Math.CraftWorth, "CC_PEv10_CraftWhat".Translate((v.a?.LabelShort ?? "?").Named("CRAFTER")).Resolve());
                }
            }
            int thanks = lessons > 0 ? PeopleEvents10Math.LearnGoodwill : mornings > 0 ? PeopleEvents10Math.CopyGoodwill : 0;
            PeopleEvents10.Goodwill(society, thanks, GoodwillWhy.Welcome);
            PeopleEvents10.Goodwill(vault, thanks, GoodwillWhy.Welcome);
            if (map != null)
            {
                PeoplesG.SayLine(copyist, v.incident, "thanks");
                string key = book != null ? lessons > 0 ? "CC_PEv10_LearnDone" : "CC_PEv10_CopyDone" : mornings > 0 ? "CC_PEv10_CopyPart" : "CC_PEv10_LearnNone";
                Tell.Message(NewsKind.People, Importance.Notable, map,
                    key.Translate((society?.Name ?? "?").Named("SOCIETY"), (vault?.Name ?? "?").Named("VAULT"), (book?.def?.label ?? "").Named("BOOK"), lessons.Named("COUNT"),
                        (v.a?.LabelShort ?? "?").Named("CRAFTER")).Resolve(), book != null ? new LookTargets(book) : LookTargets.Invalid,
                    book != null || lessons > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                foreach (var p in PeopleEvents10.Up(v)) Hosting.GiveBack(p, map);
            }
            PeoplesG.Home(PeopleEvents10.Up(v));
        }
    }

    /// <summary>
    /// A copyist of the learned society ({FACTION}, {X}) and a technician of the Sunken Vault ({ENEMY}) at the gate together, asking to meet
    /// at the colony's table for three days: the copyist to copy the vault's records, both to be taught by {A}, the colony's crafter.
    /// </summary>
    public class HappeningWorker_LearningTraded : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeopleEvents10.Pair(PeoplesMath.Scholars, PeoplesMath.Vaultfolk, out _) != null && LearningTraded.Open() == null && LearningTraded.Crafter(map) is Pawn c
            && LearningTraded.Table(map, c) != null && WanderYear.Bench(map, c) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeopleEvents10.Pair(PeoplesMath.Scholars, PeoplesMath.Vaultfolk, out var vault);
            ctx.enemy = vault;
            if (ctx.faction == null || ctx.enemy == null) return false;
            ctx.a = LearningTraded.Crafter(ctx.map);
            ctx.place = LearningTraded.Table(ctx.map, ctx.a);
            if (ctx.a == null || ctx.place == null) return false;
            ctx.tale = ctx.place.def.label;
            ctx.thing = HappeningWorker_ScholarCopyist.Book();
            ctx.count = PeopleEvents10Math.HostDays;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Scholars_Fellow"), ctx.faction, ext.ages);
            var tech = Arrivals.Make(PeoplesD.KindNamed(ctx.enemy, "Rimfolk_SunkenVault_Technician"), ctx.enemy, ext.ages);
            if (ctx.stranger == null || tech == null) return false;
            ctx.group.Add(tech);
            ctx.placeName = LivingEvents.TownOf(ctx.enemy, ctx.map) ?? ctx.enemy.Name;
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
            if (PeopleEvents.AgeOf(ctx.enemy) < 3) tags.Add("fallen");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.place == null || ctx.place.Destroyed || !ctx.place.Spawned) return "CC_PEv10_TableGone".Translate().Resolve();
            if (option == 0 && (ctx.a == null || ctx.a.Dead || ctx.a.MapHeld != ctx.map || WanderYear.Bench(ctx.map, ctx.a) == null)) return "CC_PEv10_CrafterGone".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault && option > 1) PeoplesG.Say(ctx.stranger, Ext, ctx, "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>Their three days at the colony's table: with <see cref="teach"/>, the crafter's afternoons at the bench too.</summary>
    public class DilemmaEffect_LearningTraded : DilemmaEffect
    {
        public bool teach = true;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.faction == null || hc.enemy == null) return;
            sheet.Play("CC_PEv10_Impact_Host".Translate(hc.tale ?? "?", PeopleEvents10Math.HostDays).Resolve(), "CC_PEv10_Impact_HostBrief".Translate().Resolve());
            if (hc.thing != null)
                sheet.Add(ImpactKind.Goods, 1, "CC_PEv10_Impact_Book".Translate(hc.thing.label).Resolve(), "CC_PEv10_Impact_BookBrief".Translate().Resolve(), "pev10book");
            if (teach && hc.a != null)
            {
                sheet.Play("CC_PEv10_Impact_Lessons".Translate(hc.a.LabelShort).Resolve(), "CC_PEv10_Impact_LessonsBrief".Translate().Resolve());
                sheet.Add(ImpactKind.Skill, 1, "CC_PEv10_Impact_TeachSocial".Translate(hc.a.LabelShort).Resolve(), "CC_PEv10_Impact_TeachSocialBrief".Translate().Resolve(), "pev10teach");
                sheet.Add(ImpactKind.Other, 1, "CC_PEv10_Impact_Ages".Translate(hc.faction.Name, hc.enemy.Name).Resolve(), "CC_PEv10_Impact_AgesBrief".Translate().Resolve(), "pev10ages");
            }
            int thanks = teach ? PeopleEvents10Math.LearnGoodwill : PeopleEvents10Math.CopyGoodwill;
            sheet.Goodwill(hc.faction, thanks);
            sheet.Goodwill(hc.enemy, thanks);
        }

        public override void Apply(DilemmaContext ctx) => LearningTraded.Begin(ctx as HappeningContext, teach);
    }
}
