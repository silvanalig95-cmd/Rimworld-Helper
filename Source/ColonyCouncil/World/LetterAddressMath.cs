using System;

namespace ColonyCouncil
{
    /// <summary>Whose a letter's matter is: the person written to, their town, or their people as a whole.</summary>
    public enum LetterScope : byte
    {
        /// <summary>Between the colony and that person: greetings, condolences, news, an invitation, a debt they owe in person, a match, a reply to their own letter.</summary>
        Personal,
        /// <summary>The town's: a warning, help asked, their people's debt, a ransom, their envoy, a ward, a dealing with their council, a town asked back.</summary>
        Town,
        /// <summary>The whole people's: a pact, a truce, fealty sworn.</summary>
        People
    }

    /// <summary>Who answers a letter of a scope written to someone: they themselves, their town's head, or whoever speaks for their people.</summary>
    public enum LetterAnswerer : byte
    {
        Self,
        TownHead,
        Speaker,
        /// <summary>Nobody the colony knows speaks for it.</summary>
        None
    }

    /// <summary>How the letter reaches the one who answers it.</summary>
    public enum LetterWay : byte
    {
        /// <summary>Written to them, read by them.</summary>
        Direct,
        /// <summary>Given to someone who isn't the head, who passes it on to the one who is.</summary>
        PassOn,
        /// <summary>Only the one who answers can take it (a council's dealing): written to anyone else, it is greyed.</summary>
        HeadOnly,
        /// <summary>Nobody the colony knows speaks for the town or the people.</summary>
        NoHead
    }

    /// <summary>What decides whom a letter written to someone goes to (plain data, World/LetterRoutes.cs gathers it).</summary>
    public struct AddressFacts
    {
        /// <summary>Their role in their town.</summary>
        public NotableRole role;
        /// <summary>They are their people's own leader.</summary>
        public bool leader;
        /// <summary>Their town has a head the colony knows by name (they, or someone else).</summary>
        public bool townHead;
        /// <summary>Their people's leader is known to the colony by name.</summary>
        public bool leaderKnown;
        /// <summary>They head the town nearest the colony that speaks for their people while their leader isn't known.</summary>
        public bool nearestHead;
        /// <summary>Anyone the colony knows speaks for their people (their leader, or a head).</summary>
        public bool speakerKnown;
        /// <summary>They receive their council's dealings (Dealings.Receiver).</summary>
        public bool receiver;
    }

    /// <summary>
    /// Whom a letter goes to (the player, 10 November 2026: "it should make sense what kind of messages you can send whom,
    /// and which kind of messages are sent to the colony as a whole ... warning them is for the whole colony, or asking for
    /// some food or medicine ... this should be differentiated"). A personal letter goes to anyone; a town's matter to whoever
    /// speaks for the town, its head; a people's matter (a pact, a truce, fealty) to their leader if the colony knows them,
    /// else to the nearest town's head. A town's matter given to someone else is passed on by them: slower, with their good
    /// word or their poor one. What is passed on and how is said in the head's answer and the colony's letter. No game types,
    /// so Tools/Tests can check it.
    /// </summary>
    public static class LetterAddressMath
    {
        /// <summary>The prefix of a dealing's topic that answers a letter they wrote first (Dealings/DealingLetters.ReplyPrefix).</summary>
        public const string ReplyPrefix = "reply:";

        // ---------------- Whose matter ----------------

        /// <summary>A purpose whose scope its topic decides: a debt (theirs in person, or their people's) and a dealing (a reply to their letter, or their council's).</summary>
        public static bool Mixed(LetterPurpose p) => p == LetterPurpose.Favour || p == LetterPurpose.Dealing;

        /// <summary>
        /// Whose matter a letter of this purpose is. <paramref name="personalTopic"/>: for a debt, one they owe in person; for a
        /// dealing, an answer to a letter they wrote first.
        /// </summary>
        public static LetterScope ScopeOf(LetterPurpose p, bool personalTopic)
        {
            switch (p)
            {
                case LetterPurpose.Greetings:
                case LetterPurpose.Condolences:
                case LetterPurpose.News:
                case LetterPurpose.Invitation:
                case LetterPurpose.Match:
                case LetterPurpose.Task:
                    return LetterScope.Personal;
                case LetterPurpose.Favour:
                case LetterPurpose.Dealing:
                    return personalTopic ? LetterScope.Personal : LetterScope.Town;
                case LetterPurpose.Pact:
                case LetterPurpose.Truce:
                case LetterPurpose.Fealty:
                    return LetterScope.People;
                default:
                    return LetterScope.Town;
            }
        }

        /// <summary>A dealing's topic that answers their own letter ("reply:ID").</summary>
        public static bool ReplyTopic(string topicKey) => topicKey != null && topicKey.StartsWith(ReplyPrefix, StringComparison.Ordinal);

        /// <summary>The topic is the person's own: a debt they owe in person, or an answer to their letter.</summary>
        public static bool PersonalTopic(LetterPurpose p, string topicKey, bool personalDebt) =>
            p == LetterPurpose.Favour ? personalDebt : p == LetterPurpose.Dealing && ReplyTopic(topicKey);

        /// <summary>
        /// A town's or a people's matter someone else can pass on to the one who answers it: all of them but a dealing with
        /// their council, which its acts put to the head in person.
        /// </summary>
        public static bool Passable(LetterPurpose p) => p != LetterPurpose.Dealing;

