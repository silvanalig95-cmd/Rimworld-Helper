using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A people's call to arms on the colony (docs/design/stage2-diplomacy.md, items 1 and 2; decision 3): when their town
    /// within reach is attacked (a war world event at it, a Faction Territories invasion of it), a defence partner's letter
    /// asks for fighters, and so may a people the colony owes a debt of honour (FavourAsks, the need Fighters). Answered,
    /// two colonists or more set out within five days for their town by caravan; there the fight is on the game's caravan
    /// battle map against their attackers' band at half the home's threat points, with a few of theirs beside the colony's,
    /// or, with Faction Territories, its own invasion fight joined through the mod's own arrival. Arriving keeps it (the
    /// deed "keptWord"); refused or not set out in time, the pact is broken (a debt: refused). Nothing is saved but the ties
    /// and their letter.
    /// </summary>
    public static class CallsToArms
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static float Day => Now / (float)PactMath.DayTicks;

        // ================= Their town attacked =================

        /// <summary>
        /// A town of theirs within this home's reach under attack now: a Faction Territories invasion of it, or a running
        /// war of the bad kind (a warlord, the clans gathering) by a people at war with them, near it. With the attacker.
        /// </summary>
        public static bool Attacked(Faction f, Map home, out Settlement town, out Faction attacker)
        {
            town = null;
            attacker = null;
            var reach = home != null ? RegionTowns.Of(home) : null;
            if (f == null || reach == null) return false;
            foreach (var s in reach.towns)
            {
                if (s == null || s.Destroyed || s.Faction != f) continue;
                if (Territories.InvasionAt(s, out attacker) && attacker != null)
                {
                    town = s;
                    return true;
                }
            }
            var ws = WorldSituations.Instance;
            if (ws == null) return false;
            foreach (var sit in ws.Running)
            {
                if (sit?.def == null || sit.def.kind != "war" || !sit.def.bad || sit.ctx == null) continue;
                var by = sit.ctx.faction != null && sit.ctx.faction != f && sit.ctx.faction.HostileTo(f) ? sit.ctx.faction
                    : sit.ctx.enemy != null && sit.ctx.enemy != f && sit.ctx.enemy.HostileTo(f) ? sit.ctx.enemy : null;
                if (by == null || !Fields(by)) continue;
                var at = RiseFall.TileOf(sit.place);
                if (!at.Valid) continue;
                foreach (var s in reach.towns)
                    if (s != null && !s.Destroyed && s.Faction == f && s.Tile.Layer == at.Layer && Find.WorldGrid.ApproxDistanceInTiles(s.Tile, at) <= PactMath.WarTiles)
                    {
                        town = s;
                        attacker = by;
                        return true;
                    }
            }
            return false;
        }

        /// <summary>A people that can field a band: still standing, of people, with fighters (whatever its terms with the colony).</summary>
        public static bool Fields(Faction f) =>
            f != null && !f.IsPlayer && !f.defeated && f.def != null && f.def.humanlikeFaction && f.def.pawnGroupMakers != null
            && f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat);

        // ================= A defence partner's call =================

        /// <summary>
        /// The pact's hour (Pacts.DefenceHour) with no call of the colony's under way: a call of theirs answered or overdue,
        /// or, once a day, their town attacked and their letter asking for fighters.
        /// </summary>
        public static void PactHour(Tie t, Faction f, Map home, float day)
        {
            if (PactMath.ParseArms(t.terms.goods, out int townId, out float due))
            {
                var town = NotableBook.TownOf(townId);
                Watch(t, f, home, town, Refs.Faction(t.terms.about), due, day, true);
                return;
            }
            // A liege's call on the colony's levies (Fealty.cs) rides on the same hour, by the setting of its own.
            bool on = t.kind == Tie.Kind.Fealty ? Fealty.On : Pacts.On;
            if (!on || !Sched.FirstHourOfDay(Now) || !Tell.CanAsk) return;
            if (!Attacked(f, home, out var s, out var enemy) || !Rand.Chance(PactMath.ArmsDaily)) return;
            // One call for each attack: not again while the same war goes on against the same town (this session's asks, and
            // across a reload the history's call kept there against them lately).
            if (asked.Contains(Key(t.id, s.ID, enemy)) || KeptLately(s, enemy)) return;
            asked.Add(Key(t.id, s.ID, enemy));
            t.terms.goods = PactMath.ArmsCode(s.ID, day + PactMath.SetOutDays);
            t.terms.about = Refs.Of(enemy);
            WorldLedger.Touch(t);
            Diplomacy.Dirty();
            Letter(t, f, home, s, enemy, true);
        }

        private static readonly HashSet<string> asked = new HashSet<string>();

        private static string Key(int tie, int town, Faction enemy) => tie + ":" + town + ":" + (enemy?.loadID ?? -1);

        /// <summary>A call for this town against this enemy kept within the season (the history's dip:arms there): the same war.</summary>
        private static bool KeptLately(Settlement town, Faction enemy)
        {
            var against = Refs.Of(enemy);
            foreach (var e in History.At(Refs.Of(town), new[] { DiplomacyMath.Arms, DiplomacyMath.Levy }, Now - (int)(PactMath.SeasonDays * PactMath.DayTicks)))
                if (enemy == null || e.actors.Any(a => a.who == against && a.role == HistoryRole.Against))
                    return true;
            return false;
        }

        /// <summary>Their letter asking the colony for fighters: send them (by caravan), or refuse.</summary>
        public static void Letter(Tie t, Faction f, Map home, Settlement town, Faction enemy, bool pact)
        {
            var def = PactsDefOf.Rimfolk_CallToArms;
            if (def == null || t == null || f == null || town == null) return;
            var head = FavourCalls.HeadOf(f, home);
            string who = head != null ? NotableBook.Called(head) : PactWords.People(f);
            // A liege calling the colony's levies (Fealty.cs): its own words, the levies' forms by both ages.
            bool fealty = t.kind == Tie.Kind.Fealty;
            var form = fealty ? FealtyWords.LevyForm(home, f) : pact ? PactWords.PactForm(home, f) : DipWords.Form(home, f);
            string text = PactWords.T(fealty ? "CC_Arms_Fealty" : pact ? "CC_Arms_Pact" : "CC_Arms_Favour", who.Named("WHO"), PactWords.People(f).Named("PEOPLE"), town.Name.Named("TOWN"),
                              PactWords.People(enemy).Named("ENEMY"), form.word.Named("WORD"), form.rite.Named("RITE"))
                          + "\n\n" + PactWords.P("CC_Arms_How", town.Name.Named("TOWN"), PactMath.MinFighters.Named("COUNT"), CardWords.DistanceShort(town.Tile).Named("DISTANCE"))
                          + "\n\n" + PactWords.P(fealty ? "CC_Arms_Refuse_Fealty" : pact ? "CC_Arms_Refuse_Pact" : "CC_Arms_Refuse_Favour", PactWords.People(f).Named("PEOPLE"),
                              FealtyMath.LeviesRefusedGoodwill.Named("GOODWILL"));
            var letter = LetterMaker.MakeLetter(PactWords.P(fealty ? "CC_Arms_Label_Fealty" : "CC_Arms_Label", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD")), text, def,
                new LookTargets(town), f);
            if (letter is ChoiceLetter_CallToArms l)
            {
                l.tie = t.id;
                l.town = town.ID;
                l.home = Homes.KeyOf(home);
            }
            Tell.Ask(letter, home);
        }

        // ================= Watching for the colony's fighters =================

        /// <summary>
        /// An answered call (a pact's, or a debt's): a caravan of the colony's with two grown colonists or more bound for their
        /// town or there is on its way (the call waits for it); at the town (or beside it) the fight begins and the call is
        /// kept; nobody set out by its day, it is refused; the town gone, it is let go with nobody at fault.
        /// </summary>
        private static void Watch(Tie t, Faction f, Map home, Settlement town, Faction enemy, float due, float day, bool pact)
        {
            if (town == null || town.Destroyed || town.Faction != f)
            {
                Close(t, pact, null);
                return;
            }
            // Their caravan joined the fight through Faction Territories' own "Join fight", taken as its arrival: the colony's
            // people are on the town's map, and no caravan of theirs is left on the world map to be seen arriving.
            if (OnTownMap(town))
            {
                Arrived(t, f, home, town, enemy, null, pact);
                return;
            }
            var c = Answering(town);
            if (c != null)
            {
                // Faction Territories' invasion is joined at the town itself, through the mod's own arrival; the game's fight beside it.
                bool there = Territories.InvasionAt(town, out _) ? c.Tile == town.Tile : Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f;
                if (there)
                {
                    Arrived(t, f, home, town, enemy, c, pact);
                    return;
                }
                // On the road: the call waits for them.
                if (due < day + 1f) SetDue(t, town.ID, day + 1f, pact);
                return;
            }
            if (day > due) Refused(t, f, home, true, pact);
        }

        private static void SetDue(Tie t, int town, float due, bool pact)
        {
            if (pact) t.terms.goods = PactMath.ArmsCode(town, due);
            else t.terms.days = due;
            WorldLedger.Touch(t);
        }

        /// <summary>A caravan of the colony's with two grown colonists or more, bound for the town or within a tile of it.</summary>
        public static Caravan Answering(Settlement town)
        {
            if (town == null) return null;
            foreach (var c in Find.WorldObjects.Caravans)
            {
                if (c == null || !c.IsPlayerControlled || c.Tile.Layer != town.Tile.Layer) continue;
                int grown = c.PawnsListForReading.Count(p => p != null && p.IsColonist && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.Dead);
                bool bound = c.pather != null && c.pather.Moving && c.pather.Destination == town.Tile;
                bool there = Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f;
                if (PactMath.Answers(grown, bound || there)) return c;
            }
            return null;
        }

        /// <summary>Two grown colonists or more on the town's own map (only a fight there puts them on it).</summary>
        public static bool OnTownMap(Settlement town)
        {
            var map = town != null && town.HasMap ? town.Map : null;
            if (map?.mapPawns == null) return false;
            int grown = map.mapPawns.FreeColonistsSpawned.Count(p => p != null && !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.Dead);
            return PactMath.Answers(grown, true);
        }

        /// <summary>They got there: the fight begins (or, the danger past, they are thanked), and the call is kept. No caravan: in the fight there already.</summary>
        private static void Arrived(Tie t, Faction f, Map home, Settlement town, Faction enemy, Caravan c, bool pact)
        {
            string how;
            try
            {
                how = c != null ? Fight(f, home, town, enemy, c) : PactWords.P("CC_Arms_JoinedInvasion", town.Name.Named("TOWN"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calls to arms: the fight", e);
                how = null;
            }
            // A liege's levies come (Fealty.cs): the deed "levies" and the history's dip:levy.
            if (t.kind == Tie.Kind.Fealty) Fealty.LeviesCame(t, f, home, town, enemy);
            else
            {
                var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.For) };
                if (enemy != null) actors.Add(new HistoryActor(Refs.Of(enemy), HistoryRole.Against));
                Diplomacy.Write(DiplomacyMath.Arms, actors, Refs.Of(town), PactWords.P("CC_Arms_Hist", PactWords.People(f).Named("PEOPLE"), town.Name.Named("TOWN")), true);
                FactionMemory.Note(f, "keptWord", PactWords.P("CC_Arms_Deed", town.Name.Named("TOWN")));
            }
            Close(t, pact, Tie.State.Kept);
            if (how == null)
                // apart: the colony's caravan reached their town after the danger; told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, home, PactWords.P("CC_Arms_Late", PactWords.People(f).Named("PEOPLE"), town.Name.Named("TOWN")),
                    c != null ? new LookTargets(c) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The call is over: a pact's call cleared (the pact goes on), a debt's kept or let go; with <paramref name="state"/> null, let go with nobody at fault.</summary>
        private static void Close(Tie t, bool pact, Tie.State? state)
        {
            if (pact)
            {
                t.terms.goods = null;
                t.terms.about = Ref.None;
                WorldLedger.Touch(t);
            }
            else FavoursOwed.Close(t, state ?? Tie.State.Open);
            Diplomacy.Dirty();
        }

        /// <summary>Refused, or left unanswered past its day: a pact broken by the colony, a debt refused.</summary>
        public static void Refused(Tie t, Faction f, Map home, bool unanswered, bool pact)
        {
            if (!pact)
            {
                FavourAsks.Refuse(t, unanswered);
                return;
            }
            // A liege's call refused: their grudge, and the oath stands (Fealty.cs).
            if (t.kind == Tie.Kind.Fealty)
            {
                Fealty.LeviesRefused(t, f, home, unanswered);
                return;
            }
            t.terms.goods = null;
            WorldLedger.Touch(t);
            // Refused by the council's answer: the speaker who sends the refusal broke the colony's word, and takes it to heart if
            // trusting (Nature.cs), in the words of one who sent it; a call left unanswered was nobody's act, and with no home
            // left there is no speaker.
            if (Pacts.BrokenByUs(f, PactWords.P(unanswered ? "CC_Arms_By_Unanswered" : "CC_Arms_By_Refused")) && !unanswered && home != null)
                Nature.Felt(TruceParley.Speaker(home), NatureAct.BrokeTruce, f, null, "Call");
        }

        /// <summary>A debt of the colony's called in for fighters: watched the same way (FavourAsks.Hourly leaves it to this).</summary>
        public static void FavourHour(float day)
        {
            foreach (var t in FavourAsks.Waiting())
            {
                if (!FavourMath.ParseNeed(t.terms.goods, out var need) || need != FavourNeed.Fighters) continue;
                try
                {
                    var f = Refs.Faction(t.to);
                    var town = t.terms.about.kind == RefKind.Town ? Refs.Town(t.terms.about) : null;
                    var enemy = t.terms.count > 0 ? Refs.Faction(Ref.People(t.terms.count - 1)) : null;
                    Watch(t, f, FavoursOwed.HomeOf(t), town, enemy, t.terms.days, day, false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("calls to arms: a debt's fighters", e);
                }
            }
        }

        // ================= The fight there =================

        /// <summary>
        /// The colony's caravan at their town: with Faction Territories' invasion there, it joins the mod's own fight;
        /// else the game's caravan battle map beside the town, their attackers' band at half the home's threat points
        /// against the colony's people and a few of theirs. Returns the letter's words, or null when no attacker is left.
        /// </summary>
        public static string Fight(Faction f, Map home, Settlement town, Faction enemy, Caravan c)
        {
            if (Territories.JoinFight(c, town)) return PactWords.P("CC_Arms_JoinedInvasion", town.Name.Named("TOWN"));
            if (enemy == null || enemy.defeated || !enemy.HostileTo(f) || !Fields(enemy)) return null;
            // At peace with the colony: they draw off rather than fight its people.
            if (!enemy.HostileTo(Faction.OfPlayer))
            {
                string peace = PactWords.P("CC_Arms_NotOurs", town.Name.Named("TOWN"), PactWords.People(enemy).Named("ENEMY"));
                // apart: what the colony's caravan found at their town, told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, home, peace, new LookTargets(c), MessageTypeDefOf.NeutralEvent);
                return peace;
            }
            if (c.Tile == town.Tile && !MoveBeside(c, town)) return null;
            if (!CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(c.Tile)) return null;
            float points = StorytellerUtility.DefaultThreatPointsNow(home ?? Homes.Seat);
            var enemies = Make(enemy, points * PactMath.FightPoints, c.Tile);
            if (enemies.Count == 0) return null;
            var allies = Make(f, points * PactMath.AlliesPoints, c.Tile).Take(4).ToList();
            var map = CaravanIncidentUtility.SetupCaravanAttackMap(c, enemies, false);
            if (map == null) return null;
            LordMaker.MakeNewLord(enemy, new LordJob_AssaultColony(enemy, true, false), map, enemies);
            var anchor = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (anchor != null && allies.Count > 0)
            {
                foreach (var p in allies) GenSpawn.Spawn(p, CellFinder.RandomSpawnCellForPawnNear(anchor.Position, map), map);
                LordMaker.MakeNewLord(f, new LordJob_AssistColony(f, anchor.Position), map, allies);
            }
            else foreach (var p in allies) Arrivals.Discard(p);
            Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            var form = PactWords.PactForm(home, f);
            Tell.Letter(NewsKind.World, Importance.Important, map, PactWords.P("CC_Arms_Fight_Label", town.Name.Named("TOWN")),
                PactWords.T("CC_Arms_Fight", PactWords.People(f).Named("PEOPLE"), town.Name.Named("TOWN"), PactWords.People(enemy).Named("ENEMY"), allies.Count.Named("ALLIES"),
                    form.word.Named("WORD")), LetterDefOf.ThreatBig, new LookTargets(enemies.Cast<Thing>()), enemy);
            return PactWords.P("CC_Arms_Fought", town.Name.Named("TOWN"));
        }

        /// <summary>Fighters of a people, as their people fight, worth these points (on this tile).</summary>
        private static List<Pawn> Make(Faction f, float points, PlanetTile tile)
        {
            var list = new List<Pawn>();
            if (f?.def?.pawnGroupMakers == null || !f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat)) return list;
            try
            {
                float min = f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat);
                var parms = new PawnGroupMakerParms { groupKind = PawnGroupKindDefOf.Combat, faction = f, points = Mathf.Max(points, min * 1.05f), tile = tile, generateFightersOnly = true };
                list.AddRange(PawnGroupMakerUtility.GeneratePawns(parms));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calls to arms: fighters made", e);
            }
            return list;
        }

        /// <summary>The caravan steps off the town's own tile to the open ground beside it, where the fight's map is made.</summary>
        private static bool MoveBeside(Caravan c, Settlement town)
        {
            var neighbours = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(town.Tile, neighbours);
            foreach (var n in neighbours.InRandomOrder())
            {
                if (!n.Valid || Find.WorldObjects.AnyWorldObjectAt(n) || Find.World.Impassable(n)) continue;
                c.pather?.StopDead();
                c.Tile = n;
                return true;
            }
            return false;
        }
    }

    /// <summary>A people's call for the colony's fighters, as a letter: send them (by caravan, within five days), or refuse.</summary>
    public class ChoiceLetter_CallToArms : ChoiceLetter
    {
        public int tie = -1;
        public int town = -1;
        public int home = HomesMath.None;

        private Tie Live(out bool pact)
        {
            pact = false;
            var p = Pacts.Get(tie) ?? Fealty.Get(tie);
            if (p != null && p.Live && PactMath.ParseArms(p.terms.goods, out int id, out _) && id == town)
            {
                pact = true;
                return p;
            }
            var d = FavoursOwed.Get(tie);
            if (d != null && d.state == Tie.State.CalledIn && FavourMath.ParseNeed(d.terms.goods, out var n) && n == FavourNeed.Fighters) return d;
            return null;
        }

        /// <summary>A pact's call still waiting for the colony's answer (the inbox's row; a debt's call is the favours' row).</summary>
        public bool WaitingOnPact => !ArchivedOnly && Live(out bool pact) is Tie t && pact && t.kind == Tie.Kind.Pact;

        /// <summary>A liege's call on the colony's levies still waiting for the colony's answer (Fealty's row).</summary>
        public bool WaitingOnLevies => !ArchivedOnly && Live(out bool pact) is Tie t && pact && t.kind == Tie.Kind.Fealty;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var t = ArchivedOnly ? null : Live(out _);
                if (t == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                var map = Homes.MapOf(home) ?? Homes.Seat;
                yield return new DiaOption(PactWords.P("CC_Arms_Opt_Send"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        if (Live(out _) == null) return;
                        // apart: the colony's answer to their letter; its caravan sets out on the map.
                        Tell.Message(NewsKind.World, Importance.Important, map, PactWords.P("CC_Arms_Msg_Send", (NotableBook.TownOf(town)?.Name ?? "?").Named("TOWN"),
                            PactMath.MinFighters.Named("COUNT")), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                        if (map != null) Find.WindowStack.Add(new Dialog_FormCaravan(map));
                    },
                    resolveTree = true
                };
                yield return new DiaOption(PactWords.P("CC_Arms_Opt_Refuse"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        var x = Live(out bool pact);
                        if (x == null) return;
                        var f = pact ? x.kind == Tie.Kind.Fealty ? Fealty.LiegeOf(x) : Pacts.PeopleOf(x) : Refs.Faction(x.to);
                        CallsToArms.Refused(x, f, map, false, pact);
                    },
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tie, "tie", -1);
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
        }
    }
}
