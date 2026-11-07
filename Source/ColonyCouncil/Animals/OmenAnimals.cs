using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Omen animals (the player's pick of 7 November 2026): an animal born to the colony under a sign is talked about, given
    /// a name and remembered, with a modest real effect. The signs, in that order: an eclipse over its map, an aurora (both
    /// the game's own conditions), a shiny Pokémon born or hatched (with PokéWorld), and a day the colony keeps (a tradition,
    /// the founding day, a day the player named or one kept before: half the births on it). At most one omen in 20 days
    /// and three living in a colony (AnimalPowersMath).
    /// <list type="bullet">
    /// <item>Born: news, the chronicle, a name from its keeper (its mother's person, the herdmaster, else whoever is best
    /// with animals), half the time one the sign gives ("Umbra", "Glimmer"), said out loud if the keeper is by, and a word
    /// from whoever is with them; a line in the keeper's journal.</item>
    /// <item>While it thrives (alive, up, not ill, not starving), everyone of its colony is a little glad of it (+2).</item>
    /// <item>If it dies: a bad omen for everyone of its colony (-4 for 5 days), told, and in the chronicle.</item>
    /// </list>
    /// Newborns are looked at once an hour (one born in the last two hours counts as born under the sign there is now);
    /// the omens alive are saved in <see cref="AnimalPowersBook"/>. Setting: "Omen animals" (What's on, with the animals).
    /// </summary>
    public static class OmenAnimals
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.omenAnimals && Current.Game != null;

        private static readonly HashSet<int> seen = new HashSet<int>();
        private static Game seenGame;

        /// <summary>The first omen of each home map that thrives now, as of the last hour (for the mood).</summary>
        private static readonly Dictionary<Map, Pawn> thriving = new Dictionary<Map, Pawn>();

        public static void Hourly(AnimalPowersBook book, int now)
        {
            if (book == null) return;
            if (seenGame != Current.Game)
            {
                seenGame = Current.Game;
                seen.Clear();
                thriving.Clear();
            }
            Fates(book);
            thriving.Clear();
            if (!On) return;
            var here = new List<int>();
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;
                foreach (var a in map.mapPawns.SpawnedColonyAnimals.ToList())
                {
                    if (a?.ageTracker == null) continue;
                    here.Add(a.thingIDNumber);
                    if (!seen.Add(a.thingIDNumber) || a.ageTracker.AgeBiologicalTicks > AnimalPowersMath.BornWithinTicks || book.OmenOf(a) != null) continue;
                    try
                    {
                        Consider(book, a, map, now);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("omens: a newborn", e);
                    }
                }
            }
            if (seen.Count > 4000)
            {
                seen.Clear();
                foreach (int id in here) seen.Add(id);
            }
            foreach (var o in book.omens)
            {
                var a = o.animal;
                if (a == null || !a.Spawned || a.Map == null || thriving.ContainsKey(a.Map) || !Thrives(a)) continue;
                thriving[a.Map] = a;
            }
        }

        public static bool Thrives(Pawn a) =>
            a != null && AnimalPowersMath.Thrives(!a.Dead, a.Downed, AnimalFacts.Illness(a) != null, a.needs?.food?.Starving ?? false);

        /// <summary>The omen of this colonist's home that thrives now (as of the last hour), or null.</summary>
        public static Pawn ThrivingFor(Pawn p)
        {
            var map = p?.MapHeld;
            if (map == null || !thriving.TryGetValue(map, out var a)) return null;
            return a != null && !a.Dead ? a : null;
        }

        // ---------------- A birth under a sign ----------------

        /// <summary>A day on this home's calendar that falls today and that the colony keeps, by its name; null for none.</summary>
        public static string KeptDayToday(Map map)
        {
            var cal = ColonyCalendar.For(map);
            if (cal?.days == null) return null;
            int today = CalendarMath.Wrap(GenLocalDate.DayOfYear(map));
            var d = cal.days.FirstOrDefault(x => x != null && x.day == today
                                                  && AnimalPowersMath.KeptDay(x.Dated, x.Personal, x.tradition, x.kind == CalendarKind.Founding, x.byPlayer, x.keptYears));
            return d?.label;
        }

        /// <summary>The sign over this map now, and the day kept today (for a kept day's omen).</summary>
        public static OmenSign SignNow(Map map, Pawn newborn, out string day)
        {
            day = KeptDayToday(map);
            var gc = map.gameConditionManager;
            bool eclipse = GameConditionDefOf.Eclipse != null && gc.ConditionIsActive(GameConditionDefOf.Eclipse);
            bool aurora = GameConditionDefOf.Aurora != null && gc.ConditionIsActive(GameConditionDefOf.Aurora);
            bool shiny = PokeWorldLink.IsPokemon(newborn) && PokeWorldLife.Shiny(newborn);
            return AnimalPowersMath.SignOf(eclipse, aurora, shiny, day != null);
        }

        private static void Consider(AnimalPowersBook book, Pawn a, Map map, int now)
        {
            var sign = SignNow(map, a, out string day);
            // The kept day's even chance is the animal's own, so a reload doesn't roll it again.
            float roll = Rand.ValueSeeded(a.thingIDNumber ^ 0x51A7);
            if (!AnimalPowersMath.OmenDue(sign, book.lastOmenTick, now, book.LivingOmens(Homes.KeyOf(map)), roll)) return;
            Born(book, a, map, sign, sign == OmenSign.KeptDay ? day : null, now);
        }

        /// <summary>An omen born: named, told, remembered, and talked about by whoever is by.</summary>
        public static void Born(AnimalPowersBook book, Pawn a, Map map, OmenSign sign, string day, int now)
        {
            if (book == null || a == null || map == null || sign == OmenSign.None) return;
            var keeper = HerdStory.Keeper(map, a);
            string kind = AnimalFacts.KindLabel(a);
            if (a.Name == null || a.Name.Numerical) NameIt(a, sign, keeper);
            book.AddOmen(new OmenRecord { animal = a, sign = sign, about = day, born = now, name = a.LabelShort, kind = kind, home = Homes.KeyOf(map) }, now);
            string key = AnimalPowersMath.Key(sign);
            var args = new[] { a.Named("PET"), kind.Named("KIND"), (day ?? "").Named("DAY") };
            string text = ("CC_Omen_Born_" + key).Translate(args).Resolve();
            if (keeper != null) text += " " + "CC_Omen_NamedBy".Translate(keeper.Named("PAWN"), a.Named("PET")).Resolve();
            using (Causes.Push(("CC_Omen_Cause_" + key).Translate(args).Resolve()))
            {
                News.Post(text, new LookTargets(a), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                var comp = CouncilGameComponent.Instance;
                comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, ("CC_Omen_Chronicle_" + key).Translate(args).Resolve(), keeper));
                if (keeper != null) comp?.journals.WriteTagged(keeper, "Rimfolk_Journal_Omen_Born", new[] { "sign:" + key }, null, a.Named("PET"), kind.Named("KIND"), (day ?? "").Named("DAY"));
            }
            if (AnimalPowers.InView(map, a.Position)) AnimalPowers.Glow(map, a.DrawPos, new UnityEngine.Color(1f, 0.95f, 0.75f, 0.5f), 1.6f, 0.7f);
            Talk(keeper, a, key, day);
        }

        /// <summary>The keeper says its name if they're by and in view; whoever is with them says something back.</summary>
        private static void Talk(Pawn keeper, Pawn a, string key, string day)
        {
            if (!Speech.On || keeper == null || !keeper.Spawned || keeper.Map != Find.CurrentMap || !keeper.Position.InHorDistOf(a.PositionHeld, 30f)) return;
            var tags = new[] { "sign:" + key };
            string line = Interplay.Line("Rimfolk_Pool_Omen_Name", keeper, tags, a.Named("PET"), (day ?? "").Named("DAY"));
            // apart: said over the newborn, to whoever hears it.
            if (line.NullOrEmpty() || !Speech.Say(keeper, line)) return;
            var with = Together.Company(keeper);
            if (with == null) return;
            string said = Interplay.Line("Rimfolk_Pool_Omen_Talk", with, tags, keeper.Named("OTHER"), a.Named("PET"), (day ?? "").Named("DAY"));
            // together: Together.Company found them side by side, and Speech.Say checks it again.
            if (!said.NullOrEmpty()) Speech.Say(with, said, keeper);
        }

        /// <summary>Its name: half the time one the sign gives (if no animal of the colony has it), else one of the game's.</summary>
        private static void NameIt(Pawn a, OmenSign sign, Pawn keeper)
        {
            try
            {
                if (keeper != null && Rand.Chance(0.5f))
                {
                    string name = Flavor.FromPool("Rimfolk_Pool_Omen_Names", keeper, null, null, "sign:" + AnimalPowersMath.Key(sign));
                    if (!name.NullOrEmpty())
                    {
                        name = name.Trim().TrimEnd('.');
                        bool taken = a.MapHeld != null && a.MapHeld.mapPawns.SpawnedColonyAnimals.Any(x => x != a && x.Name != null && x.Name.ToStringShort == name);
                        if (!taken && name.Length <= 16)
                        {
                            a.Name = new NameSingle(name);
                            return;
                        }
                    }
                }
                HerdStory.Name(a);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("omens: a name", e);
            }
        }

        // ---------------- Its fate ----------------

        private static void Fates(AnimalPowersBook book)
        {
            foreach (var o in book.omens.ToList())
            {
                var a = o.animal;
                if (a == null || a.Discarded)
                {
                    book.omens.Remove(o);
                    continue;
                }
                if (a.Dead)
                {
                    book.omens.Remove(o);
                    if (On) Died(o);
                    continue;
                }
                // Sold, released or run off: no longer the colony's omen, and no ill luck in it.
                if (a.Faction != Faction.OfPlayer) book.omens.Remove(o);
            }
        }

        /// <summary>A bad omen: everyone of its colony takes it to heart, and it is told and remembered.</summary>
        private static void Died(OmenRecord o)
        {
            var a = o.animal;
            var map = Homes.MapOf(o.home) ?? a.MapHeld;
            string key = AnimalPowersMath.Key(o.sign);
            string name = o.name ?? a.LabelShort;
            var args = new[] { name.Named("NAME"), (o.kind ?? AnimalFacts.KindLabel(a)).Named("KIND"), (o.about ?? "").Named("DAY") };
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Omen_Died");
            using (Causes.Push(("CC_Omen_CauseDied_" + key).Translate(args).Resolve()))
            {
                if (map != null)
                    foreach (var p in map.mapPawns.FreeColonistsSpawned)
                        if (People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()) CouncilGameComponent.GiveThought(p, thought);
                Tell.Message(NewsKind.Land, Importance.Notable, map, ("CC_Omen_Died_" + key).Translate(args).Resolve(), new LookTargets(a.Corpse ?? (Thing)a),
                    MessageTypeDefOf.NegativeEvent);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, "CC_Omen_Chronicle_Died".Translate(args).Resolve()));
            }
            var at = a.Corpse != null && a.Corpse.Spawned ? a.Corpse : null;
            if (!Speech.On || at == null || at.Map != Find.CurrentMap) return;
            var who = at.Map.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && People.HasInnerLife(p) && p.Position.InHorDistOf(at.Position, 20f))
                .OrderBy(p => p.Position.DistanceToSquared(at.Position)).FirstOrDefault();
            if (who == null) return;
            string line = Interplay.Line("Rimfolk_Pool_Omen_Died", who, new[] { "sign:" + key }, name.Named("NAME"));
            // apart: said over the body, to whoever hears it.
            if (!line.NullOrEmpty()) Speech.Say(who, line);
        }
    }

    /// <summary>Everyone of a colony is a little glad while its omen animal thrives (Animals/OmenAnimals.cs).</summary>
    public class ThoughtWorker_OmenThrives : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!OmenAnimals.On || p == null || !p.IsColonist || !People.HasInnerLife(p)) return false;
            return OmenAnimals.ThrivingFor(p) != null;
        }
    }

    /// <summary>The omen's mood, with its name in it.</summary>
    public class Thought_OmenThrives : Thought_Situational
    {
        private Pawn Omen => OmenAnimals.ThrivingFor(pawn);

        public override string LabelCap
        {
            get
            {
                var a = Omen;
                return a == null ? base.LabelCap : "CC_Omen_Thrives_Label".Translate(a.Named("PET")).Resolve().CapitalizeFirst();
            }
        }

        public override string Description
        {
            get
            {
                var a = Omen;
                var o = a == null ? null : AnimalPowersBook.Instance?.OmenOf(a);
                if (o == null) return base.Description;
                return ("CC_Omen_Thrives_Desc_" + AnimalPowersMath.Key(o.sign)).Translate(a.Named("PET"), (o.about ?? "").Named("DAY")).Resolve();
            }
        }
    }
}
