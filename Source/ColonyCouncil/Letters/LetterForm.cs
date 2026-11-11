using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's side of the letter's form (Letters/LetterFormMath.cs): reads the writer (their age, people, culture and
    /// kind, schooling, humour, pride and trust, the colony's writer's voice), how the two sides stand and what the letter is
    /// for, the true facts about the colony and their town, the date and the season where it is written, and makes the
    /// letter: an answer to the colony's letter (a letter, a call's transcript, or words passed on by mouth), the colony's own
    /// letter as its writer finishes it, the world writing first, and a friend's word. Lines come from the pools
    /// Rimfolk_Pool_Letter*, never one this session drew lately (LineMemory, Flavor.Memory) nor one already in a kept letter
    /// (LetterBook), while another fits. The setting "Letters written out in full" (on) turns it off for the short form.
    /// </summary>
    public static partial class LetterForm
    {
        /// <summary>"Letters written out in full" (on): off, answers read as they did, and the colony's letters aren't written out.</summary>
        public static bool Full => (ColonyCouncilMod.Settings?.lettersInFull ?? true) && NeighbourLetters.On;

        private static LetterWords words;

        /// <summary>The form's fixed words, from the keys, read once.</summary>
        public static LetterWords Words => words ??= new LetterWords
        {
            heading = K("CC_LetterForm_Heading"), headingCourt = K("CC_LetterForm_HeadingCourt"), dateCourtly = K("CC_LetterForm_DateCourtly"),
            datePlain = K("CC_LetterForm_DatePlain"), dateStandard = K("CC_LetterForm_DateStandard"), ps = K("CC_LetterForm_PS"), mark = K("CC_LetterForm_Mark"),
            callLine = K("CC_LetterForm_CallLine"), quote = K("CC_LetterForm_Quote"), saluteFallback = K("CC_LetterForm_SaluteFallback"),
            closeFallback = K("CC_LetterForm_CloseFallback"), signPerson = K("CC_LetterForm_SignPerson"), signUntitled = K("CC_LetterForm_SignUntitled"),
            signCouncil = K("CC_LetterForm_SignCouncil"), signNoble = K("CC_LetterForm_SignNoble"), signOurs = K("CC_LetterForm_SignOurs"),
            signOursTitled = K("CC_LetterForm_SignOursTitled"), signMark = K("CC_LetterForm_SignMark"), signHelped = K("CC_LetterForm_SignHelped"),
            signMany = K("CC_LetterForm_SignMany"), machineHead = K("CC_LetterForm_MachineHead"), machinePoint = K("CC_LetterForm_MachinePoint"),
            machinePs = K("CC_LetterForm_MachinePS"),
            // The shapes (Letters/LetterKindMath.cs).
            signShort = K("CC_LetterForm_SignShort"), adds = K("CC_LetterForm_Adds"), answer = K("CC_LetterForm_Answer"), goesOnOne = K("CC_LetterForm_GoesOnOne"),
            goesOnMany = K("CC_LetterForm_GoesOnMany"), diaryCourtly = K("CC_LetterForm_DiaryCourtly"), diaryPlain = K("CC_LetterForm_DiaryPlain"),
            diaryStandard = K("CC_LetterForm_DiaryStandard"), diarySeason = K("CC_LetterForm_DiarySeason"), signed = K("CC_LetterForm_Signed"),
            signedMore = K("CC_LetterForm_SignedMore"), and = K("CC_LetterForm_And")
        };

        private static string K(string key) => key.Translate().Resolve();

        private static Map Home => HappeningTracker.HomeMap() ?? Homes.Seat;

        // ================= Their answer =================

        /// <summary>What an answer is made from, beside the letter and the notable.</summary>
        public sealed class AnswerFacts
        {
            public Pawn speaker;
            public AnswerTone tone;
            /// <summary>The answer pool's line, filled (their heart unless their speech has its own).</summary>
            public string quote;
            /// <summary>A call's turn: null (a letter or a call made), "callback", "word".</summary>
            public string how;
            public FactSet facts;
            /// <summary>The narrator's lines under it (what it brings, why, how far it moved them), in order.</summary>
            public readonly List<string> outcomes = new List<string>();
            /// <summary>Tags for a heart of the Heart pool: a council's yes or no and its dealing's kind, a reply's kind.</summary>
            public readonly HashSet<string> heartTags = new HashSet<string>();
            /// <summary>What came with it, for the correspondence's threads (Letters/LetterThreads.cs): "aid" (help sent), "aid:no" (help refused), "gift"; and in words.</summary>
            public string sent, sentWhat;
        }

        /// <summary>
        /// Their answer, made on the form: a letter, a call's transcript (on the console) or words passed on by its carrier
        /// (an answer to the colony's spoken message), with the narrator's lines under it. Kept in the letter book as the
        /// exchange's answer.
        /// </summary>
        public static LetterText Answer(NeighbourLetter l, Notable n, AnswerFacts a)
        {
            var s = new LetterSpec { ours = false, tone = a.tone };
            s.shape = l.channel == LetterChannel.Console ? LetterShape.Call : l.spoken ? LetterShape.Spoken : LetterShape.Written;
            string purpose = PurposeTag(l, false);
            Theirs(s, n, purpose, l.hostile || NotableBook.Hostile(n), a.facts);
            s.values["TO"] = a.speaker?.LabelShort ?? l.WriterName;
            s.values["TOPIC"] = l.topic ?? "";
            s.values["HELP"] = HelpWord(l.help);
            if (l.help == "food" || l.help == "medicine") s.tags.Add(l.help);
            s.tags.Add(s.shape == LetterShape.Call ? "call" : "letter");
            s.tags.UnionWith(a.heartTags);
            // The heart: their speech's own, or the council's, the reply's thanks; else the answer pool's line.
            string speech = LetterFormMath.SpeechOf(s);
            bool own = LetterFormMath.HeartSpeech(speech) != null || purpose == "council" || purpose == "thanks";
            s.heart = own ? PickFilled(LetterPart.Heart, LetterFormMath.HeartTags(s), s) : null;
            if (s.heart.NullOrEmpty()) s.heart = a.quote;
            // Passed on by someone who isn't the head (World/LetterRoutes.cs): the head says who brought it, first.
            string passed = LetterRoutes.PassLine(l, n, speech == LetterFormMath.Olde);
            if (!passed.NullOrEmpty()) s.heart = s.heart.NullOrEmpty() ? passed : passed + " " + s.heart;
            if (s.shape == LetterShape.Call)
            {
                s.us = a.speaker?.LabelShort ?? l.WriterName;
                s.them = n.Name;
                s.callMode = a.how == "callback" ? "back" : a.how == "word" ? "word" : "call";
                s.callHead = CallHead(s.callMode == "word", n);
            }
            Lead(s, l, n, a);
            s.outcomes.AddRange(a.outcomes);
            // Its shape (Letters/LetterFormShapes.cs): the usual letter, a bare note, a story, a poem, a raid's account...
            ShapeTheirs(s, n, a.speaker ?? l.writer);
            SignNote(s, false, s.values.ContainsKey("TITLE"));
            var t = Compose(s);
            LetterBook.Answered(l, n, t, s.shape);
            // The correspondence as it builds up (Letters/LetterThreads.cs): counted, its threads kept.
            LetterThreads.Corresponded(s, t, n, false, null, a.sent, a.sentWhat);
            return t;
        }

        /// <summary>The narrator's first line: in answer to which letter, and how it came.</summary>
        private static void Lead(LetterSpec s, NeighbourLetter l, Notable n, AnswerFacts a)
        {
            string key = s.shape == LetterShape.Call
                ? a.how == "callback" ? "CC_LetterForm_Lead_Back" : a.how == "word" ? "CC_LetterForm_Lead_Word" : "CC_LetterForm_Lead_Call"
                : s.shape == LetterShape.Spoken ? "CC_LetterForm_Lead_Spoken" : l.hostile ? "CC_LetterForm_Lead_War" : "CC_LetterForm_Lead";
            s.outcomes.Add(key.Translate(n.Name.Named("NAME"), (a.speaker?.LabelShort ?? l.WriterName).Named("PAWN"),
                ("CC_Letter_Purpose_" + l.purpose).Translate().Resolve().Named("PURPOSE"), (n.townName ?? n.faction?.Name ?? "").Named("TOWN")).Resolve());
        }

        /// <summary>A call's log head, in the console's age (the colony's means: industrial, spacer).</summary>
        private static string CallHead(bool word, Notable n)
        {
            var home = Home;
            string date = DateText(Ages.Ours, false, home?.Tile ?? PlanetTile.Invalid, null);
            string key = word ? "CC_LetterForm_CallHead_Word" : Ages.Ours >= WorldAge.Spacer ? "CC_LetterForm_CallHead_Spacer" : "CC_LetterForm_CallHead";
            return key.Translate(ColonyName(home).Named("PLACE"), (n.townName ?? n.faction?.Name ?? "?").Named("OTHER"), date.Named("DATE")).Resolve();
        }

        // ================= The colony's own letter =================

        /// <summary>
        /// The colony's letter as its writer finishes it (a spoken message as it is given): in the writer's voice, for its
        /// purpose and topic, by how the colony stands with them, the colony's age and what is true at home. Kept in the
        /// letter book; null with the setting off or no writer.
        /// </summary>
        public static LetterText Ours(NeighbourLetter l, Pawn writer)
        {
            // Addressed to the one it is for: a town's matter given to someone else to pass on is the head's (World/LetterRoutes.cs).
            var n = NeighbourLetters.ForWhom(l);
            if (!Full || l == null || n == null || l.channel == LetterChannel.Console) return null;
            try
            {
                var map = l.map ?? writer?.MapHeld ?? Home;
                string purpose = PurposeTag(l, true);
                bool war = l.hostile || NotableBook.Hostile(n);
                var f = n.faction;
                var s = new LetterSpec
                {
                    ours = true, shape = l.spoken ? LetterShape.Spoken : LetterShape.Written, age = Ages.Ours, empire = Ages.IsEmpire(f),
                    council = purpose == "dealing" && l.toName != null && DealingLetters.ToName(n, l.topicKey) != null, penFriend = PenFriends.Is(n), war = war,
                    purpose = purpose
                };
                int regard = NotableBook.Opinion(n);
                s.tone = LetterFormMath.Stance(regard);
                s.motive = LetterFormMath.Motive(true, war, s.penFriend, s.council, s.empire, purpose, regard, false, 0, 0);
                string helper = null;
                if (writer != null)
                {
                    var sk = writer.skills;
                    int social = sk?.GetSkill(SkillDefOf.Social)?.Level ?? 6, intel = sk?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 6;
                    var traits = writer.story?.traits;
                    var sides = Sides(writer);
                    int mood = Mood(writer, sides);
                    s.voice = LetterFormMath.Voice(social, intel, traits?.HasTrait(TraitDefOf.Kind) ?? false, traits?.HasTrait(TraitDefOf.Abrasive) ?? false, mood,
                        sides != null && sides[(int)PersonaAxis.Pride] >= 40f);
                    s.humour = sides != null ? (int)sides[(int)PersonaAxis.Humour] : 0;
                    s.strange = Culture(writer, s.tags);
                    s.schooling = LetterFormMath.SchoolingOf(intel, false, HasTrait(writer, LetterCultureMath.SlowLearner),
                        HasTrait(writer, LetterCultureMath.TooSmart) || HasTrait(writer, LetterCultureMath.Scholar), s.age, false, false);
                    // Not schooled at all: half the time someone a little better writes it down for them.
                    if (s.schooling == Schooling.None && s.strange == null && LetterFormMath.Helped(true, Rand.Value)) helper = Helper(writer, map);
                    if (helper != null) s.schooling = Schooling.Some;
                }
                // What is true at home, for anything the letter says of the colony.
                var facts = Facts.For(new FactScene { speaker = writer, listener = n.pawn, colony = map, people = f, place = NotableBook.TownOf(n.town), asks = OursAsks });
                s.tags.UnionWith(facts.tags);
                foreach (var kv in facts.values) s.values[kv.Key] = kv.Value;
                Season(s.tags, map?.Tile ?? PlanetTile.Invalid);
                Weather(s.tags, map);
                Peoples.AddTags(f, s.tags);
                if (l.help == "food" || l.help == "medicine") s.tags.Add(l.help);
                // The purpose's own words and tags: the dealing's kind and its name, a ward asked or offered, a reply's kind.
                Topics(s, l, n);
                string colony = ColonyName(map);
                s.values["FROM"] = writer?.LabelShort ?? l.WriterName;
                s.values["TO"] = n.Name;
                s.values["TOWN"] = n.townName ?? f?.Name ?? "";
                s.values["COLONY"] = colony;
                s.values["PEOPLE"] = f?.Name ?? "";
                // Their office only, never how the colony knows them ("a trader who called"): a salutation's {TOTITLE}.
                string office = NotableBook.OfficeOf(n);
                if (!office.NullOrEmpty()) s.values["TOTITLE"] = office;
                s.values["PURPOSE"] = ("CC_Letter_Purpose_" + l.purpose).Translate().Resolve();
                s.values["TOPIC"] = l.topic ?? "";
                s.values["HELP"] = HelpWord(l.help);
                if (s.council) s.values["COUNCIL"] = DealingLetters.ToName(n, l.topicKey) ?? "";
                if (helper != null) s.values["HELPER"] = helper;
                var title = writer != null ? WorldMechanics.Title(writer) : null;
                if (title != null && !title.label.NullOrEmpty()) s.values["TITLE"] = title.label.CapitalizeFirst();
                s.heart = PickFilled(LetterPart.Ours, LetterFormMath.HeartTags(s), s);
                s.place = colony;
                s.date = DateText(s.age, false, map?.Tile ?? PlanetTile.Invalid, s);
                string speech = LetterFormMath.SpeechOf(s);
                s.sign = LetterFormMath.Signature(LetterFormMath.SignOf(true, false, false, title != null, speech, helper != null), Words, s.values);
                // Its shape, by the writer (Letters/LetterFormShapes.cs): a verse from a poet, a little preaching from a zealot...
                ShapeOurs(s, l, n, writer, map);
                SignNote(s, true, title != null);
                // Given to someone else to pass on: the narrator says so under it.
                var via = l.passTo >= 0 && l.passTo != l.notable ? NotableBook.Instance?.Get(l.notable) : null;
                if (via != null) s.outcomes.Add("CC_Route_OursNote".Translate(via.Name.Named("NAME"), n.Name.Named("HEAD")).Resolve());
                var t = Compose(s);
                LetterBook.Written(l, n, writer, t);
                LetterThreads.Corresponded(s, t, n, true, writer?.LabelShort ?? l.WriterName);
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: the colony's letter", e);
                return null;
            }
        }

        private static readonly string[] OursAsks = { FactsMath.Colony, FactsMath.Hospital, FactsMath.Conditions, FactsMath.Ties };

        /// <summary>A purpose's own values and tags in the colony's letter (from its topic key, what the letter's window chose).</summary>
        private static void Topics(LetterSpec s, NeighbourLetter l, Notable n)
        {
            string key = l.topicKey ?? "";
            try
            {
                if (l.purpose == LetterPurpose.Dealing)
                {
                    var ask = WorldWrites.AskOf(key);
                    if (ask != null)
                    {
                        s.tags.Add("k:" + ask.kind.ToString().ToLowerInvariant());
                        s.tags.Add(l.help == DealingLetters.ReplyPrefix + "yes" ? "yes" : "no");
                        s.values["GOODS"] = WorldWrites.GoodsWord(ask);
                    }
                    else if (DealingMath.ParseTopic(key, out var k, out byte sub, out _))
                    {
                        s.tags.Add("deal:" + k.ToString().ToLowerInvariant());
                        s.values["DEAL"] = Dealings.Label(k, sub).UncapitalizeFirst();
                    }
                }
                else if (l.purpose == LetterPurpose.Condolences && key.StartsWith("loss:", StringComparison.Ordinal)) s.values["LOSTNAME"] = key.Substring(5);
                else if (key == WardMath.AskTopic) s.tags.Add("ward:ask");
                else if (key == WardMath.SendTopic) s.tags.Add("ward:send");
                else if (l.purpose == LetterPurpose.Ransom && RansomMath.ParseRansom(key, out int captive)) s.values["CAPTIVE"] = PawnName(captive, l.map);
                else if (l.purpose == LetterPurpose.Ransom && RansomMath.ParseExchange(key, out int ours, out int theirs))
                {
                    s.values["OURS"] = PawnName(ours, l.map);
                    s.values["THEIRS"] = PawnName(theirs, l.map);
                }
                else if (l.purpose == LetterPurpose.Pact && !l.topic.NullOrEmpty()) s.values["PACT"] = l.topic.UncapitalizeFirst();
                else if (l.purpose == LetterPurpose.Envoy && n.faction != null) s.values["ENVOY"] = NotableBook.Called(Envoys.EnvoyOf(n.faction, l.map ?? Home));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a purpose's words", e);
            }
        }

        /// <summary>A pawn's short name by its id, at home or anywhere the game still keeps it; empty when gone.</summary>
        private static string PawnName(int id, Map map)
        {
            var p = map?.mapPawns?.AllPawns?.FirstOrDefault(x => x.thingIDNumber == id) ?? Find.WorldPawns?.AllPawnsAliveOrDead?.FirstOrDefault(x => x.thingIDNumber == id);
            return p?.LabelShort ?? "";
        }

        /// <summary>Who writes it down for one who can't: the free colonist at home who writes best (Intellectual 6 or more), or null.</summary>
        private static string Helper(Pawn writer, Map map)
        {
            if (map == null) return null;
            return NeighbourLetters.Writers(map, true).Where(p => p != writer && (p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0) >= 6)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Intellectual).Level).FirstOrDefault()?.LabelShort;
        }

        // ================= They write first, and a friend's word =================

        /// <summary>
        /// The world writing first (Dealings/WorldWrites.cs): their letter, news, a request or an invitation, by what the
        /// writer wants (a trader angling, a wary neighbour, a proud one, a duty, a friend), with the narrator's lines under
        /// it. Null with the setting off.
        /// </summary>
        public static LetterText First(Notable n, ReplyKind kind, string goods, List<string> outcomes, int askId)
        {
            if (!Full || n == null) return null;
            try
            {
                int regard = NotableBook.Opinion(n);
                var s = new LetterSpec { ours = false, shape = LetterShape.Written, tone = regard >= 40 ? AnswerTone.Warm : AnswerTone.Cordial };
                Theirs(s, n, "first", false, Facts.For(FactScene.Letter(n, null, Home, false)));
                s.tags.Add("k:" + kind.ToString().ToLowerInvariant());
                s.values["TO"] = ColonyWriter() ?? ColonyName(Home);
                if (!goods.NullOrEmpty()) s.values["GOODS"] = goods;
                s.heart = PickFilled(LetterPart.Heart, LetterFormMath.HeartTags(s), s);
                s.outcomes.AddRange(outcomes);
                ShapeTheirs(s, n, ColonyWriterPawn());
                SignNote(s, false, s.values.ContainsKey("TITLE"));
                var t = Compose(s);
                LetterBook.TheirFirst(n, askId, t);
                LetterThreads.Corresponded(s, t, n, false);
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: the world writes first", e);
                return null;
            }
        }

        /// <summary>A pen friend's word (friendmade, friendfavour, raidword {RAIDERS}), with the narrator's lines under it. Null with the setting off.</summary>
        public static LetterText Friend(Notable n, string purpose, List<string> outcomes, string raiders = null)
        {
            if (!Full || n == null) return null;
            try
            {
                var s = new LetterSpec { ours = false, shape = LetterShape.Written, tone = AnswerTone.Warm };
                Theirs(s, n, purpose, false, Facts.For(FactScene.Letter(n, null, Home, false)));
                s.penFriend = true;
                s.motive = LetterMotive.Friend;
                if (purpose == "raidword") s.grave = true;
                s.values["TO"] = LastWriter(n) ?? ColonyWriter() ?? ColonyName(Home);
                if (!raiders.NullOrEmpty()) s.values["RAIDERS"] = raiders;
                s.heart = PickFilled(LetterPart.Heart, LetterFormMath.HeartTags(s), s);
                s.outcomes.AddRange(outcomes);
                ShapeTheirs(s, n, ColonyWriterPawn());
                SignNote(s, false, s.values.ContainsKey("TITLE"));
                var t = Compose(s);
                LetterBook.Note(n, t);
                LetterThreads.Corresponded(s, t, n, false);
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a friend's word", e);
                return null;
            }
        }

        // ================= Their side, read once a letter =================

        /// <summary>
        /// Everything of the writer and their side: their age, the Empire, a council, a pen friend, war, hard times; their
        /// culture and kind (a strange voice), schooling, humour; what they want; the season and land at their town; their
        /// people's ways; the facts' tags and values; the heading, date and signature.
        /// </summary>
        private static void Theirs(LetterSpec s, Notable n, string purpose, bool war, FactSet facts)
        {
            var f = n.faction;
            var pawn = n.pawn;
            var town = NotableBook.TownOf(n.town);
            s.purpose = purpose;
            s.age = Ages.Of(f);
            s.empire = Ages.IsEmpire(f);
            s.council = purpose == "council";
            s.penFriend = PenFriends.Is(n);
            s.war = war;
            s.grave = WorldSituations.Instance?.HardTimes(f) ?? false;
            var sides = pawn != null ? Sides(pawn) : null;
            s.humour = sides != null ? (int)sides[(int)PersonaAxis.Humour] : 0;
            int pride = sides != null ? (int)sides[(int)PersonaAxis.Pride] : 0, trust = sides != null ? (int)sides[(int)PersonaAxis.Trust] : 0;
            if (pawn != null && !s.council) s.strange = Culture(pawn, s.tags);
            bool outlaw = f?.def != null && LetterCultureMath.Outlaws.Contains(f.def.defName);
            int intel = pawn?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 6;
            s.schooling = LetterFormMath.SchoolingOf(intel, outlaw, HasTrait(pawn, LetterCultureMath.SlowLearner),
                HasTrait(pawn, LetterCultureMath.TooSmart) || HasTrait(pawn, LetterCultureMath.Scholar), s.age, s.council, s.empire);
            bool helped = false;
            // Not schooled at all: now and then written for them by someone barely better.
            if (s.schooling == Schooling.None && s.strange == null && LetterFormMath.Helped(false, Rand.Value))
            {
                s.schooling = Schooling.Some;
                helped = true;
            }
            string calling = Peoples.CallingOf(f);
            bool trader = n.role == NotableRole.Trader || calling == PeoplesMath.Traders || calling == PeoplesMath.Brokers;
            s.motive = LetterFormMath.Motive(false, war, s.penFriend, s.council, s.empire, purpose, NotableBook.Opinion(n), trader, pride, trust);
            Peoples.AddTags(f, s.tags);
            // Who they are in their town (nr:head, nr:heir, nr:envoy, nr:trader, nr:acquaintance): a line that names the writer as the boss is the head's.
            s.tags.Add("nr:" + n.role.ToString().ToLowerInvariant());
            if (facts != null)
            {
                s.tags.UnionWith(facts.tags);
                foreach (var kv in facts.values) s.values[kv.Key] = kv.Value;
            }
            if (town != null)
            {
                Season(s.tags, town.Tile);
                Land(s.tags, town.Tile);
            }
            string townName = n.townName ?? f?.Name ?? "";
            s.values["FROM"] = n.Name;
            s.values["TOWN"] = townName;
            s.values["COLONY"] = ColonyName(Home);
            s.values["PEOPLE"] = f?.Name ?? "";
            s.values["PURPOSE"] = purpose;
            // Signed with their office only ("Dagna, mayor of Ashford"); one the colony met as a guest signs with their name and town.
            string title = NotableBook.OfficeOf(n);
            if (!title.NullOrEmpty()) s.values["TITLE"] = title;
            if (s.council) s.values["COUNCIL"] = Dealings.Council(f, town);
            if (helped) s.values["HELPER"] = "CC_LetterForm_TheirHelper".Translate().Resolve();
            s.place = townName;
            s.date = DateText(s.age, s.empire, town?.Tile ?? PlanetTile.Invalid, s);
            string speech = LetterFormMath.SpeechOf(s);
            s.sign = LetterFormMath.Signature(LetterFormMath.SignOf(false, s.council, s.empire, !title.NullOrEmpty(), speech, helped), Words, s.values);
        }

        /// <summary>The purpose as lines are tagged: the colony's dealing and reply, a council's answer and their thanks, else the purpose's own name.</summary>
        private static string PurposeTag(NeighbourLetter l, bool ours)
        {
            if (l.purpose == LetterPurpose.Dealing)
            {
                bool reply = l.topicKey != null && l.topicKey.StartsWith(DealingLetters.ReplyPrefix, StringComparison.Ordinal);
                return ours ? reply ? "reply" : "dealing" : reply ? "thanks" : "council";
            }
            return l.purpose.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// A writer's culture and kind: their xenotype (Biotech) and race, their ideoligion's memes (Ideology), each read
        /// softly by defName against LetterCultureMath's tables. The strange voice, or null; flavour tags added.
        /// </summary>
        private static string Culture(Pawn p, HashSet<string> tags)
        {
            try
            {
                string xeno = ModsConfig.BiotechActive ? p.genes?.Xenotype?.defName : null;
                var memes = ModsConfig.IdeologyActive ? p.ideo?.Ideo?.memes?.Where(m => m != null).Select(m => m.defName) : null;
                return LetterCultureMath.Read(xeno, p.def?.defName, memes, tags);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a writer's culture", e);
                return null;
            }
        }

        private static bool HasTrait(Pawn p, string defName)
        {
            var traits = p?.story?.traits?.allTraits;
            if (traits == null) return false;
            foreach (var t in traits)
                if (t?.def?.defName == defName)
                    return true;
            return false;
        }

        private static float[] Sides(Pawn p)
        {
            try
            {
                return Persona.Values(p);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>A writer's outlook, -2 to 2: their persona's, else their natural mood.</summary>
        private static int Mood(Pawn p, float[] sides)
        {
            if (sides != null)
            {
                float o = sides[(int)PersonaAxis.Outlook];
                return o >= 50f ? 2 : o >= 20f ? 1 : o <= -50f ? -2 : o <= -20f ? -1 : 0;
            }
            var mood = DefDatabase<TraitDef>.GetNamedSilentFail("NaturalMood");
            var traits = p.story?.traits;
            return mood != null && traits != null && traits.HasTrait(mood) ? traits.DegreeOfTrait(mood) : 0;
        }

        // ================= Time, place and weather =================

        /// <summary>The date as the writer would put it, at their place, in the form of their age (a tribal writer by the season, drawn from the When pool).</summary>
        private static string DateText(WorldAge age, bool empire, PlanetTile tile, LetterSpec s)
        {
            try
            {
                long abs = Find.TickManager.TicksAbs;
                float lon = tile.Valid ? Find.WorldGrid.LongLatOf(tile).x : 0f;
                var style = LetterFormMath.DateStyleOf(age, empire);
                string when = null;
                if (style == DateStyle.Season)
                {
                    var t = new HashSet<string>();
                    Season(t, tile);
                    if (s != null) t.UnionWith(s.tags);
                    when = Pick(LetterPart.When, t, s?.values ?? new Dictionary<string, string>());
                }
                var q = GenDate.Quadrum(abs, lon);
                return LetterFormMath.Date(style, Words, GenDate.DayOfQuadrum(abs, lon) + 1, q.Label(), (int)q, GenDate.Year(abs, lon), when);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: a date", e);
                return "";
            }
        }

        /// <summary>The season where it is written: season:spring, summer, autumn or winter.</summary>
        private static void Season(HashSet<string> tags, PlanetTile tile)
        {
            if (!tile.Valid) return;
            try
            {
                var season = GenDate.Season(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(tile));
                string tag = season == RimWorld.Season.Spring ? "season:spring" : season == RimWorld.Season.Summer || season == RimWorld.Season.PermanentSummer ? "season:summer"
                    : season == RimWorld.Season.Fall ? "season:autumn" : season == RimWorld.Season.Winter || season == RimWorld.Season.PermanentWinter ? "season:winter" : null;
                if (tag != null) tags.Add(tag);
            }
            catch
            {
            }
        }

        /// <summary>Their land, as the facts service tags a town's (home:desert, home:coast...).</summary>
        private static void Land(HashSet<string> tags, PlanetTile tile)
        {
            try
            {
                var f = Facts.For(new FactScene { asks = LandAsks, place = Find.WorldObjects.SettlementAt(tile) });
                foreach (var t in f.tags)
                    if (t.StartsWith("home:", StringComparison.Ordinal))
                        tags.Add(t);
            }
            catch
            {
            }
        }

        private static readonly string[] LandAsks = { FactsMath.Land };

        /// <summary>The weather at home as the colony's letter is written: weather:rain, snow, fog, storm or clear.</summary>
        private static void Weather(HashSet<string> tags, Map map)
        {
            string w = map?.weatherManager?.curWeather?.defName;
            if (w.NullOrEmpty()) return;
            string tag = w.IndexOf("Thunder", StringComparison.OrdinalIgnoreCase) >= 0 || w.IndexOf("Storm", StringComparison.OrdinalIgnoreCase) >= 0 ? "weather:storm"
                : w.IndexOf("Snow", StringComparison.OrdinalIgnoreCase) >= 0 ? "weather:snow" : w.IndexOf("Rain", StringComparison.OrdinalIgnoreCase) >= 0 ? "weather:rain"
                : w.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0 ? "weather:fog" : w.IndexOf("Clear", StringComparison.OrdinalIgnoreCase) >= 0 ? "weather:clear" : null;
            if (tag != null) tags.Add(tag);
        }

        /// <summary>The colony's name: its home's, else its people's.</summary>
        public static string ColonyName(Map map)
        {
            var home = map?.Parent as Settlement;
            return home != null && home.HasName ? home.Name : Faction.OfPlayer?.Name ?? "";
        }

        private static string HelpWord(string help) => help.NullOrEmpty() ? "" : (help == "food" ? "CC_Letter_Help_Food" : "CC_Letter_Help_Medicine").Translate().Resolve();

        /// <summary>The colonist who writes the colony's letters: the one the letter's window would choose.</summary>
        private static string ColonyWriter() => NeighbourLetters.DefaultWriter(Home)?.LabelShort;

        /// <summary>The colonist who last wrote to them, from the kept letters.</summary>
        private static string LastWriter(Notable n) => LetterBook.LastWriter(n);

        // ================= Lines =================

        /// <summary>The letter made from its spec, its lines drawn here; what of the gods made it into its words goes into the colony's book (Gods/GodTales.cs).</summary>
        private static LetterText Compose(LetterSpec s)
        {
            var text = LetterFormMath.Compose(s, Words, (part, tags) => Pick(part, tags, s.values, s.drawn) is string line ? Texts.Fill(line, s.values) : null, () => Rand.Value);
            GodTales.Written(s.values, text?.full ?? text?.letter);
            return text;
        }

        private static string PickFilled(LetterPart part, HashSet<string> tags, LetterSpec s) => Pick(part, tags, s.values, s.drawn) is string line ? Texts.Fill(line, s.values) : null;

        /// <summary>
        /// A line of a part's pool that fits (LetterFormMath.Pick), not one this session drew lately (LineMemory's window for
        /// the part) nor, while another fits, one already in a kept letter (so a load forgets none). Unfilled; null when none.
        /// The drawn line's tags go into <paramref name="drawn"/> (what the letter said, for the correspondence's threads: a
        /// joke begun, the colony's news).
        /// </summary>
        private static string Pick(LetterPart part, HashSet<string> tags, IReadOnlyDictionary<string, string> values, HashSet<string> drawn = null)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(LetterFormMath.PoolOf(part));
            var lines = pool?.lines?.plain;
            if (lines == null || lines.Count == 0) return null;
            string key = "letter:" + part;
            var avoid = new HashSet<string>(Flavor.Memory.Recent(key) ?? Enumerable.Empty<string>());
            string kept = LetterBook.KeptText;
            // A long piece (a poem, a sermon, a story's part...) is remembered across the game too (LetterBook.Used, saved).
            bool remembered = LetterKindMath.Remembered(part);
            if (!kept.NullOrEmpty() || remembered)
                foreach (var l in lines)
                {
                    if (LetterFormMath.Fits(l, tags) < 0) continue;
                    string text = LineTags.Of(l).text;
                    if (remembered && LetterBook.Used(LetterKindMath.Key(text))) avoid.Add(text);
                    else if (!kept.NullOrEmpty() && text.Length >= LineMemory.MinWords && LetterFormMath.Fillable(text, values) && kept.Contains(Texts.Fill(text, values))) avoid.Add(text);
                }
            string line = LetterFormMath.Pick(lines, tags, values, avoid, () => Rand.Value);
            if (line != null)
            {
                Flavor.Memory.Remember(key, line, lines.Count);
                if (remembered) LetterBook.Use(LetterKindMath.Key(line));
                if (drawn != null) Drawn(lines, line, tags, drawn);
            }
            return line;
        }

        /// <summary>The tags of the line drawn (its single tags, not a choice between several), into <paramref name="drawn"/>.</summary>
        private static void Drawn(IList<string> lines, string line, HashSet<string> tags, HashSet<string> drawn)
        {
            foreach (var l in lines)
            {
                var p = LineTags.Of(l);
                if (p?.tags == null || p.text != line || LetterFormMath.Fits(l, tags) < 0) continue;
                foreach (var t in p.tags)
                    if (t.IndexOf('|') < 0)
                        drawn.Add(t);
                return;
            }
        }
    }
}
