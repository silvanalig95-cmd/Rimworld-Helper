using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony notices what happens to it, the moment it happens (Harmony hooks in Patches/Patch_WorldEvents.cs):
    /// weddings, births, homecomings from long journeys, and friends breaking down. Each leaves journal lines, a
    /// line in the chronicle when it matters, and small moods where they fit.
    /// </summary>
    public static class WorldReactions
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static bool On => S != null && S.worldReactions && RimfolkPatches.On && CouncilGameComponent.Instance != null;

        private static int ownBreaks;

        /// <summary>One of Rimfolk's own sulks or tantrums is starting (it gives its own reason).</summary>
        public static bool InOwnBreak => ownBreaks > 0;

        /// <summary>Wrap Rimfolk's own sulks and tantrums, so they aren't also told as breakdowns.</summary>
        public static System.IDisposable OwnBreak()
        {
            ownBreaks++;
            return new Done();
        }

        private sealed class Done : System.IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                ownBreaks--;
            }
        }

        /// <summary>Close friends of someone, at home and able to care.</summary>
        private static IEnumerable<Pawn> FriendsOf(Pawn p, int minOpinion = 30) =>
            PawnsFinder.AllMaps_FreeColonistsSpawned.Where(o => o != p && People.HasInnerLife(o) && !o.DevelopmentalStage.Baby()
                                                                && (o.relations?.OpinionOf(p) ?? 0) >= minOpinion);

        public static void Wedding(Pawn a, Pawn b)
        {
            if (!On || a == null || b == null || !(a.IsColonist || b.IsColonist)) return;
            var comp = CouncilGameComponent.Instance;
            string text = "CC_World_Wedding".Translate(a.Named("A"), b.Named("B")).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, a.IsColonist ? a : b));
            foreach (var p in new[] { a, b }.Where(p => p.IsColonist)) comp.journals.Write(p, "Council_Journal_Wedding", null, (p == a ? b : a).Named("OTHER"));
            foreach (var f in FriendsOf(a).Concat(FriendsOf(b)).Distinct().Where(f => f != a && f != b).Take(4))
                comp.journals.Write(f, "Council_Journal_WeddingGuest", null, a.Named("A"), b.Named("B"));
            // A wedding is the oldest custom there is.
            comp.character?.Shift(new WayShift { tradition = 2f }, text);
        }

        public static void Birth(Pawn baby, Pawn mother, Pawn father)
        {
            if (!On || baby == null || baby.Dead) return;
            var parents = new[] { mother, father }.Where(p => p != null && !p.Dead && p.IsColonist).ToList();
            if (parents.Count == 0 && baby.Faction != Faction.OfPlayer) return;
            var comp = CouncilGameComponent.Instance;
            string text = parents.Count > 0
                ? "CC_World_Birth".Translate(baby.Named("BABY"), parents[0].Named("PARENT")).Resolve()
                : "CC_World_BirthNoParent".Translate(baby.Named("BABY")).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, parents.FirstOrDefault()));
            foreach (var p in parents) comp.journals.Write(p, "Council_Journal_Birth", null, baby.Named("BABY"));
        }

        /// <summary>Colonists back from a journey of at least two days (counted from when each was last at home).</summary>
        public static void Homecoming(List<Pawn> pawns)
        {
            if (!On || pawns == null) return;
            var comp = CouncilGameComponent.Instance;
            var back = pawns.Where(p => p != null && !p.Dead && p.IsColonist && People.HasInnerLife(p) && comp.AwayTicks(p) >= GenDate.TicksPerDay * 2).ToList();
            if (back.Count == 0) return;
            int awayTicks = back.Max(p => comp.AwayTicks(p));
            string days = TextUtil.DaysString(awayTicks);
            foreach (var p in back)
            {
                CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_HomeAgain, null, -1f, "CC_Cause_HomeAgain".Translate(days).Resolve());
                comp.journals.Write(p, "Council_Journal_Homecoming", null, days.Named("DAYS"));
            }
            // Those who stayed and missed them.
            foreach (var p in back)
                foreach (var f in FriendsOf(p, 40).Where(f => !back.Contains(f)).Take(3))
                    CouncilGameComponent.GiveThought(f, CouncilDefOf.Council_GladTheyreBack, p, -1f, "CC_Cause_GladBack".Translate(p.LabelShort).Resolve());
            string names = TextUtil.JoinAnd(back.Select(p => p.LabelShort).ToList());
            string text = "CC_World_Homecoming".Translate(names, days).Resolve();
            Notify.Message(text, new LookTargets(back), MessageTypeDefOf.PositiveEvent, Importance.Flavor);
            if (awayTicks >= GenDate.TicksPerDay * 8) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, back[0]));
        }

        /// <summary>A colonist broke down from their mood: their friends worry, and the worst breaks are remembered.</summary>
        public static void Breakdown(Pawn p, MentalStateDef state)
        {
            if (!On || ownBreaks > 0 || p == null || state == null || !p.IsColonist || !People.HasInnerLife(p)) return;
            var comp = CouncilGameComponent.Instance;
            foreach (var f in FriendsOf(p, 40).Take(3))
                comp.journals.Write(f, "Council_Journal_FriendBroke", null, p.Named("OTHER"));
            if (state.IsAggro)
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_World_Breakdown".Translate(p.Named("PAWN"), state.label.Named("STATE")).Resolve(), p));
        }

        /// <summary>
        /// A fight at home is over: if the colony saw off a real threat, it tells itself the story. Who fought, how many
        /// of them fell, what it cost, and who stood out.
        /// </summary>
        public static void Victory(CouncilGameComponent comp, List<Pawn> fighters, Faction foe, int kills, List<string> fallen, Dictionary<Pawn, int> killsBy)
        {
            if (!On || comp == null || fighters.NullOrEmpty()) return;
            // A real fight: a raid seen off, or a pack brought down. One stray wolf isn't a victory.
            if (kills < 3 && !(foe != null && kills >= 1)) return;
            var home = Find.Maps.Where(m => m.IsPlayerHome).ToList();
            if (home.Count == 0 || home.All(m => m.mapPawns.FreeColonistsSpawnedCount == 0)) return;
            // With raids every couple of days (a hard storyteller), a clean win is routine: told in the chronicle, not
            // celebrated each time. A costly one is always felt.
            int now = Find.TickManager.TicksGame;
            bool routine = fallen.Count == 0 && now - comp.social.lastVictoryTick < GenDate.TicksPerDay * 4;
            if (!routine) comp.social.lastVictoryTick = now;
            var hero = killsBy.Where(kv => kv.Key != null && !kv.Key.Dead && kv.Key.IsColonist && kv.Value >= 2).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            // Peoples nobody can talk to (mechanoids, insects) go by what they are.
            string foeName = foe == null ? "CC_Victory_Beasts".Translate().ToString() : foe.Hidden ? foe.def.pawnsPlural : foe.Name;
            bool costly = fallen.Count > 0;
            string text = costly
                ? "CC_Victory_Costly".Translate(foeName, kills, TextUtil.JoinAnd(fallen)).Resolve()
                : "CC_Victory_Clean".Translate(foeName, kills).Resolve();
            if (hero != null) text += " " + "CC_Victory_Hero".Translate(hero.Named("PAWN"), killsBy[hero]).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, hero ?? fighters[0]));
            if (routine) return;
            Notify.Message(text, new LookTargets(fighters.Where(p => p.Spawned)), MessageTypeDefOf.PositiveEvent, Importance.Notable);
            using var _cause = Causes.Push("CC_Cause_Victory".Translate(foeName).Resolve());
            var thought = costly ? CouncilDefOf.Council_VictoryCostly : CouncilDefOf.Council_Victory;
            foreach (var map in home)
                foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()).ToList())
                    CouncilGameComponent.GiveThought(p, thought);
            if (hero != null) CouncilGameComponent.GiveThought(hero, CouncilDefOf.Council_BattleHero);
            if (!costly) VisualTouches.Victory(fighters);
            var tags = new List<string> { costly ? "costly" : "clean" };
            if (foe == null) tags.Add("beasts");
            var writers = new List<Pawn>();
            if (hero != null) writers.Add(hero);
            if (hero == null) writers.AddRange(fighters.Where(p => !p.Dead && p.IsColonist).InRandomOrder().Take(1));
            foreach (var w in writers)
            {
                var t = new List<string>(tags);
                if (w == hero) t.Add("hero");
                comp.journals.WriteTagged(w, "Council_Journal_Victory", t.ToArray(), null, foeName.Named("ENEMY"), kills.Named("COUNT"),
                    (costly ? TextUtil.JoinAnd(fallen) : "").Named("DEAD"));
            }
        }

        /// <summary>A quest the colony took on is over, for better or worse.</summary>
        public static void QuestEnded(CouncilGameComponent comp, string name, bool success)
        {
            if (comp == null || name.NullOrEmpty()) return;
            string text = (success ? "CC_World_QuestDone" : "CC_World_QuestFailed").Translate(name).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, text));
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && !p.IsQuestLodger()).ToList();
            if (success)
            {
                using var _cause = Causes.Push("CC_Cause_Quest".Translate(name).Resolve());
                foreach (var p in pawns) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_QuestDone);
            }
            var writer = pawns.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (writer != null) comp.journals.Write(writer, success ? "Council_Journal_QuestDone" : "Council_Journal_QuestFailed", null, name.Named("TALE"));
        }
    }
}
