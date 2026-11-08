using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Harm, part 4: quarrels that come to blows (Harm/HarmFour.cs is the hub). Each fight is the game's own social fight,
    /// and the game's own message tells it; under the cap for both, never a child.
    /// <list type="bullet">
    /// <item>The groups come to blows (3.6.1): a dig at the other lot between two groups bitter with each other (a rivalry
    /// of 60 or more), at a shared table or a gathering, comes to blows one time in eight (half with the peacemaker in office,
    /// half under the shared meals decree), and up to two more pairs of the two groups nearby join in. With the groups.</item>
    /// <item>The sore loser (1.4.2): a hot-tempered or bitter candidate who lost a close race (a vote in it, or a fifth of the
    /// votes) walks over to the winner one to three days after, one loss in ten (half when the winner was gracious, a word
    /// and a handshake at the count): hard words, and blows half the time. With the offices' elections.</item>
    /// <item>A guest's night goes sour (2.5.1): a guest given a bed for the night (Rimfolk's visitors staying inside, and with
    /// Hospitality its guests in a guest bed), after supper, quarrels with one of the colony near them one night in 15 by the
    /// guest's temper, half again after a drink, half with the warden of guests in office, half under the best-behavior
    /// decree: the outsiders' own fistfight (no war of it), and a visitor goes on at first light. With the deeper hooks.</item>
    /// <item>The rematch (2.8.1): one in three of those who lost a duel to one of the colony, or took a bout's hard blow from
    /// them, holds it; on a later visit within the year one of their people (themselves, or kin) asks for the colonist by
    /// name: a duel to first blood, or a bout, by the outsiders' own rules, if the colonist takes it. A good word or a gift
    /// to their people before then settles it. With the outsiders' bouts and duels.</item>
    /// </list>
    /// </summary>
    public static class Quarrels
    {
        private static int Now => Find.TickManager.TicksGame;

        public static void Reset()
        {
            nights.Clear();
        }

        private static bool CanFight(Pawn p) => p != null && Together.Able(p) && !p.DevelopmentalStage.Juvenile() && p.interactions != null && !p.WorkTagIsDisabled(WorkTags.Violent)
                                               && Harm.May(p, HurtClass.Fight);

        // ================= The groups come to blows (3.6.1) =================

        /// <summary>Both at a table or a gathering: in a dining or rec room together, or in one gathering's lord.</summary>
        private static bool AtTable(Pawn a, Pawn b)
        {
            var la = a.GetLord();
            if (la != null && la == b.GetLord()) return true;
            var room = a.GetRoom();
            if (room == null || room != b.GetRoom() || room.PsychologicallyOutdoors) return false;
            string role = room.Role?.defName;
            return role == "DiningRoom" || role == "RecRoom";
        }

        /// <summary>A dig at the other lot just made (GroupMoments.After): between two bitter groups at a table, maybe a brawl.</summary>
        public static void Dig(Pawn a, Pawn b, SocialGroup ga, SocialGroup gb)
        {
            if (!Harm.On || a?.Map == null || b?.Map != a.Map || ga == null || gb == null || ga == gb || !Harm.SystemOn(HarmKinds.GroupBrawl.system)) return;
            float rivalry = Groups.Rivalry(a, b);
            bool peacemaker = ColonyState.For(a.Map)?.offices?.Peacemaker != null;
            float chance = HarmFourMath.GroupBrawl(rivalry, AtTable(a, b), peacemaker, HarmFour.Decree(a, "Council_Decree_SharedMeals"));
            if (chance <= 0f || !CanFight(a) || !CanFight(b) || !Harm.Roll(HarmKinds.GroupBrawl, chance)) return;
            Brawl(a, b, ga, gb);
        }

        private static void Brawl(Pawn a, Pawn b, SocialGroup ga, SocialGroup gb)
        {
            // together: the dig was the game's own conversation between the two, face to face.
            HarmFour.Say(b, "Rimfolk_Pool_GroupBrawl", a);
            if (!Harm.Fight(b, a, HarmKinds.GroupBrawl)) return;
            var ours = ga.Pawns.Where(p => p != a && p != b && p.Map == a.Map && p.Position.InHorDistOf(a.Position, HarmFourMath.GroupJoinRange) && !gb.Has(p) && CanFight(p)).ToList();
            var theirs = gb.Pawns.Where(p => p != a && p != b && p.Map == a.Map && p.Position.InHorDistOf(a.Position, HarmFourMath.GroupJoinRange) && !ga.Has(p) && CanFight(p)).ToList();
            int join = HarmFourMath.GroupJoins(ours.Count, theirs.Count);
            for (int i = 0; i < join; i++)
            {
                // together: both are within a few steps of the brawl.
                HarmFour.Say(theirs[i], "Rimfolk_Pool_GroupBrawlJoin", ours[i]);
                Harm.Fight(theirs[i], ours[i], HarmKinds.GroupBrawl);
            }
            HarmFour.Mote(a, "CC_HarmFour_Mote_Groups_" + HarmFourMath.GroupsForm(Ages.Ours), ga.Name.Named("ONE"), gb.Name.Named("OTHER"));
        }

        /// <summary>Dev mode: two bitter groups here come to blows now, the nearest of each.</summary>
        public static string DebugGroups(Map map)
        {
            var t = GroupTracker.Instance;
            if (t == null || !GroupLife.Active) return "needs the groups (the social web and the colonists' persona).";
            foreach (var a in map.mapPawns.FreeColonistsSpawned)
                foreach (var b in map.mapPawns.FreeColonistsSpawned)
                {
                    if (a == b || Groups.Rivalry(a, b, out var ga, out var gb) <= 0f || ga == null || gb == null || ga == gb || !CanFight(a) || !CanFight(b)) continue;
                    if (!Together.Can(a, b)) continue;
                    Brawl(a, b, ga, gb);
                    return a.LabelShort + " of " + ga.Name + " and " + b.LabelShort + " of " + gb.Name + " came to blows (rivalry " + Groups.Rivalry(a, b).ToString("0") + ").";
                }
            return "no two of rival groups together and free to fight here (they need to be in talking range).";
        }

        // ================= The sore loser (1.4.2) =================

        /// <summary>A winner who takes it graciously: kind, or good with people (Social 8 or more).</summary>
        public static bool Gracious(Pawn w) => w != null && (w.story?.traits?.HasTrait(TraitDefOf.Kind) ?? false) || (w?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) >= 8;

        /// <summary>A hot temper: the persona's fighting temper of 2 or more.</summary>
        private static bool Hot(Pawn p) => Outsiders.Temper(p) >= 2;

        /// <summary>The count is done (OfficeTracker.ApplyResults): each close race's loser may hold it against the winner.</summary>
        public static void Counted(List<OfficeTracker.SeatCount> count, bool atCount)
        {
            if (!Harm.On || count == null || !Harm.SystemOn(HarmKinds.SoreLoser.system)) return;
            foreach (var sc in count)
            {
                if (sc?.winner == null || sc.unopposed || sc.ranked == null || sc.ranked.Count < 2) continue;
                var w = sc.winner;
                var l = sc.ranked[1].Key;
                if (l == null || l == w || l.Dead || w.Dead || !CanBeSore(l, w)) continue;
                bool close = HarmFourMath.CloseRace(sc.ranked[0].Value, sc.ranked[1].Value, sc.counted);
                bool bitter = (l.relations?.OpinionOf(w) ?? 0) <= HarmFourMath.BitterOpinion;
                float chance = HarmFourMath.SoreLoser(close, Hot(l), bitter, atCount && Gracious(w));
                if (chance <= 0f || !Harm.Roll(HarmKinds.SoreLoser, chance)) continue;
                HarmFour.Wait(new HarmWaiting
                {
                    what = HarmWaiting.Sore, kind = HarmKinds.SoreLoser.key, pawn = l, other = w, name = sc.race?.office?.Name,
                    due = Now + HarmFourMath.SoreTicks(Rand.Value)
                });
            }
        }

        private static bool CanBeSore(Pawn l, Pawn w) => l.RaceProps.Humanlike && w.RaceProps.Humanlike && !l.DevelopmentalStage.Juvenile() && !w.DevelopmentalStage.Juvenile();

        /// <summary>A sore loser's day has come: they go and find the winner in their free time. True when done with.</summary>
        public static bool SoreHour(HarmWaiting x, int now)
        {
            var l = x.pawn;
            var w = x.other;
            if (l == null || w == null || l.Dead || w.Dead) return true;
            if (x.flag) return true;
            Together.When("sore|" + l.thingIDNumber, l, w, () =>
            {
                if (x.flag || l.Dead || w.Dead) return;
                x.flag = true;
                // together: the loser came to the winner (Together.When), face to face.
                HarmFour.Say(l, "Rimfolk_Pool_SoreLoser", w);
                if (Rand.Chance(HarmFourMath.SoreBlows) && CanFight(l) && CanFight(w) && Harm.Fight(l, w, HarmKinds.SoreLoser)) return;
                HarmFour.Say(w, "Rimfolk_Pool_SoreLoserReply", l);
            }, 24f, 0f);
            return x.flag;
        }

        // ================= A guest's night goes sour (2.5.1) =================

        /// <summary>The guest nights already rolled (a group's key and the day), session memory.</summary>
        private static readonly HashSet<string> nights = new HashSet<string>();

        /// <summary>Hospitality is there: its guests in the colony's guest beds are the guests of a night.</summary>
        public static bool HospitalityLoaded => ModsConfig.IsActive(Guests.Mod);

        /// <summary>The hour: in the evening after supper, each guest group with a bed for the night once.</summary>
        public static void GuestEvenings(int now)
        {
            if (!Harm.On || !Harm.SystemOn(HarmKinds.GuestNight.system) && !Harm.SystemOn(HarmKinds.GuestNightHospitality.system)) return;
            int day = now / HarmMath.DayTicks;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || !HarmFourMath.GuestEvening(GenLocalDate.HourOfDay(map)) || MapFacts.ThreatOn(map)) continue;
                // Rimfolk's visitors staying the night inside.
                var trips = VisitorTracker.Instance?.trips;
                if (trips != null && Harm.SystemOn(HarmKinds.GuestNight.system))
                    foreach (var t in trips)
                    {
                        if (t == null || t.map != map || t.state != VisitState.Here || t.stayNights <= 0 || !t.stayInside) continue;
                        if (!nights.Add("trip" + t.id + "|" + day)) continue;
                        Night(map, t.Here, t);
                    }
                // Hospitality's guests in the colony's guest beds.
                if (!Harm.SystemOn(HarmKinds.GuestNightHospitality.system)) continue;
                foreach (var g in map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer)
                                                                         && GuestBeds.IsGuestBed(p.ownership?.OwnedBed)).GroupBy(p => p.GetLord()?.loadID ?? p.thingIDNumber).ToList())
                {
                    if (!nights.Add("guest" + g.Key + "|" + day)) continue;
                    Night(map, g.ToList(), null);
                }
            }
            if (nights.Count > 300) nights.Clear();
        }

        private static void Night(Map map, List<Pawn> party, VisitorTrip trip)
        {
            var x = party?.Where(p => p != null && p.Spawned && Together.Able(p) && !p.DevelopmentalStage.Juvenile() && p.interactions != null).InRandomOrder().FirstOrDefault();
            if (x == null) return;
            var c = map.mapPawns.FreeColonistsSpawned.Where(o => CanFight(o) && o.Position.InHorDistOf(x.Position, HarmFourMath.GuestNear) && SocialLog.Near(o, x))
                .OrderBy(o => o.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (c == null) return;
            bool drink = x.health.hediffSet.HasHediff(HediffDefOf.AlcoholHigh);
            bool warden = ColonyState.For(map)?.offices?.HolderOf(Interplay.Warden) != null;
            float chance = HarmFourMath.GuestNight(Outsiders.Temper(x), drink, warden, HarmFour.Decree(c, "Council_Decree_Manners"), false);
            var kind = trip != null ? HarmKinds.GuestNight : HarmKinds.GuestNightHospitality;
            if (!debugNight && !Harm.Roll(c, kind, chance, HurtClass.Fight)) return;
            debugNight = false;
            // together: the two are near each other after supper (SocialLog.Near); the words and the fight are the outsiders' own.
            if (!Outsiders.FeastBrawl(c, x)) return;
            Harm.Note(c, kind);
            // A visitor goes on at first light, with a bruise.
            if (trip != null) trip.leaveAt = Math.Min(trip.leaveAt > 0 ? trip.leaveAt : int.MaxValue, NextDawn(map));
        }

        private static int NextDawn(Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            int toDawn = (24 - hour + 6) % 24;
            if (toDawn == 0) toDawn = 24;
            return Now + toDawn * HarmMath.HourTicks;
        }

        private static bool debugNight;

        /// <summary>Dev mode: tonight's first guest group with a bed for the night sours, past its rate, never past the cap.</summary>
        public static string DebugNight()
        {
            debugNight = true;
            nights.Clear();
            return "the first guest group with a bed for the night quarrels with one of the colony near them this evening (seven to eleven), with the deeper hooks.";
        }

        /// <summary>The answer that gives guests a bed for the night says what can come of it (VisitEffect_Stay, inside).</summary>
        public static void Describe(ImpactSheet sheet)
        {
            if (!Harm.On || !Harm.SystemOn(HarmKinds.GuestNight.system)) return;
            sheet.Add(ImpactKind.Health, -1, "CC_Impact_GuestNight".Translate().Resolve(), "CC_Impact_GuestNightBrief".Translate().Resolve(), "guestnight");
        }

        // ================= The rematch (2.8.1) =================

        /// <summary>
        /// A bout or a duel with an outsider is over (Outsiders' bout's end): one who lost the duel to one of the colony, or
        /// took the bout's hard blow from them, may hold it. Not a feast's, a champion's or a rematch's own bout.
        /// </summary>
        public static void BoutOver(OutsiderBout bout)
        {
            if (!Harm.On || bout == null || bout.off || bout.after != null || !Harm.SystemOn(HarmKinds.Rematch.system)) return;
            var c = bout.a;
            var x = bout.b;
            if (c == null || x == null || x.Dead || c.Dead || x.Faction == null || x.Faction.IsPlayer || !HarmFour.Ours(c)) return;
            bool duel = bout.kind == BoutKind.Duel;
            bool lost = duel ? bout.winner == c : bout.kind == BoutKind.Spar && bout.hurt == x;
            if (!lost || !Harm.Roll(HarmKinds.Rematch, HarmFourMath.RematchChance)) return;
            var book = HarmBook.Instance;
            if (book == null || book.waiting.Any(w => w != null && w.what == HarmWaiting.Rematch && w.pawn == c && w.id == x.Faction.loadID)) return;
            HarmFour.Wait(new HarmWaiting
            {
                what = HarmWaiting.Rematch, kind = HarmKinds.Rematch.key, pawn = c, id = x.Faction.loadID, id2 = x.thingIDNumber, name = x.LabelShort, flag = duel, made = Now
            });
        }

        /// <summary>A good word or a gift from one of the colony to one of a people (Outsiders' outcomes): their grudges are settled.</summary>
        public static void Eased(Pawn c, Pawn x)
        {
            var book = HarmBook.Instance;
            if (book == null || x?.Faction == null || book.waiting.Count == 0) return;
            book.waiting.RemoveAll(w => w != null && (w.what == HarmWaiting.Rematch || w.what == HarmSeven.Challenge || w.what == HarmSeven.Heir) && w.id == x.Faction.loadID);
        }

        /// <summary>
        /// A rematch owed, at the hour: once the beaten one has gone home and come again (or their kin come), one of their
        /// people at the colonist's home asks for them by name. True when done with.
        /// </summary>
        public static bool RematchHour(HarmWaiting x, int now)
        {
            var c = x.pawn;
            if (c == null || c.Dead || x.count > 0) return true;
            var faction = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.loadID == x.id);
            if (faction == null || faction.defeated || faction.HostileTo(Faction.OfPlayer)) return true;
            bool here = Find.Maps.Any(m => m.IsPlayerHome && m.mapPawns.AllPawnsSpawned.Any(p => p.thingIDNumber == x.id2));
            if (!here) x.flag2 = true;
            if (!HarmFourMath.RematchDue(x.made, now, x.flag2) || !c.Spawned || c.Map == null || !c.Map.IsPlayerHome) return false;
            var challenger = c.Map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == faction && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile() && Together.Able(p)
                                                                       && p.interactions != null && !p.WorkTagIsDisabled(WorkTags.Violent) && Outsiders.BoutOf(p) == null)
                .OrderByDescending(p => p.thingIDNumber == x.id2).FirstOrDefault();
            if (challenger == null || !Together.Able(c) || !Harm.May(c, HurtClass.Bout) || Outsiders.BoutOf(c) != null) return false;
            bool self = challenger.thingIDNumber == x.id2;
            Together.When("rematch|" + c.thingIDNumber, challenger, c, () =>
            {
                if (x.count > 0) return;
                x.count = 1;
                // together: the challenger came to the colonist (Together.When) and asks face to face.
                HarmFour.Say(challenger, "Rimfolk_Pool_RematchAsk", c, self ? "self" : "kin");
                int melee = c.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0, theirs = challenger.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                if (!HarmFourMath.TakesRematch(Outsiders.Temper(c), melee, theirs) || !Harm.May(c, HurtClass.Bout))
                {
                    HarmFour.Say(c, "Rimfolk_Pool_RematchNo", challenger);
                    return;
                }
                HarmFour.Say(c, "Rimfolk_Pool_RematchYes", challenger);
                Outsiders.Rematch(c, challenger, x.flag);
            }, 12f, 0f);
            // Asked: the grudge is spent.
            return x.count > 0;
        }

        /// <summary>The rematch is over: a hurt the colonist took in it is its kind's (the duel's and the bout's own words tell it).</summary>
        public static void RematchOver(OutsiderBout b, Pawn c)
        {
            if (b == null || c == null || c.Dead || b.off) return;
            foreach (var h in c.health.hediffSet.hediffs)
                if (h is Hediff_Injury && !b.before.Contains(h))
                {
                    Harm.Note(c, HarmKinds.Rematch);
                    return;
                }
        }

        /// <summary>Dev mode: a rematch owed to this colonist by the people of the nearest outsider, due at once.</summary>
        public static string DebugRematch(Pawn c)
        {
            var x = c.Map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer) && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile())
                .OrderBy(p => p.Position.DistanceToSquared(c.Position)).FirstOrDefault();
            if (x == null) return "no outsider at peace on this map to ask for a rematch.";
            HarmFour.Wait(new HarmWaiting
            {
                what = HarmWaiting.Rematch, kind = HarmKinds.Rematch.key, pawn = c, id = x.Faction.loadID, id2 = -1, name = x.LabelShort,
                flag = Outsiders.DuelOn && OutsiderMath.DuelFits((int)(Peoples.TechOf(x.Faction, TechLevel.Industrial)), ModsConfig.RoyaltyActive, x.royalty?.MostSeniorTitle != null),
                flag2 = true, made = Now - (int)(HarmFourMath.RematchAfterDays * HarmMath.DayTicks) - 1
            });
            return x.LabelShort + " of " + x.Faction.Name + " will ask " + c.LabelShort + " for a rematch within the hour, once the two are together.";
        }
    }

    // The rematch's bout (Harm/Quarrels.cs) by the outsiders' own rules (Outsiders/OutsiderOutcomes.cs): the same jobs,
    // swings, witnesses and ending.
    public static partial class Outsiders
    {
        /// <summary>
        /// A rematch asked for by name and taken: a duel to first blood (where the people's ways know duels and duels are
        /// allowed), else a bout. The colonist's job starts at once. False when it can't begin.
        /// </summary>
        public static bool Rematch(Pawn c, Pawn x, bool duel)
        {
            if (c?.Map == null || x == null || c.Map != x.Map || Peaceful(c) || Peaceful(x)) return false;
            bool asDuel = duel && DuelOn;
            if (!asDuel && !SparOn) return false;
            var job = BeginBout(c, x, asDuel ? BoutKind.Duel : BoutKind.Spar, asDuel ? DuelTerms.FirstBlood : DuelTerms.Yield);
            var bout = BoutOf(c);
            if (job == null || bout == null) return false;
            bout.after = b => Quarrels.RematchOver(b, c);
            if (asDuel) CallWitnesses(bout);
            c.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return true;
        }
    }
}
