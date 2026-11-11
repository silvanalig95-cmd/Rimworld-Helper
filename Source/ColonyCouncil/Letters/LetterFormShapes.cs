using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's side of a letter's shape (Letters/LetterKindMath.cs; the player, 10 November 2026: "a lot of text
    /// variety ... stories or stuff that is told by the different pawns or factions ... poems, detailed stories of raider
    /// attacks, ideological ramblings and all kinds of different things"). Reads who writes beyond the form (their gifts:
    /// Artistic skill, an artist's meme, a people of makers; their faith's certainty and memes; their nature; who else is at
    /// home), what is true at their town for the pieces (the facts service's town, notables, wars, world events, region,
    /// deeds and ties), a real raid on it (World/WorldSituations, the ledger's history, their people's memory of the colony),
    /// the land around it (its wild animals and plants), and the setting "How long letters may get"; chooses the shape and
    /// makes its pieces. A shape whose pieces can't be made for this writer is the usual letter.
    /// </summary>
    public static partial class LetterForm
    {
        /// <summary>How long letters may get (the setting; Mixed by default).</summary>
        public static LetterLength Length => ColonyCouncilMod.Settings?.letterLength ?? LetterLength.Mixed;

        /// <summary>What a piece about their town is drawn from: the town, its land and notables, wars, world events, the region, what their people remember of the colony, the ties.</summary>
        private static readonly string[] PieceAsks =
        {
            FactsMath.Town, FactsMath.Land, FactsMath.Notables, FactsMath.Wars, FactsMath.Situations, FactsMath.Region, FactsMath.Deeds, FactsMath.Ties
        };

        /// <summary>
        /// World events whose record is a raid on a town of the people they are about, at its place: a raid on a town the
        /// colony was (its people's, its raiders the event's enemy), a war between neighbours that comes to the first one's
        /// town. A warlord's event sits at his own seat and the clans gather at theirs: neither is a raid on that town.
        /// </summary>
        private static readonly HashSet<string> RaidEvents = new HashSet<string> { "Rimfolk_WS_TownRaid", "Rimfolk_WS_NeighborsWar" };

        private const string BanditRoad = "Council_WS_BanditRoad";

        /// <summary>A record this old or older is no longer last season's news: the raid is told as an old tale.</summary>
        private const float RaidRecordDays = 240f;

        // ================= Their letter =================

        /// <summary>
        /// The shape of their letter: who they are beyond the form, what is true at their town, a raid on it; the shape
        /// chosen and its pieces made. A call and words passed on keep theirs. How well the two know each other by letter,
        /// and the thread it takes up, are read once the town's facts are in (Letters/LetterThreads.cs; <paramref name="to"/>
        /// the colonist written to).
        /// </summary>
        private static void ShapeTheirs(LetterSpec s, Notable n, Pawn to = null)
        {
            if (s.shape != LetterShape.Written || n == null) return;
            try
            {
                var f = n.faction;
                var pawn = n.pawn;
                var town = NotableBook.TownOf(n.town);
                var who = s.who = new LetterWho();
                s.length = Length;
                var facts = Facts.For(new FactScene { speaker = ColonyWriterPawn(), listener = pawn, colony = Home, people = f, place = town, asks = PieceAsks });
                Merge(s, facts);
                who.news = News(s.tags);
                Gifts(pawn, s, who, false);
                who.busy = n.role == NotableRole.Head || n.role == NotableRole.Trader;
                who.raid = RaidOf(f, town);
                who.boast = Boast(f, s);
                if (pawn != null)
                    foreach (var p in Household(pawn))
                        who.hands.Add(p);
                who.signers.Add(n.Name);
                foreach (var other in NotableBook.Instance?.notables.Where(x => x != null && x != n && x.town == n.town && x.Alive).Take(3) ?? Enumerable.Empty<Notable>())
                    who.signers.Add(other.Name);
                who.last = LetterBook.LastKind(n.id, false, null);
                who.owedAnswer = LetterBook.Owed(n.id);
                Land(s, town?.Tile ?? PlanetTile.Invalid);
                Faith(s, pawn);
                if (who.raid != null && !who.raid.raiders.NullOrEmpty() && !s.values.ContainsKey("RAIDERS")) s.values["RAIDERS"] = who.raid.raiders;
                string signed = LetterKindMath.Signed(who.signers, true, n.townName ?? f?.Name, Words);
                if (!signed.NullOrEmpty()) s.values["SIGNERS"] = signed;
                Diary(s, town?.Tile ?? PlanetTile.Invalid);
                // The correspondence: its stage, names and the thread it takes up, before the shape (old friends write long, a cooled one short).
                LetterThreads.Correspond(s, n, false, to, null);
                Make(s);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: their letter's shape", e);
                Unshape(s);
            }
        }

        // ================= The colony's own =================

        /// <summary>
        /// The shape of the colony's own letter, by its writer: a verse from a poet, a little preaching from a zealot, a bare
        /// note, other colonists' lines, something folded in, the cook's recipe, a petition signed by all, the colony's own
        /// raid told as the facts have it.
        /// </summary>
        private static void ShapeOurs(LetterSpec s, NeighbourLetter l, Notable n, Pawn writer, Map map)
        {
            if (s.shape != LetterShape.Written || writer == null) return;
            try
            {
                var who = s.who = new LetterWho();
                s.length = Length;
                Gifts(writer, s, who, true);
                var others = (map != null ? NeighbourLetters.Writers(map) : new List<Pawn>()).Where(p => p != writer && !p.DevelopmentalStage.Baby()).ToList();
                foreach (var p in others.InRandomOrder().Take(3)) who.hands.Add(p.LabelShort);
                who.signers.Add(writer.LabelShort);
                foreach (var p in others.Where(p => p.DevelopmentalStage.Adult()).Take(7)) who.signers.Add(p.LabelShort);
                bool more = others.Count(p => p.DevelopmentalStage.Adult()) > 7;
                string signed = LetterKindMath.Signed(who.signers, more, ColonyName(map), Words);
                if (!signed.NullOrEmpty()) s.values["SIGNERS"] = signed;
                who.cook = s.values.ContainsKey("COOK");
                // The colony's own raid, only as the facts have it.
                if (s.tags.Contains("colony:raid"))
                    who.raid = new RaidRecord
                    {
                        ours = true, raiders = s.values.TryGetValue("RAIDERS", out var r) ? r : null, days = 3f, end = RaidEnd.Unknown,
                        dead = s.tags.Contains("colony:raid:fallen") ? 1 : -1
                    };
                who.last = LetterBook.LastKind(n.id, true, writer.LabelShort);
                Land(s, map?.Tile ?? PlanetTile.Invalid);
                Faith(s, writer);
                // The correspondence: its stage, names and the thread the colony's letter takes up (Letters/LetterThreads.cs).
                LetterThreads.Correspond(s, n, true, n.pawn, writer.LabelShort);
                Make(s);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: the colony's letter's shape", e);
                Unshape(s);
            }
        }

        // ================= Choosing and making =================

        /// <summary>The shape chosen and its pieces made; the usual letter when they can't be.</summary>
        private static void Make(LetterSpec s)
        {
            s.kind = LetterKindMath.Choose(s, s.length, Rand.Value);
            if (s.kind == LetterKind.Usual || s.kind == LetterKind.Note) return;
            if (!LetterKindMath.Build(s, Words, Lines(s), () => Rand.Value)) Unshape(s);
        }

        private static void Unshape(LetterSpec s)
        {
            s.kind = LetterKind.Usual;
            s.piece.Clear();
            s.after.Clear();
            s.riddleAnswer = null;
        }

        /// <summary>A bare note is signed with the name alone (a council's, the Empire's, a mark and a hive's keep theirs).</summary>
        private static void SignNote(LetterSpec s, bool ours, bool titled)
        {
            if (s.kind != LetterKind.Note) return;
            s.sign = LetterFormMath.Signature(LetterFormMath.SignOf(ours, s.council, s.empire, titled, LetterFormMath.SpeechOf(s), s.values.ContainsKey("HELPER"), true), Words,
                s.values);
        }

        /// <summary>The pools as the shapes draw on them: a line that fits (filled, not one drawn lately nor remembered), and whether any fits.</summary>
        private static LetterLines Lines(LetterSpec s) => new LetterLines
        {
            pick = (part, tags) => PickFilled(part, tags, s),
            has = (part, tags) => HasLine(part, tags, s.values)
        };

        /// <summary>Whether a part's pool has a line that fits these tags with these values (nothing drawn).</summary>
        private static bool HasLine(LetterPart part, HashSet<string> tags, IReadOnlyDictionary<string, string> values)
        {
            var lines = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(LetterFormMath.PoolOf(part))?.lines?.plain;
            return lines != null && LetterFormMath.Any(lines, tags, values);
        }

        /// <summary>The facts for the pieces into the spec: every tag, and each value the letter doesn't have yet.</summary>
        private static void Merge(LetterSpec s, FactSet facts)
        {
            if (facts == null) return;
            s.tags.UnionWith(facts.tags);
            foreach (var kv in facts.values)
                if (!s.values.ContainsKey(kv.Key))
                    s.values[kv.Key] = kv.Value;
        }

        /// <summary>Real news of their town to tell: a world event there or on their people, its fortune moved, a loss, a new head, someone ill, a war, a new town near.</summary>
        private static bool News(HashSet<string> tags) =>
            tags.Contains("town:situation") || tags.Contains("situation") || tags.Contains("town:loss") || tags.Contains("town:newhead") || tags.Contains("town:ill")
            || tags.Contains("their:war") || tags.Contains("region:newtown") || tags.Contains("town:struggling") || tags.Contains("town:flourishing");

        /// <summary>
        /// A writer's gifts and nature: artistic (Artistic 8, or 5 with a burning passion; an artist's culture; a people of
        /// makers, singers or pilgrims), zealous (a faith held with certainty 0.75 whose memes weigh 6 or which is devout or
        /// preaching; a preacher's precept held at 0.5), a crank (zealous and short-tempered, suspicious or unschooled),
        /// talkative (warm 35, open 45 or merry 55), lonely (bleak at -25 and not cold).
        /// </summary>
        private static void Gifts(Pawn p, LetterSpec s, LetterWho who, bool ours)
        {
            if (p == null) return;
            var art = p.skills?.GetSkill(SkillDefOf.Artistic);
            int artLevel = art != null && !art.TotallyDisabled ? art.Level : 0;
            who.artistic = artLevel >= 8 || artLevel >= 5 && art.passion == Passion.Major || s.tags.Contains("c:craft") || s.tags.Contains("their:artisans")
                           || s.tags.Contains("their:listeners") || s.tags.Contains("their:pilgrims");
            var sides = Sides(p);
            if (sides != null)
            {
                float warmth = sides[(int)PersonaAxis.Warmth], open = sides[(int)PersonaAxis.Openness], humour = sides[(int)PersonaAxis.Humour];
                float outlook = sides[(int)PersonaAxis.Outlook], temper = sides[(int)PersonaAxis.Temper], trust = sides[(int)PersonaAxis.Trust];
                who.talkative = warmth >= 35f || open >= 45f || humour >= 55f;
                who.lonely = outlook <= -25f && warmth >= 0f;
                who.crank = temper >= 30f || trust <= -30f;
            }
            if (!ModsConfig.IdeologyActive || p.ideo?.Ideo == null) return;
            var ideo = p.ideo.Ideo;
            float certainty = Faiths.Certainty(p);
            int weight = ideo.memes?.Where(m => m != null).Sum(m => m.impact) ?? 0;
            bool devout = s.tags.Contains("c:pious") || s.tags.Contains("c:preach") || s.tags.Contains("p:onefaith");
            bool preacher = ideo.PreceptsListForReading?.Any(x => x?.def?.defName == "Proselytizing_Frequently") ?? false;
            who.zealous = certainty >= 0.75f && (weight >= 6 || devout) || preacher && certainty >= 0.5f;
            who.crank = who.zealous && (who.crank || s.schooling == Schooling.None);
        }

        /// <summary>Their faith's name and its god's, and its precepts as tags (LetterCultureMath.Precepts), for a sermon.</summary>
        private static void Faith(LetterSpec s, Pawn p)
        {
            if (p == null || !ModsConfig.IdeologyActive || p.ideo?.Ideo == null) return;
            try
            {
                var ideo = p.ideo.Ideo;
                if (!ideo.name.NullOrEmpty()) s.values["IDEO"] = ideo.name;
                string deity = ideo.KeyDeityName;
                if (!deity.NullOrEmpty()) s.values["DEITY"] = deity;
                LetterCultureMath.ReadPrecepts(ideo.PreceptsListForReading?.Where(x => x?.def != null).Select(x => x.def.defName), s.tags);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a writer's faith", e);
            }
        }

        /// <summary>The land round the writer: one of its wild animals ({ANIMAL}; {ANANIMAL} with "a" or "an"; {ANIMALS}), one of its wild plants not a tree ({PLANT}), one of its trees ({TREE}).</summary>
        private static void Land(LetterSpec s, PlanetTile tile)
        {
            if (!tile.Valid) return;
            try
            {
                var biome = Find.WorldGrid[tile]?.PrimaryBiome;
                if (biome == null) return;
                var animal = BiomeAnimals.Of(biome).Where(k => k?.RaceProps != null && k.RaceProps.Animal && !k.label.NullOrEmpty()).InRandomOrder().FirstOrDefault();
                if (animal != null)
                {
                    s.values["ANIMAL"] = animal.label;
                    s.values["ANANIMAL"] = Find.ActiveLanguageWorker.WithIndefiniteArticle(animal.label);
                    s.values["ANIMALS"] = Find.ActiveLanguageWorker.Pluralize(animal.label);
                }
                var plants = biome.AllWildPlants?.Where(d => d?.plant != null && !d.label.NullOrEmpty() && biome.CommonalityOfPlant(d) > 0f).ToList() ?? new List<ThingDef>();
                var plant = plants.Where(d => !d.plant.IsTree && d.plant.harvestedThingDef != null || !d.plant.IsTree && d.plant.purpose == PlantPurpose.Beauty).InRandomOrder().FirstOrDefault()
                            ?? plants.Where(d => !d.plant.IsTree).InRandomOrder().FirstOrDefault();
                if (plant != null) s.values["PLANT"] = plant.label;
                var tree = plants.Where(d => d.plant.IsTree && d.plant.treeCategory != TreeCategory.Super).InRandomOrder().FirstOrDefault();
                if (tree != null) s.values["TREE"] = tree.label;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: the land's animals and plants", e);
            }
        }

        /// <summary>The days a diary page covers ({DAY1}..{DAY3}), in the writer's way of dating.</summary>
        private static void Diary(LetterSpec s, PlanetTile tile)
        {
            try
            {
                long abs = Find.TickManager.TicksAbs;
                float lon = tile.Valid ? Find.WorldGrid.LongLatOf(tile).x : 0f;
                var q = GenDate.Quadrum(abs, lon);
                var days = LetterKindMath.DiaryDays(LetterFormMath.DateStyleOf(s.age, s.empire), Words, GenDate.DayOfQuadrum(abs, lon) + 1, q.Label(), (int)q, GenDate.Year(abs, lon), 3);
                for (int i = 0; i < days.Count; i++) s.values["DAY" + (i + 1)] = days[i];
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a diary's days", e);
            }
        }

        /// <summary>Who else is at home with them, by name: their spouse or lover, their children, their parents, while alive.</summary>
        private static IEnumerable<string> Household(Pawn p)
        {
            var seen = new HashSet<Pawn>();
            var rel = p.relations;
            if (rel == null) yield break;
            foreach (var r in rel.DirectRelations)
            {
                var o = r?.otherPawn;
                if (o == null || o.Dead || o.Discarded || o.Faction != p.Faction) continue;
                if (r.def != PawnRelationDefOf.Spouse && r.def != PawnRelationDefOf.Lover && r.def != PawnRelationDefOf.Fiance && r.def != PawnRelationDefOf.Parent) continue;
                if (seen.Add(o)) yield return o.LabelShort;
                if (seen.Count >= 3) yield break;
            }
            foreach (var c in rel.Children)
            {
                if (c == null || c.Dead || c.Discarded || c.Faction != p.Faction || !seen.Add(c)) continue;
                yield return c.LabelShort;
                if (seen.Count >= 3) yield break;
            }
        }

        /// <summary>
        /// A raiding people that raided the colony within a year (their memory's deed), whose letter is a boast of it: an
        /// outlaw people's or a raider's culture, at war or cool.
        /// </summary>
        private static bool Boast(Faction f, LetterSpec s)
        {
            if (f == null || !(s.war || s.tone >= AnswerTone.Cool)) return false;
            bool raiders = f.def != null && LetterCultureMath.Outlaws.Contains(f.def.defName) || s.tags.Contains("c:raider");
            if (!raiders) return false;
            int since = Find.TickManager.TicksGame - GenDate.TicksPerYear;
            return FactionMemory.Instance?.Of(f).Any(d => d != null && d.kind == "raided" && d.tick >= since) ?? false;
        }

        /// <summary>
        /// A real raid on their town, newest first: a world event at it now (raiders about) or its record (how it ended,
        /// their dead as the record has them), a battle at it in the ledger's history, the colony's own attack on their town,
        /// bandits on their people's road; the colony's fighters on their walls remembered. Null when none within the year.
        /// </summary>
        private static RaidRecord RaidOf(Faction f, Settlement town)
        {
            if (f == null) return null;
            int now = Find.TickManager.TicksGame;
            var place = town != null ? Refs.Of(town) : Ref.None;
            var ws = WorldSituations.Instance;
            RaidRecord rec = null;
            if (ws != null)
            {
                foreach (var s in ws.Running)
                {
                    if (s?.def == null) continue;
                    bool atWalls = RaidEvents.Contains(s.def.defName) && !place.IsNone && s.place == place && s.ctx?.faction == f;
                    bool road = s.def.defName == BanditRoad && s.ctx?.faction == f;
                    if (!atWalls && !road) continue;
                    rec = new RaidRecord { raiders = s.ctx?.enemy?.Name ?? (road ? null : s.ctx?.name), days = s.DaysIn, end = RaidEnd.Ongoing, road = road && !atWalls };
                    break;
                }
                if (rec == null)
                    for (int i = ws.history.Count - 1; i >= 0; i--)
                    {
                        var r = ws.history[i];
                        if (r == null || r.defName == null) continue;
                        float days = (now - r.endTick) / (float)GenDate.TicksPerDay;
                        if (days > RaidRecordDays) continue;
                        bool atWalls = RaidEvents.Contains(r.defName) && !place.IsNone && r.place == place && r.faction == f;
                        bool road = r.defName == BanditRoad && r.faction == f;
                        if (!atWalls && !road) continue;
                        rec = new RaidRecord { days = days, road = road && !atWalls, end = EndOf(r) };
                        rec.dead = r.Has("diedMany") ? 2 : r.Has("died") ? 1 : r.defName == "Rimfolk_WS_TownRaid" ? DeadOf(rec.end) : -1;
                        break;
                    }
            }
            if (rec == null && !place.IsNone)
            {
                int since = now - (int)(RaidRecordDays * GenDate.TicksPerDay);
                var battle = History.At(place, new[] { WorldMath.Battle, WorldMath.Razed }, since, 1).FirstOrDefault();
                if (battle != null) rec = new RaidRecord { days = (now - battle.tick) / (float)GenDate.TicksPerDay, end = RaidEnd.Unknown };
            }
            var mem = FactionMemory.Instance;
            if (mem != null)
            {
                int since = now - (int)(RaidRecordDays * GenDate.TicksPerDay);
                var mine = mem.Of(f).Where(d => d != null && d.tick >= since).ToList();
                var attacked = mine.Where(d => d.kind == "townAttacked").OrderByDescending(d => d.tick).FirstOrDefault();
                if (rec == null && attacked != null)
                    rec = new RaidRecord { byColony = true, raiders = ColonyName(Home), days = (now - attacked.tick) / (float)GenDate.TicksPerDay, end = RaidEnd.Unknown };
                if (rec != null && !rec.byColony && mine.Any(d => d.kind == "townDefended")) rec.helped = true;
            }
            return rec;
        }

        /// <summary>How a raid's record ended, from its world event's ending.</summary>
        private static RaidEnd EndOf(SituationRecord r)
        {
            switch (r.ending)
            {
                case "defended":
                case "held":
                case "cleared":
                case "neighbors":
                    return RaidEnd.Held;
                case "hurt":
                    return RaidEnd.Hurt;
                case "fell":
                case "sacked":
                    return RaidEnd.Sacked;
                // A war between neighbours, told by the first of them (the one whose town it came to).
                case "factionWon":
                    return RaidEnd.Held;
                case "enemyWon":
                    return RaidEnd.Hurt;
                default:
                    return RaidEnd.Unknown;
            }
        }

        /// <summary>A raid on a town the colony was: its other dead by how it ended (Colonies/TownLifeMath.OthersLost: none held, one hurt, two sacked).</summary>
        private static int DeadOf(RaidEnd e) => e == RaidEnd.Held ? 0 : e == RaidEnd.Hurt ? 1 : e == RaidEnd.Sacked ? 2 : -1;

        /// <summary>The colonist the letter's window would choose to write, as a pawn.</summary>
        private static Pawn ColonyWriterPawn() => NeighbourLetters.DefaultWriter(Home);
    }
}
