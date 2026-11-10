using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a named person beyond the walls is to their town.</summary>
    public enum NotableRole
    {
        /// <summary>The town's head: the people's own leader at their seat, else a headman, reeve, mayor or administrator.</summary>
        Head,
        /// <summary>The one likeliest to follow the head.</summary>
        Heir,
        /// <summary>Who the town sends to speak for it.</summary>
        Envoy,
        /// <summary>The town's known trader.</summary>
        Trader,
        /// <summary>Someone the colony met who went home: a prisoner set free, a camp's leader, a guest, a visiting trader.</summary>
        Acquaintance
    }

    /// <summary>What a letter (or a call) is for.</summary>
    public enum LetterPurpose
    {
        /// <summary>Keeping in touch.</summary>
        Greetings,
        /// <summary>Their people suffered a loss the colony knows of.</summary>
        Condolences,
        /// <summary>The colony knows of a threat to them.</summary>
        Warning,
        /// <summary>Asking what they know: the answer tells of real things near them.</summary>
        News,
        /// <summary>Asking them to visit the colony.</summary>
        Invitation,
        /// <summary>Asking for a small help (medicine, food), when they think well enough of the colony.</summary>
        Help,
        /// <summary>
        /// Calling in a debt of honour their people (or they) owe the colony, or answering one they called in
        /// (Diplomacy/DiplomacyLetters.cs: stage 2's letter purposes; Match, Ward, Ransom, Envoy and Fealty come after
        /// Pact and Truce in later releases).
        /// </summary>
        Favour,
        /// <summary>Proposing a pact, of defence or of trade, to a people at peace with the colony (Diplomacy/Pacts.cs).</summary>
        Pact,
        /// <summary>Offering a truce to a people at war with the colony, its term and its price (Diplomacy/Truces.cs).</summary>
        Truce,
        /// <summary>About captives (Diplomacy/Ransoms.cs, release 2a.3): one of theirs offered for ransom, or an exchange of theirs for ours.</summary>
        Ransom,
        /// <summary>Asking them to send their envoy to live at the colony for a season (Diplomacy/Envoys.cs, release 2a.3).</summary>
        Envoy,
        /// <summary>Asking them for a ward to foster for a year, or offering them a youth of the colony's (Diplomacy/Wards.cs, release 2b.3).</summary>
        Ward,
        /// <summary>Proposing a match between one of the colony's and one of theirs (Diplomacy/Matches.cs, release 2b.2).</summary>
        Match,
        /// <summary>
        /// Obsolete: the council's business, an officer's letter on a task or a secret pressed (release 2c.1), retired with
        /// the council's tasks and the keeper of secrets on 5 November 2026. Kept so saved letters keep their place; never
        /// written, and one an older save holds that is not yet read is called off quietly (NeighbourLetters).
        /// </summary>
        Task,
        /// <summary>Asking to swear fealty to their people, their terms by their ways (Diplomacy/FealtyLetters.cs, release 2c.2).</summary>
        Fealty,
        /// <summary>Asking a town the colony was for it back, to be the colony's home again (Colonies/TownTakeBack.cs, stage 3 release 2b): to its head only.</summary>
        TakeBack,
        /// <summary>
        /// A dealing with a town's council or a people's leaders as a whole (a contract, relief, a proclamation, a hire, a
        /// petition, a joint work, market rights, an invitation: Dealings/DealingLetters.cs, the diplomacy screen's step B),
        /// or the colony's answer to a letter the world wrote first (Dealings/WorldWrites.cs): to the town's head, for its council.
        /// </summary>
        Dealing
    }

    /// <summary>How the words go: a letter carried by hand, or a call over a comms console.</summary>
    public enum LetterChannel
    {
        Courier,
        Console
    }

    /// <summary>How the answer reads.</summary>
    public enum AnswerTone
    {
        Warm,
        Cordial,
        Cool,
        Curt,
        Refusal,
        /// <summary>No answer at all (only a people at war with the colony keeps silent).</summary>
        Silence
    }

    /// <summary>Whom a call reaches: the notable at once, or an aide while they're away, asleep or busy with their people's trouble.</summary>
    public enum CallReach
    {
        AtOnce,
        Away,
        Asleep,
        Busy
    }

    /// <summary>What a letter's answer is worked out from (no game types).</summary>
    public struct LetterInput
    {
        public LetterPurpose purpose;
        public LetterChannel channel;
        /// <summary>How they regard the colony, -100 to 100.</summary>
        public int opinion;
        /// <summary>How they regard the one who wrote, -100 to 100.</summary>
        public int ofWriter;
        /// <summary>The writer's Social skill, 0 to 20.</summary>
        public int social;
        /// <summary>Their nature, -2 (cold, abrasive) to 2 (kind).</summary>
        public int warmth;
        /// <summary>Their people is at war with the colony.</summary>
        public bool hostile;
        /// <summary>A people that sets store by a letter in a hand (tribes, medieval folk, a feudal empire).</summary>
        public bool traditional;
        /// <summary>Their people are in hard times (a plague, a famine, a war): less to spare.</summary>
        public bool troubled;
        /// <summary>The colony wrote to them for the same thing only a little while ago.</summary>
        public bool lately;
    }

    /// <summary>
    /// The pure rules of the colony's neighbours (World/Notables.cs) and the letters to them (World/NeighbourLetters.cs):
    /// which towns are within reach, how a notable regards the colony and its people, how long a letter takes, how an
    /// answer reads and what it changes, whom a call reaches, and how old age takes them. No game types, so Tools/Tests
    /// can check it.
    /// </summary>
    public static class NotableMath
    {
        /// <summary>
        /// The office a notable holds, as a letter signs, heads or greets with it ("Dagna, mayor of Ashford"): their role's
        /// (head, heir, envoy, trader); for one the colony met, only "trader" or "envoy" when that is how they came, else
        /// none (signed "Acraeraes of Karond-Keth 2"). Never a description ("a trader who called", "set free by the colony"):
        /// those are how the colony knows them, not titles (the player's card, 10 November 2026).
        /// </summary>
        public static NotableRole? Office(NotableRole role, string met)
        {
            if (role != NotableRole.Acquaintance) return role;
            return met == "trader" ? NotableRole.Trader : met == "envoy" ? NotableRole.Envoy : (NotableRole?)null;
        }

        public const int TicksPerDay = 60000;
        public const int TicksPerHour = 2500;

        // TechLevel as numbers (the game's enum): 2 neolithic, 3 medieval, 4 industrial, 5 spacer.
        public const int Neolithic = 2;
        public const int Medieval = 3;
        public const int Industrial = 4;
        public const int Spacer = 5;

        // ---------------- Who is within reach ----------------

        /// <summary>Towns up to this far (world tiles) are neighbours.</summary>
        public const float ReachTiles = 40f;
        /// <summary>The nearest so many towns within reach.</summary>
        public const int MaxNear = 4;
        /// <summary>And so many more the colony has dealt with, however far.</summary>
        public const int MaxDealt = 2;
        /// <summary>And so many new towns in their first year (the region's new towns, World/Region/), beside the rest.</summary>
        public const int MaxNew = 2;
        /// <summary>Towns known at most.</summary>
        public const int MaxTowns = MaxNear + MaxDealt + MaxNew;
        /// <summary>Faces the colony met who went home, kept at most.</summary>
        public const int MaxAcquaintances = 10;
        /// <summary>People of each town known by name: head, heir, envoy and trader.</summary>
        public const int RolesPerTown = 4;
        /// <summary>All notables at most (the towns' four each, and the faces met).</summary>
        public const int MaxNotables = MaxTowns * RolesPerTown + MaxAcquaintances;
        /// <summary>What a notable remembers of the colony: the newest lines.</summary>
        public const int MaxMemory = 8;
        /// <summary>Colonists a notable has an opinion of, at most.</summary>
        public const int MaxRegards = 8;
        /// <summary>Notables made in one hour at most (making people is slow).</summary>
        public const int MakePerHour = 2;

        /// <summary>
        /// The towns the colony counts as neighbours (indices into the lists, nearest first): the nearest
        /// <see cref="MaxNear"/> within <see cref="ReachTiles"/> (with none that near, the nearest two within twice that),
        /// then up to <see cref="MaxDealt"/> more the colony has dealt with, however far, then up to <see cref="MaxNew"/>
        /// new towns in their first year (<paramref name="fresh"/>) not among them.
        /// </summary>
        public static List<int> InReach(IList<float> tiles, IList<bool> dealt, IList<bool> fresh = null)
        {
            var result = new List<int>();
            int n = tiles?.Count ?? 0;
            if (n == 0) return result;
            var order = Enumerable.Range(0, n).OrderBy(i => tiles[i]).ThenBy(i => i).ToList();
            foreach (int i in order)
                if (tiles[i] <= ReachTiles && result.Count < MaxNear)
                    result.Add(i);
            if (result.Count == 0)
                foreach (int i in order)
                    if (tiles[i] <= ReachTiles * 2f && result.Count < 2)
                        result.Add(i);
            int more = 0;
            foreach (int i in order)
            {
                if (more >= MaxDealt) break;
                if (result.Contains(i) || dealt == null || i >= dealt.Count || !dealt[i]) continue;
                result.Add(i);
                more++;
            }
            int added = 0;
            foreach (int i in order)
            {
                if (added >= MaxNew) break;
                if (result.Contains(i) || fresh == null || i >= fresh.Count || !fresh[i]) continue;
                result.Add(i);
                added++;
            }
            return result;
        }

        // ---------------- How they regard the colony ----------------

        /// <summary>The colony's own standing with a notable (from letters, visits, deeds to them) halves in this many days.</summary>
        public const float HalfLifeDays = 90f;

        /// <summary>
        /// How a notable regards the colony before anything between them in person: their people's goodwill, what their
        /// people remember (good turns, wrongs, debts the colony owes), and their nature. -100 to 100.
        /// </summary>
        public static int Baseline(int goodwill, float favor, float wrong, float theirFavor, int warmth, bool hostile)
        {
            float v = goodwill * 0.55f + Math.Min(30f, Math.Max(0f, favor) * 0.6f) - Math.Min(40f, Math.Max(0f, wrong) * 0.7f)
                      - Math.Min(8f, Math.Max(0f, theirFavor) * 0.2f) + Clamp(warmth, -2, 2) * 6f;
            if (hostile) v = Math.Min(v, -10f);
            return (int)Math.Round(Math.Max(-100f, Math.Min(100f, v)));
        }

        /// <summary>The whole of it: the baseline and what passed between them in person (letters, visits, a kindness to them).</summary>
        public static int Opinion(int baseline, float personal) => (int)Math.Round(Math.Max(-100f, Math.Min(100f, baseline + personal)));

        /// <summary>
        /// How a notable regards one colonist: half of how they see the colony, what that colonist did for their people
        /// (a deed naming them, as strong as it still is), and what passed between the two.
        /// </summary>
        public static int OfColonist(int ofColony, float deedStrength, float personal) =>
            (int)Math.Round(Math.Max(-100f, Math.Min(100f, ofColony * 0.5f + Math.Min(40f, Math.Max(0f, deedStrength) * 1.5f) + personal)));

        /// <summary>A personal standing after <paramref name="days"/> without news: it halves every <see cref="HalfLifeDays"/>.</summary>
        public static float Fade(float personal, float days) => days <= 0f ? personal : (float)(personal * Math.Pow(0.5, days / HalfLifeDays));

        /// <summary>
        /// How it reads in words (0 to 6): 0 holds the colony dear (60+), 1 warm (30+), 2 friendly (10+), 3 civil (above
        /// -10), 4 cool (above -30), 5 wary (above -60), 6 hostile.
        /// </summary>
        public static int Level(int opinion)
        {
            if (opinion >= 60) return 0;
            if (opinion >= 30) return 1;
            if (opinion >= 10) return 2;
            if (opinion > -10) return 3;
            if (opinion > -30) return 4;
            if (opinion > -60) return 5;
            return 6;
        }

        /// <summary>Asking them for help needs them to think this well of the colony at least.</summary>
        public const int HelpOpinion = 25;

        public static bool CanAskHelp(int opinion, bool hostile) => !hostile && opinion >= HelpOpinion;

        /// <summary>A people at war with the colony can't be asked to visit.</summary>
        public static bool CanInvite(bool hostile) => !hostile;

        // ---------------- How the words go ----------------

        /// <summary>A people with the means to answer a call: industrial or later.</summary>
        public static bool Callable(int tech) => tech >= Industrial;

        /// <summary>A people that sets store by a letter in a hand: tribes and medieval folk, and a feudal empire.</summary>
        public static bool Traditional(int tech, bool royalTitles) => tech <= Medieval || royalTitles;

        /// <summary>Kind words between people (greetings, condolences, an invitation), rather than business.</summary>
        public static bool Personal(LetterPurpose p) => p == LetterPurpose.Greetings || p == LetterPurpose.Condolences || p == LetterPurpose.Invitation;

        /// <summary>
        /// How much the words count, by the way they went: a call is quick but a little less warm than a letter, much less
        /// for personal matters; a letter by hand on a personal matter counts more, most of all to a traditional people.
        /// </summary>
        public static float ChannelFactor(LetterPurpose p, LetterChannel c, bool traditional)
        {
            bool personal = Personal(p);
            if (c == LetterChannel.Console) return personal ? 0.7f : 0.9f;
            if (!personal) return 1f;
            return traditional ? 1.3f : 1.1f;
        }

        /// <summary>Days for a messenger to cover so many tiles, by their people's means: a runner, a rider, a courier with a truck, a shuttle.</summary>
        public static float TravelDays(float tiles, int tech)
        {
            float perTile = tech <= Neolithic ? 0.4f : tech == Medieval ? 0.33f : tech == Industrial ? 0.22f : 0.12f;
            float d = 0.4f + Math.Max(0f, tiles) * perTile;
            return Math.Max(0.75f, Math.Min(12f, d));
        }

        /// <summary>How long they take over their answer once the letter is read (a quarter of a day to a day and a quarter).</summary>
        public static float ReplyDays(float roll) => 0.25f + Clamp01(roll);

        /// <summary>A purpose no longer written (LetterPurpose.Task, the council's business): never offered, and called off before it is read.</summary>
        public static bool Retired(LetterPurpose p) => p == LetterPurpose.Task;

        /// <summary>Days to wait before writing to the same person for the same thing again (one letter at a time to each in any case).</summary>
        public static float WaitDays(LetterPurpose p)
        {
            switch (p)
            {
                case LetterPurpose.Greetings: return 6f;
                case LetterPurpose.Condolences: return 10f;
                case LetterPurpose.Warning: return 3f;
                case LetterPurpose.News: return 5f;
                case LetterPurpose.Invitation: return 15f;
                // A dealing waits on its own rules (Dealings/DealingMath.cs: one of each kind at a time, a while after a refusal).
                case LetterPurpose.Dealing: return 3f;
                default: return 20f;
            }
        }

        /// <summary>Letters and calls under way at once, colony-wide: no more, so answers never pile up.</summary>
        public const int MaxOpen = 3;

        /// <summary>Within this many days of the last letter for the same thing, it counts for less.</summary>
        public const float LatelyDays = 12f;

        /// <summary>A writer who never finds the time (away, ill, busy) for this long: the letter is let go.</summary>
        public const float GiveUpDays = 3f;

        /// <summary>Messengers that came and left without the letter before it is let go.</summary>
        public const int MaxPickups = 2;

        // ---------------- The answer ----------------

        /// <summary>How well the letter lands, before luck: who they think the colony and the writer are, the writer's way with words, their nature, what it's for and how it went.</summary>
        public static float Score(LetterInput x)
        {
            float s = x.opinion * 0.55f + x.ofWriter * 0.35f + (Clamp(x.social, 0, 20) - 6) * 1.5f + Clamp(x.warmth, -2, 2) * 6f + PurposeBase(x.purpose, x.troubled);
            if (x.channel == LetterChannel.Console) s -= 6f;
            else if (Personal(x.purpose) && x.traditional) s += 8f;
            if (x.lately) s -= 10f;
            if (x.hostile) s -= 20f;
            return s;
        }

        private static float PurposeBase(LetterPurpose p, bool troubled)
        {
            switch (p)
            {
                case LetterPurpose.Greetings: return 5f;
                case LetterPurpose.Condolences: return 15f;
                case LetterPurpose.Warning: return 12f;
                case LetterPurpose.News: return 5f;
                case LetterPurpose.Invitation: return 0f;
                default: return troubled ? -25f : -10f;
            }
        }

        /// <summary>How far luck moves an answer, either way.</summary>
        public const float Spread = 22f;

        /// <summary>A people at war with the colony answers only now and then: more often the better they think of it.</summary>
        public static float SilenceChance(LetterInput x) => x.hostile ? Math.Max(0.15f, Math.Min(0.6f, 0.45f - x.opinion / 200f)) : 0f;

        /// <summary>
        /// The answer, from how well it lands and two rolls (0..1): luck, and whether a people at war answers at all. A
        /// people at war never answers warmly. Asking for help that isn't given is a refusal, however kindly put.
        /// </summary>
        public static AnswerTone Answer(LetterInput x, float roll, float silenceRoll)
        {
            if (x.hostile && Clamp01(silenceRoll) < SilenceChance(x)) return AnswerTone.Silence;
            float s = Score(x) + (Clamp01(roll) * 2f - 1f) * Spread;
            AnswerTone t;
            if (s >= 40f) t = AnswerTone.Warm;
            else if (s >= 12f) t = AnswerTone.Cordial;
            else if (s >= -15f) t = AnswerTone.Cool;
            else if (s >= -40f) t = AnswerTone.Curt;
            else t = AnswerTone.Refusal;
            if (x.hostile && t == AnswerTone.Warm) t = AnswerTone.Cordial;
            if (x.purpose == LetterPurpose.Help && !Grants(x, t)) t = AnswerTone.Refusal;
            return t;
        }

        /// <summary>Help is given (or an invitation taken up) on a warm or cordial answer from a people at peace.</summary>
        public static bool Grants(LetterInput x, AnswerTone t) => !x.hostile && (t == AnswerTone.Warm || t == AnswerTone.Cordial);

        /// <summary>
        /// The tone their words to a plea for help are picked by: the answer's own when the help comes with it (warm or
        /// cordial), the refusal's when it doesn't, so a line that promises help is never quoted with nothing sent.
        /// </summary>
        public static AnswerTone HelpQuoteTone(AnswerTone t, bool helpSent) =>
            t == AnswerTone.Silence ? t : helpSent ? (t == AnswerTone.Warm ? AnswerTone.Warm : AnswerTone.Cordial) : AnswerTone.Refusal;

        /// <summary>How much better (or worse) they think of the colony for it; the writer gets half again of it.</summary>
        public static float OpinionShift(AnswerTone t, LetterPurpose p, float channelFactor, bool lately)
        {
            float v;
            switch (t)
            {
                case AnswerTone.Warm: v = p == LetterPurpose.Condolences ? 8f : 6f; break;
                case AnswerTone.Cordial: v = 3f; break;
                case AnswerTone.Cool: v = 1f; break;
                case AnswerTone.Curt: v = 0f; break;
                case AnswerTone.Refusal: v = -2f; break;
                default: v = 0f; break;
            }
            // Asking for help costs a little goodwill of its own, given or not.
            if (p == LetterPurpose.Help) v -= 2f;
            if (v > 0f) v *= Math.Max(0.5f, channelFactor);
            if (lately && v > 0f) v *= 0.5f;
            return v;
        }

        /// <summary>The writer's own share of it.</summary>
        public static float WriterShift(float shift) => shift * 1.5f;

        /// <summary>Their people's goodwill moves a little with a good answer (a warning of a real threat more); a people at war warms a step more to a kind word.</summary>
        public static int GoodwillShift(AnswerTone t, LetterPurpose p, bool hostile)
        {
            int v = t == AnswerTone.Warm ? 2 : t == AnswerTone.Cordial ? 1 : 0;
            if (v > 0 && p == LetterPurpose.Warning) v += 2;
            if (v > 0 && hostile) v += 1;
            if (p == LetterPurpose.Help) v = Math.Min(v, 1);
            return v;
        }

        /// <summary>
        /// A notable sends a gift with an answer (a warm greeting's token, or a gift by their regard) once a season at most
        /// (the player, 10 November 2026: "not be repeatable all the time"; until then every letter could bring one).
        /// </summary>
        public const float GiftGapDays = 15f;

        public static bool GiftAllowed(int lastGift, int now) => lastGift < 0 || now - lastGift >= GiftGapDays * TicksPerDay;

        /// <summary>Asking a people for help: once in 20 days, whoever of them is written to (until 10 November 2026, per notable).</summary>
        public const float HelpPeopleDays = 20f;

        public static bool HelpAllowed(int lastOfPeople, int now) => lastOfPeople < 0 || now - lastOfPeople >= HelpPeopleDays * TicksPerDay;

        /// <summary>A warm answer from someone who thinks well of the colony now and then comes with a small gift.</summary>
        public static bool Gift(AnswerTone t, LetterPurpose p, int opinion, float roll)
        {
            if (t != AnswerTone.Warm || opinion < 35 || p == LetterPurpose.Help || p == LetterPurpose.Invitation || p == LetterPurpose.Dealing) return false;
            return Clamp01(roll) < (p == LetterPurpose.Warning ? 0.5f : 0.35f);
        }

        // ---------------- The road ----------------

        /// <summary>
        /// The chance the messenger bringing an answer is set upon on the way and comes in hurt: now and then on any road,
        /// more often when the road is dangerous (raiders camped near their town, a warlord or bandits in the region).
        /// </summary>
        public static float WaylaidChance(bool dangerousRoad) => dangerousRoad ? 0.15f : 0.04f;

        /// <summary>
        /// The chance a people at war answers with a threat it then carries out (a raid some days after the answer): a
        /// refusal now and then, a curt answer less often, never a kinder one.
        /// </summary>
        public static float ThreatChance(AnswerTone t, bool hostile)
        {
            if (!hostile) return 0f;
            return t == AnswerTone.Refusal ? 0.2f : t == AnswerTone.Curt ? 0.1f : 0f;
        }

        /// <summary>
        /// What a dead or departed notable's role passes to: the heir takes the head's place, everyone else is followed
        /// by someone new. True: the heir steps up (and a new heir is wanted).
        /// </summary>
        public static bool HeirSucceeds(NotableRole gone, bool heirThere) => gone == NotableRole.Head && heirThere;

        // ---------------- A call ----------------

        /// <summary>
        /// Whom a call reaches, by the hour where they are, their people's trouble, and a roll (0..1): asleep in the small
        /// hours, at their people's trouble, now and then away (a people's leader more often).
        /// </summary>
        public static CallReach Reach(int theirHour, bool troubled, bool leader, float roll)
        {
            roll = Clamp01(roll);
            bool night = theirHour >= 22 || theirHour < 6;
            if (night && roll < 0.85f) return CallReach.Asleep;
            if (troubled && roll < 0.5f) return CallReach.Busy;
            if (roll < (leader ? 0.3f : 0.15f)) return CallReach.Away;
            return CallReach.AtOnce;
        }

        /// <summary>Hours until they call back: after their morning (asleep), later that day (away), when the trouble allows (busy).</summary>
        public static float CallbackHours(CallReach r, int theirHour, float roll)
        {
            roll = Clamp01(roll);
            switch (r)
            {
                case CallReach.Asleep:
                {
                    int toMorning = ((8 - theirHour) % 24 + 24) % 24;
                    return Math.Max(1f, toMorning + roll * 2f);
                }
                case CallReach.Busy: return 6f + roll * 14f;
                case CallReach.Away: return 3f + roll * 7f;
                default: return 0f;
            }
        }

        /// <summary>A callback rings this long before it counts as missed (they leave word instead).</summary>
        public const int RingTicks = TicksPerHour * 3;

        // ---------------- Old age ----------------

        /// <summary>
        /// The chance a notable dies of old age on a given day, by their age in human years: none before sixty, then
        /// about 4% a year rising by 1.2 points a year of age (16% at seventy, 28% at eighty), half a year's worth at most.
        /// </summary>
        public static float DeathChancePerDay(float humanAge)
        {
            if (humanAge < 60f) return 0f;
            float yearly = Math.Min(0.5f, 0.04f + (humanAge - 60f) * 0.012f);
            return yearly / 60f;
        }

        // ---------------- Arithmetic ----------------

        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
