using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: "Rimfolk: check myself" (Debug actions, Rimfolk). One window for the player to screenshot when
    /// something looks wrong: the game, Rimfolk and Harmony versions, which game hooks took and which didn't (and why),
    /// which of Rimfolk's parts are switched on, which of the mods it works with it found, whether a camp or a trade
    /// caravan could come to the home map now (and what stops it), and its own lines in the log this session. Plain English, like the game's own debug tools. Built once when opened (and on "Check again"),
    /// never per frame.
    /// </summary>
    public static class SelfCheck
    {
        [DebugAction("Rimfolk", "Rimfolk: check myself", displayPriority = 1000, allowedGameStates = AllowedGameStates.Playing)]
        private static void CheckMyself() => Find.WindowStack.Add(new Dialog_SelfCheck());

        [DebugAction("Rimfolk", "Send the welcome letter again", allowedGameStates = AllowedGameStates.Playing)]
        private static void WelcomeAgain()
        {
            CouncilGameComponent.Instance?.MarkWelcomed();
            WelcomeLetter.Send();
        }

        /// <summary>One part of the report: a heading, its overall state, and its lines.</summary>
        public class Part
        {
            public string heading;
            public Color color = UiKit.Good;
            public List<(string text, Color color)> lines = new List<(string, Color)>();

            public void Add(string text, Color? color = null) => lines.Add((text, color ?? Color.white));
        }

        /// <summary>
        /// The mods Rimfolk reads or adapts to in code, by name and package id, from the registry the Almanac's
        /// compatibility table uses (ModSupportDefs with code set; Integrations/ModSupport.cs). Content written for
        /// other mods (Almanac pages, events, people) is found from the defs themselves, under "Content for your other mods".
        /// </summary>
        private static IEnumerable<(string name, string[] ids, string live)> Integrations =>
            ModSupport.All.Where(d => d.code).Select(d => (d.LabelCap.ToString(), d.packageIds.ToArray(), d.live));

        public static List<Part> Report()
        {
            var parts = new List<Part>();
            Safe(parts, "Versions", Versions);
            Safe(parts, "Game hooks", Hooks);
            Safe(parts, "Rimfolk's parts", Parts);
            Safe(parts, "Mods Rimfolk works with", Mods);
            Safe(parts, "Camps and trade caravans", Visits);
            Safe(parts, "Faces that come back, the herd and the beasts", Stories);
            Safe(parts, "Rimfolk in the log this session", LogLines);
            return parts;
        }

        /// <summary>Each part on its own: one that throws says so and the rest still show.</summary>
        private static void Safe(List<Part> parts, string heading, Action<Part> fill)
        {
            var part = new Part { heading = heading };
            try
            {
                fill(part);
            }
            catch (Exception e)
            {
                part.color = UiKit.Bad;
                part.Add("Couldn't check this: " + e.GetType().Name + ": " + e.Message, UiKit.Bad);
            }
            parts.Add(part);
        }

        private static void Versions(Part part)
        {
            var meta = ColonyCouncilMod.Pack?.ModMetaData;
            part.Add("RimWorld " + VersionControl.CurrentVersionStringWithRev + ", Rimfolk " + (meta?.ModVersion ?? "?") + ", Harmony "
                     + typeof(Harmony).Assembly.GetName().Version);
            var active = ModsConfig.ActiveModsInLoadOrder.ToList();
            int ours = active.FindIndex(m => (m.PackageId ?? "").ToLowerInvariant().StartsWith(ColonyCouncilMod.PackageId));
            int harmony = active.FindIndex(m => (m.PackageId ?? "").ToLowerInvariant().StartsWith("brrainz.harmony"));
            string order = harmony < 0 ? "Harmony isn't in the mod list (it may come from elsewhere)"
                : ours >= 0 && harmony > ours ? "Harmony loads AFTER Rimfolk: move it above" : "Harmony loads before Rimfolk";
            part.Add(active.Count + " active mods" + (ours >= 0 ? "; Rimfolk is number " + (ours + 1) : "") + ". " + order + ".",
                harmony > ours && ours >= 0 ? UiKit.Bad : Color.white);
            if (harmony > ours && ours >= 0) part.color = UiKit.Bad;
            var dlcs = new List<string>();
            if (ModsConfig.RoyaltyActive) dlcs.Add("Royalty");
            if (ModsConfig.IdeologyActive) dlcs.Add("Ideology");
            if (ModsConfig.BiotechActive) dlcs.Add("Biotech");
            if (ModsConfig.AnomalyActive) dlcs.Add("Anomaly");
            if (ModsConfig.OdysseyActive) dlcs.Add("Odyssey");
            part.Add("DLCs: " + (dlcs.Count > 0 ? string.Join(", ", dlcs) : "none") + ". Language: "
                     + (LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "?") + ".");
            string screen = "Screen " + Screen.width + "x" + Screen.height + " pixels at interface scale " + Prefs.UIScale.ToString("0.##")
                            + " (" + UI.screenWidth + "x" + UI.screenHeight + ").";
            if (RimfolkTab.LastPaneTopY >= 0f)
                screen += " Inspect pane top at " + RimfolkTab.LastPaneTopY.ToString("0") + "; Rimfolk's pawn tabs last drawn "
                          + RimfolkTab.LastHeight.ToString("0") + " tall.";
            part.Add(screen, UiKit.Faint);
        }

        private static void Hooks(Part part)
        {
            var applied = RimfolkPatches.Applied;
            var failed = RimfolkPatches.Failed;
            if (RimfolkPatches.NotStarted != null || applied.Count + failed.Count == 0)
            {
                part.color = UiKit.Bad;
                part.Add("None were applied: " + (RimfolkPatches.NotStarted ?? "is Harmony loaded before Rimfolk?"), UiKit.Bad);
                return;
            }
            part.Add(applied.Count + " applied, " + failed.Count + " didn't.", failed.Count > 0 ? UiKit.Warn : UiKit.Good);
            if (failed.Count > 0) part.color = UiKit.Warn;
            if (!RimfolkPatches.On)
            {
                part.color = UiKit.Warn;
                part.Add("The \"deeper game hooks\" setting is off: they're in place but do nothing.", UiKit.Warn);
            }
            foreach (var name in failed)
                part.Add("Didn't apply: " + name + Target(name) + ". " + (RimfolkPatches.FailedWhy.TryGetValue(name, out var why) ? why : ""), UiKit.Bad);
            part.Add("Applied: " + string.Join(", ", applied.Select(n => n + Target(n))) + ".", UiKit.Faint);

            // What Harmony itself holds: the methods Rimfolk patches, and which other mods patch the same ones.
            int ours = 0;
            var shared = new List<string>();
            foreach (var m in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(m);
                if (info?.Owners == null || !info.Owners.Contains(ColonyCouncilMod.PackageId)) continue;
                ours++;
                var others = info.Owners.Where(o => o != ColonyCouncilMod.PackageId).ToList();
                if (others.Count > 0) shared.Add(m.DeclaringType?.Name + "." + m.Name + " (also " + string.Join(", ", others) + ")");
            }
            part.Add("Harmony lists " + ours + " game methods patched by Rimfolk." + (shared.Count > 0 ? " Also patched by other mods: " + string.Join("; ", shared) + "." : ""),
                UiKit.Faint);
        }

        /// <summary>" (Type.Method)" for a patch class, from its HarmonyPatch attribute, when it names its target there.</summary>
        private static string Target(string patchClass)
        {
            try
            {
                var type = typeof(RimfolkPatches).Assembly.GetTypes().FirstOrDefault(t => t.Name == patchClass);
                if (type == null) return "";
                var infos = type.GetCustomAttributes(typeof(HarmonyPatch), false).OfType<HarmonyAttribute>().Select(a => a.info).Where(i => i != null).ToList();
                string declaring = infos.Select(i => i.declaringType?.Name).LastOrDefault(n => n != null);
                string method = infos.Select(i => i.methodName).LastOrDefault(n => n != null);
                if (declaring == null && method == null) return "";
                return " (" + (declaring ?? "?") + (method != null ? "." + method : "") + ")";
            }
            catch
            {
                return "";
            }
        }

        private static void Parts(Part part)
        {
            var s = ColonyCouncilMod.Settings;
            var defaults = new CouncilSettings();
            var off = new List<string>();
            var changed = new List<string>();
            int switches = 0;
            foreach (var f in typeof(CouncilSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object now = f.GetValue(s), was = f.GetValue(defaults);
                if (f.FieldType == typeof(bool))
                {
                    switches++;
                    if (!(bool)now) off.Add(f.Name);
                    if (!Equals(now, was)) changed.Add(f.Name + " " + ((bool)now ? "on" : "off"));
                }
                else if (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType.IsEnum)
                {
                    if (f.Name.StartsWith("slowButtons")) continue;
                    if (!Equals(now, was)) changed.Add(f.Name + " " + Format(now) + " (default " + Format(was) + ")");
                }
            }
            part.Add("Stakes: " + s.stakes + ". Life's ups and downs: " + s.lifePressure + ". Council button: " + s.buttonPosition + ".");
            // The council window: where it opens, what its pages remember, and the shortcuts the player bound.
            int bound = CouncilKeys.Pages.Count(p => CouncilKeys.KeyFor(p) != null);
            var memory = WeekMemory.Instance;
            part.Add("Council window opens on " + s.councilOpensOn + ", " + (s.councilPageState?.Count ?? 0) + " page settings remembered, " + bound + " page shortcuts bound"
                     + (memory != null ? "; " + memory.pinnedPawns.Count + " colonists and " + memory.pinnedWishes.Count + " wishes pinned, " + memory.dilemmas.Count + " answers kept" : "") + ".");
            var comp = CouncilGameComponent.Instance;
            if (comp != null)
                part.Add("This colony: era " + comp.era + " (works out as " + Avail.EffectiveTech.ToStringHuman() + "), "
                         + PawnsFinder.AllMaps_FreeColonists.Count() + " colonists on maps, " + comp.chronicle.Count + " chronicle entries, welcome letter "
                         + (comp.Welcomed ? "sent or not needed" : "still to come") + ".");
            part.Add(off.Count == 0 ? "All " + switches + " switches are on." : (switches - off.Count) + " of " + switches + " switches on. Off: " + string.Join(", ", off) + ".",
                off.Count == 0 ? UiKit.Good : Color.white);
            part.Add(changed.Count == 0 ? "Everything else is at its default." : "Changed from the default: " + string.Join(", ", changed) + ".", UiKit.Faint);
        }

        private static string Format(object v) => v is float f ? f.ToString("0.##") : v?.ToString() ?? "null";

        private static void Mods(Part part)
        {
            var found = new List<string>();
            var missing = new List<string>();
            foreach (var (name, ids, live) in Integrations)
            {
                var mod = ids.Select(People.ActiveMod).FirstOrDefault(m => m != null);
                if (mod == null)
                {
                    missing.Add(name);
                    continue;
                }
                found.Add(name + ModSupport.LiveNote(live) + " [" + (mod.PackageIdPlayerFacing ?? mod.PackageId) + "]");
            }
            part.Add(found.Count > 0 ? "Found: " + string.Join("; ", found) + "." : "None of them is active.", found.Count > 0 ? UiKit.Good : Color.white);
            if (missing.Count > 0) part.Add("Not active: " + string.Join(", ", missing) + ".", UiKit.Faint);

            // Everything in Rimfolk's defs written for a particular mod (a "mods" list on the def or one of its
            // extensions): Almanac pages, events, world events, moments, people.
            var counts = new Dictionary<string, int>();
            var fields = new Dictionary<Type, FieldInfo>();
            void Count(object o)
            {
                if (o == null) return;
                var t = o.GetType();
                if (!fields.TryGetValue(t, out var fi))
                {
                    fi = t.GetField("mods", BindingFlags.Public | BindingFlags.Instance);
                    if (fi != null && !typeof(IEnumerable<string>).IsAssignableFrom(fi.FieldType)) fi = null;
                    fields[t] = fi;
                }
                if (!(fi?.GetValue(o) is IEnumerable<string> ids)) return;
                foreach (var id in ids.Where(People.ModActive).Select(id => People.ActiveMod(id)?.Name ?? id).Distinct())
                    counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            }
            foreach (var def in ColonyCouncilMod.Pack?.AllDefs ?? Enumerable.Empty<Def>())
            {
                Count(def);
                if (def.modExtensions != null)
                    foreach (var ext in def.modExtensions)
                        Count(ext);
            }
            part.Add(counts.Count == 0 ? "Content for your other mods: none of the mods Rimfolk writes for is active."
                : "Content for your other mods (" + counts.Count + " mods): " + string.Join(", ", counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
                    .Select(kv => kv.Key + " " + kv.Value)) + ".", UiKit.Faint);
        }

        /// <summary>
        /// Whether a camp or a trade caravan could come to the home map right now, and if not, what stops it. A camp is
        /// tried for real (its people made and let go again, nothing put on the map), so what this says is what the
        /// morning's chance would meet in this weather. A caravan is asked the game's own question for each friendly
        /// people that trades, and the reason is found for those it says no to.
        /// </summary>
        private static void Visits(Part part)
        {
            var map = HappeningTracker.HomeMap();
            if (map == null)
            {
                part.Add("There's no home map to check.", UiKit.Faint);
                return;
            }
            int now = Find.TickManager.TicksGame;
            const int Day = GenDate.TicksPerDay;

            var tracker = CampTracker.Instance;
            string not = tracker == null ? "Rimfolk's camps aren't running in this game" : tracker.WhyNot(map);
            if (not != null) part.Add("Camps: none can come now: " + not + ".", tracker?.camp != null ? UiKit.Good : Color.white);
            else
            {
                var why = new List<string>();
                var setup = tracker.Prepare(map, why);
                if (setup != null)
                {
                    part.Add("Camps: one could come now (" + setup.kind.label + " of " + setup.faction.Name + "). Each morning at ten there's a "
                             + (CampTimer.DailyChance * 100f).ToString("0") + "% chance one does.", UiKit.Good);
                    CampTracker.Discard(setup);
                }
                else
                {
                    part.color = UiKit.Warn;
                    part.Add("Camps: the way is open, but none could set up just now: " + string.Join("; ", why.Distinct()) + ".", UiKit.Warn);
                }
            }

            var inc = IncidentDefOf.TraderCaravanArrival;
            part.Add(map.storyState.lastFireTicks.TryGetValue(inc, out int last)
                ? "Trade caravans: the last came on day " + last / Day + ", " + (now - last) / Day + " days ago."
                : "Trade caravans: none has come to this map yet.");
            if (ModsConfig.IsActive(RimMarketStall.PackageId))
                part.Add("RimMarket's merchants take a stall only from a trade caravan that has come to trade.", UiKit.Faint);
            var stop = map.gameConditionManager.ActiveConditions.FirstOrDefault(c => c.def.preventNeutralVisitors);
            if (stop != null)
            {
                part.Add("No caravan comes while " + stop.LabelCap + " lasts.", UiKit.Warn);
                return;
            }
            if (!(inc.Worker is IncidentWorker_TraderCaravanArrival worker)) return;
            var parms = StorytellerUtility.DefaultParmsNow(inc.category, map);
            var could = new List<string>();
            var stopped = new List<string>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f.IsPlayer || f.Hidden || f.defeated || f.temporary || f.HostileTo(Faction.OfPlayer) || f.def.caravanTraderKinds.NullOrEmpty()) continue;
                if (worker.FactionCanBeGroupSource(f, parms)) could.Add(f.Name);
                else stopped.Add(f.Name + " (" + CaravanStop(map, f) + ")");
            }
            if (could.Count > 0) part.Add("Could come now: " + string.Join(", ", could) + ".", UiKit.Good);
            else
            {
                part.color = UiKit.Warn;
                part.Add("No friendly people's caravan could come now, so the storyteller's caravans are lost while this lasts.", UiKit.Warn);
            }
            if (stopped.Count > 0) part.Add("Can't come now: " + string.Join("; ", stopped) + ".", could.Count > 0 ? UiKit.Faint : UiKit.Warn);
        }

        /// <summary>
        /// The slow stories of the newer rounds, and what stops each right now: the regulars the colony keeps up with and
        /// whether one could come back (Happenings/Regulars.cs), the herd's leader and its line (Animals/HerdStory.cs),
        /// the beast that stays, its candidate or the rest before the next, the shiny rumour (Animals/ResidentBeast.cs),
        /// and whether traveling trainers can come at all.
        /// </summary>
        private static void Stories(Part part)
        {
            var comp = CouncilGameComponent.Instance;
            var map = HappeningTracker.HomeMap();
            if (comp == null || map == null)
            {
                part.Add("There's no home map to check.", UiKit.Faint);
                return;
            }
            int now = Find.TickManager.TicksGame;
            const int Day = GenDate.TicksPerDay;

            // Regulars.
            var regulars = Regulars.All.Where(r => r.Alive).ToList();
            if (regulars.Count == 0)
                part.Add("Regulars: none remembered yet. A visit by the tinker, scholar, traveling doctor, schoolteacher, bonesetter, wandering cook, scribe, players or kindred spirit adds one (five at most).", UiKit.Faint);
            else
            {
                var bits = regulars.Select(r =>
                {
                    string when = r.DaysSince < RegularMath.ReturnAfterDays ? "due back in " + (RegularMath.ReturnAfterDays - r.DaysSince).ToString("0") + " days"
                        : r.DaysSince <= RegularMath.FadeDays ? "could come back now, forgotten in " + (RegularMath.FadeDays - r.DaysSince).ToString("0") + " days"
                        : "fading";
                    return r.pawn.LabelShort + " the " + r.kind + " (" + r.visits + (r.visits == 1 ? " visit" : " visits") + ", bond " + r.bond + ", " + when + ")";
                });
                part.Add("Regulars: " + regulars.Count + " remembered: " + string.Join("; ", bits) + ".");
                var pick = Regulars.Pick(map);
                part.Add(pick != null
                    ? "One could come back now (" + pick.pawn.LabelShort + "): the storyteller draws \"a familiar face at the gate\" from day 25, ten days apart at most."
                    : "None is due back just now.", pick != null ? UiKit.Good : UiKit.Faint);
            }
            if (!AnimalLife.On)
            {
                part.Add("The herd's leader and the beast that stays need Rimfolk's animal moments (the setting is off).", UiKit.Warn);
                return;
            }

            // The herd.
            var herd = comp.herd;
            var head = AnimalFacts.Herd(map);
            if (herd?.leader != null && !herd.leader.Dead)
            {
                string line = herd.generation > 1 ? ", the " + HerdStory.Ordinal(herd.generation) + " generation of " + herd.lineName + "'s line" : herd.lineName != null ? ", the first of " + herd.lineName + "'s line" : "";
                part.Add("Herd leader: " + herd.leader.LabelShort + line + ", since day " + (herd.leaderSince / Day + 1) + " (" + head.Count + " head). "
                         + (herd.lastSeasonTick >= 0 ? "Its last season has been told." : AnimalFacts.Old(herd.leader) ? "It is old: its last season is told at the day's check." : "Not old yet."), UiKit.Good);
            }
            else part.Add(head.Count >= HerdStoryMath.MinHerd
                ? "Herd leader: none yet; one is chosen at the day's check from the " + head.Count + " head."
                : "Herd leader: none; the herd has " + head.Count + " head on this map and needs " + HerdStoryMath.MinHerd + " for a leader.", UiKit.Faint);

            // The beast that stays.
            var rb = comp.beast;
            if (rb == null) return;
            if (!ResidentBeast.On) part.Add("The beast that stays needs the strangers setting on.", UiKit.Warn);
            else if (rb.Here)
            {
                float left = HerdStoryMath.FateAfterDays - rb.DaysNamed;
                part.Add("Beast that stays: " + rb.animal.LabelShort + " the " + AnimalFacts.KindLabel(rb.animal) + (rb.pokemon ? " (a Pokémon" + (rb.shiny ? ", shiny" : "") + ")" : "")
                         + ", named " + rb.DaysNamed.ToString("0") + " days ago" + (rb.namer != null ? " by " + rb.namer.LabelShort : "") + "; " + rb.StanceNow
                         + (rb.stolen > 0 ? ", at the stores " + rb.stolen + (rb.stolen == 1 ? " time" : " times") : "") + "; asked " + rb.asks + " of " + HerdStoryMath.MaxAsks
                         + " times; part of the land in " + Mathf.Max(0f, left).ToString("0") + " days.", UiKit.Good);
            }
            else
            {
                int fitting = map.mapPawns.AllPawnsSpawned.Count(a => ResidentBeast.Fits(a, map) && !rb.knownIds.Contains(a.thingIDNumber));
                float rest = rb.fateTick >= 0 ? HerdStoryMath.RestDays - (now - rb.fateTick) / (float)Day : 0f;
                if (rest > 0f) part.Add("Beast that stays: none; the last one's story closed, and another can be the one watched in " + rest.ToString("0") + " days.", UiKit.Faint);
                else if (rb.candidateId != 0 && rb.candidateDays > 0)
                    part.Add("Beast that stays: a wild animal has been seen " + rb.candidateDays + (rb.candidateDays == 1 ? " day" : " days") + " running; named at " + HerdStoryMath.ResidentAfterDays + ". "
                             + fitting + " of size on the land now.", UiKit.Good);
                else part.Add(fitting > 0
                    ? "Beast that stays: none yet; " + fitting + " wild " + (fitting == 1 ? "animal" : "animals") + " of size on the land now (a predator, or a grazer of a stag's size); one seen " + HerdStoryMath.ResidentAfterDays + " days running is named."
                    : "Beast that stays: none; no wild animal of size is on the land now.", UiKit.Faint);
            }
            if (rb.rumourKind != null && rb.rumourUntil > now)
                part.Add("Shiny rumour: a " + (DefDatabase<ThingDef>.GetNamedSilentFail(rb.rumourKind)?.label ?? rb.rumourKind) + ", " + ((rb.rumourUntil - now) / Day) + " days left; one chance in eight a day it shows itself.", UiKit.Good);

            // Traveling trainers.
            var trainers = DefDatabase<CampKindDef>.GetNamedSilentFail("Council_Camp_Trainers");
            if (trainers != null)
                part.Add(trainers.Possible ? "Traveling trainers can camp here (PokéWorld's Pokémon are in play)."
                    : PokeWorldLink.Active ? "Traveling trainers can't come: PokéWorld's spawn slider is at no Pokémon." : "Traveling trainers come only with PokéWorld.", UiKit.Faint);

            // The allies (World/Allies.cs), and the wardens' post as a trading stop (Camps/RoadPost.cs).
            var ally = Allies.Of();
            if (ally != null)
                part.Add("Allies: " + ally.Name + ". They send food when the stores fall under " + Allies.HungryDays.ToString("0") + " days (" + AdvisorWorker_Food.DaysOfFood(map).ToString("0.0")
                         + " days now, event Council_Evt_AllyFood), and their fighters come first when the colony is attacked.", UiKit.Good);
            else part.Add("No allies: the other settlers' year (Council_WS_RivalColony) ends in an alliance when the colony has been warm to them.", UiKit.Faint);
            var post = CampTracker.Instance?.holding;
            if (post != null && !post.kind.roadWord.NullOrEmpty())
                part.Add("The " + post.Label + " settled " + post.place + " are a trading stop: " + (post.LoadOnRoad ? post.LoadLine() : "no load on the road; food, hides or wool can be sent down with the carters from their page")
                         + ". Word from the road comes up with their visits.", UiKit.Good);
        }

        /// <summary>
        /// Why the game won't send this people's caravan now: a group of their enemies on the map (one the colony isn't at
        /// war with; the game's own rule), or weather they don't travel in.
        /// </summary>
        private static string CaravanStop(Map map, Faction f)
        {
            var home = map.ParentFaction ?? Faction.OfPlayer;
            foreach (var lord in map.lordManager.lords)
            {
                var o = lord.faction;
                if (o == null || o == f || o == home || !lord.AnyActivePawn) continue;
                var job = lord.LordJob;
                if ((job == null || job.CanBlockHostileVisitors) && !(job is LordJob_VoluntarilyJoinable) && o.HostileTo(f) && !o.HostileTo(home))
                    return "people of " + o.Name + " are on the map, and the two are at war";
            }
            var range = f.def.allowedArrivalTemperatureRange;
            if (!range.Includes(map.mapTemperature.OutdoorTemp) || !range.Includes(map.mapTemperature.SeasonalTemp))
                return "they don't travel in this weather";
            return "the game won't send them now";
        }

        private static void LogLines(Part part)
        {
            List<LogMessage> mine;
            try
            {
                mine = Log.Messages.Where(m => m?.text != null && m.text.Contains("[Rimfolk]")).ToList();
            }
            catch (InvalidOperationException)
            {
                part.Add("The log was busy; check again.", UiKit.Faint);
                return;
            }
            int errors = mine.Count(m => m.type == LogMessageType.Error);
            int warnings = mine.Count(m => m.type == LogMessageType.Warning);
            part.Add(errors + " errors, " + warnings + " warnings, " + (mine.Count - errors - warnings) + " other lines.",
                errors > 0 ? UiKit.Bad : warnings > 0 ? UiKit.Warn : UiKit.Good);
            if (errors > 0) part.color = UiKit.Bad;
            else if (warnings > 0) part.color = UiKit.Warn;
            foreach (var m in mine.Where(m => m.type != LogMessageType.Message).Reverse().Take(8))
            {
                string first = m.text.Split('\n')[0].Trim();
                if (first.Length > 220) first = first.Substring(0, 220) + "...";
                part.Add((m.repeats > 1 ? m.repeats + "x " : "") + first, m.type == LogMessageType.Error ? UiKit.Bad : UiKit.Warn);
            }
        }

        /// <summary>The report as plain text, for the clipboard and the log.</summary>
        public static string Plain(List<Part> parts)
        {
            var sb = new StringBuilder("[Rimfolk] check myself\n");
            foreach (var p in parts)
            {
                sb.Append("\n== ").Append(p.heading).Append('\n');
                foreach (var (text, _) in p.lines) sb.Append("- ").Append(text).Append('\n');
            }
            return sb.ToString();
        }
    }

    /// <summary>The "Rimfolk: check myself" window: the report, with buttons to copy it, put it in the log, or check again.</summary>
    public class Dialog_SelfCheck : Window
    {
        private List<SelfCheck.Part> parts;
        private string plain;
        private Vector2 scroll;
        private float height = 600f;

        public Dialog_SelfCheck()
        {
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = false;
            Build();
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(780f, UI.screenWidth - 40f), Mathf.Min(860f, UI.screenHeight - 40f));

        private void Build()
        {
            parts = SelfCheck.Report();
            plain = SelfCheck.Plain(parts);
        }

        public override void DoWindowContents(Rect inRect)
        {
            int bad = parts.Count(p => p.color == UiKit.Bad), warn = parts.Count(p => p.color == UiKit.Warn);
            string stat = bad > 0 ? bad + " need a look" : warn > 0 ? warn + " to note" : "all well";
            float top = UiKit.PageHeader(new Rect(inRect.x, inRect.y, inRect.width - 24f, UiKit.HeaderHeight), Icons.Council, "Rimfolk: check myself",
                "What Rimfolk found when it looked at itself. Take a screenshot, or copy it, for a bug report.", stat,
                bad > 0 ? UiKit.Bad : warn > 0 ? UiKit.Warn : UiKit.Good);

            const float buttonsH = 34f;
            var outRect = new Rect(inRect.x, inRect.y + top, inRect.width, inRect.height - top - buttonsH - 6f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            foreach (var p in parts)
            {
                y += UiKit.SectionTitle(0f, y, view.width, p.heading, null, p.color);
                foreach (var (text, color) in p.lines) y += UiKit.Paragraph(4f, y, view.width - 4f, text, color) + 3f;
                y += 8f;
            }
            height = Mathf.Max(y, 100f);
            Widgets.EndScrollView();

            float bw = 170f;
            var row = new Rect(inRect.x, inRect.yMax - buttonsH + 4f, bw, 30f);
            if (Widgets.ButtonText(row, "Copy to clipboard"))
            {
                GUIUtility.systemCopyBuffer = plain;
                Messages.Message("Copied Rimfolk's check to the clipboard.", MessageTypeDefOf.SilentInput, false);
            }
            row.x += bw + 8f;
            if (Widgets.ButtonText(row, "Write it to the log"))
            {
                Log.Message(plain);
                Messages.Message("Rimfolk's check is in the log.", MessageTypeDefOf.SilentInput, false);
            }
            row.x += bw + 8f;
            if (Widgets.ButtonText(row, "Check again")) Build();
        }
    }
}
