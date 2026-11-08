using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Every act "Do something with..." offers (docs/design/diplomacy-screen.md, step A, point 3), each from its own code path.</summary>
    public enum PeopleActKind : byte
    {
        // Letters, by occasion (World/LetterOccasions.cs).
        Greetings,
        Condolences,
        Warning,
        News,
        Invitation,
        Help,
        // Pacts and truces, fealty (Diplomacy/PactsPanels.cs, FealtyPanels.cs).
        Truce,
        Pact,
        CallPact,
        Swear,
        Renounce,
        // People between the two (Diplomacy/RansomPanels.cs, FeastPanels.cs, MatchPanels.cs, WardPanels.cs).
        AskEnvoy,
        SendEnvoy,
        Feast,
        InviteFeast,
        Match,
        ProposeMatch,
        AskWard,
        SendWard,
        Exchange,
        // Debts of honor (Diplomacy/DiplomacyModel.cs).
        CallDebt,
        AnswerDebt,
        // A town's own (Diplomacy/FealtyPanels.cs, Colonies/TownsAfter.cs, UI/JourneyDialog.cs).
        Levies,
        Level,
        AskBack,
        TakeBack,
        Journey,
        // Someone of theirs at the colony: face to face (Outsiders/).
        FaceToFace,
        /// <summary>An act another part (or mod) put on the card: its own tip says what it is for.</summary>
        Other
    }

    /// <summary>How an act travels: a letter by the post (its reach), a colonist on the road, a signal, the council at home, face to face.</summary>
    public enum ActWay : byte
    {
        Letter,
        Colonist,
        Signal,
        Council,
        Face,
        None
    }

    /// <summary>The sections the acts are shown in.</summary>
    public enum ActGroup : byte
    {
        Letters,
        Bonds,
        People,
        Debts,
        Town
    }

    /// <summary>
    /// The acts' words chosen as plain rules (docs/design/post-and-letters.md, step 2, point 11: what each is for, said with
    /// the option): which act a card act is by its id, how it travels, what section it sits in, and the keys of its line
    /// (what it brings, what it costs). Each line was written from what the act does in code (see the keyed file
    /// Rimfolk_PeoplesScreen.xml). No game types, so Tools/Tests checks it.
    /// </summary>
    public static class PeopleActMath
    {
        /// <summary>The card acts' ids (CardActs.Register's last argument) and the act each is.</summary>
        public static readonly Dictionary<string, PeopleActKind> ById = new Dictionary<string, PeopleActKind>
        {
            ["truce-offer"] = PeopleActKind.Truce,
            ["pact-propose"] = PeopleActKind.Pact,
            ["pact-call-people"] = PeopleActKind.CallPact,
            ["fealty-swear"] = PeopleActKind.Swear,
            ["fealty-renounce-people"] = PeopleActKind.Renounce,
            ["envoy-ask"] = PeopleActKind.AskEnvoy,
            ["envoy-send"] = PeopleActKind.SendEnvoy,
            ["envoy-send-town"] = PeopleActKind.SendEnvoy,
            ["feast-hold"] = PeopleActKind.Feast,
            ["feast-invite"] = PeopleActKind.InviteFeast,
            ["match-seek"] = PeopleActKind.Match,
            ["match-propose"] = PeopleActKind.ProposeMatch,
            ["ward-ask"] = PeopleActKind.AskWard,
            ["ward-send"] = PeopleActKind.SendWard,
            ["ransom-exchange-people"] = PeopleActKind.Exchange,
            ["vassal-levies"] = PeopleActKind.Levies,
            ["vassal-level"] = PeopleActKind.Level,
            ["town-takeback-ask"] = PeopleActKind.AskBack,
            ["town-takeback"] = PeopleActKind.TakeBack,
            ["journey"] = PeopleActKind.Journey
        };

        /// <summary>The card acts that belong elsewhere than a people, a town or a person's page (an envoy's bed, seeing them off: on their own card).</summary>
        public static readonly HashSet<string> Elsewhere = new HashSet<string> { "envoy-bed", "envoy-seeoff" };

        public static PeopleActKind KindOf(string id) => id != null && ById.TryGetValue(id, out var k) ? k : PeopleActKind.Other;

        /// <summary>The letter act of a purpose, or Other for the purposes that are acts of their own.</summary>
        public static PeopleActKind KindOf(LetterPurpose p)
        {
            switch (p)
            {
                case LetterPurpose.Greetings: return PeopleActKind.Greetings;
                case LetterPurpose.Condolences: return PeopleActKind.Condolences;
                case LetterPurpose.Warning: return PeopleActKind.Warning;
                case LetterPurpose.News: return PeopleActKind.News;
                case LetterPurpose.Invitation: return PeopleActKind.Invitation;
                case LetterPurpose.Help: return PeopleActKind.Help;
                default: return PeopleActKind.Other;
            }
        }

        /// <summary>The kind letters, in the order shown: news first (always open), then the ones that wait on an occasion.</summary>
        public static readonly LetterPurpose[] Letters =
            { LetterPurpose.News, LetterPurpose.Greetings, LetterPurpose.Condolences, LetterPurpose.Warning, LetterPurpose.Invitation, LetterPurpose.Help };

        public static bool IsLetter(PeopleActKind k) => k <= PeopleActKind.Help;

        /// <summary>How it travels: by letter (the post's reach, or a call); a colonist on the road; a signal; the council at home; face to face.</summary>
        public static ActWay Way(PeopleActKind k)
        {
            switch (k)
            {
                case PeopleActKind.SendEnvoy:
                case PeopleActKind.Journey:
                    return ActWay.Colonist;
                case PeopleActKind.CallPact:
                case PeopleActKind.Levies:
                    return ActWay.Signal;
                case PeopleActKind.Renounce:
                case PeopleActKind.Level:
                case PeopleActKind.TakeBack:
                case PeopleActKind.AnswerDebt:
                    return ActWay.Council;
                case PeopleActKind.FaceToFace:
                    return ActWay.Face;
                case PeopleActKind.Other:
                    return ActWay.None;
                default:
                    return ActWay.Letter;
            }
        }

        public static ActGroup GroupOf(PeopleActKind k)
        {
            if (IsLetter(k)) return ActGroup.Letters;
            switch (k)
            {
                case PeopleActKind.Truce:
                case PeopleActKind.Pact:
                case PeopleActKind.CallPact:
                case PeopleActKind.Swear:
                case PeopleActKind.Renounce:
                    return ActGroup.Bonds;
                case PeopleActKind.CallDebt:
                case PeopleActKind.AnswerDebt:
                    return ActGroup.Debts;
                case PeopleActKind.Levies:
                case PeopleActKind.Level:
                case PeopleActKind.AskBack:
                case PeopleActKind.TakeBack:
                case PeopleActKind.Journey:
                    return ActGroup.Town;
                default:
                    return ActGroup.People;
            }
        }

        /// <summary>
        /// The key of what it brings, said with it (CC_PplAct_For_ and the act). Its other form (_Alt) where what it does
        /// turns on a setting: a truce keeps raids away only with "Truces keep raids away" (and the deeper hooks); a warning
        /// heeded warmly is a debt of honor only where debts of honor are kept.
        /// </summary>
        public static string ForKey(PeopleActKind k, bool alt) => "CC_PplAct_For_" + k + (alt && HasAlt(k) ? "_Alt" : "");

        public static bool HasAlt(PeopleActKind k) => k == PeopleActKind.Truce || k == PeopleActKind.Warning;

        /// <summary>Whether what it brings turns on a setting that is off (the _Alt line): a truce without raids kept away, a warning without debts of honor.</summary>
        public static bool Alt(PeopleActKind k, bool truceKeepsRaidsAway, bool favoursOn) =>
            k == PeopleActKind.Truce ? !truceKeepsRaidsAway : k == PeopleActKind.Warning && !favoursOn;

        /// <summary>The key of what it costs (CC_PplAct_Cost_ and the act), or null for an act that costs nothing of note.</summary>
        public static string CostKey(PeopleActKind k, bool spoken)
        {
            switch (k)
            {
                case PeopleActKind.AskBack:
                case PeopleActKind.TakeBack:
                case PeopleActKind.Level:
                case PeopleActKind.Other:
                case PeopleActKind.FaceToFace:
                case PeopleActKind.Journey:
                    return null;
                case PeopleActKind.Greetings:
                case PeopleActKind.Condolences:
                case PeopleActKind.Warning:
                case PeopleActKind.News:
                    return spoken ? "CC_PplAct_Cost_Spoken" : "CC_PplAct_Cost_Letter";
                default:
                    return "CC_PplAct_Cost_" + k;
            }
        }

        /// <summary>The key of how it travels when it isn't a letter (a letter says the post's reach line, Post/PostWords.cs).</summary>
        public static string WayKey(ActWay w) => w == ActWay.Letter || w == ActWay.None ? null : "CC_PplAct_Way_" + w;

        /// <summary>The section's heading key.</summary>
        public static string GroupKey(ActGroup g) => "CC_PplAct_Group_" + g;
    }
}
