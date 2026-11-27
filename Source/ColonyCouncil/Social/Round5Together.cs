using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The fifth content round's moments between the colony's people (docs/content-round-5-together.md), read from the
    /// map where a social event is cast (SocialEvents.FitsLife and Fire, beside the council's seasons): two sowing the
    /// same fields (both at the game's own sowing job), two old hands
    /// (both a year or more at the colony, by the game's own record), two at the same game (the same board, table, pin or
    /// ring; {THING} names it), and a child who lost someone lately (the game's own memory of the death; {X_nameDef} names
    /// the dead). And the six quirks' words over the head (Immersion/HabitBubbles.cs), each when it fits what they are
    /// doing. Nothing here ticks or is saved.
    /// </summary>
    public static class Round5Together
    {
        /// <summary>Whether the scene holds for A and B now.</summary>
        public static bool Fits(string scene, Pawn a, Pawn b)
        {
            if (a == null || b == null) return false;
            switch (scene)
            {
                case Round5TogetherMath.Sowing:
                    return a.CurJobDef == JobDefOf.Sow && b.CurJobDef == JobDefOf.Sow;
                case Round5TogetherMath.OldHands:
                    return a.RaceProps.Humanlike && b.RaceProps.Humanlike && Round5TogetherMath.OldHand(DaysHere(a)) && Round5TogetherMath.OldHand(DaysHere(b));
                case Round5TogetherMath.Game:
                {
                    var game = GameOf(a);
                    return game != null && Round5TogetherMath.SameGame(game.thingIDNumber, GameOf(b)?.thingIDNumber ?? 0);
                }
                case Round5TogetherMath.ChildGrief:
                    return b.DevelopmentalStage.Child() && !a.DevelopmentalStage.Juvenile() && LostLately(b) != null;
                default:
                    return false;
            }
        }

        /// <summary>The scene's words: the game ({THING}), the one the child lost ({X_nameDef}).</summary>
        public static void Fill(string scene, SocialEventContext ctx)
        {
            if (ctx == null) return;
            switch (scene)
            {
                case Round5TogetherMath.Game:
                    ctx.thing = GameOf(ctx.a)?.def;
                    break;
                case Round5TogetherMath.ChildGrief:
                    ctx.stranger = LostLately(ctx.b);
                    break;
            }
        }

        /// <summary>Days they have lived at the colony (the game's record of time as a colonist).</summary>
        public static float DaysHere(Pawn p) => p?.records == null ? 0f : p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) / GenDate.TicksPerDay;

        /// <summary>The game they are playing at now: the building their pastime is at, when the game counts it a game of skill or chance.</summary>
        public static Thing GameOf(Pawn p)
        {
            var job = p?.CurJob;
            if (job?.def?.joyKind == null) return null;
            var t = job.targetA.Thing;
            if (t?.def == null || !GroupPlaces.IsGames(t.def)) t = job.targetB.Thing;
            return t?.def != null && t.Spawned && GroupPlaces.IsGames(t.def) ? t : null;
        }

        /// <summary>Someone of the colony the child lost in the last few days, by the game's memory of the death naming them; null when none.</summary>
        public static Pawn LostLately(Pawn child)
        {
            var memories = child?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return null;
            for (int i = 0; i < memories.Count; i++)
            {
                var m = memories[i];
                if (m?.def == null || !Round5TogetherMath.ChildGriefThought(m.def.defName) || !Round5TogetherMath.GriefFresh(m.age, GenDate.TicksPerDay)) continue;
                var dead = m.otherPawn;
                // Someone of the colony (a dead pawn keeps its faction, as DilemmaWorkers_Round2.DiedLately reads it).
                if (dead != null && dead != child && dead.Dead && dead.Faction == Faction.OfPlayer && dead.RaceProps.Humanlike) return dead;
            }
            return null;
        }

        // ---------------- The quirks' words over the head ----------------

        /// <summary>
        /// The six quirks of the round, each with its pool and when it fits (Immersion/HabitBubbles.cs calls this with its
        /// own list's Add): the early days at a meal or in their free time with someone by; a word when a newcomer, a child or
        /// one of the old is near enough to hear it; at a game; at a meal with someone near.
        /// </summary>
        public static void Bubbles(Action<string, string, Func<Pawn, int, bool>> add)
        {
            if (add == null) return;
            add("Rimfolk_R5T_Quirk_EarlyDays", "Rimfolk_Pool_R5T_EarlyDays", (p, hour) => p.Awake() && (Eating(p) || Leisure(p)) && Together.Company(p) != null);
            add("Rimfolk_R5T_Quirk_NewcomersFriend", "Rimfolk_Pool_R5T_NewFaces",
                (p, hour) => p.Awake() && !Round5TogetherMath.Newcomer(DaysHere(p)) && Near(p, o => Round5TogetherMath.Newcomer(DaysHere(o))));
            add("Rimfolk_R5T_Quirk_PlaysToWin", "Rimfolk_Pool_R5T_PlaysToWin", (p, hour) => p.Awake() && GameOf(p) != null);
            add("Rimfolk_R5T_Quirk_Peacemaker", "Rimfolk_Pool_R5T_Peacemaker", (p, hour) => p.Awake() && Eating(p) && Together.Company(p) != null);
            add("Rimfolk_R5T_Quirk_TimeForChildren", "Rimfolk_Pool_R5T_Children",
                (p, hour) => p.Awake() && !p.DevelopmentalStage.Juvenile() && Near(p, o => o.DevelopmentalStage.Child()));
            add("Rimfolk_R5T_Quirk_FollowsTheOld", "Rimfolk_Pool_R5T_Elders",
                (p, hour) => p.Awake() && Near(p, o => o.RaceProps.Humanlike && Round5TogetherMath.Elder(Flavor.HumanAge(o))));
        }

        private static bool Eating(Pawn p) => p.CurJobDef == JobDefOf.Ingest;

        private static bool Leisure(Pawn p) => p.CurJob?.def?.joyKind != null;

        /// <summary>One of the colony's own, up and about, who fits and can hear them from where they stand (Together.Hears).</summary>
        private static bool Near(Pawn p, Predicate<Pawn> fits)
        {
            if (p?.Map == null) return false;
            var all = p.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o == p || o.DevelopmentalStage.Baby() || !fits(o)) continue;
                if (Together.Hears(o, p, Round5TogetherMath.NearCells)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A shared memory for the two of a social event, as <see cref="DilemmaEffect_SharedMemory"/> gives one, with a label
    /// that says when it was ({YEAR} and {SEASON} by the map's calendar) and what it was about (the event's own words:
    /// {THING}, {X_nameDef}).
    /// </summary>
    public class R5TMemoryEffect : DilemmaEffect
    {
        public SharedMemoryDef memory;
        [MustTranslate] public string label;
        public float weight = 1f;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (memory == null || ctx?.a == null || ctx.b == null || !Persona.On) return;
            sheet.Add(ImpactKind.Opinion, 1, "CC_Impact_SharedMemory".Translate(ctx.a.LabelShort, ctx.b.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // together: a social event between two who are near each other (SocialEvents.Hourly casts only those).
            if (memory == null || ctx?.a == null || ctx.b == null || !Persona.On) return;
            SharedMemories.Add(ctx.a, ctx.b, memory, Label(ctx), weight);
        }

        /// <summary>The label as the two will remember it.</summary>
        public string Label(DilemmaContext ctx)
        {
            var map = ctx.map ?? ctx.a?.MapHeld;
            int year = map != null ? GenLocalDate.Year(map) : GenDate.Year(Find.TickManager.TicksAbs, 0f);
            string season = "";
            if (map != null)
            {
                // A land of one long season names it as the others are named: "the summer of 5503".
                var s = GenLocalDate.Season(map);
                if (s == Season.PermanentSummer) s = Season.Summer;
                else if (s == Season.PermanentWinter) s = Season.Winter;
                season = s.Label();
            }
            return ctx.Format(Round5TogetherMath.Label(label.NullOrEmpty() ? memory.label : label, year, season));
        }
    }
}
