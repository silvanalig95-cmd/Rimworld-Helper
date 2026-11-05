using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class FeastDefOf
    {
        public static LetterDef Rimfolk_FeastSuggest;
        public static JobDef Rimfolk_FeastRun;
        public static JobDef Rimfolk_FeastShoot;
        public static JobDef Rimfolk_FeastPrize;
        public static DutyDef Rimfolk_FeastGuest;
        public static DutyDef Rimfolk_FeastIndoors;

        static FeastDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(FeastDefOf));
    }

    /// <summary>
    /// Feasts, hunts, games and contests with guests (docs/design/stage2-diplomacy.md, round b, item 5; release 2b.1;
    /// decision 7; the rules in FeastMath.cs). From a people's card, the calendar or at the steward's suggestion the player
    /// plans a feast (FeastDialog.cs): up to three notables of peoples at peace within three days' travel, invited by the
    /// existing letters and calls (World/NeighbourLetters.cs, the purpose Invitation with the topic "feast:HOME":
    /// FeastLetters.cs); up to four contests by what the colony has; a prize from the stores. Each answers by their own
    /// reckoning; once every answer is in, the day is the first fine one after the last of them can arrive. On the day
    /// each walks in with a small retinue and waits by the gate (LordJob_FeastGuests), the contests are held one after
    /// another on the map (FeastContests.cs), the feast is the colony's gathering with the guests at its table (a play-out
    /// of the kind Feast, its guests "feast": PlayGuests.cs), the winners are said aloud and the prize carried to the
    /// champion; the guests stay the night by the missions' hosting rules (Missions/Hosting.cs, Hospitality's guest beds
    /// first) and go home at first light. How each was welcomed moves their regard and their people's memory (the deed
    /// "feasted", or "slighted" for an empty plate); the history keeps "dip:feast" and "dip:contest". The colony's own at a
    /// neighbour's feast (the missions' invitation) may win something there, told when they come home.
    /// <para>2b.2's weddings host their guests here: <see cref="Host"/> brings guests who already agreed on a day, with
    /// no letters and no contests unless given, and <see cref="StageChanged"/> tells the wedding when they have come.</para>
    /// </summary>
    public static class Feasts
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The setting "Feasts with guests": off, nothing new is planned or suggested, and a feast under way plays out.</summary>
        public static bool On => ColonyCouncilMod.Settings?.diplomacyFeasts ?? true;

        /// <summary>
        /// A feast's stage moved (the guests answered, came, sat down, went to bed, went home): for what hosts its guests
        /// here (2b.2's wedding listens for FeastStage.Here and FeastStage.Done on a plan of its purpose and tag). Called on
        /// the main thread; a listener that throws is caught and logged.
        /// </summary>
        public static event Action<GuestFeast, Map, FeastStage> StageChanged;

        internal static void Moved(GuestFeast p, Map map, FeastStage to)
        {
            if (p == null || p.stage == to) return;
            p.stage = to;
            Diplomacy.Dirty();
            var listeners = StageChanged;
            if (listeners == null) return;
            foreach (var d in listeners.GetInvocationList())
            {
                try
                {
                    ((Action<GuestFeast, Map, FeastStage>)d)(p, map, to);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("feasts: a listener of the stage", e);
                }
            }
        }

        // ================= Reading =================

        /// <summary>Every feast under way at a home of the colony's, with its home (a new list).</summary>
        public static List<(DiplomacyHome home, GuestFeast plan)> All()
        {
            var list = new List<(DiplomacyHome, GuestFeast)>();
            var book = DiplomacyBook.Instance;
            if (book == null) return list;
            foreach (var h in book.Live())
                if (h.feast != null)
                    list.Add((h, h.feast));
            return list;
        }

        /// <summary>The feast under way at this home, or null.</summary>
        public static GuestFeast At(Map home) => DiplomacyBook.Instance?.Existing(home)?.feast;

        /// <summary>A feast by its id, under way or the last one at its home.</summary>
        public static GuestFeast Get(int id, out DiplomacyHome home)
        {
            home = null;
            var book = DiplomacyBook.Instance;
            if (book == null) return null;
            foreach (var h in book.Live())
            {
                if (h.feast != null && h.feast.id == id || h.lastFeast != null && h.lastFeast.id == id)
                {
                    home = h;
                    return h.feast != null && h.feast.id == id ? h.feast : h.lastFeast;
                }
            }
            return null;
        }

        /// <summary>The feast whose history entry this is (its invitations or the feast held), under way or the last at its home.</summary>
        public static GuestFeast OfEntry(int entryId, out DiplomacyHome home)
        {
            home = null;
            var book = DiplomacyBook.Instance;
            if (book == null || entryId <= 0) return null;
            foreach (var h in book.Live())
                foreach (var p in new[] { h.feast, h.lastFeast })
                    if (p != null && (p.entry == entryId || p.heldEntry == entryId))
                    {
                        home = h;
                        return p;
                    }
            return null;
        }

        /// <summary>The feast whose guest (or one of a guest's retinue) this is while they're at the colony, or null.</summary>
        public static GuestFeast Of(Pawn p, out DiplomacyHome home, out FeastGuest guest)
        {
            home = null;
            guest = null;
            if (p == null) return null;
            foreach (var (h, plan) in All())
            {
                var g = plan.GuestOf(p);
                if (g == null) continue;
                home = h;
                guest = g;
                return plan;
            }
            return null;
        }

        public static GuestFeast Of(Pawn p) => Of(p, out _, out _);

        /// <summary>The guests at a home now (each guest and their retinue, on the map), for the feast's gathering (PlayGuests.Feast).</summary>
        public static List<Pawn> GuestsAt(Map map)
        {
            var p = At(map);
            if (p == null || !p.OnMap) return new List<Pawn>();
            return p.AllGuests.Where(x => x != null && x.Spawned && x.Map == map && !x.Dead).ToList();
        }

        public static bool Empire(FeastGuest g) => Ages.IsEmpire(g?.people);

        // ================= Days and hours, by the home's own clock =================

        private static float Longitude(Map map) => map == null ? 0f : Find.WorldGrid.LongLatOf(map.Tile).x;

        private static long Abs(int tick) => GenDate.TickGameToAbs(tick);

        /// <summary>The day number (days since the world began, by the home's clock) at a tick.</summary>
        public static int DayOf(Map map, int tick) => (int)((Abs(tick) + GenDate.LocalTicksOffsetFromLongitude(Longitude(map))) / GenDate.TicksPerDay);

        public static int HourOf(Map map, int tick) => GenDate.HourOfDay(Abs(tick), Longitude(map));

        public static int Today(Map map) => DayOf(map, Now);

        /// <summary>The game tick a day begins at, by the home's clock.</summary>
        public static int DayStart(Map map, int day) => GenDate.TickAbsToGame((int)((long)day * GenDate.TicksPerDay - GenDate.LocalTicksOffsetFromLongitude(Longitude(map))));

        /// <summary>A day in words, as the game writes a date ("5th of Aprimay, 5501").</summary>
        public static string DateText(Map map, int day)
        {
            if (map == null || day < 0) return "?";
            try
            {
                return GenDate.DateFullStringAt(Abs(DayStart(map, day) + GenDate.TicksPerHour * 12), Find.WorldGrid.LongLatOf(map.Tile));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: a date", e);
                return "?";
            }
        }

        // ================= Planning =================

        /// <summary>Why this home can't plan a feast with guests now, or null.</summary>
        public static string PlanWhyNot(Map home)
        {
            if (!On) return FeastWords.P("CC_Feast_Why_Off");
            var book = DiplomacyBook.Instance;
            if (book == null || home == null || !Homes.IsHome(home)) return FeastWords.P("CC_Feast_Why_NoHome");
            if (MapFacts.InVacuum(home)) return FeastWords.P("CC_Feast_Why_Vacuum");
            var h = book.Existing(home);
            if (h?.feast != null) return FeastWords.P("CC_Feast_Why_UnderWay");
            if (h != null && !FeastMath.MayPlan(false, h.feastHeld, Now)) return FeastWords.P("CC_Feast_Why_Season");
            if (!NeighbourLetters.On) return "CC_Letter_Why_Off".Translate().Resolve();
            return null;
        }

        /// <summary>Days for them to come to the home, by their people's means (as a messenger of theirs travels).</summary>
        public static float TravelDays(Notable n, Map home)
        {
            var town = n != null ? NotableBook.TownOf(n.town) : null;
            if (town == null || home == null) return 99f;
            float tiles = Find.WorldGrid.ApproxDistanceInTiles(home.Tile, town.Tile);
            return NotableMath.TravelDays(tiles, (int)(n.faction?.def?.techLevel ?? TechLevel.Industrial));
        }

        /// <summary>What their people hope for at the feast.</summary>
        public static FeastHope HopeOf(Faction f) => f == null ? FeastHope.None : FeastMath.Hope(Ages.Of(f), Ages.IsEmpire(f));

        /// <summary>Why this notable can't be invited now (by a letter or a call), or null.</summary>
        public static string InviteWhyNot(Notable n, Map home)
        {
            if (n == null || !n.Alive) return "CC_Letter_Why_Gone".Translate().Resolve();
            var f = n.faction;
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (Diplomacy.Hostile(f)) return FeastWords.P("CC_Feast_Why_War", FeastWords.People(f).Named("PEOPLE"));
            int regard = NotableBook.Opinion(n);
            if (regard < FeastMath.MinRegard) return FeastWords.P("CC_Feast_Why_Regard", n.Name.Named("NAME"));
            if (TravelDays(n, home) > FeastMath.MaxTravelDays) return FeastWords.P("CC_Feast_Why_Far", n.Name.Named("NAME"));
            if (n.pawn == null || !LivingEvents.FreeToTravel(n.pawn) || n.pawn.Spawned) return FeastWords.P("CC_Feast_Why_NotFree", n.Name.Named("NAME"));
            if (Of(n.pawn) != null || All().Any(x => x.plan.guests.Any(g => g.notable == n.id && g.state != GuestState.Declined && g.state != GuestState.Gone)))
                return FeastWords.P("CC_Feast_Why_Invited", n.Name.Named("NAME"));
            string letter = NeighbourLetters.WhyNot(n, LetterPurpose.Invitation);
            if (letter != null) return letter;
            if (NeighbourLetters.ChannelWhyNot(n, LetterChannel.Courier, home) != null && NeighbourLetters.ChannelWhyNot(n, LetterChannel.Console, home) != null)
                return NeighbourLetters.ChannelWhyNot(n, LetterChannel.Courier, home);
            return null;
        }

        /// <summary>The notables the colony might invite (of peoples it deals with and not at war), the best regard first.</summary>
        public static List<Notable> Candidates(Map home, Faction only = null)
        {
            var book = NotableBook.Instance;
            if (book == null) return new List<Notable>();
            return book.notables.Where(n => n != null && n.Alive && n.faction != null && (only == null || n.faction == only) && Diplomacy.Deals(n.faction) && !Diplomacy.Hostile(n.faction))
                .OrderBy(n => InviteWhyNot(n, home) == null ? 0 : 1).ThenByDescending(n => NotableBook.Opinion(n)).Take(24).ToList();
        }

        /// <summary>Why the colony can't hold this contest at this home now, or null (by what it has: people, game, tables, a mod's targets).</summary>
        public static string ContestWhyNot(ContestKind k, Map home) => FeastContests.WhyNot(k, home);

        /// <summary>The colony has a musical instrument (the game's concert gathering, the Empire's hope).</summary>
        public static bool HasInstrument(Map home) => home != null && home.listerBuildings.allBuildingsColonist.Any(b => b is Building_MusicalInstrument && b.Spawned);

        /// <summary>Fine things from the stores that may be a prize: art, and weapons and clothes of good quality or better, the finest first.</summary>
        public static List<Thing> Prizes(Map home)
        {
            var list = new List<Thing>();
            if (home == null) return list;
            try
            {
                foreach (var t in home.listerThings.AllThings)
                {
                    if (t == null || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || t.Position.GetSlotGroup(home) == null) continue;
                    var inner = (t as MinifiedThing)?.InnerThing ?? t;
                    bool art = inner.TryGetComp<CompArt>()?.Active ?? false;
                    bool good = inner.TryGetQuality(out var q) && q >= QualityCategory.Good && (inner.def.IsWeapon || inner.def.IsApparel);
                    if (!art && !good) continue;
                    if (t is Apparel ap && ap.WornByCorpse) continue;
                    list.Add(t);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: the prizes", e);
            }
            return list.OrderByDescending(t => t.MarketValue * t.stackCount).Take(20).ToList();
        }

        /// <summary>Cooked meals in the home's stores.</summary>
        public static int MealsInStores(Map home) => home == null ? 0 : PlaySpots.CountMeals(home);

        /// <summary>Who writes the invitations: the steward, else the one who writes the colony's letters.</summary>
        public static Pawn Writer(Map home)
        {
            if (home == null) return null;
            var all = NeighbourLetters.Writers(home);
            var steward = ColonyCouncilMod.Settings?.offices ?? false ? ColonyState.For(home)?.offices?.HolderOf("Council_Office_Steward") : null;
            if (steward != null && all.Contains(steward)) return steward;
            return NeighbourLetters.DefaultWriter(home);
        }

        /// <summary>
        /// The player's plan: the invitations go out (one letter or call to each guest, by the colony's means), the feast is
        /// set going and its first line goes into the history. Returns it, or null with why not.
        /// </summary>
        public static GuestFeast Plan(Map home, List<Notable> invited, List<ContestKind> contests, Thing prize, Pawn writer, out string why)
        {
            why = PlanWhyNot(home);
            if (why != null) return null;
            var book = DiplomacyBook.Instance;
            invited = (invited ?? new List<Notable>()).Where(n => InviteWhyNot(n, home) == null).Distinct().Take(FeastMath.MaxGuests).ToList();
            if (invited.Count == 0)
            {
                why = FeastWords.P("CC_Feast_Why_NoGuests");
                return null;
            }
            writer ??= Writer(home);
            if (writer == null)
            {
                why = FeastWords.P("CC_Feast_Why_NoWriter");
                return null;
            }
            var h = book.HomeOf(home);
            var p = new GuestFeast
            {
                id = book.NextId(), purpose = FeastPurpose.Feast, stage = FeastStage.Inviting, created = Now, writerName = writer.LabelShort,
                contests = (contests ?? new List<ContestKind>()).Distinct().Where(k => ContestWhyNot(k, home) == null).Take(FeastMath.MaxContests).ToList(),
                prize = prize != null && prize.Spawned && prize.Map == home ? prize : null
            };
            p.prizeName = p.prize != null ? PlayGive.Label(p.prize) : null;
            string topicText = FeastWords.P("CC_Feast_Topic", FeastWords.Name(home).Named("FEAST"));
            foreach (var n in invited)
            {
                var channel = NeighbourLetters.ChannelWhyNot(n, LetterChannel.Console, home) == null ? LetterChannel.Console : LetterChannel.Courier;
                var l = NeighbourLetters.Send(n, LetterPurpose.Invitation, channel, writer, new LetterTopic { key = FeastMath.Topic(Homes.KeyOf(home)), text = topicText }, null);
                if (l == null) continue;
                p.guests.Add(new FeastGuest
                {
                    notable = n.id, pawn = n.pawn, name = n.Name, people = n.faction, town = n.town, state = GuestState.Invited, letter = l.id, hope = HopeOf(n.faction)
                });
            }
            if (p.guests.Count == 0)
            {
                why = FeastWords.P("CC_Feast_Why_NoGuests");
                return null;
            }
            h.feast = p;
            var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did) };
            foreach (var g in p.guests)
            {
                if (g.pawn != null) actors.Add(new HistoryActor(WorldLedger.PersonOf(g.pawn), HistoryRole.For));
                if (g.people != null && actors.All(a => a.who != Refs.Of(g.people))) actors.Add(new HistoryActor(Refs.Of(g.people), HistoryRole.For));
            }
            p.entry = Diplomacy.Write(DiplomacyMath.Feast, actors, Diplomacy.Colony(home),
                FeastWords.P("CC_Feast_Hist_Invited", FeastWords.Name(home).Named("FEAST"), FeastWords.Guests(p).Named("GUESTS"), FeastWords.Peoples(p).Named("PEOPLES")))?.id ?? -1;
            Diplomacy.Dirty();
            return p;
        }

        /// <summary>
        /// The entry point for hosting guests at a home who have already agreed to come (2b.2's weddings): their people, the
        /// notables who come (each with a small retinue), what for, the day they come (a tick on or after which the first
        /// fine day is taken), a key of the caller's own, and contests or a prize if any. No letters go out; the guests walk
        /// in on the day, wait by the gate, sit at the feast with the colony, stay the night by the hosting rules and go home
        /// at first light, as for a feast. Their welcome moves their regard as a feast's does. Listen to
        /// <see cref="StageChanged"/> for FeastStage.Here (they are at the gate) and FeastStage.Done. Returns the plan, or
        /// null when the home has one under way or nobody can come.
        /// </summary>
        public static GuestFeast Host(Map home, Faction people, List<Notable> guests, FeastPurpose purpose, int when, string tag, List<ContestKind> contests = null, Thing prize = null)
        {
            var book = DiplomacyBook.Instance;
            if (book == null || home == null || !Homes.IsHome(home) || people == null || Diplomacy.Hostile(people)) return null;
            var h = book.HomeOf(home);
            if (h.feast != null) return null;
            var p = new GuestFeast
            {
                id = book.NextId(), purpose = purpose, tag = tag, stage = FeastStage.Set, created = Now,
                contests = (contests ?? new List<ContestKind>()).Distinct().Take(FeastMath.MaxContests).ToList(),
                prize = prize, prizeName = prize != null ? PlayGive.Label(prize) : null
            };
            foreach (var n in (guests ?? new List<Notable>()).Where(n => n?.pawn != null && n.Alive).Distinct().Take(FeastMath.MaxGuests))
                p.guests.Add(new FeastGuest
                {
                    notable = n.id, pawn = n.pawn, name = n.Name, people = n.faction ?? people, town = n.town, state = GuestState.Coming, comeBy = Math.Max(when, Now),
                    hope = HopeOf(n.faction ?? people)
                });
            if (p.guests.Count == 0) return null;
            h.feast = p;
            SetDay(h, p, home);
            Diplomacy.Dirty();
            return p;
        }

        /// <summary>Why the player can't call this feast off now, or null: only before the guests come.</summary>
        public static string CallOffWhyNot(GuestFeast p) =>
            p == null || p.stage == FeastStage.Done ? FeastWords.P("CC_Feast_Why_Over") : p.OnMap ? FeastWords.P("CC_Feast_Why_AlreadyHere") : null;

        /// <summary>The player calls it off before the guests come: those who said yes think a little less of the colony.</summary>
        public static void CallOffByColony(GuestFeast p)
        {
            Get(p?.id ?? -1, out var h);
            var map = h?.Map;
            if (p == null || h == null || CallOffWhyNot(p) != null) return;
            foreach (var g in p.guests.Where(g => g.state == GuestState.Coming))
            {
                var n = NotableBook.Instance?.Get(g.notable);
                if (n == null) continue;
                n.personal += FeastMath.CalledOffRegard;
                n.Remember(FeastWords.P("CC_Feast_Note_CalledOff", FeastWords.Name(map).Named("FEAST")), FeastMath.CalledOffRegard);
            }
            NotableBook.Changed();
            CallOff(h, p, map, FeastWords.P("CC_Feast_Off_ByColony"), true);
        }

        /// <summary>Called off (by the colony, the weather, a raid, nobody coming): told, and the plan let go.</summary>
        public static void CallOff(DiplomacyHome h, GuestFeast p, Map map, string why, bool told)
        {
            if (h == null || p == null) return;
            p.calledOff = true;
            p.offWhy = why;
            p.ended = Now;
            FeastContests.Stop(p);
            foreach (var g in p.guests)
                if (g.state == GuestState.Here)
                    g.state = GuestState.Gone;
            LeaveAll(p, map);
            if (told && map != null)
                // apart: what became of the feast, told in a message; the guests (if any are here) walk out on the map.
                Tell.Message(NewsKind.World, Importance.Notable, map, FeastWords.P("CC_Feast_Msg_CalledOff", FeastWords.Name(map).Named("FEAST"), (why ?? "").Named("WHY")),
                    LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            Close(h, p, map);
        }

        /// <summary>The plan let go: kept as the home's last for its card; the prize is the colony's again.</summary>
        private static void Close(DiplomacyHome h, GuestFeast p, Map map)
        {
            if (p.prize != null && p.prize.Spawned && !p.prizeGiven) p.prize.SetForbidden(false, false);
            Moved(p, map, FeastStage.Done);
            if (h.feast == p) h.feast = null;
            h.lastFeast = p;
            Diplomacy.Dirty();
        }

        // ================= Their answers =================

        /// <summary>
        /// Their answer to the invitation, as they read it (FeastLetters.Read): yes by their own reckoning, and when they can
        /// be at the colony. Returns the line their letter says it in.
        /// </summary>
        public static string Answer(GuestFeast p, FeastGuest g, Notable n, Map home, float backDays, bool call, out bool yes)
        {
            yes = false;
            if (p == null || g == null || n == null || p.stage != FeastStage.Inviting || g.state != GuestState.Invited)
                return FeastWords.T("CC_Feast_Ans_Moot", FeastWords.Name(home).Named("FEAST"));
            int regard = NotableBook.Opinion(n);
            int goodwill = n.faction != null && n.faction.HasGoodwill ? n.faction.PlayerGoodwill : 0;
            bool hope = FeastMath.Offered(g.hope, p.contests, HasInstrument(home));
            yes = !Diplomacy.Hostile(n.faction) && n.Alive && LivingEvents.FreeToTravel(n.pawn) && FeastMath.Accepts(regard, goodwill, hope, TravelDays(n, home), Rand.Value);
            if (!yes)
            {
                g.state = GuestState.Declined;
                Diplomacy.Dirty();
                return FeastWords.T("CC_Feast_Ans_No", n.Name.Named("NAME"), FeastWords.Name(home).Named("FEAST"));
            }
            g.state = GuestState.Coming;
            g.comeBy = Now + Mathf.RoundToInt(FeastMath.ComeDays(backDays, call) * GenDate.TicksPerDay);
            g.retinue.Clear();
            Diplomacy.Dirty();
            string rite = FeastWords.Rite(n.faction, home, out bool faith);
            string line = FeastWords.T(hope ? "CC_Feast_Ans_YesHope" : "CC_Feast_Ans_Yes", n.Name.Named("NAME"), FeastWords.Name(home).Named("FEAST"), FeastWords.Hope(g.hope).Named("HOPE"));
            if (faith && !rite.NullOrEmpty()) line += " " + FeastWords.P("CC_Feast_Ans_TheirRite", FeastWords.People(n.faction).Named("PEOPLE"), rite.Named("RITE"));
            return line;
        }

        // ================= The hour =================

        /// <summary>The hour (Diplomacy.Hourly): each feast a step on; tales of the colony's own told once they're home.</summary>
        public static void Hourly(int now)
        {
            foreach (var (h, p) in All())
            {
                try
                {
                    Step(h, p, h.Map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("feasts: the hour", e);
                }
            }
            try
            {
                FeastAway.Tell();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: tales from away", e);
            }
        }

        private static void Step(DiplomacyHome h, GuestFeast p, Map map, int now)
        {
            if (map == null)
            {
                CallOff(h, p, null, null, false);
                return;
            }
            switch (p.stage)
            {
                case FeastStage.Inviting:
                    Inviting(h, p, map);
                    return;
                case FeastStage.Set:
                    Waiting(h, p, map, now);
                    return;
                case FeastStage.Done:
                    Close(h, p, map);
                    return;
                default:
                    // On the map: the 250-tick look comes from their group (LordJob_FeastGuests). With nobody of theirs
                    // left here at all, it ends as it stands.
                    if (!p.AllGuests.Any(x => x != null && !x.Dead && x.MapHeld == map)) End(h, p, map, false);
                    else Rare(Homes.KeyOf(map), true);
                    return;
            }
        }

        /// <summary>The answers: a letter called off or lost is a no; once none is out, the day is set (or, with nobody coming, it's over).</summary>
        private static void Inviting(DiplomacyHome h, GuestFeast p, Map map)
        {
            var book = NotableBook.Instance;
            bool open = false;
            foreach (var g in p.guests)
            {
                var l = book?.letters.FirstOrDefault(x => x != null && x.id == g.letter);
                if (l != null && l.Open)
                {
                    open = true;
                    continue;
                }
                if (g.state == GuestState.Invited) g.state = GuestState.Declined;
            }
            if (open) return;
            if (!p.guests.Any(g => g.state == GuestState.Coming))
            {
                CallOff(h, p, map, FeastWords.P("CC_Feast_Off_NoGuests"), true);
                return;
            }
            SetDay(h, p, map);
        }

        /// <summary>Every answer is in: the day is the first after the last of them can arrive (a feast is in the evening, so a guest due by noon comes that day).</summary>
        private static void SetDay(DiplomacyHome h, GuestFeast p, Map map)
        {
            int last = p.guests.Where(g => g.state == GuestState.Coming).Select(g => g.comeBy).DefaultIfEmpty(Now).Max();
            last = Math.Max(last, Now);
            p.day = Math.Max(Today(map), FeastMath.FeastDay(DayOf(map, last), HourOf(map, last)));
            Moved(p, map, FeastStage.Set);
            if (p.purpose != FeastPurpose.Feast) return;
            // apart: the day the answers set, told in a letter; the guests walk in on the map on that day.
            Tell.Letter(NewsKind.World, Importance.Notable, map, FeastWords.P("CC_Feast_Set_Label", FeastWords.Name(map).Named("FEAST")),
                FeastWords.T("CC_Feast_Set", FeastWords.Name(map).Named("FEAST"), FeastWords.Guests(p, true).Named("GUESTS"), FeastWords.Peoples(p, true).Named("PEOPLES"),
                    DateText(map, p.day).Named("DATE"))
                + "\n\n" + Declined(p) + FeastWords.P("CC_Feast_Set_How", FeastWords.Contests(p.contests).Named("CONTESTS")),
                LetterDefOf.PositiveEvent, LookTargets.Invalid);
        }

        private static string Declined(GuestFeast p)
        {
            var no = p.guests.Where(g => g.state == GuestState.Declined).Select(FeastWords.Name).ToList();
            return no.Count == 0 ? "" : FeastWords.P("CC_Feast_Set_Declined", TextUtil.JoinAnd(no).Named("NAMES")) + "\n\n";
        }

        /// <summary>The day set: on it (or the first fine day after, within two), the guests walk in.</summary>
        private static void Waiting(DiplomacyHome h, GuestFeast p, Map map, int now)
        {
            int today = Today(map);
            int hour = GenLocalDate.HourOfDay(map);
            if (today < p.day) return;
            foreach (var g in p.guests)
                if (g.state == GuestState.Coming && (g.pawn == null || g.pawn.Dead || g.pawn.Discarded || Diplomacy.Hostile(g.people)))
                    g.state = GuestState.Declined;
            if (!p.guests.Any(g => g.state == GuestState.Coming))
            {
                CallOff(h, p, map, FeastWords.P("CC_Feast_Off_NoGuests"), true);
                return;
            }
            if (FeastMath.ArriveNow(today, p.day, hour, Fine(map), MapFacts.ThreatOn(map)))
            {
                Arrive(h, p, map);
                return;
            }
            if (FeastMath.GiveUp(today, p.day, hour))
                CallOff(h, p, map, FeastWords.P(MapFacts.ThreatOn(map) ? "CC_Feast_Off_Danger" : "CC_Feast_Off_Weather"), true);
        }

        /// <summary>Fine enough for the guests to come and the outdoor contests to be held.</summary>
        public static bool Fine(Map map)
        {
            if (map == null) return false;
            var w = map.weatherManager?.curWeather;
            bool foul = map.gameConditionManager != null && map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout);
            return FeastMath.Fine(w?.rainRate ?? 0f, w?.snowRate ?? 0f, map.mapTemperature.OutdoorTemp, foul);
        }

        // ================= Their arrival =================

        /// <summary>One or two of their lighter fighters to walk with a guest, else anyone of theirs.</summary>
        private static PawnKindDef RetinueKind(Faction f)
        {
            var opts = f?.def?.pawnGroupMakers?.Where(m => m?.kindDef == PawnGroupKindDefOf.Combat && m.options != null).SelectMany(m => m.options)
                .Select(o => o.kind).Where(k => k?.RaceProps?.Humanlike == true && k.combatPower <= 90f).ToList();
            return opts == null || opts.Count == 0 ? Arrivals.PersonKind(f) : opts.RandomElement();
        }

        /// <summary>Each guest walks in from the edge with one or two of their people, a group to each people, toward the gate.</summary>
        internal static void Arrive(DiplomacyHome h, GuestFeast p, Map map)
        {
            if (!Arrivals.TryEntryCell(map, out var entry)) return;
            var gate = PlaySpots.GateCell(map, null);
            if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
            var came = new List<Pawn>();
            foreach (var group in p.guests.Where(g => g.state == GuestState.Coming).GroupBy(g => g.people).ToList())
            {
                var party = new List<Pawn>();
                foreach (var g in group)
                {
                    try
                    {
                        if (g.pawn == null || g.pawn.Spawned || !LivingEvents.FreeToTravel(g.pawn) || !Arrivals.Spawn(g.pawn, map, entry))
                        {
                            g.state = GuestState.Declined;
                            continue;
                        }
                        party.Add(g.pawn);
                        g.state = GuestState.Here;
                        int n = Rand.RangeInclusive(1, FeastMath.MaxRetinue);
                        var kind = RetinueKind(g.people);
                        for (int i = 0; i < n && kind != null; i++)
                        {
                            var r = Arrivals.Make(kind, g.people);
                            GenSpawn.Spawn(r, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);
                            g.retinue.Add(r);
                            party.Add(r);
                        }
                        foreach (var x in g.Party) Hosting.Provision(x, 2f);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("feasts: a guest's arrival", e);
                    }
                }
                if (party.Count == 0) continue;
                try
                {
                    LordMaker.MakeNewLord(group.Key, new LordJob_FeastGuests(Homes.KeyOf(map), gate), map, party);
                    came.AddRange(party);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("feasts: their group", e);
                }
            }
            if (!p.guests.Any(g => g.state == GuestState.Here))
            {
                CallOff(h, p, map, FeastWords.P("CC_Feast_Off_NoGuests"), true);
                return;
            }
            p.arrived = Now;
            p.contest = 0;
            p.contestStart = -1;
            Moved(p, map, FeastStage.Here);
            var people = p.guests.Where(g => g.state == GuestState.Here).Select(g => g.people).FirstOrDefault();
            string rite = FeastWords.Rite(people, map, out bool faith);
            string text = FeastWords.T(p.purpose == FeastPurpose.Wedding ? "CC_Feast_Arrive_Wedding" : "CC_Feast_Arrive", FeastWords.Guests(p, true).Named("GUESTS"),
                FeastWords.Peoples(p, true).Named("PEOPLES"), FeastWords.Name(map).Named("FEAST"));
            if (p.purpose == FeastPurpose.Feast)
            {
                text += "\n\n" + FeastWords.P(p.contests.Count > 0 ? "CC_Feast_Arrive_How" : "CC_Feast_Arrive_HowNone", FeastWords.Contests(p.contests).Named("CONTESTS"));
                if (faith && !rite.NullOrEmpty()) text += " " + FeastWords.P("CC_Feast_Arrive_Rite", FeastWords.People(people).Named("PEOPLE"), rite.Named("RITE"));
                if (p.guests.Any(g => g.state == GuestState.Here && g.hope != FeastHope.None))
                    text += "\n\n" + string.Join(" ", p.guests.Where(g => g.state == GuestState.Here && g.hope != FeastHope.None).Select(g =>
                        FeastWords.P("CC_Feast_Arrive_Hope", FeastWords.Name(g).Named("NAME"), FeastWords.Hope(g.hope).Named("HOPE"))));
            }
            Tell.Letter(NewsKind.World, Importance.Notable, map, FeastWords.P("CC_Feast_Arrive_Label", FeastWords.Name(map).Named("FEAST")), text, LetterDefOf.PositiveEvent,
                new LookTargets(came.Take(3)), people);
        }

        /// <summary>A guest's group on the home, or null.</summary>
        public static Lord LordOf(Map map, Pawn p)
        {
            var l = p?.GetLord();
            return l?.LordJob is LordJob_FeastGuests ? l : null;
        }

        /// <summary>Every group of the feast's guests on the home.</summary>
        public static List<Lord> Lords(Map map, GuestFeast p)
        {
            var list = new List<Lord>();
            if (map?.lordManager == null || p == null) return list;
            foreach (var l in map.lordManager.lords)
                if (l?.LordJob is LordJob_FeastGuests && l.ownedPawns.Any(x => p.GuestOf(x) != null))
                    list.Add(l);
            return list;
        }

        /// <summary>Where the guests keep about now: the gate, a contest, the feast, indoors for the night.</summary>
        public static void Focus(Map map, GuestFeast p, IntVec3 cell, float radius = 6f)
        {
            if (!cell.IsValid) return;
            foreach (var l in Lords(map, p))
                if (l.LordJob is LordJob_FeastGuests j && (j.focus != cell || Math.Abs(j.radius - radius) > 0.1f))
                {
                    j.focus = cell;
                    j.radius = radius;
                    l.CurLordToil?.UpdateAllDuties();
                }
        }

        // ================= The day, every 250 ticks from their group =================

        private static readonly Dictionary<int, int> lastLook = new Dictionary<int, int>();
        private static Game lookGame;

        /// <summary>
        /// The day's look (LordToil_FeastGuests, every 250 ticks; the hour too): their meals and beds by the hosting rules,
        /// the gate, the contests one after another, the feast, the night, and first light.
        /// </summary>
        public static void Rare(int homeKey, bool fromHour = false)
        {
            int now = Now;
            if (lookGame != Current.Game)
            {
                // A game loaded: what this session saw of another game's feasts is let go.
                lookGame = Current.Game;
                lastLook.Clear();
                greeted.Clear();
                concerts.Clear();
                sour.Clear();
            }
            if (!fromHour && lastLook.TryGetValue(homeKey, out int last) && now - last < 200) return;
            lastLook[homeKey] = now;
            var map = Homes.MapOf(homeKey);
            var h = DiplomacyBook.Instance?.Existing(map);
            var p = h?.feast;
            if (map == null || p == null || !p.OnMap) return;
            try
            {
                Look(h, p, map, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: the day's look", e);
            }
        }

        private static void Look(DiplomacyHome h, GuestFeast p, Map map, int now)
        {
            // Anyone of theirs gone from the map, dead or held is no longer here.
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here).ToList())
            {
                var x = g.pawn;
                if (x != null && x.Dead)
                {
                    Died(h, p, map, g);
                    return;
                }
                if (x != null && (x.IsPrisoner || x.Faction != g.people))
                {
                    Insulted(h, p, map);
                    return;
                }
                if (x == null || x.MapHeld != map) g.state = GuestState.Gone;
            }
            if (!p.guests.Any(g => g.state == GuestState.Here))
            {
                End(h, p, map, false);
                return;
            }
            // However the day went, they don't stay past a second night.
            if (now - p.arrived > 2 * GenDate.TicksPerDay)
            {
                End(h, p, map, false);
                return;
            }
            Watch(p, map);
            Tend(p, map);
            int hour = GenLocalDate.HourOfDay(map);
            switch (p.stage)
            {
                case FeastStage.Here:
                    Focus(map, p, PlaySpots.GateCell(map, null));
                    if (!Settled(p)) return;
                    Greet(p, map);
                    if (p.purpose != FeastPurpose.Feast && p.contests.Count == 0)
                    {
                        // A wedding's guests wait for it at the gate; its own code holds the ceremony, the feast comes in the evening.
                        if (hour >= FeastMath.FeastFrom) StartFeast(h, p, map);
                        return;
                    }
                    if (hour >= FeastMath.ContestsFrom && now - p.arrived >= GenDate.TicksPerHour)
                    {
                        p.contest = 0;
                        p.contestStart = -1;
                        Moved(p, map, FeastStage.Contests);
                    }
                    else if (hour >= FeastMath.FeastFrom) StartFeast(h, p, map);
                    return;
                case FeastStage.Contests:
                    Contests(h, p, map, hour);
                    return;
                case FeastStage.Feasting:
                    Feasting(h, p, map, hour);
                    return;
                case FeastStage.Night:
                    Night(h, p, map, hour, now);
                    return;
            }
        }

        /// <summary>Everyone of theirs has reached the gate (or is past their walk in).</summary>
        private static bool Settled(GuestFeast p) => p.AllGuests.All(x => x == null || !x.Spawned || x.GetLord()?.CurLordToil is LordToil_FeastGuests) || Now - p.arrived > 8 * GenDate.TicksPerHour;

        private static readonly HashSet<int> greeted = new HashSet<int>();

        /// <summary>At the gate: one of the colony's (the speaker, else whoever is free and nearest) greets each guest face to face, and each guest answers.</summary>
        private static void Greet(GuestFeast p, Map map)
        {
            if (!greeted.Add(p.id)) return;
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here))
            {
                var x = g.pawn;
                if (x == null || !x.Spawned) continue;
                // together: a word at the gate, said only when one of the colony's is standing with them (Speech.Say checks it).
                var c = map.mapPawns.FreeColonistsSpawned.Where(o => Together.Able(o) && !o.DevelopmentalStage.Baby() && SocialLog.Near(o, x)).OrderBy(o => o.Position.DistanceToSquared(x.Position))
                    .FirstOrDefault();
                if (c != null) Outsiders.Say(c, "feastwelcome", x);
                Outsiders.Say(x, "feastarrive", c);
            }
        }

        /// <summary>Their meals: anything of the colony's they eat counts (a fine meal for the Empire); their bed for the night.</summary>
        private static void Watch(GuestFeast p, Map map)
        {
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here))
            {
                var x = g.pawn;
                if (x == null || !x.Spawned) continue;
                if (Hosting.EatingOurs(x))
                {
                    var food = x.CurJob?.targetA.Thing;
                    g.ate = true;
                    if (food?.def?.ingestible != null && food.def.ingestible.preferability >= FoodPreferability.MealFine) g.fine = true;
                }
                if (x.InBed() && x.CurrentBed() is Building_Bed bed && bed.Faction == Faction.OfPlayer) g.bed = true;
                else if (x.CurJobDef == HostingDefOf.Council_GuestSleep && !x.Awake() && x.CurrentBed() == null) g.floor = true;
            }
        }

        /// <summary>Free to be sent to eat or to bed: at the colony with their group, up, calm, not at a contest or the feast's table, no raid on.</summary>
        public static bool Free(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Downed || p.InMentalState || p.Drafted || !p.Awake() || p.InBed()) return false;
            if (!(p.GetLord()?.CurLordToil is LordToil_FeastGuests)) return false;
            if (FeastContests.InContest(p)) return false;
            var d = p.CurJobDef;
            if (d == JobDefOf.Ingest || d == FeastDefOf.Rimfolk_FeastRun || d == FeastDefOf.Rimfolk_FeastShoot || d == JobDefOf.Hunt || d == OutsiderDefOf.Council_OutsiderBout) return false;
            return PlayOuts.Interruptible(p) && p.Map.dangerWatcher.DangerRating != StoryDanger.High;
        }

        /// <summary>
        /// Meals and beds by the missions' hosting rules: the hungry eat the colony's food at its table (fine meals first for
        /// the Empire's), with nothing of the colony's from their packs; once the night has come each lies down in a bed of
        /// the colony's they may have (Hospitality's guest beds first), else on the floor, and gets up once rested.
        /// </summary>
        private static void Tend(GuestFeast p, Map map)
        {
            bool night = p.stage == FeastStage.Night;
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here))
                foreach (var x in g.Party.ToList())
                {
                    if (x == null || x.Dead || !x.Spawned || x.Map != map) continue;
                    float food = x.needs?.food?.CurLevelPercentage ?? 1f;
                    bool sleepy = night && Hosting.WantsSleep(x);
                    if (Hosting.LyingAwake(x) && !sleepy)
                    {
                        Hosting.GetUp(x);
                        continue;
                    }
                    if (!Free(x)) continue;
                    if (food < Hosting.EatBelow || sleepy && food < Hosting.SupperBelow)
                    {
                        var meal = Hosting.Meal(x, Empire(g)) ?? Hosting.Raw(x);
                        if (meal != null && Hosting.Eat(x, meal, null)) continue;
                        if (food < Hosting.PackBelow || sleepy)
                        {
                            var pack = Hosting.Pack(x);
                            if (pack != null && Hosting.Eat(x, pack, null)) continue;
                        }
                    }
                    if (!sleepy) continue;
                    var bed = BedFor(p, x, map);
                    if (bed != null && Hosting.Sleep(x, bed)) continue;
                    Hosting.SleepRough(x);
                }
        }

        /// <summary>A bed of the colony's for a guest: one they have, a guest bed of Hospitality's, else a spare one they'd share.</summary>
        private static Building_Bed BedFor(GuestFeast p, Pawn x, Map map)
        {
            bool Ours(Pawn o) => p.GuestOf(o) != null;
            var owned = x.ownership?.OwnedBed;
            if (owned != null && owned.Spawned && owned.Map == map) return owned;
            var guestBed = map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>()
                .Where(b => b.Spawned && GuestBeds.IsGuestBed(b) && !b.Medical && RestUtility.CanUseBedEver(x, b.def) && b.OwnersForReading.All(o => o == null || Ours(o))
                            && b.OwnersForReading.Count < b.SleepingSlotsCount && x.CanReserveAndReach(b, PathEndMode.OnCell, Danger.Some, b.SleepingSlotsCount, 0))
                .OrderBy(b => b.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (guestBed != null) return guestBed;
            return Hosting.Beds(map, Ours, x).OrderBy(b => b.Position.DistanceToSquared(x.Position))
                .FirstOrDefault(b => Hosting.CanShare(b, x) && (b.OwnersForReading.Contains(x) || b.OwnersForReading.Count < b.SleepingSlotsCount));
        }

        // ================= The contests =================

        private static void Contests(DiplomacyHome h, GuestFeast p, Map map, int hour)
        {
            while (p.contest < p.contests.Count)
            {
                var k = p.contests[p.contest];
                if (p.contestStart < 0)
                {
                    // An outdoor contest waits for the light, fine weather and no danger; past its hour it is let go. Nothing
                    // new begins once the feast should be under way.
                    if (FeastMath.Outdoor(k) && hour >= FeastMath.OutdoorBy)
                    {
                        Skip(p, k, FeastWords.P("CC_Feast_Off_Light"));
                        continue;
                    }
                    if (hour >= FeastMath.FeastTo - 1 || hour < FeastMath.FirstLight)
                    {
                        Skip(p, k, FeastWords.P("CC_Feast_Off_Late"));
                        continue;
                    }
                    if (MapFacts.ThreatOn(map)) return;
                    if (FeastMath.Outdoor(k) && !Fine(map))
                    {
                        if (hour >= FeastMath.OutdoorBy - 2) Skip(p, k, FeastWords.P("CC_Feast_Off_Rain"));
                        return;
                    }
                    string why = FeastContests.Start(p, k, map);
                    if (why != null)
                    {
                        Skip(p, k, why);
                        continue;
                    }
                    p.contestStart = Now;
                    Diplomacy.Dirty();
                    return;
                }
                var result = FeastContests.Step(p, map);
                if (result == null)
                {
                    if (Now - p.contestStart > FeastMath.MaxTicks(k))
                    {
                        result = FeastContests.Finish(p, map, true);
                        if (result == null) Skip(p, k, FeastWords.P("CC_Feast_Off_Long"));
                    }
                    if (result == null) return;
                }
                FeastContests.Stop(p);
                Record(p, map, result);
                p.contest++;
                p.contestStart = -1;
                return;
            }
            StartFeast(h, p, map);
        }

        private static void Skip(GuestFeast p, ContestKind k, string why)
        {
            p.results.Add(new FeastResult { kind = k, off = true, why = why });
            p.contest++;
            p.contestStart = -1;
            FeastContests.Stop(p);
            Diplomacy.Dirty();
        }

        /// <summary>A contest won: the winner's people and the colony hear of it, and the history keeps it.</summary>
        private static void Record(GuestFeast p, Map map, FeastResult r)
        {
            p.results.Add(r);
            Diplomacy.Dirty();
            if (r.off || r.winnerName.NullOrEmpty())
            {
                // apart: the contest's end told in a message; it was played out on the map.
                Tell.Message(NewsKind.World, Importance.Notable, map, FeastWords.Result(r), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return;
            }
            var actors = new List<HistoryActor>();
            if (r.winner != null) actors.Add(new HistoryActor(WorldLedger.PersonOf(r.winner), HistoryRole.Did));
            actors.Add(new HistoryActor(Diplomacy.Colony(map), r.ours ? HistoryRole.Did : HistoryRole.With));
            foreach (var f in p.guests.Where(g => g.state != GuestState.Declined && g.people != null).Select(g => g.people).Distinct())
                actors.Add(new HistoryActor(Refs.Of(f), r.people == f ? HistoryRole.Did : HistoryRole.With));
            Diplomacy.Write(DiplomacyMath.Contest, actors, Diplomacy.Colony(map), FeastWords.Result(r));
            // apart: the contest's end told in a message; the winner's cheer is said on the map.
            Tell.Message(NewsKind.World, Importance.Notable, map, FeastWords.Result(r), r.winner != null && r.winner.Spawned ? new LookTargets(r.winner) : LookTargets.Invalid,
                r.ours ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
        }

        // ================= The feast =================

        /// <summary>The feast: the colony's gathering with the guests at its table (a play-out of the kind Feast), at its hours.</summary>
        private static void StartFeast(DiplomacyHome h, GuestFeast p, Map map)
        {
            FeastContests.Stop(p);
            Moved(p, map, FeastStage.Feasting);
            bool music = p.guests.Any(g => g.state == GuestState.Here && g.hope == FeastHope.MealAndMusic);
            // Its hours: the evening, or from now when the contests ran late.
            int hour = GenLocalDate.HourOfDay(map);
            int from = hour >= FeastMath.FeastFrom ? hour : FeastMath.FeastFrom, to = Math.Max(FeastMath.FeastTo, Math.Min(23, hour + 2));
            PlayOut play = null;
            try
            {
                var eff = new DilemmaEffect_Feast
                {
                    label = FeastWords.Name(map), guests = PlayGuests.Feast, guestCount = 12, music = music, hours = new IntRange(from, to), waitHours = 3f,
                    duration = new FloatRange(2.5f, 3f)
                };
                play = eff.Begin(new DilemmaContext { map = map });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: the feast's gathering", e);
            }
            p.playId = play?.id ?? -1;
            Focus(map, p, Visitors.IndoorsCell(map, p.AllGuests.Where(x => x.Spawned).ToList()), 8f);
        }

        private static void Feasting(DiplomacyHome h, GuestFeast p, Map map, int hour)
        {
            var play = p.playId >= 0 ? PlayOutTracker.Instance?.Get(p.playId) : null;
            if (play != null && play.state == PlayState.Running)
            {
                p.held = true;
                var spot = GatheringSpot(map, p.playId);
                if (spot.IsValid) Focus(map, p, spot, 6f);
                MaybeConcert(p, map);
                return;
            }
            if (play != null && play.state == PlayState.Pending) return;
            // With answers not playing out on the map, the guests are served at the table by the hosting rules for the evening.
            if (p.playId < 0 && hour < FeastMath.FeastTo && !p.held)
            {
                if (hour < FeastMath.FeastFrom) return;
                p.held = true;
                foreach (var g in p.guests.Where(g => g.state == GuestState.Here))
                {
                    var meal = g.pawn != null && Free(g.pawn) ? Hosting.Meal(g.pawn, Empire(g)) : null;
                    if (meal != null) Hosting.Eat(g.pawn, meal, null);
                }
                return;
            }
            if (p.playId < 0 && hour < FeastMath.FeastTo && hour >= FeastMath.FeastFrom) return;
            if (play != null && play.state != PlayState.Done) return;
            // Over (or it never came about): the winners said aloud and the prize carried, then the night.
            Winners(p, map);
            p.nightAt = Now;
            Moved(p, map, FeastStage.Night);
            Focus(map, p, Visitors.IndoorsCell(map, p.AllGuests.Where(x => x.Spawned).ToList()), 5f);
            SourNights(p, map);
        }

        /// <summary>The gathering's place, from its group on the map.</summary>
        private static IntVec3 GatheringSpot(Map map, int playId)
        {
            foreach (var l in map.lordManager.lords)
                if (l?.LordJob is LordJob_PlayOut j && j.playId == playId)
                    return j.Spot;
            return IntVec3.Invalid;
        }

        private static readonly HashSet<int> concerts = new HashSet<int>();

        /// <summary>
        /// The Empire hopes for music: where the colony has an instrument, its best player plays for the guests at the feast
        /// (the game's own concert gathering, with Royalty), once.
        /// </summary>
        private static void MaybeConcert(GuestFeast p, Map map)
        {
            if (p.music || !p.guests.Any(g => g.state == GuestState.Here && g.hope == FeastHope.MealAndMusic) || !concerts.Add(p.id)) return;
            try
            {
                var def = DefDatabase<GatheringDef>.GetNamedSilentFail("Concert");
                if (def == null || !ModsConfig.RoyaltyActive || !HasInstrument(map)) return;
                var player = map.mapPawns.FreeColonistsSpawned.Where(c => c.Awake() && !c.Downed && !c.InMentalState && !c.Drafted && !(c.skills?.GetSkill(SkillDefOf.Artistic)?.TotallyDisabled ?? true))
                    .OrderByDescending(c => c.skills.GetSkill(SkillDefOf.Artistic).Level).FirstOrDefault(c => def.Worker.CanExecute(map, c));
                if (player == null || !def.Worker.TryExecute(map, player)) return;
                p.music = true;
                Diplomacy.Dirty();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: music for the Empire's guests", e);
            }
        }

        /// <summary>
        /// The winners said aloud at the feast's end, the champion named, and the prize carried to them by whoever invited the
        /// guests (else the speaker, else anyone free): a guest takes it home in their pack; one of the colony's has it set
        /// down at their feet.
        /// </summary>
        private static void Winners(GuestFeast p, Map map)
        {
            var won = p.results.Where(r => !r.off && r.winner != null && !r.winnerName.NullOrEmpty()).ToList();
            if (won.Count == 0 || p.champion != null) return;
            var who = won.Select(r => r.winner).Distinct().ToList();
            int best = FeastMath.Champion(who.Select(x => won.Count(r => r.winner == x)).ToList());
            if (best < 0) return;
            var champ = who[best];
            p.champion = champ;
            p.championName = champ.LabelShort;
            foreach (var r in won)
                if (r.winner != null && r.winner.Spawned)
                    // together: said aloud at the feast, by the winner standing with the others.
                    Outsiders.Say(r.winner, "feastwinner", null);
            bool guest = p.GuestOf(champ) != null;
            string text = FeastWords.T(guest ? "CC_Feast_Champion_Guest" : "CC_Feast_Champion_Ours", champ.Named("CHAMPION"), FeastWords.Name(map).Named("FEAST"),
                (guest ? FeastWords.People(champ.Faction) : "").Named("PEOPLE"));
            var prize = p.prize;
            if (prize != null && prize.Spawned && prize.Map == map && champ.Spawned && champ.Map == map)
            {
                var carrier = PrizeCarrier(p, map, champ);
                if (carrier != null && carrier.CanReserveAndReach(prize, PathEndMode.ClosestTouch, Danger.Some) && carrier.CanReach(champ, PathEndMode.Touch, Danger.Some))
                {
                    prize.SetForbidden(true, false);
                    var job = JobMaker.MakeJob(FeastDefOf.Rimfolk_FeastPrize, prize, champ);
                    job.count = prize.stackCount;
                    carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    if (carrier.CurJobDef == FeastDefOf.Rimfolk_FeastPrize)
                        text += " " + FeastWords.P("CC_Feast_Champion_Prize", carrier.Named("PAWN"), p.prizeName.Named("PRIZE"), champ.Named("CHAMPION"));
                    else prize.SetForbidden(false, false);
                }
            }
            else if (p.prize == null && !p.prizeName.NullOrEmpty())
                text += " " + FeastWords.P("CC_Feast_Champion_PrizeGone", p.prizeName.Named("PRIZE"));
            // apart: the champion named at the feast's end, told in a message; the prize is carried to them on the map.
            Tell.Message(NewsKind.World, Importance.Notable, map, text, champ.Spawned ? new LookTargets(champ) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        private static Pawn PrizeCarrier(GuestFeast p, Map map, Pawn champ)
        {
            var all = map.mapPawns.FreeColonistsSpawned.Where(c => c != champ && Together.Able(c) && !c.DevelopmentalStage.Juvenile() && PlayOuts.CanJoin(c) && !PlayOuts.NeedsPress(c)
                                                                   && (c.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false)).ToList();
            return all.FirstOrDefault(c => c.LabelShort == p.writerName) ?? all.FirstOrDefault(c => ColonyState.For(map)?.offices?.Speaker == c)
                   ?? all.OrderBy(c => c.Position.DistanceToSquared(champ.Position)).FirstOrDefault();
        }

        /// <summary>The prize handed over (JobDriver_FeastPrize): a guest puts it in their pack, one of the colony's has it at their feet.</summary>
        public static void PrizeHanded(Pawn carrier, Pawn champ)
        {
            var p = Of(champ) ?? (champ != null && DiplomacyBook.Instance?.Existing(champ.MapHeld)?.feast is GuestFeast f && f.champion == champ ? f : null);
            var carried = carrier?.carryTracker?.CarriedThing;
            if (carried == null) return;
            if (p != null) p.prizeGiven = true;
            try
            {
                if (p?.GuestOf(champ) != null && champ.inventory != null)
                {
                    carrier.carryTracker.innerContainer.TryTransferToContainer(carried, champ.inventory.innerContainer, carried.stackCount);
                    var g = p.GuestOf(champ);
                    if (g != null) g.prize = true;
                }
                else if (carrier.carryTracker.TryDropCarriedThing(champ.Position, ThingPlaceMode.Near, out var lying) && lying != null) lying.SetForbidden(false, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: the prize handed over", e);
            }
            // together: handed over face to face.
            Outsiders.Say(carrier, "feastprize", champ);
            Outsiders.Say(champ, "feastprizethanks", carrier);
            Diplomacy.Dirty();
        }

        // ================= The night =================

        private static readonly HashSet<int> sour = new HashSet<int>();

        /// <summary>
        /// After the feast, a guest's night may go sour (one guest night in 15, a hot temper more): words with one of the
        /// colony's near them come to blows, the outsiders' own fistfight (the game's social fight). Only with the deeper
        /// hooks, which keep a brawl from counting as the colony attacking its guests; never a child on either side.
        /// </summary>
        private static void SourNights(GuestFeast p, Map map)
        {
            if (!sour.Add(p.id) || !RimfolkPatches.On) return;
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here))
            {
                var x = g.Party.Where(o => o != null && o.Spawned && Together.Able(o) && !o.DevelopmentalStage.Juvenile()).InRandomOrder().FirstOrDefault();
                if (x == null || !FeastMath.SourNight(Outsiders.Temper(x), false, Rand.Value, HarmScale)) continue;
                var c = map.mapPawns.FreeColonistsSpawned.Where(o => Together.Able(o) && !o.DevelopmentalStage.Juvenile() && o.Position.InHorDistOf(x.Position, 12f)
                                                                 && !o.WorkTagIsDisabled(WorkTags.Violent) && MayBeHurt(o) && SocialLog.Near(o, x))
                    .OrderBy(o => o.Position.DistanceToSquared(x.Position)).FirstOrDefault();
                if (c == null) continue;
                if (!Outsiders.FeastBrawl(c, x)) continue;
                g.sour = true;
                // together: the two came to words where they stood; the fight is the game's own on the map.
                Tell.Message(NewsKind.World, Importance.Important, map, FeastWords.T("CC_Feast_Msg_Sour", x.Named("GUEST"), c.Named("PAWN")), new LookTargets(x),
                    MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>The "Hurts from everyday life" scale, once the harm review's setting is built; 1 until then.</summary>
        public static float HarmScale => 1f;

        /// <summary>Under the harm rule's shared cap: not a child, not pregnant, not hurt in the last three days.</summary>
        public static bool MayBeHurt(Pawn p)
        {
            if (p?.health?.hediffSet == null) return false;
            float since = float.MaxValue;
            foreach (var hd in p.health.hediffSet.hediffs)
                if (hd is Hediff_Injury) since = Math.Min(since, hd.ageTicks / (float)GenDate.TicksPerDay);
            bool pregnant = p.health.hediffSet.hediffs.Any(hd => hd.def?.defName != null && hd.def.defName.StartsWith("Pregnant"));
            return FeastMath.MayBeHurt(p.DevelopmentalStage.Juvenile(), pregnant, since);
        }

        private static void Night(DiplomacyHome h, GuestFeast p, Map map, int hour, int now)
        {
            if (FeastMath.Leave(hour, now - p.nightAt) || now - p.nightAt > 20 * GenDate.TicksPerHour) End(h, p, map, false);
        }

        // ================= The end =================

        /// <summary>
        /// First light (or it ended early): the guests walk home, and each one's welcome moves their regard: their notable
        /// remembers it, their head too when the head wasn't the guest, their people the deed "feasted" for a good one, and
        /// "slighted" for an empty plate. The feast held goes into the history, and the letter says how it went.
        /// </summary>
        public static void End(DiplomacyHome h, GuestFeast p, Map map, bool insulted)
        {
            if (p == null || h == null) return;
            FeastContests.Stop(p);
            var lines = new List<string>();
            int contests = p.results.Count(r => !r.off);
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here || g.state == GuestState.Gone && g.pawn != null && g.pawn.MapHeld != map && p.arrived >= 0))
            {
                if (g.pawn == null || g.pawn.Dead) continue;
                bool hopeMet = g.hope == FeastHope.MealAndMusic ? g.fine && p.music : p.results.Any(r => !r.off && FeastMath.Answers(g.hope, r.kind));
                g.welcome = insulted ? -15f : FeastMath.Welcome(g.ate, g.fine, hopeMet, contests, g.prize, g.bed, g.floor, g.hurt, g.sour, Empire(g));
                var n = NotableBook.Instance?.Get(g.notable);
                if (n != null)
                {
                    n.personal += g.welcome;
                    n.Remember(FeastWords.P(g.welcome >= 0 ? "CC_Feast_Note_Good" : "CC_Feast_Note_Poor", FeastWords.Name(map).Named("FEAST")), g.welcome);
                }
                var head = FavourCalls.HeadOf(g.people, map);
                if (head != null && head != n && g.welcome > 0)
                {
                    float share = FeastMath.HeadShare(g.welcome);
                    head.personal += share;
                    head.Remember(FeastWords.P("CC_Feast_Note_Head", FeastWords.Name(g).Named("NAME"), FeastWords.Name(map).Named("FEAST")), share);
                }
                if (g.people != null && !insulted)
                {
                    if (!g.ate && p.held) FactionMemory.Note(g.people, "slighted", FeastWords.Name(g));
                    else if (g.welcome >= FeastMath.RememberedAt) FactionMemory.Note(g.people, "feasted", FeastWords.Name(g), FeastMath.DeedWeight(g.welcome));
                }
                lines.Add(FeastWords.P(g.welcome >= FeastMath.RememberedAt ? "CC_Feast_End_Glad" : g.welcome >= 0 ? "CC_Feast_End_Fair" : "CC_Feast_End_Poor",
                    FeastWords.Name(g).Named("NAME"), Why(g, hopeMet).Named("WHY")));
            }
            NotableBook.Changed();
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here)) g.state = GuestState.Gone;
            LeaveAll(p, map);
            p.ended = Now;
            if (p.arrived >= 0)
            {
                h.feastHeld = Now;
                var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(map), HistoryRole.Did) };
                foreach (var g in p.guests.Where(g => g.state == GuestState.Gone && g.pawn != null))
                    actors.Add(new HistoryActor(WorldLedger.PersonOf(g.pawn), HistoryRole.For));
                foreach (var f in p.guests.Where(g => g.state == GuestState.Gone && g.people != null).Select(g => g.people).Distinct())
                    actors.Add(new HistoryActor(Refs.Of(f), HistoryRole.For));
                string held = p.championName.NullOrEmpty()
                    ? FeastWords.P("CC_Feast_Hist_Held", FeastWords.Name(map).Named("FEAST"), FeastWords.Guests(p, true).Named("GUESTS"))
                    : FeastWords.P("CC_Feast_Hist_HeldChampion", FeastWords.Name(map).Named("FEAST"), FeastWords.Guests(p, true).Named("GUESTS"), p.championName.Named("CHAMPION"));
                p.heldEntry = Diplomacy.Write(DiplomacyMath.Feast, actors, Diplomacy.Colony(map), held, true)?.id ?? -1;
                if (p.purpose == FeastPurpose.Feast)
                {
                    string results = FeastWords.Results(p);
                    string text = FeastWords.T(insulted ? "CC_Feast_End_Insulted" : "CC_Feast_End", FeastWords.Guests(p, true).Named("GUESTS"), FeastWords.Name(map).Named("FEAST"))
                                  + (results.NullOrEmpty() ? "" : "\n\n" + results) + (lines.Count == 0 ? "" : "\n\n" + string.Join("\n", lines));
                    float mean = p.guests.Where(g => g.state == GuestState.Gone).Select(g => g.welcome).DefaultIfEmpty(0f).Average();
                    Tell.Letter(NewsKind.World, Importance.Notable, map, FeastWords.P("CC_Feast_End_Label", FeastWords.Name(map).Named("FEAST")), text,
                        insulted || mean < 0 ? LetterDefOf.NegativeEvent : mean >= FeastMath.RememberedAt ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent,
                        LookTargets.Invalid);
                }
            }
            Close(h, p, map);
        }

        /// <summary>The welcome's main reason in a few words: what pleased or slighted them most.</summary>
        private static string Why(FeastGuest g, bool hopeMet)
        {
            if (!g.ate) return FeastWords.P("CC_Feast_For_Unfed");
            if (g.sour) return FeastWords.P("CC_Feast_For_Sour");
            if (g.prize) return FeastWords.P("CC_Feast_For_Prize");
            if (hopeMet) return FeastWords.P("CC_Feast_For_Hope", FeastWords.Hope(g.hope).Named("HOPE"));
            if (Empire(g) && !g.fine) return FeastWords.P("CC_Feast_For_PlainMeal");
            if (g.floor && !g.bed) return FeastWords.P("CC_Feast_For_Floor");
            return FeastWords.P("CC_Feast_For_Table");
        }

        /// <summary>The guests get up and walk home; beds go back to the colony.</summary>
        private static void LeaveAll(GuestFeast p, Map map)
        {
            if (map == null) return;
            try
            {
                foreach (var l in Lords(map, p)) l.ReceiveMemo(LordJob_FeastGuests.MemoLeave);
                foreach (var g in p.guests)
                    foreach (var x in g.Party.ToList())
                    {
                        if (x == null || x.Dead || x.IsPrisonerOfColony) continue;
                        Hosting.GetUp(x);
                        Hosting.GiveBack(x, map);
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: seeing the guests off", e);
            }
        }

        // ================= Lost, hurt, insulted =================

        /// <summary>One of theirs lost to their group (LordJob_FeastGuests): dead, a death at the colony; taken, an insult.</summary>
        public static void Lost(int homeKey, Pawn x, PawnLostCondition condition)
        {
            var p = Of(x, out var h, out var g);
            var map = h?.Map;
            if (p == null || map == null || !p.OnMap) return;
            if (x.Dead && g != null && g.pawn == x)
            {
                Died(h, p, map, g);
                return;
            }
            if (condition == PawnLostCondition.MadePrisoner || condition == PawnLostCondition.ChangedFaction) Insulted(h, p, map);
        }

        /// <summary>A guest died at the colony: the feast is over at once, and their people remember it as a slight, more if they were out in the open.</summary>
        private static void Died(DiplomacyHome h, GuestFeast p, Map map, FeastGuest g)
        {
            var x = g.pawn;
            var room = x?.Corpse?.GetRoom();
            bool sheltered = room != null && !room.PsychologicallyOutdoors;
            if (g.people != null) FactionMemory.Note(g.people, "slighted", FeastWords.Name(g), sheltered ? 4 : 12);
            var head = FavourCalls.HeadOf(g.people, map);
            if (head != null)
            {
                float shift = EnvoyMath.DiedRegard(sheltered);
                head.personal += shift;
                head.Remember(FeastWords.P("CC_Feast_Note_Died", FeastWords.Name(g).Named("NAME"), FeastWords.Name(map).Named("FEAST")), shift);
                NotableBook.Changed();
            }
            g.state = GuestState.Gone;
            Tell.Letter(NewsKind.World, Importance.Important, map, FeastWords.P("CC_Feast_Died_Label", FeastWords.Name(g).Named("NAME")),
                FeastWords.T("CC_Feast_Died", FeastWords.Name(g).Named("NAME"), FeastWords.People(g.people).Named("PEOPLE"), FeastWords.Name(map).Named("FEAST")),
                LetterDefOf.NegativeEvent, x?.Corpse != null && x.Corpse.Spawned ? new LookTargets(x.Corpse) : LookTargets.Invalid, g.people);
            CallOff(h, p, map, null, false);
        }

        /// <summary>Hurt by the colony while its guests, or one of them taken: they leave at once, offended.</summary>
        public static void Harmed(int homeKey)
        {
            var map = Homes.MapOf(homeKey);
            var h = DiplomacyBook.Instance?.Existing(map);
            var p = h?.feast;
            if (p == null || !p.OnMap) return;
            Insulted(h, p, map);
        }

        private static void Insulted(DiplomacyHome h, GuestFeast p, Map map)
        {
            foreach (var g in p.guests.Where(g => g.state == GuestState.Here && g.people != null))
                FactionMemory.Note(g.people, "slighted", FeastWords.Name(g), 12);
            End(h, p, map, true);
        }

        /// <summary>A home is no longer the colony's: its feast ends (the guests go home).</summary>
        internal static void HomeLost(DiplomacyHome h)
        {
            if (h?.feast == null) return;
            CallOff(h, h.feast, h.Map, null, false);
        }

        // ================= The day's suggestions =================

        /// <summary>
        /// Once a day (Diplomacy's day): the steward may suggest a feast with guests, once a season at most, when the colony is
        /// fed and quiet and a notable who thinks well of it (20 or more) is within reach: the council's question. Tales of
        /// the colony's own at a neighbour's feast are let go once old.
        /// </summary>
        public static void Daily(int now)
        {
            FeastAway.Prune(now);
            if (!On || !Tell.CanAsk || !(ColonyCouncilMod.Settings?.offices ?? false)) return;
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            foreach (var home in Homes.All)
            {
                if (home == null || MapFacts.InVacuum(home)) continue;
                var h = book.HomeOf(home);
                var steward = ColonyState.For(home)?.offices?.HolderOf("Council_Office_Steward");
                if (steward == null || steward.Dead || steward.MapHeld != home) continue;
                var friend = Candidates(home).FirstOrDefault(n => InviteWhyNot(n, home) == null && NotableBook.Opinion(n) >= FeastMath.SuggestRegard);
                int colonists = home.mapPawns.FreeColonistsSpawnedCount;
                bool fed = MealsInStores(home) >= colonists + 3;
                if (!FeastMath.Suggests(PlanWhyNot(home) == null, h.feastSuggested, now, friend != null, fed, !MapFacts.ThreatOn(home), Rand.Value)) continue;
                h.feastSuggested = now;
                Suggest(home, steward, friend);
                return;
            }
        }

        /// <summary>The steward's suggestion as the council's question: plan it (the dialog opens with their people's notable picked), or not now.</summary>
        public static void Suggest(Map home, Pawn steward, Notable friend)
        {
            var def = FeastDefOf.Rimfolk_FeastSuggest;
            if (def == null || steward == null || friend == null) return;
            string text = FeastWords.T("CC_Feast_Suggest", steward.Named("PAWN"), FeastWords.Name(home).Named("FEAST"), NotableBook.Called(friend).Named("NAME"),
                              FeastWords.People(friend.faction).Named("PEOPLE"))
                          + "\n\n" + FeastWords.P("CC_Feast_Suggest_How", FeastMath.MaxGuests.Named("GUESTS"), FeastMath.MaxContests.Named("CONTESTS"));
            var letter = LetterMaker.MakeLetter(FeastWords.P("CC_Feast_Suggest_Label", FeastWords.Name(home).Named("FEAST")), text, def, new LookTargets(steward), friend.faction);
            if (letter is ChoiceLetter_FeastSuggest l)
            {
                l.home = Homes.KeyOf(home);
                l.notable = friend.id;
            }
            Tell.Ask(letter, home);
        }

        // ================= Words for the pages =================

        /// <summary>Why they are here, for Here and away ("a guest at the feast"), or null for anyone else.</summary>
        public static string HereAs(Pawn x)
        {
            var p = Of(x, out var h, out var g);
            if (p == null || g == null || !p.OnMap) return null;
            string feast = FeastWords.Name(h.Map);
            if (g.pawn == x) return FeastWords.P(p.purpose == FeastPurpose.Wedding ? "CC_Feast_HereAs_Wedding" : "CC_Feast_HereAs", feast.Named("FEAST"));
            return FeastWords.P("CC_Feast_HereAs_Retinue", FeastWords.Name(g).Named("NAME"), feast.Named("FEAST"));
        }
    }

    /// <summary>The steward suggests a feast with guests: plan it (the dialog opens with their notable picked), or not now.</summary>
    public class ChoiceLetter_FeastSuggest : ChoiceLetter
    {
        public int home = HomesMath.None;
        public int notable = -1;

        private bool Valid(out Map map, out Notable n)
        {
            map = Homes.MapOf(home) ?? Homes.Seat;
            n = NotableBook.Instance?.Get(notable);
            return map != null && Feasts.PlanWhyNot(map) == null;
        }

        public bool Waiting => !ArchivedOnly && Valid(out _, out _);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !Valid(out _, out _))
                {
                    yield return Option_Close;
                    yield break;
                }
                yield return new DiaOption(FeastWords.P("CC_Feast_Opt_Plan"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        if (!Valid(out var map, out var n)) return;
                        // apart: the council's answer opens the plan; the invitations go out from it.
                        Find.WindowStack.Add(new Dialog_Feast(map, n?.faction, n));
                    },
                    resolveTree = true
                };
                yield return new DiaOption(FeastWords.P("CC_Feast_Opt_NotNow"))
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref notable, "notable", -1);
        }
    }
}
