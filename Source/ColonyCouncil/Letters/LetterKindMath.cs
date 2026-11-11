using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The shape a letter takes (the player, 10 November 2026: "sometimes the letters can be longer, with some stories or
    /// stuff that is told by the different pawns or factions. some might include poems, detailed stories of raider attacks,
    /// ideological ramblings and all kinds of different things. they don't all always fully have to follow the same
    /// structure, some could be several pages long, some just boiled down to the necessities"). The usual letter of
    /// 0.202.0 stays the most common; the rest come by the writer, their motive, the purpose, the tone and how things stand.
    /// </summary>
    public enum LetterKind : byte
    {
        /// <summary>The form of 0.202.0: an opening, the heart, now and then an aside, a closing, a signature, now and then a P.S.</summary>
        Usual,
        /// <summary>The bare note: a line or two, the necessities only.</summary>
        Note,
        /// <summary>The long letter in several parts, each with its own news or story.</summary>
        Long,
        /// <summary>A story told: a tale of their town, a hunt, a feud, a wedding gone wrong, a ghost, a winter.</summary>
        Story,
        /// <summary>A raid's account: how raiders came, who stood where, what burned, who fell.</summary>
        Raid,
        /// <summary>A poem or a song, whole.</summary>
        Poem,
        /// <summary>A sermon or a tract by their ideoligion's memes and precepts.</summary>
        Sermon,
        /// <summary>A chronicle: a town's newsletter or broadsheet, a crier's roll, a tribe's season told.</summary>
        Chronicle,
        /// <summary>A petition with signatures.</summary>
        Petition,
        /// <summary>A list or an account.</summary>
        Account,
        /// <summary>A recipe or a remedy.</summary>
        Recipe,
        /// <summary>A riddle (its answer in the writer's next letter).</summary>
        Riddle,
        /// <summary>A letter by several hands: others at home add a line each.</summary>
        Hands,
        /// <summary>Something folded in with it, described: a pressed flower, a child's drawing, a map.</summary>
        Enclosure,
        /// <summary>A diary page enclosed, or a letter broken off and taken up again the next day.</summary>
        Diary
    }

    /// <summary>How long letters may get (the setting under "Letters written out in full").</summary>
    public enum LetterLength
    {
        /// <summary>None of the long shapes; bare notes a little more often.</summary>
        Short,
        /// <summary>Most letters a readable length, long ones as occasional treats.</summary>
        Mixed,
        /// <summary>The long shapes two and a half times as often.</summary>
        Long
    }

    /// <summary>What a letter back to a notable is (Letters/LetterReply.cs): the answer their letter written first waits for, greetings, or anything that can be written now.</summary>
    public enum ReplyAs
    {
        Answer,
        Greetings,
        Any
    }

    /// <summary>How a raid on a town ended, as its record has it.</summary>
    public enum RaidEnd : byte
    {
        /// <summary>Not known (a battle recorded, an old tale).</summary>
        Unknown,
        /// <summary>Still going on: raiders about.</summary>
        Ongoing,
        /// <summary>The raiders were beaten off.</summary>
        Held,
        /// <summary>The town was hurt: stores taken, houses burned.</summary>
        Hurt,
        /// <summary>The town was sacked.</summary>
        Sacked
    }

    /// <summary>
    /// A real raid, from a record the game keeps (World/WorldSituations, the ledger's history, a people's memory of the
    /// colony): on their town, on their road, by the colony itself, or on the colony (its own letters). Nothing in a raid's
    /// account of it says more than this.
    /// </summary>
    public sealed class RaidRecord
    {
        /// <summary>Who came, by name, or null when the record doesn't say.</summary>
        public string raiders;
        /// <summary>How long ago, in days.</summary>
        public float days;
        public RaidEnd end;
        /// <summary>On their road, not at their walls.</summary>
        public bool road;
        /// <summary>The colony's fighters stood with them.</summary>
        public bool helped;
        /// <summary>The colony itself came at them.</summary>
        public bool byColony;
        /// <summary>A raid on the colony (the colony's own letter tells it).</summary>
        public bool ours;
        /// <summary>Their dead: -1 not known, 0 none, 1 some, 2 many.</summary>
        public int dead = -1;
    }

    /// <summary>
    /// Who writes, beyond what <see cref="LetterSpec"/> holds, as the shapes are chosen by: their gifts and nature, real
    /// news and a real raid at their town, who else at home might add a line, the shape of their last letter.
    /// </summary>
    public sealed class LetterWho
    {
        /// <summary>Artistic skill, an artist's meme or precept, or a people of singers and makers.</summary>
        public bool artistic;
        /// <summary>A true believer, or a crank: their faith is certain and its memes strong.</summary>
        public bool zealous;
        /// <summary>A zealous writer of the fringe (short-tempered, suspicious or unschooled): their tract reads like it.</summary>
        public bool crank;
        /// <summary>Warm, open or merry by nature: they run on.</summary>
        public bool talkative;
        /// <summary>Warm but bleak, far from anyone: a letter is company.</summary>
        public bool lonely;
        /// <summary>A town's head, a trader in season: little time.</summary>
        public bool busy;
        /// <summary>Real news of their town to tell (the facts: a world event there, its fortune moved, a loss, a new head, a war).</summary>
        public bool news;
        /// <summary>A real raid (<see cref="RaidRecord"/>), or null.</summary>
        public RaidRecord raid;
        /// <summary>Their people raided the colony and they are of a raiding people: a boast of it.</summary>
        public bool boast;
        /// <summary>The colony's own: the cook is known ({COOK}), for a recipe.</summary>
        public bool cook;
        /// <summary>The shape of this writer's last letter (Usual when none).</summary>
        public LetterKind last = LetterKind.Usual;
        /// <summary>The answer to the riddle in their last letter, still owed (said in this one's P.S.), or null.</summary>
        public string owedAnswer;
        /// <summary>Who else at home might add a line, by name or as they are called at home.</summary>
        public readonly List<string> hands = new List<string>();
        /// <summary>Who signs a petition, by name.</summary>
        public readonly List<string> signers = new List<string>();
    }

    /// <summary>Where the lines of a shape come from: a line that fits (filled, never one drawn lately), and whether any fits at all.</summary>
    public sealed class LetterLines
    {
        public Func<LetterPart, HashSet<string>, string> pick;
        public Func<LetterPart, HashSet<string>, bool> has;

        public string Pick(LetterPart part, HashSet<string> tags) => pick?.Invoke(part, tags);

        public bool Has(LetterPart part, HashSet<string> tags) => has != null ? has(part, tags) : pick?.Invoke(part, tags) != null;
    }

    /// <summary>
    /// Letters of many shapes (the player, 10 November 2026: "I just want a lot of variety, content and interesting writing
    /// that makes reading every letter a fun or interesting experience"). Which shapes fit a writer and a situation
    /// (<see cref="Fits"/>), how likely each is (<see cref="Weight"/>, by the setting <see cref="LetterLength"/>), the choice
    /// (<see cref="Choose"/>: never the same unusual shape twice running from one writer), and the making of each from its
    /// pools (<see cref="Build"/>): stories, raids and chronicles from parts with slots filled from what is true, poems,
    /// sermons and set pieces whole. A long letter is read in pages (<see cref="Pages"/>). Pure (no game types): Tools/Tests
    /// checks the rules and makes whole letters of every shape from the real pools.
    /// </summary>
    public static class LetterKindMath
    {
        public static readonly LetterKind[] Kinds = (LetterKind[])Enum.GetValues(typeof(LetterKind));

        /// <summary>The shapes that make a long letter: none of them with the setting at Short.</summary>
        public static readonly HashSet<LetterKind> LongKinds = new HashSet<LetterKind>
            { LetterKind.Long, LetterKind.Story, LetterKind.Raid, LetterKind.Sermon, LetterKind.Chronicle, LetterKind.Diary };

        public static bool IsLong(LetterKind k) => LongKinds.Contains(k);

        /// <summary>A shape's tag on lines and in the context: lk:usual, lk:note, lk:story...</summary>
        public static string Tag(LetterKind k) => "lk:" + k.ToString().ToLowerInvariant();

        /// <summary>In the context when the setting is Short: a long poem is tagged !len:short.</summary>
        public const string ShortTag = "len:short";

        /// <summary>A piece's lead-in and its after-line (Rimfolk_Pool_LetterLead): lk:KIND with lk:lead, or with lk:after for the line after it.</summary>
        public const string LeadTag = "lk:lead", AfterTag = "lk:after";

        /// <summary>A sermon for any faith (Rimfolk_Pool_LetterSermon): drawn only when the writer's own faith's pieces aren't (one time in five, or none fits).</summary>
        public const string AnyFaithTag = "sm:any";

        // ================= What a letter is about =================

        /// <summary>The purposes of a letter that is about nothing in particular: room for a story, a poem, the news.</summary>
        private static readonly HashSet<string> chatty = new HashSet<string> { "greetings", "news", "first", "thanks", "friendmade", "reply", "invitation" };

        /// <summary>A chatty letter: greetings, news, an invitation, a friend's word, their thanks, a letter written first that asks for nothing.</summary>
        public static bool Chatty(LetterSpec s) => s.purpose != null && chatty.Contains(s.purpose) && !Asks(s);

        /// <summary>Their letter written first that asks for something (k:request), or the colony's asking for help.</summary>
        public static bool Asks(LetterSpec s) => s.purpose == "first" && s.tags.Contains("k:request") || s.ours && s.purpose == "help";

        private static bool Light(LetterSpec s) => !LetterFormMath.Grave(s);

        private static bool WarmOrCordial(LetterSpec s) => s.tone <= AnswerTone.Cordial;

        private static bool Official(LetterSpec s) => s.council || s.empire || s.motive == LetterMotive.Duty;

        /// <summary>The speech a piece is written in: a medieval writer's, an unschooled one's, a strange voice; a barely schooled hand and a plain writer draw the plain pieces.</summary>
        public static string PieceSpeech(string speech) => speech == LetterFormMath.SomeSchool ? null : speech;

        /// <summary>The speeches each shape has pieces in (null: plain). A writer of another speech writes the usual letter.</summary>
        public static readonly Dictionary<LetterKind, string[]> Speeches = new Dictionary<LetterKind, string[]>
        {
            [LetterKind.Long] = new[] { null, LetterFormMath.Olde },
            [LetterKind.Story] = new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool, LetterCultureMath.Beast, LetterCultureMath.Ancient },
            [LetterKind.Raid] = new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool, LetterCultureMath.Machine, LetterCultureMath.Beast },
            [LetterKind.Poem] = new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool, LetterCultureMath.Fey, LetterCultureMath.Green, LetterCultureMath.Machine, LetterCultureMath.Ancient,
                LetterCultureMath.Beast, LetterCultureMath.Hive, LetterCultureMath.Deep },
            [LetterKind.Sermon] = new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool, LetterCultureMath.Deep, LetterCultureMath.Hive },
            [LetterKind.Chronicle] = new[] { null, LetterFormMath.Olde, LetterCultureMath.Machine, LetterCultureMath.Hive },
            [LetterKind.Petition] = new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool },
            [LetterKind.Account] = new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool, LetterCultureMath.Machine },
            [LetterKind.Recipe] = new[] { null, LetterFormMath.Olde, LetterCultureMath.Machine, LetterCultureMath.Green, LetterCultureMath.Beast },
            [LetterKind.Riddle] = new[] { null, LetterFormMath.Olde, LetterCultureMath.Fey, LetterCultureMath.Ancient, LetterCultureMath.Machine, LetterCultureMath.Deep },
            [LetterKind.Hands] = new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool },
            [LetterKind.Enclosure] = new[] { null, LetterFormMath.Olde, LetterFormMath.NoSchool, LetterCultureMath.Fey, LetterCultureMath.Beast, LetterCultureMath.Green,
                LetterCultureMath.Machine, LetterCultureMath.Ancient, LetterCultureMath.Hive },
            [LetterKind.Diary] = new[] { null, LetterFormMath.Olde, LetterCultureMath.Ancient, LetterCultureMath.Machine, LetterCultureMath.Beast, LetterCultureMath.Green }
        };

        /// <summary>Whether a shape has pieces in this writer's speech (the usual letter and the note in every speech; the ancient never write a bare note).</summary>
        public static bool SpeaksIn(LetterKind k, string speech)
        {
            if (k == LetterKind.Usual) return true;
            if (k == LetterKind.Note) return speech != LetterCultureMath.Ancient;
            return Speeches.TryGetValue(k, out var list) && Array.IndexOf(list, PieceSpeech(speech)) >= 0;
        }

        // ================= Which shapes fit =================

        /// <summary>
        /// Whether a shape fits the writer and the situation. A call and words passed on are always as they are. The usual
        /// letter always fits; a bare note nearly always (never from the ancient). The rest:
        /// - only in a written letter, in a speech the shape has pieces in, never at war but a raider's boast;
        /// - long, storied letters from pen friends, the warm, the lonely, the zealous and the talkative, in a letter about
        ///   nothing in particular, warm or courteous;
        /// - poems from artistic writers (and the fey and the plant folk, who sing); sermons from the zealous;
        /// - a raid's account from a town raided in fact (or the colony's own raid in its own letter), else told as an old
        ///   tale by those who tell tales; a boast from raiders who raided the colony;
        /// - a chronicle where there is real news, or from a town's head; a petition or a list where something is asked;
        ///   a recipe, a riddle, a letter by several hands, an enclosure, a diary page where the writer and the tone allow;
        /// - none of the business between two peoples, from a council, in a curt letter or a refusal.
        /// The colony's own take some: a verse from a poet, a little preaching from a zealot, a bare note, other hands, an
        /// enclosure, the cook's recipe, a petition for help, and the colony's own raid told to a friend.
        /// </summary>
        public static bool Fits(LetterKind k, LetterSpec s)
        {
            if (s == null) return false;
            if (k == LetterKind.Usual) return true;
            if (s.shape != LetterShape.Written) return false;
            var who = s.who ?? new LetterWho();
            string speech = LetterFormMath.SpeechOf(s);
            if (!SpeaksIn(k, speech)) return false;
            if (k == LetterKind.Note) return true;
            if (s.council) return false;
            // A cooled correspondence (Letters/LetterThreadMath.cs): shorter and colder, the usual letter or a note (or a raider's boast).
            if (s.fam == Familiarity.Cooled && !LetterThreadMath.CooledFits(k, !s.ours && who.boast)) return false;
            // At war, a letter is a letter or a note, or a raider's boast of what they did to the colony.
            if (s.war) return k == LetterKind.Raid && !s.ours && who.boast;
            if (LetterFormMath.BusinessPurpose(s.purpose)) return false;
            if (s.tone >= AnswerTone.Refusal) return false;
            bool chat = Chatty(s), light = Light(s), warmish = WarmOrCordial(s);
            bool friendly = s.penFriend || s.motive == LetterMotive.Friend;
            if (s.ours) return FitsOurs(k, s, who, chat, light, warmish);
            switch (k)
            {
                case LetterKind.Long:
                    return chat && warmish && (who.talkative || who.lonely || who.zealous || s.penFriend);
                case LetterKind.Story:
                    return chat && warmish && (who.talkative || who.lonely || s.penFriend || s.age == WorldAge.Tribal || s.humour >= 40
                                               || speech == LetterCultureMath.Beast || speech == LetterCultureMath.Ancient);
                case LetterKind.Raid:
                    if (s.tone >= AnswerTone.Curt && !who.boast) return false;
                    if (!(chat || s.purpose == "warning")) return false;
                    if (who.raid != null && !who.raid.ours) return true;
                    if (who.boast) return true;
                    // An old tale: from those who tell tales.
                    return warmish && (who.talkative || s.age == WorldAge.Tribal || speech == LetterCultureMath.Ancient || speech == LetterCultureMath.Beast
                                       || s.tags.Contains("c:martial") || s.tags.Contains("c:raider") || s.tags.Contains("their:mercenaries"));
                case LetterKind.Poem:
                    if (!(who.artistic || speech == LetterCultureMath.Fey || speech == LetterCultureMath.Green)) return false;
                    if (s.purpose == "condolences") return s.tone <= AnswerTone.Cool;
                    return chat && warmish;
                case LetterKind.Sermon:
                    return who.zealous && s.tone <= AnswerTone.Cool && (chat || s.purpose == "condolences");
                case LetterKind.Chronicle:
                    return chat && s.tone <= AnswerTone.Cool && (who.news || who.busy || s.tags.Contains("their:scholars") || s.tags.Contains("their:citizens")
                                                                 || s.tags.Contains("their:linesmen") || s.tags.Contains("their:crew"));
                case LetterKind.Petition:
                    return Asks(s) && s.tone <= AnswerTone.Cool && (LetterFormMath.Grave(s) || who.busy || s.tags.Contains("town:struggling") || s.tags.Contains("town:poor"));
                case LetterKind.Account:
                    return s.tone <= AnswerTone.Cool && (Asks(s) || chat && (s.motive == LetterMotive.Trade || speech == LetterCultureMath.Machine || speech == LetterFormMath.NoSchool));
                case LetterKind.Recipe:
                    // A remedy in place of the medicine they can't send, or a recipe between friends.
                    if (s.purpose == "help" && s.tags.Contains("medicine")) return s.tone <= AnswerTone.Cool;
                    return chat && warmish && light && (friendly || who.talkative);
                case LetterKind.Riddle:
                    return chat && warmish && light && (s.humour >= 30 || speech == LetterCultureMath.Fey || speech == LetterCultureMath.Ancient);
                case LetterKind.Hands:
                    return (chat || s.purpose == "condolences") && warmish && !s.empire && who.hands.Count > 0 && (friendly || s.tone == AnswerTone.Warm || s.purpose == "condolences");
                case LetterKind.Enclosure:
                    return (chat || s.purpose == "condolences") && warmish && (friendly || s.tone == AnswerTone.Warm);
                case LetterKind.Diary:
                    return chat && warmish && (who.lonely || who.talkative);
                default:
                    return false;
            }
        }

        private static bool FitsOurs(LetterKind k, LetterSpec s, LetterWho who, bool chat, bool light, bool warmish)
        {
            switch (k)
            {
                case LetterKind.Poem:
                    if (!who.artistic) return false;
                    return s.purpose == "condolences" ? s.tone <= AnswerTone.Cool : chat && warmish;
                case LetterKind.Sermon:
                    return who.zealous && s.tone <= AnswerTone.Cool && (chat || s.purpose == "condolences");
                case LetterKind.Hands:
                    return (chat || s.purpose == "condolences") && warmish && who.hands.Count > 0 && !s.voice.abrasive;
                case LetterKind.Enclosure:
                    return (chat || s.purpose == "condolences") && warmish && !s.voice.abrasive;
                case LetterKind.Recipe:
                    return chat && warmish && light && who.cook;
                case LetterKind.Petition:
                    return s.purpose == "help" && s.tone <= AnswerTone.Cool && who.signers.Count >= 2;
                case LetterKind.Raid:
                    // The colony's own raid, only as the facts have it, told to those who care to hear it.
                    return who.raid != null && who.raid.ours && warmish && (chat || s.purpose == "warning");
                default:
                    return false;
            }
        }

        // ================= How likely =================

        /// <summary>
        /// How likely a shape is, when it fits (0 when not). The usual letter 10. A bare note 1.5, three times for a curt
        /// letter or a refusal, three at war, two and a half from an official, twice from the busy and the colony's blunt
        /// writers, half between warm friends. A long letter 0.8 (a half more each for the talkative, the lonely and a pen
        /// friend). A story 1 (a half more from a tribe, a third more from the talkative). A raid's account 3 from a record
        /// (half again within a season), 2 for a boast, 0.6 as an old tale. A poem 2, a sermon 2.5, a chronicle 1.2 (half
        /// again with news, from the head), a petition 1.5, a list 1, a recipe 0.6, a riddle 0.6 (more for the merriest),
        /// other hands 0.8, an enclosure 0.8, a diary 0.6. The last unusual shape of this writer not again. Short: no long
        /// shape, a note half again as often; Long and storied: the long shapes two and a half times.
        /// </summary>
        public static float Weight(LetterKind k, LetterSpec s, LetterLength length)
        {
            if (!Fits(k, s)) return 0f;
            var who = s.who ?? new LetterWho();
            if (k != LetterKind.Usual && k == who.last) return 0f;
            if (length == LetterLength.Short && IsLong(k)) return 0f;
            float w;
            switch (k)
            {
                case LetterKind.Usual: return 10f;
                case LetterKind.Note:
                    w = 1.5f;
                    if (s.tone >= AnswerTone.Curt) w *= 3f;
                    if (s.war) w *= 3f;
                    if (Official(s)) w *= 2.5f;
                    if (who.busy) w *= 2f;
                    if (s.ours && s.voice.main == VoiceMain.Blunt) w *= 2f;
                    if (s.tone == AnswerTone.Warm && (s.penFriend || s.motive == LetterMotive.Friend)) w *= 0.5f;
                    if (length == LetterLength.Short) w *= 1.5f;
                    else if (length == LetterLength.Long) w *= 0.7f;
                    // How well the two know each other: a note between old friends is rarer, a cooled one's far likelier.
                    return Math.Min(w * LetterThreadMath.KindFactor(k, s.fam), 30f);
                case LetterKind.Long:
                    w = 0.8f;
                    if (who.talkative) w *= 1.5f;
                    if (who.lonely) w *= 1.5f;
                    if (s.penFriend) w *= 1.5f;
                    break;
                case LetterKind.Story:
                    w = 1f;
                    if (s.age == WorldAge.Tribal) w *= 1.5f;
                    if (who.talkative) w *= 1.3f;
                    break;
                case LetterKind.Raid:
                    if (who.raid != null) w = who.raid.days <= 15f ? 4.5f : 3f;
                    else w = who.boast ? 2f : 0.6f;
                    break;
                case LetterKind.Poem: w = 2f; break;
                case LetterKind.Sermon: w = 2.5f; break;
                case LetterKind.Chronicle:
                    w = 1.2f;
                    if (who.news) w *= 1.5f;
                    if (who.busy) w *= 1.5f;
                    break;
                case LetterKind.Petition: w = 1.5f; break;
                case LetterKind.Account: w = 1f; break;
                case LetterKind.Recipe: w = 0.6f; break;
                case LetterKind.Riddle: w = s.humour >= 60 ? 0.9f : 0.6f; break;
                case LetterKind.Hands: w = 0.8f; break;
                case LetterKind.Enclosure: w = 0.8f; break;
                case LetterKind.Diary: w = 0.6f; break;
                default: return 0f;
            }
            if (length == LetterLength.Long && IsLong(k)) w *= 2.5f;
            // Nobody sends several pages to a stranger; old friends write long and add more (Letters/LetterThreadMath.cs).
            return w * LetterThreadMath.KindFactor(k, s.fam);
        }

        /// <summary>The shape a letter takes: one of those that fit, by their weights (<paramref name="roll"/> 0 to 1).</summary>
        public static LetterKind Choose(LetterSpec s, LetterLength length, float roll)
        {
            float total = 0f;
            foreach (var k in Kinds) total += Weight(k, s, length);
            if (total <= 0f) return LetterKind.Usual;
            float r = Math.Max(0f, Math.Min(0.9999f, roll)) * total;
            foreach (var k in Kinds)
            {
                float w = Weight(k, s, length);
                if (w <= 0f) continue;
                if (r < w) return k;
                r -= w;
            }
            return LetterKind.Usual;
        }

        // ================= The tags a piece is drawn by =================

        /// <summary>
        /// The tags a piece of a shape is drawn by: the letter's (LetterFormMath.Tags), the shape's own (lk:KIND), Short's
        /// len:short, and the writer's speech as its pieces are written (a barely schooled hand draws the plain pieces).
        /// </summary>
        public static HashSet<string> PieceTags(LetterSpec s, LetterKind k, params string[] more)
        {
            var t = LetterFormMath.Tags(s);
            t.Add(Tag(k));
            if (s.length == LetterLength.Short) t.Add(ShortTag);
            // A poem's form only says what it is: every form is open to every poem.
            if (k == LetterKind.Poem)
                foreach (var f in Forms)
                    t.Add(f);
            string speech = LetterFormMath.SpeechOf(s);
            if (speech != null && PieceSpeech(speech) == null) t.Remove(speech);
            if (s.who?.crank == true) t.Add("zeal:crank");
            else if (s.who?.zealous == true) t.Add("zeal:true");
            if (s.who?.raid != null) AddRaid(t, s.who.raid);
            else if (s.who?.boast == true) t.Add("rd:boast");
            else t.Add("rd:old");
            foreach (var m in more)
                if (!string.IsNullOrEmpty(m))
                    t.Add(m);
            return t;
        }

        /// <summary>
        /// A real raid as tags: rd:now (a record; rd:old for an old tale), rd:fresh within ten days, rd:recent within a
        /// season, rd:past before; rd:end:held, hurt, sacked, ongoing or unknown; rd:road or rd:walls; rd:helped when the
        /// colony's fighters stood with them; rd:you when the colony came at them; rd:ours for the colony's own raid; their
        /// dead as rd:dead:none, some or many (none said when not known).
        /// </summary>
        public static void AddRaid(HashSet<string> t, RaidRecord r)
        {
            if (r == null)
            {
                t.Add("rd:old");
                return;
            }
            t.Add("rd:now");
            t.Add(r.days <= 10f ? "rd:fresh" : r.days <= 30f ? "rd:recent" : "rd:past");
            t.Add("rd:end:" + r.end.ToString().ToLowerInvariant());
            // The colony's own attack is told in its own lines (rd:you), not as any raid at their walls.
            if (r.road) t.Add("rd:road");
            else if (!r.byColony) t.Add("rd:walls");
            if (r.helped) t.Add("rd:helped");
            if (r.byColony) t.Add("rd:you");
            if (r.ours) t.Add("rd:ours");
            if (r.dead == 0) t.Add("rd:dead:none");
            else if (r.dead == 1) t.Add("rd:dead:some");
            else if (r.dead >= 2) t.Add("rd:dead:many");
        }

        // ================= Making a shape =================

        /// <summary>The themes of a story told (each with its openings, turns and endings in Rimfolk_Pool_LetterStory).</summary>
        public static readonly string[] Themes =
        {
            "st:hunt", "st:feud", "st:wedding", "st:ghost", "st:winter", "st:storm", "st:fair", "st:stranger", "st:harvest", "st:lost", "st:journey", "st:contest", "st:feast",
            // Their god's sign, curse or myth (Gods/World/GodLetters.cs): only when the letter tells of one (god:miracle, god:curse, god:myth).
            "st:god"
        };

        /// <summary>What a chronicle's items can be about, one item each (Rimfolk_Pool_LetterChronicle, ch:item and one of these).</summary>
        public static readonly string[] Subjects =
        {
            "chs:fortune", "chs:situation", "chs:loss", "chs:head", "chs:ill", "chs:war", "chs:peace", "chs:newtown", "chs:camp", "chs:raid", "chs:deed", "chs:colony",
            "chs:caravan", "chs:season", "chs:short",
            // Their god's sign or curse, or their holy war (Gods/World/GodLetters.cs).
            "chs:god"
        };

        /// <summary>A poem's forms (Rimfolk_Pool_LetterPoem): each poem names one, which only says what it is.</summary>
        public static readonly string[] Forms = { "po:ballad", "po:verse", "po:lament", "po:drinking", "po:hymn", "po:nonsense", "po:work", "po:war", "po:song" };

        /// <summary>The bodies a long letter's parts can have: the news, everyday life, a story, a short verse, a raid's account.</summary>
        public static readonly string[] PartBodies = { "pa:news", "pa:life", "pa:story", "pa:poem", "pa:raid" };

        /// <summary>
        /// Makes the shape's pieces into the spec (<see cref="LetterSpec.piece"/>, the paragraphs after the heart;
        /// <see cref="LetterSpec.after"/>, the lines under the signature): false, nothing changed, when the pools have no
        /// piece for this writer and situation (the letter is then the usual one). The heart (what the letter is for, which
        /// says what the game does) is never replaced.
        /// </summary>
        public static bool Build(LetterSpec s, LetterWords w, LetterLines lines, Func<float> roll)
        {
            if (s == null || lines == null) return false;
            roll ??= () => 0.5f;
            w ??= new LetterWords();
            var piece = new List<string>();
            var after = new List<string>();
            bool ok;
            switch (s.kind)
            {
                case LetterKind.Usual:
                case LetterKind.Note:
                    return true;
                case LetterKind.Long: ok = Long(s, lines, roll, piece); break;
                case LetterKind.Story: ok = Lead(s, lines, roll, piece, LetterKind.Story, 0.85f) && Story(s, lines, roll, piece, LetterKind.Story, false) && After(s, lines, roll, piece, LetterKind.Story, 0.35f); break;
                case LetterKind.Raid: ok = Lead(s, lines, roll, piece, LetterKind.Raid, 0.9f) && Raid(s, lines, roll, piece, false) && After(s, lines, roll, piece, LetterKind.Raid, 0.3f); break;
                case LetterKind.Poem: ok = Whole(s, lines, roll, piece, LetterKind.Poem, LetterPart.Poem, 0.7f, 0.3f); break;
                case LetterKind.Sermon: ok = Sermon(s, lines, roll, piece); break;
                case LetterKind.Chronicle: ok = Chronicle(s, lines, roll, piece); break;
                case LetterKind.Petition: ok = Petition(s, lines, roll, piece); break;
                case LetterKind.Account: ok = Account(s, lines, roll, piece); break;
                case LetterKind.Recipe: ok = Whole(s, lines, roll, piece, LetterKind.Recipe, LetterPart.Set, 0.8f, 0.2f, "rc:recipe"); break;
                case LetterKind.Riddle: ok = Riddle(s, lines, roll, piece); break;
                case LetterKind.Hands: ok = Hands(s, w, lines, roll, after); break;
                case LetterKind.Enclosure: ok = Enclosure(s, lines, roll, after); break;
                case LetterKind.Diary: ok = Diary(s, lines, roll, piece); break;
                default: ok = false; break;
            }
            if (!ok || piece.Count == 0 && after.Count == 0) return false;
            s.piece.Clear();
            s.piece.AddRange(piece);
            s.after.Clear();
            s.after.AddRange(after);
            return true;
        }

        private static void Add(List<string> list, string p)
        {
            if (!string.IsNullOrEmpty(p)) list.Add(Lines(p).Trim());
        }

        /// <summary>The lead-in before a piece, now and then (always true: a piece needs none).</summary>
        private static bool Lead(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece, LetterKind k, float chance)
        {
            if (roll() < chance) Add(piece, lines.Pick(LetterPart.Lead, PieceTags(s, k, LeadTag)));
            return true;
        }

        /// <summary>A line after the piece, now and then.</summary>
        private static bool After(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece, LetterKind k, float chance)
        {
            if (roll() < chance) Add(piece, lines.Pick(LetterPart.Lead, PieceTags(s, k, AfterTag)));
            return true;
        }

        /// <summary>A piece authored whole (a poem, a sermon, a recipe): its lead-in now and then, the piece, a line after it now and then.</summary>
        private static bool Whole(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece, LetterKind k, LetterPart part, float lead, float after, string tag = null)
        {
            var t = PieceTags(s, k, tag);
            string body = lines.Pick(part, t);
            if (string.IsNullOrEmpty(body)) return false;
            Lead(s, lines, roll, piece, k, lead);
            foreach (var p in Paragraphs(body)) Add(piece, p);
            After(s, lines, roll, piece, k, after);
            return true;
        }

        /// <summary>
        /// A sermon, by the writer's own faith first: four times in five a piece of their own memes and precepts while one
        /// fits, else any (the pieces for any faith, tagged sm:any, among them).
        /// </summary>
        private static bool Sermon(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece)
        {
            bool own = roll() < 0.8f && lines.Has(LetterPart.Sermon, PieceTags(s, LetterKind.Sermon));
            return Whole(s, lines, roll, piece, LetterKind.Sermon, LetterPart.Sermon, 0.5f, 0.15f, own ? null : AnyFaithTag);
        }

        /// <summary>
        /// A story told, from its parts: a theme that has all three for this writer (an opening, a turn, an ending), then
        /// a moral or a joke now and then (a joke only in a light letter). <paramref name="small"/>: a long letter's part,
        /// with no coda.
        /// </summary>
        private static bool Story(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece, LetterKind k, bool small)
        {
            var themes = new List<string>(Themes);
            Shuffle(themes, roll);
            foreach (var theme in themes)
            {
                var open = PieceTags(s, LetterKind.Story, "st:open", theme);
                var turn = PieceTags(s, LetterKind.Story, "st:turn", theme);
                var end = PieceTags(s, LetterKind.Story, "st:end", theme);
                if (!lines.Has(LetterPart.Story, open) || !lines.Has(LetterPart.Story, turn) || !lines.Has(LetterPart.Story, end)) continue;
                string a = lines.Pick(LetterPart.Story, open), b = lines.Pick(LetterPart.Story, turn), c = lines.Pick(LetterPart.Story, end);
                if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || string.IsNullOrEmpty(c)) continue;
                Add(piece, a);
                Add(piece, b);
                Add(piece, c);
                if (!small && roll() < 0.5f)
                {
                    string coda = Light(s) && roll() < 0.5f ? "st:joke" : "st:moral";
                    Add(piece, lines.Pick(LetterPart.Story, PieceTags(s, LetterKind.Story, coda)));
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// A raid's account from its parts, by the record (or as an old tale): how they came, who stood where, what burned
        /// (when the town was hurt or sacked, or in an old tale now and then), who fell (only as the record has it: never
        /// a death the record doesn't hold), how it ended, and after. Grim, or grim-funny from a merry writer in a light
        /// letter. <paramref name="small"/>: a long letter's part (no aftermath).
        /// </summary>
        private static bool Raid(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece, bool small)
        {
            var r = s.who?.raid;
            bool wry = Light(s) && s.tone <= AnswerTone.Cordial && s.humour >= 20 && roll() < 0.5f;
            string mood = wry ? "rd:wry" : "rd:grim";
            HashSet<string> T(string part) => PieceTags(s, LetterKind.Raid, part, mood);
            if (!lines.Has(LetterPart.Raid, T("rd:come")) || !lines.Has(LetterPart.Raid, T("rd:end"))) return false;
            string come = lines.Pick(LetterPart.Raid, T("rd:come"));
            if (string.IsNullOrEmpty(come)) return false;
            Add(piece, come);
            if (roll() < 0.85f) Add(piece, lines.Pick(LetterPart.Raid, T("rd:stand")));
            bool old = r == null;
            bool burned = r != null && (r.end == RaidEnd.Hurt || r.end == RaidEnd.Sacked) || old && roll() < 0.5f;
            if (burned) Add(piece, lines.Pick(LetterPart.Raid, T("rd:burn")));
            // Who fell: as the record has it (none, some, many), or in an old tale now and then; never a death not recorded.
            bool fall = r != null ? r.dead >= 0 : roll() < 0.5f;
            if (fall) Add(piece, lines.Pick(LetterPart.Raid, T("rd:fall")));
            Add(piece, lines.Pick(LetterPart.Raid, T("rd:end")));
            if (!small && roll() < 0.6f) Add(piece, lines.Pick(LetterPart.Raid, T("rd:after")));
            return true;
        }

        /// <summary>
        /// A long letter in parts: three or four (four or five with the setting at Long and storied), each with a heading
        /// now and then and its own body: the news first when there is some, then everyday life (a paragraph or two), a
        /// story, a short verse from an artistic writer, a raid's account from a town raided; never two of a kind but
        /// everyday life, never the same paragraph twice.
        /// </summary>
        private static bool Long(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece)
        {
            int n = s.length == LetterLength.Long ? 4 + (roll() < 0.6f ? 1 : 0) : 3 + (roll() < 0.4f ? 1 : 0);
            var bodies = new List<string>();
            var have = new List<string>();
            if (lines.Has(LetterPart.Part, PieceTags(s, LetterKind.Long, "pa:news"))) have.Add("pa:news");
            if (s.who?.raid != null && !s.who.raid.ours && lines.Has(LetterPart.Raid, PieceTags(s, LetterKind.Raid, "rd:come", "rd:grim"))) have.Add("pa:raid");
            if (s.who?.artistic == true && lines.Has(LetterPart.Poem, PieceTags(s, LetterKind.Poem, ShortTag))) have.Add("pa:poem");
            have.Add("pa:story");
            bool life = lines.Has(LetterPart.Part, PieceTags(s, LetterKind.Long, "pa:life"));
            if (have.Contains("pa:news")) bodies.Add("pa:news");
            var rest = new List<string>(have);
            rest.Remove("pa:news");
            Shuffle(rest, roll);
            while (bodies.Count < n)
            {
                if (rest.Count > 0 && (roll() < 0.6f || !life))
                {
                    bodies.Add(rest[0]);
                    rest.RemoveAt(0);
                }
                else if (life) bodies.Add("pa:life");
                else break;
            }
            int made = 0;
            var seen = new HashSet<string>();
            for (int i = 0; i < bodies.Count; i++)
            {
                var part = new List<string>();
                switch (bodies[i])
                {
                    case "pa:news":
                        Add(part, lines.Pick(LetterPart.Part, PieceTags(s, LetterKind.Long, bodies[i])));
                        break;
                    case "pa:life":
                        // Everyday life, a paragraph or two.
                        int paras = roll() < 0.5f ? 2 : 1;
                        for (int k = 0; k < paras + 2 && part.Count < paras; k++)
                        {
                            string life2 = lines.Pick(LetterPart.Part, PieceTags(s, LetterKind.Long, bodies[i]));
                            if (!string.IsNullOrEmpty(life2) && !seen.Contains(life2.Trim()) && !part.Contains(life2.Trim())) Add(part, life2);
                        }
                        break;
                    case "pa:story":
                        Story(s, lines, roll, part, LetterKind.Long, false);
                        break;
                    case "pa:poem":
                        Add(part, lines.Pick(LetterPart.Poem, PieceTags(s, LetterKind.Poem, ShortTag)));
                        break;
                    case "pa:raid":
                        Raid(s, lines, roll, part, true);
                        break;
                }
                part.RemoveAll(p => seen.Contains(p));
                if (part.Count == 0) continue;
                foreach (var p in part) seen.Add(p);
                // A heading now and then: the first part's seldom, the last's often.
                string order = made == 0 ? "pa:first" : i == bodies.Count - 1 ? "pa:last" : "pa:next";
                if (roll() < (made == 0 ? 0.35f : 0.75f))
                {
                    // A heading names what follows by ph: (the body's own lines are pa:), so a body never stands for a heading.
                    // Never the same heading twice in one letter.
                    var headTags = PieceTags(s, LetterKind.Long, "pa:head", order, "ph:" + bodies[i].Substring(3));
                    string head = null;
                    for (int k = 0; k < 3 && (head == null || seen.Contains(head)); k++)
                    {
                        string h = lines.Pick(LetterPart.Part, headTags);
                        head = string.IsNullOrEmpty(h) ? null : Lines(h).Trim();
                    }
                    if (!string.IsNullOrEmpty(head) && seen.Add(head)) part.Insert(0, head);
                }
                foreach (var p in part)
                    foreach (var q in Paragraphs(p))
                        Add(piece, q);
                made++;
            }
            return made >= 2;
        }

        /// <summary>
        /// A chronicle: its lead-in now and then, the masthead in the writer's age (a broadsheet, a bulletin, a crier's
        /// roll, a tribe's season told, the Empire's gazette), up to four items each about one real thing (a subject the
        /// facts hold; never two on one subject), a notice or two of everyday life, and its foot.
        /// </summary>
        private static bool Chronicle(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece)
        {
            string head = lines.Pick(LetterPart.Chronicle, PieceTags(s, LetterKind.Chronicle, "ch:head"));
            if (string.IsNullOrEmpty(head)) return false;
            var items = new List<string>();
            var subjects = new List<string>(Subjects);
            Shuffle(subjects, roll);
            foreach (var subj in subjects)
            {
                if (items.Count >= 4) break;
                var t = PieceTags(s, LetterKind.Chronicle, "ch:item", subj);
                if (!lines.Has(LetterPart.Chronicle, t)) continue;
                Add(items, lines.Pick(LetterPart.Chronicle, t));
            }
            int notices = items.Count >= 3 ? 1 : 2;
            for (int i = 0; i < notices; i++) Add(items, lines.Pick(LetterPart.Chronicle, PieceTags(s, LetterKind.Chronicle, "ch:notice")));
            if (items.Count < 2) return false;
            Lead(s, lines, roll, piece, LetterKind.Chronicle, 0.7f);
            Add(piece, head);
            foreach (var i in items) Add(piece, i);
            Add(piece, lines.Pick(LetterPart.Chronicle, PieceTags(s, LetterKind.Chronicle, "ch:end")));
            return true;
        }

        /// <summary>A petition: its lead-in, the body by what is asked, and its signatures (real names: the writer's townsfolk the colony knows, or the colonists).</summary>
        private static bool Petition(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece)
        {
            string body = lines.Pick(LetterPart.Set, PieceTags(s, LetterKind.Petition, "pe:body"));
            if (string.IsNullOrEmpty(body)) return false;
            Lead(s, lines, roll, piece, LetterKind.Petition, 0.8f);
            foreach (var p in Paragraphs(body)) Add(piece, p);
            string signed = s.values.TryGetValue("SIGNERS", out var v) ? v : null;
            if (!string.IsNullOrEmpty(signed)) Add(piece, signed);
            return true;
        }

        /// <summary>A list or an account: its heading, three to five items, one line each, and its foot.</summary>
        private static bool Account(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece)
        {
            string head = lines.Pick(LetterPart.Set, PieceTags(s, LetterKind.Account, "ac:head"));
            if (string.IsNullOrEmpty(head)) return false;
            var items = new List<string>();
            int n = 3 + (int)(roll() * 3f);
            // What is asked first, when it is.
            if (Asks(s)) Add(items, lines.Pick(LetterPart.Set, PieceTags(s, LetterKind.Account, "ac:item", "ac:asked")));
            for (int i = 0; items.Count < n && i < n + 3; i++)
            {
                string it = lines.Pick(LetterPart.Set, PieceTags(s, LetterKind.Account, "ac:item"));
                if (!string.IsNullOrEmpty(it) && !items.Contains(it.Trim())) Add(items, it);
            }
            if (items.Count < 2) return false;
            Lead(s, lines, roll, piece, LetterKind.Account, 0.6f);
            Add(piece, head + "\n" + string.Join("\n", items));
            Add(piece, lines.Pick(LetterPart.Set, PieceTags(s, LetterKind.Account, "ac:end")));
            return true;
        }

        /// <summary>The separator between a riddle and its answer in its line ("What has a neck ... ~~ A bottle.").</summary>
        public const string AnswerMark = "~~";

        /// <summary>A riddle: its lead-in, the riddle, a line that the answer comes next time; the answer kept (<see cref="LetterSpec.riddleAnswer"/>).</summary>
        private static bool Riddle(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece)
        {
            string line = lines.Pick(LetterPart.Set, PieceTags(s, LetterKind.Riddle, "ri:riddle"));
            if (string.IsNullOrEmpty(line)) return false;
            SplitRiddle(line, out string riddle, out string answer);
            if (string.IsNullOrEmpty(riddle)) return false;
            Lead(s, lines, roll, piece, LetterKind.Riddle, 0.85f);
            foreach (var p in Paragraphs(riddle)) Add(piece, p);
            Add(piece, lines.Pick(LetterPart.Set, PieceTags(s, LetterKind.Riddle, "ri:later")));
            s.riddleAnswer = answer;
            return true;
        }

        /// <summary>A riddle's line taken apart: the riddle, and its answer after <see cref="AnswerMark"/> (null when it has none).</summary>
        public static void SplitRiddle(string line, out string riddle, out string answer)
        {
            riddle = line?.Trim();
            answer = null;
            if (string.IsNullOrEmpty(line)) return;
            int i = line.IndexOf(AnswerMark, StringComparison.Ordinal);
            if (i < 0) return;
            riddle = line.Substring(0, i).Trim();
            answer = line.Substring(i + AnswerMark.Length).Trim();
        }

        /// <summary>Others at home add a line each under the signature (one to three, by name or as they are called at home).</summary>
        private static bool Hands(LetterSpec s, LetterWords w, LetterLines lines, Func<float> roll, List<string> after)
        {
            var who = s.who;
            if (who == null || who.hands.Count == 0) return false;
            int n = Math.Min(who.hands.Count, 1 + (int)(roll() * 3f));
            var said = new HashSet<string>();
            for (int i = 0; i < n; i++)
            {
                var t = PieceTags(s, LetterKind.Hands, "ha:line");
                string line = lines.Pick(LetterPart.Hands, t);
                if (string.IsNullOrEmpty(line) || !said.Add(line)) continue;
                after.Add(Texts.Fill(w.adds, ("HAND", Texts.Capitalize(who.hands[i])), ("LINE", line.Trim())));
            }
            return after.Count > 0;
        }

        /// <summary>Something folded in with the letter, described under the signature.</summary>
        private static bool Enclosure(LetterSpec s, LetterLines lines, Func<float> roll, List<string> after)
        {
            string line = lines.Pick(LetterPart.Enclosure, PieceTags(s, LetterKind.Enclosure, "en:line"));
            if (string.IsNullOrEmpty(line)) return false;
            Add(after, line);
            return true;
        }

        /// <summary>
        /// A diary page copied out (its lead-in, two or three days, each "The 4th day." and the day's entry), or the letter
        /// broken off and taken up again (a line that breaks it off, a line that takes it up the next day, and a paragraph of
        /// everyday life).
        /// </summary>
        private static bool Diary(LetterSpec s, LetterLines lines, Func<float> roll, List<string> piece)
        {
            bool broken = roll() < 0.4f;
            if (broken)
            {
                string brk = lines.Pick(LetterPart.Diary, PieceTags(s, LetterKind.Diary, "di:break"));
                string resume = lines.Pick(LetterPart.Diary, PieceTags(s, LetterKind.Diary, "di:resume"));
                string life = lines.Pick(LetterPart.Part, PieceTags(s, LetterKind.Long, "pa:life"));
                if (!string.IsNullOrEmpty(brk) && !string.IsNullOrEmpty(resume))
                {
                    Add(piece, brk);
                    Add(piece, resume);
                    Add(piece, life);
                    return true;
                }
            }
            var days = new List<string>();
            for (int i = 1; i <= 3; i++)
                if (s.values.TryGetValue("DAY" + i, out var d) && !string.IsNullOrEmpty(d))
                    days.Add(d);
            if (days.Count == 0) return false;
            int n = Math.Min(days.Count, 2 + (roll() < 0.5f ? 1 : 0));
            var entries = new List<string>();
            var seen = new HashSet<string>();
            for (int i = 0; i < n; i++)
            {
                string e = lines.Pick(LetterPart.Diary, PieceTags(s, LetterKind.Diary, "di:entry", i == n - 1 ? "di:latest" : null));
                if (string.IsNullOrEmpty(e) || !seen.Add(e)) continue;
                entries.Add(days[days.Count - n + i] + " " + e.Trim());
            }
            if (entries.Count < 2) return false;
            Lead(s, lines, roll, piece, LetterKind.Diary, 0.9f);
            foreach (var e in entries) Add(piece, e);
            return true;
        }

        // ================= Small rules =================

        /// <summary>
        /// The days a diary page covers, oldest first: up to <paramref name="count"/> days ending today, in the writer's
        /// way of dating (the days of this quadrum only; a tribe's by mornings).
        /// </summary>
        public static List<string> DiaryDays(DateStyle style, LetterWords w, int day, string quadrum, int quadrumIndex, int year, int count)
        {
            w ??= new LetterWords();
            var list = new List<string>();
            int first = Math.Max(1, day - count + 1);
            int n = 0;
            for (int d = first; d <= day; d++, n++)
            {
                switch (style)
                {
                    case DateStyle.Season:
                        var mornings = w.diarySeason.Split('|');
                        list.Add(mornings[Math.Min(n, mornings.Length - 1)].Trim());
                        break;
                    case DateStyle.Courtly:
                        list.Add(Texts.Fill(w.diaryCourtly, ("ORD", LetterFormMath.Ordinal(d))));
                        break;
                    case DateStyle.Plain:
                        list.Add(Texts.Fill(w.diaryPlain, ("DAY", d.ToString()), ("QUADRUM", quadrum ?? "")));
                        break;
                    default:
                        list.Add(Texts.Fill(w.diaryStandard, ("YEAR", year.ToString()), ("Q", (quadrumIndex + 1).ToString()), ("DD", d.ToString("00"))));
                        break;
                }
            }
            return list;
        }

        /// <summary>Who signed a petition, in words: "Signed: A, B and C", or with "and many others of {TOWN}" when more signed than are named.</summary>
        public static string Signed(IList<string> names, bool more, string town, LetterWords w)
        {
            w ??= new LetterWords();
            if (names == null || names.Count == 0) return null;
            string list = JoinAnd(names, w.and);
            return more ? Texts.Fill(w.signedMore, ("NAMES", list), ("TOWN", town ?? "")) : Texts.Fill(w.signed, ("NAMES", list));
        }

        /// <summary>"A", "A and B", "A, B and C".</summary>
        public static string JoinAnd(IList<string> names, string and)
        {
            if (names == null || names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            var sb = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append(i == names.Count - 1 ? " " + (and ?? "and") + " " : ", ");
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        /// <summary>A piece's paragraphs: authored with "\n\n" between them (a poem's lines with "\n"), read either way.</summary>
        public static List<string> Paragraphs(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text)) return list;
            string t = Lines(text);
            foreach (var p in t.Split(new[] { "\n\n" }, StringSplitOptions.None))
            {
                string q = p.Trim('\n', ' ');
                if (q.Length > 0) list.Add(q);
            }
            return list;
        }

        /// <summary>A pool line's written line breaks ("\n" as two characters, as the defs hold it) as real ones.</summary>
        public static string Lines(string text) => string.IsNullOrEmpty(text) ? text ?? "" : text.Replace("\\n", "\n");

        /// <summary>A shuffle by the dice given.</summary>
        private static void Shuffle<T>(List<T> list, Func<float> roll)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Math.Min(i, (int)(roll() * (i + 1)));
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ================= Reading it in pages =================

        /// <summary>A page of a letter in the reading window holds about this many characters.</summary>
        public const int PageChars = 1400;

        /// <summary>A letter longer than this shows only its first page in the game's own letter, with a note where the rest is.</summary>
        public const int ShowMax = 1800;

        /// <summary>
        /// A letter in pages of about <paramref name="max"/> characters: broken between paragraphs, a paragraph too long
        /// for a page between its lines (a poem's) or its sentences. Nothing lost: the pages joined are the letter.
        /// </summary>
        public static List<string> Pages(string text, int max = PageChars)
        {
            var pages = new List<string>();
            if (string.IsNullOrEmpty(text)) return pages;
            var units = new List<(string text, string sep)>();
            var paras = text.Split(new[] { "\n\n" }, StringSplitOptions.None);
            for (int i = 0; i < paras.Length; i++)
            {
                string p = paras[i];
                if (p.Length <= max) units.Add((p, "\n\n"));
                else
                {
                    var bits = Split(p, max);
                    for (int b = 0; b < bits.Count; b++) units.Add((bits[b].text, b == bits.Count - 1 ? "\n\n" : bits[b].sep));
                }
            }
            var cur = new StringBuilder();
            string lastSep = "";
            foreach (var (t, sep) in units)
            {
                if (cur.Length > 0 && cur.Length + lastSep.Length + t.Length > max)
                {
                    pages.Add(cur.ToString());
                    cur.Clear();
                }
                else if (cur.Length > 0) cur.Append(lastSep);
                cur.Append(t);
                lastSep = sep;
            }
            if (cur.Length > 0) pages.Add(cur.ToString());
            return pages;
        }

        /// <summary>A paragraph too long for a page in pieces: between its lines, else after its sentences.</summary>
        private static List<(string text, string sep)> Split(string p, int max)
        {
            var list = new List<(string, string)>();
            string sep = p.Contains("\n") ? "\n" : " ";
            var parts = sep == "\n" ? p.Split('\n') : SplitSentences(p);
            var cur = new StringBuilder();
            foreach (var part in parts)
            {
                if (cur.Length > 0 && cur.Length + sep.Length + part.Length > max)
                {
                    list.Add((cur.ToString(), sep));
                    cur.Clear();
                }
                else if (cur.Length > 0) cur.Append(sep);
                cur.Append(part);
            }
            if (cur.Length > 0) list.Add((cur.ToString(), sep));
            return list;
        }

        private static string[] SplitSentences(string p)
        {
            var list = new List<string>();
            int start = 0;
            for (int i = 0; i < p.Length - 1; i++)
                if ((p[i] == '.' || p[i] == '?') && p[i + 1] == ' ')
                {
                    list.Add(p.Substring(start, i + 1 - start));
                    start = i + 2;
                }
            if (start < p.Length) list.Add(p.Substring(start));
            return list.ToArray();
        }

        // ================= Writing back =================

        /// <summary>
        /// What a letter back is for (the player, 10 November 2026: "i should also be able start a reply right from this
        /// window"): the answer their letter written first waits for (news, a request, an invitation) while it can be
        /// written; else greetings, when they can be written now (news, greetings, a friend's word and any answer to the
        /// colony's are answered with greetings); else whatever can be written to them now.
        /// </summary>
        public static ReplyAs ReplyOf(bool theirLetterWaits, bool greetingsOpen) => theirLetterWaits ? ReplyAs.Answer : greetingsOpen ? ReplyAs.Greetings : ReplyAs.Any;

        // ================= Not twice =================

        /// <summary>The long pieces remembered across the game, at most (LetterBook's list, saved).</summary>
        public const int MaxUsed = 200;

        /// <summary>The key a piece is remembered by: its first words as written (unfilled, tags removed).</summary>
        public static string Key(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            string t = line.Trim();
            if (t.StartsWith("[", StringComparison.Ordinal))
            {
                int e = t.IndexOf(']');
                if (e > 0) t = t.Substring(e + 1).Trim();
            }
            return t.Length > 60 ? t.Substring(0, 60) : t;
        }

        /// <summary>A piece remembered: the newest last, the oldest let go past <see cref="MaxUsed"/>; one already there moves to the end.</summary>
        public static void Remember(List<string> used, string key, int max = MaxUsed)
        {
            if (used == null || string.IsNullOrEmpty(key)) return;
            used.Remove(key);
            used.Add(key);
            while (used.Count > max) used.RemoveAt(0);
        }

        /// <summary>The pools whose lines are long pieces, remembered across the game so none comes round again soon.</summary>
        public static bool Remembered(LetterPart part) =>
            part == LetterPart.Poem || part == LetterPart.Sermon || part == LetterPart.Story || part == LetterPart.Raid || part == LetterPart.Set
            || part == LetterPart.Chronicle || part == LetterPart.Part || part == LetterPart.Diary;
    }
}
