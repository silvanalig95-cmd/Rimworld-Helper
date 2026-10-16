using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// People who change in the middle of a game, and Rimfolk's records keeping up with them. Big &amp; Small can give a
    /// colonist a new race or body, a xenogerm or a gene mod a new xenotype, and a character editor any new name, trait,
    /// gender or age, all on the same person. Rimfolk keeps its records on the person, so they carry over; what needs care
    /// is what it has worked out about them. Every so often each colonist's look is compared with the last
    /// (<see cref="ChangeMath"/>), and when it changed: who they are (tags, voice, leanings) is read again at once; their
    /// open wishes are asked whether they still make sense for the new body (a meal for someone who no longer eats), and
    /// the ones that don't are set aside without blame; someone left without an inner life (a ghoul, the raised dead) has
    /// their wishes set aside and their seat on the council falls empty as before; and a new body or xenotype gets a
    /// line in their own journal.
    /// </summary>
    /// <remarks>
    /// Run from the other mods' people pass (every 1,000 ticks, <see cref="GameComponent_PeopleMods"/>): a few fields per
    /// colonist, compared with the last look. The first look at someone (a new colonist, a loaded game) is only noted.
    /// Nothing is saved, so a change made while the game was closed is taken as it is.
    /// </remarks>
    public static class PeopleChanges
    {
        private static readonly Dictionary<int, ChangeMath.Look> last = new Dictionary<int, ChangeMath.Look>();
        private static Game game;

        public static ChangeMath.Look LookOf(Pawn p)
        {
            float lifeStageFactor = Mathf.Max(0.05f, p.ageTracker?.CurLifeStage?.bodySizeFactor ?? 1f);
            return new ChangeMath.Look
            {
                race = p.def?.defName ?? "",
                xenotype = ModsConfig.BiotechActive ? p.genes?.Xenotype?.defName ?? p.genes?.xenotypeName ?? "" : "",
                gender = p.gender.ToString(),
                stage = p.DevelopmentalStage.ToString(),
                traits = ChangeMath.TraitKey(p.story?.traits?.allTraits?.Select(t => t?.def == null ? null : t.def.defName + ":" + t.Degree)),
                name = p.Name?.ToStringFull ?? p.LabelShort,
                // The body's own size, apart from age: a child growing up isn't a change of body.
                size = ChangeMath.SizeKey(p.BodySize / lifeStageFactor),
                inner = People.HasInnerLife(p)
            };
        }

        /// <summary>One look at every colonist; what changed since the last is put right.</summary>
        public static void Check()
        {
            if (game != Current.Game)
            {
                game = Current.Game;
                last.Clear();
            }
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            var seen = new HashSet<int>();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
            {
                if (p?.RaceProps == null || p.IsQuestLodger()) continue;
                int id = p.thingIDNumber;
                seen.Add(id);
                ChangeMath.Look now;
                try
                {
                    now = LookOf(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("look at a colonist", e);
                    continue;
                }
                if (!last.TryGetValue(id, out var before))
                {
                    last[id] = now;
                    continue;
                }
                last[id] = now;
                var change = ChangeMath.Diff(before, now);
                if (change == ChangeMath.Change.None) continue;
                try
                {
                    Changed(comp, p, change, before, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a colonist changed", e);
                }
            }
            // Those no longer among the colony's free people are forgotten (they're looked at afresh if they come back).
            if (last.Count > seen.Count)
                foreach (var id in last.Keys.Where(k => !seen.Contains(k)).ToList()) last.Remove(id);
        }

        private static void Changed(CouncilGameComponent comp, Pawn p, ChangeMath.Change change, ChangeMath.Look before, ChangeMath.Look now)
        {
            if (ChangeMath.RereadWho(change))
            {
                PeopleTags.Forget(p);
                ModPeople.Reset();
            }
            if (Prefs.DevMode) Log.Message("[Rimfolk] " + p.LabelShort + " changed: " + change);
            if (ChangeMath.SetAside(change, now))
            {
                int set = SetAside(comp, p, w => true, true);
                if (set > 0)
                    News.Post("CC_Changed_SetAside".Translate(p.Named("PAWN"), set.Named("COUNT")).Resolve(), new LookTargets(p), NewsKind.People, MessageTypeDefOf.NeutralEvent);
                return;
            }
            if (ChangeMath.RecheckWishes(change))
            {
                var beneficiaryOf = new List<string>();
                int set = SetAside(comp, p, w =>
                {
                    if (w.Beneficiary != p || ModPeople.WishFits(w.def, p)) return false;
                    beneficiaryOf.Add(w.Label);
                    return true;
                }, false);
                if (set > 0)
                    News.Post("CC_Changed_WishSetAside".Translate(p.Named("PAWN"), string.Join(", ", beneficiaryOf).Named("WISH")).Resolve(), new LookTargets(p), NewsKind.People,
                        MessageTypeDefOf.NeutralEvent);
            }
            string tag = ChangeMath.JournalTag(change, before, now);
            if (tag != null && comp.journals != null)
                comp.journals.WriteTagged(p, "Council_Journal_Changed", new[] { tag });
        }

        /// <summary>
        /// Their open wishes that match are set aside, as cancelled (no blame, no mood); returns how many. With
        /// <paramref name="unsign"/>, they're also taken off others' petitions they signed.
        /// </summary>
        private static int SetAside(CouncilGameComponent comp, Pawn p, Predicate<Wish> which, bool unsign)
        {
            if (comp.wishes == null) return 0;
            int n = 0;
            foreach (var w in comp.wishes.ToList())
            {
                if (w == null || w.state != WishState.Open) continue;
                if (w.pawn != p && w.Beneficiary != p)
                {
                    // Someone who signed another's petition is just taken off it.
                    if (unsign) w.coSigners?.Remove(p);
                    continue;
                }
                if (!which(w)) continue;
                comp.Resolve(w, WishState.Cancelled);
                n++;
            }
            return n;
        }
    }
}
