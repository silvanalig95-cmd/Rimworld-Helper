using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A god speaks in Rimfolk's dreams (docs/design/gods.md, "Worship": dreams and visions; brainstorm 10). Of the dreams
    /// someone has on waking (Dreams/Dreams.cs), a share are their god's (WorshipMath.GodDreamShare: twice for the priest,
    /// half again for a psychic gift or a mage). What the god says:
    /// <list type="bullet">
    /// <item>A warning, only of something real now: a beast sacred to it marked for the hunt, a sickness among them, its
    /// own anger with the colony.</item>
    /// <item>A demand: an offering of a kind at its place within three days. The offerings' carrying brings it (the dreamer
    /// first); met, the god is pleased, let go, displeased.</item>
    /// <item>A name: a god of the world the colony didn't know.</item>
    /// <item>A vision, for the priest or a psychic: the god's ties and what pleases it.</item>
    /// </list>
    /// The priest and the psychic see more clearly: the god by name, plainly; for anyone else a figure they take to be it.
    /// Prayed for, the lost come back for a night (an answer to a prayer for the dead). The dream's words go in their journal,
    /// the news and a dream mood that gives something besides mood (awe).
    /// </summary>
    public static class GodDreams
    {
        /// <summary>From the dreams' hour (DreamTracker.Hourly), before a dream is chosen: maybe it is the god's. True when it was.</summary>
        public static bool TryDream(CouncilGameComponent comp, Pawn p)
        {
            if (!Worship.On || p == null || comp == null) return false;
            try
            {
                var b = Worship.Book;
                if (b == null) return false;
                // A prayer for the dead answered: they come back for a night.
                if (lostQueued.TryGetValue(p.thingIDNumber, out var lost))
                {
                    lostQueued.Remove(p.thingIDNumber);
                    var lg = Gods.ById(lost.god);
                    if (lg != null) return Have(comp, p, lg, "lost", null, lost.name);
                }
                var g = Worship.GodOf(p);
                bool priest = Worship.IsPriest(p);
                bool psychic = Worship.Psychic(p);
                float share = WorshipMath.GodDreamShare(g != null, priest, psychic, Worship.Devout(p), Worship.Scoffer(p)) * (GodWorshipDef.For(g)?.dreamFactor ?? 1f);
                if (!Rand.Chance(share)) return false;
                var map = p.MapHeld;
                string danger = Danger(p, g, map, out var about);
                bool canDemand = map != null && GodWorshipDef.TakesOfferings(g) && !(GodWorshipDef.For(g)?.asksNothing ?? false) && Shrines.Of(map, g).Count > 0
                                 && !b.demands.Any(d => d != null && !d.done && d.god == g.id && d.home == Gods.HomeKey(map));
                var unknown = Gods.All.Where(x => !x.known && x.kind != GodKind.Dead).ToList();
                var kind = WorshipMath.PickDream(danger != null, canDemand, unknown.Count > 0, priest || psychic, Rand.Value);
                switch (kind)
                {
                    case GodDreamKind.Warning:
                        return Have(comp, p, g, "warning:" + danger, about, null);
                    case GodDreamKind.Demand:
                        return Demand(comp, b, p, g, map);
                    case GodDreamKind.Name:
                    {
                        var other = unknown.RandomElement();
                        bool had = Have(comp, p, other, "name", null, null);
                        if (had) Gods.Learn(other, "dream");
                        return had;
                    }
                    default:
                        return Have(comp, p, g, "vision", null, null);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a god's dream", e);
                return false;
            }
        }

        /// <summary>A real danger the god might warn of now, as a tag ("beast", "sickness", "anger"), with who or what it is about; null when there's none.</summary>
        private static string Danger(Pawn p, God g, Map map, out string about)
        {
            about = null;
            if (g == null || map == null) return null;
            var def = GodDomainDef.Of(g.domain);
            if (def?.Animals != null && def.Animals.Count > 0)
                foreach (var des in map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt))
                    if (des.target.Thing is Pawn beast && def.Animals.Contains(beast.def))
                    {
                        about = beast.LabelShort;
                        return "beast";
                    }
            var sick = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => Prayers.Grave(x) != null);
            if (sick != null)
            {
                about = sick.LabelShort;
                return "sickness";
            }
            if (Gods.Favour(g, map) <= FavourWord.Displeased) return "anger";
            return null;
        }

        private static bool Demand(CouncilGameComponent comp, WorshipBook b, Pawn p, God g, Map map)
        {
            var likes = GodWorshipDef.Likes(g);
            if (likes.Length == 0) return Have(comp, p, g, "vision", null, null);
            var kind = likes[Rand.Range(0, likes.Length)];
            var shrine = Shrines.Of(map, g).Select(s => s.thing).FirstOrDefault();
            if (shrine == null) return false;
            string what = ("CC_Wsh_Kind_" + kind).Translate();
            if (!Have(comp, p, g, "demand", null, what)) return false;
            b.demands.Add(new DemandRecord
            {
                god = g.id, shrine = shrine, kind = kind, dreamer = p, home = Gods.HomeKey(map), until = Worship.Now + WorshipMath.DemandDays * GenDate.TicksPerDay
            });
            while (b.demands.Count > WorshipBook.MaxDemands) b.demands.RemoveAt(0);
            // The offerings' carrying brings it: planned now for the god's place.
            if (!b.offerings.Any(o => o != null && o.shrine == shrine && o.fate == OfferingFate.Waiting))
                b.offerings.Add(new OfferingRecord { shrine = shrine, god = g.id, home = Gods.HomeKey(map), chosen = Worship.Now, demand = true });
            return true;
        }

        /// <summary>The dream had: its words (the god's pool, {GOD} its name, {NAME} the one it's about), a dream mood carrying them, the journal, the news.</summary>
        private static bool Have(CouncilGameComponent comp, Pawn p, God g, string what, string about, string name)
        {
            if (g == null) return false;
            bool clear = Worship.IsPriest(p) || Worship.Psychic(p);
            string kind = what.Contains(":") ? what.Substring(0, what.IndexOf(':')) : what;
            var tags = new List<string> { "dream:" + kind, clear ? "clear" : "veiled" };
            if (what.StartsWith("warning:")) tags.Add("danger:" + what.Substring(8));
            string text = Worship.LineWith("Rimfolk_Pool_Worship_Dream", p, g, null, new[] { (about ?? "").Named("SUBJECT"), (name ?? "").Named("NAME") }, tags.ToArray());
            if (text.NullOrEmpty()) return false;
            GiveDream(p, text);
            comp.journals.WriteText(p, text);
            comp.dreams.recent.Add(new DreamRecord { pawn = p, name = p.LabelShort, text = text, kind = DreamKind.Strange, tick = Worship.Now });
            while (comp.dreams.recent.Count > 80) comp.dreams.recent.RemoveAt(0);
            string message = "CC_Wsh_DreamMessage".Translate(p.Named("PAWN"), text.Named("DREAM"), Worship.CardName(g).Named("GOD")).Resolve();
            News.Post(message, p, NewsKind.People, MessageTypeDefOf.NeutralEvent, kind == "warning" || kind == "demand" ? Importance.Notable : Importance.Flavor);
            Worship.Witness(g, p.MapHeld, GodActKind.Omen, message, p.PositionHeld);
            return true;
        }

        private static void GiveDream(Pawn p, string text)
        {
            var td = WorshipDefOf.Rimfolk_Dream_God;
            var memories = p.needs?.mood?.thoughts?.memories;
            if (td == null || memories == null || !ColonyCouncilMod.Settings.MoodEffects) return;
            foreach (var d in DefDatabase<ThoughtDef>.AllDefsListForReading.Where(x => x.ThoughtClass == typeof(Thought_Dream)).ToList()) memories.RemoveMemoriesOfDef(d);
            if (!(ThoughtMaker.MakeThought(td) is Thought_Dream m)) return;
            m.dream = text;
            memories.TryGainMemory(m, null);
        }

        // ================= Demands =================

        /// <summary>An offering that answered a demand was taken: the demand is met, the god pleased.</summary>
        public static void Met(WorshipBook b, God g, Map map, OfferingRecord o)
        {
            var d = b.demands.FirstOrDefault(x => x != null && !x.done && !x.met && x.god == g.id && x.home == Gods.HomeKey(map));
            if (d == null) return;
            d.met = true;
            d.done = true;
            Worship.Favour(g, map, WorshipMath.DemandMet, "CC_Wsh_Why_DemandMet".Translate((o.label ?? "").Named("WHAT")).Resolve(), FavourSource.Offering);
        }

        /// <summary>The region's day: a demand let go past its days displeases the god.</summary>
        public static void Daily(WorshipBook b, int now)
        {
            foreach (var d in b.demands.Where(x => x != null && !x.done && now > x.until).ToList())
            {
                d.done = true;
                var g = Gods.ById(d.god);
                var map = Homes.MapOf(d.home);
                if (g == null) continue;
                string text = "CC_Wsh_DemandIgnored".Translate(Worship.CardName(g).Named("GOD"), ("CC_Wsh_Kind_" + d.kind).Translate().Named("WHAT")).Resolve();
                Worship.Favour(g, map, WorshipMath.DemandIgnored, text, FavourSource.Neglect, GodOffence.Neglect);
                Tell.Minor(NewsKind.Colony, Importance.Notable, map, text, d.shrine != null && d.shrine.Spawned ? new LookTargets(d.shrine) : null);
            }
        }

        // ================= The lost, for a night =================

        private static readonly Dictionary<int, (int god, string name)> lostQueued = new Dictionary<int, (int, string)>();

        /// <summary>A prayer for the dead answered: their next dream is of the one lost, at peace (session memory: a load forgets it).</summary>
        public static void QueueLost(Pawn p, God g, string name)
        {
            if (p == null || g == null) return;
            if (lostQueued.Count > 40) lostQueued.Clear();
            lostQueued[p.thingIDNumber] = (g.id, name);
        }

        /// <summary>Dev: a god's dream for someone now.</summary>
        public static bool DevDream(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            var g = Worship.GodOf(p);
            if (comp == null || g == null) return false;
            var map = p.MapHeld;
            string danger = Danger(p, g, map, out var about);
            if (danger != null) return Have(comp, p, g, "warning:" + danger, about, null);
            if (map != null && Shrines.Of(map, g).Count > 0 && GodWorshipDef.TakesOfferings(g) && !(GodWorshipDef.For(g)?.asksNothing ?? false)) return Demand(comp, Worship.Book, p, g, map);
            return Have(comp, p, g, "vision", null, null);
        }
    }
}
