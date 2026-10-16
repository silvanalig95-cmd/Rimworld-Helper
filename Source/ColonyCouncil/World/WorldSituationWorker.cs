using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a world situation does beyond its data. The base class handles everything a def can say: it picks the
    /// people from the def's needs, names the situation, picks the text, weighs the modifiers and picks the ending when
    /// time runs out. Subclasses add what only code can do (a warlord, a town founded, a leader replaced).
    /// </summary>
    public class WorldSituationWorker
    {
        public WorldSituationDef def;

        protected static CouncilSettings S => ColonyCouncilMod.Settings;

        protected static int Now => Find.TickManager.TicksGame;

        // ---------------- Can it start? ----------------

        /// <summary>Settings aside (the tracker checks those): the season, the era, mods, people.</summary>
        public virtual bool CanStart(Map map)
        {
            if (map == null) return false;
            var tech = Avail.EffectiveTech;
            if (tech < def.minTech || tech > def.maxTech) return false;
            if (!def.seasons.NullOrEmpty() && !def.seasons.Contains(GenLocalDate.Season(map))) return false;
            if (!def.mods.NullOrEmpty() && !def.mods.Any(m => ModsConfig.IsActive(m) || People.ModActive(m))) return false;
            if (!def.notWithMods.NullOrEmpty() && def.notWithMods.Any(m => ModsConfig.IsActive(m) || People.ModActive(m))) return false;
            if (def.battle && !IncidentWorker_Happening.FightsPossible) return false;
            if ((!def.colonyIs.NullOrEmpty() || !def.colonyIsNot.NullOrEmpty()) && !(CouncilGameComponent.Instance?.character.Fits(def.colonyIs, def.colonyIsNot) ?? false)) return false;
            var colonists = IncidentWorker_Happening.Colonists(map);
            if (colonists.Count < Mathf.Max(1, def.minColonists)) return false;
            if (def.colonist != null && !colonists.Any(p => def.colonist.Allows(p, null))) return false;
            if (def.faction != null && !Candidates(def.faction, null, map).Any()) return false;
            if (!def.after.NullOrEmpty() && AfterRecord(map) == null) return false;
            return CanHappen(map);
        }

        // ---------------- Follow-ups ----------------

        /// <summary>
        /// For a follow-up (<see cref="WorldSituationDef.after"/>): the world event it follows, newest first, ended the
        /// right way at the right time and not followed up by this one yet. Its people must still fit, if it has any.
        /// </summary>
        public SituationRecord AfterRecord(Map map)
        {
            var history = WorldSituations.Instance?.history;
            if (history == null || def.after.NullOrEmpty()) return null;
            int now = Now;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var r = history[i];
                if (r == null || r.defName == null || !def.after.Contains(r.defName) || r.Followed(def.defName)) continue;
                if (r.ending == null || r.ending == WorldSituations.Quietly) continue;
                if (!def.afterEndings.NullOrEmpty() && !def.afterEndings.Contains(r.ending)) continue;
                float days = (now - r.endTick) / (float)GenDate.TicksPerDay;
                if (days < def.afterDays.min || days > def.afterDays.max) continue;
                if (def.faction != null && !def.faction.optional && (r.faction == null || !Candidates(def.faction, null, map).Contains(r.faction))) continue;
                return r;
            }
            return null;
        }

        /// <summary>A follow-up takes over from what came before: its people (when they fit), {TALE}, and tags to read it by.</summary>
        protected void TakeOver(WorldSituation s, Map map)
        {
            if (def.after.NullOrEmpty()) return;
            var r = AfterRecord(map);
            if (r == null) return;
            r.Follow(def.defName);
            s.ctx.tale = r.Called;
            s.Set("after:" + r.defName);
            if (!r.ending.NullOrEmpty()) s.Set("afterEnd:" + r.ending);
            if (r.helped) s.Set("afterHelped");
            if (def.faction != null && r.faction != null && Candidates(def.faction, null, map).Contains(r.faction))
            {
                s.ctx.faction = r.faction;
                s.ctx.placeName = NearestTown(r.faction, map)?.Name ?? r.faction.Name;
            }
        }

        /// <summary>Anything else this one needs (a river, a free tile, a leader).</summary>
        protected virtual bool CanHappen(Map map) => true;

        // ---------------- Who ----------------

        /// <summary>Peoples that fit a need (and, for the second, the first people).</summary>
        public static IEnumerable<Faction> Candidates(SituationFactionNeed need, Faction first, Map map)
        {
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f.IsPlayer || f.Hidden || f.defeated || f.temporary || !f.def.humanlikeFaction || f == first) continue;
                if (WorldSituations.Instance?.Busy(f) ?? false) continue;
                bool hostile = f.HostileTo(Faction.OfPlayer);
                if (need.hostile && !hostile) continue;
                if (need.friendly && hostile) continue;
                if (f.HasGoodwill && (f.PlayerGoodwill < need.minGoodwill || f.PlayerGoodwill > need.maxGoodwill)) continue;
                if (need.canRaid && !Raids.CanRaid(f)) continue;
                if (need.leader && !LeaderReplaceable(f)) continue;
                if (need.ideo && (!ModsConfig.IdeologyActive || f.ideos?.PrimaryIdeo == null)) continue;
                if (first != null && need.hostileToFirst && !f.HostileTo(first)) continue;
                if (first != null && need.friendlyToFirst && f.HostileTo(first)) continue;
                if (need.settlement || need.minSettlements > 1 || need.maxDistance < 9000f)
                {
                    int towns = TownsOf(f, map).Count();
                    if (towns < Mathf.Max(need.settlement ? 1 : 0, need.minSettlements)) continue;
                    if (need.maxDistance < 9000f && Distance(map, NearestTown(f, map)) > need.maxDistance) continue;
                }
                yield return f;
            }
        }

        /// <summary>One of the candidates: of the colony's own era by preference, and nearer ones more often.</summary>
        protected Faction PickFaction(SituationFactionNeed need, Faction first, Map map)
        {
            if (need == null) return null;
            var tech = Avail.EffectiveTech;
            return Candidates(need, first, map).RandomElementByWeightWithFallback(f =>
                Arrivals.TechWeight(f, tech) * Mathf.Lerp(1.5f, 0.5f, Mathf.Clamp01(Distance(map, NearestTown(f, map)) / 40f)));
        }

        /// <summary>
        /// A leader who can be replaced without breaking the game: not an empire's, not in our hands, not wanted by a
        /// quest, nobody's kin here. Not of a people whose next leader the game picks from pawns on a map (that could be
        /// a visitor at the colony, or one of its prisoners).
        /// </summary>
        public static bool LeaderReplaceable(Faction f)
        {
            var l = f?.leader;
            if (l == null || l.Dead || l.Destroyed || f.def.HasRoyalTitles || f.def.permanentEnemy || f.def.generateNewLeaderFromMapMembersOnly) return false;
            if (l.Spawned || l.IsPrisoner || l.GetCaravan() != null || People.Kidnapped(l) || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(l)) return false;
            if (!Find.WorldPawns.Contains(l)) return false;
            if (l.relations != null && l.relations.DirectRelations.Any(r => r.otherPawn != null && r.otherPawn.Faction == Faction.OfPlayer)) return false;
            return true;
        }

        public static IEnumerable<Settlement> TownsOf(Faction f, Map map)
        {
            var layer = map?.Tile.Layer;
            return Find.WorldObjects.Settlements.Where(s => s.Faction == f && !s.Destroyed && (layer == null || s.Tile.Layer == layer));
        }

        public static Settlement NearestTown(Faction f, Map map)
        {
            if (f == null || map == null) return null;
            Settlement best = null;
            float bestDist = float.MaxValue;
            foreach (var s in TownsOf(f, map))
            {
                if (!s.HasName) continue;
                float d = Distance(map, s);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = s;
                }
            }
            return best;
        }

        public static float Distance(Map map, Settlement s) => map == null || s == null ? 9999f : LivingEvents.Distance(map.Tile, s.Tile);

        /// <summary>"to the north", "to the southeast": which way a place lies from the colony.</summary>
        public static string Direction(Map map, PlanetTile to)
        {
            if (map == null || !to.Valid) return "";
            try
            {
                if (to.Layer != map.Tile.Layer) return "";
                var d = Find.WorldGrid.GetDirection8WayFromTo(map.Tile, to);
                switch (d)
                {
                    case Direction8Way.North: return "CC_WS_Dir_N".Translate();
                    case Direction8Way.NorthEast: return "CC_WS_Dir_NE".Translate();
                    case Direction8Way.East: return "CC_WS_Dir_E".Translate();
                    case Direction8Way.SouthEast: return "CC_WS_Dir_SE".Translate();
                    case Direction8Way.South: return "CC_WS_Dir_S".Translate();
                    case Direction8Way.SouthWest: return "CC_WS_Dir_SW".Translate();
                    case Direction8Way.West: return "CC_WS_Dir_W".Translate();
                    default: return "CC_WS_Dir_NW".Translate();
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Who and what: the people, the town, the colonist, the name. False: it can't happen after all (and anything
        /// made is let go again by <see cref="Abort"/>).
        /// </summary>
        public virtual bool Cast(WorldSituation s, Map map)
        {
            var ctx = s.ctx;
            ctx.map = map;
            if (def.faction != null)
            {
                ctx.faction = PickFaction(def.faction, null, map);
                if (ctx.faction == null) return false;
                ctx.placeName = NearestTown(ctx.faction, map)?.Name ?? ctx.faction.Name;
            }
            // A follow-up: the people of what came before, and what to call it ({TALE}).
            TakeOver(s, map);
            if (def.second != null)
            {
                ctx.enemy = PickFaction(def.second, ctx.faction, map);
                if (ctx.enemy == null && !def.second.optional) return false;
            }
            if (def.colonist != null)
            {
                ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => def.colonist.Allows(p, null))
                    .OrderByDescending(p => def.colonist.bestAt != null ? p.skills?.GetSkill(def.colonist.bestAt)?.Level ?? 0 : Rand.Value).FirstOrDefault();
                if (ctx.a == null) return false;
            }
            else ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (!def.names.NullOrEmpty()) ctx.name = Pick(def.names, s);
            return true;
        }

        /// <summary>It's going ahead: spawn, found, replace (after the start effects, before the letter).</summary>
        public virtual void Begin(WorldSituation s)
        {
        }

        /// <summary>It couldn't start after all: let go of anyone made for it.</summary>
        public virtual void Abort(WorldSituation s)
        {
            foreach (var p in s.held.ToList()) Arrivals.Discard(p);
            s.held.Clear();
        }

        // ---------------- While it lasts ----------------

        /// <summary>Every hour. Return an ending key to end it now (null: carry on).</summary>
        public virtual string Hourly(WorldSituation s) => StillPossible(s) ? null : GoneEnding(s);

        /// <summary>Once a day. Return an ending key to end it now.</summary>
        public virtual string Daily(WorldSituation s) => null;

        /// <summary>The people it's about are gone or at war with the colony now: it can't go on as it was.</summary>
        protected virtual bool StillPossible(WorldSituation s)
        {
            var f = s.ctx.faction;
            if (def.faction == null) return true;
            // Gone from the save altogether (their mod was removed): nothing left to tell about them.
            if (f == null) return false;
            if (f.defeated) return false;
            if (def.faction.friendly && f.HostileTo(Faction.OfPlayer)) return false;
            return true;
        }

        /// <summary>How it ends when its people are gone (an ending keyed "gone", or the quiet end; quietly too when there's no name left to tell it with).</summary>
        protected virtual string GoneEnding(WorldSituation s) => s.ctx.faction != null && def.Ending("gone") != null ? "gone" : WorldSituations.Quietly;

        /// <summary>The ending to tell when time runs out: the first that fits, in order.</summary>
        public virtual SituationEnding TimeoutEnding(WorldSituation s)
        {
            foreach (var e in def.endings)
            {
                if (!e.timeout || !Fits(s, e)) continue;
                if (e.chance < 1f && !Rand.Chance(e.chance)) continue;
                return e;
            }
            return def.endings.LastOrDefault(e => e.timeout);
        }

        public static bool Fits(WorldSituation s, SituationEnding e) =>
            s.HasAll(e.requires) && s.HasNone(e.forbids) && s.help >= e.minHelp && s.help <= e.maxHelp;

        /// <summary>Whether a beat can come now (flags, fights, people). False: it's skipped (or retried, with <paramref name="retry"/>).</summary>
        public virtual bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            retry = false;
            if (!s.HasAll(beat.requires) || !s.HasNone(beat.forbids)) return false;
            if (beat.needsBattles)
            {
                if (!IncidentWorker_Happening.FightsPossible) return false;
                if (!IncidentWorker_Happening.BattlesAllowed(map))
                {
                    retry = true;
                    return false;
                }
            }
            return true;
        }

        /// <summary>Who a beat is put to ({A}), when it names someone. False: nobody fits, so it can't come.</summary>
        public virtual bool CastBeat(WorldSituation s, SituationBeat beat, SituationContext ctx, Map map)
        {
            if (beat.colonist == null && beat.skill == null)
            {
                if (ctx.a == null || ctx.a.Dead || !ctx.a.IsColonist || !ctx.a.Spawned)
                    ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
                return true;
            }
            var pool = IncidentWorker_Happening.Colonists(map).Where(p => beat.colonist == null ? !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState : beat.colonist.Allows(p, null));
            if (beat.skill != null)
            {
                var best = pool.Where(p => p.skills?.GetSkill(beat.skill) is SkillRecord r && !r.TotallyDisabled && r.Level >= beat.minLevel)
                    .OrderByDescending(p => p.skills.GetSkill(beat.skill).Level).FirstOrDefault();
                ctx.a = best;
            }
            else ctx.a = pool.RandomElementWithFallback();
            return ctx.a != null;
        }

        /// <summary>
        /// A beat is about to come, its people cast and before its text is written: set what its text and answers need
        /// ({COUNT}, an ally to ask, the odds of a battle). False: it doesn't come after all.
        /// </summary>
        public virtual bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx) => true;

        /// <summary>A beat just came (its letter is out): anything code must do.</summary>
        public virtual void BeatHappened(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
        }

        /// <summary>Whether an answer is offered at all (an ally to ask, a captive to hand over).</summary>
        public virtual bool Offered(WorldSituation s, SituationBeat beat, int option)
        {
            var o = beat.options[option];
            return s.HasAll(o.showIf) && s.HasNone(o.hideIf);
        }

        /// <summary>Why an answer can't be picked now (beyond its effects), or null.</summary>
        public virtual string Unavailable(WorldSituation s, SituationContext ctx, SituationBeat beat, int option) => null;

        /// <summary>The chance an answer's risk comes true: A's skill (the option's risk skill) lowers it.</summary>
        public virtual float RiskChance(WorldSituation s, SituationContext ctx, SituationOption o)
        {
            float chance = o.riskChance;
            var r = o.riskSkill != null ? ctx.a?.skills?.GetSkill(o.riskSkill) : null;
            if (r != null && !r.TotallyDisabled) chance -= r.Level * 0.03f;
            return Mathf.Clamp(chance, 0.05f, 0.95f);
        }

        /// <summary>After an answer's effects: anything code must do. Returns the outcome to tell (may change it).</summary>
        public virtual string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome) => outcome;

        /// <summary>It's over (the ending's effects and letter are done): anything code must do.</summary>
        public virtual void Ended(WorldSituation s, SituationEnding ending)
        {
        }

        // ---------------- What it changes ----------------

        /// <summary>A factor on an event's storyteller weight while this runs (Rimfolk events have <paramref name="ext"/>).</summary>
        public virtual float Factor(WorldSituation s, IncidentDef incident, HappeningExtension ext)
        {
            float f = 1f;
            foreach (var m in def.modifiers)
                if (m.Matches(incident, ext) && m.Applies(s)) f *= m.factor;
            return f;
        }

        /// <summary>A factor on the strength of a people's raids while this runs.</summary>
        public virtual float RaidPointsFactor(WorldSituation s, Faction f) => 1f;

        /// <summary>A people whose raids come more often while this runs (or null).</summary>
        public virtual Faction RaidFaction(WorldSituation s) => null;

        // ---------------- Text ----------------

        /// <summary>The tags its lines are picked by: era, season, flags, the colony's part, and the worker's own.</summary>
        public HashSet<string> Tags(WorldSituation s)
        {
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            var map = s.ctx.map ?? HappeningTracker.HomeMap();
            if (map != null)
            {
                switch (GenLocalDate.Season(map))
                {
                    case Season.Spring: tags.Add("spring"); break;
                    case Season.Summer:
                    case Season.PermanentSummer: tags.Add("summer"); break;
                    case Season.Fall: tags.Add("fall"); break;
                    case Season.Winter:
                    case Season.PermanentWinter: tags.Add("winter"); break;
                }
            }
            foreach (var f in s.flags) tags.Add(f);
            tags.Add(s.help > 0 ? "helped" : "ignored");
            if (s.ctx.faction != null && (WorldSituations.Instance?.Helped(s.ctx.faction) ?? false)) tags.Add("remembered");
            if (ModsConfig.IdeologyActive) tags.Add("ideology");
            if (s.ctx.enemy == null) tags.Add("noenemy");
            // The peoples' own era, for lines about how they live (a tribe has no radio, whatever the colony has).
            if (s.ctx.faction != null) tags.Add(s.ctx.faction.def.techLevel <= TechLevel.Medieval ? "theirLowtech" : "theirHightech");
            if (s.ctx.enemy != null) tags.Add(s.ctx.enemy.def.techLevel <= TechLevel.Medieval ? "enemyLowtech" : "enemyHightech");
            if (s.ctx.a != null) tags.Add(s.ctx.a.gender == Gender.Female ? "aFemale" : "aMale");
            // What the people remember of the colony and of single colonists: their:grateful, their:grudge, their:friend...
            Interplay.AddMemoryTags(tags, s.ctx.faction);
            KinMemory.AddTags(tags, s.ctx.faction, s.ctx.a);
            AddTags(s, tags);
            return tags;
        }

        protected virtual void AddTags(WorldSituation s, HashSet<string> tags)
        {
        }

        /// <summary>A line that fits (tags removed), or null.</summary>
        public string Pick(List<string> lines, WorldSituation s)
        {
            if (lines.NullOrEmpty()) return null;
            return Flavor.Pick(lines, Tags(s)) ?? Flavor.PickText(lines);
        }

        /// <summary>A line that fits, with its placeholders filled in (and every sentence starting with a capital).</summary>
        public string Line(List<string> lines, WorldSituation s, SituationContext ctx = null)
        {
            string line = Pick(lines, s);
            return line.NullOrEmpty() ? null : Sentences((ctx ?? s.ctx).Format(line));
        }

        /// <summary>
        /// Capitals at the start of every sentence and paragraph, so a name that comes in lowercase ("the Ashen
        /// Banner", "the gray cough", a leader's title) can open a sentence.
        /// </summary>
        public static string Sentences(string text)
        {
            if (text.NullOrEmpty()) return text;
            var c = text.ToCharArray();
            bool start = true;
            for (int i = 0; i < c.Length; i++)
            {
                char ch = c[i];
                if (start && char.IsLetter(ch))
                {
                    c[i] = char.ToUpperInvariant(ch);
                    start = false;
                }
                else if (ch == '.' || ch == '!' || ch == '?') start = i + 1 < c.Length && char.IsWhiteSpace(c[i + 1]);
                else if (ch == '\n') start = true;
                else if (start && !char.IsWhiteSpace(ch) && ch != '"' && ch != '(' && ch != '\'') start = false;
            }
            return new string(c);
        }

        /// <summary>The line on the World page and under the start letter.</summary>
        public virtual string Summary(WorldSituation s) => Line(def.summaries, s);

        /// <summary>More to say under the start letter (who's hunting the warlord, the new leader's nature): the "extra" lines of its SituationText.</summary>
        public virtual string StartExtra(WorldSituation s) => Text("extra", s);

        /// <summary>Where the start letter points (a town on the world map, a visitor).</summary>
        public virtual LookTargets StartLook(WorldSituation s)
        {
            var town = s.ctx.faction != null ? NearestTown(s.ctx.faction, s.ctx.map) : null;
            return town != null ? new LookTargets(town) : LookTargets.Invalid;
        }

        // ---------------- Helpers ----------------

        /// <summary>A line from the def's <see cref="SituationText"/> by key, with its placeholders filled in (null if none).</summary>
        public string Text(string key, WorldSituation s, SituationContext ctx = null) => Line(def.GetModExtension<SituationText>()?.Get(key), s, ctx);

        public bool HasText(string key) => !(def.GetModExtension<SituationText>()?.Get(key)).NullOrEmpty();

        /// <summary>Silver scaled by the colony's wealth, rounded to tens.</summary>
        public static int Silver(Map map, IntRange range)
        {
            float v = range.RandomInRange * LivingEvents.WealthFactor(map);
            return Mathf.Max(10, Mathf.RoundToInt(v / 10f) * 10);
        }

        /// <summary>
        /// Goodwill that never turns a people hostile (or an ally into a stranger beyond what was asked): a loss stops
        /// at -60, so only the colony's own choices can start a war.
        /// </summary>
        public static void Goodwill(Faction f, int amount, bool message = true)
        {
            if (f == null || f.IsPlayer || f.defeated || !f.HasGoodwill || amount == 0) return;
            int change = amount > 0 ? amount : Mathf.Min(0, Mathf.Max(amount, -60 - f.PlayerGoodwill));
            if (change == 0) return;
            try
            {
                f.TryAffectGoodwillWith(Faction.OfPlayer, change, message, false);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event goodwill", e);
            }
        }

        /// <summary>Someone of a people, made for a world event and kept in the world while it needs them.</summary>
        protected static Pawn MakePerson(WorldSituation s, Faction f, PawnKindDef kind = null, FloatRange? age = null, bool fighter = false, Gender? gender = null)
        {
            kind = kind ?? Arrivals.PersonKind(f);
            if (kind == null) return null;
            Pawn p;
            try
            {
                var req = new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false, mustBeCapableOfViolence: fighter, colonistRelationChanceFactor: 0f,
                    biologicalAgeRange: age ?? new FloatRange(25f, 55f), fixedGender: gender,
                    dontGiveWeapon: !fighter && Avail.IsPreIndustrial && f != null && f.def.techLevel > TechLevel.Medieval);
                p = PawnGenerator.GeneratePawn(req);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event person", e);
                return null;
            }
            if (p == null) return null;
            s.Hold(p);
            return p;
        }

        /// <summary>
        /// Out in the world and nobody's in particular: a world pawn not on a map, not in a caravan, a pod or a trader's
        /// hold, not a prisoner or one of ours, not kidnapped and not wanted by a quest. Only such a person may die or
        /// vanish off-screen.
        /// </summary>
        public static bool FreeInWorld(Pawn p) =>
            p != null && !p.Dead && !p.Destroyed && !p.Spawned && p.holdingOwner == null && Find.WorldPawns.Contains(p) && p.GetCaravan() == null
            && !p.IsPrisoner && p.Faction != Faction.OfPlayer && !People.Kidnapped(p) && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p);

        /// <summary>A world pawn dies out in the world (the old chief, the warlord brought down): quietly, without the game's own letters.</summary>
        public static bool DieOffscreen(Pawn p)
        {
            if (!FreeInWorld(p)) return false;
            try
            {
                p.Kill(null);
                return p.Dead;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event death", e);
                return false;
            }
        }

        /// <summary>
        /// Someone the colony will never see again (taken away, gone over the mountains): off the map, or out of the
        /// world if they're free out there. Someone in a caravan, a pod, a trader's hold or a colonist's arms, a people's
        /// leader or someone a quest needs stays where they are.
        /// </summary>
        public static void Vanish(Pawn p)
        {
            if (p == null || p.Dead || p.Discarded || PawnUtility.IsFactionLeader(p) || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p)) return;
            if (!p.Spawned && (p.holdingOwner != null || Find.WorldPawns.Contains(p) && !FreeInWorld(p))) return;
            try
            {
                if (p.Spawned) p.DeSpawn();
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(p);
                else Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event vanish", e);
            }
        }

        /// <summary>The colonist who dealt a dead pawn's killing blow, from the combat log (or null).</summary>
        public static Pawn Slayer(Pawn dead)
        {
            var hediffs = dead?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            foreach (var h in hediffs.Where(h => h.combatLogEntry != null).OrderBy(h => h.ageTicks).Take(5))
            {
                var k = h.combatLogEntry.Target?.GetConcerns().FirstOrDefault() as Pawn;
                if (k != null && k != dead && k.Faction == Faction.OfPlayer && k.RaceProps.Humanlike) return k;
            }
            return null;
        }
    }
}
