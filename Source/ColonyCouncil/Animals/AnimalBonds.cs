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
    /// Friendships, rivalries and grief among the colony's named or bonded animals, and an old animal that knows its
    /// handler again (the setting "Friendships and grief among animals"). The player's condition: no letters and no
    /// messages for any of it. It shows on the map (where they sleep, who they follow, a nuzzle or a hiss in the log and
    /// over their heads, a heart), on the animal's card, and in colonists' words (what someone who sees it says, and
    /// their journals):
    /// <list type="bullet">
    /// <item>Two seen asleep side by side three nights (AnimalNatureMath.FriendNights) are friends: by day one falls in
    /// with the other now and then with a nuzzle, and at bedtime a tired one goes to lie down beside its friend asleep.</item>
    /// <item>Two of different kinds about each other may fall out, a cat and a dog far likelier: a hiss or a growl and a
    /// chase now and then. Nobody is hurt.</item>
    /// <item>A bonded animal whose person died goes to lie on their grave every few days for two months, then now and
    /// then; a friend of an animal that died misses it for a month, and goes to its grave if it has one.</item>
    /// <item>One sold, given away or released that meets its old handler again (two days to four months after, on
    /// whatever map) comes over to them, with the game's own nuzzle.</item>
    /// </list>
    /// Friends and rivals not seen together for forty days drift apart. Kept in the <see cref="AnimalBook"/> by id.
    /// </summary>
    public static class AnimalBonds
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => AnimalLife.On && S.animalBonds;

        private static int Now => Find.TickManager.TicksGame;

        private static readonly HashSet<int> recentDead = new HashSet<int>();
        private static readonly HashSet<long> rolled = new HashSet<long>();
        private static int rolledDay = -1;
        private static Game game;

        private static void Fresh()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            recentDead.Clear();
            rolled.Clear();
            rolledDay = -1;
        }

        /// <summary>The colony's animals that died this session (the pets' watch tells it), for the friends they leave.</summary>
        public static IEnumerable<int> RecentDead
        {
            get
            {
                Fresh();
                return recentDead;
            }
        }

        private static int Today => Now / GenDate.TicksPerDay;

        private static long PairKey(int x, int y) => ((long)Math.Min(x, y) << 32) | (uint)Math.Max(x, y);

        // ---------------- What the pets' watch tells it ----------------

        /// <summary>A named or bonded animal of the colony died (Strangers/Pets.cs): its friends start to miss it.</summary>
        public static void Notify_Died(Pawn pet)
        {
            if (pet == null) return;
            Fresh();
            recentDead.Add(pet.thingIDNumber);
            var book = AnimalBook.Instance;
            if (book == null) return;
            foreach (var t in book.ties)
            {
                if (!t.Has(pet.thingIDNumber) || t.Lost) continue;
                if (t.kind == AnimalTie.Friends)
                {
                    t.lostId = pet.thingIDNumber;
                    t.lostTick = Now;
                }
            }
            book.ties.RemoveAll(t => t.Has(pet.thingIDNumber) && !t.Lost);
        }

        /// <summary>
        /// A named or bonded animal left the colony alive (Strangers/Pets.cs): sold, given to a people, released or gone
        /// wild. Kept with the one who handled it, for the day they meet again.
        /// </summary>
        public static void Notify_Left(Pawn pet)
        {
            if (!On || pet == null || pet.Dead) return;
            var handler = AnimalTempers.LastPerson(pet) ?? PetTracker.PersonOf(pet);
            if (handler == null || handler.Dead) return;
            AnimalBook.Instance?.Left(pet, handler, pet.Faction == null);
        }

        // ---------------- The hour ----------------

        /// <summary>
        /// Once an hour (AnimalLife.Hourly), on each home map: at night who sleeps beside whom, at bedtime a tired friend
        /// lying down beside the other, by day who is about whom (a feud may start), one friends' or rivals' moment, one
        /// grave visited; and anywhere, an old animal that knows its handler again.
        /// </summary>
        public static void Hourly()
        {
            if (!On) return;
            Fresh();
            var book = AnimalBook.Instance;
            if (book == null) return;
            if (rolledDay != Today)
            {
                rolledDay = Today;
                rolled.Clear();
            }
            foreach (var map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    int hour = GenLocalDate.HourOfDay(map);
                    var animals = map.mapPawns.SpawnedColonyAnimals.Where(AnimalTempers.Has).ToList();
                    // Friends and feuds are the companions' (grief is anyone's with a temperament).
                    var companions = animals.Where(AnimalTempers.Companion).ToList();
                    if (animals.Count > 0)
                    {
                        if (hour >= 22 || hour <= 4) Nights(book, companions, NightOf(map, hour));
                        if (hour >= 19 && hour <= 23) BedDown(book, companions);
                        bool calm = map.dangerWatcher.DangerRating == StoryDanger.None;
                        if (hour >= 6 && hour <= 19 && calm)
                        {
                            Day(book, companions);
                            Moment(book, companions);
                            if (hour >= 7 && hour <= 17) Grief(book, map, animals, false);
                        }
                    }
                }
                if (map.dangerWatcher.DangerRating == StoryDanger.None) Meetings(book, map, false);
            }
        }

        private static string Name(Pawn a) => a.LabelShort;

        private static void Rename(AnimalTie t, Pawn x)
        {
            if (t.a == x.thingIDNumber) t.nameA = Name(x);
            else if (t.b == x.thingIDNumber) t.nameB = Name(x);
        }

        // ---------------- Friends: who sleeps beside whom ----------------

        /// <summary>Which night it is on this map, by its own clock: the small hours count with the evening before.</summary>
        private static int NightOf(Map map, int hour)
        {
            int day = GenLocalDate.Year(map) * GenDate.DaysPerYear + GenLocalDate.DayOfYear(map);
            return hour < 12 ? day - 1 : day;
        }

        private static void Nights(AnimalBook book, List<Pawn> animals, int today)
        {
            var asleep = animals.Where(a => a.Spawned && !a.Dead && !a.Awake()).ToList();
            for (int i = 0; i < asleep.Count; i++)
                for (int j = i + 1; j < asleep.Count; j++)
                {
                    var x = asleep[i];
                    var y = asleep[j];
                    if (!x.Position.InHorDistOf(y.Position, AnimalNatureMath.SideBySide)) continue;
                    var t = book.TieFor(x, y);
                    if (t.kind == AnimalTie.Rivals) continue;
                    Rename(t, x);
                    Rename(t, y);
                    t.lastSeen = Now;
                    if (!AnimalNatureMath.NightCounts(t.lastNight, today)) continue;
                    t.nights++;
                    t.lastNight = today;
                    if (t.kind == AnimalTie.Pair && t.nights >= AnimalNatureMath.FriendNights
                        && book.Count(x.thingIDNumber, AnimalTie.Friends) < AnimalNatureMath.MaxFriends && book.Count(y.thingIDNumber, AnimalTie.Friends) < AnimalNatureMath.MaxFriends)
                        BecomeFriends(t, x, y);
                }
        }

        private static void BecomeFriends(AnimalTie t, Pawn x, Pawn y)
        {
            t.kind = AnimalTie.Friends;
            t.since = Now;
            AnimalTempers.Noted(x, "CC_BondDid_Friends", y.Named("OTHER"));
            AnimalTempers.Noted(y, "CC_BondDid_Friends", x.Named("OTHER"));
            Write(x, y, "friends");
        }

        /// <summary>The person of one of the two writes it down, once (whoever has one, the first's first).</summary>
        private static void Write(Pawn x, Pawn y, string tag)
        {
            var person = PetTracker.PersonOf(x) ?? PetTracker.PersonOf(y);
            if (person == null) return;
            var pet = PetTracker.PersonOf(x) == person ? x : y;
            var other = pet == x ? y : x;
            CouncilGameComponent.Instance?.journals.WriteTagged(person, "Council_Journal_AnimalBonds", BondTags(pet, other, tag), null, pet.Named("PET"), other.Named("OTHER"));
        }

        private static string[] BondTags(Pawn a, Pawn b, string tag)
        {
            var tags = new List<string> { tag, HerdStoryMath.BodyTag(AnimalFacts.Body(a)) };
            if (a.def == b.def) tags.Add("samekind");
            if (CatAndDog(a, b)) tags.Add("catdog");
            if (PokeWorldLink.IsPokemon(a) || PokeWorldLink.IsPokemon(b)) tags.Add("pokemon");
            return tags.ToArray();
        }

        private static string Family(Pawn a) => AnimalNatureMath.Family(a.def?.defName, a.def?.label, AnimalFacts.Body(a) == HerdStoryMath.HerdBody.Bird, a.RaceProps.Insect);

        private static bool CatAndDog(Pawn a, Pawn b)
        {
            string fa = Family(a), fb = Family(b);
            return fa == "feline" && fb == "canine" || fa == "canine" && fb == "feline";
        }

        /// <summary>At bedtime, a tired friend goes to lie down beside the other, asleep already (one a map an hour).</summary>
        private static void BedDown(AnimalBook book, List<Pawn> animals)
        {
            foreach (var t in book.ties.Where(t => t.kind == AnimalTie.Friends && !t.Lost).InRandomOrder().ToList())
            {
                var x = animals.FirstOrDefault(a => a.thingIDNumber == t.a);
                var y = animals.FirstOrDefault(a => a.thingIDNumber == t.b);
                if (x == null || y == null) continue;
                var (awake, sleeper) = x.Awake() && !y.Awake() ? (x, y) : y.Awake() && !x.Awake() ? (y, x) : (null, null);
                if (awake == null || !AnimalTempers.Free(awake) || (awake.needs?.rest?.CurLevelPercentage ?? 1f) > 0.5f) continue;
                if (awake.Position.InHorDistOf(sleeper.Position, AnimalNatureMath.SideBySide)) continue;
                if (!AnimalTempers.CellNear(awake, sleeper.Position, 1, out var cell)) continue;
                awake.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, cell), JobCondition.InterruptForced, null, false, true);
                AnimalTempers.Noted(awake, "CC_BondDid_SleptBeside", sleeper.Named("OTHER"));
                return;
            }
        }

        // ---------------- By day: who is about whom ----------------

        /// <summary>Two awake near each other by day: friends and rivals are seen together; two others may fall out (once a day a pair, at most).</summary>
        private static void Day(AnimalBook book, List<Pawn> animals)
        {
            var up = animals.Where(a => a.Spawned && a.Awake() && !a.Downed).ToList();
            for (int i = 0; i < up.Count; i++)
                for (int j = i + 1; j < up.Count; j++)
                {
                    var x = up[i];
                    var y = up[j];
                    if (!x.Position.InHorDistOf(y.Position, AnimalNatureMath.AtOdds)) continue;
                    var t = book.TieOf(x.thingIDNumber, y.thingIDNumber);
                    if (t != null && t.kind != AnimalTie.Pair)
                    {
                        t.lastSeen = Now;
                        continue;
                    }
                    if (!rolled.Add(PairKey(x.thingIDNumber, y.thingIDNumber))) continue;
                    var tx = AnimalTempers.Of(x) ?? Temper.Curious;
                    var ty = AnimalTempers.Of(y) ?? Temper.Curious;
                    float chance = AnimalNatureMath.RivalChance(CatAndDog(x, y), x.RaceProps.predator && y.RaceProps.predator, tx, ty, false, x.def == y.def);
                    if (!Rand.Chance(chance)) continue;
                    if (book.Count(x.thingIDNumber, AnimalTie.Rivals) >= AnimalNatureMath.MaxRivals || book.Count(y.thingIDNumber, AnimalTie.Rivals) >= AnimalNatureMath.MaxRivals) continue;
                    BecomeRivals(book, x, y);
                }
            if (rolled.Count > 5000) rolled.Clear();
        }

        internal static AnimalTie BecomeRivals(AnimalBook book, Pawn x, Pawn y)
        {
            var t = book.TieFor(x, y);
            t.kind = AnimalTie.Rivals;
            t.since = t.lastSeen = Now;
            t.nights = 0;
            AnimalTempers.Noted(x, "CC_BondDid_Rivals", y.Named("OTHER"));
            AnimalTempers.Noted(y, "CC_BondDid_Rivals", x.Named("OTHER"));
            Write(x, y, "rivals");
            Squabble(x, y);
            return t;
        }

        internal static AnimalTie BecomeFriendsNow(AnimalBook book, Pawn x, Pawn y)
        {
            var t = book.TieFor(x, y);
            t.nights = Math.Max(t.nights, AnimalNatureMath.FriendNights);
            t.lastSeen = Now;
            BecomeFriends(t, x, y);
            return t;
        }

        /// <summary>One friends' or rivals' moment on the map this hour, at the pair's own pace.</summary>
        private static bool Moment(AnimalBook book, List<Pawn> animals, bool force = false)
        {
            foreach (var t in book.ties.Where(t => t.kind != AnimalTie.Pair && !t.Lost).InRandomOrder().ToList())
            {
                var x = animals.FirstOrDefault(a => a.thingIDNumber == t.a);
                var y = animals.FirstOrDefault(a => a.thingIDNumber == t.b);
                if (x == null || y == null) continue;
                bool rivals = t.kind == AnimalTie.Rivals;
                if (!force && !Rand.Chance(AnimalNatureMath.TieHourChance(rivals))) continue;
                if (Rand.Chance(0.5f)) (x, y) = (y, x);
                if (rivals ? Squabble(x, y) : FallIn(x, y)) return true;
            }
            return false;
        }

        /// <summary>Friends: one falls in with the other for a while, with a nuzzle if they're close, and a heart.</summary>
        private static bool FallIn(Pawn x, Pawn y)
        {
            if (!AnimalTempers.Free(x) || !MoreHabits.Calm(y) || !y.Awake() || !x.Position.InHorDistOf(y.Position, 25f)) return false;
            if (!x.CanReach(y, PathEndMode.Touch, Danger.Some)) return false;
            var follow = JobMaker.MakeJob(JobDefOf.FollowClose, y);
            follow.followRadius = 1.8f;
            follow.expiryInterval = 900;
            follow.checkOverrideOnExpire = true;
            x.jobs.StartJob(follow, JobCondition.InterruptForced, null, false, true);
            // together: the two within sight of each other (SocialLog.Add checks), the one walking over to the other.
            if (SocialLog.Near(x, y))
            {
                FleckMaker.ThrowMetaIcon(x.Position, x.Map, FleckDefOf.Heart, 0.42f);
                SocialLog.Add(x, y, "CC_BondLog_Nuzzle".Translate(x.Named("PET"), y.Named("OTHER")).Resolve());
            }
            AnimalTempers.Noted(x, "CC_BondDid_FollowedFriend", y.Named("OTHER"));
            AnimalTempers.Remark(x, "Council_Pool_SeeAnimals", BondTags(x, y, "friends"), PetTracker.PersonOf(x), y.Named("OTHER"));
            return true;
        }

        /// <summary>Its sound at a rival, by its voice: a cat hisses, a dog growls, a horse snorts, a bird squawks; anything else bares its teeth.</summary>
        private static string HissKey(Pawn a)
        {
            if (PokeWorldLink.IsPokemon(a)) return "CC_BondLog_Hiss_pokemon";
            switch (Family(a))
            {
                case "feline": return "CC_BondLog_Hiss_cat";
                case "canine": return "CC_BondLog_Hiss_dog";
                case "hoofed":
                case "grazer": return "CC_BondLog_Hiss_horse";
                case "bird": return "CC_BondLog_Hiss_bird";
                default: return "CC_BondLog_Hiss_beast";
            }
        }

        /// <summary>Rivals in sight of each other: one hisses or growls (in the log and over its head), the other bolts, and it gives chase a moment.</summary>
        private static bool Squabble(Pawn x, Pawn y)
        {
            // together: only when the two are close and in sight of each other (SocialLog.Near).
            if (!MoreHabits.Calm(x) || !MoreHabits.Calm(y) || !x.Awake() || !y.Awake() || !SocialLog.Near(x, y)) return false;
            if (x.GetLord() != null || y.GetLord() != null) return false;
            SocialLog.Add(x, y, HissKey(x).Translate(x.Named("PET"), y.Named("OTHER")).Resolve());
            var flee = FleeUtility.FleeJob(y, x, 9);
            if (flee != null) y.jobs.StartJob(flee, JobCondition.InterruptForced, null, false, true);
            var chase = JobMaker.MakeJob(JobDefOf.FollowClose, y);
            chase.followRadius = 2f;
            chase.expiryInterval = 240;
            chase.checkOverrideOnExpire = false;
            if (x.CanReach(y, PathEndMode.Touch, Danger.Some)) x.jobs.StartJob(chase, JobCondition.InterruptForced, null, false, true);
            AnimalTempers.Noted(x, "CC_BondDid_Chased", y.Named("OTHER"));
            AnimalTempers.Noted(y, "CC_BondDid_ChasedOff", x.Named("OTHER"));
            AnimalTempers.Remark(x, "Council_Pool_SeeAnimals", BondTags(x, y, "rivals"), PetTracker.PersonOf(x) ?? PetTracker.PersonOf(y), y.Named("OTHER"));
            return true;
        }

        // ---------------- Grief ----------------

        /// <summary>The grave this body lies in, on its map, or null.</summary>
        private static Building_Grave GraveOf(Corpse c) => c?.ParentHolder as Building_Grave;

        /// <summary>The dead person it was bonded to, most lately dead first, with when they died (from their body), or null.</summary>
        private static Pawn DeadPerson(Pawn a, out float daysSince, out Building_Grave grave)
        {
            daysSince = -1f;
            grave = null;
            if (a?.relations == null) return null;
            Pawn best = null;
            foreach (var r in a.relations.DirectRelations)
            {
                var p = r.otherPawn;
                if (r.def != PawnRelationDefOf.Bond || p == null || !p.Dead || p.RaceProps?.Humanlike != true) continue;
                var body = p.Corpse;
                if (body == null) continue;
                float days = (Now - body.timeOfDeath) / (float)GenDate.TicksPerDay;
                if (!AnimalNatureMath.Grieving(days) || (best != null && days >= daysSince)) continue;
                best = p;
                daysSince = days;
                grave = GraveOf(body);
            }
            return best;
        }

        /// <summary>The grave of a friend of its that died, on this map, or null.</summary>
        private static Building_Grave FriendGrave(Map map, int id)
        {
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Grave))
                if (t is Building_Grave g && g.Corpse?.InnerPawn?.thingIDNumber == id) return g;
            return null;
        }

        /// <summary>
        /// A bonded animal whose person lies in a grave on this map goes to lie on it now and then (every few days at first);
        /// a friend of an animal buried here, while it misses it. One a map an hour.
        /// </summary>
        private static bool Grief(AnimalBook book, Map map, List<Pawn> animals, bool force)
        {
            foreach (var a in animals.InRandomOrder())
            {
                if (!AnimalTempers.Free(a)) continue;
                var dead = DeadPerson(a, out float days, out var grave);
                Pawn about = dead;
                if (dead == null || grave == null || grave.Map != map)
                {
                    // A friend it misses, with a grave here.
                    var lost = book.ties.FirstOrDefault(t => t.Lost && t.Has(a.thingIDNumber) && t.lostId != a.thingIDNumber);
                    if (lost == null) continue;
                    grave = FriendGrave(map, lost.lostId);
                    if (grave == null) continue;
                    days = (Now - lost.lostTick) / (float)GenDate.TicksPerDay;
                    about = grave.Corpse?.InnerPawn;
                    if (days > AnimalNatureMath.MissDays) continue;
                }
                if (!force && !Rand.Chance(AnimalNatureMath.GraveHourChance(days))) continue;
                if (!force && !AnimalTempers.Ready("grave|" + a.thingIDNumber, 1.5f)) continue;
                if (!AnimalTempers.CellNear(a, grave.Position, 1, out var cell)) continue;
                var job = AnimalTempers.Linger(AnimalNatureDefOf.Rimfolk_AnimalVigil, cell, grave, AnimalNatureMath.VigilTicks, about);
                a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (a.CurJob != job) continue;
                AnimalTempers.Noted(a, "CC_BondDid_Grave", (about?.LabelShort ?? "?").Named("OTHER"));
                if (dead != null && about == dead)
                {
                    var mourner = Mourner(dead, map);
                    AnimalTempers.Remark(a, "Council_Pool_SeeAnimals", new[] { "grave", "person", HerdStoryMath.BodyTag(AnimalFacts.Body(a)) }, mourner, dead.Named("OTHER"));
                    if (mourner != null && AnimalTempers.Ready("grave-journal|" + a.thingIDNumber, 12f))
                        CouncilGameComponent.Instance?.journals.WriteTagged(mourner, "Council_Journal_AnimalBonds", new[] { "grave", "person" }, null, a.Named("PET"), dead.Named("OTHER"));
                }
                else
                {
                    var keeper = PetTracker.PersonOf(a);
                    AnimalTempers.Remark(a, "Council_Pool_SeeAnimals", new[] { "grave", "friend", HerdStoryMath.BodyTag(AnimalFacts.Body(a)) }, keeper,
                        (about?.LabelShort ?? "?").Named("OTHER"));
                    if (keeper != null && AnimalTempers.Ready("grave-journal|" + a.thingIDNumber, 12f))
                        CouncilGameComponent.Instance?.journals.WriteTagged(keeper, "Council_Journal_AnimalBonds", new[] { "grave", "friend" }, null, a.Named("PET"),
                            (about?.LabelShort ?? "?").Named("OTHER"));
                }
                return true;
            }
            return false;
        }

        /// <summary>Who mourns the dead most here: a partner, else kin, else the one who thought best of them.</summary>
        private static Pawn Mourner(Pawn dead, Map map)
        {
            var living = map.mapPawns.FreeColonistsSpawned.Where(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()).ToList();
            var partner = living.FirstOrDefault(p => p.relations != null && (p.relations.DirectRelationExists(PawnRelationDefOf.Spouse, dead) || p.relations.DirectRelationExists(PawnRelationDefOf.Lover, dead)
                                                                              || p.relations.DirectRelationExists(PawnRelationDefOf.Fiance, dead)));
            if (partner != null) return partner;
            var kin = living.FirstOrDefault(p => p.relations != null && p.GetRelations(dead).Any(r => r.familyByBloodRelation));
            if (kin != null) return kin;
            return living.Where(p => p.relations != null).OrderByDescending(p => p.relations.OpinionOf(dead)).FirstOrDefault(p => p.relations.OpinionOf(dead) >= 20);
        }

        // ---------------- An old animal knows its handler ----------------

        /// <summary>
        /// One of the colony's that left it alive, on the same map as the one who handled it two days to four months on:
        /// it comes over to them (the game's own nuzzle), they say something, and they write it down. One a map an hour.
        /// </summary>
        private static bool Meetings(AnimalBook book, Map map, bool force)
        {
            var here = book.gone.Where(g => g.handler != null && g.handler.Spawned && g.handler.Map == map && ColonyCouncil.Together.Able(g.handler)).ToList();
            if (here.Count == 0) return false;
            var ids = new Dictionary<int, Pawn>();
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (p.RaceProps?.Animal == true) ids[p.thingIDNumber] = p;
            foreach (var g in here.InRandomOrder())
            {
                if (!ids.TryGetValue(g.id, out var a)) continue;
                if (a.Faction == Faction.OfPlayer)
                {
                    // It's the colony's again: nothing to know again.
                    book.gone.Remove(g);
                    continue;
                }
                float daysGone = (Now - g.tick) / (float)GenDate.TicksPerDay;
                float daysMet = g.metTick < 0 ? -1f : (Now - g.metTick) / (float)GenDate.TicksPerDay;
                if (!force && !AnimalNatureMath.Recognises(daysGone, daysMet)) continue;
                if (a.Dead || a.Downed || a.InMentalState || a.HostileTo(Faction.OfPlayer) || !a.Awake()) continue;
                if (!a.Position.InHorDistOf(g.handler.Position, AnimalNatureMath.MeetRange) || !a.CanReach(g.handler, PathEndMode.Touch, Danger.Some)) continue;
                // Not the colony's now: it comes over to them at a trot and stays at their side a while (not the game's
                // nuzzle, which is for an animal of their own), a heart over it as it knows them.
                var come = JobMaker.MakeJob(JobDefOf.FollowClose, g.handler);
                come.followRadius = 1.5f;
                come.expiryInterval = 1200;
                come.checkOverrideOnExpire = true;
                come.locomotionUrgency = LocomotionUrgency.Jog;
                a.jobs.StartJob(come, JobCondition.InterruptForced, null, false, true);
                if (a.CurJob != come) continue;
                FleckMaker.ThrowMetaIcon(a.Position, a.Map, FleckDefOf.Heart, 0.42f);
                // together: written in the log only when the two are already close and in sight (SocialLog.Add checks).
                SocialLog.Add(a, g.handler, "CC_BondLog_KnowsYou".Translate(a.Named("PET"), g.handler.Named("OTHER")).Resolve());
                g.metTick = Now;
                g.meetings++;
                string how = g.wild ? "wild" : "sold";
                AnimalTempers.Remark(a, "Council_Pool_SeeAnimals", new[] { "knowsme", how, g.meetings > 1 ? "again" : null }, g.handler);
                if (g.meetings == 1)
                    CouncilGameComponent.Instance?.journals.WriteTagged(g.handler, "Council_Journal_AnimalBonds", new[] { "knowsme", how }, null, a.Named("PET"));
                return true;
            }
            return false;
        }

        // ---------------- The card ----------------

        /// <summary>Its friends and rivals, for its card: "sleeps beside Pepper", "at odds with Whiskers".</summary>
        public static IEnumerable<string> InspectParts(Pawn a)
        {
            if (!On) yield break;
            var book = AnimalBook.Instance;
            if (book == null) yield break;
            int id = a.thingIDNumber;
            var friends = book.TiesOf(id, AnimalTie.Friends).Where(t => !t.Lost).Select(t => t.NameOf(t.Other(id))).Where(n => !n.NullOrEmpty()).ToList();
            if (friends.Count > 0) yield return "CC_Bond_Inspect_Friends".Translate(friends.ToCommaList(true).Named("NAMES")).Resolve();
            var rivals = book.TiesOf(id, AnimalTie.Rivals).Select(t => t.NameOf(t.Other(id))).Where(n => !n.NullOrEmpty()).ToList();
            if (rivals.Count > 0) yield return "CC_Bond_Inspect_Rivals".Translate(rivals.ToCommaList(true).Named("NAMES")).Resolve();
        }

        /// <summary>Its grief, for its card: its dead person (and their grave on this map), or a friend it misses; null when none.</summary>
        public static string GriefLine(Pawn a)
        {
            if (!On) return null;
            var dead = DeadPerson(a, out _, out var grave);
            if (dead != null)
                return (grave != null && grave.Map == a.Map ? "CC_Bond_Inspect_GraveOf" : "CC_Bond_Inspect_Grieves").Translate(dead.Named("OTHER")).Resolve();
            var book = AnimalBook.Instance;
            var lost = book?.ties.FirstOrDefault(t => t.Lost && t.Has(a.thingIDNumber) && t.lostId != a.thingIDNumber);
            return lost != null ? "CC_Bond_Inspect_Misses".Translate(lost.NameOf(lost.lostId).Named("OTHER")).Resolve() : null;
        }

        // ---------------- Dev actions ----------------

        /// <summary>The dev action: a friends' or rivals' moment on this map now, past the pace.</summary>
        public static string DebugMoment(Map map)
        {
            var book = AnimalBook.Instance;
            if (book == null || map == null) return "no game.";
            var animals = map.mapPawns.SpawnedColonyAnimals.Where(AnimalTempers.Has).ToList();
            return Moment(book, animals, true) ? "a friends' or rivals' moment began (see the log, the bubble and the card)."
                : "no friends or rivals here who are up, near enough and free (friends within 25 cells, rivals in sight of each other).";
        }

        /// <summary>The dev action: a grave visited now by a grieving animal on this map, past the pace.</summary>
        public static string DebugGrief(Map map)
        {
            var book = AnimalBook.Instance;
            if (book == null || map == null) return "no game.";
            var animals = map.mapPawns.SpawnedColonyAnimals.Where(AnimalTempers.Has).ToList();
            return Grief(book, map, animals, true) ? "a grieving animal went to the grave."
                : "no animal here grieving a bonded person buried on this map (dead within " + AnimalNatureMath.LongGriefDays + " days), nor a friend buried here, that is free now.";
        }

        /// <summary>The dev action: an old animal meets its handler now, past the days and the wait between meetings.</summary>
        public static string DebugMeeting(Map map)
        {
            var book = AnimalBook.Instance;
            if (book == null || map == null) return "no game.";
            return Meetings(book, map, true) ? "an old animal of the colony went over to its handler."
                : "none of the " + book.gone.Count + " animals that left the colony is on this map, within " + AnimalNatureMath.MeetRange + " cells of its handler and calm.";
        }
    }
}
