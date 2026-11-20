using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The four seats that worked only through numbers, seen at their work (the content update's A1, 20 November 2026), as
    // the others are (Interplay_Offices.cs, _Offices2.cs): the speaker sits an hour with whoever trusts the council least
    // and hears them out; the quartermaster walks round the stores at dawn once a week and the count goes in the news and
    // This week; the reeve walks the fields at first light in the growing season and marks blight for cutting with the
    // game's own order; the marshal walks the walls at dusk in the two days after an attack, with a word for whoever is on
    // watch. Each is a play-out of the colony's own (PlayOut.routine, with "seat:" in PlayOut.afterward: never counted as
    // one of the colony's gatherings, never waiting for room among them, let go quietly if it can't happen); with
    // "Answers play out on the map" off, the speaker walks over for a word (Together.When), the count and the marks are
    // made without the walk, and the marshal's evening isn't kept. The rules are Offices/SeatMath.cs's.
    public static partial class Interplay
    {
        public const string Speaker = "Council_Office_Speaker";
        public const string Quartermaster = "Council_Office_Quartermaster";

        /// <summary>A seat's own work on the map: PlayOut.afterward starts with this (PlayAfter.Run hands it back here).</summary>
        public const string SeatPrefix = "seat:";

        private const string SeatHear = SeatPrefix + "hear", SeatCount = SeatPrefix + "count", SeatFields = SeatPrefix + "fields", SeatWalls = SeatPrefix + "walls";

        private static readonly Color SeatColor = new Color(0.85f, 0.9f, 1f);

        /// <summary>One of the seats' own walks or sittings (not an answer the player gave, and not a gathering).</summary>
        public static bool IsSeatWork(PlayOut p) => p?.afterward != null && p.afterward.StartsWith(SeatPrefix, System.StringComparison.Ordinal);

        /// <summary>The council's day (Daily): the speaker, the quartermaster and the reeve see whether today is theirs.</summary>
        private static void SeatsDaily(CouncilGameComponent comp)
        {
            Safe("interplay: speaker", () =>
            {
                foreach (var sp in Officers(Speaker)) SpeakerDay(comp, sp);
            });
            Safe("interplay: quartermaster", () =>
            {
                foreach (var qm in Officers(Quartermaster)) CountDay(qm);
            });
            Safe("interplay: reeve's walk", () =>
            {
                foreach (var r in Officers(Reeve)) ReeveDay(r);
            });
        }

        /// <summary>The council's hour (Hourly): the marshal's dusk after an attack.</summary>
        private static void SeatsHourly()
        {
            foreach (var m in Officers(Marshal)) MarshalDusk(m);
        }

        /// <summary>Not fired within <paramref name="days"/> (as <see cref="Ready"/>, without starting the wait).</summary>
        private static bool Due(string key, float days) => !cooldowns.TryGetValue(key, out int t) || t > Now || Now - t >= days * Day;

        private static void Mark(string key) => cooldowns[key] = Now;

        /// <summary>Dev mode (Offices/SeatDebug.cs): the next seat's play-out may start in the next few hours, whatever its own hours are.</summary>
        private static bool debugNow;

        /// <summary>A seat's play-out set going from code: the colony's own, never a gathering, quietly let go if it can't be.</summary>
        private static PlayOut Begin(DilemmaEffect_PlayOut effect, Pawn holder, Pawn other, string afterward)
        {
            effect.routine = true;
            if (debugNow)
            {
                int h = GenLocalDate.HourOfDay(holder.Map);
                effect.hours = new IntRange(h, (h + 3) % 24);
            }
            var play = effect.Begin(new DilemmaContext { map = holder.Map, a = holder, b = other });
            if (play != null) play.afterward = afterward;
            return play;
        }

        // ---------------- The speaker hears someone out ----------------

        private static void SpeakerDay(CouncilGameComponent comp, Pawn sp)
        {
            if (!Able(sp) || sp.Map == null || !sp.Map.IsPlayerHome || comp?.standings == null) return;
            string key = "speaker|" + sp.thingIDNumber;
            if (!Due(key, SeatMath.SpeakerEvery)) return;
            var people = sp.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != sp && Ours(p) && !p.DevelopmentalStage.Juvenile() && CouncilGameComponent.CanVoiceWishes(p)).ToList();
            if (people.Count == 0) return;
            int i = SeatMath.Lowest(people.Select(p => comp.standings.TrustOf(p)).ToList(), people.Select(p => Due("heard|" + p.thingIDNumber, SeatMath.HearAgain)).ToList());
            // Dev mode: whoever trusts the council least, however much that is.
            if (i < 0 && debugNow) i = people.IndexOf(people.OrderBy(p => comp.standings.TrustOf(p)).First());
            if (i < 0) return;
            var them = people[i];
            Mark(key);
            Mark("heard|" + them.thingIDNumber);
            if (PlayOuts.On)
            {
                var label = OfficeNames.Key("CC_Seat_Label_Hear");
                var play = Begin(new DilemmaEffect_Visit
                {
                    who = PlayWho.A, to = PlaySpot.Person, whom = PlayWho.B, sit = true, sitBy = PlaySpot.Dining, label = label,
                    hours = new IntRange(9, 20), duration = new FloatRange(SeatMath.HearHours, SeatMath.HearHours), waitHours = 12f
                }, sp, them, SeatHear);
                if (play != null) return;
            }
            // Without the map's play-outs: a word, when the two are together, or the speaker walking over in their free time.
            var (speaker, low) = (sp, them);
            Together.When("speaker|hear|" + low.thingIDNumber, speaker, low, () => HeardOut(speaker, low, null), 24f, 2f);
        }

        /// <summary>The hour is over (or, without the play-outs, the word was had): their trust comes back a little, a line each, a word in the news.</summary>
        private static void HeardOut(Pawn sp, Pawn them, PlayOut play)
        {
            // together: the two sat an hour across a table (the visit's own job), or met for a word (Together.When).
            var comp = Comp;
            if (!On || comp == null || sp == null || them == null || sp.Dead || them.Dead || !Holds(sp, Speaker)) return;
            float trust = SeatMath.HeardTrust(sp.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0);
            comp.standings.Change(them, trust, OfficeNames.Key("CC_Log_SeatHeard", sp.Named("PAWN")));
            string theirs = Line("Rimfolk_Pool_SeatHeardSays", them, null, sp.Named("OTHER"));
            bool said = !theirs.NullOrEmpty() && Speech.Say(them, theirs, sp, SeatColor);
            string reply = Line("Rimfolk_Pool_SeatHearReply", sp, null, them.Named("OTHER"));
            if (!reply.NullOrEmpty()) Speech.Say(sp, reply, them, SeatColor);
            string text = OfficeNames.Key(play != null ? "CC_Seat_HeardSat" : "CC_Seat_HeardWord", sp.Named("PAWN"), them.Named("OTHER"));
            // The visit tells its own outcome when it ends; a word without the play-outs is told here.
            if (play != null) play.outcome = text;
            else News.Post(text, new LookTargets(sp, them), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
            if (Rand.Chance(0.35f)) Journal(them, "Rimfolk_Journal_SeatHeard", null, sp.Named("OTHER"));
        }

        // ---------------- The quartermaster's count ----------------

        private static void CountDay(Pawn qm)
        {
            var council = ColonyState.Of(qm)?.offices;
            if (!Able(qm) || qm.Map == null || !qm.Map.IsPlayerHome || council == null) return;
            if (Now - council.storesCountTick < SeatMath.CountEvery * Day && council.storesCountTick >= 0) return;
            if (!Ready("count|" + qm.thingIDNumber, 1f)) return;
            if (PlayOuts.On)
            {
                var play = Begin(new DilemmaEffect_Procession
                {
                    target = DilemmaTarget.A, count = 1, organizer = PlayWho.A, route = PlayRoutes.Stores, label = OfficeNames.Key("CC_Seat_Label_Count"),
                    hours = new IntRange(SeatMath.DawnFrom, SeatMath.DawnTo), duration = new FloatRange(1f, 1.5f), waitHours = 4f
                }, qm, null, SeatCount);
                if (play != null) return;
            }
            Counted(qm, null);
        }

        /// <summary>The round of the stores is over: the count, in the news and on This week, and a word if something is short.</summary>
        private static void Counted(Pawn qm, PlayOut play)
        {
            var council = ColonyState.Of(qm)?.offices;
            var map = play?.map ?? qm.MapHeld;
            if (council == null || map == null || !Holds(qm, Quartermaster)) return;
            int people = Mathf.Max(1, map.mapPawns.FreeColonistsSpawnedCount);
            float food = MapFacts.Of(map).FoodDays;
            int medicine = MapFacts.Of(map).Medicine;
            int meals = MealWorth.StoredServings(map, d => d.ingestible != null && d.ingestible.IsMeal && d.ingestible.HumanEdible && !d.IsDrug);
            var shortest = SeatMath.Shortest(food, medicine / (float)people, meals / (float)people);
            string days = food >= 999f ? "CC_Seat_CountDaysMany".Translate().Resolve() : Mathf.FloorToInt(food).ToString();
            string text = OfficeNames.Key(play != null ? "CC_Seat_Count" : "CC_Seat_CountNoWalk", qm.Named("PAWN"), days.Named("DAYS"), meals.Named("MEALS"), medicine.Named("MEDICINE"))
                          + " " + ("CC_Seat_Short_" + shortest).Translate().Resolve();
            council.storesCountTick = Now;
            council.storesCount = text;
            // Said only when something really is short.
            // apart: said aloud at the last store of the round, to nobody in particular.
            if (shortest != StoresShort.None && qm.Spawned)
            {
                string word = Line("Rimfolk_Pool_SeatCountShort", qm, new[] { "short:" + shortest.ToString().ToLowerInvariant() });
                if (!word.NullOrEmpty()) Speech.Say(qm, word, null, SeatColor);
            }
            if (play != null)
            {
                play.outcome = text;
                play.note = "";
            }
            else News.Post(text, new LookTargets(qm), NewsKind.Colony, MessageTypeDefOf.SilentInput, Importance.Flavor);
        }

        // ---------------- The reeve's walk ----------------

        private static void ReeveDay(Pawn reeve)
        {
            if (!Able(reeve) || reeve.Map == null || !reeve.Map.IsPlayerHome || !FieldBlight.Fields(reeve.Map).Any()) return;
            var map = reeve.Map;
            var season = GenLocalDate.Season(map);
            bool growing = (season == Season.Spring || season == Season.Summer || season == Season.PermanentSummer)
                           && FieldBlight.Fields(map).Any(z => z.cells.Any(c => c.GetPlant(map) != null));
            bool blight = FieldBlight.Blighted(map, false).Count > 0;
            string key = "reeve|" + reeve.thingIDNumber;
            float since = cooldowns.TryGetValue(key, out int t) && t <= Now ? (Now - t) / (float)Day : 999f;
            if (!SeatMath.ReeveDue(growing, blight, since) && !debugNow) return;
            Mark(key);
            if (PlayOuts.On)
            {
                var play = Begin(new DilemmaEffect_Procession
                {
                    target = DilemmaTarget.A, count = 1, organizer = PlayWho.A, route = PlayRoutes.Fields, label = OfficeNames.Key("CC_Seat_Label_Fields"),
                    hours = new IntRange(SeatMath.DawnFrom, SeatMath.DawnTo), duration = new FloatRange(1f, 1.5f), waitHours = 4f
                }, reeve, null, SeatFields);
                if (play != null) return;
            }
            // Without the walk: the blight marked all the same, and told.
            var sick = FieldBlight.Blighted(map, false);
            if (sick.Count == 0) return;
            foreach (var p in sick) FieldBlight.Mark(map, p, DesignationDefOf.CutPlant);
            News.Post(FieldsText(reeve, sick.Count, false), new LookTargets(reeve), NewsKind.Colony, MessageTypeDefOf.SilentInput, Importance.Flavor);
        }

        /// <summary>
        /// The reeve at a field on the morning walk (PlayRoutes.AtStop): the blight in it marked for cutting with the game's
        /// own order, and said when there was some. The plants marked go on the walk's tally.
        /// </summary>
        public static void AtField(PlayOut p, Zone_Growing field)
        {
            var reeve = p?.hands.FirstOrDefault()?.pawn;
            var map = p?.map;
            if (reeve == null || map == null || field == null) return;
            int n = 0;
            ThingDef crop = null;
            foreach (var c in field.cells)
            {
                var plant = c.GetPlant(map);
                if (plant == null || !plant.Blighted || map.designationManager.DesignationOn(plant, DesignationDefOf.CutPlant) != null) continue;
                FieldBlight.Mark(map, plant, DesignationDefOf.CutPlant);
                crop = crop ?? plant.def;
                n++;
            }
            if (n == 0) return;
            p.tally += n;
            // apart: said aloud at the field, to nobody in particular.
            string word = Line("Rimfolk_Pool_SeatBlight", reeve, null, (crop?.label ?? "").Named("CROP"));
            if (!word.NullOrEmpty()) Speech.Say(reeve, word, null, SeatColor);
        }

        private static string FieldsText(Pawn reeve, int marked, bool walked) =>
            OfficeNames.Key(marked > 0 ? (walked ? "CC_Seat_FieldsBlight" : "CC_Seat_FieldsBlightNoWalk") : "CC_Seat_FieldsClear", reeve.Named("PAWN"), marked.Named("COUNT"));

        // ---------------- The marshal at dusk ----------------

        private static void MarshalDusk(Pawn marshal)
        {
            if (!PlayOuts.On || !Able(marshal) || marshal.Map == null || !marshal.Map.IsPlayerHome) return;
            var map = marshal.Map;
            int last = map.storyState?.LastThreatBigTick ?? -1;
            if (last <= 0) return;
            // Set going in the afternoon before dusk, once for each evening.
            int hour = GenLocalDate.HourOfDay(map);
            if ((hour < 12 || hour >= SeatMath.DuskTo) && !debugNow) return;
            string key = "marshal|dusk|" + marshal.thingIDNumber;
            float since = cooldowns.TryGetValue(key, out int t) && t <= Now ? (Now - t) / (float)Day : 999f;
            if (!SeatMath.MarshalDue((Now - last) / (float)Day, since)) return;
            // Not while the attack is still on the map.
            if (MapFacts.HostilesOn(map)) return;
            Mark(key);
            Begin(new DilemmaEffect_Vigil
            {
                target = DilemmaTarget.A, count = 1, organizer = PlayWho.A, to = PlaySpot.Gate, patrol = true, label = OfficeNames.Key("CC_Seat_Label_Walls"),
                hours = new IntRange(SeatMath.DuskFrom, SeatMath.DuskTo), duration = new FloatRange(SeatMath.MarshalHours, SeatMath.MarshalHours), waitHours = 2f
            }, marshal, null, SeatWalls);
        }

        /// <summary>
        /// The marshal at a look-out on the dusk walk (PlayVigils.Arrived): a word for whoever is on watch near it (drafted,
        /// or keeping a watch of their own), each once a walk.
        /// </summary>
        public static void AtLookOut(PlayOut p, Pawn marshal)
        {
            if (!On || p == null || marshal == null || !marshal.Spawned || p.afterward != SeatWalls) return;
            var t = PlayOutTracker.Instance;
            foreach (var x in marshal.Map.mapPawns.FreeColonistsSpawned)
            {
                if (x == marshal || !Ours(x) || !x.Position.InHorDistOf(marshal.Position, 8f)) continue;
                var g = t?.GroupFor(x);
                bool onWatch = x.Drafted || (g != null && g != p && g.kind == PlayKind.Vigil);
                if (!onWatch || !Ready("marshalword|" + p.id + "|" + x.thingIDNumber, 1f)) continue;
                // together: a few steps apart at the look-out; to a drafted watcher it is said aloud, not as a chat.
                string word = Line("Rimfolk_Pool_SeatOnWatch", marshal, null, x.Named("OTHER"));
                if (!word.NullOrEmpty()) Speech.Say(marshal, word, Together.Can(marshal, x) ? x : null, SeatColor);
                p.tally++;
                return;
            }
        }

        // ---------------- A seat's walk or sitting is over ----------------

        /// <summary>
        /// What a seat's play-out leaves when it's over (PlayAfter.Run): <paramref name="happened"/> false when it never came
        /// about, and then nothing is claimed.
        /// </summary>
        public static void SeatWorkOver(PlayOut p, string what, bool happened)
        {
            if (!happened || p == null) return;
            switch (what)
            {
                case DilemmaEffect_SeatSupper.After:
                    // The holder answered for their promise at the table: the hard call of their new seat (Offices/SeatHalfway.cs).
                    if (p.organizer != null) SeatCalls.Call(p.organizer, SeatMath.CallSupper, p.what ?? "");
                    break;
                case SeatHear:
                    HeardOut(p.visitor, p.other, p);
                    break;
                case SeatCount:
                {
                    var qm = p.hands.FirstOrDefault()?.pawn;
                    // Only a round that reached its last store is a count.
                    if (qm != null && p.plots.Count > 0 && p.amount + 1 >= p.plots.Count) Counted(qm, p);
                    break;
                }
                case SeatFields:
                {
                    var reeve = p.hands.FirstOrDefault()?.pawn;
                    if (reeve == null) break;
                    p.outcome = FieldsText(reeve, p.tally, true);
                    p.note = "";
                    break;
                }
                case SeatWalls:
                {
                    var marshal = p.hands.FirstOrDefault()?.pawn;
                    if (marshal == null) break;
                    p.outcome = OfficeNames.Key(p.tally > 0 ? "CC_Seat_WallsWord" : "CC_Seat_Walls", marshal.Named("PAWN"), p.tally.Named("COUNT"));
                    p.note = "";
                    break;
                }
            }
        }

        // ---------------- Dev mode (Offices/SeatDebug.cs) ----------------

        /// <summary>Runs a seat's day now, its wait forgotten and its hours from now (dev mode). Null when it went, else why not.</summary>
        internal static string DebugSeat(string office, Pawn holder)
        {
            if (!On || holder == null) return "the setting \"Systems talk to each other\" is off, or nobody holds the seat";
            if (!Able(holder)) return holder.LabelShort + " isn't up and about";
            foreach (var k in cooldowns.Keys.Where(k => k.EndsWith("|" + holder.thingIDNumber)).ToList()) cooldowns.Remove(k);
            debugNow = true;
            try
            {
                switch (office)
                {
                    case Speaker:
                        foreach (var k in cooldowns.Keys.Where(k => k.StartsWith("heard|")).ToList()) cooldowns.Remove(k);
                        SpeakerDay(Comp, holder);
                        break;
                    case Quartermaster:
                        var council = ColonyState.Of(holder)?.offices;
                        if (council != null) council.storesCountTick = -1;
                        CountDay(holder);
                        break;
                    case Reeve:
                        ReeveDay(holder);
                        break;
                    case Marshal:
                        int t = holder.Map?.storyState?.LastThreatBigTick ?? -1;
                        if (t <= 0 || Now - t > SeatMath.MarshalDays * Day) return "no attack in the last two days (raise a raid first)";
                        if (!PlayOuts.On) return "the marshal's walk needs \"Answers play out on the map\"";
                        MarshalDusk(holder);
                        break;
                }
            }
            finally
            {
                debugNow = false;
            }
            return null;
        }

        /// <summary>The words a seat's walk begins with (PlayWords.BeginText): one person going round, not a group.</summary>
        public static string SeatBeginText(PlayOut p, string names, string where)
        {
            string label = (p.label ?? "").CapitalizeFirst();
            return (p.kind == PlayKind.Vigil ? "CC_Seat_BeginWalls" : "CC_Seat_BeginRound").Translate(label.Named("LABEL"), names.Named("NAMES"), (where ?? "").Named("WHERE")).Resolve();
        }
    }
}
