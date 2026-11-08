using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Release G's Choir of the Spire and Sunken Vault at the colony's gate (docs/design/peoples.md, "Release G"; the defs in
    // Defs/IncidentDefs/Rimfolk_Events_PeoplesG.xml and Defs/MissionDefs/Rimfolk_Missions_PeoplesG.xml): the Choir's
    // listening (a calm for some, unease for others, shown as moods), its psychic and archotech goods for the colony's art,
    // and its listener's stay to learn what it is to be ordinary; the Vault's barter of its last spacer goods for
    // components and food, the craft it lost, and the vault-born child who has never seen a sky. The Vault declines an age
    // every few years unless supplied (the age rule, Peoples/PeopleAgesMath.cs: ageDeclines in its def); what the colony
    // gives it here is remembered as supply (VaultSupply.Noted, the peoples' memory the rule reads).

    // ===================== The Choir of the Spire: the listening =====================

    /// <summary>
    /// The Choir's listening: its listeners sit in a ring in the colony's middle, a while, and then everyone at home feels
    /// it (PeoplesGMath.Felt): a calm for most, an unease for some, more for the psychically sensitive and more still when
    /// the void stirs; the psychically deaf feel nothing. Shown as moods (Rimfolk_Thought_ChoirCalm, _ChoirUnease), told.
    /// </summary>
    public static class ChoirListening
    {
        private sealed class Sitting
        {
            public Map map;
            public Faction f;
            public Pawn with;
            public IncidentDef incident;
            public List<Pawn> listeners = new List<Pawn>();
        }

        /// <summary>The sittings under way this session, by the first listener's id (one loaded halfway is felt by nobody; they go home on their hours).</summary>
        private static readonly Dictionary<int, Sitting> sittings = new Dictionary<int, Sitting>();

        public static ThoughtDef CalmDef => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_ChoirCalm");

        public static ThoughtDef UneaseDef => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_ChoirUnease");

        /// <summary>The void stirs (Anomaly: the monolith has woken); the Choir know it, and the unease comes more often.</summary>
        public static bool VoidStirs()
        {
            try
            {
                return ModsConfig.AnomalyActive && Find.Anomaly != null && Find.Anomaly.Level > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The colony's middle, where they sit: its gathering spot, else its middle.</summary>
        public static IntVec3 Middle(Map map)
        {
            var spot = map.listerBuildings.allBuildingsColonist.FirstOrDefault(b => b.def.defName == "PartySpot" || b.def.defName == "Campfire");
            return spot != null ? spot.Position : CampSite.ColonyCenter(map);
        }

        /// <summary>A ring of standable cells round the middle, one for each who sits.</summary>
        public static List<IntVec3> Ring(Map map, IntVec3 mid, int n)
        {
            var cells = new List<IntVec3>();
            if (map == null || !mid.IsValid) return cells;
            foreach (var c in GenRadial.RadialCellsAround(mid, 4f, false))
            {
                if (cells.Count >= n) break;
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || c.GetFirstBuilding(map) != null || c.DistanceTo(mid) < 1.9f) continue;
                if (cells.Any(o => o.AdjacentTo8WayOrInside(c))) continue;
                cells.Add(c);
            }
            return cells;
        }

        /// <summary>
        /// The sitting begun: the listeners (and, with <paramref name="with"/>, the colony's one who sits with them) each to a
        /// place in the ring, a while there; heard by the colony when the first listener rises (Listened). With the play-outs
        /// off, or nowhere to sit, at once.
        /// </summary>
        public static void Begin(HappeningContext ctx, Pawn with)
        {
            var map = ctx?.map;
            if (map == null) return;
            var listeners = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, map)).ToList();
            var mid = Middle(map);
            var ring = Ring(map, mid, listeners.Count + 1);
            int ticks = PeoplesGMath.ListenHours * GenDate.TicksPerHour;
            if (PlayOuts.On && listeners.Count > 0 && ring.Count >= listeners.Count)
            {
                PeoplesD.Gather(listeners, ctx.faction, mid, PeoplesGMath.ListenHours + 2);
                bool started = false;
                for (int i = 0; i < listeners.Count; i++) started |= PeoplesE.Start(listeners[i], PeoplesG.ListenJob, ring[i], null, ticks);
                if (started)
                {
                    sittings[listeners[0].thingIDNumber] = new Sitting { map = map, f = ctx.faction, with = with, incident = ctx.incident, listeners = listeners };
                    if (with != null && with.Spawned && with.Map == map && !with.Drafted && !with.Downed && !with.InMentalState && ring.Count > listeners.Count)
                        PeoplesE.Start(with, PeoplesG.ListenJob, ring[listeners.Count], null, ticks);
                    return;
                }
            }
            Heard(map, ctx.faction, with, listeners, ctx.incident);
        }

        /// <summary>A sitting's hours done: the first listener rising is the colony hearing it; one of the colony who sat with them comes away the calmer (a psycaster with their focus the fuller).</summary>
        public static void Listened(Pawn pawn)
        {
            if (pawn == null) return;
            if (pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                if (ModsConfig.RoyaltyActive && pawn.psychicEntropy != null && pawn.HasPsylink) pawn.psychicEntropy.OffsetPsyfocusDirectly(0.3f);
                return;
            }
            if (!sittings.TryGetValue(pawn.thingIDNumber, out var s)) return;
            sittings.Remove(pawn.thingIDNumber);
            Heard(s.map, s.f, s.with, s.listeners, s.incident);
        }

        /// <summary>The colony heard: each at home feels the calm or the unease (or nothing), told with who felt which; the listeners go home.</summary>
        public static void Heard(Map map, Faction f, Pawn with, List<Pawn> listeners, IncidentDef incident)
        {
            if (map == null) return;
            bool stirs = VoidStirs();
            var calm = new List<Pawn>();
            var uneasy = new List<Pawn>();
            foreach (var p in IncidentWorker_Happening.Colonists(map))
            {
                int felt = p == with ? 1 : PeoplesGMath.Felt(p.GetStatValue(StatDefOf.PsychicSensitivity), Rand.Value, stirs);
                var t = felt > 0 ? CalmDef : felt < 0 ? UneaseDef : null;
                if (t == null) continue;
                CouncilGameComponent.GiveThought(p, t);
                (felt > 0 ? calm : uneasy).Add(p);
            }
            var speaker = listeners?.FirstOrDefault(p => p != null && p.Spawned && !p.Dead);
            if (speaker != null) PeoplesG.SayLine(speaker, incident, stirs ? "void" : "heard");
            string Names(List<Pawn> l) => l.Count == 0 ? "CC_PeoplesG_Nobody".Translate().Resolve() : TextUtil.JoinAnd(l.Select(p => p.LabelShort).ToList());
            Tell.Message(NewsKind.People, Importance.Notable, map, "CC_PeoplesG_Heard".Translate((f?.Name ?? "?").Named("FACTION"), Names(calm).Named("CALM"), Names(uneasy).Named("UNEASY")).Resolve(),
                speaker != null ? new LookTargets(speaker) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            if (listeners != null) PeoplesG.Home(listeners);
        }
    }

    /// <summary>
    /// Two or three listeners of the Choir of the Spire come to the gate in pale robes: they would sit a while in the
    /// colony's middle and listen to it. Afterwards some at home feel a calm, others an unease, the psychically sensitive
    /// the most ({TALE}). Or {A} sits with them and comes away the calmer. Or the gate stays shut.
    /// </summary>
    public class HappeningWorker_ChoirListening : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Listeners) != null && Colonists(map).Count >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Listeners);
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Choir_Cantor"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            int n = Rand.RangeInclusive(1, 2);
            for (int i = 0; i < n; i++) ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Choir_Listener"), ctx.faction, ext.ages));
            // The one most open to the hum sits with them, if the colony likes.
            ctx.a = PeoplesD.Able(ctx.map).OrderByDescending(p => p.GetStatValue(StatDefOf.PsychicSensitivity)).FirstOrDefault();
            ctx.note = ChoirListening.VoidStirs() ? "void" : null;
            ctx.tale = (ctx.note == "void" ? "CC_PeoplesG_ListenVoid" : "CC_PeoplesG_ListenHum").Translate().Resolve();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "void") tags.Add("void");
            if (ctx.a != null && ctx.a.HasPsylink) tags.Add("psycaster");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "listen" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The listening let: the listeners sit in the ring (ChoirListening.Begin), with <see cref="together"/> {A} beside them.</summary>
    public class DilemmaEffect_ChoirListen : DilemmaEffect
    {
        public bool together;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Play("CC_PeoplesG_Impact_Listen".Translate().Resolve(), "CC_PeoplesG_Impact_ListenBrief".Translate().Resolve());
            var calm = ChoirListening.CalmDef;
            var unease = ChoirListening.UneaseDef;
            var map = ctx?.map;
            if (map == null || calm == null || unease == null) return;
            var all = IncidentWorker_Happening.Colonists(map);
            if (together && ctx.a != null) sheet.Thought(new List<Pawn> { ctx.a }, ctx.a.LabelShort, calm, p => null);
            var rest = all.Where(p => !together || p != ctx.a).ToList();
            if (rest.Count == 0) return;
            sheet.Add(ImpactKind.Mood, 0, "CC_PeoplesG_Impact_ListenFelt".Translate(rest.Count).Resolve(), "CC_PeoplesG_Impact_ListenFeltBrief".Translate().Resolve(), "choirfelt");
        }

        public override void Apply(DilemmaContext ctx) => ChoirListening.Begin(ctx as HappeningContext, together ? ctx?.a : null);
    }

    // ===================== The Choir of the Spire: art for their goods =====================

    /// <summary>What the Choir give for art: psychic gear and the rare archotech goods of the Spire, whatever the game and the DLCs have.</summary>
    public static class ChoirGoods
    {
        public static readonly string[] Names =
        {
            "PsychicSoothePulser", "PsychicAnimalPulser", "ArchiteCapsule", "PsychicHarmonizer", "PsychicSensitizer", "MechSerumHealer", "Apparel_EltexSkullcap"
        };

        public static List<ThingDef> Defs() => Names.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

        /// <summary>The colony's art packed up, the dearest first, as many as they take.</summary>
        public static List<Thing> Art(Map map) => map == null ? new List<Thing>() : PlayCarry.Stock(map, PlayCarry.Art, null).Take(PeoplesGMath.ArtPieces).ToList();

        public static float Worth(List<Thing> art) => art.Sum(t => t.MarketValue * t.stackCount);

        /// <summary>Their good for art of this worth (PeoplesGMath.Pick), or null.</summary>
        public static ThingDef For(float worth)
        {
            var defs = Defs();
            int i = PeoplesGMath.Pick(worth, defs.Select(d => d.BaseMarketValue).ToList());
            return i < 0 ? null : defs[i];
        }
    }

    /// <summary>
    /// The Choir's keeper of the hum comes to the gate: they would take the colony's art ({TALE}) and give for it one of
    /// the Spire's goods ({THING}), set down at the gate. Or the colony's best artist {A} talks with them about the work,
    /// and learns.
    /// </summary>
    public class HappeningWorker_ChoirArt : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (PeoplesD.Friendly(PeoplesMath.Listeners) == null || Colonists(map).Count == 0) return false;
            float worth = ChoirGoods.Worth(ChoirGoods.Art(map));
            return worth >= PeoplesGMath.ArtMinWorth && ChoirGoods.For(worth) != null;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Listeners);
            if (ctx.faction == null) return false;
            var art = ChoirGoods.Art(ctx.map);
            float worth = ChoirGoods.Worth(art);
            ctx.thing = ChoirGoods.For(worth);
            if (ctx.thing == null || art.Count == 0) return false;
            ctx.count = art.Count;
            ctx.variant = art.Count;
            ctx.tale = TextUtil.JoinAnd(art.Select(t => ((t as MinifiedThing)?.InnerThing ?? t).LabelNoCount).ToList());
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Artistic) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.count == 1) tags.Add("one");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && ChoirGoods.Art(ctx.map).Count < ctx.count ? "CC_PeoplesG_ArtGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "art" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The art carried out to the keeper (Couriers.TakeFor), and their good ({THING}) set down at the gate.</summary>
    public class DilemmaEffect_ChoirArt : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.thing == null) return;
            sheet.Goods(-1, PlayCarry.Label(PlayCarry.Art, null), Mathf.Max(1, ctx.count), PlayCarry.Art);
            sheet.Goods(1, ctx.thing, 1);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || ctx.thing == null) return;
            int n = Mathf.Min(ctx.count, ChoirGoods.Art(map).Count);
            if (n > 0 && !Couriers.TakeFor(ctx, PlayCarry.Art, null, n)) PlayCarry.Take(map, PlayCarry.Art, null, n);
            var good = ThingMaker.MakeThing(ctx.thing, ctx.thing.MadeFromStuff ? GenStuff.DefaultStuffFor(ctx.thing) : null);
            var x = ctx.stranger;
            Couriers.Loot(new List<Thing> { good }, map, x != null && x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, Couriers.PeopleOf(ctx), x);
        }
    }

    // ===================== The Choir of the Spire: the listener's stay =====================

    /// <summary>
    /// The Choir asks the colony to take one of its listeners for some days, to learn what it is to be ordinary: they come
    /// a couple of days after the colony agrees, eat at its table and sleep in a bed it has free (Hosting), and each
    /// afternoon they spend an hour beside someone of the colony at their work, watching (JobDriver_PeoplesG, the one they
    /// follow the better for it). At the end they leave the Choir's thanks; a stay with no afternoon beside anyone was
    /// wasted.
    /// </summary>
    public class HappeningWorker_MissionChoirStay : HappeningWorker_MissionHost
    {
        protected override string Kind => "choirStay";

        protected override float RestDays => 75f;

        protected override bool Possible(Map map) => PeoplesD.Friendly(PeoplesMath.Listeners) != null && StorytellerEvents.Adults(map).Count() >= 2;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = PeoplesD.Friendly(PeoplesMath.Listeners);
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.x = Arrivals.Make(PeoplesD.KindNamed(m.faction, "Rimfolk_Choir_Listener"), m.faction, new FloatRange(18f, 30f));
            if (m.x == null) return false;
            Arrivals.Hold(m.x);
            m.name = m.x.LabelShort;
            m.wait = Rand.Range(1.5f, 2.5f);
            m.days = m.wait + Rand.Range(PeoplesGMath.StayMinDays, PeoplesGMath.StayMaxDays);
            m.reward = StoryChains.Silver(map, 80f, 180f);
            m.goodwill = 10;
            m.penalty = 5;
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        /// <summary>Someone of the colony at their work now, up and about, whom the listener could go and stand beside.</summary>
        private static Pawn Working(Map map, Pawn x) =>
            PeoplesD.Able(map).Where(p => p.CurJob != null && p.CurJobDef != JobDefOf.Wait && !p.InBed() && x.CanReach(p, PathEndMode.Touch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(x.Position)).FirstOrDefault();

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && Harmed(m))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            if (m.stage == 0)
            {
                Remind(m, map, now);
                if (now < m.due) return;
                if (!Arrive(m, map, PeoplesD.GateSpot(map), (m.deadline - now) / (float)GenDate.TicksPerHour))
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                m.stage = 1;
                string line = Line(More?.ready, m, map);
                if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.PositiveEvent);
                return;
            }
            Tend(m, map);
            // An afternoon's hour beside someone at their work: a day counted once.
            int hour = GenLocalDate.HourOfDay(map);
            int day = now / GenDate.TicksPerDay;
            if (PeoplesGMath.OrdinaryHour(hour) && x.Spawned && x.Map == map && Hosting.Free(x) && m.misses != day)
            {
                var who = Working(map, x);
                if (who != null && PeoplesE.Start(x, PeoplesG.OrdinaryJob, who, null, PeoplesGMath.OrdinaryTicks)) m.misses = day;
            }
            bool gone = !x.Spawned || x.Map != map;
            if (now < m.deadline && !gone) return;
            var look = x.Spawned ? new LookTargets(x) : LookTargets.Invalid;
            if (m.tally == 0)
            {
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Failed, "idle", null, look);
                return;
            }
            Couriers.PaySilver(map, x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, m.reward, m.faction, x);
            Missions.Goodwill(m, m.goodwill);
            SeeOff(m, map);
            Missions.Finish(m, MissionState.Done, "stayed", m.tally.ToString(), look);
        }

        /// <summary>An hour beside someone at their work done: the day counted for the stay, said over their head, and the one they followed a little the better for it.</summary>
        public static void Followed(Pawn pawn, Pawn who)
        {
            var m = StoriesComponent.Instance?.missions?.FirstOrDefault(x => x.Open && x.kind == "choirStay" && x.x == pawn);
            if (m == null) return;
            m.tally++;
            Speech.Say(pawn, "CC_PeoplesG_OrdinarySays".Translate().Resolve());
            // together: the listener's job brought them to the side of the one they watched, for the hour.
            var t = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_ChoirFollowed");
            if (t != null && who != null && who.Faction != null && who.Faction.IsPlayer && !who.Dead) CouncilGameComponent.GiveThought(who, t, pawn);
        }

        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false);

        protected override Building_Bed BedFor(Mission m, Map map, Pawn p) =>
            Hosting.Beds(map, o => o == p, p).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        public override string Status(Mission m) =>
            m.stage == 0
                ? "CC_PeoplesG_Stay_Coming".Translate(m.name ?? "?").Resolve()
                : "CC_PeoplesG_Stay_Staying".Translate(m.name ?? "?", m.tally).Resolve() + " " + MealsText(m);
    }

    // ===================== The Sunken Vault: supply =====================

    /// <summary>
    /// The Sunken Vault declines an age every few years unless supplied (PeopleAgesMath.Season, ageDeclines). Components and
    /// parts the colony sells it in the trade window count by themselves (PeopleAges.Trade); what it gives at the gate (its
    /// barter, a gift, a craft taught) is remembered here as the rule's gifts (FactionMemory, PeopleAgeGifts.DeedKind).
    /// </summary>
    public static class VaultSupply
    {
        /// <summary>The vault's ask for power cells (Defs/MissionDefs/Rimfolk_Missions_PeoplesG.xml): delivered, its parts are supply too (Patch_FactionMemory.MissionEnded).</summary>
        public const string MissionKind = "vaultcells";

        public static void Noted(Faction f, float worth, string what)
        {
            if (f == null || f.IsPlayer || f.defeated) return;
            int weight = PeoplesGMath.SupplyWeight(worth);
            if (weight <= 0) return;
            FactionMemory.Note(f, PeopleAgeGifts.DeedKind, what, weight);
        }

        /// <summary>The silver worth of the components and food of an answer (the event's {COUNT} components, {TALE} raw food).</summary>
        public static float Worth(int components, int food) => Mathf.Max(0, components) * ThingDefOf.ComponentIndustrial.BaseMarketValue + Mathf.Max(0, food) * 1.2f;
    }

    /// <summary>What the colony gave the vault at the gate, remembered as supply (VaultSupply.Noted); with <see cref="gift"/>, given for nothing, as a friend's.</summary>
    public class DilemmaEffect_VaultSupply : DilemmaEffect
    {
        public bool gift;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Add(ImpactKind.Other, 1, (gift ? "CC_PeoplesG_Impact_SupplyGift" : "CC_PeoplesG_Impact_Supply").Translate(ctx?.faction?.Name ?? "?").Resolve(),
                "CC_PeoplesG_Impact_SupplyBrief".Translate().Resolve(), "vaultsupply");

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.faction == null) return;
            VaultSupply.Noted(hc.faction, VaultSupply.Worth(hc.count, hc.variant) * (gift ? 1.5f : 1f), "CC_PeopleAge_Gift_components".Translate().Resolve());
        }
    }

    // ===================== The Sunken Vault: the barter =====================

    /// <summary>
    /// A technician of the Sunken Vault comes up the road with the last of the vault's spacer goods ({TALE}), and wants
    /// components and food for them: {COUNT} components and some raw food, carried out to them, and the vault's crate set
    /// down at the gate. Or the colony gives the components and food and takes nothing, and the vault remembers its
    /// supplier. What the vault is given holds its age (PeopleAgesMath, ageDeclines).
    /// </summary>
    public class HappeningWorker_VaultBarter : HappeningWorker_Visitors
    {
        /// <summary>The vault's last spacer goods, fewer the further it has fallen: glitterworld medicine, an advanced component, a charge rifle.</summary>
        public static List<(ThingDef def, int count)> Crate(Faction f)
        {
            int age = PeopleAgesMath.AgeOfTech((int)Peoples.TechOf(f));
            var list = new List<(ThingDef, int)>();
            var med = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineUltratech");
            if (med != null) list.Add((med, age >= 3 ? 2 : 1));
            if (age >= 2) list.Add((ThingDefOf.ComponentSpacer, 1));
            var rifle = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_ChargeRifle");
            if (age >= 3 && rifle != null) list.Add((rifle, 1));
            return list;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Vaultfolk) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            if (ctx.faction == null) return false;
            var crate = Crate(ctx.faction);
            if (crate.Count == 0) return false;
            float wf = LivingEvents.WealthFactor(ctx.map);
            ctx.note = HappeningWorker_UnionGoods.Write(crate);
            ctx.count = PeoplesGMath.BarterComponents(wf);
            ctx.variant = PeoplesGMath.BarterFood(wf);
            ctx.tale = TextUtil.JoinAnd(crate.Select(c => Couriers.Amount(c.count, null, c.def)).ToList());
            ctx.thing = ThingDefOf.ComponentIndustrial;
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Scout"), ctx.faction, ext.ages));
            ctx.a = PeoplesD.Able(ctx.map).OrderBy(p => p.Position.DistanceToSquared(PeoplesD.GateSpot(ctx.map) ?? p.Position)).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            int age = PeopleAgesMath.AgeOfTech((int)Peoples.TechOf(ctx.faction));
            if (age < 3) tags.Add("fallen");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "barter" : option == 1 ? "gift" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The Sunken Vault: a craft they lost =====================

    /// <summary>The craft taught at the colony's bench: when their technician has worked it through, the vault can make it again, remembered as supply and as a lesson.</summary>
    public static class VaultCraft
    {
        private static readonly Dictionary<int, IncidentDef> lessons = new Dictionary<int, IncidentDef>();

        public static void Pending(Pawn technician, IncidentDef incident)
        {
            if (technician != null) lessons[technician.thingIDNumber] = incident;
        }

        /// <summary>A lesson's hours at the bench done: the colony's teachers' is their own; their technician's is the craft learned again, told, and home.</summary>
        public static void Taught(Pawn pawn, Thing bench)
        {
            if (pawn == null || pawn.Faction == null || pawn.Faction.IsPlayer) return;
            lessons.TryGetValue(pawn.thingIDNumber, out var incident);
            lessons.Remove(pawn.thingIDNumber);
            string craft = bench?.def?.label ?? "?";
            var f = pawn.Faction;
            VaultSupply.Noted(f, PeoplesGMath.CraftWorth, craft);
            FactionMemory.Note(f, "learnedHere", pawn.LabelShort);
            pawn.skills?.Learn(SkillDefOf.Crafting, 1200f, true);
            PeoplesG.SayLine(pawn, incident, "learned");
            Tell.Message(NewsKind.World, Importance.Notable, pawn.MapHeld, "CC_PeoplesG_CraftLearned".Translate(pawn.LabelShort.Named("PAWN"), craft.Named("CRAFT"), f.Name.Named("FACTION")).Resolve(),
                new LookTargets(pawn), MessageTypeDefOf.PositiveEvent);
            PeoplesG.Home(new List<Pawn> { pawn });
        }
    }

    /// <summary>
    /// A technician of the Sunken Vault comes to ask the colony to teach them a craft the vault has lost: the work of the
    /// colony's {TALE}, the bench where {A} works. {A} teaches them there for a few hours; with {B} beside them, the
    /// colony's other hands learn it again too. The vault can make it again, and remembers who taught it.
    /// </summary>
    public class HappeningWorker_VaultCraft : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Vaultfolk) != null && WanderYear.Bench(map, PeoplesD.Best(map, SkillDefOf.Crafting)) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            if (ctx.faction == null) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Crafting, p => !p.WorkTagIsDisabled(WorkTags.Crafting));
            if (ctx.a == null) return false;
            ctx.place = WanderYear.Bench(ctx.map, ctx.a);
            if (ctx.place == null) return false;
            ctx.tale = ctx.place.def.label;
            ctx.b = PeoplesD.Able(ctx.map).Where(p => p != ctx.a && !p.WorkTagIsDisabled(WorkTags.Crafting)).OrderBy(p => p.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0).FirstOrDefault() ?? ctx.a;
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.b == ctx.a) tags.Add("alone");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed || !ctx.place.Spawned) ? "CC_PeoplesG_BenchGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "teach" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The lesson at the bench: their technician and {A} (with <see cref="withB"/>, {B} too) there a few hours (VaultCraft.Taught); with the play-outs off, at once.</summary>
    public class DilemmaEffect_VaultTeach : DilemmaEffect
    {
        public bool withB;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            sheet.Play("CC_PeoplesG_Impact_Teach".Translate(hc.a.LabelShort, hc.tale ?? "?").Resolve(), "CC_PeoplesG_Impact_TeachBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            var x = ctx?.stranger;
            var bench = hc?.place;
            if (hc == null || map == null || x == null) return;
            VaultCraft.Pending(x, hc.incident);
            int ticks = PeoplesGMath.TeachHours * GenDate.TicksPerHour;
            if (PlayOuts.On && bench != null && bench.Spawned && PeoplesG.Errand(x, map, bench, null, PeoplesG.TeachJob, ticks, PeoplesGMath.TeachHours + 3))
            {
                foreach (var p in new[] { hc.a, withB ? hc.b : null }.Where(p => p != null).Distinct())
                    if (p.Spawned && p.Map == map && !p.Drafted && !p.Downed && !p.InMentalState) PeoplesE.Start(p, PeoplesG.TeachJob, bench, null, ticks);
                return;
            }
            VaultCraft.Taught(x, bench);
        }
    }

    // ===================== The Sunken Vault: the child who never saw a sky =====================

    /// <summary>The vault-born child under the sky: where they stand to see it, and what they make of it.</summary>
    public static class VaultChild
    {
        /// <summary>Open ground under the sky near the gate.</summary>
        public static IntVec3 SkySpot(Map map)
        {
            var gate = PeoplesD.GateSpot(map);
            if (map == null || gate == null) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(gate.Value, 10f, true))
                if (c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetFirstBuilding(map) == null
                    && map.reachability.CanReach(gate.Value, c, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)))
                    return c;
            return gate.Value;
        }

        /// <summary>Their first sky seen: said over their head, told.</summary>
        public static void SawTheSky(Pawn child)
        {
            if (child == null || !child.Spawned) return;
            Speech.Say(child, "CC_PeoplesG_SkySays".Translate().Resolve());
            Tell.Message(NewsKind.People, Importance.Notable, child.Map, "CC_PeoplesG_SawTheSky".Translate(child.Named("PAWN")).Resolve(), new LookTargets(child), MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>
    /// A surface-scout of the Sunken Vault comes up the road with a vault-born child ({X}) who has never seen a sky until
    /// this week, and keeps looking up. The child asks to stay. Taken in, they are the colony's, a person joining as any
    /// other does; or they may stand under the sky a while before the scout takes them home; or the gate stays shut.
    /// </summary>
    public class HappeningWorker_VaultChild : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Vaultfolk) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            if (ctx.faction == null) return false;
            bool child = ModsConfig.BiotechActive;
            var ages = child ? new FloatRange(PeoplesGMath.ChildMin, PeoplesGMath.ChildMax) : new FloatRange(PeoplesGMath.YouthMin, PeoplesGMath.YouthMax);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Dweller"), ctx.faction, ages, null, null, child);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Scout"), ctx.faction, new FloatRange(22f, 45f)));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.stranger != null && ctx.stranger.DevelopmentalStage.Juvenile()) tags.Add("child");
            else tags.Add("youth");
            int hour = GenLocalDate.HourOfDay(ctx.map);
            if (hour >= 19 || hour < 5) tags.Add("night");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.group.FirstOrDefault() ?? ctx.stranger, Ext, ctx, option == 0 ? "stay" : option == 1 ? "sky" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The child stands under the open sky near the gate a while (VaultChild.SawTheSky); taken in, the scout goes home alone.</summary>
    public class DilemmaEffect_VaultSky : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet) =>
            sheet.Play("CC_PeoplesG_Impact_Sky".Translate(ctx?.stranger?.LabelShort ?? "?").Resolve(), "CC_PeoplesG_Impact_SkyBrief".Translate().Resolve());

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            var map = ctx?.map;
            var x = ctx?.stranger;
            if (hc == null || map == null || !PeoplesG.Up(x, map)) return;
            bool ours = x.Faction != null && x.Faction.IsPlayer;
            if (ours) PeoplesG.Home(hc.group);
            var spot = VaultChild.SkySpot(map);
            if (PlayOuts.On && spot.IsValid)
            {
                bool started = ours ? PeoplesE.Start(x, PeoplesG.SkyJob, spot, null, PeoplesGMath.SkyTicks) : PeoplesG.Errand(x, map, spot, null, PeoplesG.SkyJob, PeoplesGMath.SkyTicks, 4f);
                if (started) return;
            }
            VaultChild.SawTheSky(x);
        }
    }

    // ===================== Dev mode =====================

    /// <summary>Dev mode: release G's state at a glance, and its slow things brought on now (Debug actions, "Rimfolk", "Peoples: ...").</summary>
    public static class PeoplesGDebug
    {
        private static void Say(string text) => Messages.Message("Peoples: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples: release G's rare genes, mechs, art and the vault's supply (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void Log()
        {
            var sb = new System.Text.StringBuilder("[Rimfolk] Peoples, release G:");
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => Peoples.CallingOf(f) == PeoplesMath.Vaultfolk))
            {
                var r = PeopleAgeBook.Instance?.Of(f);
                sb.Append("\n  " + f.Name + ": " + Peoples.TechOf(f) + ", progress " + (r?.progress ?? 0f).ToString("0.00") + ", remembered supply "
                          + PeopleAges.Gifts(f, PeoplesG.Now).ToString("0") + " silver");
            }
            foreach (var m in Find.Maps.Where(m => m.IsPlayerHome))
            {
                var rare = HelixSample.Rarest(m, out int rarity);
                sb.Append("\n  " + m + ": rarest genes " + (rare?.LabelShort ?? "nobody") + " (" + rarity + "), gene work for " + HelixGenes.Takers(m).Count()
                          + ", hurt mechs " + LatticeRepair.Hurt(m).Count + ", mech parts " + HappeningWorker_LatticeParts.Worth(m, out _).ToString("0")
                          + " silver, art " + ChoirGoods.Worth(ChoirGoods.Art(m)).ToString("0") + " silver, turret site " + LatticeTurret.Site(m) + ", the void stirs " + ChoirListening.VoidStirs());
            }
            Verse.Log.Message(sb.ToString());
            Say("logged.");
        }

        [DebugAction("Rimfolk", "Peoples: the Choir's listening heard now, without them (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Heard()
        {
            var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => Peoples.CallingOf(x) == PeoplesMath.Listeners && !x.defeated);
            if (f == null)
            {
                Say("no Choir of the Spire in this world.");
                return;
            }
            ChoirListening.Heard(Find.CurrentMap, f, null, null, null);
        }

        [DebugAction("Rimfolk", "Peoples: the Sunken Vault falls an age now (testing)", allowedGameStates = AllowedGameStates.Playing)]
        private static void VaultFalls()
        {
            var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => Peoples.CallingOf(x) == PeoplesMath.Vaultfolk && !x.defeated);
            if (f == null) Say("no Sunken Vault in this world.");
            else if (!PeopleAges.Push(f, -1)) Say("the vault can fall no further.");
        }
    }
}
