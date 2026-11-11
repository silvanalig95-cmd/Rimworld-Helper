using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the gods on the map ("Rimfolk", "Gods: ..."). The stranger at the gate now or after its omens, the
    /// stranger's stay ended now, the sacred animal now, someone following it, a god's answer in the weather, each omen
    /// here, a walk to a god's mark, and what is under way and remembered (log).
    /// </summary>
    public static class GodsOnMapDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static GodsOnMapBook Book
        {
            get
            {
                var b = GodsOnMapBook.Instance;
                if (!Gods.On) Say("the gods are off (the setting \"Gods\").");
                else if (b == null || GodBook.Instance == null) Say("no pantheon in this game.");
                else return b;
                return null;
            }
        }

        private static void PickGod(Func<God, bool> fits, Action<God> then)
        {
            var list = Gods.All.Where(g => g != null && fits(g)).OrderBy(g => g.domain).ThenBy(g => g.name)
                .Select(g => new FloatMenuOption(Gods.FullName(g) + " (" + g.domain + (g.known ? "" : ", a rumour") + ")", () => then(g))).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(no god takes that shape)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [DebugAction("Rimfolk", "Gods: a stranger at the gate now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStranger()
        {
            var b = Book;
            if (b == null) return;
            if (!GodsMath.AvatarsCome(Gods.Realness))
            {
                Say("in Believed no god comes to the gate (set how real the gods are first).");
                return;
            }
            PickGod(GodShapes.Stranger, g => Say(GodStranger.Fire(b, Find.CurrentMap, g, true) ? Gods.Name(g) + " is at the gate, in disguise." : "the stranger couldn't come (one already here, or no people at peace)."));
        }

        [DebugAction("Rimfolk", "Gods: a stranger at the gate after two days of omens...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStrangerOmens()
        {
            var b = Book;
            if (b == null) return;
            PickGod(GodShapes.Stranger, g =>
            {
                b.coming = null;
                Say(GodComings.Plan(b, ComingKind.Stranger, Find.CurrentMap, g, 2) ? "planned: the omens over two days, then " + Gods.Name(g) + " at the gate." : "couldn't plan it.");
            });
        }

        [DebugAction("Rimfolk", "Gods: end the stranger's stay now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEndStay()
        {
            var b = Book;
            var s = b?.stranger;
            if (s == null)
            {
                Say("no stranger is staying.");
                return;
            }
            if (s.phase == StrangerPhase.Waiting)
            {
                Say("the colony hasn't answered the stranger yet.");
                return;
            }
            GodStranger.End(b, s, Find.TickManager.TicksGame);
            Say("the stranger's stay ends: " + s.phase + ".");
        }

        [DebugAction("Rimfolk", "Gods: a sacred animal now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAnimal()
        {
            var b = Book;
            if (b == null) return;
            var map = Find.CurrentMap;
            PickGod(g => SacredAnimal.KindsFor(g, map).Count > 0, g => Find.WindowStack.Add(new FloatMenu(SacredAnimal.KindsFor(g, map).Select(k => new FloatMenuOption(k.LabelCap, () =>
            {
                b.beast = null;
                var a = SacredAnimal.Bring(b, map, g, k);
                Say(a != null ? "a " + k.label + " of " + Gods.Name(g) + " comes in from the edge." : "it couldn't come.");
            })).ToList())));
        }

        [DebugAction("Rimfolk", "Gods: the sacred animal leads a colonist...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLead()
        {
            var s = Book?.beast;
            if (s?.a == null || !s.a.Spawned)
            {
                Say("no sacred animal on the land.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(Find.CurrentMap.mapPawns.FreeColonistsSpawned.Select(p => new FloatMenuOption(p.LabelShort, () =>
            {
                s.led = false;
                s.follower = null;
                Say(SacredAnimal.Lead(s, p) ? p.LabelShort + " follows it (" + s.boon + ")." : "it can't lead now.");
            })).ToList()));
        }

        [DebugAction("Rimfolk", "Gods: a god answers in the weather here...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWeather()
        {
            var b = Book;
            if (b == null) return;
            var map = Find.CurrentMap;
            PickGod(GodShapes.Weather, g => Find.WindowStack.Add(new FloatMenu(new[] { 8f, -8f }.Select(by => new FloatMenuOption(by > 0 ? "pleased" : "angered", () =>
            {
                b.NoteWeather(Gods.HomeKey(map), -999999);
                Say(GodWeather.Answer(g, map, by, true) ? Gods.Name(g) + " answers in the weather." : "no answer: its voices don't fit now (rain only when pleased, an eclipse only by day, an aurora only at night) or the land has no such weather.");
            })).ToList())));
        }

        [DebugAction("Rimfolk", "Gods: an omen here...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOmen()
        {
            var map = Find.CurrentMap;
            if (Book == null) return;
            Find.WindowStack.Add(new FloatMenu(((OmenKind[])Enum.GetValues(typeof(OmenKind))).Select(k => new FloatMenuOption(k + (GodOmens.Can(map, k) ? "" : " (can't show here)"), () =>
                PickGod(_ => true, g => Say(GodOmens.Show(new GodOmenNote { god = g.id, map = map, kind = k, at = Find.TickManager.TicksGame }) ? k + " shown." : k + " couldn't show now."))))
                .ToList()));
        }

        [DebugAction("Rimfolk", "Gods: someone visits a god's mark now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugVisit()
        {
            var b = Book;
            if (b == null) return;
            var map = Find.CurrentMap;
            var mark = b.marks.Where(m => m.home == Homes.KeyOf(map)).OrderByDescending(m => m.tick).FirstOrDefault();
            var who = map.mapPawns.FreeColonistsSpawned.Where(Together.Able).RandomElementWithFallback();
            if (mark == null || who == null)
            {
                Say("no mark here, or nobody free.");
                return;
            }
            Say(GodMarks.Send(who, mark) ? who.LabelShort + " walks out to the mark." : "couldn't send anyone.");
        }

        [DebugAction("Rimfolk", "Gods: a god rises from the land now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRise()
        {
            var map = Find.CurrentMap;
            PickGod(GodShapes.Rises, g => Say(GodRising.Rise(map, g) ? Gods.Name(g) + " rises." : "it can't rise now (dark, nowhere to rise, or its true form's mod not loaded)."));
        }

        [DebugAction("Rimfolk", "Gods: the sleeper under the mountain wakes now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWake()
        {
            var b = Book;
            var god = GodWaking.Sleeper;
            var map = Find.CurrentMap;
            if (b == null || god == null)
            {
                Say("no god sleeps under the mountain in this pantheon.");
                return;
            }
            var miner = GodWaking.Miner(out var m) ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            Say(GodWaking.Wake(b, god, m ?? map, miner, Find.TickManager.TicksGame) ? Gods.Name(god) + " wakes." : "it can't wake now (already up).");
        }

        [DebugAction("Rimfolk", "Gods: the rock turns over a miner now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTurn()
        {
            var b = Book;
            var god = GodWaking.Sleeper;
            var miner = GodWaking.Miner(out var map);
            if (b == null || god == null || miner == null)
            {
                Say("no sleeper in this pantheon, or nobody mining under thick rock.");
                return;
            }
            GodWaking.Turned(b, god, map, miner, Find.TickManager.TicksGame);
            Say("the rock rumbles over " + miner.LabelShort + " (" + b.deepWarnings + " times now).");
        }

        [DebugAction("Rimfolk", "Gods: someone sits near the sacred beast now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSit()
        {
            var s = Book?.beast;
            var def = DefDatabase<JobDef>.GetNamedSilentFail(JobDriver_SitNearSacredAnimal.JobName);
            var p = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(Together.Able).RandomElementWithFallback();
            if (s?.a == null || !s.a.Spawned || def == null || p == null)
            {
                Say("no sacred beast on the land, or nobody free.");
                return;
            }
            p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(def, s.a), Verse.AI.JobTag.Misc);
            Say(p.LabelShort + " goes to sit near it.");
        }

        [DebugAction("Rimfolk", "Gods: the moon returns a deed tonight...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReturn()
        {
            var b = Book;
            var map = Find.CurrentMap;
            var moon = Gods.All.FirstOrDefault(g => g != null && GodShapes.Of(g).returns);
            if (b == null || moon == null)
            {
                Say("no god returns the day by night in this pantheon.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(((GodsOnMapMath.NightReturn[])Enum.GetValues(typeof(GodsOnMapMath.NightReturn))).Select(k => new FloatMenuOption(
                k == GodsOnMapMath.NightReturn.None ? "the day's own deed" : k.ToString(),
                () => Say(GodReturn.Return(b, moon, map, Find.TickManager.TicksGame, k) ? "returned (" + k + "), the moon " + GodOmens.MoonTonight + "." : "nothing to return, or it couldn't show."))).ToList()));
        }

        [DebugAction("Rimfolk", "Gods: the gods on the map (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLog()
        {
            var b = Book;
            if (b == null) return;
            int now = Find.TickManager.TicksGame;
            var sb = new StringBuilder("[Rimfolk] Gods on the map (" + Gods.Realness + "):\n");
            var c = b.coming;
            sb.AppendLine("  coming: " + (c == null ? "none" : c.kind + " of " + Gods.Name(Gods.ById(c.god)) + " in " + ((c.at - now) / 2500f).ToString("0.0") + " h, waited " + c.waited));
            foreach (var o in b.omens) sb.AppendLine("  omen: " + o.kind + " of " + Gods.Name(Gods.ById(o.god)) + " in " + ((o.at - now) / 2500f).ToString("0.0") + " h");
            var s = b.stranger;
            if (s != null)
                sb.AppendLine("  stranger: " + s.x?.LabelShort + " as " + Gods.Name(s.God) + ", " + s.phase + ", " + s.welcome + ", ate " + s.ate + ", slept " + s.slept + ", talkers "
                              + s.talkers.Count + ", kindnesses " + s.kindnesses + ", content " + (s.contentN > 0 ? (s.contentSum / s.contentN).ToString("0.00") : "?") + ", kind: "
                              + string.Join(", ", s.kind.Select((p, i) => p?.LabelShort + " " + (i < s.points.Count ? s.points[i] : 0))));
            var a = b.beast;
            if (a != null)
                sb.AppendLine("  beast: " + a.a?.LabelShort + " of " + Gods.Name(a.God) + ", leaves in " + ((a.leaveAt - now) / 2500f).ToString("0.0") + " h, fed " + a.fed + ", led " + a.led
                              + ", hurt " + a.hurt + ", chosen " + a.chosen?.LabelShort);
            sb.AppendLine("  last: stranger " + (now - b.lastStranger) / 60000 + " d ago, animal " + (now - b.lastAnimal) / 60000 + " d ago, any " + (now - b.lastAny) / 60000 + " d ago");
            sb.AppendLine("  the moon tonight: " + GodOmens.MoonTonight + "; the deep rumbled " + b.deepWarnings + " times; woken " + b.woken?.LabelShort + "; risen " + b.risen?.LabelShort
                          + "; last return " + (now - b.lastReturn) / 60000 + " d ago");
            foreach (var m in b.marks) sb.AppendLine("  mark: " + Gods.Name(Gods.ById(m.god)) + " at " + m.cell + " (" + m.kind + ", " + (now - m.tick) / 60000 + " d, " + m.visits + " visits): " + m.words);
            foreach (var g in Gods.All.Take(80))
            {
                var shape = GodShapes.Of(g);
                sb.AppendLine("  shape of " + Gods.Name(g) + ": " + shape.defName + (shape.stranger ? ", stranger" : "") + (shape.beast ? ", beast" : "") + (shape.weather ? ", weather" : "")
                              + (shape.bodiless ? ", bodiless" : ""));
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }
    }
}
