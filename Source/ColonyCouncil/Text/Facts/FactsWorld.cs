using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The providers of the world: wars and peace, the world's events, the conditions over the land.
    public static partial class Facts
    {
        // ---------------- Wars and peace ----------------

        /// <summary>A people whose wars count: another people of the world, seen, standing, of people (not one at war with everyone).</summary>
        public static bool Counts(Faction g) =>
            g != null && !g.IsPlayer && !g.Hidden && !g.defeated && !g.temporary && g.def != null && g.def.humanlikeFaction && !g.def.permanentEnemy;

        private static void Wars(FactSet f, Faction p)
        {
            var book = OutsiderBook.Instance;
            if (p == null) return;
            int now = Now;
            // Their war: the newest of theirs still on (the game's own relations), with how it has gone since it began.
            WarSeen theirs = null;
            Faction foe = null;
            foreach (var g in Find.FactionManager.AllFactionsListForReading)
            {
                if (!Counts(g) || g == p || !p.HostileTo(g)) continue;
                var w = book?.wars.FirstOrDefault(x => x.end < 0 && x.Of(p) && x.Of(g));
                if (foe == null || w != null && (theirs == null || w.start > theirs.start))
                {
                    foe = g;
                    theirs = w;
                }
            }
            if (foe != null)
            {
                bool fresh = theirs != null && theirs.start >= 0 && Days(theirs.start) <= 20f;
                f.Tag("their:war", fresh ? Fresh(Days(theirs.start), 20f, 2.5f) : 1f);
                f.Value("WAR", foe.Name, "war:" + foe.loadID);
                if (fresh) f.Tag("war:new", Fresh(Days(theirs.start), 20f, 2.5f));
                else if (theirs == null || theirs.start < 0 || Days(theirs.start) >= 60f) f.Tag("war:long");
                if (theirs != null)
                {
                    int ours = Towns(p), them = Towns(foe);
                    int oursThen = theirs.a == p ? theirs.townsA : theirs.townsB, themThen = theirs.a == p ? theirs.townsB : theirs.townsA;
                    if (ours < oursThen) f.Tag("war:losing", 2f);
                    else if (them < themThen) f.Tag("war:winning", 2f);
                    if (theirs.fought >= 0 && Days(theirs.fought) <= 15f) f.Tag("war:fought", Fresh(Days(theirs.fought), 15f, 3f));
                }
                if (foe.HostileTo(Faction.OfPlayer)) f.Tag("war:ourfoe");
                else if (foe.HasGoodwill && foe.PlayerGoodwill >= 25) f.Tag("war:ourfriend", 1f);
            }
            if (book == null) return;
            // A war of theirs that ended lately.
            var peace = book.wars.Where(w => w.end >= 0 && w.Of(p) && Days(w.end) <= 30f && Counts(w.Other(p))).OrderByDescending(w => w.end).FirstOrDefault();
            if (peace != null)
            {
                f.Tag("their:peace", Fresh(Days(peace.end), 30f, 2.5f));
                f.Value("PEACEWITH", peace.Other(p).Name, "peace:" + peace.Other(p).loadID);
            }
            // Two other peoples who made peace lately (not one beaten: both still stand).
            var calm = book.wars.Where(w => w.end >= 0 && !w.Of(p) && Days(w.end) <= 30f && Counts(w.a) && Counts(w.b) && !w.a.HostileTo(w.b))
                .OrderByDescending(w => w.end).FirstOrDefault();
            if (calm != null)
            {
                f.Tag("world:peace", Fresh(Days(calm.end), 30f, 1.5f));
                f.Value("PEACEA", calm.a.Name);
                f.Value("PEACEB", calm.b.Name);
            }
            // A war between two other peoples, new this season.
            var other = book.wars.Where(w => w.end < 0 && !w.Of(p) && w.start >= 0 && Days(w.start) <= 30f && Counts(w.a) && Counts(w.b)).OrderByDescending(w => w.start).FirstOrDefault();
            if (other != null)
            {
                f.Tag("world:war", Fresh(Days(other.start), 30f, 1.5f));
                f.Value("WARA", other.a.Name);
                f.Value("WARB", other.b.Name);
            }
            // Two peoples' fighters met on the colony's land.
            var battle = book.wars.Where(w => w.fought >= 0 && Days(w.fought) <= 10f && w.a != null && w.b != null).OrderByDescending(w => w.fought).FirstOrDefault();
            if (battle != null)
            {
                f.Tag("battle:here", Fresh(Days(battle.fought), 10f, 2.5f));
                f.Value("BATTLEA", battle.a.Name);
                f.Value("BATTLEB", battle.b.Name);
                if (battle.Of(p)) f.Tag("battle:theirs", 2.5f);
            }
        }

        /// <summary>How many towns a people has standing now.</summary>
        public static int Towns(Faction g)
        {
            int n = 0;
            foreach (var s in Find.WorldObjects.Settlements)
                if (s.Faction == g && !s.Destroyed)
                    n++;
            return n;
        }

        // ---------------- The world's events ----------------

        /// <summary>
        /// A world event's kind, from its name: sickness, hunger, cold, war, trade, faith, politics, disaster, empire,
        /// beasts, or null.
        /// </summary>
        public static string Kind(string defName)
        {
            string n = defName ?? "";
            foreach (var (kind, words) in Kinds)
                foreach (var w in words)
                    if (n.Contains(w))
                        return kind;
            return null;
        }

        /// <summary>The kinds, in the order they're tried, and the words in a world event's name that make one.</summary>
        private static readonly (string kind, string[] words)[] Kinds =
        {
            ("sickness", new[] { "Plague", "Murrain", "Fever", "Cough", "Sick", "Pox" }),
            ("hunger", new[] { "Famine", "Hunger", "Locust", "Drought", "Blight" }),
            ("cold", new[] { "HardWinter", "IceRoad", "Cold", "Freeze" }),
            ("empire", new[] { "Imperial", "Levy", "Lords" }),
            ("war", new[] { "War", "Feud", "Mercenar", "Bandit", "Claimant", "Deserter", "Raid" }),
            ("trade", new[] { "Trade", "Fair", "Market", "Rush", "Road", "Toll", "Embargo", "Scarce", "Lanes", "Waystation", "Crossroads" }),
            ("faith", new[] { "Pilgrim", "Holy", "Prophet", "Blessed" }),
            ("politics", new[] { "Succession", "Votes", "Peace", "Moot", "Assize", "NewTown" }),
            ("disaster", new[] { "Flood", "Fire", "Comet", "ShipDown" }),
            ("beasts", new[] { "Herd", "Beast", "Migration", "Drovers" })
        };

        private static void Situations(FactSet f, Faction p)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return;
            WorldSituation best = null;
            int bestRank = -1;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || s.ctx == null) continue;
                int rank = s.ctx.faction == p && p != null ? 3 : s.ctx.enemy == p && p != null ? 2 : s.ctx.faction == null ? 1 : 0;
                if (rank <= 0) continue;
                if (rank > bestRank || rank == bestRank && best != null && s.startTick > best.startTick)
                {
                    best = s;
                    bestRank = rank;
                }
            }
            if (best != null)
            {
                float days = best.DaysIn;
                float h = bestRank >= 2 ? Fresh(days, 12f, 3f) : Fresh(days, 12f, 1.8f);
                f.Tag("situation", h);
                f.Value("SITUATION", Texts.WithThe(best.ctx.name, best.def.label), "ws:" + best.id);
                if (!best.ctx.placeName.NullOrEmpty()) f.Value("SITUATIONPLACE", best.ctx.placeName);
                if (bestRank >= 2) f.Tag("situation:theirs", h);
                f.Tag(best.def.bad ? "situation:bad" : "situation:good");
                if (days <= 3f) f.Tag("situation:new", h);
                else if (days >= 10f) f.Tag("situation:long");
                string kind = Kind(best.def.defName);
                if (kind != null) f.Tag("situation:" + kind, h);
                f.Tag("ws:" + best.def.defName, h);
                if (best.help > 0) f.Tag("situation:helped", 1.5f);
            }
            // One just over, theirs or the region's.
            var ended = ws.history.Where(r => r != null && r.endTick > 0 && Days(r.endTick) <= 15f && (r.faction == null || r.faction == p)).OrderByDescending(r => r.endTick).FirstOrDefault();
            if (ended != null)
            {
                float h = Fresh(Days(ended.endTick), 15f, ended.faction == p ? 2.5f : 1.5f);
                f.Tag("situation:ended", h);
                f.Value("ENDED", Texts.WithThe(ended.name, DefDatabase<WorldSituationDef>.GetNamedSilentFail(ended.defName ?? "")?.label ?? ended.label));
                string kind = Kind(ended.defName);
                if (kind != null) f.Tag("ended:" + kind, h);
                // Which one it was, and whether it was a hard time: a fair, a singer or a comet isn't "over, at last".
                if (!ended.defName.NullOrEmpty())
                {
                    f.Tag("ended:ws:" + ended.defName, h);
                    if (DefDatabase<WorldSituationDef>.GetNamedSilentFail(ended.defName)?.bad == true) f.Tag("ended:bad", h);
                }
                if (ended.faction == p && p != null) f.Tag("ended:theirs", h);
                if (ended.helped) f.Tag("ended:helped", 2f);
            }
        }

        private static void Conditions(FactSet f, Map map)
        {
            GameCondition best = null;
            foreach (var c in (map?.gameConditionManager?.ActiveConditions ?? new List<GameCondition>()).Concat(Find.World?.gameConditionManager?.ActiveConditions ?? new List<GameCondition>()))
                if (c?.def != null && TalkHooks.IsDisaster(c.def) && (best == null || c.startTick > best.startTick))
                    best = c;
            if (best == null) return;
            f.Tag("condition", 2f);
            f.Tag("condition:" + best.def.defName, 2f);
            f.Value("CONDITION", Find.ActiveLanguageWorker.WithDefiniteArticle(best.Label));
        }
    }
}
