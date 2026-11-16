using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The reason every change Rimfolk makes in a people's goodwill gives the game (the player, 16 November 2026: "sometimes
    /// my relations with factions change and i have no idea why"). The game's message then names it ("Relations with
    /// Stormriders have changed from 53 to 45 (a letter that slighted their god).") and the Factions tab lists it among that
    /// people's recent events. Each is a HistoryEventDef of Rimfolk's own (Defs/HistoryEventDefs/Rimfolk_GoodwillReasons.xml,
    /// named in World/GoodwillReasonMath.cs) that no precept or other mod reads; the faction memory's hooks skip them, as the
    /// code that changes the goodwill notes its own deed. The wrappers take one each: WorldSituationWorker.Goodwill,
    /// StoryChains.Goodwill, Missions.Goodwill (by the task's outcome), Camp.LoseGoodwill and the diplomacy helpers.
    /// </summary>
    public static class GoodwillWhy
    {
        private static readonly Dictionary<string, HistoryEventDef> found = new Dictionary<string, HistoryEventDef>();

        /// <summary>The reason by its def name (null if the def is missing: the game's message then names none).</summary>
        public static HistoryEventDef Of(string defName)
        {
            if (defName == null) return null;
            if (found.TryGetValue(defName, out var d)) return d;
            d = DefDatabase<HistoryEventDef>.GetNamedSilentFail(defName);
            if (d != null) found[defName] = d;
            return d;
        }

        // ---------------- What the effects of an answer, an event or a meeting give ----------------

        private static HistoryEventDef current;

        /// <summary>
        /// The reason the effects run now give for their goodwill (DilemmaEffect_Goodwill and the events' goodwill effects):
        /// set around a meeting's effects (Strangers/Encounters.cs) and an event's own (as it happens, or a story's ending);
        /// an answer the council chose has none set and gives <see cref="Council"/>.
        /// </summary>
        public static HistoryEventDef ForEffects => current ?? Council;

        /// <summary>Effects run inside this give <paramref name="why"/> for their goodwill, until it is disposed.</summary>
        public static Scope During(HistoryEventDef why) => new Scope(why);

        public readonly struct Scope : IDisposable
        {
            private readonly HistoryEventDef was;

            public Scope(HistoryEventDef why)
            {
                was = current;
                current = why;
            }

            public void Dispose() => current = was;
        }

        // ---------------- The reasons ----------------

        public static HistoryEventDef Slight => Of(GoodwillReasonMath.Slight);
        public static HistoryEventDef Letter => Of(GoodwillReasonMath.Letter);
        public static HistoryEventDef Word => Of(GoodwillReasonMath.Word);
        public static HistoryEventDef Name => Of(GoodwillReasonMath.Name);
        public static HistoryEventDef Council => Of(GoodwillReasonMath.Council);
        public static HistoryEventDef Decree => Of(GoodwillReasonMath.Decree);
        public static HistoryEventDef Meeting => Of(GoodwillReasonMath.Meeting);
        public static HistoryEventDef Welcome => Of(GoodwillReasonMath.Welcome);
        public static HistoryEventDef Event => Of(GoodwillReasonMath.Event);
        public static HistoryEventDef WorldEvent => Of(GoodwillReasonMath.WorldEvent);
        public static HistoryEventDef CampWell => Of(GoodwillReasonMath.CampWell);
        public static HistoryEventDef CampSour => Of(GoodwillReasonMath.CampSour);
        public static HistoryEventDef CampSent => Of(GoodwillReasonMath.CampSent);
        public static HistoryEventDef CampRefused => Of(GoodwillReasonMath.CampRefused);
        public static HistoryEventDef CampDefended => Of(GoodwillReasonMath.CampDefended);
        public static HistoryEventDef Brawl => Of(GoodwillReasonMath.Brawl);
        public static HistoryEventDef Bout => Of(GoodwillReasonMath.Bout);
        public static HistoryEventDef Challenge => Of(GoodwillReasonMath.Challenge);
        public static HistoryEventDef Joined => Of(GoodwillReasonMath.Joined);
        public static HistoryEventDef Gift => Of(GoodwillReasonMath.Gift);
        public static HistoryEventDef DealKept => Of(GoodwillReasonMath.DealKept);
        public static HistoryEventDef DealBroken => Of(GoodwillReasonMath.DealBroken);
        public static HistoryEventDef Unpaid => Of(GoodwillReasonMath.Unpaid);
        public static HistoryEventDef OrderFilled => Of(GoodwillReasonMath.OrderFilled);
        public static HistoryEventDef OrderMissed => Of(GoodwillReasonMath.OrderMissed);
        public static HistoryEventDef HardTimes => Of(GoodwillReasonMath.HardTimes);
        public static HistoryEventDef Friendship => Of(GoodwillReasonMath.Friendship);
        public static HistoryEventDef Together => Of(GoodwillReasonMath.Together);
        public static HistoryEventDef TheirWork => Of(GoodwillReasonMath.TheirWork);
        public static HistoryEventDef Helped => Of(GoodwillReasonMath.Helped);
        public static HistoryEventDef Paid => Of(GoodwillReasonMath.Paid);
        public static HistoryEventDef TurnedAway => Of(GoodwillReasonMath.TurnedAway);
        public static HistoryEventDef Amends => Of(GoodwillReasonMath.Amends);
        public static HistoryEventDef Ledger => Of(GoodwillReasonMath.Ledger);
        public static HistoryEventDef PromiseKept => Of(GoodwillReasonMath.PromiseKept);
        public static HistoryEventDef PromiseBroken => Of(GoodwillReasonMath.PromiseBroken);
        public static HistoryEventDef DutyDone => Of(GoodwillReasonMath.DutyDone);
        public static HistoryEventDef DutyRefused => Of(GoodwillReasonMath.DutyRefused);
        public static HistoryEventDef Returned => Of(GoodwillReasonMath.Returned);
        public static HistoryEventDef KeptTheirs => Of(GoodwillReasonMath.KeptTheirs);
        public static HistoryEventDef Deserter => Of(GoodwillReasonMath.Deserter);
        public static HistoryEventDef TheirDead => Of(GoodwillReasonMath.TheirDead);
        public static HistoryEventDef ShrineLost => Of(GoodwillReasonMath.ShrineLost);
        public static HistoryEventDef Harvest => Of(GoodwillReasonMath.Harvest);
        public static HistoryEventDef Caught => Of(GoodwillReasonMath.Caught);
        public static HistoryEventDef Smear => Of(GoodwillReasonMath.Smear);
        public static HistoryEventDef Tribute => Of(GoodwillReasonMath.Tribute);
        public static HistoryEventDef Judged => Of(GoodwillReasonMath.Judged);
        public static HistoryEventDef OathBroken => Of(GoodwillReasonMath.OathBroken);
        public static HistoryEventDef Levies => Of(GoodwillReasonMath.Levies);
        public static HistoryEventDef WardHome => Of(GoodwillReasonMath.WardHome);
        public static HistoryEventDef WardDied => Of(GoodwillReasonMath.WardDied);
        public static HistoryEventDef DebtRefused => Of(GoodwillReasonMath.DebtRefused);
        public static HistoryEventDef Truce => Of(GoodwillReasonMath.Truce);
        public static HistoryEventDef Peace => Of(GoodwillReasonMath.Peace);
        public static HistoryEventDef Beholden => Of(GoodwillReasonMath.Beholden);
        public static HistoryEventDef SharedGod => Of(GoodwillReasonMath.SharedGod);
        public static HistoryEventDef RivalGod => Of(GoodwillReasonMath.RivalGod);
        public static HistoryEventDef HolyWar => Of(GoodwillReasonMath.HolyWar);
        public static HistoryEventDef Relic => Of(GoodwillReasonMath.Relic);
        public static HistoryEventDef Feast => Of(GoodwillReasonMath.Feast);
        public static HistoryEventDef Faith => Of(GoodwillReasonMath.Faith);
        public static HistoryEventDef Sacred => Of(GoodwillReasonMath.Sacred);

        /// <summary>A task done for them, or one failed or let go, by the change it makes (Missions.Goodwill).</summary>
        public static HistoryEventDef Task(int amount) => Of(GoodwillReasonMath.Task(amount));
    }
}
