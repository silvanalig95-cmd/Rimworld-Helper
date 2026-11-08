using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of a people whose age changed (Languages/English/Keyed/Rimfolk_PeopleAges.xml): the letter, one for each
    /// change, opening with the colony's means of hearing it (Ages.Form, the founding's word: a traveler, a rider's news,
    /// the radio, a comms relay), then what they learned or lost (by the age), their head's words in their own voice (by
    /// their calling), what of it was the colony's doing or why they fell back, and what the colony will see of it (their
    /// people at the gate and their traders' goods); the history's line (world:age); the line on their card and on the
    /// region's page of peoples: their age and which way they are heading, and why.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PeopleAgeWords
    {
        /// <summary>The history's kind for a people's change of age (the world family).</summary>
        public const string Kind = "world:age";

        static PeopleAgeWords()
        {
            CardSections.RegisterLines(CardKind.People, CardSlot.Now, 13f, r => CardLines(ThingRefs.Faction(r)), null, "people-age");
        }

        private static string T(string key, params NamedArgument[] args) => TextVariants.Vary(key).Translate(args).Resolve();

        /// <summary>An age in a word: tribal, medieval, industrial, spacer.</summary>
        public static string AgeWord(int age) => ("CC_PeopleAge_Age_" + Math.Max(0, Math.Min(3, age))).Translate().Resolve();

        /// <summary>The town of theirs the letter looks at: the nearest a home.</summary>
        private static Settlement TownOf(Faction f, out Map home)
        {
            home = null;
            Settlement best = null;
            float bestTiles = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s.Faction != f) continue;
                var h = RegionTowns.NearestHome(s.Tile, out float tiles);
                if (best == null || h != null && tiles < bestTiles)
                {
                    best = s;
                    home = h;
                    bestTiles = h != null ? tiles : float.MaxValue;
                }
            }
            return best;
        }

        // ---------------- The letter ----------------

        /// <summary>A people's change of age, told once (a positive letter for a rise, a neutral one for a fall), and the history's line.</summary>
        public static void Tell(Faction f, PeopleAgeRecord r, List<string> why)
        {
            if (f == null || r == null || r.was < 0) return;
            bool rise = r.age > r.was;
            var town = TownOf(f, out var home);
            home ??= Homes.Seat;
            var form = Ages.Form(AgeFeature.Founding, home, f);
            string calling = Peoples.CallingOf(f) ?? "";
            var a = new[]
            {
                form.word.CapitalizeFirst().Named("MEANS"), f.Name.Named("PEOPLE"), AgeWord(r.age).Named("AGE"), AgeWord(r.was).Named("WAS"),
                (f.LeaderTitle ?? Peoples.Of(f)?.head ?? "").Named("LEADER")
            };
            var parts = new List<string> { T((rise ? "CC_PeopleAge_Rise_" : "CC_PeopleAge_Fall_") + r.age, a) };
            string voice = "CC_PeopleAge_Voice_" + calling + (rise ? "_Rise" : "_Fall");
            if (voice.CanTranslate()) parts.Add(T(voice, a));
            string because = Because(why, rise);
            if (because != null) parts.Add(T("CC_PeopleAge_Letter_" + because, a));
            string text = string.Join(" ", parts) + "\n\n" + T(rise ? "CC_PeopleAge_Seen_Rise" : "CC_PeopleAge_Seen_Fall", a);
            string title = T(rise ? "CC_PeopleAge_RiseTitle" : "CC_PeopleAge_FallTitle", a);
            var look = town != null ? new LookTargets(new GlobalTargetInfo(town)) : LookTargets.Invalid;
            global::ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Important, home, title, text, rise ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent, look, f,
                "age|" + f.loadID + "|" + r.age);
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(f), HistoryRole.Did) };
            global::ColonyCouncil.History.Add(Kind, actors, town != null ? Refs.Of(town) : Ref.None, HistoryMath.Milestone,
                T(rise ? "CC_PeopleAge_H_Rise" : "CC_PeopleAge_H_Fall", a), values: new List<string> { f.Name, AgeWord(r.age) });
        }

        /// <summary>What of a rise was the colony's doing (its largest part), or why they fell back (the largest sign); null for the years alone.</summary>
        private static string Because(List<string> why, bool rise)
        {
            if (why == null) return null;
            var mine = rise ? new[] { "trade", "gifts", "teaching", "ally", "pact" } : new[] { "alone", "burned", "war", "unsupplied" };
            return why.FirstOrDefault(mine.Contains);
        }

        // ---------------- Their card and the region's page ----------------

        /// <summary>
        /// Their age and which way it is heading, with why ("medieval age, rising: trades with you, learns from you"), or
        /// null for a people not of Rimfolk's or with the setting off.
        /// </summary>
        public static string Heading(Faction f)
        {
            if (f == null || !PeopleAges.On || !Peoples.IsOurs(f)) return null;
            try
            {
                var r = PeopleAgeBook.Instance?.Of(f);
                int age = Peoples.AgeOf(f);
                if (r == null) return T("CC_PeopleAge_Heading_Unseen", AgeWord(age).Named("AGE"));
                var why = r.Why.Take(3).Select(k => ("CC_PeopleAge_Why_" + k).Translate().Resolve()).ToList();
                string reasons = TextUtil.JoinAnd(why);
                string key = r.heading > 0 ? r.blocked ? "Holding" : "Rising" : r.heading < 0 ? r.blocked ? "Holding" : "Falling" : "Settled";
                return T("CC_PeopleAge_Heading_" + key, AgeWord(age).Named("AGE"), reasons.Named("WHY"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' ages: the heading", e);
                return null;
            }
        }

        /// <summary>On their card: the heading, and when they last changed age.</summary>
        public static List<string> CardLines(Faction f)
        {
            string head = Heading(f);
            if (head == null) return null;
            var lines = new List<string> { head.CapitalizeFirst() + "." };
            var r = PeopleAgeBook.Instance?.Of(f);
            if (r != null && r.changed >= 0 && r.was >= 0)
                lines.Add(T(r.age > r.was ? "CC_PeopleAge_Card_Rose" : "CC_PeopleAge_Card_Fell", AgeWord(r.age).Named("AGE"), FactionMemory.AgoText(r.changed).Named("AGO")));
            return lines;
        }
    }
}
