using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Empire titles and Ideology rites in invitations (docs/design/stage1-region.md, item 7): the missions' invitations (a
    /// feast, midwinter or a wedding at their town) and their asks for a funeral or a wedding name the host by the title
    /// the game shows them (the town's head through the world ledger's town and person views, WorldMechanics.Title for the
    /// Empire), the colony's titled guest by theirs, and the rite: a ritual the inviting people's faith really has (with
    /// Ideology, by the name the game gives it), else how their age keeps such a day (Ages.Form). Read when the mission is
    /// offered and kept on it as words.
    /// </summary>
    public static class InviteForms
    {
        private static string T(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        /// <summary>
        /// The town's head as the ledger knows them, called by their title: an Empire head by the royal title the game
        /// shows ("Baroness Ana Bell"), any other by their title in the town ("Tom Hale, their reeve"). Null when the
        /// ledger knows no living head there.
        /// </summary>
        public static string Host(Settlement town, out bool royal)
        {
            royal = false;
            if (town == null) return null;
            try
            {
                var head = WorldLedger.Town(town.ID)?.head;
                if (head == null || !head.alive || head.name.NullOrEmpty()) return null;
                if (Ages.IsEmpire(town.Faction))
                {
                    var view = head.pawn != null ? WorldLedger.Person(head.pawn) : WorldLedger.Person(head.person);
                    string title = view?.royal?.label;
                    if (!title.NullOrEmpty())
                    {
                        royal = true;
                        return InviteMath.Titled(title, head.name);
                    }
                }
                return head.title.NullOrEmpty() ? head.name : T("CC_Invite_HostTitled", head.name.Named("NAME"), head.title.Named("TITLE"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invitations: the host", e);
                return null;
            }
        }

        /// <summary>
        /// Someone of theirs an occasion is for, named and never met (made, named, let go): for the Empire one of its titled
        /// kinds, so the title before their name is the one the game gives them ("Dame Ilse Marr").
        /// </summary>
        public static string Named(Faction f, FloatRange ages, Gender? gender, out bool royal)
        {
            royal = false;
            if (f == null) return "?";
            Pawn p = null;
            try
            {
                var kind = Ages.IsEmpire(f) ? RegionFaces.TitledKind(f) ?? Arrivals.PersonKind(f) : Arrivals.PersonKind(f);
                p = kind == null ? null : Arrivals.Make(kind, f, ages, gender, null);
                if (p == null) return "?";
                string title = Ages.IsEmpire(f) ? WorldMechanics.Title(p)?.label : null;
                royal = !title.NullOrEmpty();
                return InviteMath.Titled(title, p.LabelShort);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invitations: someone of theirs named", e);
                return p?.LabelShort ?? "?";
            }
            finally
            {
                if (p != null) Arrivals.Discard(p);
            }
        }

        /// <summary>
        /// The colony's guest by their title: a grown colonist of this home who holds a title the inviting people gave (the
        /// Empire's), the most senior first. Null with none.
        /// </summary>
        public static Pawn TitledGuest(Map map, Faction f, out string called)
        {
            called = null;
            if (map == null || f == null || !Ages.IsEmpire(f)) return null;
            try
            {
                Pawn best = null;
                RoyalTitleView bestTitle = null;
                foreach (var p in StorytellerEvents.Adults(map))
                {
                    var t = WorldMechanics.Title(p);
                    if (t == null || t.people != f || t.label.NullOrEmpty()) continue;
                    if (bestTitle != null && t.seniority <= bestTitle.seniority) continue;
                    best = p;
                    bestTitle = t;
                }
                if (best == null) return null;
                called = InviteMath.Titled(bestTitle.label, best.LabelShort);
                return best;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invitations: the titled guest", e);
                return null;
            }
        }

        /// <summary>
        /// The rite of an occasion: with Ideology, a ritual the inviting people's faith really has that fits it (its name as
        /// the game gives it, "the Feast of Lanterns"; <paramref name="theirFaith"/>); else how their age keeps such a day
        /// (Ages.Form: "a hunt and a tournament"). Null for an occasion with neither (a funeral without a rite of theirs).
        /// </summary>
        public static string Rite(Faction f, Map map, string occasion, out bool theirFaith)
        {
            theirFaith = false;
            if (f == null) return null;
            try
            {
                if (ModsConfig.IdeologyActive && Find.IdeoManager != null && !Find.IdeoManager.classicMode)
                {
                    var rituals = f.ideos?.PrimaryIdeo?.PreceptsListForReading?.OfType<Precept_Ritual>().ToList() ?? new List<Precept_Ritual>();
                    var kinds = rituals.Select(r => InviteMath.RiteKind((r.def?.defName ?? "") + " " + (r.sourcePattern?.defName ?? "") + " " + (r.patternGroupTag ?? ""))).ToList();
                    int i = InviteMath.Best(occasion, kinds);
                    if (i >= 0 && !rituals[i].Label.NullOrEmpty())
                    {
                        theirFaith = true;
                        return rituals[i].Label;
                    }
                }
                var feature = InviteMath.Feature(occasion);
                return feature == null ? null : Ages.Form(feature.Value, map, f).rite;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invitations: the rite", e);
                return null;
            }
        }

        /// <summary>
        /// What the invitation adds to its words: who it comes from, how their people keep the day, and the colony's guest
        /// named by their title. Empty when none is known.
        /// </summary>
        public static string Lines(Mission m)
        {
            if (m == null) return "";
            var parts = new List<string>();
            var people = (m.faction?.Name ?? "?").Named("FACTION");
            if (!m.host.NullOrEmpty() && m.host != m.name) parts.Add(T("CC_Invite_From", m.host.Named("HOST")));
            if (!m.rite.NullOrEmpty())
            {
                string key = m.riteOfFaith ? "CC_Invite_Rite_" : "CC_Invite_AgeRite_";
                key += m.variant == "funeral" ? "funeral" : m.variant == "wedding" ? "wedding" : "feast";
                if (key.CanTranslate()) parts.Add(T(key, people, m.rite.Named("RITE"), (m.name ?? "").Named("DEAD")));
            }
            if (!m.guest.NullOrEmpty()) parts.Add(T("CC_Invite_Guest", people, m.guest.Named("GUEST")));
            return string.Join(" ", parts);
        }

        /// <summary>The tags its lines may ask for: [rite] (their faith's own), [agerite], [royalhost], [titledguest].</summary>
        public static void AddTags(Mission m, List<string> tags)
        {
            if (m == null || tags == null) return;
            if (!m.rite.NullOrEmpty()) tags.Add(m.riteOfFaith ? "rite" : "agerite");
            if (m.royalHost) tags.Add("royalhost");
            if (!m.guest.NullOrEmpty()) tags.Add("titledguest");
        }
    }
}
