using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What happens around a wish: promises and refusals, reminders and last calls, colonists taking matters
    /// into their own hands, what granting or ignoring does to trust, mood, work and the people watching,
    /// and wishes set off by events.
    /// </summary>
    public partial class CouncilGameComponent
    {
        private static readonly Color SpeechWish = new Color(1f, 0.92f, 0.6f);
        private static readonly Color SpeechGood = new Color(0.6f, 1f, 0.6f);
        private static readonly Color SpeechSour = new Color(1f, 0.65f, 0.55f);
        private static readonly Color SpeechGrey = new Color(0.8f, 0.8f, 0.85f);

        /// <summary>A scene sentence from a pool, formatted for this wish. Null if nothing fits.</summary>
        public string Scene(string pool, Pawn p, Wish w)
        {
            string line = Flavor.FromPool(pool, p, w);
            return line.NullOrEmpty() ? null : (w != null ? w.Format(line) : line.Formatted(p.Named("PAWN")).Resolve());
        }

        private string PoolLine(string pool, Pawn p, Wish w, string fallbackKey = null)
        {
            string line = Flavor.FromPool(pool, p, w);
            if (line.NullOrEmpty() && fallbackKey != null) line = fallbackKey.Translate();
            return line.NullOrEmpty() ? null : w.Format(line);
        }

        private static float Gentleness(Pawn p)
        {
            var traits = p?.story?.traits;
            float f = 1f;
            if (traits != null)
            {
                if (traits.HasTrait(TraitDefOf.Kind)) f *= 0.5f;
                if (traits.HasTrait(TraitDefOf.Ascetic)) f *= 0.6f;
                if (traits.HasTrait(TraitDefOf.Abrasive)) f *= 1.3f;
                if (traits.HasTrait(TraitDefOf.Greedy) || traits.HasTrait(TraitDefOf.Jealous)) f *= 1.3f;
            }
            return f;
        }

        // ================= The player's answers =================

        /// <summary>"I'll see to it." More patience now; breaking it later costs a lot more than never promising.</summary>
        public void Promise(Wish w)
        {
            if (w == null || w.state != WishState.Open || w.promised) return;
            w.promised = true;
            w.expiresTick += Mathf.Max(0, w.TicksLeft) / 2 + GenDate.TicksPerDay * 2;
            foreach (var p in w.Supporters) standings.Change(p, 4f, "CC_Log_Promised".Translate(w.Label));
            if (w.ForSomeoneElse) standings.Change(w.Beneficiary, 4f, "CC_Log_Promised".Translate(w.Label));
            string line = PoolLine("Council_Pool_Promised", w.pawn, w, "CC_PromisedFallback");
            Messages.Message("CC_PromisedMessage".Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve() + (line != null ? " \"" + TextUtil.ShortSpeech(line, 120) + "\"" : ""),
                new LookTargets(w.pawn), MessageTypeDefOf.NeutralEvent, false);
            Speak(w.pawn, line, SpeechGood);
            AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_Promised".Translate(w.pawn.LabelShort, w.Label).Resolve(), w.pawn));
            string act = null;
            try
            {
                act = w.Worker.OnPromised(w);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("promise " + w.def.defName, e);
            }
            if (!act.NullOrEmpty())
                Messages.Message(w.Format(act.CanTranslate() ? act.Translate().ToString() : act), w.Worker.LookAt(w), MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>An honest no. Stings now, but it's over: no reminders, no slow souring.</summary>
        public void Refuse(Wish w)
        {
            if (w == null || w.state != WishState.Open) return;
            float pen = Settings.PenaltyScale;
            foreach (var p in w.Supporters.ToList())
            {
                float g = Gentleness(p);
                standings.Change(p, -6f * g, "CC_Log_Refused".Translate(w.Label));
                standings.For(p).refused++;
                if (pen > 0f) GiveThought(p, CouncilDefOf.Council_Refused, null, pen * g);
                SetCooldown(p, w.def, 3f);
            }
            string line = PoolLine("Council_Pool_Refused", w.pawn, w, "CC_RefusedFallback");
            Messages.Message("CC_RefusedMessage".Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve() + (line != null ? " \"" + TextUtil.ShortSpeech(line, 120) + "\"" : ""),
                new LookTargets(w.pawn), MessageTypeDefOf.NeutralEvent, false);
            Speak(w.pawn, line, SpeechSour);
            AddChronicle(new ChronicleEntry(ChronicleKind.WishDismissed, "CC_Chronicle_Refused".Translate(w.pawn.LabelShort, w.Label).Resolve(), w.pawn));
            Safe("objection", () => ResolveObjection(w, false));
            w.state = WishState.Refused;
            wishes.Remove(w);
        }

        /// <summary>The player said "not now": set aside quietly, with a small dent in trust when downsides are on.</summary>
        public void Dismiss(Wish w)
        {
            if (w == null || w.state != WishState.Open) return;
            if (Settings.Negatives)
            {
                foreach (var p in w.Supporters) standings.Change(p, -2f * Gentleness(p), "CC_Log_SetAside".Translate(w.Label));
            }
            AddChronicle(new ChronicleEntry(ChronicleKind.WishDismissed,
                "CC_Chronicle_WishDismissed".Translate(w.pawn.LabelShort, w.Label).Resolve(), w.pawn));
            foreach (var p in w.Supporters) SetCooldown(p, w.def, 2f);
            w.state = WishState.Dismissed;
            wishes.Remove(w);
        }

        // ================= Objections =================

        /// <summary>
        /// Not everyone agrees. When a wish is voiced, an ascetic may object to luxury, a rival to anything their
        /// rival wants, the jealous to things that stir envy. Granting it then irritates the objector; refusing
        /// or letting it lapse pleases them.
        /// </summary>
        private void MaybeObject(Wish w)
        {
            var map = w.pawn.MapHeld;
            if (map == null || !Rand.Chance(w.def.objectionChance)) return;
            var objectors = w.def.objectorTraits;
            Pawn best = null;
            float bestScore = 0f;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (w.Involves(p) || !CanVoiceWishes(p) || p.relations == null) continue;
                var traits = p.story?.traits;
                float score = 0f;
                if (w.def.luxury && traits != null && traits.HasTrait(TraitDefOf.Ascetic)) score += 3f;
                int opinion = p.relations.OpinionOf(w.pawn);
                if (opinion <= -20) score += 2f + (-opinion - 20) / 20f;
                if (w.def.envy >= 0.5f && traits != null && (traits.HasTrait(TraitDefOf.Jealous) || traits.HasTrait(TraitDefOf.Greedy))) score += 1.5f;
                if (w.def.category == WishCategory.Comfort && TraitDegree(p, TraitDefOf.Industriousness) > 0) score += 0.7f;
                if (traits != null && objectors != null)
                    foreach (var t in objectors)
                        if (traits.HasTrait(t)) score += 3f;
                if (traits != null && traits.HasTrait(TraitDefOf.Kind) && (objectors == null || !objectors.Contains(TraitDefOf.Kind))) score -= 2f;
                if (w.def.objectByOpinionOfOther != 0 && w.other != null && w.other != p)
                {
                    int toOther = p.relations.OpinionOf(w.other);
                    if (w.def.objectByOpinionOfOther > 0 ? toOther >= 20 : toOther <= -20) score += 2f;
                }
                if (score > bestScore)
                {
                    best = p;
                    bestScore = score;
                }
            }
            if (best == null || bestScore < 1.5f) return;
            w.objector = best;
            string line = w.def.objectionPool != null ? Flavor.FromPool(w.def.objectionPool.defName, best, w, ToneUtility.ToneFor(best)) : null;
            if (line.NullOrEmpty()) line = Flavor.FromPool("Council_Pool_Objection", best, w, ToneUtility.ToneFor(best));
            line = line?.Formatted(best.Named("PAWN"), w.pawn.Named("OTHER"), w.Label.Named("WISH"), (w.other ?? w.Beneficiary).Named("ABOUT")).Resolve();
            if (line.NullOrEmpty()) return;
            Messages.Message("CC_ObjectionMessage".Translate(best.Named("PAWN"), w.pawn.Named("OTHER"), w.Label.Named("WISH")).Resolve() + " \"" + TextUtil.ShortSpeech(line, 120) + "\"",
                new LookTargets(best), MessageTypeDefOf.SilentInput, false);
            Speak(best, line, SpeechSour);
            AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_Objection".Translate(best.LabelShort, w.pawn.LabelShort, w.Label).Resolve(), best));
        }

        private void ResolveObjection(Wish w, bool granted)
        {
            var o = w.objector;
            if (o == null || o.Dead || !o.IsColonist) return;
            if (granted)
            {
                standings.Change(o, -3f * Gentleness(o), "CC_Log_ObjectionOverruled".Translate(w.Label));
                if (Settings.Negatives) GiveThought(o, CouncilDefOf.Council_ObjectionIgnored, w.pawn, Settings.PenaltyScale);
            }
            else
            {
                standings.Change(o, 2f, "CC_Log_ObjectionHeard".Translate(w.Label));
                GiveThought(o, CouncilDefOf.Council_ObjectionHeard);
            }
        }

        // ================= Reminders and last calls =================

        private void CheckStages(Wish w)
        {
            if (!Settings.reminders || !w.pawn.Spawned) return;
            float e = w.Elapsed;
            if (w.stage == 0 && e >= 0.5f)
            {
                w.stage = 1;
                Remind(w);
            }
            else if (w.stage == 1 && e >= 0.82f)
            {
                w.stage = 2;
                LastCall(w);
            }
        }

        private void Remind(Wish w)
        {
            var tags = Flavor.TagsFor(w.pawn, w);
            string line = w.def.reminder.Any ? w.def.reminder.Pick(ToneUtility.ToneFor(w.pawn), tags) : null;
            line = line != null ? w.Format(line) : PoolLine("Council_Pool_Reminder", w.pawn, w);
            if (line.NullOrEmpty()) return;
            Messages.Message("CC_ReminderMessage".Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve() + " \"" + TextUtil.ShortSpeech(line, 120) + "\"",
                new LookTargets(w.pawn), MessageTypeDefOf.SilentInput, false);
            Speak(w.pawn, line, SpeechWish);
        }

        private void LastCall(Wish w)
        {
            if (Settings.selfHelp && WouldDoItThemselves(w.pawn))
            {
                string what = null;
                try
                {
                    what = w.Worker.TrySelfHelp(w);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("self-help " + w.def.defName, e);
                }
                if (!what.NullOrEmpty())
                {
                    w.selfHelped = true;
                    var st = standings.For(w.pawn);
                    st.selfHelped++;
                    bool proud = TraitDegree(w.pawn, TraitDefOf.Industriousness) > 0;
                    standings.Change(w.pawn, proud ? -1f : -4f, "CC_Log_SelfHelp".Translate(w.Label));
                    if (Settings.Negatives && !proud) GiveThought(w.pawn, CouncilDefOf.Council_TookMatters, null, Settings.PenaltyScale);
                    string line = PoolLine("Council_Pool_SelfHelp", w.pawn, w);
                    Find.LetterStack.ReceiveLetter("CC_SelfHelpTitle".Translate(w.pawn.Named("PAWN")).Resolve(),
                        w.Format(what.CanTranslate() ? what.Translate().ToString() : what) + (line != null ? "\n\n\"" + line + "\"" : ""), LetterDefOf.NeutralEvent, w.Worker.LookAt(w));
                    Speak(w.pawn, line, SpeechWish);
                    AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_SelfHelp".Translate(w.pawn.LabelShort, w.Label).Resolve(), w.pawn));
                    return;
                }
            }
            string call = PoolLine("Council_Pool_LastCall", w.pawn, w);
            if (call.NullOrEmpty()) return;
            Messages.Message("CC_LastCallMessage".Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH"), TextUtil.DaysString(w.TicksLeft).Named("TIME")).Resolve()
                             + " \"" + TextUtil.ShortSpeech(call, 140) + "\"",
                new LookTargets(w.pawn), MessageTypeDefOf.CautionInput, false);
            Speak(w.pawn, call, SpeechSour);
        }

        private static int TraitDegree(Pawn p, TraitDef t) =>
            t != null && p?.story?.traits != null && p.story.traits.HasTrait(t) ? p.story.traits.DegreeOfTrait(t) : 0;

        /// <summary>Hard workers and handy people fix things themselves; the lazy wait for someone else.</summary>
        private static bool WouldDoItThemselves(Pawn p)
        {
            int industry = TraitDegree(p, TraitDefOf.Industriousness);
            if (industry < 0) return false;
            float chance = industry > 0 ? 0.8f : 0.4f;
            var cons = p.skills?.GetSkill(SkillDefOf.Construction);
            if (cons != null && !cons.TotallyDisabled && cons.Level >= 6) chance += 0.15f;
            return Rand.Chance(chance);
        }

        // ================= Granting =================

        public void Grant(Wish w)
        {
            var s = Settings;
            var who = w.Beneficiary;
            GiveThought(who, w.def.grantedThought);
            foreach (var co in w.coSigners)
                if (co != who) GiveThought(co, w.def.grantedThought);

            if (w.ForSomeoneElse)
            {
                GiveThought(w.pawn, CouncilDefOf.Council_HelpedSomeone);
                GiveThought(who, CouncilDefOf.Council_SpokeUpForMe, w.pawn);
            }

            // Trust: the one who got it most, those who backed it too, and a little more if it was promised.
            var st = standings.For(who);
            st.granted++;
            st.recentGrants.Add(Find.TickManager.TicksGame);
            standings.Change(who, w.promised ? 18f : 12f, "CC_Log_Granted".Translate(w.Label));
            foreach (var p in w.Supporters)
                if (p != who) standings.Change(p, 6f, "CC_Log_Granted".Translate(w.Label));
            if (w.promised)
            {
                st.promisesKept++;
                GiveThought(who, CouncilDefOf.Council_PromiseKept);
            }
            if (w.coSigners.Count > 0) GiveThought(who, CouncilDefOf.Council_StoodTogether, w.coSigners.First());
            foreach (var co in w.coSigners) GiveThought(co, CouncilDefOf.Council_StoodTogether, w.pawn);

            // Fired up: works a bit faster for a few days.
            if (s.MoodEffects && Rand.Chance(0.4f)) AddHediff(who, CouncilDefOf.Council_Motivated, w.promised ? 0.8f : 0.5f);
            RunEffects(w, w.def.grantEffects);

            if (s.MoodEffects && w.def.inspirationChance > 0f && Rand.Chance(w.def.inspirationChance) && who.mindState?.inspirationHandler != null && !who.mindState.inspirationHandler.Inspired)
            {
                var insp = w.Worker.InspirationFor(w) ?? who.mindState.inspirationHandler.GetRandomAvailableInspirationDef();
                if (insp != null)
                    who.mindState.inspirationHandler.TryStartInspiration(insp, "CC_InspirationReason".Translate(who.Named("PAWN"), w.Label.Named("WISH")).Resolve());
            }

            Tone tone = ToneUtility.ToneFor(who);
            var tags = Flavor.TagsFor(who, w);
            string thanks = w.Format(w.def.thanks.Pick(tone, tags));
            if (thanks.NullOrEmpty()) thanks = "CC_ThanksFallback".Translate();
            string title = "CC_WishGrantedTitle".Translate(who.Named("PAWN"), w.Label.Named("WISH")).Resolve();
            string scene = Scene("Council_Pool_Scene_Thanks", who, w);
            string body = (scene != null ? scene + "\n\n" : "") + "\"" + thanks + "\"";
            if (w.coSigners.Count > 0)
                body += "\n\n" + "CC_PetitionGranted".Translate(TextUtil.JoinAnd(w.coSigners.Select(p => p.LabelShort).ToList())).Resolve();
            if (w.promised) body += "\n\n" + "CC_PromiseKeptNote".Translate(who.Named("PAWN")).Resolve();

            if (s.grantedLetters)
                Find.LetterStack.ReceiveLetter(title, body, LetterDefOf.PositiveEvent, new LookTargets(who));
            else
                Messages.Message(title + ": \"" + TextUtil.ShortSpeech(thanks, 120) + "\"", new LookTargets(who), MessageTypeDefOf.PositiveEvent, false);

            Speak(who, thanks, SpeechGood);
            AddChronicle(new ChronicleEntry(ChronicleKind.WishGranted,
                "CC_Chronicle_WishGranted".Translate(who.LabelShort, w.Label).Resolve(), who));

            foreach (var p in w.Supporters) SetCooldown(p, w.def);
            SetCooldown(who, w.def);
            w.state = WishState.Granted;
            ambitions.Notify_WishGranted(w);
            Safe("ripples", () => GrantRipples(w));
            Safe("objection", () => ResolveObjection(w, true));
        }

        /// <summary>
        /// The others notice. Friends are happy for them. The jealous, the greedy, rivals, and above all those
        /// who have been waiting longer, feel passed over. Some decide they want the same thing.
        /// </summary>
        private void GrantRipples(Wish w)
        {
            var who = w.Beneficiary;
            var map = who.MapHeld;
            if (map == null) return;
            var s = Settings;
            float pen = s.PenaltyScale;
            Pawn envious = null;
            int passedOver = 0;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p == who || w.Involves(p) || p.relations == null || p.needs?.mood == null) continue;
                int opinion = p.relations.OpinionOf(who);
                var own = OpenWishBy(p);
                bool waitedLonger = own != null && own.createdTick < w.createdTick;
                bool jealous = p.story?.traits?.HasTrait(TraitDefOf.Jealous) ?? false;
                bool greedy = p.story?.traits?.HasTrait(TraitDefOf.Greedy) ?? false;

                if (s.jealousy && pen > 0f)
                {
                    float chance = w.def.envy * 0.3f;
                    if (jealous) chance *= 2.2f;
                    if (greedy) chance *= 1.5f;
                    if (waitedLonger) chance *= 2f;
                    if (opinion <= -15) chance *= 1.8f;
                    else if (opinion >= 30) chance *= 0.25f;
                    if (Rand.Chance(Mathf.Clamp01(chance * pen)))
                    {
                        GiveThought(p, CouncilDefOf.Council_Favoritism, who, pen);
                        if (waitedLonger)
                        {
                            standings.Change(p, -4f * Gentleness(p), "CC_Log_PassedOver".Translate(who.LabelShort));
                            passedOver++;
                            string grumble = Flavor.FromPool("Council_Pool_PassedOver", p, own);
                            if (grumble != null)
                                Speak(p, grumble.Formatted(p.Named("PAWN"), who.Named("OTHER"), w.Label.Named("WISH"), own.Label.Named("MINE")).Resolve(), SpeechSour);
                        }
                        if ((jealous || greedy) && envious == null && w.def.meToo && own == null) envious = p;
                        continue;
                    }
                }
                if (opinion >= 30 && Rand.Chance(0.5f)) GiveThought(p, CouncilDefOf.Council_HappyForThem, who);
                // Word gets around: the council listens.
                if (own != null) standings.Change(p, 1f, null);
            }

            if (passedOver > 0)
                AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_PassedOver".Translate(passedOver, who.LabelShort, w.Label).Resolve(), who));

            if (envious != null && OpenWishes.Count() < s.maxOpenWishes && !OnCooldown(envious, w.def) && CanVoiceWishes(envious))
            {
                var c = SafeCandidates(w.def, envious).FirstOrDefault();
                if (c != null) CreateWish(w.def, envious, envious, c, "envy", who);
            }
        }

        // ================= Letting a wish go =================

        private void Expire(Wish w)
        {
            var s = Settings;
            float pen = s.PenaltyScale;
            foreach (var p in w.Supporters.ToList())
            {
                var st = standings.For(p);
                float ent = st.Entitlement;
                float g = Gentleness(p);
                st.ignored++;
                standings.Change(p, -9f * ent * g, "CC_Log_Ignored".Translate(w.Label));
                if (w.promised)
                {
                    st.promisesBroken++;
                    standings.Change(p, -18f * g, "CC_Log_BrokenPromise".Translate(w.Label));
                }
                if (pen > 0f)
                {
                    GiveThought(p, w.promised ? CouncilDefOf.Council_BrokenPromise : CouncilDefOf.Council_WishIgnored, null, pen * ent * g);
                    if (Rand.Chance(Mathf.Clamp01(0.35f * pen * ent))) AddHediff(p, CouncilDefOf.Council_Disgruntled, s.Harsh ? 0.8f : 0.3f);
                }
            }
            // Friends take it personally, a little.
            if (pen > 0f && w.pawn.MapHeld != null)
            {
                foreach (var f in w.pawn.MapHeld.mapPawns.FreeColonistsSpawned)
                {
                    if (f == w.pawn || w.Involves(f) || f.relations == null) continue;
                    if (f.relations.OpinionOf(w.pawn) >= 40) standings.Change(f, -2f, "CC_Log_FriendIgnored".Translate(w.pawn.LabelShort));
                }
            }
            RunEffects(w, w.def.ignoreEffects);

            Tone tone = ToneUtility.ToneFor(w.pawn);
            var tags = Flavor.TagsFor(w.pawn, w);
            string line = w.Format(w.def.expired.Pick(tone, tags));
            if (w.promised)
            {
                string broken = PoolLine("Council_Pool_BrokenPromise", w.pawn, w);
                if (!broken.NullOrEmpty()) line = broken;
            }
            if (line.NullOrEmpty()) line = "CC_ExpiredFallback".Translate();
            string titleKey = w.promised ? "CC_PromiseBrokenTitle" : "CC_WishExpiredTitle";
            Messages.Message(titleKey.Translate(w.pawn.Named("PAWN"), w.Label.Named("WISH")).Resolve() + ": \"" + TextUtil.ShortSpeech(line, 120) + "\"",
                new LookTargets(w.pawn), w.promised && pen > 0f ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent, false);
            if (w.pawn.Spawned) Speak(w.pawn, line, w.promised ? SpeechSour : SpeechGrey);
            AddChronicle(new ChronicleEntry(ChronicleKind.WishExpired,
                (w.promised ? "CC_Chronicle_PromiseBroken" : "CC_Chronicle_WishExpired").Translate(w.pawn.LabelShort, w.Label).Resolve(), w.pawn));
            foreach (var p in w.Supporters) SetCooldown(p, w.def, 1.5f);
            w.state = WishState.Expired;
            Safe("objection", () => ResolveObjection(w, false));

            if (s.Harsh) MaybeSulk(w.pawn, w);
        }

        /// <summary>Demanding stakes: a bitter, unhappy colonist may storm off to sulk.</summary>
        private void MaybeSulk(Pawn p, Wish w)
        {
            if (p == null || !p.Spawned || p.InMentalState || p.Downed) return;
            var st = standings.For(p);
            if (st.Level > TrustLevel.Doubtful) return;
            if ((p.needs?.mood?.CurLevelPercentage ?? 1f) > 0.3f) return;
            if (Find.TickManager.TicksGame - st.lastSulkTick < GenDate.TicksPerDay * 6) return;
            if (!Rand.Chance(st.Level == TrustLevel.Bitter ? 0.45f : 0.2f)) return;
            var state = (p.story?.traits?.HasTrait(TraitDefOf.Abrasive) ?? false)
                ? DefDatabase<MentalStateDef>.GetNamedSilentFail("InsultingSpree") ?? MentalStateDefOf.Wander_Sad
                : MentalStateDefOf.Wander_Sad;
            string reason = w != null ? "CC_MentalReason".Translate(w.Label.Named("WISH")).Resolve() : "CC_MentalReasonGeneral".Translate().Resolve();
            if (p.mindState?.mentalStateHandler?.TryStartMentalState(state, reason, false, false, true) == true)
            {
                st.lastSulkTick = Find.TickManager.TicksGame;
                AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_Sulk".Translate(p.LabelShort).Resolve(), p));
            }
        }

        // ================= Effects =================

        private void RunEffects(Wish w, List<WishEffect> effects)
        {
            if (effects == null) return;
            foreach (var e in effects) e?.Run(w);
        }

        public static void AddHediff(Pawn p, HediffDef def, float severity)
        {
            if (p?.health == null || def == null || p.Dead) return;
            var existing = p.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                existing.Severity = Mathf.Max(existing.Severity, severity);
                var comp = existing.TryGetComp<HediffComp_Disappears>();
                if (comp != null) comp.ticksToDisappear = Mathf.Max(comp.ticksToDisappear, comp.Props.disappearsAfterTicks.RandomInRange);
                return;
            }
            var h = HediffMaker.MakeHediff(def, p);
            h.Severity = severity;
            p.health.AddHediff(h);
        }

        // ================= Wishes set off by events =================

        private readonly List<Thought> tmpThoughts = new List<Thought>();

        /// <summary>
        /// Every hour: did something just happen to someone that would make them speak up? A raid, a death, a
        /// night in the cold, a meal eaten off the floor. If so, that wish comes up within a few hours instead
        /// of whenever the dice say.
        /// </summary>
        private void CheckTriggers()
        {
            var s = Settings;
            if (!s.eventWishes) return;
            int now = Find.TickManager.TicksGame;
            if (now < GenDate.TicksPerDay * 2) return;
            if (OpenWishes.Count() >= s.maxOpenWishes + 1) return;
            var options = new List<(WishDef def, Pawn pawn, WishCandidate c, WishTrigger trig, float w)>();
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned)
            {
                if (!CanVoiceWishes(p) || OpenWishBy(p) != null) continue;
                var st = standings.For(p);
                if (now - st.lastTriggerTick < GenDate.TicksPerDay * 1.5f) continue;
                tmpThoughts.Clear();
                p.needs.mood.thoughts.GetAllMoodThoughts(tmpThoughts);
                foreach (var def in DefDatabase<WishDef>.AllDefsListForReading)
                {
                    if (def.triggers == null || OnCooldown(p, def)) continue;
                    foreach (var trig in def.triggers)
                    {
                        if (!trig.Matches(p, tmpThoughts)) continue;
                        foreach (var c in SafeCandidates(def, p))
                            options.Add((def, p, c, trig, c.weight * def.weight * trig.boost));
                        break;
                    }
                }
            }
            if (options.Count == 0 || !Rand.Chance(0.35f)) return;
            var pick = options.RandomElementByWeight(o => o.w);
            standings.For(pick.pawn).lastTriggerTick = now;
            CreateWish(pick.def, pick.pawn, pick.pawn, pick.c, pick.trig.key);
        }

        // ================= Daily =================

        private void Daily()
        {
            standings.Daily();
            var s = Settings;
            if (!s.enableWishes) return;
            // Bitter colonists grumble, and in Demanding mode they may sulk even without a fresh slight.
            foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned.ToList())
            {
                var st = standings.For(p, false);
                if (st == null || st.Level != TrustLevel.Bitter || !CanVoiceWishes(p)) continue;
                if (Rand.Chance(0.3f))
                {
                    string line = Scene("Council_Pool_Bitter", p, null);
                    if (line != null)
                    {
                        Speak(p, line, SpeechSour);
                        if (s.Negatives) Messages.Message("CC_BitterMessage".Translate(p.Named("PAWN")).Resolve() + " \"" + TextUtil.ShortSpeech(line, 120) + "\"",
                            new LookTargets(p), MessageTypeDefOf.SilentInput, false);
                    }
                }
                if (s.Harsh && Rand.Chance(0.2f)) MaybeSulk(p, null);
            }
        }
    }
}
