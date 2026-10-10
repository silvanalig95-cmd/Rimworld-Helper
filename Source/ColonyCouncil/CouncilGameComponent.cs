using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart of the mod: owns open wishes, the chronicle, the ambition tracker and personal touches,
    /// and runs everything once per in-game hour.
    /// </summary>
    public partial class CouncilGameComponent : GameComponent
    {
        public List<Wish> wishes = new List<Wish>();
        public List<ChronicleEntry> chronicle = new List<ChronicleEntry>();
        public AmbitionTracker ambitions = new AmbitionTracker();
        public PersonalTouchTracker touches = new PersonalTouchTracker();
        public StandingTracker standings = new StandingTracker();
        public int nextWishId = 1;

        private Dictionary<string, int> cooldowns = new Dictionary<string, int>();
        private List<string> tmpKeys;
        private List<int> tmpValues;

        public const int MaxChronicle = 300;
        private const int HourTicks = 2500;

        public static CouncilGameComponent Instance => Current.Game?.GetComponent<CouncilGameComponent>();

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        public CouncilGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) PruneCooldowns();
            Scribe_Collections.Look(ref wishes, "wishes", LookMode.Deep);
            Scribe_Collections.Look(ref chronicle, "chronicle", LookMode.Deep);
            Scribe_Deep.Look(ref ambitions, "ambitions");
            Scribe_Deep.Look(ref touches, "touches");
            Scribe_Deep.Look(ref standings, "standings");
            Scribe_Values.Look(ref nextWishId, "nextWishId", 1);
            Scribe_Collections.Look(ref cooldowns, "cooldowns", LookMode.Value, LookMode.Value, ref tmpKeys, ref tmpValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (wishes == null) wishes = new List<Wish>();
                if (chronicle == null) chronicle = new List<ChronicleEntry>();
                if (cooldowns == null) cooldowns = new Dictionary<string, int>();
                if (ambitions == null) ambitions = new AmbitionTracker();
                if (touches == null) touches = new PersonalTouchTracker();
                if (standings == null) standings = new StandingTracker();
                wishes.RemoveAll(w => w == null || w.def == null || w.pawn == null);
            }
        }

        public override void GameComponentTick()
        {
            int t = Find.TickManager.TicksGame;
            if (t % HourTicks != 157) return;
            Hourly();
        }

        private void Hourly()
        {
            Safe("wish upkeep", UpdateWishes);
            if (Settings.enableWishes) Safe("event wishes", CheckTriggers);
            if (Settings.enableWishes) Safe("wish generation", () => TryGenerateWish(false));
            if (Find.TickManager.TicksGame % GenDate.TicksPerDay < HourTicks) Safe("daily", Daily);
            if (Settings.enableAmbitions) Safe("ambitions", () => ambitions.HourlyTick(this));
            if (Settings.enablePersonalTouches) Safe("personal touches", () => touches.HourlyTick(this));
        }

        private static void Safe(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        // ================= Queries =================

        public IEnumerable<Wish> OpenWishes => wishes.Where(w => w.state == WishState.Open);

        public Wish OpenWishBy(Pawn p)
        {
            for (int i = 0; i < wishes.Count; i++)
            {
                var w = wishes[i];
                if (w.state == WishState.Open && w.pawn == p) return w;
            }
            return null;
        }

        public Wish OpenWishInvolving(Pawn p)
        {
            for (int i = 0; i < wishes.Count; i++)
            {
                var w = wishes[i];
                if (w.state == WishState.Open && w.Involves(p)) return w;
            }
            return null;
        }

        public void AddChronicle(ChronicleEntry entry)
        {
            chronicle.Add(entry);
            if (chronicle.Count > MaxChronicle) chronicle.RemoveRange(0, chronicle.Count - MaxChronicle);
        }

        // ================= Eligibility =================

        public static bool CanVoiceWishes(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Map == null || !p.Map.IsPlayerHome) return false;
            if (!p.IsFreeNonSlaveColonist || p.IsQuestLodger()) return false;
            if (p.needs?.mood == null) return false;
            if (p.DevelopmentalStage.Baby()) return false;
            if (p.InMentalState || p.Downed || p.Drafted) return false;
            if (p.Map.dangerWatcher.DangerRating == StoryDanger.High) return false;
            return true;
        }

        private static bool StillColonist(Pawn p) =>
            p != null && !p.Dead && !p.Destroyed && p.IsColonist && !p.IsSlaveOfColony;

        // ================= Cooldowns =================

        private static string CooldownKey(Pawn p, WishDef def) => p.ThingID + "|" + def.defName;

        private bool OnCooldown(Pawn p, WishDef def)
        {
            return cooldowns.TryGetValue(CooldownKey(p, def), out int until) && until > Find.TickManager.TicksGame;
        }

        private void SetCooldown(Pawn p, WishDef def, float factor = 1f)
        {
            if (p == null || def == null) return;
            cooldowns[CooldownKey(p, def)] = Find.TickManager.TicksGame + (int)(def.cooldownDays * factor * GenDate.TicksPerDay);
        }

        private void PruneCooldowns()
        {
            if (cooldowns == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (var k in cooldowns.Where(kv => kv.Value < now).Select(kv => kv.Key).ToList())
                cooldowns.Remove(k);
        }

        // ================= Generation =================

        private struct Option
        {
            public WishDef def;
            public Pawn requester;
            public Pawn beneficiary;
            public WishCandidate candidate;
            public Wish petitionTarget;
            public float weight;
        }

        /// <summary>Rolls for a new wish (or forces one). Returns the new wish, if any.</summary>
        public Wish TryGenerateWish(bool force, Pawn onlyFrom = null)
        {
            var s = Settings;
            // Let a new colony settle in before anyone starts asking for things.
            if (!force && Find.TickManager.TicksGame < GenDate.TicksPerDay * 3) return null;
            if (!force && !Rand.Chance(1f / (Mathf.Max(0.25f, s.daysBetweenWishes) * 24f))) return null;
            if (OpenWishes.Count() >= s.maxOpenWishes && !force) return null;

            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(CanVoiceWishes).ToList();
            if (pawns.Count == 0) return null;

            var options = new List<Option>();
            foreach (var requester in pawns)
            {
                if (onlyFrom != null && requester != onlyFrom) continue;
                if (OpenWishBy(requester) != null) continue;
                float personal = PersonalityFactor(requester) * TrustFactor(requester);

                foreach (var def in DefDatabase<WishDef>.AllDefsListForReading)
                {
                    if (OnCooldown(requester, def)) continue;
                    float defFactor = def.weight * personal * CategoryFactor(requester, def);
                    if (defFactor <= 0f) continue;
                    foreach (var c in SafeCandidates(def, requester))
                    {
                        AddOption(options, def, requester, requester, c, defFactor);
                    }
                }

                // Kind colonists speak up for others.
                if (s.wishesForOthers && requester.story?.traits != null && requester.story.traits.HasTrait(TraitDefOf.Kind))
                {
                    foreach (var other in pawns)
                    {
                        if (other == requester || OpenWishInvolving(other) != null) continue;
                        foreach (var def in DefDatabase<WishDef>.AllDefsListForReading)
                        {
                            if (!def.allowForOthers || OnCooldown(other, def)) continue;
                            foreach (var c in SafeCandidates(def, other))
                            {
                                AddOption(options, def, requester, other, c, def.weight * 0.5f);
                            }
                        }
                    }
                }
            }

            if (options.Count == 0) return null;
            var pick = options.RandomElementByWeight(o => o.weight);
            if (pick.petitionTarget != null)
            {
                CoSign(pick.petitionTarget, pick.requester);
                return pick.petitionTarget;
            }
            return CreateWish(pick.def, pick.requester, pick.beneficiary, pick.candidate);
        }

        private void AddOption(List<Option> options, WishDef def, Pawn requester, Pawn beneficiary, WishCandidate c, float factor)
        {
            if (c == null || c.weight <= 0f) return;
            var existing = wishes.FirstOrDefault(w => w.state == WishState.Open && w.def == def && w.subjectKey == (c.subjectKey ?? "")
                                                      && (def.allowPetition || w.Beneficiary == beneficiary));
            if (existing != null)
            {
                if (!Settings.petitions || !def.allowPetition || existing.Involves(requester) || beneficiary != requester) return;
                options.Add(new Option { def = def, requester = requester, petitionTarget = existing, weight = c.weight * factor * 0.6f });
                return;
            }
            options.Add(new Option { def = def, requester = requester, beneficiary = beneficiary, candidate = c, weight = c.weight * factor });
        }

        private static IEnumerable<WishCandidate> SafeCandidates(WishDef def, Pawn pawn)
        {
            List<WishCandidate> list;
            try
            {
                list = def.Worker.CandidatesFor(pawn)?.Where(c => c != null).ToList();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("candidates for " + def.defName, e);
                yield break;
            }
            if (list == null) yield break;
            foreach (var c in list) yield return c;
        }

        private static float PersonalityFactor(Pawn p)
        {
            float f = 1f;
            var traits = p.story?.traits;
            if (traits != null)
            {
                if (traits.HasTrait(TraitDefOf.Abrasive)) f *= 1.3f;
                if (traits.HasTrait(TraitDefOf.Greedy)) f *= 1.25f;
                if (traits.HasTrait(TraitDefOf.Jealous)) f *= 1.15f;
                if (traits.HasTrait(TraitDefOf.Kind)) f *= 0.85f;
            }
            float mood = p.needs?.mood?.CurLevelPercentage ?? 0.5f;
            if (mood < 0.35f) f *= 1.3f;
            else if (mood > 0.8f) f *= 0.8f;
            return f;
        }

        /// <summary>
        /// Bitter colonists mostly stop asking (they grumble instead); doubtful ones push harder; recently
        /// spoiled ones come back sooner.
        /// </summary>
        private float TrustFactor(Pawn p)
        {
            var st = standings.For(p, false);
            if (st == null) return 1f;
            float f = 1f;
            switch (st.Level)
            {
                case TrustLevel.Bitter: f = 0.5f; break;
                case TrustLevel.Doubtful: f = 1.25f; break;
                case TrustLevel.Devoted: f = 0.85f; break;
            }
            return f * (1f + 0.12f * st.RecentGrants());
        }

        private static float CategoryFactor(Pawn p, WishDef def)
        {
            var traits = p.story?.traits;
            if (traits == null) return 1f;
            float f = 1f;
            if (def.luxury && traits.HasTrait(TraitDefOf.Ascetic)) f *= 0.1f;
            if (def.luxury && traits.HasTrait(TraitDefOf.Greedy)) f *= 1.5f;
            int industry = traits.HasTrait(TraitDefOf.Industriousness) ? traits.DegreeOfTrait(TraitDefOf.Industriousness) : 0;
            if (def.category == WishCategory.Work) f *= industry > 0 ? 1.6f : industry < 0 ? 0.5f : 1f;
            if (def.category == WishCategory.Comfort && industry < 0) f *= 1.3f;
            return f;
        }

        // ================= Lifecycle =================

        public Wish CreateWish(WishDef def, Pawn requester, Pawn beneficiary, WishCandidate c, string trigger = null, Pawn envied = null)
        {
            var standing = standings.For(beneficiary ?? requester);
            // Trusting colonists are patient; bitter ones aren't.
            float patience = Mathf.Clamp(1f + standing.trust / 200f, 0.6f, 1.4f);
            var w = new Wish
            {
                id = nextWishId++,
                def = def,
                pawn = requester,
                beneficiary = beneficiary == requester ? null : beneficiary,
                other = c.other,
                thingDef = c.thingDef,
                subjectKey = c.subjectKey ?? "",
                subjectLabel = c.subjectLabel,
                targetValue = c.targetValue,
                baseline = c.baseline,
                createdTick = Find.TickManager.TicksGame,
                expiresTick = Find.TickManager.TicksGame + (int)(def.expiryDays * Settings.wishDurationFactor * patience * GenDate.TicksPerDay),
                tone = ToneUtility.ToneFor(requester),
                trigger = trigger,
                envied = envied,
                repeat = standing.asked.Contains(def.defName)
            };
            if (!standing.asked.Contains(def.defName)) standing.asked.Add(def.defName);
            w.reason = w.Format(c.reason);
            var tags = Flavor.TagsFor(requester, w);
            var lines = w.ForSomeoneElse && def.requestForOther.Any ? def.requestForOther : def.request;
            w.quote = w.Format(lines.Pick(w.tone, tags));
            if (envied != null)
            {
                string envyLine = Flavor.FromPool("Council_Pool_MeToo", requester, w, w.tone);
                if (!envyLine.NullOrEmpty()) w.quote = w.Format(envyLine) + " " + w.quote;
            }
            if (w.ForSomeoneElse && !def.requestForOther.Any)
                w.quote = "CC_ForOtherFallback".Translate(beneficiary.Named("FOR"), w.Label.Named("WISH")).Resolve();
            wishes.Add(w);

            AnnounceNewWish(w);
            return w;
        }

        private void AnnounceNewWish(Wish w)
        {
            var s = Settings;
            w.scene = Scene(w.trigger.NullOrEmpty() ? "Council_Pool_Scene" : "Council_Pool_Scene_Trigger", w.pawn, w);
            string title = w.ForSomeoneElse
                ? "CC_WishLetterTitleForOther".Translate(w.pawn.Named("PAWN"), w.Beneficiary.Named("FOR"), w.Label.Named("WISH")).Resolve()
                : "CC_WishLetterTitle".Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve();

            if (s.wishLetters)
            {
                Find.LetterStack.ReceiveLetter(title, DescribeForLetter(w), LetterDefOf.NeutralEvent, w.Worker.LookAt(w));
            }
            else
            {
                Messages.Message(title + ": \"" + TextUtil.ShortSpeech(w.quote, 120) + "\"", w.Worker.LookAt(w), MessageTypeDefOf.NeutralEvent, false);
            }

            Speak(w.pawn, w.quote, new Color(1f, 0.92f, 0.6f));
            AddChronicle(new ChronicleEntry(ChronicleKind.WishMade,
                "CC_Chronicle_WishMade".Translate(w.pawn.LabelShort, w.Label, w.reason ?? "").Resolve(), w.pawn));
        }

        public string DescribeForLetter(Wish w)
        {
            var sb = new StringBuilder();
            if (!w.scene.NullOrEmpty())
            {
                sb.AppendLine(w.scene);
                sb.AppendLine();
            }
            sb.AppendLine("\"" + w.quote + "\"");
            sb.AppendLine();
            if (!w.reason.NullOrEmpty()) sb.AppendLine("CC_Why".Translate() + ": " + w.reason);
            string when = w.Worker.FulfilledWhen(w);
            if (!when.NullOrEmpty()) sb.AppendLine("CC_FulfilledWhen".Translate() + ": " + when);
            sb.AppendLine("CC_Reward".Translate() + ": " + RewardText(w));
            sb.AppendLine("CC_Expires".Translate() + ": " + TextUtil.DaysString(w.TicksLeft));
            sb.AppendLine();
            sb.Append("CC_SeeCouncilTab".Translate());
            return sb.ToString();
        }

        public string RewardText(Wish w)
        {
            if (!Settings.MoodEffects) return "CC_RewardNone".Translate();
            var t = w.def.grantedThought;
            if (t == null || t.stages == null || t.stages.Count == 0) return "CC_RewardSmall".Translate();
            float mood = t.stages[0].baseMoodEffect * Settings.rewardFactor;
            string s = "CC_RewardMood".Translate(mood.ToString("+0;-0"), t.durationDays.ToString("0.#"));
            if (w.def.inspirationChance > 0f) s += ", " + "CC_RewardInspiration".Translate(w.def.inspirationChance.ToStringPercent());
            return s;
        }

        private void UpdateWishes()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = wishes.Count - 1; i >= 0; i--)
            {
                var w = wishes[i];
                if (w.state != WishState.Open)
                {
                    wishes.RemoveAt(i);
                    continue;
                }
                if (!StillColonist(w.pawn) || !StillColonist(w.Beneficiary))
                {
                    Resolve(w, WishState.Cancelled);
                    continue;
                }
                w.coSigners.RemoveAll(p => !StillColonist(p));

                // Away in a caravan or on another expedition: nothing to check until they're home.
                var home = w.Beneficiary.MapHeld;
                if (home == null || !home.IsPlayerHome)
                {
                    if (now >= w.expiresTick) Expire(w);
                    continue;
                }

                bool valid, fulfilled;
                try
                {
                    valid = w.Worker.StillValid(w);
                    fulfilled = valid && w.Worker.IsFulfilled(w);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("checking wish " + w.def.defName, e);
                    valid = false;
                    fulfilled = false;
                }

                if (!valid) Resolve(w, WishState.Cancelled);
                else if (fulfilled) Grant(w);
                else if (now >= w.expiresTick) Expire(w);
                else CheckStages(w);
            }
        }

        private void Resolve(Wish w, WishState state)
        {
            w.state = state;
            wishes.Remove(w);
        }

        private void CoSign(Wish w, Pawn p)
        {
            if (w.Involves(p)) return;
            w.coSigners.Add(p);
            w.expiresTick += GenDate.TicksPerDay * 2;
            SetCooldown(p, w.def);
            Tone tone = ToneUtility.ToneFor(p);
            string line = "CC_CoSignLine_" + tone;
            string speech = line.CanTranslate() ? line.Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve()
                : "CC_CoSignLine_Plain".Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve();
            Messages.Message("CC_PetitionJoined".Translate(p.Named("PAWN"), w.pawn.Named("OTHER"), w.Label.Named("WISH"), (w.coSigners.Count + 1).Named("COUNT")).Resolve(),
                new LookTargets(p), MessageTypeDefOf.NeutralEvent, false);
            Speak(p, speech, new Color(1f, 0.92f, 0.6f));
            AddChronicle(new ChronicleEntry(ChronicleKind.Petition,
                "CC_Chronicle_Petition".Translate(p.LabelShort, w.pawn.LabelShort, w.Label).Resolve(), p));
        }

        // ================= Effects =================

        public static void GiveThought(Pawn p, ThoughtDef def, Pawn otherPawn = null, float scale = -1f)
        {
            if (def == null || p?.needs?.mood?.thoughts?.memories == null) return;
            if (!ColonyCouncilMod.Settings.MoodEffects) return;
            if (def.IsSocial && (otherPawn == null || otherPawn == p)) return;
            var thought = ThoughtMaker.MakeThought(def) as Thought_Memory;
            if (thought == null) return;
            thought.moodPowerFactor = scale >= 0f ? scale : ColonyCouncilMod.Settings.rewardFactor;
            p.needs.mood.thoughts.memories.TryGainMemory(thought, otherPawn);
        }

        /// <summary>Floating speech text above a colonist (vanilla mote, no dependencies).</summary>
        public static void Speak(Pawn p, string line, Color color)
        {
            if (!ColonyCouncilMod.Settings.speechText || p == null || !p.Spawned || line.NullOrEmpty()) return;
            if (Find.CurrentMap != p.Map) return;
            MoteMaker.ThrowText(p.DrawPos + new Vector3(0f, 0f, 0.8f), p.Map, TextUtil.ShortSpeech(line), color, 5f);
        }
    }
}
