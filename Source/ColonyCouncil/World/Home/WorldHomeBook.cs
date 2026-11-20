using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A colonist's vow of a vigil day each week while a holy year lasts (Rimfolk_Dlm_WS_Vigil): who, for which world event, and when the next is due.</summary>
    public class VigilVow : IExposable
    {
        public Pawn pawn;
        public Map map;
        /// <summary>The holy year's world event (its id), and the god the vow is to (-1: the colonist's own faith).</summary>
        public int situation = -1, god = -1;
        /// <summary>The game day the next vigil is due, and how many have been kept.</summary>
        public int nextDay;
        public int kept;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref situation, "situation", -1);
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref nextDay, "nextDay");
            Scribe_Values.Look(ref kept, "kept");
        }
    }

    /// <summary>A piece of clothing dyed in mourning colours for the neighbours' dead (Rimfolk_Dlm_WS_Mourning), and its colour before, to put back when the mourning ends.</summary>
    public class MourningDye : IExposable
    {
        public Thing apparel;
        public int situation = -1;
        /// <summary>It had a colour of its own before (dyed), and which.</summary>
        public bool had;
        public float r, g, b, a = 1f;

        public void ExposeData()
        {
            Scribe_References.Look(ref apparel, "apparel");
            Scribe_Values.Look(ref situation, "situation", -1);
            Scribe_Values.Look(ref had, "had");
            Scribe_Values.Look(ref r, "r");
            Scribe_Values.Look(ref g, "g");
            Scribe_Values.Look(ref b, "b");
            Scribe_Values.Look(ref a, "a", 1f);
        }
    }

    /// <summary>
    /// What the world brought home keeps with the game (World/Home/): the vows of a vigil day each week while a holy year lasts,
    /// and the clothes dyed in mourning colours while the neighbours mourn. The rest lives on the world events' own flags
    /// (kept apart, a side backed, a great one taken for a god's doing). Its hour is the world events' (WorldHome.Hourly).
    /// </summary>
    public class WorldHomeBook : GameComponent
    {
        public List<VigilVow> vows = new List<VigilVow>();
        public List<MourningDye> dyed = new List<MourningDye>();

        public const int MaxVows = 6;

        public WorldHomeBook(Game game)
        {
        }

        private static WorldHomeBook cached;
        private static Game cachedGame;

        public static WorldHomeBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<WorldHomeBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        private static bool Running(int id) => WorldSeason.Running.Any(s => s.id == id);

        // ================= The vigil days =================

        /// <summary>A vow taken: a vigil day each week from tomorrow, for as long as the holy year lasts.</summary>
        public void Vow(Pawn p, Map map, int situation, int god)
        {
            if (p == null || map == null) return;
            vows.RemoveAll(v => v == null || v.pawn == p);
            vows.Add(new VigilVow { pawn = p, map = map, situation = situation, god = god, nextDay = Day + WorldHomeMath.FirstVigilDays });
            while (vows.Count > MaxVows) vows.RemoveAt(0);
        }

        private static int Day => Find.TickManager.TicksGame / GenDate.TicksPerDay;

        public void Hour(int now)
        {
            foreach (var v in vows.ToList())
            {
                if (v?.pawn == null || v.pawn.Dead || v.pawn.Faction != Faction.OfPlayer || v.map == null || !Find.Maps.Contains(v.map))
                {
                    vows.Remove(v);
                    continue;
                }
                if (!Running(v.situation))
                {
                    vows.Remove(v);
                    WorldQuestions.VowDone(v);
                    continue;
                }
                if (Day < v.nextDay) continue;
                int hour = GenLocalDate.HourOfDay(v.map);
                if (hour < WorldHomeMath.VigilFrom || hour > WorldHomeMath.VigilTo) continue;
                if (!v.pawn.Spawned || v.pawn.Map != v.map || v.pawn.Downed || v.pawn.InMentalState || v.pawn.Drafted || MapFacts.ThreatOn(v.map))
                {
                    // Not today: the next day's morning.
                    if (hour >= WorldHomeMath.VigilTo) v.nextDay = Day + 1;
                    continue;
                }
                // Set going: the next a week on. Not now (nobody free, no room in the day): tried again next hour, else tomorrow.
                if (WorldQuestions.VigilDay(v))
                {
                    v.kept++;
                    v.nextDay = WorldHomeMath.NextVigilDay(Day);
                }
                else if (hour >= WorldHomeMath.VigilTo) v.nextDay = Day + 1;
            }
            foreach (var d in dyed.ToList())
            {
                if (d == null || d.apparel == null || d.apparel.Destroyed)
                {
                    dyed.Remove(d);
                    continue;
                }
                if (Running(d.situation)) continue;
                dyed.Remove(d);
                WorldQuestions.Undye(d);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref vows, "vows", LookMode.Deep);
            Scribe_Collections.Look(ref dyed, "dyed", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                vows ??= new List<VigilVow>();
                vows.RemoveAll(v => v?.pawn == null);
                dyed ??= new List<MourningDye>();
                dyed.RemoveAll(d => d?.apparel == null);
            }
        }
    }
}
