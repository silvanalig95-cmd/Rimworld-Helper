using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony asks for its old town back, by letter to its head (docs/design/stage3-many-colonies.md, section 2,
    /// "Taking it back"; release 2b): the letter purpose TakeBack in the neighbour letters' own machinery (World/
    /// NeighbourLetters.cs, through DiplomacyLetters: the writer at a table, the colony's means on the road, their messenger
    /// with the answer), written only to the head of a town the colony was that is still its people's, at peace with the
    /// colony, at most once a year, and while the game's colony limit allows one more home (release 1's 5B may raise it by
    /// one). The topic is "takeback:KEY", the town's key. Their answer weighs the head's regard of the colony, their
    /// people's goodwill, the town's fortune (a struggling town is readier), the years since and how much it has grown
    /// (<see cref="FormerMath.TakeBackChance"/>); a yes reaches the colony with their answer, and from then the town can
    /// be taken back from its card or its letter (<see cref="TownTakeBack"/>).
    /// </summary>
    public static class TownLetters
    {
        public const string Prefix = "takeback:";

        public static bool Owns(LetterPurpose p) => p == LetterPurpose.TakeBack;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The town the colony was this notable heads, still its people's, or null.</summary>
        public static LedgerFormer TownOf(Notable n)
        {
            if (n == null || n.role != NotableRole.Head || n.town < 0) return null;
            var rec = WorldLedger.FormerOf(n.town);
            return rec != null && rec.Standing && rec.end == TownEnd.None ? rec : null;
        }

        /// <summary>The letter's purpose is offered in the letter window only to the head of a town the colony was.</summary>
        public static bool Shows(Notable n) => TownOf(n) != null;

        /// <summary>Why the colony can't ask for it back now (in words), or null.</summary>
        public static string WhyNot(Notable n, Map home) => WhyNot(TownOf(n), n);

        public static string WhyNot(LedgerFormer rec, Notable head = null)
        {
            if (!HandOvers.On) return "CC_TownAfter_Why_Off".Translate();
            if (rec == null || !rec.Standing || rec.end != TownEnd.None) return "CC_TownAfter_Why_NotHead".Translate();
            string town = rec.Town?.Name ?? rec.colonyName ?? "?";
            if (rec.people.HostileTo(Faction.OfPlayer)) return "CC_TownAfter_Why_War".Translate(rec.people.Name.Named("PEOPLE")).Resolve();
            if (rec.agreed) return "CC_TownAfter_Why_Agreed".Translate(town.Named("TOWN")).Resolve();
            if (rec.askedAt >= 0 && Now - rec.askedAt < FormerMath.AskAgainDays * FormerMath.DayTicks)
                return "CC_TownAfter_Why_Lately".Translate(town.Named("TOWN")).Resolve();
            if (FoundingMath.LimitFor(Prefs.MaxNumberOfPlayerSettlements, Foundings.Counted()) < 0)
                return "CC_TownAfter_Why_Limit".Translate(Prefs.MaxNumberOfPlayerSettlements.Named("COUNT")).Resolve();
            return null;
        }

        /// <summary>The one thing a letter of this purpose is about: the town, the colony's home again.</summary>
        public static List<LetterTopic> Topics(Notable n)
        {
            var list = new List<LetterTopic>();
            var rec = TownOf(n);
            if (rec == null) return list;
            list.Add(new LetterTopic
            {
                key = Prefix + rec.key, text = "CC_TownAfter_Topic".Translate((rec.Town?.Name ?? rec.colonyName ?? "?").Named("TOWN")).Resolve()
            });
            return list;
        }

        /// <summary>The head of a town the colony was, to write to, or null.</summary>
        public static Notable HeadOf(LedgerFormer rec) =>
            rec == null ? null : NotableBook.Instance?.notables.FirstOrDefault(x => x != null && x.town == rec.key && x.role == NotableRole.Head && x.Alive);

        /// <summary>
        /// They have read it (or heard the call): yes or no by their own reckoning, the answer's tone following it (no is a
        /// refusal; yes never worse than cool). A yes reaches the colony with their answer (a call: within hours). Overtaken
        /// by events (the town no longer theirs, at war, agreed already), nothing is asked or refused.
        /// </summary>
        public static string Read(NeighbourLetter l, Notable n, ref AnswerTone tone)
        {
            var rec = l?.topicKey != null && l.topicKey.StartsWith(Prefix, StringComparison.Ordinal) && int.TryParse(l.topicKey.Substring(Prefix.Length), out int key)
                ? WorldLedger.FormerOf(key) : null;
            if (rec == null || !rec.Standing || rec.end != TownEnd.None || rec.agreed || rec.people.HostileTo(Faction.OfPlayer) || n?.faction != rec.people)
            {
                DiplomacyLetters.Moot(l);
                tone = AnswerTone.Cool;
                return "CC_TownAfter_Ans_Moot".Translate((n?.Name ?? "?").Named("NAME")).Resolve();
            }
            int now = Now;
            var town = rec.Town;
            string townName = town?.Name ?? rec.colonyName ?? "?";
            var ledgerTown = WorldLedger.TownRecord(rec.key);
            float prosperity = ledgerTown != null && ledgerTown.Seeded ? ledgerTown.prosperity : -1f;
            int otherTowns = Find.WorldObjects.Settlements.Count(s => s != null && s.Faction == rec.people && s.ID != rec.key);
            int regard = NotableBook.Opinion(n), goodwill = rec.people.PlayerGoodwill;
            float years = FormerMath.YearsSinceExact(rec.handedAt, now), grown = FormerMath.Grown(rec.seed, prosperity, otherTowns);
            float chance = FormerMath.TakeBackChance(regard, goodwill, prosperity, years, grown);
            rec.askedAt = now;
            WorldLedger.FormerChanged();
            if (Rand.Value >= chance)
            {
                tone = AnswerTone.Refusal;
                string why = FormerMath.TakeBackWhy(regard, goodwill, prosperity, years, grown);
                return "CC_TownAfter_Ans_No".Translate(n.Name.Named("NAME"), townName.Named("TOWN")).Resolve() + " "
                       + ("CC_TownAfter_No_" + why).Translate(n.Name.Named("NAME"), townName.Named("TOWN"), FormerMath.YearsSince(rec.handedAt, now).Named("YEARS")).Resolve();
            }
            if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            rec.agreed = true;
            rec.agreedTold = false;
            float days = l.channel == LetterChannel.Console ? Rand.Range(2f, 6f) / 24f : l.backDays + 0.25f;
            rec.agreedAt = now + (int)(days * GenDate.TicksPerDay);
            WorldLedger.FormerChanged();
            return "CC_TownAfter_Ans_Yes".Translate(n.Name.Named("NAME"), townName.Named("TOWN"), (rec.colonyName ?? townName).Named("COLONY")).Resolve();
        }
    }

    /// <summary>
    /// Taking a town the colony was back (docs/design/stage3-many-colonies.md, section 2, "Taking it back"; release 2b,
    /// the player's decision 10A).
    /// <list type="bullet">
    /// <item><b>By their word</b> (<see cref="Make"/>, from the town's card or the letter that says they agreed, inside a
    /// long event): the game's colony limit checked (release 1's 5B raises it by one at most); the settlement the colony's
    /// again, its map made the game's way for a home and the kept layout placed on it (Colonies/TownLayout.cs); its former
    /// colonists walk out of their rooms as colonists, and up to three of its other people who choose to come; the people,
    /// with no town left, is made temporary so the game lets it go (it stays a people if it has other towns, and leads on
    /// without them); the milestone colony:takenBack and a letter.</item>
    /// <item><b>By force</b>, as the game allows (<see cref="Watch"/>, in the region's hour): attacked, its map is made from
    /// the layout with its people at home (Patch_TownLayoutSettlement); beaten, the game's destroyed settlement keeps the
    /// map; the bond ends, and its people and every people of the same faith remember a heavy wrong (the faction memory's
    /// townTaken and townTakenFaith, the ledger's grudge); settled again with the game's Settle button, the milestone
    /// colony:takenBack.</item>
    /// </list>
    /// </summary>
    public static class TownTakeBack
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Why it can't be taken back now (in words), or null: their yes, the town still theirs, the colony limit.</summary>
        public static string WhyNot(LedgerFormer rec)
        {
            if (rec == null || !rec.Standing || rec.end != TownEnd.None) return "CC_TownAfter_Why_NotHead".Translate();
            if (!rec.agreed || Now < rec.agreedAt) return "CC_TownAfter_Why_NotAgreed".Translate();
            // Its map open (a quest, a visit by force): its people are on it, so it waits until the map closes.
            if (rec.Town?.HasMap ?? false) return "CC_TownAfter_Why_MapOpen".Translate(TownsAfter.TownName(rec).Named("TOWN")).Resolve();
            if (rec.people.HostileTo(Faction.OfPlayer)) return "CC_TownAfter_Why_War".Translate(rec.people.Name.Named("PEOPLE")).Resolve();
            if (FoundingMath.LimitFor(Prefs.MaxNumberOfPlayerSettlements, Foundings.Counted()) < 0)
                return "CC_TownAfter_Why_Limit".Translate(Prefs.MaxNumberOfPlayerSettlements.Named("COUNT")).Resolve();
            return null;
        }

        /// <summary>The player's own click: take it back now (a long event), or say why not.</summary>
        public static void Ask(LedgerFormer rec)
        {
            string why = WhyNot(rec);
            if (why != null)
            {
                // direct: the answer to the player's own click.
                Messages.Message(why, MessageTypeDefOf.RejectInput, false);
                return;
            }
            LongEventHandler.QueueLongEvent(() => Make(rec), "CC_TownAfter_Long", false, null);
        }

        /// <summary>
        /// The town is the colony's home again (see the class). Its former colonists who are its people's still and free come
        /// back to it; up to three of its other people choose to come with it.
        /// </summary>
        public static bool Make(LedgerFormer rec)
        {
            if (WhyNot(rec) != null) return false;
            var s = rec.Town;
            var people = rec.people;
            if (s == null) return false;
            int now = Now;
            try
            {
                // The game's colony limit, raised by one when the town would pass it (release 1's 5B).
                int pref = Prefs.MaxNumberOfPlayerSettlements;
                int limit = FoundingMath.LimitFor(pref, Foundings.Counted());
                bool raised = false;
                if (limit > pref)
                {
                    Prefs.MaxNumberOfPlayerSettlements = limit;
                    Prefs.Save();
                    raised = true;
                }
                // Who comes: its former colonists, and up to three of its other people who choose to.
                var formers = rec.formers.Where(p => TownLayouts.FreeAtHome(p, people)).Take(FormerMath.MaxFormersOnMap).ToList();
                var joiners = Joiners(rec, people, formers);
                bool lastTown = !Find.WorldObjects.Settlements.Any(o => o != null && o != s && o.Faction == people);
                // Its people's leader steps aside first, so the game makes no stranger to lead a people it lets go.
                var oldHead = people.leader;
                people.leader = null;
                if (lastTown) people.temporary = true;
                // The settlement is the colony's again, and looks it.
                s.SetFaction(Faction.OfPlayer);
                try
                {
                    AccessTools.Field(typeof(Settlement), "cachedMat")?.SetValue(s, null);
                    s.trader?.TryDestroyStock();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("town after: the settlement's look", e);
                }
                // Its map, made the game's way for a home, of the size the colony was, and the colony as it was left on it.
                var g = rec.layout?.grid;
                var size = g != null && g.mapX > 0 && g.mapZ > 0 ? new IntVec3(g.mapX, 1, g.mapZ) : Find.World.info.initialMapSize;
                var map = GetOrGenerateMapUtility.GetOrGenerateMap(s.Tile, size, null);
                CellRect? rect = null;
                if (map != null && rec.layout != null)
                {
                    try
                    {
                        rect = TownLayouts.Place(map, rec.layout, Faction.OfPlayer, true);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("town after: the colony's layout on its map again", e);
                    }
                }
                // They walk out of their rooms as colonists.
                var came = new List<Pawn>();
                if (map != null)
                {
                    var area = rect ?? CellRect.CenteredOn(map.Center, 20).ClipInsideMap(map);
                    foreach (var p in formers.Concat(joiners))
                    {
                        try
                        {
                            var cell = TownLayouts.HomeCell(map, rec.layout, area, p);
                            if (Arrivals.Join(p, map, cell)) came.Add(p);
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("town after: one who comes home", e);
                        }
                    }
                }
                // Its people lead on without them, when they keep other towns.
                if (!lastTown && people.leader == null)
                {
                    try
                    {
                        people.TryGenerateNewLeader();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("town after: its people's next leader", e);
                    }
                }
                if (lastTown) people.defeated = true;
                Records(rec, s, map, came, formers, joiners, oldHead, lastTown, raised, now);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: taking it back", e);
                return false;
            }
        }

        /// <summary>Its other people who choose to join the colony: their regard of the colony's people, kin among them, the town's hard times; three at most.</summary>
        internal static List<Pawn> Joiners(LedgerFormer rec, Faction people, List<Pawn> formers)
        {
            var list = new List<Pawn>();
            try
            {
                var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList();
                var ledgerTown = WorldLedger.TownRecord(rec.key);
                float prosperity = ledgerTown != null && ledgerTown.Seeded ? ledgerTown.prosperity : -1f;
                var candidates = Find.WorldPawns.AllPawnsAlive.Where(p => p != null && p.RaceProps.Humanlike && p.DevelopmentalStage.Adult() && !formers.Contains(p)
                                                                           && !rec.formers.Contains(p) && TownLayouts.FreeAtHome(p, people)).ToList();
                var scores = candidates.Select(p =>
                {
                    int best = -100;
                    bool kin = false;
                    if (p.relations != null)
                        foreach (var c in colonists)
                        {
                            best = Math.Max(best, p.relations.OpinionOf(c));
                            if (!kin && p.relations.FamilyByBlood.Contains(c)) kin = true;
                        }
                    return FormerMath.JoinScore(best, kin, prosperity);
                }).ToList();
                foreach (int i in FormerMath.Joiners(scores)) list.Add(candidates[i]);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: who comes with it", e);
            }
            return list;
        }

        /// <summary>The records of a town taken back by their word: the town's end, the colony's record, its notables, the milestone and the letter.</summary>
        private static void Records(LedgerFormer rec, Settlement s, Map map, List<Pawn> came, List<Pawn> formers, List<Pawn> joiners, Pawn oldHead, bool lastTown, bool raised,
            int now)
        {
            rec.end = TownEnd.TakenBack;
            rec.endedAt = now;
            rec.agreed = false;
            foreach (var p in came) WorldLedger.Release(p, KeepFor.Former);
            WorldLedger.FormerChanged();
            WorldLedger.ReopenColony(rec.key);
            NotableBook.Instance?.TownGoneOver(rec.key);
            var town = WorldLedger.TownRecord(rec.key);
            if (town != null)
            {
                if (town.gone < 0) town.gone = now;
                WorldLedger.TownChanged();
            }
            string townName = s.Name ?? rec.colonyName ?? "?";
            var actors = new List<HistoryActor> { new HistoryActor(Ref.Colony(rec.key), HistoryRole.About) };
            if (rec.people != null) actors.Add(new HistoryActor(Refs.Of(rec.people), HistoryRole.With));
            if (oldHead != null) actors.Add(new HistoryActor(WorldLedger.PersonOf(oldHead), HistoryRole.Did));
            History.Add(FoundingMath.KindTakenBack, actors, Ref.Town(rec.key), HistoryMath.Milestone,
                "CC_TownAfter_HistoryBack".Translate(townName.Named("TOWN"), (rec.people?.Name ?? "?").Named("PEOPLE")).Resolve());
            var cameFormers = came.Where(formers.Contains).ToList();
            var cameJoiners = came.Where(joiners.Contains).ToList();
            var form = Ages.Form(AgeFeature.Back, Ages.Ours, rec.age);
            string text = TextVariants.Vary("CC_TownAfter_BackText").Translate(townName.Named("TOWN"), (rec.people?.Name ?? "?").Named("PEOPLE"), (oldHead?.LabelShort ?? "?").Named("HEAD"),
                form.word.Named("FORM")).Resolve();
            if (cameFormers.Count > 0) text += "\n\n" + "CC_TownAfter_BackFormers".Translate(Foundings.Names(cameFormers).Named("NAMES")).Resolve();
            if (cameJoiners.Count > 0) text += "\n\n" + "CC_TownAfter_BackJoiners".Translate(Foundings.Names(cameJoiners).Named("NAMES"), (rec.people?.Name ?? "?").Named("PEOPLE")).Resolve();
            text += "\n\n" + (rec.layout != null ? "CC_TownAfter_BackLayout" : "CC_TownAfter_BackNoLayout").Translate(townName.Named("TOWN")).Resolve();
            if (lastTown) text += "\n\n" + "CC_TownAfter_BackPeopleGone".Translate((rec.people?.Name ?? "?").Named("PEOPLE")).Resolve();
            if (raised) text += "\n\n" + "CC_Found_LimitRaised".Translate(Prefs.MaxNumberOfPlayerSettlements.Named("COUNT")).Resolve();
            Tell.Letter(NewsKind.Colony, Importance.Important, map, "CC_TownAfter_BackLabel".Translate(townName.Named("TOWN")).Resolve(), text, LetterDefOf.PositiveEvent,
                came.Count > 0 ? new LookTargets(came.Where(p => p.Spawned)) : new LookTargets(s));
            Homes.Clear();
            Find.GameEnder.CheckOrUpdateGameOver();
            if (map != null) Current.Game.CurrentMap = map;
            LensModel.Dirty();
            Cards.Dirty();
        }

        // ================= By force =================

        /// <summary>
        /// The region's hour: what became of each town the colony was that is no longer standing as its people's, read once
        /// from the world (<see cref="FormerMath.Became"/>): beaten (the game's destroyed settlement at its place), or the
        /// colony's home again there after.
        /// </summary>
        public static void Watch(LedgerFormer rec, int now)
        {
            // Given up by its own people (release 5): the region keeps the ruin, nobody beat it.
            if (rec == null || rec.end == TownEnd.TakenBack || rec.end == TownEnd.Settled || rec.end == TownEnd.GivenUp) return;
            var s = rec.Town;
            bool stands = s != null && !s.Destroyed;
            if (stands && rec.tile < 0) rec.tile = s.Tile.tileId;
            if (stands && rec.end == TownEnd.None) return;
            if (rec.tile < 0) return;
            WorldObject ruin = null;
            Settlement home = null;
            foreach (var o in Find.WorldObjects.AllWorldObjects)
            {
                if (o == null || o.Destroyed || o.Tile.tileId != rec.tile || o.Tile.Layer != Find.WorldGrid.Surface) continue;
                if (o is DestroyedSettlement && (rec.people == null || o.Faction == rec.people)) ruin = o;
                else if (o is Settlement h && h.Faction == Faction.OfPlayer && h.HasMap) home = h;
            }
            var became = FormerMath.Became(rec.end, stands, stands && s.Faction == Faction.OfPlayer, ruin != null, home != null);
            if (became == TownEnd.Beaten && rec.end == TownEnd.None) Beaten(rec, ruin, now);
            else if (became == TownEnd.Settled && rec.end != TownEnd.Settled)
            {
                if (rec.end == TownEnd.None) Beaten(rec, ruin, now);
                Settled(rec, home, now);
            }
        }

        /// <summary>Beaten by force: the bond ends, and its people and every people of the same faith remember a heavy wrong (the faction memory, the ledger's grudge).</summary>
        private static void Beaten(LedgerFormer rec, WorldObject ruin, int now)
        {
            rec.end = TownEnd.Beaten;
            rec.endedAt = now;
            rec.agreed = false;
            if (!rec.bondEnded)
            {
                rec.bondEnded = true;
                rec.bondEndedAt = now;
            }
            WorldLedger.FormerChanged();
            string townName = WorldLedger.TownRecord(rec.key)?.name ?? rec.colonyName ?? "?";
            var people = rec.people;
            var seat = Homes.Seat;
            var wronged = new List<Faction>();
            if (people != null)
            {
                FactionMemory.Note(people, FormerMath.DeedTaken, townName, FormerMath.TakenWeight);
                Grudge(people, seat, rec);
                wronged.Add(people);
                // Every people of the same faith remembers it too.
                var faith = people.ideos?.PrimaryIdeo;
                if (faith != null)
                    foreach (var f in Find.FactionManager.AllFactionsListForReading)
                    {
                        if (f == null || f == people || f.IsPlayer || !FactionMemory.Remembers(f) || f.defeated || f.ideos == null || !f.ideos.AllIdeos.Contains(faith)) continue;
                        FactionMemory.Note(f, FormerMath.DeedTakenFaith, townName, FormerMath.TakenFaithWeight);
                        Grudge(f, seat, rec);
                        wronged.Add(f);
                    }
            }
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Us, HistoryRole.Did), new HistoryActor(Ref.Colony(rec.key), HistoryRole.About) };
            if (people != null) actors.Add(new HistoryActor(Refs.Of(people), HistoryRole.Against));
            History.Add(FoundingMath.Family + ":townBeaten", actors, Ref.Town(rec.key), HistoryMath.Milestone,
                "CC_TownAfter_HistoryBeaten".Translate(townName.Named("TOWN"), (people?.Name ?? "?").Named("PEOPLE")).Resolve());
            string others = wronged.Count > 1 ? " " + "CC_TownAfter_BeatenFaith".Translate(TextUtil.JoinAnd(wronged.Skip(1).Select(f => f.Name).ToList()).Named("PEOPLES")).Resolve() : "";
            // A people with no town left (the game's own defeat) has nobody left to remember it but the ledger's grudge.
            string beaten = people != null && people.defeated ? "CC_TownAfter_BeatenTextLast" : "CC_TownAfter_BeatenText";
            Tell.Letter(NewsKind.World, Importance.Important, seat, "CC_TownAfter_BeatenLabel".Translate(townName.Named("TOWN")).Resolve(),
                beaten.Translate(townName.Named("TOWN"), (people?.Name ?? "?").Named("PEOPLE")).Resolve() + others, LetterDefOf.NegativeEvent,
                ruin != null ? new LookTargets(ruin) : LookTargets.Invalid);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>The ledger's grudge of a town taken by force: from that people to the colony, about the town.</summary>
        private static void Grudge(Faction f, Map seat, LedgerFormer rec)
        {
            try
            {
                WorldLedger.AddTie(Tie.Kind.Grudge, Refs.Of(f), Diplomacy.Colony(seat), new TieTerms { key = FormerMath.GrudgeOrigin, about = Ref.Town(rec.key) }, Tie.Never,
                    FormerMath.GrudgeOrigin);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: the grudge of a town taken", e);
            }
        }

        /// <summary>Settled again with the game's Settle button where it was beaten: the colony's home again, the milestone.</summary>
        private static void Settled(LedgerFormer rec, Settlement home, int now)
        {
            rec.end = TownEnd.Settled;
            rec.endedAt = now;
            WorldLedger.FormerChanged();
            string townName = home?.Name ?? rec.colonyName ?? "?";
            if (home?.Map != null && WorldLedger.Colonies.Count > 0) WorldLedger.NoteColony(home.Map, now, rec.key, rec.colonyName);
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Us, HistoryRole.Did), new HistoryActor(Ref.Colony(rec.key), HistoryRole.About) };
            if (rec.people != null) actors.Add(new HistoryActor(Refs.Of(rec.people), HistoryRole.Against));
            History.Add(FoundingMath.KindTakenBack, actors, home != null ? Ref.Town(home.ID) : Ref.Town(rec.key), HistoryMath.Milestone,
                "CC_TownAfter_HistoryForce".Translate(townName.Named("TOWN"), (rec.people?.Name ?? "?").Named("PEOPLE")).Resolve());
            Tell.Minor(NewsKind.Colony, Importance.Important, home?.Map, "CC_TownAfter_SettledLine".Translate(townName.Named("TOWN"), (rec.colonyName ?? townName).Named("COLONY")).Resolve(),
                home != null ? new LookTargets(home) : LookTargets.Invalid);
            LensModel.Dirty();
            Cards.Dirty();
        }
    }
}
