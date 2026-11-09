using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Better armour and clothes for each colonist in the game (docs/design/apparel.md, step 3, point 6): a home map's
    /// colonists (children and slaves too, not babies or quest guests), what they wear and carry, the apparel lying in
    /// storage they may take (a stockpile or shelf, not forbidden, not burning; an outfit stand's), the colony's raids
    /// (WeaponPickBook's record), the season ahead on their map (now and the next two twelfths, the game's own "Need warm
    /// clothes" look), their work, their title's, role's and faith's apparel rules and their outfit. Each colonist's own
    /// rating of each piece (ApparelLive.ForColonist, kept an hour, worked out ahead a tenth of the colony at a time by
    /// <see cref="ApparelPickBook"/>); the colony's choice made by <see cref="ApparelPickMath.Assign"/> at most once an hour
    /// (sooner when what they wear or what lies in storage changes); the words of each suggestion; and what a button does:
    /// the game's own Wear job walked out on the map (what it takes the place of dropped there, as the game does), a piece
    /// handed over by another, two colonists trading armour, a kind allowed in an outfit. Nothing is worked out per frame.
    /// </summary>
    public static class ApparelPick
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>"Apparel suggestions": the gear tab's rows, who should wear a piece on its card, the Defenses page's view.</summary>
        public static bool On => ApparelLive.On && (S?.apparelSuggestions ?? true);

        /// <summary>The kinds from storage the choice weighs at most: the best of each role by its rating, and the warmest and coolest for the weather.</summary>
        public const int TopPerRole = 8, TopWeather = 8;

        public sealed class Picks
        {
            public Map map;
            public int tick = -1, signature;
            public float made, sigAt;
            public WearInput input = new WearInput();
            public List<WearSuggestion> list = new List<WearSuggestion>();
            public readonly Dictionary<string, Thing> things = new Dictionary<string, Thing>();
            public readonly Dictionary<string, Pawn> pawns = new Dictionary<string, Pawn>();
            public readonly Dictionary<string, ApparelReport> reports = new Dictionary<string, ApparelReport>();
            public readonly Dictionary<string, string> places = new Dictionary<string, string>();
            /// <summary>The kinds the choice rates (a piece of each), for working colonists' ratings out ahead.</summary>
            public readonly List<(ApparelReport report, Thing thing)> kinds = new List<(ApparelReport, Thing)>();
            /// <summary>Pieces lying in storage the colony could take, and those lying there forbidden.</summary>
            public int stored, forbidden;
            /// <summary>The season ahead on the map: outside now, its coldest night and hottest day.</summary>
            public float now, coldest, hottest;

            public IEnumerable<WearSuggestion> Of(Pawn p) => p == null ? Enumerable.Empty<WearSuggestion>() : list.Where(s => s.Colonist == Key(p));

            public Pawn Pawn(string key) => key != null && pawns.TryGetValue(key, out var p) ? p : null;

            public Thing Thing(string key) => key != null && things.TryGetValue(key, out var t) ? t : null;

            public ApparelReport Report(string key) => key != null && reports.TryGetValue(key, out var r) ? r : null;

            public string Place(string key) => key != null && places.TryGetValue(key, out var p) ? p : "";
        }

        private static readonly Dictionary<int, Picks> byMap = new Dictionary<int, Picks>();
        private static readonly Dictionary<(int, ApparelReport, string), (int tick, float rating)> asRole = new Dictionary<(int, ApparelReport, string), (int, float)>();
        private static bool dirty;

        public static void Reset()
        {
            byMap.Clear();
            asRole.Clear();
        }

        /// <summary>Something the choice reads changed by the player's hand (an order given, "not now", an outfit allowed): made again at the next look.</summary>
        public static void Dirty() => dirty = true;

        public static string Key(Pawn p) => "p" + p.thingIDNumber;

        public static string Key(Thing t) => "a" + t.thingIDNumber;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------------------------------------------------------- who and what

        /// <summary>The colonists on a home map the choice is for: the colony's people (slaves and children too), not babies or guests on a quest.</summary>
        public static IEnumerable<Pawn> Colonists(Map map)
        {
            if (map?.mapPawns == null) yield break;
            var seen = new HashSet<int>();
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.SlavesOfColonySpawned))
            {
                if (p == null || p.Dead || !p.RaceProps.Humanlike || p.IsQuestLodger() || p.apparel == null || p.DevelopmentalStage.Baby() || !seen.Add(p.thingIDNumber)) continue;
                yield return p;
            }
        }

        public static Picks Current(Map map) => map != null && byMap.TryGetValue(map.uniqueID, out var p) ? p : null;

        /// <summary>The colony's choice on a home map: kept, made again once an hour, or sooner when what it reads has changed.</summary>
        public static Picks For(Map map)
        {
            if (map == null || !Homes.IsHome(map) || (!On && !ApparelAsks.On)) return null;
            var p = Current(map);
            if (p == null || Stale(p))
            {
                try
                {
                    p = Build(map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("better clothes for each colonist", e);
                    p = new Picks { map = map, tick = Now, made = Time.realtimeSinceStartup };
                }
                byMap[map.uniqueID] = p;
                dirty = false;
            }
            return p;
        }

        private static bool Stale(Picks p)
        {
            int now = Now;
            if (now - p.tick >= GenDate.TicksPerHour || now < p.tick) return true;
            float real = Time.realtimeSinceStartup;
            if (dirty && real - p.made > 0.5f) return true;
            if (real - p.sigAt < 1f) return false;
            p.sigAt = real;
            return Signature(p.map) != p.signature && real - p.made > 2f;
        }

        /// <summary>What the colonists wear and carry, their outfits, and how many pieces there are on the map, in one number.</summary>
        private static int Signature(Map map)
        {
            unchecked
            {
                int h = 17;
                foreach (var p in Colonists(map))
                {
                    h = h * 31 + p.thingIDNumber;
                    var worn = p.apparel.WornApparel;
                    for (int i = 0; i < worn.Count; i++) h = h * 31 + worn[i].thingIDNumber;
                    h = h * 31 + (p.inventory?.innerContainer?.Count ?? 0);
                    h = h * 31 + (p.outfits?.CurrentApparelPolicy?.id ?? 0);
                }
                var all = map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel);
                h = h * 31 + all.Count;
                int forbidden = 0;
                foreach (var t in all)
                    if (t != null && t.IsForbidden(Faction.OfPlayer))
                        forbidden++;
                h = h * 31 + forbidden;
                h = h * 31 + (ApparelPickBook.Instance?.notNow.Count ?? 0);
                return h;
            }
        }

        // ---------------------------------------------------------------- the season ahead

        /// <summary>The season ahead on a map: outside now, and the coldest night and hottest day of this twelfth and the next two (the sun's daily swing of 7 °C), as the game's "Need warm clothes" looks ahead.</summary>
        public static (float now, float coldest, float hottest) Ahead(Map map)
        {
            float now = map?.mapTemperature?.OutdoorTemp ?? 21f, lo = now, hi = now;
            try
            {
                var tile = map.Tile;
                var tw = GenLocalDate.Twelfth(map);
                for (int i = 0; i < 3; i++)
                {
                    float avg = GenTemperature.AverageTemperatureAtTileForTwelfth(tile, tw);
                    lo = Math.Min(lo, avg - ApparelLive.SunSwing);
                    hi = Math.Max(hi, avg + ApparelLive.SunSwing);
                    tw = tw.NextTwelfth();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the season ahead, for the clothes suggestions", e);
            }
            return (now, lo, hi);
        }

        // ---------------------------------------------------------------- the rules of title, role and faith

        /// <summary>A colonist's apparel rules: their title's and role's active requirements (met or not), their faith's desired apparel, their title's least quality.</summary>
        private sealed class Rules
        {
            public readonly List<(ApparelRequirement req, string source, bool met)> reqs = new List<(ApparelRequirement, string, bool)>();
            public readonly HashSet<ThingDef> desired = new HashSet<ThingDef>();
            public readonly HashSet<ThingDef> wornDefs = new HashSet<ThingDef>();
            public QualityCategory minQuality = QualityCategory.Awful;
        }

        private static Rules RulesOf(Pawn p)
        {
            var r = new Rules();
            foreach (var w in p.apparel.WornApparel)
                if (w?.def != null)
                    r.wornDefs.Add(w.def);
            try
            {
                foreach (var rws in p.apparel.AllRequirements)
                {
                    if (rws.requirement == null) continue;
                    var src = rws.Source;
                    if (!ApparelUtility.IsRequirementActive(rws.requirement, src, p, out _)) continue;
                    r.reqs.Add((rws.requirement, src == ApparelRequirementSource.Title ? "title" : "role", rws.requirement.IsMet(p)));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's title and role apparel, for the clothes suggestions", e);
            }
            try
            {
                if (p.Ideo != null)
                    foreach (var pr in p.Ideo.PreceptsListForReading)
                        if (pr is Precept_Apparel pa && pa.apparelDef != null && (pa.TargetGender == Gender.None || pa.TargetGender == p.gender))
                            r.desired.Add(pa.apparelDef);
                if (p.royalty != null)
                    foreach (var title in p.royalty.AllTitlesForReading)
                        if (title?.def != null && title.def.requiredMinimumApparelQuality > r.minQuality)
                            r.minQuality = title.def.requiredMinimumApparelQuality;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's faith's apparel and title's quality, for the clothes suggestions", e);
            }
            return r;
        }

        /// <summary>What keeps a worn piece on its wearer: a rule of their title or role it meets, or their faith's desired apparel; null for none.</summary>
        private static string RuleOf(Rules r, ThingDef def)
        {
            foreach (var (req, source, _) in r.reqs)
                if (req.ApparelMeetsRequirement(def, false))
                    return source;
            return r.desired.Contains(def) ? "faith" : null;
        }

        // ---------------------------------------------------------------- the choice

        private static Picks Build(Map map)
        {
            var picks = new Picks { map = map, tick = Now, made = Time.realtimeSinceStartup };
            picks.sigAt = picks.made;
            picks.signature = Signature(map);
            var input = picks.input;
            var (w, mechs, melee) = ApparelPickMath.Raids(WeaponPickBook.Instance?.Seen(), Now);
            input.Raids = w;
            input.MechShare = mechs;
            input.MeleeShare = melee;
            (picks.now, picks.coldest, picks.hottest) = Ahead(map);
            var book = ApparelPickBook.Instance;
            var colonists = Colonists(map).ToList();
            var rules = new Dictionary<string, Rules>();
            var reportIds = new Dictionary<ApparelReport, int>();

            WearPiece Add(Thing t, ApparelReport r, string where)
            {
                string key = Key(t);
                var x = input.Piece(key);
                if (x != null) return x;
                if (!reportIds.TryGetValue(r, out int id)) reportIds[r] = id = reportIds.Count;
                var (warmth, cooling) = ApparelLive.Comfort(t);
                x = new WearPiece
                {
                    Key = key, Label = t.LabelNoCount, Def = t.def.defName, Group = "r" + id + "/" + where, Role = r.Rating.Role, Overall = r.Rating.Overall,
                    Layers = t.def.apparel.layers.Where(l => l != null).Select(l => l.defName).ToList(),
                    Groups = (t.def.apparel.GetInterferingBodyPartGroups(BodyDefOf.Human) ?? new BodyPartGroupDef[0]).Where(g => g != null).Select(g => g.defName).ToList(),
                    Warmth = warmth, Cooling = cooling, Stage = ApparelLive.DamageStage(t), FromDead = t is Apparel ap && ap.WornByCorpse && t.def.apparel.careIfWornByCorpse
                };
                foreach (var k in new[] { "guns", "melee", "mechanoids", "fire" })
                    if (r.Rating.Situations.TryGetValue(k, out var v))
                        x.Situations[k] = v;
                input.Pieces.Add(x);
                picks.things[key] = t;
                picks.reports[key] = r;
                if (!picks.kinds.Any(k => k.report == r)) picks.kinds.Add((r, t));
                return x;
            }

            // Who they are, what they wear and carry.
            foreach (var p in colonists)
            {
                var c = Colonist(p, book, picks);
                input.Colonists.Add(c);
                picks.pawns[c.Key] = p;
                var rl = RulesOf(p);
                rules[c.Key] = rl;
                foreach (var ap in p.apparel.WornApparel)
                {
                    var r = ap != null ? ApparelLive.Of(ap) : null;
                    if (r?.Rating == null) continue;
                    var x = Add(ap, r, "worn:" + c.Key);
                    x.WornBy = c.Key;
                    x.Locked = p.apparel.IsLocked(ap);
                    x.Rule = RuleOf(rl, ap.def);
                    c.Wears.Add(x.Key);
                }
                var pack = p.inventory?.innerContainer;
                if (pack != null)
                    foreach (var t in pack)
                    {
                        if (!(t is Apparel) || !ApparelLive.Rateable(t.def)) continue;
                        var r = ApparelLive.Of(t);
                        if (r?.Rating == null) continue;
                        Add(t, r, "pack:" + c.Key).CarriedBy = c.Key;
                        c.Carries.Add(Key(t));
                    }
                Keepsakes(p, c, input);
            }
            Fighting(colonists, input);

            // What lies in storage: the best kinds of each role, the warmest and coolest, as many of each as there are colonists.
            var stored = new List<(Thing t, ApparelReport r, Building_OutfitStand stand)>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
            {
                if (t == null || !t.Spawned || !ApparelLive.Rateable(t.def) || !t.IsInAnyStorage() || t.IsBurning()) continue;
                if (t.IsForbidden(Faction.OfPlayer))
                {
                    picks.forbidden++;
                    continue;
                }
                var r = ApparelLive.Of(t);
                if (r?.Rating != null) stored.Add((t, r, null));
            }
            foreach (var stand in map.listerBuildings.AllBuildingsColonistOfClass<Building_OutfitStand>())
            {
                if (stand == null || !stand.Spawned || stand.IsForbidden(Faction.OfPlayer) || stand.HeldItems == null) continue;
                foreach (var item in stand.HeldItems)
                {
                    if (!(item is Apparel) || !ApparelLive.Rateable(item.def)) continue;
                    var r = ApparelLive.Of(item);
                    if (r?.Rating != null) stored.Add((item, r, stand));
                }
            }
            picks.stored = stored.Count;
            bool cold = input.Colonists.Any(c => c.ColdShort >= ApparelPickMath.ShortAt), hot = input.Colonists.Any(c => c.HeatShort >= ApparelPickMath.ShortAt);
            var kinds = stored.GroupBy(x => x.r).ToList();
            var keep = new HashSet<ApparelReport>();
            foreach (var role in kinds.GroupBy(g => g.Key.Rating.Role))
                foreach (var g in role.OrderByDescending(g => g.Key.Rating.Overall).Take(TopPerRole))
                    keep.Add(g.Key);
            if (cold)
                foreach (var g in kinds.OrderByDescending(g => ApparelLive.Comfort(g.First().t).warmth).Take(TopWeather))
                    keep.Add(g.Key);
            if (hot)
                foreach (var g in kinds.OrderByDescending(g => ApparelLive.Comfort(g.First().t).cooling).Take(TopWeather))
                    keep.Add(g.Key);
            int perKind = Math.Max(1, colonists.Count);
            foreach (var g in kinds.Where(g => keep.Contains(g.Key)))
                foreach (var (t, r, stand) in g.Take(perKind))
                {
                    var x = Add(t, r, "stored");
                    x.Stored = true;
                    picks.places[x.Key] = stand != null ? "CC_WeaponPick_On".Translate(stand.def.label.Named("LABEL")).Resolve() : WeaponPick.PlaceOf(t);
                }

            // Each colonist with each piece; alike pieces (one group) weighed once each. Others' worn pieces only for trades of armour.
            foreach (var c in input.Colonists)
            {
                var p = picks.Pawn(c.Key);
                var rl = rules[c.Key];
                var alike = new Dictionary<string, WearOption>();
                foreach (var x in input.Pieces)
                {
                    if (x.WornBy != null && x.WornBy != c.Key && !x.Armour) continue;
                    var t = picks.Thing(x.Key);
                    var r = picks.Report(x.Key);
                    if (x.WornBy == c.Key)
                    {
                        input.Options[(c.Key, x.Key)] = Own(p, c, t, r, rl);
                        continue;
                    }
                    if (x.WornBy == null && alike.TryGetValue(x.Group, out var same))
                    {
                        input.Options[(c.Key, x.Key)] = Copy(same);
                        continue;
                    }
                    var o = OptionFor(picks, p, c, t, r, rl);
                    input.Options[(c.Key, x.Key)] = o;
                    if (x.WornBy == null) alike[x.Group] = o;
                }
            }
            // The choice; a piece a colonist can't walk to (or whose carrier they can't reach) is taken out for them, and the choice made again.
            for (int round = 0; round < 3; round++)
            {
                picks.list = ApparelPickMath.Assign(input);
                bool again = false;
                foreach (var s in picks.list)
                {
                    if (s.Kind == WearKind.Pack || s.Kind == WearKind.Outfit) continue;
                    var p = picks.Pawn(s.Colonist);
                    var t = picks.Thing(s.Piece);
                    var target = s.Kind == WearKind.Spare || s.Kind == WearKind.Trade ? (LocalTargetInfo)picks.Pawn(s.Other) : Where(t);
                    if (p == null || t == null || !target.IsValid || p.CanReach(target, s.Kind == WearKind.Wear ? PathEndMode.ClosestTouch : PathEndMode.Touch, Danger.Deadly)) continue;
                    input.Options[(s.Colonist, s.Piece)].Cant = "NoPath".Translate().CapitalizeFirst();
                    again = true;
                }
                if (!again) break;
            }
            return picks;
        }

        private static WearOption Copy(WearOption o) => new WearOption
        {
            Rating = o.Rating, Cant = o.Cant, Outfit = o.Outfit, OutfitFixable = o.OutfitFixable, Replaces = new List<string>(o.Replaces), As = new Dictionary<string, float>(o.As),
            Meets = new List<string>(o.Meets), Unmet = o.Unmet, Work = o.Work, GenderMood = o.GenderMood
        };

        /// <summary>A colonist as the choice sees them: their work, the season ahead in what they wear, set aside or not.</summary>
        private static WearColonist Colonist(Pawn p, ApparelPickBook book, Picks picks)
        {
            var c = new WearColonist { Key = Key(p), Name = p.LabelShort, CanFight = !p.WorkTagIsDisabled(WorkTags.Violent), Work = WorkOf(p), NotNow = book?.IsNotNow(p) ?? false };
            try
            {
                float min = p.GetStatValue(StatDefOf.ComfyTemperatureMin), max = p.GetStatValue(StatDefOf.ComfyTemperatureMax);
                (c.ColdShort, c.ColdSpare) = ApparelPickMath.Margin(min, picks.coldest, true);
                (c.HeatShort, c.HeatSpare) = ApparelPickMath.Margin(max, picks.hottest, false);
                c.ColdNow = picks.now < ApparelPickMath.ColdAt ? Math.Max(0f, min - picks.now) : 0f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's comfortable temperatures, for the clothes suggestions", e);
            }
            return c;
        }

        private static readonly (string work, string type, string skill)[] Works =
        {
            ("doctor", "Doctor", "Medicine"), ("crafter", "Crafting", "Crafting"), ("crafter", "Smithing", "Crafting"), ("crafter", "Tailoring", "Crafting"),
            ("researcher", "Research", "Intellectual"), ("grower", "Growing", "Plants"), ("miner", "Mining", "Mining"), ("cook", "Cooking", "Cooking"),
            ("builder", "Construction", "Construction"), ("social", "Warden", "Social")
        };

        /// <summary>Their main work, for clothes that speed it: the work they do first (its priority) at a skill of 6 or more; null for none.</summary>
        public static string WorkOf(Pawn p)
        {
            if (p?.skills == null || p.workSettings == null || !p.workSettings.EverWork) return null;
            string best = null;
            int bestPrio = int.MaxValue, bestLevel = -1;
            foreach (var (work, type, skill) in Works)
            {
                var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(type);
                var sk = DefDatabase<SkillDef>.GetNamedSilentFail(skill);
                if (wt == null || sk == null || p.WorkTypeIsDisabled(wt) || !p.workSettings.WorkIsActive(wt)) continue;
                int level = p.skills.GetSkill(sk)?.Level ?? 0;
                int prio = p.workSettings.GetPriority(wt);
                if (level < 6 || prio <= 0) continue;
                if (prio < bestPrio || (prio == bestPrio && level > bestLevel))
                {
                    best = work;
                    bestPrio = prio;
                    bestLevel = level;
                }
            }
            return best;
        }

        /// <summary>How much each colonist fights (WeaponPickMath.FightWeight), against the rest of the colony, as the weapons weigh it.</summary>
        private static void Fighting(List<Pawn> colonists, WearInput input)
        {
            var draftedDef = DefDatabase<RecordDef>.GetNamedSilentFail("TimeDrafted");
            var rows = new List<(WearColonist c, Pawn p, float skill, float drafted, float fights)>();
            foreach (var p in colonists)
            {
                var c = input.Colonist(Key(p));
                if (c == null) continue;
                var rec = p.records;
                float drafted = draftedDef != null && rec != null ? rec.GetValue(draftedDef) : 0f;
                float kills = rec != null ? Math.Max(0f, rec.GetValue(RecordDefOf.Kills) - rec.GetValue(RecordDefOf.KillsAnimals)) : 0f;
                float shots = rec != null ? rec.GetValue(RecordDefOf.ShotsFired) : 0f;
                int skill = Math.Max(p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0);
                rows.Add((c, p, skill, drafted, kills * 10f + shots));
            }
            if (rows.Count == 0) return;
            float topSkill = Math.Max(1f, rows.Max(r => r.skill)), topDrafted = rows.Max(r => r.drafted), topFights = rows.Max(r => r.fights);
            foreach (var (c, p, skill, drafted, fights) in rows)
            {
                float skillShare = skill / topSkill;
                float draftShare = topDrafted >= GenDate.TicksPerHour ? drafted / topDrafted : skillShare;
                float fightShare = topFights >= 1f ? fights / topFights : skillShare;
                bool hunter = p.workSettings != null && p.workSettings.EverWork && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hunting) && p.workSettings.WorkIsActive(WorkTypeDefOf.Hunting);
                c.Fights = WeaponPickMath.FightWeight(c.CanFight, skillShare, draftShare, fightShare, hunter, c.Work == "doctor" || c.Work == "crafter");
            }
        }

        /// <summary>
        /// The worn pieces that mean something to a colonist, for the warning and the small cost: Rimfolk's lucky-item quirk on a
        /// piece of apparel (its mood comes with any piece of its kind; a deep one's good side stronger), and an heirloom they
        /// wear. Only while those moods are on.
        /// </summary>
        private static void Keepsakes(Pawn p, WearColonist c, WearInput input)
        {
            var comp = CouncilGameComponent.Instance;
            var s = S;
            if (comp == null || s == null) return;
            try
            {
                if (s.quirks && s.MoodEffects && comp.quirks != null)
                    foreach (var q in comp.quirks.Of(p))
                    {
                        if (!LuckyItems.IsLucky(q) || q.item == null || !q.item.IsApparel) continue;
                        var t = p.apparel.WornApparel.FirstOrDefault(a => a.thingIDNumber == q.thingId) ?? p.apparel.WornApparel.FirstOrDefault(a => a.def == q.item);
                        var stages = q.def.thought?.stages;
                        if (t == null || input.Piece(Key(t)) == null || stages == null || stages.Count < 2) continue;
                        float with = PracticeMath.Mood(stages[0]?.baseMoodEffect ?? 0f, q.Deep), without = stages[1]?.baseMoodEffect ?? 0f;
                        if (!s.Negatives) without = Math.Max(0f, without);
                        c.Keepsakes.Add(new WearKeepsake { Kind = KeepsakeKind.Lucky, Piece = Key(t), Def = q.item.defName, Label = q.item.label, MoodWith = with, MoodWithout = without });
                    }
                var h = s.heirlooms ? comp.legacy?.HeirloomCarriedBy(p) : null;
                var thought = h?.thing is Apparel ? DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Heirloom") : null;
                if (thought?.stages != null && thought.stages.Count >= 2 && input.Piece(Key(h.thing)) != null)
                    c.Keepsakes.Add(new WearKeepsake
                    {
                        Kind = KeepsakeKind.Heirloom, Piece = Key(h.thing), Def = h.thing.def.defName, Label = h.thing.def.label, Of = h.ofName ?? "",
                        MoodWith = thought.stages[h.heir == p ? 0 : 1]?.baseMoodEffect ?? 0f
                    });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's keepsakes, for the clothes suggestions", e);
            }
        }

        /// <summary>A piece they wear, as its own option: their rating of it, the rules it meets, their work.</summary>
        private static WearOption Own(Pawn p, WearColonist c, Thing t, ApparelReport r, Rules rl)
        {
            var o = new WearOption { Rating = ApparelLive.ForColonist(p, r, t).rating?.Rating ?? r.Rating.Overall, Work = ApparelPickMath.WorkLean(r.Facts, c.Work) };
            Meets(o, rl, t.def, false);
            return o;
        }

        private static void Meets(WearOption o, Rules rl, ThingDef def, bool unmet)
        {
            foreach (var (req, source, met) in rl.reqs)
            {
                if (!req.ApparelMeetsRequirement(def, false)) continue;
                if (!o.Meets.Contains(source)) o.Meets.Add(source);
                if (unmet && !met && o.Unmet == null) o.Unmet = source;
            }
            if (rl.desired.Contains(def))
            {
                if (!o.Meets.Contains("faith")) o.Meets.Add("faith");
                if (unmet && o.Unmet == null && !rl.wornDefs.Contains(def)) o.Unmet = "faith";
            }
        }

        /// <summary>
        /// One colonist with one piece they don't wear: what stops them, their rating of it, what it would take the place of
        /// (and its rating judged as each of those roles), their outfit, the rules it meets, their work, the other gender.
        /// </summary>
        private static WearOption OptionFor(Picks picks, Pawn p, WearColonist c, Thing t, ApparelReport r, Rules rl)
        {
            var o = new WearOption { Cant = CantFor(p, t, rl) };
            if (o.Cant != null) return o;
            var (wearer, fc) = ApparelLive.ForColonist(p, r, t);
            if (fc == null || wearer == null)
            {
                o.Cant = "?";
                return o;
            }
            o.Rating = fc.Rating;
            foreach (var y in ApparelLive.Replaces(p, t.def, t))
            {
                var key = Key(y);
                var py = picks.input.Piece(key);
                if (py == null) continue;
                o.Replaces.Add(key);
                if (py.Role != r.Rating.Role && !o.As.ContainsKey(py.Role)) o.As[py.Role] = AsRole(p, r, wearer, py.Role);
            }
            var policy = p.outfits?.CurrentApparelPolicy;
            if (policy?.filter != null && !policy.filter.Allows(t))
            {
                o.Outfit = policy.label ?? "?";
                o.OutfitFixable = !policy.filter.Allows(t.def) && OutfitLimitsAllow(policy, t);
            }
            Meets(o, rl, t.def, true);
            o.Work = ApparelPickMath.WorkLean(r.Facts, c.Work);
            o.GenderMood = wearer.WrongGender && !wearer.WrongGenderAlready ? wearer.WrongGenderMood.mood : 0f;
            return o;
        }

        /// <summary>A piece's rating for a colonist judged as another role, kept an hour.</summary>
        private static float AsRole(Pawn p, ApparelReport r, ApparelWearer wearer, string role)
        {
            int now = Now;
            var key = (p.thingIDNumber, r, role);
            if (asRole.TryGetValue(key, out var kept) && now - kept.tick < ApparelLive.KeepTicks && now >= kept.tick) return kept.rating;
            if (asRole.Count > 4000) asRole.Clear();
            float v;
            try
            {
                v = ApparelForMath.For(r, wearer, role).Rating;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a piece rated as another role, for the clothes suggestions", e);
                v = r.Rating.Overall;
            }
            asRole[key] = (now, v);
            return v;
        }

        /// <summary>Its quality and wear are within what the outfit allows (so allowing its kind there would let it).</summary>
        private static bool OutfitLimitsAllow(ApparelPolicy policy, Thing t)
        {
            try
            {
                var f = policy.filter;
                if (t.TryGetQuality(out var q) && !f.AllowedQualityLevels.Includes(q)) return false;
                if (t.def.useHitPoints && t.MaxHitPoints > 0 && !f.AllowedHitPointsPercents.IncludesEpsilon((float)t.HitPoints / t.MaxHitPoints)) return false;
                if (t is Apparel ap && ap.WornByCorpse && SpecialThingFilterDefOf.AllowDeadmansApparel != null && !f.Allows(SpecialThingFilterDefOf.AllowDeadmansApparel)) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Why a colonist can't wear a piece, or null: coded to another, their role, their age, nothing to wear it on
        /// (ApparelLive.CantWear); the game's own refusal; it would take off a piece they locked; below the quality their title asks.
        /// </summary>
        private static string CantFor(Pawn p, Thing t, Rules rl)
        {
            string cant = ApparelLive.CantWear(p, t, t.def);
            if (cant != null) return cant;
            if (t is Apparel ap)
            {
                if (!ap.PawnCanWear(p, true)) return "CC_ApparelPick_CantWear".Translate(p.Named("PAWN")).Resolve();
                if (p.apparel.WouldReplaceLockedApparel(ap)) return "CC_ApparelPick_Locked".Translate(p.Named("PAWN")).Resolve();
            }
            if (rl.minQuality > QualityCategory.Awful && t.TryGetQuality(out var q) && q < rl.minQuality)
                return "CC_ApparelPick_TitleQuality".Translate(p.Named("PAWN"), rl.minQuality.GetLabel().Named("QUALITY")).Resolve();
            return null;
        }

        /// <summary>A colonist's ratings of the kinds the colony's choice weighs, worked out ahead (the pass's slice), so the choice reads kept ones.</summary>
        public static void Warm(Picks picks, Pawn p)
        {
            if (picks == null || p?.apparel == null) return;
            foreach (var (r, t) in picks.kinds)
                if (t != null && !t.Destroyed)
                    ApparelLive.ForColonist(p, r, t);
        }

        // ---------------------------------------------------------------- where it is

        /// <summary>Where a piece is to be fetched from: the piece, or the outfit stand holding it.</summary>
        public static LocalTargetInfo Where(Thing t)
        {
            if (t == null || t.Destroyed) return LocalTargetInfo.Invalid;
            if (t.Spawned) return t;
            if (t.ParentHolder is Building_OutfitStand stand && stand.Spawned) return stand;
            return LocalTargetInfo.Invalid;
        }

        /// <summary>
        /// Whether a suggestion still stands as the map is now: the colonist here and up and not wearing it; a piece from storage
        /// still lying there (not forbidden, not taken); one in their pack or another's still there; a trade's two still
        /// wearing what they wore; a piece their outfit didn't allow still not allowed (allowed, the next choice takes it up).
        /// </summary>
        public static bool Stands(Picks picks, WearSuggestion s)
        {
            if (picks == null || s == null) return false;
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Piece);
            if (p == null || t == null || p.Dead || p.Destroyed || !p.Spawned || p.Map != picks.map || t.Destroyed || p.apparel == null) return false;
            if (t is Apparel ap && p.apparel.WornApparel.Contains(ap)) return false;
            switch (s.Kind)
            {
                case WearKind.Trade:
                    var other = picks.Pawn(s.Other);
                    var mine = picks.Thing(s.Instead.FirstOrDefault()) as Apparel;
                    return other != null && other.Spawned && !other.Dead && other.Map == picks.map && other.apparel != null && other.apparel.WornApparel.Contains(t as Apparel)
                           && mine != null && p.apparel.WornApparel.Contains(mine);
                case WearKind.Pack:
                    return p.inventory?.innerContainer != null && p.inventory.innerContainer.Contains(t);
                case WearKind.Spare:
                    var giver = picks.Pawn(s.Other);
                    return giver != null && giver.Spawned && !giver.Dead && giver.Map == picks.map && giver.inventory?.innerContainer != null && giver.inventory.innerContainer.Contains(t);
                case WearKind.Outfit:
                    var policy = p.outfits?.CurrentApparelPolicy;
                    return policy?.filter != null && !policy.filter.Allows(t) && StoredStill(picks, t);
                default:
                    return StoredStill(picks, t);
            }
        }

        private static bool StoredStill(Picks picks, Thing t)
        {
            if (t.ParentHolder is Building_OutfitStand stand) return stand.Spawned && stand.Map == picks.map && !stand.IsForbidden(Faction.OfPlayer);
            return t.Spawned && t.Map == picks.map && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage();
        }

        /// <summary>A colonist's suggestions that still stand: gaps first, then the rest by gain.</summary>
        public static List<WearSuggestion> Standing(Pawn p)
        {
            if (p == null || !p.Spawned || !On) return new List<WearSuggestion>();
            var picks = For(p.Map);
            return picks == null ? new List<WearSuggestion>()
                : picks.Of(p).Where(s => Stands(picks, s)).OrderBy(s => s.Kind == WearKind.Outfit ? 1 : 0).ThenBy(s => s.Gap.HasValue ? 0 : 1).ThenByDescending(s => s.Gain).ToList();
        }

        // ---------------------------------------------------------------- words

        private static string F1(float v) => WeaponForMath.RatingText(v);

        public static string Label(Picks picks, string key) => picks.Thing(key)?.LabelNoCount ?? picks.input.Piece(key)?.Label ?? "?";

        /// <summary>The worn pieces it takes the place of, in words: "his tattered T-shirt and duster".</summary>
        private static string InsteadWords(Picks picks, WearSuggestion s)
        {
            var labels = s.Instead.Select(k => Label(picks, k)).ToList();
            return labels.Count == 0 ? "" : labels.ToCommaList(true);
        }

        /// <summary>A work's name, as the lines say it: "doctor", "cook".</summary>
        public static string WorkWord(string work) => ("CC_ApparelPick_Work_" + (work ?? "")).Translate().Resolve();

        /// <summary>The head of a suggestion: "Warmth for Bautista: parka (+40 °C)", "No helmet: flak helmet for Ana", "Better for Ana: recon armour (+1.6)".</summary>
        public static string Head(Picks picks, WearSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            string piece = Label(picks, s.Piece);
            if (s.Kind == WearKind.Outfit) return "CC_ApparelPick_Head_Outfit".Translate(p.Named("PAWN"), s.Outfit.Named("OUTFIT"), piece.Named("APPAREL")).Resolve();
            if (s.Kind == WearKind.Trade)
                return "CC_ApparelPick_Head_Trade".Translate(p.Named("PAWN"), picks.Pawn(s.Other).Named("OTHER"), piece.Named("APPAREL"), ApparelPickMath.GainText(s.Gain).Named("GAIN")).Resolve();
            switch (s.Gap)
            {
                case WearGap.Warmth:
                case WearGap.Cooling:
                    return ("CC_ApparelPick_Head_" + s.Gap).Translate(p.Named("PAWN"), piece.Named("APPAREL"), ApparelPickMath.DegreesText(s.Degrees).Named("DEGREES")).Resolve();
                case WearGap.Armour:
                case WearGap.Helmet:
                    return ("CC_ApparelPick_Head_" + s.Gap).Translate(p.Named("PAWN"), piece.Named("APPAREL")).Resolve();
                case WearGap.Worn:
                    return "CC_ApparelPick_Head_Worn".Translate(p.Named("PAWN"), piece.Named("APPAREL"), InsteadWords(picks, s).Named("WORN")).Resolve();
            }
            if (s.Kind == WearKind.Spare)
                return "CC_ApparelPick_Head_Spare".Translate(p.Named("PAWN"), picks.Pawn(s.Other).Named("OTHER"), piece.Named("APPAREL"), ApparelPickMath.GainText(s.Gain).Named("GAIN")).Resolve();
            return "CC_ApparelPick_Head".Translate(p.Named("PAWN"), piece.Named("APPAREL"), ApparelPickMath.GainText(s.Gain).Named("GAIN")).Resolve();
        }

        private static string MoodText(float v)
        {
            int n = (int)Math.Round(v);
            return n > 0 ? "+" + n : n.ToString();
        }

        private static string Share(float v) => Mathf.RoundToInt(Mathf.Abs(v) * 100f) + "%";

        /// <summary>One reason or cost in words.</summary>
        public static string Words(Picks picks, WearSuggestion s, WearReason r)
        {
            var p = picks.Pawn(s.Colonist);
            string piece = Label(picks, s.Piece);
            switch (r.Kind)
            {
                case WearWhy.Warmth:
                case WearWhy.Cooling:
                    return ("CC_ApparelPick_Why_" + r.Kind).Translate(p.Named("PAWN"), r.Level.Named("SHORT"), ApparelPickMath.DegreesText(r.Value).Named("DEGREES")).Resolve();
                case WearWhy.Worn:
                    return ("CC_ApparelPick_Why_Worn_" + Math.Max(0, Math.Min(1, r.Level))).Translate(p.Named("PAWN"), r.Text.Named("WORN")).Resolve();
                case WearWhy.Dead:
                    return "CC_ApparelPick_Why_Dead".Translate(p.Named("PAWN"), r.Text.Named("WORN")).Resolve();
                case WearWhy.Work:
                    return "CC_ApparelPick_Why_Work".Translate(p.Named("PAWN"), WorkWord(r.Text).Named("WORK"), Share(r.Value).Named("SHARE")).Resolve();
                case WearWhy.Slower:
                    return "CC_ApparelPick_Cost_Slower".Translate(p.Named("PAWN"), WorkWord(r.Text).Named("WORK"), Share(r.Value).Named("SHARE")).Resolve();
                case WearWhy.Better:
                    return "CC_ApparelPick_Why_Better".Translate(p.Named("PAWN"), F1(s.Rating).Named("RATING"), F1(s.Before).Named("BEFORE")).Resolve();
                case WearWhy.AddOn:
                    return "CC_ApparelPick_Why_AddOn".Translate(p.Named("PAWN"), F1(s.Rating).Named("RATING")).Resolve();
                case WearWhy.Spare:
                    return picks.Pawn(r.Text) is Pawn giver ? "CC_ApparelPick_Why_Spare".Translate(giver.Named("OTHER")).Resolve() : "";
                case WearWhy.Trade:
                    return "CC_ApparelPick_Why_Trade".Translate(picks.Pawn(s.Other).Named("OTHER")).Resolve();
                case WearWhy.TradeColony:
                    return "CC_WeaponPick_Why_TradeColony".Translate().Resolve();
                case WearWhy.RarelyFights:
                    return picks.Pawn(r.Text) is Pawn rare ? "CC_WeaponPick_Why_RarelyFights".Translate(rare.Named("OTHER")).Resolve() : "";
                case WearWhy.Lucky:
                    return picks.Pawn(r.Text) is Pawn lucky
                        ? "CC_ApparelPick_Cost_Lucky".Translate(lucky.Named("OTHER"), r.Extra.Named("APPAREL"), MoodText(r.Level).Named("WITHOUT"), MoodText(r.Value).Named("WITH")).Resolve()
                        : "";
                case WearWhy.Heirloom:
                    return picks.Pawn(r.Text) is Pawn heir
                        ? "CC_ApparelPick_Cost_Heirloom".Translate(heir.Named("OTHER"), r.Extra.Named("APPAREL"), r.Of.Named("LOST"), MoodText(r.Value).Named("WITH")).Resolve()
                        : "";
                case WearWhy.Gender:
                    return "CC_ApparelPick_Cost_Gender".Translate(p.Named("PAWN"), MoodText(r.Value).Named("MOOD")).Resolve();
                default:
                    return ("CC_ApparelPick_Why_" + r.Kind).Translate(p.Named("PAWN"), piece.Named("APPAREL")).Resolve();
            }
        }

        private static string Join(List<string> parts) =>
            parts.Count == 0 ? "" : parts.Count == 1 ? parts[0] : "CC_WeaponPick_And".Translate(string.Join(", ", parts.Take(parts.Count - 1)).Named("A"), parts.Last().Named("B")).Resolve();

        /// <summary>The reasons in one line: "he is 18 °C short for the cold ahead, and it adds 40".</summary>
        public static string Why(Picks picks, WearSuggestion s) => Join(s.Reasons.Select(r => Words(picks, s, r)).Where(x => !x.NullOrEmpty()).ToList());

        /// <summary>What it costs, as a sentence ("Though Ana would be upset to take off her lucky cowboy hat ..."), or empty.</summary>
        public static string Costs(Picks picks, WearSuggestion s)
        {
            var parts = s.Costs.Select(r => Words(picks, s, r)).Where(x => !x.NullOrEmpty()).ToList();
            return parts.Count == 0 ? "" : "CC_WeaponPick_Costs".Translate(Join(parts).Named("COST")).Resolve();
        }

        /// <summary>The whole line: "Parka: +4.1 for Bautista: he is 18 °C short ...", in place of what, and what it costs; a trade says both sides; an outfit's says so.</summary>
        public static string Full(Picks picks, WearSuggestion s)
        {
            var p = picks.Pawn(s.Colonist);
            string piece = Label(picks, s.Piece).CapitalizeFirst();
            string line = s.Kind == WearKind.Trade
                ? "CC_ApparelPick_FullTrade".Translate(piece.Named("APPAREL"), p.Named("PAWN"), ApparelPickMath.GainText(s.Gain).Named("GAIN"), picks.Pawn(s.Other).Named("OTHER"),
                    Label(picks, s.Instead.FirstOrDefault()).Named("MINE"), ApparelPickMath.GainText(s.OtherGain).Named("OTHERGAIN"), Why(picks, s).Named("WHY")).Resolve()
                : "CC_ApparelPick_Full".Translate(piece.Named("APPAREL"), ApparelPickMath.GainText(s.Gain).Named("GAIN"), p.Named("PAWN"), Why(picks, s).Named("WHY")).Resolve();
            if (s.Kind != WearKind.Trade && s.Instead.Count > 0) line += " " + "CC_ApparelPick_InPlaceOf".Translate(p.Named("PAWN"), InsteadWords(picks, s).Named("WORN")).Resolve();
            if (s.Kind == WearKind.Outfit) line += " " + "CC_ApparelPick_OutfitBars".Translate(p.Named("PAWN"), s.Outfit.Named("OUTFIT")).Resolve();
            string costs = Costs(picks, s);
            return costs.NullOrEmpty() ? line : line + " " + costs;
        }

        // ---------------------------------------------------------------- acting on it

        private static JobDef spareJob, tradeJob;
        private static bool jobsLooked;

        private static void LookJobs()
        {
            if (jobsLooked) return;
            jobsLooked = true;
            spareJob = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_TakeSpareApparel");
            tradeJob = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_SwapApparel");
        }

        /// <summary>A colonist is on their way to it already (the job a button gave them, under way).</summary>
        public static bool OnTheWay(Picks picks, WearSuggestion s)
        {
            LookJobs();
            var p = picks?.Pawn(s.Colonist);
            var job = p?.CurJob;
            if (job == null) return false;
            var t = picks.Thing(s.Piece);
            if (s.Kind == WearKind.Trade) return job.def == tradeJob && job.targetB.Thing == t;
            if (s.Kind == WearKind.Spare) return job.def == spareJob && job.targetB.Thing == t;
            return job.def == JobDefOf.Wear && job.targetA.Thing == t;
        }

        /// <summary>The colonists who share an outfit, for its button's words.</summary>
        public static int SharingOutfit(Map map, ApparelPolicy policy) => policy == null ? 0 : Colonists(map).Count(p => p.outfits?.CurrentApparelPolicy == policy);

        /// <summary>
        /// Why a button can't be used now, or null: the suggestion no longer stands, the colonist is down or not themselves,
        /// can't use their hands, can't walk there, it burns or someone else has it reserved; for a hand-over or a trade, the
        /// other not able to.
        /// </summary>
        public static string WhyNot(Picks picks, WearSuggestion s)
        {
            LookJobs();
            if (!Stands(picks, s)) return "CC_ApparelPick_Gone".Translate().Resolve();
            if (s.Kind == WearKind.Outfit) return null;
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Piece);
            if (p.Downed || p.InMentalState) return "CC_WeaponPick_Busy".Translate(p.Named("PAWN")).Resolve();
            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return "Incapable".Translate().CapitalizeFirst();
            if (s.Kind == WearKind.Spare || s.Kind == WearKind.Trade)
            {
                if ((s.Kind == WearKind.Spare ? spareJob : tradeJob) == null) return "?";
                var other = picks.Pawn(s.Other);
                if (other.Downed || other.InMentalState || other.Drafted || !other.Awake()) return "CC_WeaponPick_Busy".Translate(other.Named("PAWN")).Resolve();
                if (!p.CanReach(other, PathEndMode.Touch, Danger.Deadly)) return "NoPath".Translate().CapitalizeFirst();
                return null;
            }
            if (s.Kind == WearKind.Wear)
            {
                var where = Where(t);
                if (!p.CanReach(where, PathEndMode.ClosestTouch, Danger.Deadly)) return "NoPath".Translate().CapitalizeFirst();
                if (t.Spawned && t.IsBurning()) return "BurningLower".Translate().CapitalizeFirst();
                if (t.Spawned && !p.CanReserve(t)) return "CC_WeaponPick_Reserved".Translate().Resolve();
            }
            return null;
        }

        /// <summary>
        /// Gives the order: the colonist walks over and puts it on by the game's own Wear job (from an outfit stand too), what it
        /// takes the place of dropped there as the game does; takes it from their own pack and puts it on; walks to the one who
        /// carries it and is handed it; walks to the other to trade armour; or, for a piece their outfit doesn't allow, allows
        /// its kind in that outfit. <paramref name="queue"/>: after what they were already told to do (wear all).
        /// </summary>
        public static bool Do(Picks picks, WearSuggestion s, bool queue = false)
        {
            if (WhyNot(picks, s) != null) return false;
            var p = picks.Pawn(s.Colonist);
            var t = picks.Thing(s.Piece);
            try
            {
                Job job;
                switch (s.Kind)
                {
                    case WearKind.Outfit:
                        AllowInOutfit(p, t);
                        return true;
                    case WearKind.Trade:
                        job = JobMaker.MakeJob(tradeJob, picks.Pawn(s.Other), t, picks.Thing(s.Instead.FirstOrDefault()));
                        break;
                    case WearKind.Spare:
                        job = JobMaker.MakeJob(spareJob, picks.Pawn(s.Other), t);
                        break;
                    case WearKind.Pack:
                        // From their own pack: set down at their feet and put on there.
                        if (!p.inventory.innerContainer.TryDrop(t, p.Position, p.Map, ThingPlaceMode.Near, out var dropped) || dropped == null) return false;
                        dropped.SetForbidden(false, false);
                        t = dropped;
                        job = JobMaker.MakeJob(JobDefOf.Wear, t);
                        break;
                    default:
                        job = t.ParentHolder is Building_OutfitStand stand ? JobMaker.MakeJob(JobDefOf.Wear, t, stand) : JobMaker.MakeJob(JobDefOf.Wear, t);
                        break;
                }
                bool given = p.jobs.TryTakeOrderedJob(job, JobTag.Misc, queue);
                var where = s.Kind == WearKind.Spare || s.Kind == WearKind.Trade ? (LocalTargetInfo)picks.Pawn(s.Other) : Where(t);
                if (given && where.IsValid) FleckMaker.Static(where.Cell.ToVector3Shifted(), p.Map, FleckDefOf.FeedbackEquip);
                Dirty();
                return given;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("giving the order for a suggested piece of clothing", e);
                return false;
            }
        }

        /// <summary>Allows a piece's kind in the colonist's outfit (every colonist in that outfit may then wear it); its quality or wear outside the outfit's limits opens the outfit instead.</summary>
        public static void AllowInOutfit(Pawn p, Thing t)
        {
            var policy = p?.outfits?.CurrentApparelPolicy;
            if (policy?.filter == null || t == null) return;
            if (!policy.filter.Allows(t.def) && OutfitLimitsAllow(policy, t))
            {
                policy.filter.SetAllow(t.def, true);
                Messages.Message("CC_ApparelPick_Allowed".Translate(t.def.label.Named("KIND"), policy.label.Named("OUTFIT")).Resolve(), new LookTargets(p), MessageTypeDefOf.TaskCompletion, false);
            }
            else Find.WindowStack.Add(new Dialog_ManageApparelPolicies(policy));
            Dirty();
        }

        /// <summary>
        /// "Wear all suggested": every suggestion that stands and can be done now is ordered, each colonist's after what they
        /// were already told (a piece their outfit doesn't allow is not: that is the player's own choice, from its row). Only
        /// orders; nothing is done by itself later. Returns how many were ordered.
        /// </summary>
        public static int DoAll(Picks picks)
        {
            if (picks == null) return 0;
            int n = 0;
            var told = new HashSet<string>();
            foreach (var s in picks.list.Where(x => x.Kind != WearKind.Outfit).OrderBy(x => x.Gap.HasValue ? 0 : 1).ThenBy(x => (int)x.Kind))
            {
                if (!Doable(picks, s)) continue;
                if (s.Kind == WearKind.Trade && (told.Contains(s.Other) || told.Contains(s.Colonist))) continue;
                if (Do(picks, s, told.Contains(s.Colonist)))
                {
                    told.Add(s.Colonist);
                    n++;
                }
            }
            return n;
        }

        /// <summary>Whether a suggestion can be ordered now from "Wear all suggested": it stands, isn't under way, nothing stops it, and isn't an outfit's.</summary>
        public static bool Doable(Picks picks, WearSuggestion s) => s.Kind != WearKind.Outfit && Stands(picks, s) && !OnTheWay(picks, s) && WhyNot(picks, s) == null;

        // ---------------------------------------------------------------- who should wear this

        /// <summary>The colonists a piece suits best (its card, "Who should wear this"), from the choice on the map in view; with what it adds, and the need it fills.</summary>
        public static List<(Pawn p, float rating, float gain, WearGap? gap, bool wears)> WhoShouldWear(Thing thing)
        {
            var list = new List<(Pawn, float, float, WearGap?, bool)>();
            var map = Find.CurrentMap;
            var picks = On ? For(map) : null;
            if (picks == null || thing == null) return list;
            var input = picks.input;
            var x = input.Piece(Key(thing));
            bool added = false;
            var r = ApparelLive.Of(thing);
            if (r?.Rating == null) return list;
            var weighedHere = new List<string>();
            if (x != null)
            {
                // A piece someone wears that the choice weighs only for its wearer (clothes, not armour): the others weighed here, not kept.
                foreach (var c in input.Colonists)
                {
                    if (input.Options.ContainsKey((c.Key, x.Key))) continue;
                    var p = picks.Pawn(c.Key);
                    if (p == null || p.Destroyed) continue;
                    try
                    {
                        input.Options[(c.Key, x.Key)] = OptionFor(picks, p, c, thing, r, RulesOf(p));
                        weighedHere.Add(c.Key);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("who should wear a piece, on its card", e);
                    }
                }
            }
            else
            {
                // Not one the colony's choice weighs (elsewhere, a trader's, a kind the choice left out): weighed here, not kept.
                var (warmth, cooling) = ApparelLive.Comfort(thing);
                x = new WearPiece
                {
                    Key = "card", Label = thing.LabelNoCount, Def = thing.def.defName, Role = r.Rating.Role, Overall = r.Rating.Overall,
                    Layers = thing.def.apparel.layers.Where(l => l != null).Select(l => l.defName).ToList(),
                    Groups = (thing.def.apparel.GetInterferingBodyPartGroups(BodyDefOf.Human) ?? new BodyPartGroupDef[0]).Where(g => g != null).Select(g => g.defName).ToList(),
                    Warmth = warmth, Cooling = cooling, Stage = ApparelLive.DamageStage(thing), Stored = true
                };
                input.Pieces.Add(x);
                added = true;
                foreach (var c in input.Colonists)
                {
                    var p = picks.Pawn(c.Key);
                    if (p == null || p.Destroyed) continue;
                    try
                    {
                        input.Options[(c.Key, x.Key)] = OptionFor(picks, p, c, thing, r, RulesOf(p));
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("who should wear a piece, on its card", e);
                    }
                }
            }
            try
            {
                foreach (var (c, rating, gain, gap) in ApparelPickMath.WhoShouldWear(input, x))
                {
                    var p = picks.Pawn(c.Key);
                    if (p != null) list.Add((p, rating, gain, gap, c.Wears.Contains(x.Key)));
                }
            }
            finally
            {
                if (added)
                {
                    input.Pieces.Remove(x);
                    foreach (var c in input.Colonists) input.Options.Remove((c.Key, x.Key));
                }
                foreach (var k in weighedHere) input.Options.Remove((k, x.Key));
            }
            return list;
        }
    }
}
