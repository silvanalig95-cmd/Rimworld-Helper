using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Invitations to a feast with guests in the existing letters and calls (World/NeighbourLetters.cs): an invitation (the
    /// purpose Invitation) whose topic is "feast:HOME" is the colony's feast at that home. The writer sits down to it or
    /// calls from the console, their messenger takes it, and once they've read it they answer by their own reckoning
    /// (Feasts.Answer: their regard, what they hope for being offered, the road); the answer's tone follows it (yes, never
    /// worse than cordial; no, never warmer than cool), and the letter says when they can come.
    /// </summary>
    public static class FeastLetters
    {
        /// <summary>The letter is an invitation to a feast with guests.</summary>
        public static bool Is(NeighbourLetter l) => l != null && l.purpose == LetterPurpose.Invitation && FeastMath.ParseTopic(l.topicKey, out _);

        /// <summary>
        /// They have read it (NeighbourLetters.Read): their answer to the feast, and the tone it gives the letter. Returns the
        /// line their letter says it in (for the invitation's answer), or null for any other letter.
        /// </summary>
        public static string Read(NeighbourLetter l, Notable n, Pawn speaker, ref AnswerTone tone)
        {
            if (!Is(l) || n == null) return null;
            try
            {
                FeastMath.ParseTopic(l.topicKey, out int homeKey);
                var map = Homes.MapOf(homeKey) ?? l.map ?? Homes.Seat;
                var p = Feasts.At(map);
                var g = p?.guests.FirstOrDefault(x => x.letter == l.id) ?? p?.guests.FirstOrDefault(x => x.notable == n.id);
                float back = l.backDays > 0f ? l.backDays : Feasts.TravelDays(n, map);
                string line = Feasts.Answer(p, g, n, map, back, l.channel == LetterChannel.Console, out bool yes);
                if (yes)
                {
                    if (tone > AnswerTone.Cordial) tone = AnswerTone.Cordial;
                }
                else if (tone < AnswerTone.Cool) tone = AnswerTone.Cool;
                return line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: their answer", e);
                return null;
            }
        }
    }

    /// <summary>
    /// The colony's own at a neighbour's feast (the missions' invitation, Missions/Workers_MissionRemembered.cs): each of
    /// them may win one of the contests their people hold there (FeastMath.AwayWin, by their best skill for it), and it is
    /// told when they are home again; nothing happens to them there.
    /// </summary>
    public static class FeastAway
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The contests a people holds at their own feast, by their ways.</summary>
        private static ContestKind[] TheirContests(Faction f)
        {
            switch (Feasts.HopeOf(f))
            {
                case FeastHope.Hunt: return new[] { ContestKind.Hunt, ContestKind.Wrestling, ContestKind.Race };
                case FeastHope.HuntOrTourney: return new[] { ContestKind.Hunt, ContestKind.Wrestling };
                case FeastHope.RaceOrMatch: return new[] { ContestKind.Race, ContestKind.Shooting };
                case FeastHope.MealAndMusic: return new[] { ContestKind.Tables };
                default: return new[] { ContestKind.Wrestling, ContestKind.Tables };
            }
        }

        private static int Skill(Pawn p, ContestKind k)
        {
            SkillDef s;
            switch (k)
            {
                case ContestKind.Wrestling: s = SkillDefOf.Melee; break;
                case ContestKind.Hunt:
                case ContestKind.Shooting: s = SkillDefOf.Shooting; break;
                case ContestKind.Tables: s = SkillDefOf.Intellectual; break;
                default:
                    return Math.Max(0, (int)((p.GetStatValue(StatDefOf.MoveSpeed) - 3.5f) * 4f));
            }
            return p.skills?.GetSkill(s)?.Level ?? 0;
        }

        /// <summary>
        /// The colony's guests sat at their feast to the end (the invitation's "attended"): each may have won something there,
        /// kept to be told when they are home.
        /// </summary>
        public static void Attended(Mission m, Caravan here)
        {
            if (!Feasts.On || m?.faction == null || here == null) return;
            try
            {
                var home = m.Map ?? Homes.Seat;
                var h = DiplomacyBook.Instance?.HomeOf(home);
                if (h == null) return;
                var kinds = TheirContests(m.faction);
                foreach (var p in MissionPeoples.Colonists(here))
                {
                    if (p.DevelopmentalStage.Baby()) continue;
                    var k = p.DevelopmentalStage.Juvenile() ? ContestKind.Race : kinds.OrderByDescending(x => Skill(p, x)).First();
                    if (!FeastMath.AwayWin(Skill(p, k), Rand.Value)) continue;
                    h.tales.RemoveAll(t => t.pawn == p);
                    if (h.tales.Count >= FeastMath.MaxTales) h.tales.RemoveAt(0);
                    h.tales.Add(new FeastTale
                    {
                        pawn = p, at = Now,
                        text = FeastWords.T("CC_Feast_Away_Won", p.Named("PAWN"), FeastWords.Contest(k).Named("CONTEST"), (m.place ?? FeastWords.People(m.faction)).Named("PLACE"),
                            FeastWords.People(m.faction).Named("PEOPLE"))
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: the colony's own at their feast", e);
            }
        }

        /// <summary>The hour: a tale whose colonist is home again is told (the evening's news).</summary>
        public static void Tell()
        {
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            foreach (var h in book.Live())
            {
                if (h.tales.Count == 0) continue;
                foreach (var t in h.tales.ToList())
                {
                    var p = t.pawn;
                    if (p == null || p.Dead)
                    {
                        h.tales.Remove(t);
                        continue;
                    }
                    if (!p.Spawned || p.Map == null || !p.Map.IsPlayerHome) continue;
                    h.tales.Remove(t);
                    // apart: told in the evening's news once they are home; it happened at the neighbour's feast.
                    ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Notable, p.Map, t.text, new LookTargets(p));
                }
            }
        }

        /// <summary>Once a day: tales never told within their days are let go.</summary>
        public static void Prune(int now)
        {
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            foreach (var h in book.Live())
                h.tales.RemoveAll(t => t == null || t.pawn == null || now - t.at > FeastMath.TaleDays * GenDate.TicksPerDay);
        }
    }
}
