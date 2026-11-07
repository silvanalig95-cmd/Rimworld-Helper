using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the colony's animals with powers (Debug actions, "Rimfolk", "Animal powers: ..."): what each part sees now,
    /// in the log; the Pokémon types' pass at once; a spar, a binding, an omen's birth and a familiar's death on demand; the
    /// sacred animals' day asked now. Each works in any save that has what it needs (the mod, a mage, an animal).
    /// </summary>
    public static class AnimalPowersDebug
    {
        private static void Say(string text) => Messages.Message("Animal powers: " + text, MessageTypeDefOf.SilentInput, false);

        private static int Now => Find.TickManager.TicksGame;

        private static List<Pawn> Selected => Find.Selector.SelectedPawns.ToList();

        [DebugAction("Rimfolk", "Animal powers: what each part sees now (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLog()
        {
            var sb = new StringBuilder("[Rimfolk] Animal powers:\n");
            var book = AnimalPowersBook.Instance;
            int now = Now;
            sb.AppendLine("Pokémon types: " + (PokeWorldLink.Active ? PokeWorldTypes.On ? "on" : "off in the settings" : "PokéWorld not loaded"));
            if (PokeWorldTypes.On)
            {
                PokeWorldTypes.Hourly(now);
                foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
                    foreach (var (p, mask) in PokeWorldTypes.Here(map))
                    {
                        var powers = ((PokemonPower[])System.Enum.GetValues(typeof(PokemonPower))).Where(x => AnimalPowersMath.Has(mask, x)).Select(x => x + (PokeWorldTypes.PartOn(x) ? "" : " (off)"));
                        var room = p.Spawned ? p.GetRoom() : null;
                        sb.AppendLine("  " + p.LabelShort + " (" + string.Join("/", PokeWorldLink.TypesOf(p.def)) + "): " + string.Join(", ", powers)
                                      + "; " + (PokeWorldTypes.Fit(p, map) ? PokeWorldTypes.Resting(p) ? "resting" : "moving" : "not up")
                                      + (room == null ? "" : room.UsesOutdoorTemperature ? ", outdoors" : ", room " + room.Temperature.ToString("0.0") + " °C, " + room.CellCount + " cells")
                                      + (p.Spawned && PokeWorldTypes.BatteryBeside(p, map) != null ? ", a battery beside" : "")
                                      + (book != null ? ", calmed last " + book.CalmedAt(p) : ""));
                    }
            }
            sb.AppendLine("Familiars: " + (!ModsConfig.IsActive(MagicGolems.PackageId) ? "A RimWorld of Magic not loaded" : MagicFamiliars.On ? "on" : MagicFamiliars.Ready() ? "off in the settings" : "the mod's mana not found"));
            if (book != null)
            {
                foreach (var f in book.familiars)
                    sb.AppendLine("  " + f.mage?.LabelShort + " and " + f.animal?.LabelShort + ", since " + f.since + (f.mage != null && f.animal != null && f.mage.Spawned && f.animal.Spawned && f.mage.Map == f.animal.Map
                        ? ", " + f.mage.Position.DistanceTo(f.animal.Position).ToString("0") + " cells apart" : ""));
                foreach (var b in book.bindings)
                    sb.AppendLine("  waiting: " + b.mage?.LabelShort + " to bind " + b.animal?.LabelShort + (b.byWish ? " (the council's answer)" : " (their own choice)") + ", tries " + b.tries);
            }
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.Where(MagicFamiliars.OurMage))
                sb.AppendLine("  a mage: " + p.LabelShort + ", mana " + (MagicFamiliars.ManaOf(p)?.CurLevel.ToString("0.00") ?? "?") + ", would choose " + (MagicFamiliars.Candidate(p, p.Map)?.LabelShort ?? "nobody"));
            sb.AppendLine("Omens: " + (OmenAnimals.On ? "on" : "off") + ", the last " + (book?.lastOmenTick ?? -1) + ", a kept day today: " + (OmenAnimals.KeptDayToday(Find.CurrentMap) ?? "none")
                          + ", the sign now over this map: " + OmenAnimals.SignNow(Find.CurrentMap, null, out _));
            if (book != null)
                foreach (var o in book.omens)
                    sb.AppendLine("  " + o.name + " (" + o.kind + "), born under " + AnimalPowersMath.Key(o.sign) + (o.about != null ? " (" + o.about + ")" : "") + ", " + (OmenAnimals.Thrives(o.animal) ? "thriving" : "not thriving"));
            sb.AppendLine("Sacred: " + (!ModsConfig.IdeologyActive ? "Ideology not loaded" : SacredAnimals.On ? "on" : "off") + ", the faith's kinds: "
                          + (SacredAnimals.Kinds().Count == 0 ? "none" : string.Join(", ", SacredAnimals.Kinds().Select(k => k.label))) + ", on this map: "
                          + string.Join(", ", SacredAnimals.Here(Find.CurrentMap).Select(a => a.LabelShort)));
            Log.Message(sb.ToString());
            Say("written to the log.");
        }

        [DebugAction("Rimfolk", "Animal powers: the Pokémon types' pass now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTypes()
        {
            if (!PokeWorldTypes.On)
            {
                Say("PokéWorld isn't loaded, or the setting is off.");
                return;
            }
            PokeWorldTypes.Hourly(Now);
            PokeWorldTypes.Rare(AnimalPowersBook.Instance, Now);
            Say("done (" + PokeWorldTypes.Here(Find.CurrentMap).Count() + " typed Pokémon of the colony here).");
        }

        [DebugAction("Rimfolk", "Animal powers: the selected colonist spars with a Fighting type (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSpar()
        {
            var p = Selected.FirstOrDefault(x => x.IsColonist);
            if (p == null || PokeWorldTypes.SparJob == null)
            {
                Say("select a colonist (with PokéWorld loaded).");
                return;
            }
            PokeWorldTypes.Hourly(Now);
            var mon = PokeWorldTypes.SparPartner(p);
            if (mon == null)
            {
                Say("no Fighting type of the colony's free and in reach here.");
                return;
            }
            var job = JobMaker.MakeJob(PokeWorldTypes.SparJob, mon);
            job.count = AnimalPowersMath.SparMinTicks;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            Say(p.LabelShort + " goes to spar with " + mon.LabelShort + ".");
        }

        [DebugAction("Rimfolk", "Animal powers: bind the selected animal to the selected mage now (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBind()
        {
            var mage = Selected.FirstOrDefault(MagicFamiliars.OurMage);
            var a = Selected.FirstOrDefault(x => x.RaceProps.Animal) ?? MagicFamiliars.Candidate(mage, mage?.Map);
            if (mage == null || a == null || MagicFamiliars.BindJob == null || !MagicFamiliars.Bindable(a, mage))
            {
                Say("select a mage of A RimWorld of Magic (and one of the colony's animals that is nobody else's, or none for their own choice).");
                return;
            }
            var book = AnimalPowersBook.Instance;
            if (book?.FamiliarOf(mage) != null) book.familiars.Remove(book.FamiliarOf(mage));
            MagicFamiliars.Queue(mage, a, false);
            var job = JobMaker.MakeJob(MagicFamiliars.BindJob, a);
            job.count = AnimalPowersMath.BindingTicks;
            mage.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            Say(mage.LabelShort + " goes to bind " + a.LabelShort + " as a familiar.");
        }

        [DebugAction("Rimfolk", "Animal powers: the selected mage's familiar dies (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFamiliarDies()
        {
            var book = AnimalPowersBook.Instance;
            var f = Selected.Select(p => book?.FamiliarOf(p) ?? book?.BondOfAnimal(p)).FirstOrDefault(x => x != null);
            if (f?.animal == null || f.animal.Dead)
            {
                Say("select a mage with a familiar, or the familiar.");
                return;
            }
            f.animal.Kill(null);
            MagicFamiliars.Rare(book, Now);
            Say(f.name + " is dead; " + f.mage.LabelShort + " feels it.");
        }

        [DebugAction("Rimfolk", "Animal powers: the selected animal was born under a sign (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOmen()
        {
            var a = Selected.FirstOrDefault(x => AnimalFacts.Ours(x));
            var book = AnimalPowersBook.Instance;
            if (a == null || book == null)
            {
                Say("select one of the colony's animals.");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var sign in new[] { OmenSign.Eclipse, OmenSign.Aurora, OmenSign.Shiny, OmenSign.KeptDay })
            {
                var s = sign;
                options.Add(new FloatMenuOption(AnimalPowersMath.Key(s), () =>
                {
                    string day = s == OmenSign.KeptDay ? OmenAnimals.KeptDayToday(a.Map) ?? ColonyCalendar.For(a.Map)?.days.FirstOrDefault()?.label ?? "the founding day" : null;
                    OmenAnimals.Born(book, a, a.Map, s, day, Now);
                    OmenAnimals.Hourly(book, Now);
                    Say(a.LabelShort + " is an omen now (" + AnimalPowersMath.Key(s) + ").");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Animal powers: ask the sacred animals' day now (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSacredDay()
        {
            var map = Find.CurrentMap;
            var tracker = DilemmaTracker.For(map);
            if (!SacredAnimals.On || tracker == null)
            {
                Say("Ideology isn't active, the setting is off, or no council here.");
                return;
            }
            if (!SacredAnimals.Here(map).Any())
            {
                Say("no animal the colony's faith holds sacred is on this map (" + (SacredAnimals.Kinds().Count == 0 ? "the faith holds none sacred" : string.Join(", ", SacredAnimals.Kinds().Select(k => k.label))) + ").");
                return;
            }
            bool asked = tracker.TryFire(true, d => d.Worker is DilemmaWorker_SacredDay);
            Say(asked ? "asked." : "not asked (no believer to ask it, or a question is waiting).");
        }
    }
}
