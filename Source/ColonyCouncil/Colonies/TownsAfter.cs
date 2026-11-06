using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a town the colony was does afterwards (docs/design/stage3-many-colonies.md, section 2, "What it does
    /// afterwards"; release 2b). The kept layout and its placing are Colonies/TownLayout.cs, taking it back
    /// Colonies/TownTakeBack.cs; here, on the region's hour and day while a town the colony was is on record:
    /// <list type="bullet">
    /// <item><b>Letters.</b> Its head writes as a notable does, at most a season apart, in the voice of someone who was the
    /// colony's speaker: news of its own (its fortune, its people), the colony's days remembered, someone of its former
    /// people asking after their own in the colony (Rimfolk_Pool_TownLetter, with the facts town:former, them:former,
    /// {OLDCOLONY} and the years since).</item>
    /// <item><b>Trade.</b> The game's own: when a trader caravan of theirs comes, its Trader, a face the colony knows,
    /// comes with it as the one who trades (Patch_TownTrader).</item>
    /// <item><b>Its days.</b> It keeps the colony's founding day and the day of the hand-over, and invites the colony to
    /// one a year at most, by the invitation missions (Rimfolk_Mission_TownDay, Missions/TownDayMission.cs).</item>
    /// <item><b>Homecomings.</b> A former colonist (never the head) whose spouse, child or close friend is in the colony,
    /// or whose town struggles, may ask to come home: once a season per town at most, by Tell.Ask. Welcomed, they walk in
    /// from the town's side as colonists, and the one they came home to meets them.</item>
    /// <item><b>Moving there</b> is a moving-on question of its own (Social/MovingOnTown.cs).</item>
    /// </list>
    /// Nothing per tick and no schedule slot: the region's hour (<see cref="Pass.Region"/>) watches what became of each
    /// town and when their yes reaches the colony; the region's day does the rest. Off ("The town's life"), no letter,
    /// homecoming, invitation or trader comes; taking it back and the kept layout go on.
    /// </summary>
    public static class TownsAfter
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The town's life (Many colonies: "The town's life").</summary>
        public static bool LifeOn => S?.townLife ?? true;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: " + what, e);
            }
        }

        // ================= The hour =================

        /// <summary>The region's hour: what became of each town (taken by force, settled again), and their yes reaching the colony.</summary>
        public static void Hourly(int now)
        {
            foreach (var rec in WorldLedger.Formers.ToList())
            {
                if (rec == null) continue;
                Safe("what became of it", () => TownTakeBack.Watch(rec, now));
                if (rec.agreed && !rec.agreedTold && now >= rec.agreedAt && rec.Standing && rec.end == TownEnd.None)
                {
                    rec.agreedTold = true;
                    WorldLedger.FormerChanged();
                    Safe("their yes", () => FoundingLetters.AskTakeBack(rec));
                }
            }
        }

        // ================= The day =================

        /// <summary>The region's day: the head's letter, a homecoming asked, its day's invitation, the trader home again, a yes that waited too long.</summary>
        public static void Daily(int now)
        {
            foreach (var rec in WorldLedger.Formers.ToList())
            {
                if (rec == null || !rec.Standing || rec.end != TownEnd.None) continue;
                if (rec.agreed && rec.agreedTold && now - rec.agreedAt > FormerMath.YearTicks)
                {
                    // Their yes, never taken up in a year, lapses.
                    rec.agreed = false;
                    WorldLedger.FormerChanged();
                    Tell.Minor(NewsKind.World, Importance.Notable, Homes.Seat, "CC_TownAfter_YesLapsed".Translate(TownName(rec).Named("TOWN")).Resolve(), Look(rec));
                }
                Safe("the trader home", () => TraderHome(rec));
                if (!LifeOn || rec.people.HostileTo(Faction.OfPlayer)) continue;
                if (FormerMath.LetterDue(rec.handedAt, rec.lastLetter, now, Rand.Value)) Safe("the head's letter", () => Letter(rec, now));
                Safe("a homecoming", () => Homecomings(rec, now));
                Safe("its days", () => Days(rec, now));
            }
        }

        // ================= Words =================

        public static string TownName(LedgerFormer rec) => rec?.Town?.Name ?? WorldLedger.TownRecord(rec?.key ?? -1)?.name ?? rec?.colonyName ?? "?";

        public static LookTargets Look(LedgerFormer rec) => rec?.Town != null ? new LookTargets(rec.Town) : LookTargets.Invalid;

        /// <summary>The years since the hand-over in words, never a countdown: "less than a year", "a year", "3 years".</summary>
        public static string SinceWords(int handedAt, int now)
        {
            int years = FormerMath.YearsSince(handedAt, now);
            return years <= 0 ? "CC_TownAfter_Since_Less".Translate().Resolve() : years == 1 ? "CC_TownAfter_Since_One".Translate().Resolve()
                : "CC_TownAfter_Since_Many".Translate(years.Named("YEARS")).Resolve();
        }

        /// <summary>The town's fortune band as its letters' tag (FormerMath.LetterTag), from its ledger record.</summary>
        private static float Prosperity(LedgerFormer rec)
        {
            var t = WorldLedger.TownRecord(rec.key);
            return t != null && t.Seeded ? t.prosperity : -1f;
        }

        private static bool Struggling(LedgerFormer rec) => FormerMath.LetterTag(Prosperity(rec)) == "struggling";

        // ================= Letters =================

        /// <summary>
        /// Its head writes: a line in the voice of someone who was the colony's speaker, chosen by the town's fortune, its age,
        /// the years since and what is true between the two (the facts service: their town, the region, the ties), and
        /// someone of its former people asking after their own in the colony.
        /// </summary>
        private static void Letter(LedgerFormer rec, int now)
        {
            var head = rec.people.leader;
            var n = TownLetters.HeadOf(rec);
            var home = Homes.Seat;
            var town = rec.Town;
            if (head == null || head.Dead || home == null || town == null) return;
            rec.lastLetter = now;
            WorldLedger.FormerChanged();
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail("Rimfolk_Pool_TownLetter");
            if (pool == null) return;
            var facts = Facts.For(FactScene.Event(home, rec.people, town));
            var tags = new HashSet<string>(facts.tags)
            {
                FormerMath.LetterTag(Prosperity(rec)), Ages.Tag(rec.age), rec.formers.Contains(head) ? "former" : "new"
            };
            if (FormerMath.YearsSince(rec.handedAt, now) >= 1) tags.Add("years");
            var asker = Asker(rec, out var askedAfter);
            if (asker != null) tags.Add("asks");
            string line = pool.lines.Pick(Tone.Plain, tags, head);
            if (line.NullOrEmpty()) return;
            var values = new Dictionary<string, string>(facts.values)
            {
                ["HEAD"] = head.LabelShort, ["TOWN"] = town.Name, ["OLDCOLONY"] = rec.colonyName ?? town.Name, ["YEARSSINCE"] = SinceWords(rec.handedAt, now),
                ["COLONY"] = Homes.Name(home), ["PEOPLE"] = rec.people.Name
            };
            if (asker != null)
            {
                values["ASKER"] = asker.LabelShort;
                values["KIN"] = askedAfter.LabelShort;
            }
            string title = n != null ? NotableBook.TitleOf(n) : rec.people.LeaderTitle;
            string text = ("CC_TownAfter_LetterIntro_" + rec.age).Translate(head.LabelShort.Named("HEAD"), (title ?? "").Named("TITLE"), town.Name.Named("TOWN")).Resolve() + "\n\n\""
                          + Texts.Fill(line, values) + "\"";
            string fortune = RegionBook.FortunesOn ? RegionFortunes.Line(WorldLedger.TownRecord(rec.key)) : null;
            if (!fortune.NullOrEmpty()) text += "\n\n" + "CC_TownAfter_LetterFortune".Translate(town.Name.Named("TOWN"), fortune.UncapitalizeFirst().Named("FORTUNE")).Resolve();
            // apart: a letter from their town; nobody of theirs is here.
            Tell.Letter(NewsKind.World, Importance.Notable, home, "CC_TownAfter_LetterLabel".Translate(head.LabelShort.Named("HEAD"), town.Name.Named("TOWN")).Resolve(), text,
                LetterDefOf.NeutralEvent, new LookTargets(town));
        }

        /// <summary>One of its former people with someone of their own in the colony (a spouse, a child, a close friend), who asks after them in the letter.</summary>
        private static Pawn Asker(LedgerFormer rec, out Pawn kin)
        {
            kin = null;
            foreach (var p in rec.formers.Where(p => p != null && !p.Dead && p.Faction == rec.people).InRandomOrder())
            {
                var k = KinInColony(p, out _);
                if (k == null) continue;
                kin = k;
                return p;
            }
            return null;
        }

        // ================= Homecomings =================

        /// <summary>
        /// Someone of the colony's own for a former colonist: their spouse or child among the colony's people (kin), else a
        /// close friend; null for none. <paramref name="close"/>: the tie is kin.
        /// </summary>
        public static Pawn KinInColony(Pawn p, out bool close)
        {
            close = false;
            if (p?.relations == null) return null;
            Pawn friend = null;
            foreach (var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
            {
                if (c?.relations == null || c == p || c.Dead || !c.RaceProps.Humanlike) continue;
                // Their spouse, or their child (one whose parent they are).
                if (p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, c) || c.relations.DirectRelationExists(PawnRelationDefOf.Parent, p))
                {
                    close = true;
                    return c;
                }
                if (friend == null && FormerMath.CloseFriends(p.relations.OpinionOf(c), c.relations.OpinionOf(p))) friend = c;
            }
            return friend;
        }

        /// <summary>
        /// A homecoming asked from this town (once a season at most, never in its first): the former colonist with the best
        /// reason (their spouse or child in the colony, a close friend, their town struggling), never the head, asks by
        /// Tell.Ask; each asks about once.
        /// </summary>
        private static void Homecomings(LedgerFormer rec, int now)
        {
            if (!Tell.CanAsk || Homes.Seat == null) return;
            bool hard = Struggling(rec);
            var head = rec.people.leader;
            Pawn best = null, bestKin = null;
            bool bestClose = false;
            foreach (var p in rec.formers)
            {
                if (p == head || !TownLayouts.FreeAtHome(p, rec.people) || !p.DevelopmentalStage.Adult() || MovingOn.Asked(MovingOn.Key("home", p))) continue;
                var kin = KinInColony(p, out bool close);
                if (!FormerMath.WouldAsk(false, true, close, kin != null && !close, hard)) continue;
                if (best == null || close && !bestClose || kin != null && bestKin == null)
                {
                    best = p;
                    bestKin = kin;
                    bestClose = close;
                }
            }
            if (best == null) return;
            float chance = FormerMath.HomecomingChance(bestClose, bestKin != null && !bestClose, hard);
            if (!FormerMath.HomecomingDue(rec.handedAt, rec.lastHomecoming, now, chance, Rand.Value)) return;
            rec.lastHomecoming = now;
            MovingOn.MarkAsked(MovingOn.Key("home", best));
            WorldLedger.FormerChanged();
            FoundingLetters.AskHomecoming(rec, best, bestKin, bestClose, hard);
        }

        /// <summary>The home they come back to: where the one they came home to lives, else the seat.</summary>
        public static Map HomeFor(Pawn kin) => kin?.MapHeld != null && Homes.IsHome(kin.MapHeld) ? kin.MapHeld : Homes.Seat;

        /// <summary>Why a homecoming can't be now (in words), or null: they are still the town's and free, and there is a home to come to.</summary>
        public static string HomecomingWhyNot(LedgerFormer rec, Pawn p, Pawn kin)
        {
            if (rec == null || !rec.Standing || p == null || !TownLayouts.FreeAtHome(p, rec.people)) return "CC_TownAfter_HomeGone".Translate((p?.LabelShort ?? "?").Named("PAWN")).Resolve();
            if (HomeFor(kin) == null) return "CC_TownAfter_HomeNoHome".Translate();
            return null;
        }

        /// <summary>
        /// Welcomed home: they walk in from the town's side of the map as one of the colony's again, no longer the town's
        /// (nor its notable); the one they came home to goes to meet them, and the two speak face to face when they do.
        /// </summary>
        public static void Homecoming(LedgerFormer rec, Pawn p, Pawn kin)
        {
            if (HomecomingWhyNot(rec, p, kin) != null) return;
            var home = HomeFor(kin);
            var edge = EdgeToward(home, rec.Town);
            if (!Arrivals.Join(p, home, edge)) return;
            rec.formers.Remove(p);
            WorldLedger.Release(p, KeepFor.Former);
            if (rec.second == p) rec.second = null;
            WorldLedger.FormerChanged();
            NotableBook.Instance?.LetGoOf(p);
            string town = TownName(rec);
            var actors = new List<HistoryActor> { new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.Did), new HistoryActor(Ref.Colony(Homes.KeyOf(home)), HistoryRole.For) };
            History.Add(FoundingMath.KindHomecoming, actors, Ref.Town(rec.key), HistoryMath.Milestone,
                "CC_TownAfter_HomeHistory".Translate(p.LabelShort.Named("PAWN"), town.Named("TOWN"), Homes.Name(home).Named("HOME")).Resolve());
            Tell.Message(NewsKind.Colony, Importance.Important, home, "CC_TownAfter_HomeCame".Translate(p.LabelShort.Named("PAWN"), town.Named("TOWN")).Resolve(), new LookTargets(p),
                MessageTypeDefOf.PositiveEvent);
            if (kin != null && kin.Spawned && kin.Map == home)
            {
                var a = kin;
                var b = p;
                // The welcome: the one they came home to goes to meet them, and the two speak face to face (Together.When).
                Together.When("town-home-" + b.thingIDNumber, a, b, () =>
                {
                    if (Speech.On)
                    {
                        // together: said face to face, the moment the two are together.
                        Speech.Say(a, Flavor.FromPool("Rimfolk_Pool_TownHomecoming", a, null, null, "welcome"), b);
                        Speech.Say(b, Flavor.FromPool("Rimfolk_Pool_TownHomecoming", b, null, null, "back"), a);
                    }
                    SocialLog.Add(a, b, "CC_TownAfter_HomeLog".Translate(a.Named("PAWN"), b.Named("OTHER"), town.Named("TOWN")).Resolve(), false);
                }, 12f, 0f);
            }
        }

        /// <summary>A cell at the map's edge on the side the town lies, reachable, else null (the game's own entry cell).</summary>
        private static IntVec3? EdgeToward(Map home, Settlement town)
        {
            if (home == null || town == null || town.Tile.Layer != home.Tile.Layer) return null;
            try
            {
                float heading = Find.WorldGrid.GetHeadingFromTo(home.Tile, town.Tile);
                bool roofOk = home.TileInfo.AllowRoofedEdgeWalkIn;
                bool Open(IntVec3 c) => c.Walkable(home) && (roofOk || !home.roofGrid.Roofed(c));
                foreach (int s in RaidersCampMath.Sides(heading))
                    if (CellFinder.TryFindRandomEdgeCellWith(c => Open(c) && home.reachability.CanReachColony(c), home, new Rot4(s), CellFinder.EdgeRoadChance_Friendly, out var cell))
                        return cell;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town after: the town's side of the map", e);
            }
            return null;
        }

        // ================= Its days =================

        /// <summary>The colony's founding day or the day of the hand-over coming round: the town invites the colony, a year apart at most.</summary>
        private static void Days(LedgerFormer rec, int now)
        {
            var day = FormerMath.DayComing(rec.founded, rec.handedAt, now, rec.lastInvite);
            if (day == TownDay.None) return;
            var home = Homes.Seat;
            if (home == null) return;
            if (!TownDayMission.Invite(rec, day, home)) return;
            rec.lastInvite = now;
            WorldLedger.FormerChanged();
        }

        // ================= Trade =================

        /// <summary>The town's Trader, a face the colony knows, to be the one who trades with a caravan of theirs; null for none free (the game makes its own).</summary>
        public static Pawn TraderFor(Faction f)
        {
            if (!LifeOn || f == null || !HandOverPeoples.IsOurs(f)) return null;
            var rec = WorldLedger.FormerOf(f);
            if (rec == null || !rec.Standing || rec.end != TownEnd.None) return null;
            var n = NotableBook.Instance?.At(rec.key, NotableRole.Trader);
            var p = n?.pawn;
            if (!TownLayouts.FreeAtHome(p, f) || p.Downed || !p.DevelopmentalStage.Adult() || PawnUtility.IsFactionLeader(p)) return null;
            return p;
        }

        /// <summary>The town's Trader back home in the world after a caravan: no longer the caravan's trader.</summary>
        private static void TraderHome(LedgerFormer rec)
        {
            var p = NotableBook.Instance?.At(rec.key, NotableRole.Trader)?.pawn;
            if (p?.mindState == null || !p.mindState.wantsToTradeWithColony || !TownLayouts.FreeAtHome(p, rec.people)) return;
            p.mindState.wantsToTradeWithColony = false;
            PawnComponentsUtility.AddAndRemoveDynamicComponents(p);
        }
    }

    /// <summary>The town's card: its map is the colony as it was left, their yes to the colony's letter, how it ended; and its acts.</summary>
    [StaticConstructorOnStartup]
    public static class TownAfterCards
    {
        static TownAfterCards()
        {
            CardSections.TownLines(CardSlot.Now, 4.6f, s =>
            {
                var r = s != null ? WorldLedger.FormerOf(s.ID) : null;
                if (r == null || r.people != s.Faction) return null;
                var lines = new List<string>();
                if (r.layout != null) lines.Add("CC_TownAfter_CardLayout".Translate((r.colonyName ?? s.Name).Named("COLONY")).Resolve());
                if (r.agreed) lines.Add("CC_TownAfter_CardAgreed".Translate(s.Name.Named("TOWN")).Resolve());
                return lines.Count > 0 ? lines : null;
            }, "CC_HandOver_TownHead", "town-former-after");
            CardActs.Register(CardKind.Place, _ => "CC_TownAfter_ActAsk".Translate().Resolve(), r => TownLetters.WhyNot(WorldLedger.FormerOf(CardSections.TownOf(r)?.ID ?? -1)) ?? AskWhyNot(r),
                r => AskBack(r), r => Shows(r) && !(WorldLedger.FormerOf(CardSections.TownOf(r).ID)?.agreed ?? false), 46f, "CC_TownAfter_ActAskTip".Translate(), "town-takeback-ask");
            CardActs.Register(CardKind.Place, _ => "CC_TownAfter_ActTake".Translate().Resolve(), r => TownTakeBack.WhyNot(WorldLedger.FormerOf(CardSections.TownOf(r)?.ID ?? -1)),
                r => TownTakeBack.Ask(WorldLedger.FormerOf(CardSections.TownOf(r)?.ID ?? -1)), r => Shows(r) && (WorldLedger.FormerOf(CardSections.TownOf(r).ID)?.agreed ?? false), 46.5f,
                "CC_TownAfter_ActTakeTip".Translate(), "town-takeback");
        }

        private static bool Shows(ThingRef r)
        {
            var s = CardSections.TownOf(r);
            var rec = s != null ? WorldLedger.FormerOf(s.ID) : null;
            return rec != null && rec.Standing && rec.end == TownEnd.None;
        }

        private static string AskWhyNot(ThingRef r)
        {
            var rec = WorldLedger.FormerOf(CardSections.TownOf(r)?.ID ?? -1);
            var head = TownLetters.HeadOf(rec);
            if (head == null) return "CC_TownAfter_Why_NoHead".Translate();
            return NeighbourLetters.WhyNot(head, LetterPurpose.TakeBack);
        }

        /// <summary>The colony's letter to the head, its purpose and topic chosen (the letter window, World/NeighboursPanel.cs).</summary>
        private static void AskBack(ThingRef r)
        {
            var rec = WorldLedger.FormerOf(CardSections.TownOf(r)?.ID ?? -1);
            var head = TownLetters.HeadOf(rec);
            if (head == null) return;
            Find.WindowStack.Add(new Dialog_NeighbourLetter(head, LetterPurpose.TakeBack, TownLetters.Prefix + rec.key));
        }
    }
}
