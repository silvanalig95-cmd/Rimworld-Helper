using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Something a colonist keeps to themselves: a raider's past, noble blood, a child left behind, a debt, a
    /// kindness done in secret. They may confide in a close friend; a friend can let it slip, a rival will;
    /// and when it comes out, everyone reacts according to their nature.
    /// </summary>
    public class SecretDef : Def
    {
        public float weight = 1f;
        /// <summary>Who could have this secret (traits, passions...).</summary>
        public DilemmaRole who;
        /// <summary>Words in their backstory titles that make this secret likely (raider, noble, soldier...). Any one fits.</summary>
        public List<string> backstory;
        /// <summary>Only with a matching backstory (otherwise a match just makes it likelier).</summary>
        public bool backstoryOnly;
        public int minAge = 16;
        /// <summary>What it is, told when it comes out ({PAWN_...} the owner).</summary>
        [MustTranslate] public string truth;
        /// <summary>A secret people will be glad to learn (a hidden kindness).</summary>
        public bool good;
        /// <summary>A big one: a letter rather than a message.</summary>
        public bool major;
        /// <summary>What happens when it comes out: A is the owner, B whoever let it slip. Others' opinions mostly.</summary>
        public List<DilemmaEffect> revealEffects = new List<DilemmaEffect>();
        /// <summary>The owner's memory when it comes out (default: exposed).</summary>
        public ThoughtDef ownerThought;

        public bool Fits(Pawn p, out bool backstoryMatch)
        {
            backstoryMatch = false;
            if (p.ageTracker == null || p.ageTracker.AgeBiologicalYears < minAge) return false;
            if (!DilemmaRole.Fits(who, p, false)) return false;
            if (!backstory.NullOrEmpty())
            {
                string titles = ((p.story?.Childhood?.title ?? "") + " " + (p.story?.Adulthood?.title ?? "")).ToLowerInvariant();
                // Whole words only: "con" shouldn't match "construction", nor "heir" "their".
                backstoryMatch = backstory.Any(w => System.Text.RegularExpressions.Regex.IsMatch(titles, @"\b" + System.Text.RegularExpressions.Regex.Escape(w.ToLowerInvariant()) + @"\b"));
                if (backstoryOnly && !backstoryMatch) return false;
            }
            return true;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (truth.NullOrEmpty()) yield return "secret has no truth text";
        }
    }

    public class Secret : IExposable
    {
        public SecretDef def;
        public Pawn owner;
        public List<Pawn> knowers = new List<Pawn>();
        public bool revealed;
        public int revealedTick;
        public Pawn leaker;
        public bool judged;

        public string Truth => def.truth.Formatted(owner.Named("PAWN")).Resolve();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref owner, "owner");
            Scribe_Collections.Look(ref knowers, "knowers", LookMode.Reference);
            Scribe_Values.Look(ref revealed, "revealed");
            Scribe_Values.Look(ref revealedTick, "revealedTick");
            Scribe_References.Look(ref leaker, "leaker");
            Scribe_Values.Look(ref judged, "judged");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (knowers == null) knowers = new List<Pawn>();
                knowers.RemoveAll(k => k == null);
            }
        }
    }

    public class SecretTracker : IExposable
    {
        public List<Secret> secrets = new List<Secret>();
        /// <summary>Colonists already looked at (most have no secret worth the name).</summary>
        private HashSet<string> rolled = new HashSet<string>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public Secret Of(Pawn p) => secrets.FirstOrDefault(s => s.owner == p);

        public IEnumerable<Secret> RecentlyRevealed(float days) =>
            secrets.Where(s => s.revealed && Find.TickManager.TicksGame - s.revealedTick < days * GenDate.TicksPerDay);

        private static bool Eligible(Pawn p) => p != null && !p.Dead && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile();

        public void Daily(CouncilGameComponent comp)
        {
            if (!S.secrets) return;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.Where(Eligible).ToList())
            {
                if (rolled.Contains(p.ThingID)) continue;
                if ((p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0) < GenDate.TicksPerDay * 2) continue;
                // Teens raised here get their chance once they're old enough to have a past.
                if ((p.ageTracker?.AgeBiologicalYears ?? 0) < 16) continue;
                rolled.Add(p.ThingID);
                TryGive(p);
            }
            foreach (var s in secrets.ToList())
            {
                try
                {
                    Step(comp, s);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("secret " + s.def?.defName, e);
                }
            }
        }

        private void TryGive(Pawn p)
        {
            var options = new List<(SecretDef def, float w)>();
            foreach (var d in DefDatabase<SecretDef>.AllDefsListForReading)
            {
                if (!d.Fits(p, out bool match)) continue;
                options.Add((d, d.weight * (match ? 6f : 1f)));
            }
            // A backstory that fits a secret makes one much likelier.
            bool anyMatch = options.Any(o => o.w > o.def.weight);
            if (options.Count == 0 || !Rand.Chance(anyMatch ? 0.7f : 0.35f)) return;
            secrets.Add(new Secret { def = options.RandomElementByWeight(o => o.w).def, owner = p });
        }

        private void Step(CouncilGameComponent comp, Secret s)
        {
            var owner = s.owner;
            s.knowers.RemoveAll(k => k == null || k.Dead);
            if (owner == null || s.def == null)
            {
                secrets.Remove(s);
                return;
            }
            if (owner.Dead)
            {
                if (!s.revealed) Grave(comp, s);
                secrets.Remove(s);
                return;
            }
            if (s.revealed || !owner.IsColonist) return;
            var social = comp.social;

            // Confiding: in a close friend who doesn't know yet.
            if (Rand.Chance(0.04f))
            {
                var friend = social.ties.Where(t => t.kind == TieKind.Friends && t.announced && t.strength >= 50f && t.Involves(owner))
                    .Select(t => t.Other(owner)).Where(f => f != null && f.IsColonist && !f.Dead && !s.knowers.Contains(f)).RandomElementWithFallback();
                if (friend != null) Confide(comp, s, friend);
            }
            // Someone who knows lets it slip; much likelier if they've fallen out since.
            foreach (var k in s.knowers.ToList())
            {
                if (!k.IsColonist) continue;
                float chance = 0.008f;
                if (social.Strength(k, owner, TieKind.Rivals) >= 20f) chance *= 6f;
                else if (social.Strength(k, owner, TieKind.Friends) >= 70f) chance *= 0.3f;
                var traits = k.story?.traits;
                if (traits != null && traits.HasTrait(TraitDefOf.Abrasive)) chance *= 2f;
                if (traits != null && traits.HasTrait(TraitDefOf.Kind)) chance *= 0.4f;
                if (Rand.Chance(chance))
                {
                    Reveal(comp, s, k);
                    return;
                }
            }
            // At the bottom, some people just say it out loud.
            if ((owner.needs?.mood?.CurLevelPercentage ?? 1f) < 0.15f && Rand.Chance(0.05f)) Reveal(comp, s, owner);
        }

        public void Confide(CouncilGameComponent comp, Secret s, Pawn friend)
        {
            var owner = s.owner;
            s.knowers.Add(friend);
            comp.social.Bump(owner, friend, TieKind.Friends, 6f, "CC_TieNote_Confided".Translate());
            CouncilGameComponent.GiveThought(owner, CouncilDefOf.Council_Secret_Unburdened);
            CouncilGameComponent.GiveThought(friend, CouncilDefOf.Council_Secret_Trusted, owner);
            comp.journals.Write(owner, "Council_Journal_Confided", null, friend.Named("OTHER"));
            comp.journals.Write(friend, "Council_Journal_Confidant", null, owner.Named("OTHER"));
            Messages.Message("CC_Secret_Confided".Translate(owner.Named("A"), friend.Named("B")).Resolve(), new LookTargets(owner, friend), MessageTypeDefOf.SilentInput, false);
        }

        /// <summary>It comes out. <paramref name="leaker"/> is the owner when they confess it themselves.</summary>
        public void Reveal(CouncilGameComponent comp, Secret s, Pawn leaker)
        {
            var owner = s.owner;
            bool confessed = leaker == null || leaker == owner;
            s.revealed = true;
            s.revealedTick = Find.TickManager.TicksGame;
            s.leaker = confessed ? null : leaker;
            string how = confessed
                ? "CC_Secret_HowConfessed".Translate(owner.Named("PAWN")).Resolve()
                : "CC_Secret_HowLeaked".Translate(leaker.Named("LEAKER"), owner.Named("PAWN")).Resolve();
            string text = s.Truth + "\n\n" + how;
            var look = confessed ? new LookTargets(owner) : new LookTargets(owner, leaker);
            if (s.def.major) Find.LetterStack.ReceiveLetter("CC_Secret_Label".Translate(owner.LabelShort), text, s.def.good ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, look);
            else Messages.Message("CC_Secret_Short".Translate(owner.Named("PAWN")).Resolve() + " " + s.Truth, look, s.def.good ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, false);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, s.Truth, owner));
            comp.annals.Notify_Secret(owner, s.def.label);

            // The owner.
            var ownerThought = s.def.ownerThought ?? (confessed ? CouncilDefOf.Council_Secret_Confessed : CouncilDefOf.Council_Secret_Exposed);
            CouncilGameComponent.GiveThought(owner, ownerThought);
            comp.journals.Write(owner, confessed ? "Council_Journal_SecretConfessed" : "Council_Journal_SecretOut", null, (leaker ?? owner).Named("OTHER"));
            // Betrayal: a confidant who told.
            if (!confessed && s.knowers.Contains(leaker) && !s.def.good)
            {
                CouncilGameComponent.GiveThought(owner, CouncilDefOf.Council_Secret_Betrayed, leaker);
                comp.social.Weaken(owner, leaker, TieKind.Friends, 30f, "CC_TieNote_Betrayed".Translate());
                comp.social.Bump(owner, leaker, TieKind.Rivals, 15f, "CC_TieNote_Betrayed".Translate());
            }
            // Everyone else, according to their nature.
            var ctx = new DilemmaContext { map = owner.MapHeld ?? Find.AnyPlayerHomeMap, a = owner, b = confessed ? null : leaker, why = s.def.label };
            foreach (var e in s.def.revealEffects)
            {
                try
                {
                    e.Apply(ctx);
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("secret effect " + s.def.defName, ex);
                }
            }
            // The council may be asked to do something about it.
            if (S.enableDilemmas) comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_Secret);
        }

        /// <summary>Died with it. If someone knew, they tell it now; otherwise it's gone.</summary>
        private void Grave(CouncilGameComponent comp, Secret s)
        {
            var teller = s.knowers.Where(k => k.IsColonist && !k.Dead).RandomElementWithFallback();
            if (teller == null)
            {
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Secret_ToTheGrave".Translate(s.owner.Named("PAWN")).Resolve(), null));
                return;
            }
            string text = "CC_Secret_Posthumous".Translate(teller.Named("TELLER"), s.owner.Named("PAWN")).Resolve() + " " + s.Truth;
            Messages.Message(text, teller, MessageTypeDefOf.NeutralEvent, false);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, text, teller));
            comp.journals.Write(teller, "Council_Journal_SecretKept", null, s.owner.Named("OTHER"));
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref secrets, "secrets", LookMode.Deep);
            Scribe_Collections.Look(ref rolled, "rolled", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (secrets == null) secrets = new List<Secret>();
                secrets.RemoveAll(s => s == null || s.def == null || s.owner == null);
                if (rolled == null) rolled = new HashSet<string>();
            }
        }
    }

    /// <summary>A dilemma about a secret that has just come out: A is its owner, B whoever let it slip (if anyone).</summary>
    public class DilemmaWorker_Secret : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.secrets || !CanHappenNow(map)) return false;
            if (map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            foreach (var s in comp.secrets.RecentlyRevealed(6f).Where(s => !s.judged && (def.secrets.NullOrEmpty() || def.secrets.Contains(s.def.defName))).InRandomOrder())
            {
                var a = s.owner;
                if (a == null || !a.Spawned || a.Map != map || !def.roleA.Allows(a, null)) continue;
                Pawn b = null;
                if (def.roleB != null)
                {
                    b = s.leaker != null && def.roleB.Allows(s.leaker, a) ? s.leaker
                        : map.mapPawns.FreeColonistsSpawned.Where(p => def.roleB.Allows(p, a)).RandomElementWithFallback();
                    if (b == null) continue;
                }
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = def.thingCount };
                if (!Extra(ctx)) continue;
                s.judged = true;
                return true;
            }
            ctx = null;
            return false;
        }
    }
}
