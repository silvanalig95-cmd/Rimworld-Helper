using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The base of every Rimfolk world event. On its own it picks A (and B) from the extension's roles, applies the
    /// effects and sends a letter, or a choice letter when there are options. Subclasses cast strangers, spawn things
    /// and act on the answer.
    /// </summary>
    public class IncidentWorker_Happening : IncidentWorker
    {
        public HappeningExtension Ext => def.GetModExtension<HappeningExtension>();

        protected static CouncilSettings S => ColonyCouncilMod.Settings;

        protected static int Now => Find.TickManager.TicksGame;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            var map = parms.target as Map;
            var ext = Ext;
            if (map == null || ext == null || !map.IsPlayerHome) return false;
            try
            {
                return Gates(map, ext) && CanHappen(map, ext);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("event check " + def.defName, e);
                return false;
            }
        }

        /// <summary>Settings, mods, season, the colony's character, enough people.</summary>
        protected virtual bool Gates(Map map, HappeningExtension ext)
        {
            if (!S.worldEvents) return false;
            float temp = map.mapTemperature.OutdoorTemp;
            if (temp < ext.minTemp || temp > ext.maxTemp) return false;
            if (ext.battle && !BattlesAllowed(map)) return false;
            if (!ext.mods.NullOrEmpty() && !ext.mods.Any(m => ModsConfig.IsActive(m) || People.ModActive(m))) return false;
            if (!ext.seasons.NullOrEmpty() && !ext.seasons.Contains(GenLocalDate.Season(map))) return false;
            if ((!ext.colonyIs.NullOrEmpty() || !ext.colonyIsNot.NullOrEmpty()) && !(CouncilGameComponent.Instance?.character.Fits(ext.colonyIs, ext.colonyIsNot) ?? false)) return false;
            if (Colonists(map).Count < Mathf.Max(1, ext.minColonists)) return false;
            return true;
        }

        /// <summary>Fights only when the player wants them and the storyteller sends them, and not on top of another.</summary>
        public static bool BattlesAllowed(Map map) =>
            S.worldEventBattles && Find.Storyteller.difficulty.threatScale > 0.01f
            && !GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)
            && Now - map.storyState.LastThreatBigTick > GenDate.TicksPerDay * 2;

        /// <summary>A cheap check that the event could find its people now (nobody is made or spawned).</summary>
        protected virtual bool CanHappen(Map map, HappeningExtension ext) =>
            ext.colonist == null || Colonists(map).Any(p => ext.colonist.Allows(p, null));

        /// <summary>The colonists at home who have an inner life (no quest lodgers, no mechanoids).</summary>
        public static List<Pawn> Colonists(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p)).ToList();

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = parms.target as Map;
            var ext = Ext;
            if (map == null || ext == null) return false;
            var ctx = new HappeningContext { incident = def, map = map, points = parms.points };
            try
            {
                if (!Cast(ctx, ext) || !Begin(ctx, ext))
                {
                    Abort(ctx);
                    return false;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("event " + def.defName, e);
                Abort(ctx);
                return false;
            }
            Send(ctx, ext);
            CouncilGameComponent.Instance?.happenings.Notify_Fired(def, ext);
            return true;
        }

        /// <summary>Who it happens to: A (and B) from the roles. Subclasses add a stranger, a faction, a thing.</summary>
        protected virtual bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var pawns = Colonists(ctx.map);
            ctx.a = ext.colonist != null ? PickA(pawns, ext) : pawns.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (ctx.a == null) return false;
            if (ext.second != null)
            {
                ctx.b = pawns.Where(p => p != ctx.a && ext.second.Allows(p, ctx.a)).RandomElementWithFallback();
                if (ctx.b == null) return false;
            }
            if (ext.amount.max > 0) ctx.count = Mathf.RoundToInt(ext.amount.RandomInRange / 10f) * 10;
            return true;
        }

        protected virtual Pawn PickA(List<Pawn> pawns, HappeningExtension ext)
        {
            var options = pawns.Where(p => ext.colonist.Allows(p, null)).ToList();
            if (ext.colonist.bestAt != null) return options.OrderByDescending(p => p.skills?.GetSkill(ext.colonist.bestAt)?.Level ?? 0).FirstOrDefault();
            return options.RandomElementWithFallback();
        }

        /// <summary>Spawns whatever comes (visitors, a beast, a condition). False = it can't happen after all.</summary>
        protected virtual bool Begin(HappeningContext ctx, HappeningExtension ext) => true;

        /// <summary>Undoes what Cast made when the event can't go ahead.</summary>
        protected virtual void Abort(HappeningContext ctx)
        {
            if (ctx.xWaiting && ctx.stranger != null) Arrivals.Discard(ctx.stranger);
            ctx.stranger = null;
        }

        /// <summary>Tags for picking the text: the colony's era is already in; add the event's own (A's past, the skill).</summary>
        protected virtual void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
        }

        protected string Pick(List<string> lines, HappeningContext ctx)
        {
            if (lines.NullOrEmpty()) return null;
            var tags = new HashSet<string>();
            Flavor.AddEraTags(tags);
            tags.Add(GenLocalDate.Season(ctx.map).ToString().ToLowerInvariant());
            AddTags(ctx, tags);
            return Flavor.Pick(lines, tags) ?? Flavor.PickText(lines);
        }

        /// <summary>Something to add under the letter's text (what's at stake, a tip).</summary>
        protected virtual string Extra(HappeningContext ctx) => null;

        protected virtual LookTargets Look(HappeningContext ctx)
        {
            var targets = new List<GlobalTargetInfo>();
            foreach (var p in new[] { ctx.stranger, ctx.a, ctx.b, ctx.pet })
                if (p != null && p.Spawned) targets.Add(p);
            if (ctx.place != null && ctx.place.Spawned) targets.Add(ctx.place);
            return targets.Count > 0 ? new LookTargets(targets) : LookTargets.Invalid;
        }

        protected virtual void Send(HappeningContext ctx, HappeningExtension ext)
        {
            string label = ctx.Format(Pick(ext.titles, ctx) ?? def.letterLabel ?? def.label).CapitalizeFirst();
            ctx.text = ctx.Format(Pick(ext.texts, ctx));
            ctx.why = label;
            ctx.cause = label;
            string extra = Extra(ctx);
            string body = ctx.text + (extra.NullOrEmpty() ? "" : "\n\n" + extra);
            using (Causes.Push("CC_Cause_EventHappened".Translate(label).Resolve()))
            {
                foreach (var e in ext.effects) HappeningTracker.Safe(e, ctx);
            }
            if (ext.options.NullOrEmpty())
            {
                Find.LetterStack.ReceiveLetter(label, body, def.letterDef ?? (ext.threat ? LetterDefOf.ThreatSmall : LetterDefOf.NeutralEvent), Look(ctx));
                Happened(ctx, ext, label);
                return;
            }
            var letterDef = ext.threat ? CouncilDefOf.Council_EventLetterThreat : CouncilDefOf.Council_EventLetter;
            int ticks = Mathf.RoundToInt(Mathf.Max(0.1f, ext.answerDays) * GenDate.TicksPerDay);
            body += "\n\n" + "CC_Evt_Deadline".Translate(TextUtil.DaysString(ticks), ctx.Format(ext.options[Mathf.Clamp(ext.defaultOption, 0, ext.options.Count - 1)].label)).Resolve();
            var letter = (ChoiceLetter_Happening)LetterMaker.MakeLetter(label, body, letterDef, Look(ctx));
            letter.ctx = ctx;
            letter.StartTimeout(ticks);
            Find.LetterStack.ReceiveLetter(letter);
            Asked(ctx, ext);
        }

        /// <summary>A plain event happened: the chronicle, A's journal and the colony's character.</summary>
        protected virtual void Happened(HappeningContext ctx, HappeningExtension ext, string label)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            if (ext.ways != null) comp.character.Shift(ext.ways, label);
            if (ext.notable) comp.AddChronicle(new ChronicleEntry(ChronicleKind.World, label + ": " + Flavor.Strip(FirstSentence(ctx.text)), ctx.a));
            Journal(ctx, ext);
        }

        /// <summary>A question was put to the colony (the journal waits for the answer).</summary>
        protected virtual void Asked(HappeningContext ctx, HappeningExtension ext)
        {
        }

        /// <summary>A's journal: lines may be tagged with the answer the council gave ([opt0], [opt1]...) and [risk].</summary>
        protected void Journal(HappeningContext ctx, HappeningExtension ext, params string[] tags)
        {
            if (ext == null || ext.journal.NullOrEmpty() || ctx.a == null) return;
            var args = new List<NamedArgument>
            {
                (ctx.stranger != null ? ctx.stranger.Named("X") : "?".Named("X")),
                (ctx.relation ?? "").Named("RELATION"),
                (ctx.dead ?? "").Named("DEAD"),
                (ctx.beast ?? "").Named("BEAST"),
                (ctx.faction?.Name ?? "").Named("FACTION"),
                (ctx.enemy?.Name ?? "").Named("ENEMY"),
                (ctx.skill?.label ?? "").Named("SKILL"),
                (ctx.tale ?? "").Named("TALE")
            };
            CouncilGameComponent.Instance?.journals.WriteTagged(ctx.a, ext.journal, tags, null, args.ToArray());
        }

        private static string FirstSentence(string text)
        {
            if (text.NullOrEmpty()) return "";
            int cut = text.IndexOfAny(new[] { '.', '!', '?' });
            if (cut > 0 && cut < 220) return text.Substring(0, cut + 1);
            return text.Length > 220 ? text.Substring(0, 220) + "..." : text;
        }

        // ---------------- The answer ----------------

        /// <summary>Why this option can't be picked now (beyond its effects), or null.</summary>
        public virtual string Unavailable(HappeningContext ctx, int option) => null;

        /// <summary>The chance that the option's risk happens. The extension's risk skill (A's) lowers it.</summary>
        public virtual float RiskChance(HappeningContext ctx, int option, float chance)
        {
            var ext = Ext;
            var r = ext?.riskSkill != null && (ext.riskSkillOption < 0 || ext.riskSkillOption == option) ? ctx.a?.skills?.GetSkill(ext.riskSkill) : null;
            if (r != null && !r.TotallyDisabled) chance -= r.Level * 0.035f;
            return Mathf.Clamp(chance, 0.05f, 0.95f);
        }

        /// <summary>After the option's effects: anything code must do. Returns the outcome to tell (may change it).</summary>
        public virtual string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            Journal(ctx, Ext, "opt" + option, risk ? "risk" : "safe");
            return outcome;
        }

        /// <summary>Lets go of anyone kept waiting for the answer who didn't join.</summary>
        public virtual void Cleanup(HappeningContext ctx)
        {
            var x = ctx.stranger;
            if (ctx.xWaiting && x != null && !x.Spawned && x.Faction != Faction.OfPlayer) Arrivals.Release(x);
        }

        // ---------------- Helpers for subclasses ----------------

        protected static float PointsNow(Map map, FloatRange factor) => StorytellerUtility.DefaultThreatPointsNow(map) * factor.RandomInRange;

        protected static string Hours(float hours) => TextUtil.DaysString(Mathf.RoundToInt(hours * GenDate.TicksPerHour));
    }
}
