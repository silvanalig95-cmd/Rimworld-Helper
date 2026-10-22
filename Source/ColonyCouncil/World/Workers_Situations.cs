using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The world situations that need code beyond their defs: a leader dies, two claimants fight over a seat, a town is
    // founded or abandoned, a healer tours, mercenaries look for work, locusts, a fair, a war of creeds, peace talks.
    // The rest (plague, famine, trade, pilgrims, winter, a bandit road, herds, a silver rush...) are data only, with the
    // base worker or the small ones at the end of this file.

    // ===================== The old chief dies =====================

    /// <summary>
    /// A neighbor's leader ({OTHER}, {DEAD}) dies and the game picks the next ({X}). What the new one is like decides
    /// the mood: flags kind, greedy, harsh or steady, and sameCreed when they share the colony's creed. Kind and greedy
    /// leaders open trade (a beat reads the flag); a harsh one cools things.
    /// </summary>
    public class SituationWorker_Succession : WorldSituationWorker
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var f = s.ctx.faction;
            var old = f.leader;
            if (old == null || !LeaderReplaceable(f)) return false;
            // The game chooses the next leader its own way; nothing is changed if it can't.
            bool ok;
            try
            {
                ok = f.TryGenerateNewLeader();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event new leader", e);
                ok = false;
            }
            var next = f.leader;
            if (!ok || next == null || next == old || next.Dead)
            {
                f.leader = old;
                return false;
            }
            s.ctx.dead = old.LabelShort;
            s.ctx.other = old;
            s.ctx.stranger = next;
            // Not the leader any more, so the game sends no letter of its own.
            DieOffscreen(old);
            Nature(s, next);
            return true;
        }

        public static void Nature(WorldSituation s, Pawn p)
        {
            var t = p?.story?.traits;
            var abrasive = DefDatabase<TraitDef>.GetNamedSilentFail("Abrasive");
            string nature = "steady";
            if (t != null)
            {
                if (t.HasTrait(TraitDefOf.Kind)) nature = "kind";
                else if (t.HasTrait(TraitDefOf.Bloodlust) || t.HasTrait(TraitDefOf.Psychopath) || abrasive != null && t.HasTrait(abrasive)) nature = "harsh";
                else if (t.HasTrait(TraitDefOf.Greedy)) nature = "greedy";
            }
            s.flags.AddDistinct(nature);
            if (ModsConfig.IdeologyActive && p.Ideo != null && p.Ideo == Faction.OfPlayer.ideos?.PrimaryIdeo) s.flags.AddDistinct("sameCreed");
        }

        public override void Begin(WorldSituation s)
        {
            var f = s.ctx.faction;
            if (s.Has("kind")) Goodwill(f, 6);
            else if (s.Has("harsh")) Goodwill(f, -8);
            if (s.Has("sameCreed")) Goodwill(f, 5);
        }

    }

    // ===================== Two claimants =====================

    /// <summary>
    /// A neighbor's leader ({DEAD}) lies dying with no heir named, and two claim the seat:
    /// {X} (the heir, of the old leader's blood) and {OTHER} (a war captain). The colony may back one. When it's over the
    /// winner leads, the old leader dies, and the loser goes into exile. Flags: backHeir, backCaptain, and the result
    /// (heirWon or captainWon) once decided. Endings: heir / captain, each with Backed and Opposed variants.
    /// </summary>
    public class SituationWorker_Claimants : WorldSituationWorker
    {
        private static Pawn Old(WorldSituation s) => s.held.FirstOrDefault(p => p != s.ctx.stranger && p != s.ctx.other) ?? s.ctx.faction?.leader;

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var f = s.ctx.faction;
            var old = f.leader;
            if (old == null || !LeaderReplaceable(f)) return false;
            s.ctx.dead = old.LabelShort;
            var heir = MakePerson(s, f, f.def.fixedLeaderKinds?.RandomElementWithFallback(), new FloatRange(20f, 34f), false);
            var captain = MakePerson(s, f, null, new FloatRange(34f, 55f), true);
            if (heir == null || captain == null) return false;
            try
            {
                heir.relations?.AddDirectRelation(PawnRelationDefOf.Parent, old);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event heir", e);
            }
            s.ctx.stranger = heir;
            s.ctx.other = captain;
            // The old leader is kept until it's decided.
            s.Hold(old);
            return true;
        }

        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            if (s.ctx.stranger?.gender == Gender.Female) tags.Add("heirFemale");
            if (s.ctx.other?.gender == Gender.Female) tags.Add("captainFemale");
        }

        public override string Hourly(WorldSituation s)
        {
            if (!StillPossible(s)) return GoneEnding(s);
            var heir = s.ctx.stranger;
            var captain = s.ctx.other;
            bool heirOk = heir != null && !heir.Dead && !heir.Discarded;
            bool captainOk = captain != null && !captain.Dead && !captain.Discarded;
            if (!heirOk && !captainOk) return WorldSituations.Quietly;
            // One of them is gone: the other has the seat.
            if (!heirOk || !captainOk) return Decide(s, heirOk);
            return null;
        }

        public override SituationEnding TimeoutEnding(WorldSituation s)
        {
            float heirChance = s.Has("backHeir") ? 0.65f : s.Has("backCaptain") ? 0.35f : 0.5f;
            return def.Ending(Decide(s, Rand.Chance(heirChance)));
        }

        private string Decide(WorldSituation s, bool heirWins)
        {
            s.flags.AddDistinct(heirWins ? "heirWon" : "captainWon");
            string side = heirWins ? "heir" : "captain";
            string backed = heirWins ? "backHeir" : "backCaptain";
            string opposed = heirWins ? "backCaptain" : "backHeir";
            return s.Has(backed) ? side + "Backed" : s.Has(opposed) ? side + "Opposed" : side;
        }

        public override void Ended(WorldSituation s, SituationEnding ending)
        {
            var f = s.ctx.faction;
            if (f == null || f.defeated || ending == null) return;
            var winner = s.Has("heirWon") ? s.ctx.stranger : s.Has("captainWon") ? s.ctx.other : null;
            var loser = winner == s.ctx.stranger ? s.ctx.other : s.ctx.stranger;
            // Only one of their own, free to take it: never someone who has since joined the colony or sits in its cells.
            if (winner == null || winner.Dead || winner.Discarded || winner.Faction != f || winner.IsPrisoner) return;
            var old = Old(s);
            try
            {
                f.leader = winner;
                if (Find.WorldPawns.Contains(winner)) Find.WorldPawns.ForcefullyKeptPawns.Remove(winner);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event claimant", e);
            }
            if (old != null && old != winner && !old.Dead) DieOffscreen(old);
            // Exile: gone from the story.
            if (loser != null && !loser.Dead && !loser.Spawned) Vanish(loser);
        }
    }

    // ===================== A new town =====================

    /// <summary>
    /// Settlers of a neighboring people found a town near the colony ({PLACE}, {TALE}: which way and how far). The town
    /// is real and stays: the game's own settlement, made the way the game makes them. Numbers: town (its id).
    /// </summary>
    public class SituationWorker_NewTown : WorldSituationWorker
    {
        private static bool TryTile(Map map, out PlanetTile tile)
        {
            tile = PlanetTile.Invalid;
            if (map == null || !map.Tile.Valid) return false;
            var layer = map.Tile.Layer;
            int min = Mathf.Max(3, SettlementProximityGoodwillUtility.MaxDist + 1);
            return TileFinder.TryFindPassableTileWithTraversalDistance(map.Tile, min, min + 7, out tile,
                t => t.Layer == layer && !Find.WorldObjects.AnyWorldObjectAt(t) && TileFinder.IsValidTileForNewSettlement(t, null, false) && ClearOfColony(t));
        }

        /// <summary>
        /// Far enough from every one of the colony's homes that the game doesn't hold the town's nearness against the
        /// colony (it takes goodwill every quadrum from a colony settled close to a people's town, and that can end in war).
        /// Straight-line only, with a margin: this runs inside the tile search's flood fill, and asking the world for a
        /// walking distance here starts a second flood fill inside the first, which hangs the game.
        /// </summary>
        private static bool ClearOfColony(PlanetTile t)
        {
            float near = SettlementProximityGoodwillUtility.MaxDist + 1.5f;
            foreach (var home in Find.WorldObjects.Settlements)
            {
                if (home.Faction != Faction.OfPlayer || home.Tile.Layer != t.Layer) continue;
                if (Find.WorldGrid.ApproxDistanceInTiles(t, home.Tile) <= near) return false;
            }
            return true;
        }

        protected override bool CanHappen(Map map) => TryTile(map, out _);

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var f = s.ctx.faction;
            if (!TryTile(map, out var tile)) return false;
            Settlement town;
            try
            {
                town = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                town.SetFaction(f);
                town.Tile = tile;
                town.Name = SettlementNameGenerator.GenerateSettlementName(town, f.def.settlementNameMaker);
                Find.WorldObjects.Add(town);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event new town", e);
                return false;
            }
            s.SetNum("town", town.ID);
            s.ctx.placeName = town.Name;
            s.ctx.tale = Where(map, tile);
            return true;
        }

        public static string Where(Map map, PlanetTile tile)
        {
            string dir = Direction(map, tile);
            float d = LivingEvents.Distance(map.Tile, tile);
            string key = d <= 4f ? "CC_WS_Where_Near" : d <= 8f ? "CC_WS_Where_Days" : "CC_WS_Where_Far";
            return key.Translate(dir).Resolve();
        }

        public static Settlement Town(WorldSituation s)
        {
            int id = s.Num("town");
            return id == 0 ? null : Find.WorldObjects.Settlements.FirstOrDefault(t => t.ID == id && !t.Destroyed);
        }

        public override LookTargets StartLook(WorldSituation s)
        {
            var t = Town(s);
            return t != null ? new LookTargets(t) : base.StartLook(s);
        }

        protected override bool StillPossible(WorldSituation s) => base.StillPossible(s) && Town(s) != null;
    }

    // ===================== A town abandoned =====================

    /// <summary>
    /// A neighbor's town ({PLACE}) is being given up: the people leave over the days the situation lasts, and when it
    /// ends the town is gone from the map (an abandoned settlement in its place), unless the colony has business there
    /// then (a caravan on its way, a quest): then a few stay on ("holdouts"). Flags: why (drought, raids, sickness,
    /// mine, river). {ITEM}: the people's next town, where the refugees are headed.
    /// </summary>
    public class SituationWorker_Abandoned : WorldSituationWorker
    {
        private static readonly string[] Reasons = { "drought", "raids", "sickness", "mine", "river" };

        /// <summary>A town that can go without breaking anything: not the people's last, not ours to visit, not in a quest.</summary>
        public static bool Safe(Settlement t)
        {
            if (t == null || t.Destroyed || t.Faction == null || t.Faction.IsPlayer || t.def != WorldObjectDefOf.Settlement || t.HasMap) return false;
            if (t.Faction.def.HasRoyalTitles || t.Faction.defeated) return false;
            if (!t.questTags.NullOrEmpty()) return false;
            if (Find.WorldObjects.Settlements.Count(x => x.Faction == t.Faction && !x.Destroyed) < 2) return false;
            if (t.GetComponent<TradeRequestComp>()?.ActiveRequest ?? false) return false;
            foreach (var c in Find.WorldObjects.Caravans)
                if (c.Tile == t.Tile || c.pather != null && c.pather.Moving && c.pather.Destination == t.Tile) return false;
            // Transport pods or a shuttle on their way there.
            foreach (var pods in Find.WorldObjects.TravellingTransporters)
                if (pods.destinationTile == t.Tile) return false;
            foreach (var q in Find.QuestManager.QuestsListForReading)
            {
                if (q.State != QuestState.Ongoing && q.State != QuestState.NotYetAccepted) continue;
                if (q.QuestLookTargets.Any(l => l.HasWorldObject && l.WorldObject == t)) return false;
            }
            return true;
        }

        private static IEnumerable<Settlement> Candidates(Map map) =>
            Find.WorldObjects.Settlements.Where(t => t.HasName && t.Faction != null && !t.Faction.HostileTo(Faction.OfPlayer) && !t.Faction.Hidden
                                                     && t.Tile.Layer == map.Tile.Layer && LivingEvents.Distance(map.Tile, t.Tile) <= 30f
                                                     && !(WorldSituations.Instance?.Busy(t.Faction) ?? false) && Safe(t));

        protected override bool CanHappen(Map map) => Candidates(map).Any();

        public override bool Cast(WorldSituation s, Map map)
        {
            var town = Candidates(map).RandomElementByWeightWithFallback(t => Mathf.Lerp(2f, 0.4f, LivingEvents.Distance(map.Tile, t.Tile) / 30f));
            if (town == null) return false;
            s.ctx.map = map;
            s.ctx.faction = town.Faction;
            s.ctx.placeName = town.Name;
            s.ctx.tale = SituationWorker_NewTown.Where(map, town.Tile);
            s.ctx.item = TownsOf(town.Faction, map).Where(t => t != town && t.HasName).OrderBy(t => LivingEvents.Distance(town.Tile, t.Tile)).FirstOrDefault()?.Name ?? town.Faction.Name;
            s.SetNum("town", town.ID);
            var reasons = Reasons.Where(r => r != "mine" || !Avail.IsPreIndustrial || Rand.Chance(0.5f)).ToList();
            if ((map.TileInfo as SurfaceTile)?.Rivers.NullOrEmpty() ?? true) reasons.Remove("river");
            s.flags.Add(reasons.RandomElement());
            s.ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (!def.names.NullOrEmpty()) s.ctx.name = Pick(def.names, s);
            return true;
        }

        private static Settlement Town(WorldSituation s)
        {
            int id = s.Num("town");
            return Find.WorldObjects.Settlements.FirstOrDefault(t => t.ID == id && !t.Destroyed);
        }

        public override LookTargets StartLook(WorldSituation s)
        {
            var t = Town(s);
            return t != null ? new LookTargets(t) : LookTargets.Invalid;
        }

        public override string Hourly(WorldSituation s)
        {
            // Gone some other way (destroyed in a raid, taken over): the story ends with it.
            if (Town(s) == null) return WorldSituations.Quietly;
            return null;
        }

        public override SituationEnding TimeoutEnding(WorldSituation s)
        {
            var town = Town(s);
            if (town == null || !Safe(town)) return def.Ending("holdouts") ?? base.TimeoutEnding(s);
            return base.TimeoutEnding(s);
        }

        public override void Ended(WorldSituation s, SituationEnding ending)
        {
            if (ending == null || ending.key == "holdouts") return;
            var town = Town(s);
            if (town == null || !Safe(town)) return;
            try
            {
                var ruin = WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.AbandonedSettlement);
                ruin.Tile = town.Tile;
                ruin.SetFaction(town.Faction);
                town.Destroy();
                Find.WorldObjects.Add(ruin);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event abandoned town", e);
            }
        }
    }

    // ===================== A famous healer =====================

    /// <summary>
    /// A famous healer ({X}) tours the region, in {PLACE} now. Invited (for silver), they come for a day: they treat
    /// the colonist whose condition is worst ({A}: a lost limb, a chronic illness, an old scar), and the colony's own
    /// doctor ({B}) learns from them. {ITEM}: what was mended, in the game's words.
    /// </summary>
    public class SituationWorker_Healer : WorldSituationWorker
    {
        /// <summary>The colonist who needs the healer most: something a doctor can't easily fix.</summary>
        public static Pawn Patient(Map map) =>
            IncidentWorker_Happening.Colonists(map).Where(p => Serious(p) > 0).OrderByDescending(Serious).FirstOrDefault();

        /// <summary>How badly they need a healer: missing parts, chronic illness, addictions, scars (0: not at all).</summary>
        private static int Serious(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return 0;
            int score = 0;
            foreach (var h in set.hediffs)
            {
                if (h is Hediff_MissingPart mp && !mp.IsFreshNonSolidExtremity && mp.Part != null && !set.PartOrAnyAncestorHasDirectlyAddedParts(mp.Part)) score = Mathf.Max(score, 4);
                else if (h.def.chronic) score = Mathf.Max(score, 3);
                else if (h is Hediff_Addiction) score = Mathf.Max(score, 2);
                else if (h is Hediff_Injury inj && inj.IsPermanent()) score = Mathf.Max(score, 1);
            }
            return score;
        }

        protected override bool CanHappen(Map map) => Patient(map) != null;

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var x = MakePerson(s, s.ctx.faction, null, new FloatRange(42f, 70f), false);
            if (x == null) return false;
            var med = x.skills?.GetSkill(SkillDefOf.Medicine);
            if (med != null)
            {
                med.Level = Rand.RangeInclusive(16, 19);
                med.passion = Passion.Major;
            }
            s.ctx.stranger = x;
            s.ctx.a = Patient(map);
            s.ctx.b = VarietyUtil.Best(map, SkillDefOf.Medicine);
            return s.ctx.a != null;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            var map = ctx.map;
            if (beat.key == "offer")
            {
                ctx.a = Patient(map);
                if (ctx.a == null) return false;
                ctx.b = VarietyUtil.Best(map, SkillDefOf.Medicine);
                ctx.count = Silver(map, new IntRange(800, 1300));
                if (WorldSituations.Instance?.Helped(s.ctx.faction) ?? false) ctx.count = Mathf.RoundToInt(ctx.count * 0.5f / 10f) * 10;
                s.SetNum("price", ctx.count);
                return true;
            }
            if (beat.key == "visit")
            {
                var x = s.ctx.stranger;
                var patient = s.ctx.a != null && !s.ctx.a.Dead && s.ctx.a.IsColonist ? s.ctx.a : Patient(map);
                ctx.a = patient;
                ctx.b = VarietyUtil.Best(map, SkillDefOf.Medicine);
                if (ctx.b == ctx.a) ctx.b = null;
                if (x != null && !x.Dead && !x.Spawned)
                {
                    var lord = Arrivals.Visit(new List<Pawn> { x }, map, x.Faction ?? s.ctx.faction, null, 18f);
                    if (lord == null && x.Spawned) Arrivals.SendAway(new[] { x });
                }
                ctx.item = Treat(patient);
                if (ctx.item.NullOrEmpty())
                {
                    // Nothing left to mend (it healed, or they're gone): the healer won't take the colony's silver.
                    s.flags.AddDistinct("nothingToMend");
                    Couriers.PaySilver(map, IntVec3.Invalid, s.Num("price"), s.ctx.faction, x);
                }
                if (ctx.b != null)
                {
                    ctx.b.skills?.GetSkill(SkillDefOf.Medicine)?.Learn(4000f, true);
                    s.flags.AddDistinct("taught");
                }
                s.ctx.a = patient;
                s.ctx.b = ctx.b;
                s.ctx.item = ctx.item;
                // The healer leaves the next morning, and that's the end of it.
                s.pendingEnding = "visited";
                s.pendingTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
                return true;
            }
            return true;
        }

        /// <summary>Mends the patient's worst lasting condition (a few small things first, if those come before it). The game's words for it.</summary>
        private static string Treat(Pawn p)
        {
            if (p == null || p.Dead) return null;
            var said = new List<string>();
            try
            {
                for (int i = 0; i < 4 && Serious(p) > 0; i++)
                {
                    var what = HealthUtility.FixWorstHealthCondition(p, System.Array.Empty<HediffDef>());
                    if (what.NullOrEmpty()) break;
                    said.Add(what.Resolve());
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event healer", e);
            }
            return said.Count == 0 ? null : said.Last();
        }
    }

    // ===================== Mercenaries =====================

    /// <summary>
    /// A mercenary company ({NAME}, captain {X}) is looking for work. Hired, it rides against {ENEMY} (a warlord's people
    /// first, if one is rising), and comes back days later with a share of the spoils, or doesn't come back. A rising
    /// warlord whose people they beat may fall with them. Flags: hired, fed, againstWarlord, won, lost.
    /// </summary>
    public class SituationWorker_Mercenaries : WorldSituationWorker
    {
        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            // Against a rising warlord's people if there is one, else a people at war with the colony.
            var warlord = WorldSituations.Instance?.active.FirstOrDefault(w => !w.ended && w.def.Worker is WarlordWorker && w.ctx.faction != null && !w.ctx.faction.defeated);
            s.ctx.enemy = warlord?.ctx.faction ?? Find.FactionManager.AllFactionsListForReading
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && TownsOf(f, map).Any())
                .OrderBy(f => Distance(map, NearestTown(f, map))).FirstOrDefault();
            if (s.ctx.enemy == null) return false;
            if (warlord != null) s.flags.Add("againstWarlord");
            var x = MakePerson(s, s.ctx.faction, null, new FloatRange(30f, 55f), true);
            if (x == null) return false;
            s.ctx.stranger = x;
            return true;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key == "offer")
            {
                ctx.count = Silver(ctx.map, new IntRange(600, 1100));
                ctx.placeName = NearestTown(s.ctx.enemy, ctx.map)?.Name ?? s.ctx.enemy?.Name;
                s.ctx.placeName = ctx.placeName;
                return s.ctx.enemy != null && !s.ctx.enemy.defeated;
            }
            if (beat.key == "return")
            {
                float chance = 0.6f + (s.Has("againstWarlord") ? 0.05f : 0f);
                bool won = Rand.Chance(chance);
                s.Set(won ? "won" : "lost");
                s.pendingEnding = won ? "won" : "lost";
                s.pendingTick = Find.TickManager.TicksGame;
                if (won && s.Has("againstWarlord"))
                {
                    // The warlord was with his people when they came: he falls with them.
                    var w = WorldSituations.Instance?.active.FirstOrDefault(x => !x.ended && x.def.Worker is WarlordWorker && x.ctx.faction == s.ctx.enemy);
                    if (w != null && w.def.Ending("mercenaries") != null && Rand.Chance(0.5f) && w.ctx.stranger != null && !w.ctx.stranger.Spawned && !w.ctx.stranger.Dead)
                    {
                        w.ctx.other = s.ctx.stranger;
                        w.ctx.name = w.ctx.name ?? s.ctx.name;
                        s.flags.AddDistinct("warlordFell");
                        w.pendingEnding = "mercenaries";
                        w.pendingTick = Find.TickManager.TicksGame;
                    }
                }
                return false;
            }
            return true;
        }
    }

    // ===================== Locusts =====================

    /// <summary>
    /// A swarm of locusts works its way across the region. Each day it may pass over the colony and strip the open
    /// fields (roofed fields and greenhouses are safe; nobody is hurt). Smudge fires make it likelier to pass by and
    /// lighter when it doesn't. Flags: smoke, harvested, struck. Text keys: struckTitles, struckTexts.
    /// </summary>
    public class SituationWorker_Locusts : WorldSituationWorker
    {
        protected override bool CanHappen(Map map) => GameCondition_Hail.Crops(map).Count >= 20;

        public override string Daily(WorldSituation s)
        {
            if (s.Has("struck") || s.DaysIn < 1.5f) return null;
            var map = s.ctx.map;
            if (map == null || !Find.Maps.Contains(map)) return null;
            float chance = s.Has("smoke") ? 0.1f : 0.2f;
            if (!Rand.Chance(chance)) return null;
            var crops = GameCondition_Hail.Crops(map);
            if (crops.Count == 0) return null;
            float share = s.Has("smoke") ? 0.25f : 0.5f;
            int hit = 0;
            foreach (var p in crops)
            {
                if (!Rand.Chance(share)) continue;
                hit++;
                if (p.Growth < 0.3f) p.Kill(null, null);
                else
                {
                    p.Growth = Mathf.Max(0.05f, p.Growth - Rand.Range(0.3f, 0.6f));
                    map.mapDrawer.MapMeshDirty(p.Position, MapMeshFlagDefOf.Things);
                }
            }
            s.Set("struck");
            s.ctx.count = hit;
            string label = Text("struckTitles", s) ?? s.Label;
            string text = Text("struckTexts", s);
            if (!text.NullOrEmpty()) Find.LetterStack.ReceiveLetter(label, text, WorldSituationDefOf.Council_WorldNewsBad, LookTargets.Invalid);
            return null;
        }
    }

    // ===================== A great fair =====================

    /// <summary>
    /// A great fair at a neighbor's town ({PLACE}). The colony can send a cart of its surplus ({THING}, {COUNT}; paid
    /// when the fair closes, better than any trader pays) and put its best forward in a contest ({SKILL}, {A}).
    /// Flags: sent, entered, prize, noPrize.
    /// </summary>
    public class SituationWorker_Fair : WorldSituationWorker
    {
        private static readonly string[] ContestSkills = { "Artistic", "Crafting", "Cooking", "Shooting", "Animals", "Plants" };

        private static bool Sellable(ThingDef d) =>
            d != null && d != ThingDefOf.Silver && d.category == ThingCategory.Item && TradeabilityUtility.PlayerCanSell(d.tradeability) && !d.IsDrug && !d.IsCorpse
            && !d.IsMedicine && d.stackLimit >= 10 && d.BaseMarketValue >= 0.5f && d.BaseMarketValue <= 40f && d.GetCompProperties<CompProperties_Rottable>() == null;

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            var map = ctx.map;
            if (beat.key == "cart")
            {
                var pick = map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value >= 20 && Sellable(kv.Key) && kv.Value * kv.Key.BaseMarketValue >= 250f)
                    .RandomElementByWeightWithFallback(kv => kv.Value * kv.Key.BaseMarketValue);
                if (pick.Key == null) return false;
                float target = Rand.Range(300f, 650f) * LivingEvents.WealthFactor(map);
                ctx.thing = pick.Key;
                ctx.count = VarietyUtil.Round(Mathf.Clamp(Mathf.RoundToInt(target / pick.Key.BaseMarketValue), 10, Mathf.RoundToInt(pick.Value * 0.6f)));
                ctx.item = VarietyUtil.Bundle(pick.Key, ctx.count);
                s.ctx.thing = ctx.thing;
                s.ctx.item = ctx.item;
                return ctx.count >= 10;
            }
            if (beat.key == "contest")
            {
                var skills = ContestSkills.Select(n => DefDatabase<SkillDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
                foreach (var sk in skills.InRandomOrder())
                {
                    var best = VarietyUtil.Best(map, sk);
                    if (best == null || best.skills.GetSkill(sk).Level < 6) continue;
                    if (sk == SkillDefOf.Shooting && best.WorkTagIsDisabled(WorkTags.Violent)) continue;
                    ctx.a = best;
                    ctx.skill = sk;
                    ctx.contest = true;
                    s.ctx.skill = sk;
                    s.ctx.contest = true;
                    return true;
                }
                return false;
            }
            return true;
        }

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat.key == "contest" && s.Has("entered") && ctx.a != null && ctx.skill != null)
            {
                int level = ctx.a.skills?.GetSkill(ctx.skill)?.Level ?? 0;
                bool won = Rand.Chance(Mathf.Clamp(0.15f + (level - 6) * 0.07f, 0.1f, 0.85f));
                s.Set(won ? "prize" : "noPrize");
                // The contestant, for the closing letter (A belongs to whichever question came last).
                s.ctx.b = ctx.a;
            }
            return outcome;
        }
    }

    // ===================== A war of creeds =====================

    /// <summary>
    /// Two neighbors' creeds fall out in public (Ideology): {FACTION} and their {TALE}, {ENEMY} and their {ITEM}.
    /// Preachers come to the colony; it can take a side, host a debate or stay out. Flags: sideFaction, sideEnemy,
    /// debate, debateFailed, ourCreed (the colony shares {FACTION}'s creed), theirCreed (it shares {ENEMY}'s).
    /// </summary>
    public class SituationWorker_HolyWar : WorldSituationWorker
    {
        /// <summary>Two neighbors at peace with the colony whose creeds differ.</summary>
        private static List<(Faction, Faction)> Pairs(Map map)
        {
            var list = Candidates(new SituationFactionNeed { friendly = true, ideo = true }, null, map).ToList();
            var pairs = new List<(Faction, Faction)>();
            foreach (var a in list)
                foreach (var b in list)
                    if (a != b && a.ideos.PrimaryIdeo != b.ideos.PrimaryIdeo) pairs.Add((a, b));
            return pairs;
        }

        protected override bool CanHappen(Map map) => ModsConfig.IdeologyActive && Pairs(map).Count > 0;

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!ModsConfig.IdeologyActive || !base.Cast(s, map)) return false;
            var pair = Pairs(map).RandomElementWithFallback();
            if (pair.Item1 == null) return false;
            s.ctx.faction = pair.Item1;
            s.ctx.enemy = pair.Item2;
            s.ctx.placeName = NearestTown(pair.Item1, map)?.Name ?? pair.Item1.Name;
            var a = s.ctx.faction.ideos?.PrimaryIdeo;
            var b = s.ctx.enemy?.ideos?.PrimaryIdeo;
            if (a == null || b == null || a == b) return false;
            s.ctx.tale = a.name;
            s.ctx.item = b.name;
            var ours = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ours == a) s.flags.Add("ourCreed");
            else if (ours == b) s.flags.Add("theirCreed");
            return true;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            if (beat.key == "preachers") ctx.b = VarietyUtil.Best(ctx.map, SkillDefOf.Social);
            return true;
        }
    }

    // ===================== Peace talks =====================

    /// <summary>
    /// Two neighbors at war with each other ({FACTION}, {ENEMY}), both at peace with the colony, ask it to host talks.
    /// Hosted, the talks go on for days; the colony's best talker ({A}) helps. If they hold, the two make peace (for
    /// real: the game's own relation between them). Never with Factional War, which has wars and peace of its own.
    /// Flags: hosting, peace, failed.
    /// </summary>
    public class SituationWorker_PeaceTalks : WorldSituationWorker
    {
        private static bool CanMakePeace(Faction f) =>
            f != null && f.HasGoodwill && !f.def.permanentEnemy && !f.def.permanentEnemyToEveryoneExceptPlayer && f.def.permanentEnemyToEveryoneExcept.NullOrEmpty()
            && !f.def.HasRoyalTitles;

        /// <summary>Two peoples who could make peace: neither is anyone's enemy for good, nor the other's.</summary>
        private static bool CanMakePeace(Faction a, Faction b) =>
            a != b && CanMakePeace(a) && CanMakePeace(b) && !a.defeated && !b.defeated && !a.def.PermanentlyHostileTo(b.def) && !b.def.PermanentlyHostileTo(a.def);

        /// <summary>Two neighbors at peace with the colony and at war with each other.</summary>
        private static List<(Faction, Faction)> Pairs(Map map)
        {
            var friends = Candidates(new SituationFactionNeed { friendly = true }, null, map).Where(CanMakePeace).ToList();
            var pairs = new List<(Faction, Faction)>();
            foreach (var a in friends)
                foreach (var b in friends)
                    if (a.HostileTo(b) && CanMakePeace(a, b)) pairs.Add((a, b));
            return pairs;
        }

        protected override bool CanHappen(Map map) => Pairs(map).Count > 0;

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var pair = Pairs(map).RandomElementWithFallback();
            if (pair.Item1 == null) return false;
            s.ctx.faction = pair.Item1;
            s.ctx.enemy = pair.Item2;
            s.ctx.placeName = NearestTown(pair.Item1, map)?.Name ?? pair.Item1.Name;
            return true;
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            var map = ctx.map;
            if (beat.key == "ask")
            {
                ctx.count = VarietyUtil.Round(Mathf.RoundToInt(Rand.Range(40, 70) * Mathf.Max(1f, IncidentWorker_Happening.Colonists(map).Count / 6f)));
                ctx.a = VarietyUtil.Best(map, SkillDefOf.Social);
                return ctx.a != null;
            }
            if (beat.key == "talks")
            {
                var a = s.ctx.faction;
                var b = s.ctx.enemy;
                if (a == null || b == null || !a.HostileTo(b) || !CanMakePeace(a, b)) return false;
                var talker = VarietyUtil.Best(map, SkillDefOf.Social);
                s.ctx.a = talker;
                int level = talker?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                float chance = Mathf.Clamp(0.3f + level * 0.025f, 0.3f, 0.8f);
                if (Rand.Chance(chance))
                {
                    MakePeace(a, b);
                    s.Set("peace");
                    s.pendingEnding = "peace";
                }
                else
                {
                    s.Set("failed");
                    s.pendingEnding = "failed";
                }
                s.pendingTick = Find.TickManager.TicksGame;
                return false;
            }
            return true;
        }

        private static void MakePeace(Faction a, Faction b)
        {
            try
            {
                var ab = a.RelationWith(b, true);
                var ba = b.RelationWith(a, true);
                if (ab != null) ab.baseGoodwill = Mathf.Max(ab.baseGoodwill, 0);
                if (ba != null) ba.baseGoodwill = Mathf.Max(ba.baseGoodwill, 0);
                a.SetRelationDirect(b, FactionRelationKind.Neutral, false);
                b.SetRelationDirect(a, FactionRelationKind.Neutral, false);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event peace", e);
            }
        }

        protected override bool StillPossible(WorldSituation s) =>
            base.StillPossible(s) && s.ctx.enemy != null && !s.ctx.enemy.defeated && !s.ctx.enemy.HostileTo(Faction.OfPlayer);
    }

    // ===================== Small ones =====================

    /// <summary>Only where a river runs by the colony (a flood downriver).</summary>
    public class SituationWorker_River : WorldSituationWorker
    {
        protected override bool CanHappen(Map map) => !((map.TileInfo as SurfaceTile)?.Rivers).NullOrEmpty();
    }

    /// <summary>Only where winter bites (a hard winter): it's cold outside already.</summary>
    public class SituationWorker_Winter : WorldSituationWorker
    {
        protected override bool CanHappen(Map map) => map.mapTemperature.OutdoorTemp < 5f && map.mapTemperature.SeasonalTemp < 8f;
    }

    /// <summary>Only with fields to lose (a famine, a drought): the colony grows food in the open.</summary>
    public class SituationWorker_Fields : WorldSituationWorker
    {
        protected override bool CanHappen(Map map) => GameCondition_Hail.Crops(map).Count >= 10;
    }
}
