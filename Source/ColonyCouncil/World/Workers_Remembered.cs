using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Rimfolk events that follow from what a people remembers of the colony (World/FactionMemory.cs): an envoy who comes
    // to repay an old debt, or to call in a favor the colony owes; relief from old friends in a hard time; an old enemy
    // offering peace once the feud has gone quiet for years; and a quarrel between two peoples that one of them asks the
    // colony to settle. Texts: Defs/IncidentDefs/Council_Events_Remembered*.xml. None ends in a fight.

    /// <summary>Which memory an event follows from, and how it plays out (see <see cref="HappeningWorker_Remembered"/>).</summary>
    public class RememberedEvent : DefModExtension
    {
        /// <summary>
        /// grateful (a people in the colony's debt), owed (a people the colony owes), relief (a grateful people, while times
        /// are hard), oldEnemy (a people at war, the feud quiet for a long while), quarrel (a grateful people and another).
        /// </summary>
        public string want = "grateful";
        /// <summary>How strongly the deed must still be remembered.</summary>
        public float minStrength = 8f;
        /// <summary>How long ago it must have been (news travels, envoys take their time).</summary>
        public float minAgeDays = 10f;
        /// <summary>An old enemy: days since anything last passed between them and the colony.</summary>
        public float quietDays = 90f;
        /// <summary>The visitors wait at the edge of the map until the colony answers (an enemy's envoy), else walk in.</summary>
        public bool waits;
        /// <summary>{COUNT}: silver, scaled by how big the deed was and the colony's wealth (a gift's worth, a favor's price).</summary>
        public FloatRange value = FloatRange.Zero;
        /// <summary>Answers that only make sense with {DEAD}, a colonist the people killed (weregild).</summary>
        public List<int> needsDead;
        /// <summary>What two peoples quarrel over ({TALE} in a quarrel), tagged like any line.</summary>
        [MustTranslate] public List<string> matters;
    }

    /// <summary>
    /// A Rimfolk event about a people and what it remembers: the people (and the deed) is picked from the colony's memory,
    /// their envoy comes (walks in, or waits at the edge), and the answers settle the memory, add to it, or both, through
    /// <see cref="DilemmaEffect_Remember"/>. Lines can ask for the deed ([deed:released], [deed:hardship]...), which side
    /// it's on ([grateful], [owed], [ourwrong], [theirwrong]), [named] when it names someone, [killedours] when the people
    /// killed a colonist ({DEAD}), and for relief what is sent ([food], [medicine]) and why ([famine], [plague], [winter],
    /// [flood], [hungry], [sick]); the land's tags work too. {TALE} is the deed in a few words ("set Ana free"), or in a
    /// quarrel what it's about; {PLACE} is the people's nearest town; {ENEMY} the other people in a quarrel.
    /// </summary>
    public class HappeningWorker_Remembered : HappeningWorker_Visitors
    {
        private static readonly RememberedEvent Empty = new RememberedEvent();

        public RememberedEvent R => def.GetModExtension<RememberedEvent>() ?? Empty;

        public const string NotePrefix = "memory:";

        public static FactionDeed DeedOf(DilemmaContext ctx)
        {
            string n = (ctx as HappeningContext)?.note;
            if (n == null || !n.StartsWith(NotePrefix) || !int.TryParse(n.Substring(NotePrefix.Length), out int id)) return null;
            return FactionMemory.Instance?.ById(id);
        }

        // ---------------- Who ----------------

        /// <summary>A people that can send someone: not the colony, not hidden, with people of its own.</summary>
        private static bool Sends(Faction f) =>
            FactionMemory.Remembers(f) && !f.defeated && Arrivals.PersonKind(f) != null
            && (!IsEmpire(f) || ModsConfig.RoyaltyActive);

        /// <summary>The Empire only with Royalty itself (its def is Royalty's).</summary>
        private static bool IsEmpire(Faction f) => f?.def?.defName == "Empire";

        /// <summary>An enemy that could make peace at all (not pirates and the like).</summary>
        public static bool CanMakePeace(Faction f) =>
            Sends(f) && !f.def.permanentEnemy && f.HasGoodwill && f.HostileTo(Faction.OfPlayer) && f.CanChangeGoodwillFor(Faction.OfPlayer, 50);

        private struct Choice
        {
            public Faction faction;
            public FactionDeed deed;
            public Faction other;
            public float weight;
        }

        private static readonly HashSet<string> Rescues = new HashSet<string> { "released", "tended", "hardship", "refugees", "dignitary", "campHelped" };

        private List<Choice> Choices(Map map)
        {
            var list = new List<Choice>();
            var mem = FactionMemory.Instance;
            if (mem == null) return list;
            var r = R;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (!Sends(f)) continue;
                switch (r.want)
                {
                    case "grateful":
                    {
                        if (!LivingEvents.Friendly(f)) continue;
                        var d = mem.Best(f, DeedSide.Favor, r.minStrength, r.minAgeDays);
                        if (d != null) list.Add(new Choice { faction = f, deed = d, weight = d.Strength });
                        break;
                    }
                    case "owed":
                    {
                        if (!LivingEvents.Friendly(f)) continue;
                        var d = mem.Best(f, DeedSide.TheirFavor, r.minStrength, r.minAgeDays);
                        if (d != null) list.Add(new Choice { faction = f, deed = d, weight = d.Strength });
                        break;
                    }
                    case "relief":
                    {
                        if (!LivingEvents.Friendly(f)) continue;
                        var d = mem.Best(f, DeedSide.Favor, r.minStrength, r.minAgeDays);
                        if (d != null) list.Add(new Choice { faction = f, deed = d, weight = d.Strength * (Rescues.Contains(d.kind) ? 2f : 1f) });
                        break;
                    }
                    case "oldEnemy":
                    {
                        if (!CanMakePeace(f) || mem.SinceLast(f) < r.quietDays * GenDate.TicksPerDay) continue;
                        var ours = mem.Best(f, DeedSide.Wrong, r.minStrength, 0f, false);
                        var theirs = mem.Best(f, DeedSide.TheirWrong, r.minStrength, 0f, false);
                        var d = ours == null ? theirs : theirs == null ? ours : ours.Strength >= theirs.Strength ? ours : theirs;
                        if (d != null) list.Add(new Choice { faction = f, deed = d, weight = d.Strength });
                        break;
                    }
                    case "quarrel":
                    {
                        if (!LivingEvents.Friendly(f) || LivingEvents.TownOf(f, map) == null) continue;
                        float grateful = mem.Sum(f, DeedSide.Favor);
                        if (grateful < r.minStrength && f.PlayerGoodwill < 40) continue;
                        // Never two peoples at war with each other: their envoys would fight on the colony's land.
                        var other = Find.FactionManager.AllFactionsListForReading
                            .Where(o => o != f && Sends(o) && LivingEvents.Friendly(o) && !o.HostileTo(f) && LivingEvents.TownOf(o, map) != null)
                            .RandomElementByWeightWithFallback(o => 1f + mem.Sum(o, DeedSide.Favor) / 10f);
                        if (other != null) list.Add(new Choice { faction = f, other = other, deed = mem.Best(f, DeedSide.Favor, 1f), weight = 1f + grateful / 10f });
                        break;
                    }
                }
            }
            return list;
        }

        // ---------------- Can it happen? ----------------

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (!FactionMemory.On || !StorytellerEvents.Adults(map).Any()) return false;
            var r = R;
            if (r.want == "relief" && HardTime(map) == null) return false;
            if (r.want == "oldEnemy" && (MapFacts.ThreatOn(map) || Now - map.storyState.LastThreatBigTick < GenDate.TicksPerDay * 3)) return false;
            return Choices(map).Count > 0;
        }

        /// <summary>
        /// Why times are hard, as a tag, or null: a plague, a famine (or locusts, or a drought), a hard winter or a flood in
        /// the region, or at home the stores nearly empty or the sick filling the beds.
        /// </summary>
        public static string HardTime(Map map)
        {
            var ws = WorldSituations.Instance;
            if (ws != null)
            {
                foreach (var s in ws.Running)
                {
                    string n = s.def?.defName ?? "";
                    if (n == "Council_WS_Plague") return "plague";
                    if (n == "Council_WS_Famine" || n == "Council_WS_Locusts" || n == "Council_WS_Drought") return "famine";
                    if (n == "Council_WS_HardWinter") return "winter";
                    if (n == "Council_WS_Flood") return "flood";
                }
            }
            if (map == null) return null;
            var people = map.mapPawns.FreeColonistsSpawned;
            if (people.Count == 0) return null;
            // Less than about four days' food for everyone.
            if (map.resourceCounter.TotalHumanEdibleNutrition < people.Count * 1.6f * 4f) return "hungry";
            int sick = people.Count(p => p.health?.hediffSet?.hediffs?.Any(h => h.def.makesSickThought) ?? false);
            if (sick >= 2 && sick * 3 >= people.Count) return "sick";
            return null;
        }

        // ---------------- Casting ----------------

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var r = R;
            var pick = Choices(map).RandomElementByWeightWithFallback(c => Mathf.Max(0.01f, c.weight));
            if (pick.faction == null) return false;
            ctx.faction = pick.faction;
            ctx.enemy = pick.other;
            var deed = pick.deed;
            if (deed != null) ctx.note = NotePrefix + deed.id;
            ctx.a = ext.colonist?.bestAt != null ? VarietyUtil.Best(map, ext.colonist.bestAt) : null;
            ctx.a = ctx.a ?? StorytellerEvents.BestOrAny(map, SkillDefOf.Social);
            if (ctx.a == null) return false;
            MakeGroup(ctx, ext, Mathf.Max(1, ext.count.RandomInRange), ext.ages);
            if (ctx.stranger == null) return false;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name;
            ctx.tale = deed != null ? FactionMemory.Text(deed) : "";
            float size = deed != null ? Mathf.Clamp(deed.Strength / 12f, 0.75f, 2f) : 1f;
            if (r.value.max > 0f) ctx.count = Mathf.Max(10, Mathf.RoundToInt(r.value.RandomInRange * size * LivingEvents.WealthFactor(map) / 10f) * 10);
            switch (r.want)
            {
                case "relief":
                    if (!Relief(ctx, map)) return false;
                    break;
                case "oldEnemy":
                    ctx.dead = FactionMemory.Instance?.Best(ctx.faction, DeedSide.TheirWrong, 1f, 0f, false, d => d.kind == "killedOurs" && !d.name.NullOrEmpty())?.name ?? "";
                    break;
                case "quarrel":
                {
                    if (ctx.enemy == null) return false;
                    var kind = Arrivals.PersonKind(ctx.enemy);
                    var p2 = kind == null ? null : Arrivals.Make(kind, ctx.enemy, ext.ages);
                    if (p2 == null) return false;
                    ctx.group.Add(p2);
                    ctx.tale = r.matters.NullOrEmpty() ? "" : Pick(r.matters, ctx);
                    break;
                }
            }
            return true;
        }

        /// <summary>What old friends send in a hard time: food or medicine of the colony's era, enough to matter.</summary>
        private static bool Relief(HappeningContext ctx, Map map)
        {
            string why = HardTime(map);
            bool medicine = why == "plague" || why == "sick";
            bool low = Avail.IsPreIndustrial;
            ThingDef def;
            if (medicine)
            {
                def = DefDatabase<ThingDef>.GetNamedSilentFail(low ? "MedicineHerbal" : "MedicineIndustrial") ?? DefDatabase<ThingDef>.GetNamedSilentFail("MedicineHerbal");
                ctx.count = Rand.RangeInclusive(6, 10) + map.mapPawns.FreeColonistsSpawnedCount / 3;
            }
            else
            {
                def = (low ? null : DefDatabase<ThingDef>.GetNamedSilentFail("MealSurvivalPack")) ?? DefDatabase<ThingDef>.GetNamedSilentFail("Pemmican");
                if (def == null) return false;
                // About three days' food for everyone at home.
                float each = Mathf.Max(0.01f, def.GetStatValueAbstract(StatDefOf.Nutrition));
                float need = map.mapPawns.FreeColonistsSpawnedCount * 1.6f * 3f;
                ctx.count = Mathf.Clamp(Mathf.RoundToInt(need / each / 5f) * 5, 5, Mathf.Max(5, def.stackLimit * 4));
            }
            ctx.thing = def;
            return def != null && ctx.count > 0;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var everyone = ctx.group.Prepend(ctx.stranger).Where(p => p != null).ToList();
            if (R.waits)
            {
                foreach (var p in everyone) Arrivals.Hold(p);
                ctx.xWaiting = true;
                return true;
            }
            var spot = GatheringSpot(map);
            float hours = ext.stayHours.RandomInRange;
            var mine = everyone.Where(p => p.Faction == ctx.faction).ToList();
            var theirs = everyone.Where(p => p.Faction != ctx.faction).ToList();
            if (Arrivals.Visit(mine, map, ctx.faction, spot, hours) == null) return false;
            if (theirs.Count > 0 && ctx.enemy != null) Arrivals.Visit(theirs, map, ctx.enemy, spot, hours);
            return true;
        }

        public override void Cleanup(HappeningContext ctx)
        {
            if (ctx.xWaiting)
                foreach (var p in ctx.group.Where(p => p != null && !p.Spawned && p.Faction != Faction.OfPlayer))
                    Arrivals.Release(p);
            base.Cleanup(ctx);
        }

        // ---------------- Text ----------------

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            HappeningWorker_Setting.AddLandTags(ctx.map, tags);
            var d = DeedOf(ctx);
            if (d != null)
            {
                tags.Add("deed:" + d.kind);
                switch (d.side)
                {
                    case DeedSide.Favor: tags.Add("grateful"); break;
                    case DeedSide.TheirFavor: tags.Add("owed"); break;
                    case DeedSide.Wrong: tags.Add("ourwrong"); break;
                    case DeedSide.TheirWrong: tags.Add("theirwrong"); break;
                }
                if (!d.name.NullOrEmpty()) tags.Add("named");
                if (MemoryMath.AgoOf(Now - d.tick, out _) >= MemoryMath.Ago.LastYear) tags.Add("longago");
            }
            if (!ctx.dead.NullOrEmpty()) tags.Add("killedours");
            if (R.want == "relief")
            {
                tags.Add(ctx.thing != null && ctx.thing.IsMedicine ? "medicine" : "food");
                string why = HardTime(ctx.map);
                if (why != null) tags.Add(why);
            }
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var r = R;
            if (r.needsDead != null && r.needsDead.Contains(option) && ctx.dead.NullOrEmpty()) return "CC_Memory_NoDead".Translate().Resolve();
            return null;
        }
    }

    // ===================== Effects =====================

    /// <summary>
    /// The people remember the answer: a new deed (<see cref="kind"/>, with the event's people, or the other people with
    /// <see cref="enemy"/>), and/or the deed the event was about settled (<see cref="settle"/>). The name a deed carries is
    /// the other people's name in a quarrel, else the visitor's, else nothing.
    /// </summary>
    public class DilemmaEffect_Remember : DilemmaEffect
    {
        public string kind;
        public int weight;
        public bool enemy;
        public bool settle;
        /// <summary>What the deed is about: x (the visitor's name), dead ({DEAD}), other (the other people), place, thing ({THING}), none.</summary>
        public string about = "auto";

        public override void Apply(DilemmaContext ctx)
        {
            if (chance < 1f && !Rand.Chance(chance)) return;
            var h = ctx as HappeningContext;
            if (settle)
            {
                var deed = HappeningWorker_Remembered.DeedOf(ctx);
                FactionMemory.Settle(deed);
                // A good turn come home moves the stories of those who met the envoy (Interplay/).
                Interplay.Notify_DeedSettled(ctx, deed);
            }
            if (kind.NullOrEmpty()) return;
            var f = enemy ? h?.enemy : ctx.faction;
            var other = enemy ? ctx.faction : h?.enemy;
            string name;
            switch (about)
            {
                case "x": name = ctx.stranger?.LabelShort; break;
                case "dead": name = h?.dead; break;
                case "other": name = other?.Name; break;
                case "place": name = h?.placeName; break;
                case "thing": name = ctx.thing?.label; break;
                case "none": name = null; break;
                default: name = other?.Name ?? ctx.stranger?.LabelShort; break;
            }
            FactionMemory.Note(f, kind, name.NullOrEmpty() ? null : name, weight);
        }
    }

    /// <summary>Things worth {COUNT} silver (times <see cref="factor"/>) of the colony's era, left beside the visitor (or where traders leave goods).</summary>
    public class DilemmaEffect_Gift : DilemmaEffect
    {
        public float factor = 1f;
        public bool fancy;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx.map == null || ctx.count <= 0) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var things = Loot.Make(ctx.count * factor, fancy);
            var near = ctx.stranger != null && ctx.stranger.Spawned && ctx.stranger.Map == ctx.map ? ctx.stranger.Position : DropCellFinder.TradeDropSpot(ctx.map);
            Loot.Deliver(things, ctx.map, near, false);
        }
    }

    /// <summary>The event's goods ({THING}, {COUNT} of them, times <see cref="factor"/>) left beside the visitor (or where traders leave goods).</summary>
    public class DilemmaEffect_Goods : DilemmaEffect
    {
        public float factor = 1f;

        public override void Apply(DilemmaContext ctx)
        {
            int n = Mathf.RoundToInt(ctx.count * factor);
            if (ctx.map == null || ctx.thing == null || n <= 0) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var near = ctx.stranger != null && ctx.stranger.Spawned && ctx.stranger.Map == ctx.map ? ctx.stranger.Position : IntVec3.Invalid;
            StorytellerEvents.Hand(ctx.thing, n, ctx.map, near);
        }
    }

    /// <summary>
    /// Peace with the event's people (an old enemy): their goodwill is lifted to <see cref="goodwill"/>, past the line
    /// where the game ends a war. Greyed out if they are no longer at war with the colony (it has happened another way).
    /// </summary>
    public class DilemmaEffect_MakePeace : DilemmaEffect
    {
        public int goodwill = 5;

        public override string Unavailable(DilemmaContext ctx)
        {
            var f = ctx.faction;
            return f != null && !HappeningWorker_Remembered.CanMakePeace(f) ? "CC_Memory_NotAtWar".Translate(f.Name).Resolve() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var f = ctx.faction;
            if (f == null || !HappeningWorker_Remembered.CanMakePeace(f)) return;
            int change = Mathf.Max(1, Mathf.Max(1, goodwill) - f.PlayerGoodwill);
            f.TryAffectGoodwillWith(Faction.OfPlayer, change, true, false);
        }
    }
}