        // ---------------- Who answers ----------------

        /// <summary>
        /// Who answers a letter of this purpose and scope written to someone: anything personal they do themselves; a town's
        /// matter, the town's head (they, if they are); a people's matter, their leader if known, else the head of the nearest
        /// town, who sends it on; a council's dealing, whoever receives for the council. None: nobody the colony knows speaks
        /// for it.
        /// </summary>
        public static LetterAnswerer WhoAnswers(LetterPurpose p, LetterScope scope, AddressFacts f)
        {
            if (scope == LetterScope.Personal) return LetterAnswerer.Self;
            if (p == LetterPurpose.Dealing) return f.receiver ? LetterAnswerer.Self : f.townHead ? LetterAnswerer.TownHead : LetterAnswerer.None;
            if (scope == LetterScope.Town) return f.role == NotableRole.Head ? LetterAnswerer.Self : f.townHead ? LetterAnswerer.TownHead : LetterAnswerer.None;
            if (f.leader) return LetterAnswerer.Self;
            if (f.leaderKnown) return LetterAnswerer.Speaker;
            if (f.role == NotableRole.Head && f.nearestHead) return LetterAnswerer.Self;
            return f.speakerKnown ? LetterAnswerer.Speaker : LetterAnswerer.None;
        }

        /// <summary>How a letter of this purpose reaches the one who answers it.</summary>
        public static LetterWay Way(LetterPurpose p, LetterAnswerer a)
        {
            if (a == LetterAnswerer.Self) return LetterWay.Direct;
            if (a == LetterAnswerer.None) return LetterWay.NoHead;
            return Passable(p) ? LetterWay.PassOn : LetterWay.HeadOnly;
        }

        /// <summary>The section of the letter's window a purpose of this scope sits in: "To the town" for a town's or a people's matter, "To {NAME}" for the rest.</summary>
        public static bool TownSection(LetterScope scope) => scope != LetterScope.Personal;

        // ---------------- Passing it on ----------------

        /// <summary>Passing it on within their town: a day more.</summary>
        public const float SameTownDays = 1f;

        /// <summary>
        /// The days passing it on adds: a day within their town, else the leg from their town to the head's by their people's
        /// means (<paramref name="legDays"/>, LetterMath.LegDays), never less than a day.
        /// </summary>
        public static float PassDays(bool sameTown, float legDays) => sameTown ? SameTownDays : Math.Max(SameTownDays, legDays);

        /// <summary>A call: the one who took it passes it on and the head calls back, within their town in some hours, from another town in more.</summary>
        public static float PassCallHours(bool sameTown, float roll)
        {
            roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
            return sameTown ? 3f + roll * 5f : 8f + roll * 14f;
        }

        /// <summary>
        /// What the go-between's word adds to how the letter lands with the head (NotableMath.Score's word), by how they regard
        /// the colony (NotableMath.Level): a little from one who thinks well of it, more from the heir or an envoy, nothing from
        /// one who is civil, and against it from one who thinks poorly of it.
        /// </summary>
        public static float Word(NotableRole role, int opinion)
        {
            int level = NotableMath.Level(opinion);
            bool weighty = role == NotableRole.Heir || role == NotableRole.Envoy;
            float w;
            switch (level)
            {
                case 0: w = 6f; break;
                case 1: w = 5f; break;
                case 2: w = 3f; break;
                case 3: w = 0f; break;
                case 4: w = -3f; break;
                case 5: w = -6f; break;
                default: w = -9f; break;
            }
            if (weighty && w > 0f) w *= 2f;
            else if (weighty && w == 0f) w = 2f;
            return w;
        }

        /// <summary>The go-between's word in the letter's window and their answer: "CC_Route_Word_" Warm (it helps), Weighty (the heir's or an envoy's), Plain, Poor.</summary>
        public static string WordKey(NotableRole role, int opinion)
        {
            float w = Word(role, opinion);
            if (w < 0f) return "CC_Route_Word_Poor";
            if (w == 0f) return "CC_Route_Word_Plain";
            bool weighty = role == NotableRole.Heir || role == NotableRole.Envoy;
            return weighty ? "CC_Route_Word_Weighty" : "CC_Route_Word_Warm";
        }

        // ---------------- Words ----------------

        /// <summary>
        /// The head's line in their answer that says who brought it: a letter ("Acraeraes brought me your letter."), words given
        /// by mouth, a call passed on; in a medieval hand its own words.
        /// </summary>
        public static string PassLineKey(bool call, bool spoken, bool olde) =>
            "CC_Route_Line_" + (call ? "Call" : spoken ? "Spoken" : "Written") + (olde ? "_Olde" : "");

        /// <summary>The header's key for who they are to their town: "_Head" speaks for it, "_Leader" for their people, "_Other" with the head named, "_NoHead" with none known.</summary>
        public static string HeaderKey(LetterAnswerer town, bool leader) =>
            leader ? "CC_Route_Header_Leader" : town == LetterAnswerer.Self ? "CC_Route_Header_Head" : town == LetterAnswerer.None ? "CC_Route_Header_NoHead" : "CC_Route_Header_Other";

        /// <summary>The route line under the purposes: passed on within their town, or to the head's town.</summary>
        public static string RouteKey(bool sameTown, bool call) => "CC_Route_Pass_" + (call ? "Call" : sameTown ? "Same" : "Far");
    }
}
