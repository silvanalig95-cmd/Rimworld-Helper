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
    /// <summary>
    /// The old powers' finds and their beasts and gods (OldPowers.cs):
    /// <list type="bullet">
    /// <item>The sealed scroll: one of the colony standing in a room of the old ruins nobody has searched sees something
    /// sealed in the dust, walks to it and kneels: a scroll of A RimWorld of Magic's (a spell, now and then its grimoire),
    /// else a tome (Anomaly) in an older colony or a schematic or textbook in a newer one. Each room gives one at most.</item>
    /// <item>The thing under the ground: after a long wet run or with the thaw, something knocks under the colony's ground
    /// one night (dust jumps), the ground cracks the next, and on the third morning it comes up: an old cache (silver and
    /// an old worn thing of the colony's age), a precious thing of the age, A RimWorld of Magic's raw magicyte, or with
    /// Anomaly and the shared harm budget the dead crawling up out of it. Someone walks over to look.</item>
    /// <item>The beast that spoke: at dusk, after the colony's hunters took four or more beasts, or at a full moon, a wild
    /// beast walks up to someone of the colony out of doors, stands before them and says one true thing (the beasts taken,
    /// a fight coming, that they are sick, hungry, worn out or low), and walks away an ordinary beast again.</item>
    /// <item>A god going by: a god of the colony's drawn near, with nothing of the gods under way, goes by in the night: its
    /// own signs one after another (GodOmens, two hours apart), and at dawn nothing more; the one who keeps it most says it
    /// went by. Named only where the colony knows it was that god.</item>
    /// </list>
    /// </summary>
    public static class OldPowersFinds
    {
        private static int Now => OldPowers.Now;

        private static readonly Color Rune = new Color(1f, 0.82f, 0.45f), Earth = new Color(0.55f, 0.45f, 0.35f), Ghost = new Color(0.8f, 0.86f, 0.95f);

        // ================= The sealed scroll =================

        /// <summary>One of the colony awake in a room of the old ruins not searched yet, and the room's key.</summary>
        private static Pawn InRuin(Map map, OldPowersBook b, out string key)
        {
            key = null;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!OwnHappenings.Ours(p) || !OldPowers.Awake(p)) continue;
                string k = OwnUndertow.RuinKey(map, p.GetRoom());
                if (k == null || b.searched.Contains(k)) continue;
                key = k;
                return p;
            }
            return null;
        }

        public static GodPaceMath.Trigger ScrollCall(Map map)
        {
            var b = OldPowers.Book;
            if (b == null || ScrollDef() == null) return GodPaceMath.Trigger.None;
            return OldPowersMath.ScrollTrigger(InRuin(map, b, out _) != null, false);
        }

        /// <summary>
        /// What lies sealed in the old stones: A RimWorld of Magic's spell scrolls (its own category) and now and then its
        /// grimoire; else a tome (Anomaly) or a textbook in an older colony, a schematic or a textbook in a newer one.
        /// </summary>
        internal static ThingDef ScrollDef()
        {
            var scrolls = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("TM_Scrolls");
            if (scrolls != null)
            {
                var grimoire = DefDatabase<ThingDef>.GetNamedSilentFail("TM_Grimoire");
                if (grimoire != null && Rand.Chance(0.15f)) return grimoire;
                var spell = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.thingCategories != null && d.thingCategories.Contains(scrolls) && d.defName.StartsWith("SpellOf_"))
                    .RandomElementWithFallback();
                if (spell != null) return spell;
            }
            var names = OldPowers.Age <= 1 ? new[] { "Tome", "TextBook" } : new[] { "Schematic", "TextBook" };
            foreach (var n in names)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d != null) return d;
            }
            return null;
        }

        public static bool Scroll(OldPowersBook b, Map map)
        {
            var who = InRuin(map, b, out string key);
            var def = ScrollDef();
            if (who == null || def == null) return false;
            var room = who.GetRoom();
            var cell = room?.Cells.Where(c => c.Standable(map) && c != who.Position && c.GetFirstItem(map) == null && who.CanReach(c, PathEndMode.OnCell, Danger.Some))
                .InRandomOrder().FirstOrDefault() ?? IntVec3.Invalid;
            if (!cell.IsValid) return false;
            var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            if (!GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near, out var placed)) return false;
            b.Searched(key);
            OldPowers.Motes(map, placed.DrawPos, Rune, 4, 0.3f, "Mote_GlowingRune", "Mote_ArcaneCircle", "PsycastAreaEffect");
            OwnHappenings.GoTo(who, placed.Position, 1200);
            var p = OldPowers.Play(b, OldPower.Scroll, map, 2f, 150);
            p.a = who;
            p.thing = placed;
            p.cell = placed.Position;
            p.stage = 1;
            p.form = def.defName.StartsWith("SpellOf_") ? "spell" : def.defName;
            b.Came(OldPower.Scroll, Now, p.form);
            return true;
        }

        internal static void ScrollStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var who = p.a;
            var map = p.map;
            if (who == null || who.Dead || !who.Spawned || p.thing == null || !p.thing.Spawned)
            {
                OldPowers.End(b);
                return;
            }
            if (!who.Position.InHorDistOf(p.thing.Position, 1.5f) && p.count++ < 16)
            {
                if (who.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(who, p.thing.Position, 1200);
                OldPowers.Next(b, p, 150);
                return;
            }
            OwnHappenings.Stand(who, p.thing.Position, 400);
            OldPowers.Motes(map, p.thing.DrawPos, Rune, 6, 0.4f, "Mote_GlowingRune", "Mote_ArcaneCircle", "PsycastAreaEffect");
            OldPowers.Sound(map, p.thing.Position, "ItemEnchanted", "Interact_GraveDig", "PsychicSootheGlobal");
            OldPowers.Say(who, "scroll,found", p.thing.LabelNoCount.Named("THING"));
            OldPowers.Card(map, "Scroll", OldPowers.T("Scroll_Text", who.LabelShort.Named("FINDER"), p.thing.LabelNoCount.Named("THING"), OldPowers.Aged("Scroll_Ruin").Named("RUIN")),
                new LookTargets(p.thing), LetterDefOf.PositiveEvent);
            OldPowers.End(b);
        }

        // ================= The thing under the ground =================

        public static GodPaceMath.Trigger BuriedCall(Map map)
        {
            bool thaw = OldPowers.Quarter(map) == StoryWaysMath.Spring && OldPowers.DayOfSeason(map) <= 2;
            var why = OldPowersMath.BuriedTrigger(OwnHappenings.WetRun(map), thaw);
            return why != GodPaceMath.Trigger.None && Spot(map).IsValid ? why : GodPaceMath.Trigger.None;
        }

        /// <summary>Open ground near the colony's middle: earth under the sky, nothing built or stored on it, not a field.</summary>
        private static IntVec3 Spot(Map map)
        {
            var mid = OwnHappenings.Middle(map);
            for (int i = 0; i < 40; i++)
            {
                var c = CellFinder.RandomClosewalkCellNear(mid, map, 25);
                if (!c.InBounds(map) || c.Roofed(map) || c.Fogged(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null) continue;
                var t = c.GetTerrain(map);
                if (t == null || t.IsWater || t.fertility <= 0f || map.zoneManager.ZoneAt(c) != null) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        public static bool Buried(OldPowersBook b, Map map)
        {
            var cell = Spot(map);
            if (!cell.IsValid) return false;
            Knock(map, cell, 1.4f);
            var w = OldPowers.Witness(map, cell, 25f) ?? OldPowers.FreeNear(map, cell);
            if (w != null) OldPowers.Say(w, "buried,knock");
            OldPowers.Minor(map, OldPowers.T("Buried_Knock", OldPowers.Toward(OwnHappenings.Middle(map), cell).Named("POINT")), new LookTargets(cell, map));
            var p = OldPowers.Play(b, OldPower.Buried, map, 60f, GenDate.TicksPerDay);
            p.cell = cell;
            p.stage = 1;
            b.Came(OldPower.Buried, Now, "knock");
            return true;
        }

        private static void Knock(Map map, IntVec3 cell, float size)
        {
            for (int i = 0; i < 3; i++) FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.6f, 0.6f), 0f, Rand.Range(-0.6f, 0.6f)), map, size, Earth);
            OldPowers.Sound(map, cell, "TM_SoftExplosion", "Explosion_Thump", "ChunkRock_Drop");
        }

        internal static void BuriedStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            int hour = GenLocalDate.HourOfDay(map);
            switch (p.stage)
            {
                case 1:
                {
                    // The second night: the ground cracks.
                    Knock(map, p.cell, 1.8f);
                    foreach (var c in GenRadial.RadialCellsAround(p.cell, 1.9f, true))
                        if (c.InBounds(map) && c.Standable(map) && !c.Roofed(map))
                            FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_Dirt, 1);
                    OldPowers.Motes(map, p.cell.ToVector3Shifted(), Earth, 3, 0.6f, "Mote_EarthCrack", "GroundCrack");
                    var w = OldPowers.Witness(map, p.cell, 25f);
                    if (w != null) OldPowers.Say(w, "buried,crack");
                    OldPowers.Minor(map, OldPowers.T("Buried_Crack"), new LookTargets(p.cell, map));
                    p.stage = 2;
                    OldPowers.Next(b, p, GenDate.TicksPerHour * Math.Max(1, (6 + 24 - hour) % 24));
                    return;
                }
                case 2:
                {
                    // The third morning: it comes up.
                    if (hour < 5 || hour > 10)
                    {
                        OldPowers.Next(b, p, 1250);
                        return;
                    }
                    Up(b, p);
                    return;
                }
                case 3:
                {
                    var go = p.b;
                    if (go == null || go.Dead || !go.Spawned)
                    {
                        OldPowers.End(b);
                        return;
                    }
                    if (!go.Position.InHorDistOf(p.cell, 3f) && p.count++ < 16)
                    {
                        OldPowers.Next(b, p, 250);
                        return;
                    }
                    OwnHappenings.Stand(go, p.cell, 500);
                    OldPowers.Say(go, "buried,found", (p.note ?? "").Named("THING"));
                    OldPowers.End(b);
                    return;
                }
            }
        }

        private static readonly string[] Finds = { "Cache", "Relic", "Magicyte", "Waker" };

        private static void Up(OldPowersBook b, OldPowerPlay p)
        {
            var map = p.map;
            bool rom = DefDatabase<ThingDef>.GetNamedSilentFail("RawMagicyte") != null;
            bool waker = ModsConfig.AnomalyActive && Harm.On && DefDatabase<PawnKindDef>.GetNamedSilentFail("ShamblerSwarmer") != null;
            // A different find than the last ones where there is one.
            var forms = b.Forms(OldPower.Buried);
            var weights = new List<float> { 3f, 2f, rom ? 2f : 0f, waker ? 1.5f : 0f };
            int i = GodPaceMath.Pick(Finds, weights, forms, Rand.Value);
            var find = i < 0 ? OldPowersMath.BuriedFind.Cache : (OldPowersMath.BuriedFind)i;
            if (find == OldPowersMath.BuriedFind.Waker && !Harm.Roll(HarmKinds.Uncanny, OldPowersMath.WakerChance)) find = OldPowersMath.BuriedFind.Cache;
            FleckMaker.ThrowDustPuffThick(p.cell.ToVector3Shifted(), map, 2.4f, Earth);
            OldPowers.Sound(map, p.cell, "TM_SoftExplosion", "Explosion_Thump", "ChunkRock_Drop");
            GodPaceMath.Note(b.lately, OldPowersMath.Name(OldPower.Buried), Finds[(int)find]);
            if (find == OldPowersMath.BuriedFind.Waker && Wake(map, p.cell))
            {
                OldPowers.End(b);
                return;
            }
            if (find == OldPowersMath.BuriedFind.Waker) find = OldPowersMath.BuriedFind.Cache;
            var things = Things(map, find);
            if (things.Count == 0)
            {
                OldPowers.End(b);
                return;
            }
            var placed = new List<Thing>();
            foreach (var t in things)
                if (GenPlace.TryPlaceThing(t, p.cell, map, ThingPlaceMode.Near, out var at))
                    placed.Add(at);
            p.note = TextUtil.JoinAnd(placed.Select(t => t.LabelCap.ToString()).ToList());
            OldPowers.Card(map, "Buried", OldPowers.T("Buried_Text", p.note.Named("FOUND"), OldPowers.T("Buried_" + find).Named("WHAT")), new LookTargets(placed),
                LetterDefOf.PositiveEvent);
            var go = OldPowers.FreeNear(map, p.cell);
            var near = go == null ? IntVec3.Invalid : OwnHappenings.Beside(map, p.cell, go);
            if (!near.IsValid || !OwnHappenings.GoTo(go, near, GenDate.TicksPerHour))
            {
                OldPowers.End(b);
                return;
            }
            p.b = go;
            p.stage = 3;
            p.count = 0;
            OldPowers.Next(b, p, 250);
        }

        /// <summary>What comes up: a cache, a precious thing of the colony's age, magicyte (with a minor arcane gem now and then).</summary>
        private static List<Thing> Things(Map map, OldPowersMath.BuriedFind find)
        {
            var list = new List<Thing>();
            switch (find)
            {
                case OldPowersMath.BuriedFind.Relic:
                {
                    var def = ThingRoles.Ours(ThingRole.Precious, map, ThingRoles.Seed("oldpowers-buried", map, Now / GenDate.TicksPerDay));
                    if (def == null) goto case OldPowersMath.BuriedFind.Cache;
                    var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                    t.stackCount = Mathf.Clamp(ThingRoles.CountFor(150f, def), 1, def.stackLimit);
                    list.Add(t);
                    break;
                }
                case OldPowersMath.BuriedFind.Magicyte:
                {
                    var raw = DefDatabase<ThingDef>.GetNamedSilentFail("RawMagicyte");
                    if (raw == null) goto case OldPowersMath.BuriedFind.Cache;
                    var t = ThingMaker.MakeThing(raw);
                    t.stackCount = Rand.RangeInclusive(15, 35);
                    list.Add(t);
                    if (Rand.Chance(0.3f))
                    {
                        var gem = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.defName.StartsWith("TM_EStone_") && d.defName.EndsWith("_minor")).RandomElementWithFallback();
                        if (gem != null) list.Add(ThingMaker.MakeThing(gem));
                    }
                    break;
                }
                case OldPowersMath.BuriedFind.Cache:
                default:
                {
                    var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = Rand.RangeInclusive(25, 70);
                    list.Add(silver);
                    var old = OwnMurmur.OldThing();
                    if (old != null) list.Add(old);
                    break;
                }
            }
            return list;
        }

        /// <summary>With Anomaly, through the shared harm budget: one or two of the game's shamblers crawl up out of the ground and come for the colony.</summary>
        private static bool Wake(Map map, IntVec3 cell)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("ShamblerSwarmer");
            var entities = ModsConfig.AnomalyActive ? Faction.OfEntities : null;
            if (kind == null || entities == null) return false;
            var risen = new List<Pawn>();
            int n = Rand.RangeInclusive(OldPowersMath.WakersMin, OldPowersMath.WakersMax);
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var at = CellFinder.StandableCellNear(cell, map, 2f);
                    if (!at.IsValid) continue;
                    var x = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, entities, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                    GenSpawn.Spawn(x, at, map);
                    DefDatabase<EffecterDef>.GetNamedSilentFail("ShamblerRaise")?.Spawn(at, map).Cleanup();
                    risen.Add(x);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the old powers: the dead waking", e);
                }
            }
            if (risen.Count == 0) return false;
            try
            {
                LordMaker.MakeNewLord(entities, new LordJob_AssaultColony(entities, false, false, false, false, false), map, risen);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the old powers: the dead waking", e);
            }
            var w = OldPowers.Witness(map, cell, 30f);
            if (w != null) OldPowers.Say(w, "buried,waker");
            OldPowers.Card(map, "BuriedWaker", OldPowers.T("BuriedWaker_Text", risen.Count.ToString().Named("COUNT")), new LookTargets(risen), LetterDefOf.ThreatSmall);
            return true;
        }

        // ================= The beast that spoke =================

        /// <summary>Wild beasts dead in the last days that the colony took (lying on its home ground or in its stores).</summary>
        internal static int Hunted(Map map)
        {
            int since = Now - Mathf.RoundToInt(OldPowersMath.HuntedDays * GenDate.TicksPerDay);
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Count(t => t is Corpse c && c.InnerPawn?.RaceProps?.Animal == true && c.InnerPawn.Faction == null
                                                                                     && c.timeOfDeath >= since && (c.IsInAnyStorage() || map.areaManager.Home[c.Position]));
        }

        /// <summary>One of the colony out of doors and free, and a wild beast near them that could walk up; else nulls.</summary>
        private static (Pawn who, Pawn beast) Pick(Map map)
        {
            var wild = map.mapPawns.AllPawnsSpawned.Where(a => a.RaceProps.Animal && a.Faction == null && !a.Dead && !a.Downed && a.Awake() && !a.InMentalState
                                                              && a.RaceProps.FleshType != FleshTypeDefOf.Insectoid && a.CurJobDef != JobDefOf.PredatorHunt && OwnHappenings.Yields(a)).ToList();
            if (wild.Count == 0) return (null, null);
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && OldPowers.Awake(x) && OwnHappenings.Yields(x) && !x.Position.Roofed(map)).InRandomOrder())
            {
                var beast = wild.Where(a => a.Position.InHorDistOf(p.Position, OldPowersMath.BeastReach) && a.CanReach(p, PathEndMode.Touch, Danger.Some))
                    .OrderBy(a => a.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                if (beast != null) return (p, beast);
            }
            return (null, null);
        }

        public static GodPaceMath.Trigger BeastCall(Map map)
        {
            var why = OldPowersMath.BeastTrigger(Hunted(map), OldPowers.FullMoon);
            return why != GodPaceMath.Trigger.None && Pick(map).who != null ? why : GodPaceMath.Trigger.None;
        }

        public static bool Beast(OldPowersBook b, Map map)
        {
            var (who, beast) = Pick(map);
            if (who == null) return false;
            var at = OwnHappenings.Beside(map, who.Position, beast);
            if (!at.IsValid) return false;
            OwnHappenings.Lead(beast, at, 1800);
            var p = OldPowers.Play(b, OldPower.Beast, map, 2f, 200);
            p.a = beast;
            p.b = who;
            p.count = Hunted(map);
            p.stage = 1;
            b.Came(OldPower.Beast, Now, beast.def.defName);
            return true;
        }

        internal static void BeastStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var beast = p.a;
            var who = p.b;
            var map = p.map;
            if (beast == null || beast.Dead || !beast.Spawned || beast.Downed || beast.InMentalState || who == null || who.Dead || !who.Spawned)
            {
                OldPowers.End(b);
                return;
            }
            switch (p.stage)
            {
                case 1:
                {
                    if (!beast.Position.InHorDistOf(who.Position, 2.5f) && p.value++ < 14)
                    {
                        if (beast.CurJobDef != JobDefOf.Goto) OwnHappenings.Lead(beast, OwnHappenings.Beside(map, who.Position, beast), 1500);
                        OldPowers.Next(b, p, 200);
                        return;
                    }
                    if (!beast.Position.InHorDistOf(who.Position, 2.5f))
                    {
                        OldPowers.End(b);
                        return;
                    }
                    // It stands before them, and they before it; it speaks.
                    OwnHappenings.Lead(beast, beast.Position, 900);
                    OwnHappenings.Stand(who, beast.Position, 900);
                    OldPowers.Motes(map, beast.DrawPos, Ghost, 4, 0.5f, "Mote_Psi_Arcane", "Mote_Ghost", "PsycastPsychicEffect");
                    var word = OldPowersMath.BeastWordOf(p.count, OldPowers.FightAhead(map, 5f), who.health?.hediffSet?.hediffs?.Any(h => h.def.makesSickThought) ?? false,
                        who.needs?.food?.CurLevelPercentage ?? 1f, who.needs?.rest?.CurLevelPercentage ?? 1f, who.needs?.mood?.CurLevelPercentage ?? 1f);
                    string tag = "beast,word:" + word.ToString().ToLowerInvariant();
                    string line = OldPowers.Line(beast, tag, p.count.ToString().Named("COUNT"), who.LabelShort.Named("NAME"));
                    if (line.NullOrEmpty())
                    {
                        OldPowers.End(b);
                        return;
                    }
                    Speech.Say(beast, line);
                    p.note = line;
                    p.form = word.ToString();
                    p.stage = 2;
                    OldPowers.Next(b, p, 360);
                    return;
                }
                case 2:
                {
                    OldPowers.Say(who, "beast,heard");
                    OldPowers.Memory(who, "Rimfolk_Thought_OldP_BeastSpoke");
                    OldPowers.Card(map, "Beast", OldPowers.T("Beast_Text", beast.LabelNoCount.Named("BEAST"), who.LabelShort.Named("HEARER"), (p.note ?? "").Named("WORDS")),
                        new LookTargets(who), LetterDefOf.NeutralEvent);
                    // It walks off, an ordinary beast again.
                    if (CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && beast.CanReach(c, PathEndMode.OnCell, Danger.Some), map, 0f, out var edge))
                        OwnHappenings.Lead(beast, edge, GenDate.TicksPerHour * 2);
                    OldPowers.End(b);
                    return;
                }
            }
        }

        // ================= A god going by =================

        /// <summary>The god of the colony's drawn nearest, with signs of its own that can show here; or null.</summary>
        private static God PassingGod(Map map, out float near)
        {
            near = 0f;
            if (!Gods.On) return null;
            God best = null;
            foreach (var g in Gods.ColonyGods(map))
            {
                if (g == null || g.kind == GodKind.Dead) continue;
                var own = GodShapes.Omens(g);
                if (own == null || !own.Any(k => GodOmens.Can(map, k))) continue;
                float n = GodNear.Of(g, map);
                if (best != null && n <= near) continue;
                best = g;
                near = n;
            }
            return best;
        }

        private static bool GodsBusy(Map map)
        {
            var gb = GodsOnMapBook.Instance;
            return gb == null || GodComings.Busy(gb) || GodPresenceOwn.On(map) != null || gb.omens.Count > 0;
        }

        public static GodPaceMath.Trigger GodPassesCall(Map map)
        {
            var g = PassingGod(map, out float near);
            return g == null ? GodPaceMath.Trigger.None : OldPowersMath.GodPassesTrigger(near, OldPowers.Moonless, GodsBusy(map));
        }

        public static bool GodPasses(OldPowersBook b, Map map)
        {
            var g = PassingGod(map, out _);
            var gb = GodsOnMapBook.Instance;
            if (g == null || gb == null) return false;
            var kinds = GodShapes.Omens(g).Where(k => GodOmens.Can(map, k)).Distinct().Take(OldPowersMath.PassSignsAtMost).ToList();
            if (kinds.Count == 0) return false;
            int hour = GenLocalDate.HourOfDay(map);
            int planned = 0;
            for (int i = 0; i < kinds.Count && gb.omens.Count < GodsOnMapBook.MaxOmens; i++)
            {
                int inHours = (OldPowersMath.PassSignAt(i) - hour + 24) % 24;
                gb.omens.Add(new GodOmenNote { god = g.id, map = map, kind = kinds[i], at = Now + Math.Max(1, inHours) * GenDate.TicksPerHour - GenDate.TicksPerHour / 2 });
                planned++;
            }
            if (planned == 0) return false;
            int toMorning = (OldPowersMath.PassMorning - hour + 24) % 24;
            var p = OldPowers.Play(b, OldPower.GodPasses, map, toMorning + 5, Math.Max(1, toMorning) * GenDate.TicksPerHour);
            p.god = g.id;
            p.count = planned;
            p.stage = 1;
            b.Came(OldPower.GodPasses, Now, g.Key, g.id.ToString());
            return true;
        }

        internal static void GodPassesStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            var g = Gods.ById(p.god);
            int hour = GenLocalDate.HourOfDay(map);
            if (g == null)
            {
                OldPowers.End(b);
                return;
            }
            if (hour < OldPowersMath.PassMorning || hour > OldPowersMath.PassMorning + 3)
            {
                OldPowers.Next(b, p, 1250);
                return;
            }
            // At dawn, the one who keeps it most stands at the edge of the colony looking out, and says it went by.
            var keeper = map.mapPawns.FreeColonistsSpawned.Where(x => OwnHappenings.Ours(x) && OldPowers.Awake(x)).OrderByDescending(x => PawnGods.HoldOf(x, g)).FirstOrDefault();
            string name = Gods.CardName(g, map);
            if (keeper != null)
            {
                if (OwnHappenings.Yields(keeper)) OwnHappenings.Stand(keeper, keeper.Position + IntVec3.North * 6, 600);
                string moment = name != null && PawnGods.HoldOf(keeper, g) > 0f ? "godpasses,dawn,named" : Worship.Scoffer(keeper) ? "godpasses,dawn,scoffer" : "godpasses,dawn";
                OldPowers.Say(keeper, moment, (name ?? "").Named("GOD"));
                GodsOnMapWords.Saw(g, map, keeper.Position, GodActKind.Omen, keeper, OldPowers.K("GodPasses_Saw"));
            }
            string title = GodsOnMapWords.Title(g, map, false, OldPowers.K("GodPasses_Title"), OldPowers.K("GodPasses_TitleNamed"));
            string text = OldPowers.T("GodPasses_Text", p.count.ToString().Named("COUNT"));
            text += " " + (name != null && keeper != null && PawnGods.HoldOf(keeper, g) > 0f
                ? OldPowers.T("GodPasses_Named", keeper.LabelShort.Named("KEEPER"))
                : OldPowers.T("GodPasses_Unnamed"));
            OldPowers.Card(map, "GodPasses", text, keeper != null ? new LookTargets(keeper) : LookTargets.Invalid, LetterDefOf.NeutralEvent, title);
            OldPowers.End(b);
        }
    }
}
