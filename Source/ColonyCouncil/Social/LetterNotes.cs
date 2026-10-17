using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A line of what Rimfolk knows, added to the game's own letters (Harmony hooks in Patches/Patch_Letters.cs): who
    /// rides with a raid (or the old wrong its people remember), what had been weighing on a colonist who breaks, and who
    /// someone was when they die. Each says nothing unless there's something true to say.
    /// </summary>
    public static class LetterNotes
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.letterNotes && RimfolkPatches.On && CouncilGameComponent.Instance != null;

        /// <summary>For a raid's letter: the nemesis riding with it, or the colonist sworn to avenge someone on these people.</summary>
        public static string Raid(IncidentParms parms, List<Pawn> raiders)
        {
            if (!On || Raids.Sending || parms?.faction == null || raiders.NullOrEmpty()) return null;
            var comp = CouncilGameComponent.Instance;
            if (S.nemeses)
            {
                foreach (var r in raiders)
                {
                    var n = comp.nemeses.For(r);
                    if (n != null && n.announced && r.Spawned)
                        return "CC_Letter_RaidNemesis".Translate(r.Named("X"), n.DeedText.Named("DEED")).Resolve();
                }
            }
            if (S.vendettas)
            {
                var v = comp.vendettas.vendettas.FirstOrDefault(x => x.Active && x.faction == parms.faction && x.avenger.IsColonist && x.avenger.MapHeld == parms.target);
                if (v != null)
                    return (v.kidnapped ? "CC_Letter_RaidVendettaTaken" : "CC_Letter_RaidVendetta").Translate(v.avenger.Named("PAWN"), (v.victim ?? "?").Named("LOST")).Resolve();
            }
            // The old wrong these raiders remember (World/FactionMemory.cs).
            return FactionMemory.RaidNote(parms.faction);
        }

        /// <summary>For a mood break's letter: the council-shaped part of what pushed them there, if there is one.</summary>
        public static string Break(Pawn p)
        {
            if (!On || WorldReactions.InOwnBreak || p == null || !p.IsColonist || !People.HasInnerLife(p)) return null;
            var comp = CouncilGameComponent.Instance;
            int now = Find.TickManager.TicksGame;
            if (S.enableWishes)
            {
                var lapsed = comp.wishes.Where(w => w.pawn == p && w.state == WishState.Expired && now >= w.expiresTick && now - w.expiresTick < GenDate.TicksPerDay * 3)
                    .OrderByDescending(w => w.expiresTick).FirstOrDefault();
                if (lapsed != null)
                    return (lapsed.promised ? "CC_BreakNote_Broken" : "CC_BreakNote_Lapsed").Translate(p.Named("PAWN"), lapsed.Label.Named("WISH")).Resolve();
            }
            var hated = comp.decrees.active.FirstOrDefault(a => a.def != null && a.def.ReactionOf(p) == DecreeReaction.Hate);
            if (hated != null) return "CC_BreakNote_Decree".Translate(p.Named("PAWN"), hated.def.label.Named("DECREE")).Resolve();
            if (S.enableWishes)
            {
                var waiting = comp.OpenWishes.FirstOrDefault(w => w.pawn == p && w.stage >= 2);
                if (waiting != null) return "CC_BreakNote_Waiting".Translate(p.Named("PAWN"), waiting.Label.Named("WISH")).Resolve();
            }
            if (comp.standings.For(p, false)?.Level == TrustLevel.Bitter) return "CC_BreakNote_Bitter".Translate(p.Named("PAWN")).Resolve();
            if (S.nemeses)
            {
                var n = comp.nemeses.Of(p).FirstOrDefault(x => !x.ended && x.pawn != null && !x.pawn.Dead && !x.pawn.Spawned);
                if (n != null) return "CC_BreakNote_Nemesis".Translate(p.Named("PAWN"), n.pawn.Named("X")).Resolve();
            }
            return null;
        }

        /// <summary>For a colonist's death letter: how long they'd been here, what they never got to do, what they were to the colony.</summary>
        public static string Death(Pawn p)
        {
            if (!On || p == null || p.Faction != Faction.OfPlayer || !p.RaceProps.Humanlike || !People.HasInnerLife(p)) return null;
            var comp = CouncilGameComponent.Instance;
            float years = (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerYear;
            bool bornHere = p.ageTracker != null && years >= 1f && years >= p.ageTracker.AgeBiologicalYearsFloat - 0.5f;
            string yearsText = years < 1.5f ? "CC_DeathNote_OneYear".Translate().ToString() : "CC_DeathNote_NYears".Translate(UnityEngine.Mathf.RoundToInt(years)).ToString();
            string goal = S.aspirations ? comp.aspirations.Active(p)?.Goal : null;
            var notes = new List<string>();
            if (!goal.NullOrEmpty())
            {
                string key = bornHere ? "CC_DeathNote_BornGoal" : years >= 1f ? "CC_DeathNote_YearsGoal" : "CC_DeathNote_Goal";
                notes.Add(key.Translate(p.Named("PAWN"), yearsText.Named("YEARS"), goal.Named("GOAL")).Resolve());
            }
            else if (bornHere) notes.Add("CC_DeathNote_Born".Translate(p.Named("PAWN")).Resolve());
            else if (years >= 2f) notes.Add("CC_DeathNote_Years".Translate(p.Named("PAWN"), yearsText.Named("YEARS")).Resolve());
            string office = S.offices ? comp.offices.OfficeOf(p)?.label : null;
            string title = S.epithets ? comp.epithets.Of(p).FirstOrDefault()?.label : null;
            if (!office.NullOrEmpty()) notes.Add("CC_DeathNote_Office".Translate(office.Named("OFFICE")).Resolve());
            else if (!title.NullOrEmpty()) notes.Add("CC_DeathNote_Title".Translate(p.Named("PAWN"), title.Named("TITLE")).Resolve());
            // A name with a place in the colony's years says what goes with them (the last of the founders).
            var told = S.epithets ? comp.epithets.Of(p).FirstOrDefault(e => !e.deathNote.NullOrEmpty()) : null;
            if (told != null) notes.Add(told.deathNote.Formatted(p.Named("PAWN")).Resolve());
            return notes.Count == 0 ? null : string.Join(" ", notes);
        }
    }
}
