using System.Linq;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How an answer's plan on the map is shown in This week ("Gatherings and plans") and on the dilemmas page: its icon,
    /// who it's about, and a line on what it is and how far it has got.
    /// </summary>
    public static class PlayOutText
    {
        public static Texture2D Icon(PlayOut p)
        {
            switch (p?.kind)
            {
                case PlayKind.Visit: return Icons.People;
                case PlayKind.Lesson: return Icons.Bonds;
                case PlayKind.WorkParty: return Icons.CatManagement;
                case PlayKind.Procession: return Icons.Roster;
                case PlayKind.Vigil: return Icons.Death;
                case PlayKind.Search: return Icons.WorldEvent;
                case PlayKind.Carry: return Icons.Trader;
                case PlayKind.Hunt: return Icons.CatFood;
                default: return Icons.Wedding;
            }
        }

        /// <summary>Who it's about: whoever leads it, goes on it, or carries the errand.</summary>
        public static Pawn Who(PlayOut p)
        {
            if (p == null) return null;
            if (p.IsGathering) return p.organizer;
            if (p.IsGroup) return p.organizer ?? p.visitor ?? p.hands.FirstOrDefault()?.pawn ?? p.invited.FirstOrDefault();
            return p.visitor;
        }

        /// <summary>A line on what it is, and, once it's going, how far it has got.</summary>
        public static string Body(PlayOut p)
        {
            if (p == null) return null;
            bool running = p.state == PlayState.Running;
            switch (p.kind)
            {
                case PlayKind.WorkParty:
                {
                    string s = "CC_Week_PlayWork".Translate(running ? p.hands.Count(h => !h.left) : p.count, PlayWords.TaskWords(p)).Resolve();
                    if (running && p.tally > 0) s += " " + "CC_Week_PlayWorkDone".Translate(p.tally).Resolve();
                    return s;
                }
                case PlayKind.Procession:
                    return running ? ("CC_Week_PlayWalk_" + Mathf.Clamp(p.phase, 0, 3)).Translate(p.hands.Count(h => !h.left)).Resolve()
                        : "CC_Week_PlayWalk".Translate(p.count).Resolve();
                case PlayKind.Vigil:
                    return p.to == PlaySpot.Person && p.other != null
                        ? "CC_Week_PlayVigilBeside".Translate(p.other.LabelShort).Resolve()
                        : "CC_Week_PlayVigil".Translate(running ? p.hands.Count(h => !h.left) : p.count).Resolve();
                case PlayKind.Search:
                {
                    string s = "CC_Week_PlaySearch".Translate(running ? p.hands.Count(h => !h.left) : p.count).Resolve();
                    if (p.thing != null && p.amount > 0) s += " " + "CC_Week_PlaySearchFind".Translate(p.thing.label).Resolve();
                    return s;
                }
                case PlayKind.Carry:
                    return "CC_Week_PlayCarry".Translate(p.amount, PlayCarry.Label(p.what, p.thing)).Resolve();
                case PlayKind.Hunt:
                    return "CC_Week_PlayHunt".Translate(TextUtil.JoinAnd(p.game.Where(a => a != null).Select(a => a.LabelShort).ToList())).Resolve();
                case PlayKind.Visit:
                case PlayKind.Lesson:
                    return p.visitor != null && p.other != null ? "CC_Week_PlayVisit".Translate(p.visitor.LabelShort, p.other.LabelShort).Resolve() : null;
                default:
                    return "CC_Week_PlayGathering".Translate(p.invited.Count + (p.organizer != null && !p.invited.Contains(p.organizer) ? 1 : 0)).Resolve();
            }
        }
    }
}
