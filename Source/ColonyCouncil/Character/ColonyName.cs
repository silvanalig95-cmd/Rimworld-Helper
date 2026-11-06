using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's one name (phase 4 of the review, part 1: the player's decision 6 of 6 November 2026): one "known for"
    /// read from the four systems that each said what the colony is known as. Its ways (CharacterTracker: what the
    /// council's choices made it, merciful or thrifty), what its work made it (ColonyTraits: walled, learned), its renown
    /// among the peoples (Renown: how far its name carries and what it is known for there, with the deeds behind it) and
    /// what each people remembers (FactionMemory). Their data stays where every other system reads it; what the player sees
    /// is this one reading: one card (Council → Ways, a home's card where the colony is shown to others, the Overview's line,
    /// the Culture tab) and one chronicle sentence when the name changes (<see cref="Look"/>; a move of renown's band is
    /// renown's own sentence, with what it is known for, <see cref="Settle"/>). The rules are ColonyNameMath.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ColonyName
    {
        static ColonyName()
        {
            CardSections.HomeLines(CardSlot.Now, 40f, h => Lines(), "CC_Name_CardHead", "colonyname");
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        // ================= What it is called =================

        /// <summary>What the colony is called, as one list (its ways and what its work made it), or null with the colony's ways off.</summary>
        public static List<ColonyNameMath.Called> Called()
        {
            var ch = CouncilGameComponent.Instance?.character;
            if (ch == null || S == null || !S.colonyCharacter) return null;
            var items = new List<ColonyNameMath.Called>();
            foreach (var w in CharacterTracker.All.OrderByDescending(w => Mathf.Abs(ch.Value(w))))
            {
                int t = ch.Tier(w);
                if (t != 0) items.Add(new ColonyNameMath.Called(ColonyNameMath.WayId(w.ToString(), t > 0), Math.Abs(t), true));
            }
            foreach (var d in ch.traits.Known)
                items.Add(new ColonyNameMath.Called(ColonyNameMath.TraitId(d.defName), 1, false));
            return ColonyNameMath.Order(items);
        }

        /// <summary>The word for one thing it is called ("merciful", "walled"), or null for a trait no longer in the game.</summary>
        public static string Word(string id)
        {
            if (id == null) return null;
            if (id.StartsWith(ColonyNameMath.WayPrefix, StringComparison.Ordinal))
            {
                string rest = id.Substring(ColonyNameMath.WayPrefix.Length);
                if (rest.Length < 2) return null;
                bool positive = rest[rest.Length - 1] == '+';
                return Enum.TryParse(rest.Substring(0, rest.Length - 1), out Way w) ? CharacterTracker.PoleLabel(w, positive) : null;
            }
            if (id.StartsWith(ColonyNameMath.TraitPrefix, StringComparison.Ordinal))
                return DefDatabase<ColonyTraitDef>.GetNamedSilentFail(id.Substring(ColonyNameMath.TraitPrefix.Length))?.label;
            return null;
        }

        private static string Words(IEnumerable<string> ids)
        {
            var words = ids.Select(Word).Where(w => !w.NullOrEmpty()).ToList();
            return words.Count == 0 ? null : TextUtil.JoinAnd(words);
        }

        /// <summary>"merciful, generous and walled": what the colony is called, strongest first, or null for nothing yet.</summary>
        public static string CalledWords()
        {
            var called = Called();
            return called == null ? null : Words(called.Select(c => c.id));
        }

        // ================= Beyond its walls =================

        /// <summary>The colony's renown as it stands (null with it off), and the band last told (Unknown before the first look).</summary>
        private static RenownMath.Reading Renowned(out RenownBand told, out bool read)
        {
            var book = DiplomacyBook.Instance;
            read = Renown.On && book != null && book.renownBand != RenownMath.NotRead;
            told = book != null ? RenownMath.Told(book.renownBand) : RenownBand.Unknown;
            return Renown.On ? Renown.Read() : null;
        }

        /// <summary>What it is known for beyond its walls as the key keeps it: a KnownFor's name ("None" while its name carries nowhere), or null while renown is not read.</summary>
        private static string KnownForPart()
        {
            var r = Renowned(out var told, out bool read);
            if (!read || r == null) return null;
            return told >= RenownBand.Town ? r.known.ToString() : ColonyNameMath.NoKnownFor;
        }

        /// <summary>The colony's name as a key (ColonyNameMath.Key).</summary>
        public static string Key() => ColonyNameMath.Key(Called(), KnownForPart());

        // ================= The card =================

        /// <summary>
        /// The card's first line: what the colony is known for and how far its name carries, in one sentence ("People call the
        /// colony merciful and walled; beyond its walls it is known for good doctors, its name talked about in the next town.").
        /// </summary>
        public static string Main()
        {
            try
            {
                string called = CalledWords();
                var r = Renowned(out _, out _);
                string knownFor = r != null && r.band >= RenownBand.Town ? RenownWords.For(r.known, Ages.Ours) : null;
                string key = ColonyNameMath.CardKey(called != null, knownFor != null, r != null);
                string renown = r != null ? RenownWords.Band(r.band, Ages.Ours) : "";
                return key.Translate((called ?? "").Named("CALLED"), (knownFor ?? "").Named("KNOWNFOR"), renown.Named("RENOWN")).Resolve().CapitalizeFirst();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the colony's name: its line", e);
                return null;
            }
        }

        /// <summary>
        /// The colony's name on one card: what it is known for and how far its name carries; the deeds behind it (renown's,
        /// the council's choices behind its ways) and what is held against it; what each people remembers, a line each; the
        /// notables who know its people; what it does.
        /// </summary>
        public static List<string> Lines()
        {
            var lines = new List<string>();
            try
            {
                string main = Main();
                if (main != null) lines.Add(main);
                var r = Renowned(out _, out _);
                string from = RenownPanels.From(r);
                if (from != null) lines.Add(from);
                string choices = Choices();
                if (choices != null) lines.Add("CC_Name_Card_Ways".Translate(choices.Named("WHATS")).Resolve());
                string against = RenownPanels.Against(r);
                if (against != null) lines.Add(against);
                lines.AddRange(Remembered());
                lines.AddRange(RenownPanels.Reach(r));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the colony's name: its card", e);
            }
            return lines;
        }

        /// <summary>The council's latest choices behind the ways it is called (at most three, newest first), or null.</summary>
        private static string Choices()
        {
            var ch = CouncilGameComponent.Instance?.character;
            var called = Called();
            if (ch == null || called == null) return null;
            var poles = called.Where(c => c.way).Select(c => Word(c.id)).Where(w => !w.NullOrEmpty()).ToList();
            if (poles.Count == 0) return null;
            var whats = new List<string>();
            for (int i = ch.recent.Count - 1; i >= 0 && whats.Count < 3; i--)
            {
                var e = ch.recent[i];
                if (e?.shift == null || e.what.NullOrEmpty()) continue;
                if (poles.Any(w => Regex.IsMatch(e.shift, @"\b" + Regex.Escape(w) + @"\b"))) whats.Add(e.what.TrimEnd('.'));
            }
            return whats.Count == 0 ? null : string.Join("; ", whats);
        }

        /// <summary>At most this many peoples' memories on the card (the strongest first; the region's Peoples page has them all).</summary>
        public const int PeoplesShown = 4;

        /// <summary>What each people remembers of the colony, a line each: how it stands with them, and what it remembers best.</summary>
        private static List<string> Remembered()
        {
            var lines = new List<string>();
            var mem = FactionMemory.On ? FactionMemory.Instance : null;
            if (mem == null) return lines;
            int now = Find.TickManager.TicksGame;
            foreach (var f in mem.Peoples())
            {
                if (lines.Count >= PeoplesShown) break;
                var best = mem.Of(f).Where(d => d.StrengthAt(now) >= 1.5f).OrderByDescending(d => Mathf.Round(d.StrengthAt(now) / 3f)).ThenByDescending(d => d.tick).FirstOrDefault();
                if (best == null) continue;
                string tone = ("CC_Memory_Tone_" + mem.Tone(f)).Translate().Resolve();
                if (tone.Length > 0) tone = char.ToLowerInvariant(tone[0]) + tone.Substring(1);
                lines.Add("CC_Name_Card_People".Translate(f.Name.Named("PEOPLE"), tone.Named("TONE"), FactionMemoryPanel.LineText(best).Named("DEED")).Resolve());
            }
            return lines;
        }

        // ================= When it changes =================

        /// <summary>
        /// A look at the colony's name (after the ways move, at the end of the council's day): when it changed since the last
        /// look, one sentence in the chronicle, whatever moved it (a way, a trait, what it is known for beyond its walls). The
        /// first look (a new game, an older save) keeps the name as it is without a word.
        /// </summary>
        public static void Look(CouncilGameComponent comp)
        {
            var ch = comp?.character;
            if (ch == null) return;
            try
            {
                string now = Key();
                if (ch.nameKey == null)
                {
                    ch.nameKey = now;
                    return;
                }
                var change = ColonyNameMath.Diff(ch.nameKey, now);
                ch.nameKey = ColonyNameMath.Merge(ch.nameKey, now);
                if (change.Empty) return;
                string line = Sentence(change);
                if (!line.NullOrEmpty()) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, line, null));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the colony's name: a look", e);
            }
        }

        /// <summary>Renown told a move of its band in its own sentence (with what the colony is known for): kept without another word.</summary>
        public static void Settle()
        {
            var ch = CouncilGameComponent.Instance?.character;
            if (ch == null) return;
            try
            {
                string now = Key();
                ch.nameKey = ch.nameKey == null ? now : ColonyNameMath.Merge(ch.nameKey, ColonyNameMath.Key(null, KnownForPart()));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the colony's name: renown told", e);
            }
        }

        /// <summary>The one sentence for a change ("People have started calling the colony walled and they have stopped calling it restless.").</summary>
        public static string Sentence(ColonyNameMath.Change c)
        {
            var clauses = new List<string>();
            foreach (var (key, what) in ColonyNameMath.Clauses(c))
            {
                string words;
                switch (what)
                {
                    case "started": words = Words(c.started); break;
                    case "wider": words = Words(c.wider); break;
                    case "stopped": words = Words(c.stopped); break;
                    default:
                        words = Enum.TryParse(what, out KnownFor k) ? RenownWords.For(k, Ages.Ours) : null;
                        break;
                }
                if (words.NullOrEmpty()) continue;
                clauses.Add(key.Translate(words.Named("WORDS"), words.Named("KNOWNFOR")).Resolve());
            }
            if (clauses.Count == 0) return null;
            return "CC_Name_Changed_Line".Translate(TextUtil.JoinAnd(clauses).Named("CLAUSES")).Resolve().CapitalizeFirst();
        }
    }
}
