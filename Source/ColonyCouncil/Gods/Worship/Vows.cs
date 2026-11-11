using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Vows (docs/design/gods.md, "Worship": vows; brainstorm 15), made with the core's Gods.Vow and kept here by what they are:
    /// <list type="bullet">
    /// <item>No meat until the sick recover: sometimes, when someone prays for one close to them who is gravely ill
    /// (WorshipMath.NoMeatVowChance), never for one who eats no meat already or eats only meat. They ask the council for
    /// vegetarian meals in the stores (a wish; the vegetarian simple meals have their use). Kept when the sick get well, or
    /// die, or after twenty days; broken the moment they eat meat.</item>
    /// <item>A pilgrimage: a life goal. On a free day they walk out to the god's holy place on the colony's land (the oldest
    /// tree for the green, the water's edge for the waters, the graves for the dead, the far edge of the land), pray an
    /// hour there, and come home: kept.</item>
    /// <item>A temple: a life goal, kept when a place dedicated to their god stands in a fine room indoors.</item>
    /// <item>A beast spared: a hunter with a god whose sacred beast walks the colony's land vows that the colony won't hunt its
    /// kind for a season; broken if one of the colony kills one.</item>
    /// </list>
    /// A kept vow pleases the god (more for a temple or a pilgrimage) and gives pride; a broken one displeases it and gives
    /// shame, and a god whose nature takes a broken word hard may show its anger (WorshipMath.WrathAfterBroken), through the
    /// presence part's moment or a plain card. A life goal let go breaks its vow.
    /// </summary>
    public static class Vows
    {
        /// <summary>How much likelier a vow of this kind is to this god: half again for the vows its own worship def names, less for the others; as usual without one.</summary>
        public static float Leaning(God g, VowKind k)
        {
            var own = GodWorshipDef.For(g)?.vows;
            if (own.NullOrEmpty()) return 1f;
            return own.Contains(k) ? 1.5f : 0.6f;
        }

        public static VowRecord Open(Pawn p, VowKind k) => Worship.Book?.vows.FirstOrDefault(v => v != null && !v.done && v.pawn == p && v.kind == k);

        public static bool HasOpen(Pawn p, VowKind k) => Open(p, k) != null;

        /// <summary>The words of a vow, for the core's record and the news: "no meat until Ana is well".</summary>
        private static string What(VowKind k, string about, ThingDef beast) =>
            ("CC_Wsh_Vow_What_" + k).Translate((about ?? "").Named("ABOUT"), (beast?.label ?? "").Named("BEAST")).Resolve();

        /// <summary>Makes a vow: the core's record, the kind kept here, the news and their journal.</summary>
        public static VowRecord Make(Pawn p, God g, VowKind k, Pawn about = null, ThingDef beast = null)
        {
            var b = Worship.Book;
            if (b == null || p == null || g == null || HasOpen(p, k)) return null;
            int days = WorshipMath.VowDays(k);
            var core = Gods.Vow(g, p, What(k, about?.LabelShort, beast), days);
            if (core == null) return null;
            var r = new VowRecord
            {
                pawn = p, god = g.id, since = core.since, kind = k, about = about, aboutName = about?.LabelShort, beast = beast,
                until = days > 0 ? core.until : -1
            };
            b.vows.Add(r);
            var map = p.MapHeld;
            string text = ("CC_Wsh_Vowed_" + k).Translate(p.LabelShort.Named("PAWN"), Gods.FullName(g).Named("GOD"), (about?.LabelShort ?? "").Named("ABOUT"),
                (beast?.label ?? "").Named("BEAST")).Resolve();
            Tell.Minor(NewsKind.People, Importance.Notable, map, text, p);
            if (p.Spawned) Worship.Say(p, "Rimfolk_Pool_Worship_Vow", g, null, "vow:" + k.ToString().ToLowerInvariant());
            CouncilGameComponent.Instance?.journals.WriteText(p, text);
            if (k == VowKind.NoMeat) MealsWish(p);
            return r;
        }

        /// <summary>A vow kept or broken: the core's favour, the pride or the shame, the greater vows' more, and now and then the god's wrath.</summary>
        public static void End(VowRecord r, bool kept, string how = null)
        {
            if (r == null || r.done) return;
            r.done = true;
            var core = Gods.Vows.FirstOrDefault(v => v != null && v.Open && v.pawn == r.pawn && v.god == r.god && v.since == r.since);
            if (core != null) Gods.EndVow(core, kept);
            var g = Gods.ById(r.god);
            var p = r.pawn;
            var map = p?.MapHeld;
            if (g == null || p == null) return;
            string key = (kept ? "CC_Wsh_VowKept_" : "CC_Wsh_VowBroken_") + r.kind;
            string text = key.Translate(p.LabelShort.Named("PAWN"), Gods.FullName(g).Named("GOD"), (r.aboutName ?? "").Named("ABOUT"), (r.beast?.label ?? "").Named("BEAST"),
                (how ?? "").Named("HOW")).Resolve();
            if (!p.Dead)
                CouncilGameComponent.GiveThought(p, kept ? WorshipDefOf.Rimfolk_Worship_VowKept : WorshipDefOf.Rimfolk_Worship_VowBroken);
            Tell.Minor(NewsKind.People, Importance.Notable, map, text, p);
            CouncilGameComponent.Instance?.journals.WriteText(p, Worship.Line(kept ? "Rimfolk_Pool_Worship_VowKeptJournal" : "Rimfolk_Pool_Worship_VowBrokenJournal", p, g,
                null, "vow:" + r.kind.ToString().ToLowerInvariant()) ?? text);
            if (kept && (r.kind == VowKind.Temple || r.kind == VowKind.Pilgrimage))
            {
                Worship.Favour(g, map, WorshipMath.GreatVowFavour, text, FavourSource.Rite);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, p));
            }
            if (!kept && WorshipMath.WrathAfterBroken(g.nature, Rand.Value))
            {
                Gods.AddFavour(g, map, WorshipMath.WrathFavour, "CC_Wsh_Why_Wrath".Translate(p.LabelShort.Named("PAWN")).Resolve(), FavourSource.Vow, GodOffence.BrokenWord);
                string wrath = ("CC_Wsh_Wrath_" + Worship.Step).Translate(p.LabelShort.Named("PAWN"), Gods.FullName(g).Named("GOD")).Resolve();
                Worship.Sign(g, map, p.PositionHeld, PresenceKind.Wrath, "CC_Wsh_Wrath_Title".Translate(Gods.Name(g).Named("GOD")).Resolve(), wrath, p, false, GodActKind.Wrath);
            }
        }

        // ================= Making them =================

        /// <summary>
        /// A prayer for someone close and gravely ill may become a vow of no meat (never for one whose faith or body keeps
        /// them from meat already, or who eats nothing but meat).
        /// </summary>
        public static void MaybeNoMeat(WorshipBook b, Pawn p, Pawn sick, Map map)
        {
            if (p == null || sick == null || HasOpen(p, VowKind.NoMeat) || p.DevelopmentalStage.Juvenile()) return;
            if (Beliefs.Forbids(p, Belief.NoMeat) || Beliefs.Forbids(p, Belief.MeatOnly) || Beliefs.Forbids(p, Belief.NoAnimalProducts)) return;
            var g = Worship.GodOf(p, GodDomain.Healing);
            if (g == null || !Rand.Chance(WorshipMath.NoMeatVowChance * Leaning(g, VowKind.NoMeat))) return;
            Make(p, g, VowKind.NoMeat, sick);
        }

        /// <summary>The vow's wish: vegetarian meals in the stores, asked of the council (Rimfolk_Wish_VowMeals) when the colony keeps none to speak of.</summary>
        private static void MealsWish(Pawn p)
        {
            try
            {
                var def = DefDatabase<WishDef>.GetNamedSilentFail("Rimfolk_Wish_VowMeals");
                var comp = CouncilGameComponent.Instance;
                if (def == null || comp == null || !ColonyCouncilMod.Settings.enableWishes || comp.OpenWishBy(p) != null || !CouncilGameComponent.CanVoiceWishes(p)) return;
                var c = def.Worker.CandidatesFor(p).FirstOrDefault();
                if (c == null) return;
                comp.CreateWish(def, p, p, c, "vow");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: the vow's meals", e);
            }
        }

        /// <summary>Someone who may vow a great thing to their god (a pilgrimage, a temple): grown, with a god, devout or the priest, never a scoffer.</summary>
        public static bool CanVow(Pawn p, VowKind k)
        {
            if (!Worship.On || p == null || !Worship.Ours(p) || p.DevelopmentalStage.Juvenile() || Worship.Scoffer(p)) return false;
            if (!Worship.Devout(p) && !Worship.IsPriest(p)) return false;
            var g = Worship.GodOf(p);
            if (g == null || g.kind == GodKind.Dead || HasOpen(p, k)) return false;
            if (k == VowKind.Temple) return p.MapHeld != null && Shrines.Temple(p.MapHeld, g) == null;
            return true;
        }

        // ================= The hour and the day =================

        public static void Hourly(WorshipBook b, Map map, int now)
        {
            foreach (var r in b.vows.Where(v => v != null && !v.done && v.pawn != null && v.pawn.MapHeld == map).ToList())
            {
                switch (r.kind)
                {
                    case VowKind.NoMeat:
                        if (r.about == null || r.about.Dead) End(r, true, "CC_Wsh_How_ToTheEnd".Translate((r.aboutName ?? "").Named("ABOUT")).Resolve());
                        else if (Prayers.Grave(r.about) == null) End(r, true);
                        else if (r.until > 0 && now >= r.until) End(r, true, "CC_Wsh_How_Days".Translate().Resolve());
                        break;
                    case VowKind.BeastSpared:
                        if (r.until > 0 && now >= r.until) End(r, true);
                        break;
                    case VowKind.Temple:
                        var g = Gods.ById(r.god);
                        if (g != null && Shrines.Temple(map, g) != null) End(r, true);
                        break;
                    case VowKind.Pilgrimage:
                        TrySetOut(r, map, now);
                        break;
                }
            }
        }

        /// <summary>The region's day: a life goal let go breaks its vow; a pilgrimage made keeps it; a hunter may vow to spare a sacred beast.</summary>
        public static void Daily(WorshipBook b, Map map, int now)
        {
            var goals = CouncilGameComponent.Instance?.aspirations;
            foreach (var r in b.vows.Where(v => v != null && !v.done && (v.kind == VowKind.Pilgrimage || v.kind == VowKind.Temple) && v.pawn?.MapHeld == map).ToList())
            {
                if (r.kind == VowKind.Pilgrimage && b.Count(r.pawn, "pilgrimage", r.since, r.god) > 0)
                {
                    End(r, true);
                    continue;
                }
                if (goals == null) continue;
                var active = goals.Active(r.pawn);
                bool holds = active?.def?.Worker is AspirationWorker_GodVow w && w.Kind == r.kind;
                if (!holds && !goals.Fulfilled(r.pawn).Any(a => a.def?.Worker is AspirationWorker_GodVow fw && fw.Kind == r.kind && a.doneTick >= r.since))
                    End(r, false, "CC_Wsh_How_GoalLost".Translate().Resolve());
            }
            BeastVow(b, map, now);
        }

        private static void BeastVow(WorshipBook b, Map map, int now)
        {
            if (!Interplay.Ready("worship-beastvow|" + Gods.HomeKey(map), 10f)) return;
            foreach (var g in Gods.ColonyGods(map))
            {
                var def = GodDomainDef.Of(g.domain);
                if (def?.Animals == null || def.Animals.Count == 0) continue;
                var beast = map.mapPawns.AllPawnsSpawned.FirstOrDefault(a => a?.RaceProps != null && a.RaceProps.Animal && a.Faction == null && !a.Dead && def.Animals.Contains(a.def));
                if (beast == null) continue;
                var hunter = map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p) && !p.DevelopmentalStage.Juvenile() && Worship.GodOf(p) == g
                                                                         && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hunting) && !HasOpen(p, VowKind.BeastSpared)
                                                                         && !Beliefs.Forbids(p, Belief.NoHunting))
                    .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();
                if (hunter == null || !Rand.Chance(WorshipMath.BeastVowChance * Leaning(g, VowKind.BeastSpared))) continue;
                Make(hunter, g, VowKind.BeastSpared, null, beast.def);
                return;
            }
        }

        // ================= Eating, killing =================

        /// <summary>Someone ate (Thing.Ingested): meat breaks a vow of no meat.</summary>
        public static void Ate(Pawn p, Thing food)
        {
            var r = Open(p, VowKind.NoMeat);
            if (r == null || food == null) return;
            if (FoodUtility.GetFoodKind(food) != FoodKind.Meat) return;
            End(r, false, "CC_Wsh_How_Ate".Translate(food.LabelNoCount.Named("FOOD")).Resolve());
        }

        /// <summary>An animal died at one of the colony's hands: a vow to spare its kind is broken.</summary>
        public static void Killed(Pawn victim, Pawn killer)
        {
            var b = Worship.Book;
            if (b == null || victim?.def == null || killer == null || killer.Faction != Faction.OfPlayer) return;
            foreach (var r in b.vows.Where(v => v != null && !v.done && v.kind == VowKind.BeastSpared && v.beast == victim.def).ToList())
                End(r, false, "CC_Wsh_How_Killed".Translate(killer.LabelShort.Named("KILLER")).Resolve());
        }

        // ================= Pilgrimage =================

        /// <summary>The god of someone's pilgrimage under way (for the job's report).</summary>
        public static God PilgrimGod(Pawn p) => Gods.ById(Open(p, VowKind.Pilgrimage)?.god ?? -1) ?? Worship.GodOf(p);

        private static void TrySetOut(VowRecord r, Map map, int now)
        {
            var p = r.pawn;
            if (p == null || !p.Spawned || p.Map != map) return;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 7 || hour > 13 || !Rand.Chance(0.2f)) return;
            if (!WorshipJobs.Free(p) || p.CurJobDef?.defName == WorshipJobs.PilgrimJob) return;
            var g = Gods.ById(r.god);
            if (g == null) return;
            var place = HolyPlace(p, g);
            if (!place.IsValid) return;
            var def = WorshipJobs.Def(WorshipJobs.PilgrimJob);
            if (def == null) return;
            var job = JobMaker.MakeJob(def, place, p.Position);
            job.count = Rand.Range(1500, 2500);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            WorshipJobs.Start(p, job);
        }

        /// <summary>
        /// The god's holy place on the colony's land: the oldest tree for the green and the hunt, the water's edge for the
        /// waters, a grave for the dead, else the far edge of the land; reachable, out in the open, a good walk from home.
        /// </summary>
        public static IntVec3 HolyPlace(Pawn p, God g)
        {
            var map = p.Map;
            try
            {
                if (g.domain == GodDomain.Wild || g.domain == GodDomain.Hunt || g.second == GodDomain.Wild)
                {
                    Plant oldest = null;
                    foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
                    {
                        if (!(t is Plant pl) || !pl.def.plant.IsTree || pl.Growth < 0.95f || pl.Position.InHorDistOf(p.Position, 25f)) continue;
                        if (pl.Map.zoneManager.ZoneAt(pl.Position) != null || !p.CanReach(pl, PathEndMode.Touch, Danger.None)) continue;
                        if (oldest == null || pl.Age > oldest.Age) oldest = pl;
                    }
                    if (oldest != null)
                        foreach (var c in GenAdj.CellsAdjacent8Way(oldest))
                            if (c.InBounds(map) && c.Standable(map) && p.CanReach(c, PathEndMode.OnCell, Danger.None)) return c;
                }
                if (g.domain == GodDomain.Waters || g.second == GodDomain.Waters)
                    if (CellFinder.TryFindRandomCellNear(p.Position, map, 80, c => !c.Fogged(map) && c.GetTerrain(map).IsWater && c.Standable(map) && !c.InHorDistOf(p.Position, 25f)
                                                                            && p.CanReach(c, PathEndMode.OnCell, Danger.None), out var w))
                        return w;
                if (g.domain == GodDomain.Dead || g.second == GodDomain.Dead)
                {
                    var grave = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(x => x.HasCorpse)
                        .OrderByDescending(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                    if (grave != null)
                        foreach (var c in GenAdj.CellsAdjacent8Way(grave))
                            if (c.InBounds(map) && c.Standable(map) && p.CanReach(c, PathEndMode.OnCell, Danger.None)) return c;
                }
                if (CellFinder.TryFindRandomCellNear(p.Position, map, 70, c => !c.Fogged(map) && c.Standable(map) && !c.Roofed(map) && !c.InHorDistOf(p.Position, 40f)
                                                                        && p.CanReach(c, PathEndMode.OnCell, Danger.None), out var far))
                    return far;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a holy place", e);
            }
            return IntVec3.Invalid;
        }

        public static void PilgrimSetsOut(Pawn p)
        {
            var g = PilgrimGod(p);
            Worship.Say(p, "Rimfolk_Pool_Worship_PilgrimOut", g);
            var text = "CC_Wsh_PilgrimOut".Translate(p.LabelShort.Named("PAWN"), Gods.Name(g).Named("GOD")).Resolve();
            Tell.Minor(NewsKind.People, Importance.Flavor, p.Map, text, p);
        }

        public static void PilgrimArrives(Pawn p, IntVec3 cell)
        {
            var g = PilgrimGod(p);
            Worship.Say(p, "Rimfolk_Pool_Worship_PilgrimThere", g);
            Worship.Glow(p.Map, cell, g);
        }

        /// <summary>The hour's prayer at the holy place is done: the pilgrimage is made (the life goal reads it), the god pleased.</summary>
        public static void PilgrimageMade(Pawn p)
        {
            var b = Worship.Book;
            var r = Open(p, VowKind.Pilgrimage);
            if (b == null || r == null) return;
            var g = Gods.ById(r.god);
            if (g == null) return;
            b.AddMark(p, "pilgrimage", g.id);
            Worship.Favour(g, p.Map, 3f, "CC_Wsh_Why_Pilgrimage".Translate(p.LabelShort.Named("PAWN")).Resolve(), FavourSource.Rite);
            Interplay.Ease(p, 6f, StrainRelief.Quiet);
        }

        // ================= Dev =================

        public static void DevVow(Pawn p, VowKind k)
        {
            var g = Worship.GodOf(p);
            if (g == null)
            {
                Messages.Message("CC_Wsh_Dev_NoGod".Translate(p.LabelShort), p, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Pawn about = k == VowKind.NoMeat ? p.Map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => x != p) ?? p : null;
            ThingDef beast = k == VowKind.BeastSpared ? GodDomainDef.Of(g.domain)?.Animals?.FirstOrDefault() ?? DefDatabase<ThingDef>.GetNamedSilentFail("Deer") : null;
            Make(p, g, k, about, beast);
        }
    }

    // ================= The life goals =================

    /// <summary>
    /// A life goal that is a vow to their god (Defs/GodDefs/Rimfolk_Worship_Goals.xml): set, it makes the vow (the god's id
    /// kept as the goal's baseline); done when the book says so (a pilgrimage made, a temple standing); impossible once the
    /// god is gone silent.
    /// </summary>
    public abstract class AspirationWorker_GodVow : AspirationWorker
    {
        public abstract VowKind Kind { get; }

        public override bool CanHave(Pawn p) => Vows.CanVow(p, Kind);

        public override bool Init(Aspiration a)
        {
            var g = Worship.GodOf(a.pawn);
            if (g == null) return false;
            a.baseline = g.id;
            a.target = 1f;
            return Vows.Make(a.pawn, g, Kind) != null;
        }

        protected static God GodOf(Aspiration a) => Gods.ById(Mathf.RoundToInt(a.baseline));

        public override bool StillPossible(Aspiration a) => Worship.On && GodOf(a) is God g && g.kind != GodKind.Dead;
    }

    public class AspirationWorker_GodPilgrimage : AspirationWorker_GodVow
    {
        public override VowKind Kind => VowKind.Pilgrimage;

        public override float Progress(Aspiration a)
        {
            var g = GodOf(a);
            var b = Worship.Book;
            return g != null && b != null && b.Count(a.pawn, "pilgrimage", a.startedTick, g.id) > 0 ? 1f : 0f;
        }

        public override string ProgressText(Aspiration a) => (Progress(a) >= 1f ? "CC_Wsh_Goal_PilgrimDone" : "CC_Wsh_Goal_PilgrimNot").Translate(Gods.Name(GodOf(a)).Named("GOD")).Resolve();
    }

    public class AspirationWorker_GodTemple : AspirationWorker_GodVow
    {
        public override VowKind Kind => VowKind.Temple;

        public override float Progress(Aspiration a)
        {
            var g = GodOf(a);
            var map = a.pawn?.MapHeld;
            if (g == null || map == null) return 0f;
            if (Shrines.Temple(map, g) != null) return 1f;
            // How far the best of its places is from a temple: its room's impressiveness, indoors.
            float best = 0f;
            foreach (var s in Shrines.Of(map, g))
            {
                var room = s.thing.GetRoom();
                if (room == null || room.PsychologicallyOutdoors) continue;
                best = Mathf.Max(best, Mathf.Clamp01(room.GetStat(RoomStatDefOf.Impressiveness) / WorshipMath.TempleImpressiveness) * 0.95f);
            }
            return best;
        }

        public override string ProgressText(Aspiration a)
        {
            var g = GodOf(a);
            var map = a.pawn?.MapHeld;
            if (g == null || map == null) return null;
            int places = Shrines.Of(map, g).Count;
            return places == 0 ? "CC_Wsh_Goal_TempleNone".Translate(Gods.Name(g).Named("GOD"), Worship.ShrineWord.Named("SHRINE")).Resolve()
                : "CC_Wsh_Goal_TempleSome".Translate(Gods.Name(g).Named("GOD"), Progress(a).ToStringPercent().Named("PERCENT")).Resolve();
        }
    }

    // ================= The wish =================

    /// <summary>The vow's wish (Rimfolk_Wish_VowMeals): vegetarian meals in the stores, only while their vow of no meat holds.</summary>
    public class WishWorker_VowMeals : WishWorker_Stock
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!Vows.HasOpen(pawn, VowKind.NoMeat)) yield break;
            foreach (var c in base.CandidatesFor(pawn)) yield return c;
        }

        public override bool StillValid(Wish wish) => Vows.HasOpen(wish.Beneficiary, VowKind.NoMeat);
    }

    // ================= The hooks =================

    /// <summary>Someone of the colony ate: meat breaks a vow of no meat (Gods/Worship/Vows.cs).</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_VowAte
    {
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!RimfolkPatches.On || ingester == null || __instance == null || ingester.Faction != Faction.OfPlayer || !Worship.On) return;
            try
            {
                Vows.Ate(ingester, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a vow and a meal", e);
            }
        }
    }

    /// <summary>An animal killed by one of the colony: a vow to spare its kind is broken (Gods/Worship/Vows.cs).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_VowBeast
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!RimfolkPatches.On || __instance == null || !__instance.Dead || __instance.RaceProps?.Animal != true || !Worship.On) return;
            try
            {
                if (dinfo?.Instigator is Pawn killer) Vows.Killed(__instance, killer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a vow and a beast", e);
            }
        }
    }
}
