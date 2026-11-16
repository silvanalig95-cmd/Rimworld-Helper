using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: Revel's happenings now (Debug actions, "Rimfolk", "Gods: Revel's ... now"), kept out of Gods/OnMap as the
    /// others' are. Each calls the happening as its hour would, past its pace; the god in a cup can be put in a stack now,
    /// found, or made to do one thing for whoever takes it next.
    /// </summary>
    public static class OwnRevelDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static RevelState S(OwnHappeningsBook b) => b.revel ??= new RevelState();

        [DebugAction("Rimfolk", "Gods: Revel's long night now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNight()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var who = map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).Take(4).ToList();
            var at = OwnRevel.Fire(map, OwnHappenings.Middle(map))?.Position ?? OwnHappenings.Middle(map);
            S(b).nightUntil = -1;
            Say(OwnRevel.BeginNight(b, map, at, who) ? "the night runs long at " + at + "; those near stay up until two, and the morning after is slow." : "nobody to stay up.");
        }

        [DebugAction("Rimfolk", "Gods: Revel's vision now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugVision()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).OrderByDescending(x => RevelGoods.High(x) != null).FirstOrDefault();
            S(b).visionUntil = -1;
            Say(OwnRevel.See(b, p, false) ? p.LabelShort + " stares, then walks out to what the vision showed (" + (S(b).visionTrue ? "true" : "false") + ")." : "nobody to see it, or nowhere to go.");
        }

        [DebugAction("Rimfolk", "Gods: Revel's craving in the night now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCraving()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours))
            {
                var c = RevelGoods.Craving(p);
                var dose = RevelGoods.For(map, c.addiction, p);
                if (dose == null) continue;
                S(b).cravingUntil = -1;
                Say(OwnRevel.Crave(b, p, dose) ? p.LabelShort + " gets up and goes to the stores for " + dose.LabelNoCount + "." : "they couldn't go.");
                return;
            }
            Say("nobody addicted with their drug in the stores.");
        }

        [DebugAction("Rimfolk", "Gods: Revel's stash found now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStash()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).RandomElementWithFallback();
            Say(OwnRevel.FindStash(b, p) ? p.LabelShort + " finds a stash at their feet." : "nobody to find it.");
        }

        [DebugAction("Rimfolk", "Gods: Revel's binge egged on now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBinge()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var p = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(RevelGoods.Binging) ?? map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours).FirstOrDefault();
            var seer = p == null ? null : map.mapPawns.FreeColonistsSpawned.Where(x => x != p && OwnHappenings.Ours(x)).OrderBy(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            Say(OwnRevel.EggedOn(b, p, seer) ? (p?.LabelShort ?? "?") + " talks and laughs with an empty seat" + (seer != null ? "; " + seer.LabelShort + " saw it." : ".") : "nobody.");
        }

        [DebugAction("Rimfolk", "Gods: Revel's craving eased now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLetGo()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var addict = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => RevelGoods.Craving(p).addiction != null);
            var sitter = addict == null ? null : OwnRevel.Sitter(map, addict, OwnRevel.God)
                                                 ?? map.mapPawns.FreeColonistsSpawned.Where(x => x != addict && OwnHappenings.Ours(x)).FirstOrDefault();
            S(b).letGoUntil = -1;
            Say(OwnRevel.SitUp(b, sitter, addict) ? sitter.LabelShort + " sits up with " + addict.LabelShort + " until dawn." : "nobody addicted, or nobody to sit with them.");
        }

        [DebugAction("Rimfolk", "Gods: Revel's horned reveller now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSatyr()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var at = OwnRevel.Fire(map, OwnHappenings.Middle(map))?.Position ?? OwnHappenings.Middle(map);
            S(b).satyrUntil = -1;
            Say(OwnRevel.Come(b, map, at) ? "a horned reveller walks in to " + at + " with a cup to share." : "he couldn't come (no people at peace to come from?).");
        }

        [DebugAction("Rimfolk", "Gods: Revel in a cup in the stores now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCup()
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var t = RevelCup.TakeNow(b, map);
            Say(t != null ? "he is in one of the " + t.LabelNoCount + " at " + t.Position + " (number " + S(b).cupK + " of " + t.stackCount + ")." : "no drink or drugs of his in the stores.");
        }

        [DebugAction("Rimfolk", "Gods: where is Revel's cup (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWhere()
        {
            var b = OwnHappenings.Book;
            if (b == null) return;
            var s = S(b);
            if (s.cupSince < 0 || s.cup == null) Log.Message("[Rimfolk] Revel is in no cup now.");
            else Log.Message("[Rimfolk] Revel is in number " + s.cupK + " of " + s.cup.stackCount + " " + s.cup.LabelNoCount + " (" + (s.cup.Spawned ? "at " + s.cup.Position : "held by " + s.cup.ParentHolder) + "), since tick " + s.cupSince + ".");
            if (s.cupBy != null) Log.Message("[Rimfolk] " + s.cupBy.LabelShort + " took his last one: " + s.cupOutcome + ".");
        }

        [DebugAction("Rimfolk", "Gods: Revel's next cup does...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNext()
        {
            var options = new List<DebugMenuOption>();
            foreach (CupOutcome o in Enum.GetValues(typeof(CupOutcome)))
            {
                var x = o;
                options.Add(new DebugMenuOption(o == CupOutcome.None ? "(as it falls)" : o.ToString(), DebugMenuOptionMode.Action, () =>
                {
                    RevelCup.Forced = x;
                    Say("whoever takes his next cup: " + (x == CupOutcome.None ? "as it falls." : x + "."));
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }
    }
}
