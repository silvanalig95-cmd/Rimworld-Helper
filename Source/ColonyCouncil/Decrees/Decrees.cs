using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public enum DecreeReaction
    {
        Hate,
        Dislike,
        Neutral,
        Like,
        Love
    }

    /// <summary>Moments when a colonist may petition the council for a decree.</summary>
    public enum DecreeMoment
    {
        FoodLow,
        RecentThreat,
        Grief,
        ColonyHappy,
        ColonyMiserable,
        Spoiled,
        Dirty,
        Prisoners,
        Autumn,
        /// <summary>The colony is worn down: average strain 40 or more (Heart/).</summary>
        Strained,
        /// <summary>Someone is ill (see <see cref="LifeChecks.Illness"/>).</summary>
        Illness,
        Winter,
        /// <summary>Someone joined the colony in the last ten days.</summary>
        Newcomer,
        /// <summary>Good times: food for twenty days or more and the colony in a good mood.</summary>
        Plenty,
        /// <summary>The world event the decree is for is running (its <see cref="DecreeDef.situationKinds"/>).</summary>
        Situation
    }

    /// <summary>Who feels how about a decree: by trait (and degree), by passion for a skill, or by being unable to fight.</summary>
    public class DecreeReactionRule
    {
        // trait and passion are def references resolved when the XML loads: vanilla traits and skills only. A mod's
        // trait or skill here breaks the decree when that mod is missing; match those by name (a string field) instead.
        public TraitDef trait;
        /// <summary>Trait degree to match; leave out to match any degree.</summary>
        public int degree = int.MinValue;
        public SkillDef passion;
        public bool nonViolent;
        /// <summary>Someone who lost a friend or family member lately.</summary>
        public bool grieving;
        /// <summary>
        /// Who they are, by name (see PeopleTags): "devout", "meme:NaturePrimacy", "trait:VTE_Workaholic". The way to
        /// name a mod's trait or a faith's meme: it simply never matches when that mod or DLC is missing.
        /// </summary>
        public string tags;
        /// <summary>Of the colony's own faith (Ideology), or of another one.</summary>
        public bool sameFaith;
        public bool otherFaith;
        public DecreeReaction reaction = DecreeReaction.Neutral;

        public bool Matches(Pawn p)
        {
            if (trait != null)
            {
                var traits = p.story?.traits;
                if (traits == null || !traits.HasTrait(trait)) return false;
                if (degree != int.MinValue && traits.DegreeOfTrait(trait) != degree) return false;
                return true;
            }
            if (passion != null)
            {
                var s = p.skills?.GetSkill(passion);
                return s != null && !s.TotallyDisabled && People.Passionate(s);
            }
            if (nonViolent) return p.WorkTagIsDisabled(WorkTags.Violent);
            if (grieving) return Flavor.IsGrieving(p);
            if (!tags.NullOrEmpty()) return PeopleTags.Matches(p, tags);
            if (sameFaith) return DecreeWork.Believer(p);
            if (otherFaith) return DecreeWork.OtherFaith(p);
            return false;
        }
    }

    /// <summary>
    /// A policy the council can put in force for a few days: trade the colony's good mood for work, food,
    /// training or learning (or the reverse). Each colonist reacts according to their nature.
    /// </summary>
    public class DecreeDef : Def
    {
        private static Dictionary<ThoughtDef, DecreeDef> byThought;

        /// <summary>The decree whose mood this is (the first, if two share one), or null. Looked up once per game session, not per mood.</summary>
        public static DecreeDef ForThought(ThoughtDef thought)
        {
            if (thought == null) return null;
            if (byThought == null)
            {
                var map = new Dictionary<ThoughtDef, DecreeDef>();
                foreach (var d in DefDatabase<DecreeDef>.AllDefsListForReading)
                    if (d.thought != null && !map.ContainsKey(d.thought))
                        map[d.thought] = d;
                byThought = map;
            }
            return byThought.TryGetValue(thought, out var found) ? found : null;
        }

        public float durationDays = 5f;
        public float cooldownDays = 7f;
        /// <summary>Applied to every free colonist while the decree holds.</summary>
        public HediffDef hediff;
        /// <summary>Situational thought; its stages are used as Hate, Dislike, Like, Love.</summary>
        public ThoughtDef thought;
        public DecreeReaction defaultReaction = DecreeReaction.Dislike;
        /// <summary>First match wins.</summary>
        public List<DecreeReactionRule> reactions = new List<DecreeReactionRule>();
        /// <summary>One line per effect, for the decrees tab.</summary>
        [MustTranslate] public List<string> effects = new List<string>();
        /// <summary>No new wishes while it holds.</summary>
        public bool suspendsWishes;
        /// <summary>Change to everyone's standards per day (austerity lowers them, festivals raise them).</summary>
        public float standardsPerDay;
        /// <summary>Hourly skill practice for everyone who can (drills, lessons).</summary>
        public List<SkillDef> practiceSkills;
        public float practiceXpPerHour;
        public bool practiceNeedsViolence;
        /// <summary>Only when someone in the colony is grieving (a day of mourning).</summary>
        public bool needsGrief;
        /// <summary>Fraction of grief memories' duration that passes each day the decree holds.</summary>
        public float griefFadePerDay;
        public ToneLines support = new ToneLines();
        public ToneLines oppose = new ToneLines();
        /// <summary>How enacting it shapes the colony's character.</summary>
        public WayShift ways;
        /// <summary>When someone who likes it may ask the council for it (any one is enough).</summary>
        public List<DecreeMoment> requestWhen;
        public int order;
        /// <summary>
        /// Feelings and strain (Heart/): strain a day for those who dislike it (half again for those who hate it), the
        /// factor on how fast strain drains while it holds, and a work speed factor carried by the heart's stat part
        /// rather than a hediff. A decree that needs the heart can't be declared while that feature is off.
        /// </summary>
        public float strainPerDay;
        public float strainRelief;
        public float workFactor;
        public bool needsHeart;
        /// <summary>
        /// The share of fuel that fires, torches and fueled heaters save while it holds (banked fires): handed back to them
        /// each hour, never to generators, stoves that only burn while cooking, or anything switched off.
        /// </summary>
        public float fuelSaving;
        /// <summary>
        /// What declaring it sets going on the map (a feast at noon, a song on the first evening), as an answer's effects
        /// (see PlayOut/): only with "Answers play out on the map" on. A is whoever asked for it, else someone at home.
        /// </summary>
        public List<DilemmaEffect> onEnact;

        // ---------------- When it can be declared at all (DecreeWork.cs) ----------------

        /// <summary>Only in these eras, named as the line tags name them (tribal, medieval, industrial, spacer, lowtech, !lowtech).</summary>
        public List<string> eras;
        /// <summary>Only once the colony has finished one of these research projects (defNames; a mod's that's missing never counts).</summary>
        public List<string> research;
        /// <summary>Only with Ideology, for a colony with a faith of its own.</summary>
        public bool needsIdeology;
        /// <summary>Only when the colony's faith holds one of these memes (defNames). Implies <see cref="needsIdeology"/>.</summary>
        public List<string> memes;
        /// <summary>Only while a world event of one of these kinds runs in the region (sickness, hunger, drought...).</summary>
        public List<string> situationKinds;
        /// <summary>...or while one of these moments holds (a quarantine also when someone at home is ill).</summary>
        public List<DecreeMoment> orWhen;
        /// <summary>Only when one of these mods or DLCs is active (package ids).</summary>
        public List<string> mods;
        /// <summary>Only for a colony of at least this many people.</summary>
        public int minColonists;

        // ---------------- What it does each day and hour (DecreeWork.cs) ----------------

        /// <summary>What it takes from the stores each morning (a tithe, alms, bread for the hungry).</summary>
        public DecreeTake take;
        /// <summary>Goodwill a day with <see cref="goodwillTo"/>, on a day something was given (or always, with nothing to take).</summary>
        public int goodwillPerDay;
        public DecreeGoodwillTo goodwillTo = DecreeGoodwillTo.Neighbour;
        /// <summary>Ideology: certainty a day for those of the colony's faith (0.02 = 2%).</summary>
        public float certaintyPerDay;
        /// <summary>A curfew: the night hours (22 to 5) are sleep for everyone but night owls and the sleepless, put back after.</summary>
        public bool nightSleep;
        /// <summary>Recreation an hour (a share of the bar) for everyone at work who doesn't mind it: a work song.</summary>
        public float joyWhileWorking;
        /// <summary>A quarantine: the factor on illnesses passing between people sharing a bedroom.</summary>
        public float contagionFactor = 1f;
        /// <summary>A quarantine: the factor on Rimfolk's visitor events while it holds.</summary>
        public float visitorFactor = 1f;
        /// <summary>A market day: word goes out, and a trade caravan of a friendly people comes that day.</summary>
        public bool callsTraders;
        /// <summary>Applied when it's declared, whether or not answers play out on the map (a gift, a thought).</summary>
        public List<DilemmaEffect> enactEffects;

        /// <summary>Never possible in this game as it stands (another era, a DLC or mod missing, another faith): not listed.</summary>
        public bool Hidden => DecreeWork.Hidden(this);

        public DecreeReaction ReactionOf(Pawn p)
        {
            // Other mods' traits, classes and memes, by name (ModPeopleDef): some come first, the rest only decide
            // for people this decree's own list says nothing about.
            var first = ModPeople.DecreeFirst(this, p);
            if (first != null) return first.Value;
            foreach (var r in reactions)
                if (r.Matches(p)) return r.reaction;
            return ModPeople.DecreeAfter(this, p) ?? defaultReaction;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (thought != null && (thought.stages == null || thought.stages.Count < 4)) yield return "decree thought needs 4 stages (hate, dislike, like, love)";
            if (take != null && take.what.NullOrEmpty()) yield return "take needs a what";
            if (requestWhen != null && requestWhen.Contains(DecreeMoment.Situation) && situationKinds.NullOrEmpty()) yield return "requestWhen Situation needs situationKinds";
        }
    }

    public class ActiveDecree : IExposable
    {
        public DecreeDef def;
        public int startTick;
        public int endTick;
        /// <summary>How many times it had been enacted lately when this one began (and once more if forced): resentment.</summary>
        public int fatigue;
        public bool forced;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
            Scribe_Values.Look(ref fatigue, "fatigue");
            Scribe_Values.Look(ref forced, "forced");
        }
    }

    /// <summary>A colonist asked the council for a decree.</summary>
    public class DecreeRequest : IExposable
    {
        public DecreeDef def;
        public Pawn pawn;
        public int tick;
        public string line;
        /// <summary>Asking for a decree in force to be lifted, rather than declared.</summary>
        public bool repeal;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref line, "line");
            Scribe_Values.Look(ref repeal, "repeal");
        }
    }

    public class DecreeTracker : IExposable
    {
        public const int MaxActive = 2;

        public List<ActiveDecree> active = new List<ActiveDecree>();
        private Dictionary<string, int> readyAt = new Dictionary<string, int>();
        public List<DecreeRequest> requests = new List<DecreeRequest>();
        private int lastRequestTick = -999999;
        public const int RequestDays = 5;
        /// <summary>A curfew's night hours: each colonist's (by ThingID) hours turned to sleep, "hour=TimeAssignmentDef;...".</summary>
        public Dictionary<string, string> nightHours = new Dictionary<string, string>();
        private List<string> tmpNightKeys, tmpNightValues;

        public DecreeRequest RequestFor(DecreeDef def) => requests.FirstOrDefault(r => r.def == def);

        public bool IsActive(DecreeDef def) => Get(def) != null;

        /// <summary>The decree in force, or null. Asked by the decree moods for every colonist, so a plain loop.</summary>
        public ActiveDecree Get(DecreeDef def)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i].def == def)
                    return active[i];
            return null;
        }

        public bool SuspendsWishes => active.Any(a => a.def.suspendsWishes);

        public int ReadyAt(DecreeDef def) => readyAt.TryGetValue(def.defName, out int t) ? t : 0;

        public bool OnCooldown(DecreeDef def) => ReadyAt(def) > Find.TickManager.TicksGame;

        /// <summary>When each decree was enacted (parallel lists), for decree fatigue.</summary>
        private List<string> useDefs = new List<string>();
        private List<int> useTicks = new List<int>();
        public const float FatigueDays = 30f;

        /// <summary>How many times it has been enacted in the last month: each time again, people resent it more.</summary>
        public int Fatigue(DecreeDef def)
        {
            int now = Find.TickManager.TicksGame, n = 0;
            for (int i = 0; i < useDefs.Count; i++)
                if (useDefs[i] == def.defName && now - useTicks[i] < FatigueDays * GenDate.TicksPerDay) n++;
            return n;
        }

        /// <summary>The chance that someone snaps when it's enacted (again, or forced through its cooldown).</summary>
        public float UnrestChance(DecreeDef def, bool force)
        {
            int fatigue = Fatigue(def) + (force ? 1 : 0);
            return fatigue <= 0 ? 0f : Mathf.Clamp(0.2f * fatigue + (force ? 0.15f : 0f), 0f, 0.8f);
        }

        /// <summary>Why it can't be enacted right now, or null. <paramref name="force"/>: pushing it through its cooldown.</summary>
        public string CannotEnact(DecreeDef def, bool force = false)
        {
            if (IsActive(def)) return null;
            int now = Find.TickManager.TicksGame;
            if (!ColonyCouncilMod.Settings.MoodEffects) return "CC_Decree_NoMood".Translate();
            if (def.needsHeart && !Heart.On) return "CC_Decree_NeedsHeart".Translate();
            if (!force && ReadyAt(def) > now) return "CC_Decree_Cooldown".Translate(TextUtil.DaysString(ReadyAt(def) - now));
            if (active.Count >= MaxActive) return "CC_Decree_TooMany".Translate(MaxActive);
            if (def.needsGrief && !PawnsFinder.AllMaps_FreeColonists.Any(Flavor.IsGrieving)) return "CC_Decree_NeedsGrief".Translate();
            return DecreeWork.Blocked(def);
        }

        public void Enact(DecreeDef def, CouncilGameComponent comp, bool force = false)
        {
            if (def == null || CannotEnact(def, force) != null || IsActive(def)) return;
            using var _cause = Causes.Push("CC_Cause_DecreeEnacted".Translate(def.label).Resolve());
            int now = Find.TickManager.TicksGame;
            force = force && OnCooldown(def);
            int fatigue = Fatigue(def) + (force ? 1 : 0);
            float unrest = UnrestChance(def, force);
            useDefs.Add(def.defName);
            useTicks.Add(now);
            while (useTicks.Count > 0 && now - useTicks[0] > FatigueDays * GenDate.TicksPerDay)
            {
                useDefs.RemoveAt(0);
                useTicks.RemoveAt(0);
            }
            active.Add(new ActiveDecree { def = def, startTick = now, endTick = now + Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay), fatigue = fatigue, forced = force });
            SyncHediffs();
            comp.Notify_DecreeEnacted(def);
            comp.character?.Shift(def.ways, "CC_Ways_Why_Decree".Translate(def.label).Resolve());
            if (fatigue > 0) Backlash(def, fatigue, force, unrest, comp);
            var req = RequestFor(def);
            var asker = req?.pawn != null && !req.pawn.Dead ? req.pawn : null;
            if (req != null)
            {
                requests.Remove(req);
                if (req.pawn != null && !req.pawn.Dead)
                {
                    comp.standings.Change(req.pawn, 5f, "CC_Log_DecreeHeeded".Translate(def.label));
                    CouncilGameComponent.GiveThought(req.pawn, CouncilDefOf.Council_DecreeHeeded);
                    // Lifting it early is going back on the council's word to them (Wishes/Kept.cs).
                    if (!req.repeal) comp.kept?.Notify_DecreeHeeded(req.pawn, def, Get(def)?.endTick ?? 0);
                }
            }
            if (!def.onEnact.NullOrEmpty() && PlayOuts.On)
            {
                var map = HappeningTracker.HomeMap();
                if (map != null)
                {
                    var a = asker != null && asker.Spawned && asker.Map == map ? asker : map.mapPawns.FreeColonistsSpawned.Where(PlayOuts.OurPerson).RandomElementWithFallback();
                    PlayOuts.Run(def.onEnact, new DilemmaContext { map = map, a = a, why = def.LabelCap }, "decree " + def.defName);
                }
            }
            DecreeWork.Enacted(this, def, asker);
        }

        /// <summary>
        /// The same decree again (or forced through its cooldown): those against it trust the council less and resent
        /// it more, and someone may snap.
        /// </summary>
        private void Backlash(DecreeDef def, int fatigue, bool forced, float unrest, CouncilGameComponent comp)
        {
            using var _cause = Causes.Push("CC_Cause_DecreeAgain".Translate(def.label).Resolve());
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p)).ToList();
            var against = pawns.Where(p => def.ReactionOf(p) <= DecreeReaction.Dislike).ToList();
            float scale = ColonyCouncilMod.Settings.ChosenPenaltyScale * Mathf.Min(fatigue, 3);
            foreach (var p in against)
            {
                bool hate = def.ReactionOf(p) == DecreeReaction.Hate;
                comp.standings.Change(p, -(hate ? 3f : 2f) * fatigue, "CC_Log_DecreeAgain".Translate(def.label));
                if (scale > 0f) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Decree_Again, null, scale);
            }
            if (!Rand.Chance(unrest)) return;
            var who = against.Where(p => def.ReactionOf(p) == DecreeReaction.Hate).Concat(against)
                .FirstOrDefault(p => p.Spawned && p.Awake() && !p.Downed && !p.InMentalState && !p.Drafted);
            if (who == null) return;
            var t = who.story?.traits;
            bool hot = !who.WorkTagIsDisabled(WorkTags.Violent) && t != null
                       && (t.HasTrait(TraitDefOf.Abrasive) || t.HasTrait(TraitDefOf.Bloodlust) || t.allTraits.Any(x => x.def.defName == "Brawler") || (t.allTraits.FirstOrDefault(x => x.def.defName == "Nerves")?.Degree ?? 0) < 0);
            var state = DefDatabase<MentalStateDef>.GetNamedSilentFail(hot ? "Tantrum" : "Wander_Sad") ?? DefDatabase<MentalStateDef>.GetNamedSilentFail("Wander_Sad");
            if (state == null || !(who.mindState?.mentalStateHandler?.TryStartMentalState(state, "CC_Decree_UnrestReason".Translate(def.label), true) ?? false)) return;
            string text = (forced ? "CC_Decree_UnrestForced" : "CC_Decree_Unrest").Translate(who.Named("PAWN"), def.label.Named("DECREE")).Resolve();
            Notify.Message(text, who, MessageTypeDefOf.NegativeEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, text, who));
            comp.journals.Write(who, "Council_Journal_DecreeUnrest", null, def.label.Named("DECREE"));
        }

        public void Repeal(DecreeDef def, CouncilGameComponent comp, bool early)
        {
            using var _cause = Causes.Push("CC_Cause_DecreeRepealed".Translate(def.label).Resolve());
            var a = Get(def);
            if (a == null) return;
            active.Remove(a);
            readyAt[def.defName] = Find.TickManager.TicksGame + Mathf.RoundToInt(def.cooldownDays * GenDate.TicksPerDay);
            SyncHediffs();
            DecreeWork.Ended(this, def);
            comp.Notify_DecreeEnded(def, early);
            comp.kept?.Notify_DecreeEnded(def, early);
            var req = RequestFor(def);
            if (req != null && req.repeal)
            {
                requests.Remove(req);
                if (early && req.pawn != null && !req.pawn.Dead)
                {
                    comp.standings.Change(req.pawn, 4f, "CC_Log_RepealHeeded".Translate(def.label));
                    CouncilGameComponent.GiveThought(req.pawn, CouncilDefOf.Council_DecreeHeeded);
                }
            }
        }

        public void Hourly(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var a in active.Where(x => now >= x.endTick).ToList()) Repeal(a.def, comp, false);
            SyncHediffs();
            DecreeWork.Hourly(this);
            foreach (var a in active)
            {
                if (a.def.fuelSaving > 0f) SaveFuel(a.def.fuelSaving);
                if (a.def.practiceSkills == null || a.def.practiceXpPerHour <= 0f) continue;
                foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
                {
                    if (p.skills == null || p.Downed || p.InBed() || p.InMentalState) continue;
                    if (a.def.practiceNeedsViolence && p.WorkTagIsDisabled(WorkTags.Violent)) continue;
                    foreach (var sk in a.def.practiceSkills)
                    {
                        var rec = p.skills.GetSkill(sk);
                        if (rec != null && !rec.TotallyDisabled) rec.Learn(a.def.practiceXpPerHour * (CouncilGameComponent.Instance?.offices.DrillFactor ?? 1f));
                    }
                }
            }
        }

        /// <summary>
        /// An hour of banked fires: every lit fire, torch and fueled heater at home gets back this share of an hour's
        /// fuel. Not generators, not stoves that only burn while cooking, not mortars, not anything switched off.
        /// </summary>
        private static void SaveFuel(float share)
        {
            try
            {
                foreach (var map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;
                    foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable))
                    {
                        if (t.Faction != Faction.OfPlayer || !(t is ThingWithComps tw)) continue;
                        var fuel = tw.GetComp<CompRefuelable>();
                        var props = fuel?.Props;
                        if (props == null || !fuel.HasFuel || props.fuelConsumptionRate <= 0f || props.consumeFuelOnlyWhenUsed || props.consumeFuelOnlyWhenPowered
                            || props.fuelIsMortarBarrel || props.destroyOnNoFuel) continue;
                        if (tw.GetComp<CompPowerPlant>() != null || !(tw.GetComp<CompFlickable>()?.SwitchIsOn ?? true)) continue;
                        float back = props.fuelConsumptionRate / 24f * share;
                        float room = props.fuelCapacity - fuel.Fuel;
                        float mult = props.FuelMultiplierCurrentDifficulty;
                        if (room <= 0f || mult <= 0.01f) continue;
                        fuel.Refuel(Mathf.Min(back, room) / mult);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("decree fuel saving", e);
            }
        }

        /// <summary>Once a day: old petitions lapse (a little sourly), and someone may make a new one.</summary>
        public void DailyRequests(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var r in requests.Where(r => r.def == null || r.pawn == null || r.pawn.Dead || now - r.tick > RequestDays * GenDate.TicksPerDay).ToList())
            {
                requests.Remove(r);
                if (r.def == null || r.pawn == null || r.pawn.Dead) continue;
                if (!r.repeal && !IsActive(r.def))
                    comp.standings.Change(r.pawn, -2f, "CC_Log_DecreeIgnored".Translate(r.def.label));
                else if (r.repeal && IsActive(r.def))
                    comp.standings.Change(r.pawn, -2f, "CC_Log_RepealIgnored".Translate(r.def.label));
            }
            if (!ColonyCouncilMod.Settings.decreeRequests || requests.Count >= 2 || now - lastRequestTick < GenDate.TicksPerDay * 3 || !Rand.Chance(0.35f)) return;
            var map = Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
            if (map == null) return;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && CouncilGameComponent.CanVoiceWishes(p)).ToList();
            if (pawns.Count == 0) return;
            // A decree half the colony resents: someone who hates it asks for it to be lifted.
            foreach (var a in active.InRandomOrder())
            {
                if (RequestFor(a.def) != null || now - a.startTick < GenDate.TicksPerDay) continue;
                int against = pawns.Count(p => a.def.ReactionOf(p) <= DecreeReaction.Dislike);
                if (against * 2 < pawns.Count) continue;
                var hater = pawns.Where(p => a.def.ReactionOf(p) == DecreeReaction.Hate).RandomElementWithFallback();
                if (hater == null) continue;
                string gripe = a.def.oppose.Pick(ToneUtility.ToneFor(hater), Flavor.TagsFor(hater));
                gripe = gripe.NullOrEmpty() ? null : gripe.Formatted(hater.Named("PAWN")).Resolve();
                requests.Add(new DecreeRequest { def = a.def, pawn = hater, tick = now, line = gripe, repeal = true });
                lastRequestTick = now;
                Messages.Message("CC_Decree_RepealRequested".Translate(hater.Named("PAWN"), a.def.label.Named("DECREE")).Resolve() + (gripe != null ? " \"" + TextUtil.ShortSpeech(gripe, 120) + "\"" : ""),
                    new LookTargets(hater), MessageTypeDefOf.NeutralEvent, false);
                CouncilGameComponent.Speak(hater, gripe, new Color(0.95f, 0.6f, 0.5f));
                return;
            }
            foreach (var def in DefDatabase<DecreeDef>.AllDefsListForReading.Where(d => !d.requestWhen.NullOrEmpty() && !d.Hidden).InRandomOrder())
            {
                if (IsActive(def) || RequestFor(def) != null || CannotEnact(def) != null) continue;
                if (!def.requestWhen.Any(m => MomentNow(m, map, pawns, def))) continue;
                var asker = pawns.Where(p => def.ReactionOf(p) == DecreeReaction.Love).RandomElementWithFallback()
                            ?? pawns.Where(p => def.ReactionOf(p) == DecreeReaction.Like).RandomElementWithFallback();
                if (asker == null) continue;
                string line = def.support.Pick(ToneUtility.ToneFor(asker), Flavor.TagsFor(asker));
                line = line.NullOrEmpty() ? null : line.Formatted(asker.Named("PAWN")).Resolve();
                requests.Add(new DecreeRequest { def = def, pawn = asker, tick = now, line = line });
                lastRequestTick = now;
                Messages.Message("CC_Decree_Requested".Translate(asker.Named("PAWN"), def.label.Named("DECREE")).Resolve() + (line != null ? " \"" + TextUtil.ShortSpeech(line, 120) + "\"" : ""),
                    new LookTargets(asker), MessageTypeDefOf.NeutralEvent, false);
                CouncilGameComponent.Speak(asker, line, new Color(0.9f, 0.85f, 0.6f));
                return;
            }
        }

        /// <summary>Whether a moment holds now at the home map (<paramref name="def"/>: the decree, for its world event).</summary>
        internal static bool MomentNow(DecreeMoment m, Map map, List<Pawn> pawns, DecreeDef def)
        {
            if (map == null || pawns == null || pawns.Count == 0) return false;
            switch (m)
            {
                case DecreeMoment.FoodLow:
                    return AdvisorWorker_Food.DaysOfFood(map) < 8f;
                case DecreeMoment.RecentThreat:
                    int last = map.storyState.LastThreatBigTick;
                    return last > 0 && Find.TickManager.TicksGame - last < GenDate.TicksPerDay * 3;
                case DecreeMoment.Grief:
                    return pawns.Any(Flavor.IsGrieving);
                case DecreeMoment.ColonyHappy:
                    return pawns.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f) > 0.75f;
                case DecreeMoment.ColonyMiserable:
                    return pawns.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f) < 0.4f;
                case DecreeMoment.Spoiled:
                    return pawns.Average(p => Standards.Of(p)) > 0.6f;
                case DecreeMoment.Dirty:
                    return WishWorker_Tidy.Filth(map) > 60;
                case DecreeMoment.Prisoners:
                    return map.mapPawns.PrisonersOfColonySpawnedCount > 0;
                case DecreeMoment.Autumn:
                    return GenLocalDate.Season(map) == Season.Fall;
                case DecreeMoment.Strained:
                    return Heart.On && Heart.ColonyStrain(pawns) >= 40f;
                case DecreeMoment.Illness:
                    return pawns.Any(p => LifeChecks.Illness(p) != null);
                case DecreeMoment.Winter:
                {
                    var season = GenLocalDate.Season(map);
                    return season == Season.Winter || season == Season.PermanentWinter;
                }
                case DecreeMoment.Newcomer:
                    return pawns.Any(p => p.records != null && p.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) < GenDate.TicksPerDay * 10);
                case DecreeMoment.Plenty:
                    return AdvisorWorker_Food.DaysOfFood(map) >= 20f && pawns.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f) > 0.6f;
                case DecreeMoment.Situation:
                    return def != null && DecreeWork.SituationRunning(def) != null;
                default:
                    return false;
            }
        }

        /// <summary>Once a day: grief fades faster under a day of mourning; tithes, alms and the faith's days do their day's work.</summary>
        public void Daily()
        {
            foreach (var a in active.ToList()) DecreeWork.Daily(this, a);
            foreach (var a in active)
            {
                if (a.def.griefFadePerDay <= 0f) continue;
                foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList())
                {
                    var mem = p.needs?.mood?.thoughts?.memories;
                    if (mem == null) continue;
                    foreach (var m in mem.Memories)
                    {
                        if (!Flavor.GriefThoughts.Contains(m.def.defName)) continue;
                        int duration = m.DurationTicks;
                        if (duration > 0) m.age = Mathf.Min(duration - 1, m.age + (int)(duration * a.def.griefFadePerDay));
                    }
                }
            }
        }

        /// <summary>Puts the decrees' hediffs on everyone they apply to, and takes off the ones that ended.</summary>
        public void SyncHediffs()
        {
            var wanted = new HashSet<HediffDef>(active.Where(a => a.def.hediff != null).Select(a => a.def.hediff));
            foreach (var def in DefDatabase<DecreeDef>.AllDefsListForReading)
            {
                var h = def.hediff;
                if (h == null) continue;
                bool on = wanted.Contains(h);
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
                {
                    var set = p.health?.hediffSet;
                    if (set == null) continue;
                    var existing = set.GetFirstHediffOfDef(h);
                    bool applies = on && !p.IsQuestLodger();
                    if (applies && existing == null) p.health.AddHediff(HediffMaker.MakeHediff(h, p));
                    else if (!applies && existing != null) p.health.RemoveHediff(existing);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref useDefs, "useDefs", LookMode.Value);
            Scribe_Collections.Look(ref useTicks, "useTicks", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (useDefs == null || useTicks == null || useDefs.Count != useTicks.Count))
            {
                useDefs = new List<string>();
                useTicks = new List<int>();
            }
            Scribe_Collections.Look(ref active, "active", LookMode.Deep);
            Scribe_Collections.Look(ref readyAt, "readyAt", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref requests, "requests", LookMode.Deep);
            Scribe_Values.Look(ref lastRequestTick, "lastRequestTick", -999999);
            Scribe_Collections.Look(ref nightHours, "nightHours", LookMode.Value, LookMode.Value, ref tmpNightKeys, ref tmpNightValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (nightHours == null) nightHours = new Dictionary<string, string>();
                if (active == null) active = new List<ActiveDecree>();
                active.RemoveAll(a => a?.def == null);
                if (readyAt == null) readyAt = new Dictionary<string, int>();
                if (requests == null) requests = new List<DecreeRequest>();
                requests.RemoveAll(r => r?.def == null || r.pawn == null);
            }
        }
    }

    /// <summary>Decree moods: the bad ones follow the stakes setting and the penalty slider.</summary>
    public class Thought_Decree : Thought_Situational, IExplainedThought
    {
        /// <summary>Which decree, and how long it has left.</summary>
        public string Why
        {
            get
            {
                var comp = CouncilGameComponent.Instance;
                var decree = DecreeDef.ForThought(def);
                var active = decree != null ? comp?.decrees.Get(decree) : null;
                string cause = active == null ? null
                    : "CC_Cause_Decree".Translate(decree.label, TextUtil.DaysString(Mathf.Max(0, active.endTick - Find.TickManager.TicksGame))).Resolve();
                if (active != null && active.fatigue > 0)
                    cause += " " + (active.forced ? "CC_Cause_DecreeForcedNote" : "CC_Cause_DecreeAgainNote").Translate((0.35f * active.fatigue).ToStringPercent()).Resolve();
                return cause;
            }
        }

        public override string Description => Causes.Append(base.Description, Why);

        private ActiveDecree Active
        {
            get
            {
                // Asked each time the mood is added up when it's a bad one: the decree is looked up once, not searched for.
                var decree = DecreeDef.ForThought(def);
                return decree != null ? CouncilGameComponent.Instance?.decrees.Get(decree) : null;
            }
        }

        public override float MoodOffset()
        {
            float m = base.MoodOffset();
            if (m >= 0f) return m;
            int fatigue = Active?.fatigue ?? 0;
            return m * ColonyCouncilMod.Settings.ChosenPenaltyScale * (1f + 0.35f * fatigue);
        }
    }

    /// <summary>How a colonist feels about the decrees in force: the thought's stages are hate, dislike, like, love.</summary>
    public class ThoughtWorker_Decree : ThoughtWorker
    {
        private DecreeDef decree;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            if (!s.MoodEffects || !p.IsFreeNonSlaveColonist || p.DevelopmentalStage.Baby()) return ThoughtState.Inactive;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return ThoughtState.Inactive;
            if (decree == null) decree = DecreeDef.ForThought(def);
            if (decree == null || !comp.decrees.IsActive(decree)) return ThoughtState.Inactive;
            switch (decree.ReactionOf(p))
            {
                case DecreeReaction.Hate: return ThoughtState.ActiveAtStage(0);
                case DecreeReaction.Dislike: return ThoughtState.ActiveAtStage(1);
                case DecreeReaction.Like: return ThoughtState.ActiveAtStage(2);
                case DecreeReaction.Love: return ThoughtState.ActiveAtStage(3);
                default: return ThoughtState.Inactive;
            }
        }
    }
}
