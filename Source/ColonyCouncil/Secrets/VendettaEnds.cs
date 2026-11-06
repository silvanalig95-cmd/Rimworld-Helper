using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The newer ways a vendetta ends (see <see cref="VendettaTracker"/>): the people who did it send the blood price
    /// (a ransom paid for the life, or for the one carried off), or come to say they are sorry once there's peace; and
    /// a vendetta left open by someone who died passes to whoever was closest to them, who takes it up (VendettaMath). The
    /// first two are Rimfolk events (Defs/IncidentDefs/Rimfolk_Events_VendettaEnds.xml); the heir is the tracker's own.
    /// </summary>
    public static class VendettaEnds
    {
        public const string SettledPool = "Council_Pool_VendettaEnds";
        public const string JournalPool = "Council_Journal_VendettaEnds";
        public const string HeirPool = "Council_Pool_VendettaHeir";

        /// <summary>An ending told by these lines rather than the older pool.</summary>
        public static bool Newer(string how) => how == "ransom" || how == "apology";

        private static int Now => Find.TickManager.TicksGame;

        private static bool Eligible(Pawn p) => People.HasInnerLife(p) && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile();

        // ---------------- Passed on ----------------

        /// <summary>
        /// What a colonist is to the dead, as the game and Rimfolk know it now: their opinion, the social web's bonds, a
        /// partner or a relative by blood, and their nature (VendettaMath.Bond).
        /// </summary>
        private static VendettaMath.Bond BondOf(CouncilGameComponent comp, Pawn p, Pawn dead)
        {
            var rels = p.GetRelations(dead).ToList();
            var traits = p.story?.traits;
            return new VendettaMath.Bond(p.relations.OpinionOf(dead), comp?.social?.Warmth(p, dead) ?? 0f,
                rels.Any(r => r == PawnRelationDefOf.Spouse || r == PawnRelationDefOf.Lover || r == PawnRelationDefOf.Fiance), rels.Any(r => r.familyByBloodRelation),
                traits != null && traits.HasTrait(TraitDefOf.Kind), traits != null && traits.HasTrait(TraitDefOf.Bloodlust));
        }

        /// <summary>Who could take up a dead avenger's vendetta at all: a grown colonist with an inner life, never one already sworn on the same people.</summary>
        private static List<Pawn> Candidates(VendettaTracker t, Vendetta v) =>
            PawnsFinder.AllMaps_FreeColonists.Where(p => p != v.avenger && Eligible(p) && p.relations != null
                                                         && !t.vendettas.Any(x => x.avenger == p && x.faction == v.faction && x.Active)).ToList();

        /// <summary>
        /// Who takes up a dead avenger's vendetta: whoever was closest to them, a partner, a friend or a relative, by how
        /// close they really were (VendettaMath.Heir: the game's opinion, the social web's bonds, a partner or a relative by
        /// blood as one part among them, the kind less readily, the bloodthirsty more), never someone already sworn on the
        /// same people. Since the player's decision of 6 November 2026; until then blood kin came first.
        /// </summary>
        public static Pawn Heir(CouncilGameComponent comp, VendettaTracker t, Vendetta v)
        {
            var dead = v?.avenger;
            if (dead == null) return null;
            var candidates = Candidates(t, v);
            int i = VendettaMath.Heir(candidates.Select(p => BondOf(comp, p, dead)).ToList());
            return i >= 0 ? candidates[i] : null;
        }

        /// <summary>
        /// At the death (the tracker's hour, before the day's look lets the social web's bonds with the dead go): who was
        /// closest of those who could take up each vendetta they leave open, kept for the day's look to pass it on.
        /// </summary>
        public static void NoteHeir(CouncilGameComponent comp, VendettaTracker t, Pawn dead)
        {
            if (comp == null || t == null || dead == null) return;
            foreach (var v in t.vendettas.Where(v => v.avenger == dead && !v.settled && v.faction != null).ToList())
            {
                try
                {
                    v.heirId = Heir(comp, t, v)?.thingIDNumber ?? VendettaMath.Nobody;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("vendetta heir noted", e);
                }
            }
        }

        /// <summary>
        /// The heir for the day's look: the one noted at the death while they still could take it up; nobody when nobody was
        /// close enough then; worked out from what is known now for an older save, or when the one noted can't (gone, or sworn
        /// on that people since).
        /// </summary>
        private static Pawn HeirNow(CouncilGameComponent comp, VendettaTracker t, Vendetta v)
        {
            if (v.heirId == VendettaMath.Nobody) return null;
            if (v.heirId > 0)
            {
                var noted = Candidates(t, v).FirstOrDefault(p => p.thingIDNumber == v.heirId);
                if (noted != null) return noted;
            }
            return Heir(comp, t, v);
        }

        /// <summary>Open vendettas whose avenger has died, against a people still at war with the colony: passed on, or let go.</summary>
        public static void PassToHeirs(CouncilGameComponent comp, VendettaTracker t)
        {
            if (comp == null || t == null) return;
            foreach (var v in t.vendettas.Where(v => !v.settled && v.avenger != null && v.avenger.Dead && v.faction != null && !v.faction.defeated).ToList())
            {
                try
                {
                    v.settled = true;
                    v.how = "heir";
                    if (!v.faction.HostileTo(Faction.OfPlayer) || Vendetta.KillRecord(v.faction) == null) continue;
                    var heir = HeirNow(comp, t, v);
                    if (heir == null) continue;
                    t.vendettas.Add(new Vendetta
                    {
                        avenger = heir, faction = v.faction, victim = v.victim, victimId = v.victimId, kidnapped = v.kidnapped, pet = v.pet,
                        tick = Now, heirOf = v.avenger.LabelShort
                    });
                    // The words say what they were to the dead: a partner, a relative by blood, or a friend.
                    var tags = new List<string> { VendettaMath.Tag(BondOf(comp, heir, v.avenger)) };
                    if (v.pet) tags.Add("pet");
                    if (v.kidnapped) tags.Add("taken");
                    string line = Flavor.FromPool(HeirPool, heir, null, null, tags.ToArray());
                    string text = !line.NullOrEmpty()
                        ? line.Formatted(heir.Named("PAWN"), v.avenger.Named("OTHER"), (v.victim ?? "?").Named("LOST"), v.faction.Name.Named("FACTION")).Resolve()
                        : "CC_Vendetta_Heir".Translate(heir.Named("PAWN"), v.avenger.Named("OTHER"), (v.victim ?? "?").Named("LOST"), v.faction.Name.Named("FACTION")).Resolve();
                    Notify.Message(text, heir, MessageTypeDefOf.NegativeEvent, Importance.Notable);
                    comp.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, text, heir));
                    comp.journals.WriteTagged(heir, "Council_Journal_VendettaHeir", tags.ToArray(), null, v.avenger.Named("OTHER"), (v.victim ?? "?").Named("LOST"), v.faction.Name.Named("FACTION"));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("vendetta heir", e);
                }
            }
        }

        // ---------------- The events ----------------

        public const string NotePrefix = "vendetta:";

        public static string Key(Vendetta v) => NotePrefix + (v.avenger?.ThingID ?? "?") + "|" + (v.faction?.loadID ?? -1) + "|" + (v.victimId ?? v.victim ?? "?");

        /// <summary>The open vendetta a context is about, or null.</summary>
        public static Vendetta Of(DilemmaContext ctx)
        {
            string n = (ctx as HappeningContext)?.note;
            if (n == null || !n.StartsWith(NotePrefix)) return null;
            return CouncilGameComponent.Instance?.vendettas?.vendettas.FirstOrDefault(v => !v.settled && Key(v) == n);
        }
    }

    /// <summary>Which vendettas an event that ends one can be about.</summary>
    public class VendettaEndEvent : DefModExtension
    {
        /// <summary>ransom: the people send the blood price (at war but ready for peace, or not at war); apology: they come to say sorry, at peace.</summary>
        public string want = "ransom";
        /// <summary>Days since it was sworn.</summary>
        public float minDays = 20f;
    }

    /// <summary>
    /// An envoy of the people a colonist swore vengeance on comes about it (VendettaEndEvent): with the blood price (a
    /// ransom for the life, {COUNT} silver), or with an apology. A is the avenger, {DEAD} the one lost, {FACTION} the
    /// people; an envoy of a people still at war waits at the edge of the map for the answer. Lines can ask for [taken]
    /// (the lost one was carried off), [pet], [kin], [partner], [friend] and [heir] (the vendetta was passed down).
    /// </summary>
    public class HappeningWorker_VendettaEnd : HappeningWorker_Visitors
    {
        private static readonly VendettaEndEvent Empty = new VendettaEndEvent();

        private VendettaEndEvent V => def.GetModExtension<VendettaEndEvent>() ?? Empty;

        private IEnumerable<Vendetta> Candidates(Map map)
        {
            var t = CouncilGameComponent.Instance?.vendettas;
            if (t == null || !S.vendettas) yield break;
            var want = V;
            foreach (var v in t.vendettas)
            {
                if (!v.Active || !v.avenger.IsColonist || !v.avenger.Spawned || v.avenger.Map != map || v.avenger.Downed) continue;
                var f = v.faction;
                if (!f.def.humanlikeFaction || Arrivals.PersonKind(f) == null || Now - v.tick < want.minDays * GenDate.TicksPerDay) continue;
                if (f.def.defName == "Empire" && !ModsConfig.RoyaltyActive) continue;
                bool hostile = f.HostileTo(Faction.OfPlayer);
                if (want.want == "apology" && hostile) continue;
                if (want.want == "ransom" && hostile && !HappeningWorker_Remembered.CanMakePeace(f)) continue;
                yield return v;
            }
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (V.want == "ransom" && GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return false;
            return Candidates(map).Any();
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var v = Candidates(ctx.map).RandomElementWithFallback();
            if (v == null) return false;
            ctx.faction = v.faction;
            ctx.a = v.avenger;
            ctx.dead = v.victim ?? "?";
            ctx.note = VendettaEnds.Key(v);
            ctx.placeName = LivingEvents.TownOf(v.faction, ctx.map) ?? v.faction.Name;
            if (ext.value.max > 0f) ctx.count = Mathf.Max(50, Mathf.RoundToInt(ext.value.RandomInRange * LivingEvents.WealthFactor(ctx.map) / 10f) * 10);
            MakeGroup(ctx, ext, Mathf.Max(1, ext.count.RandomInRange), ext.ages);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var everyone = ctx.group.Prepend(ctx.stranger).Where(p => p != null).ToList();
            if (ctx.faction.HostileTo(Faction.OfPlayer))
            {
                // An envoy of a people still at war waits at the edge of the colony's land for the answer.
                foreach (var p in everyone) Arrivals.Hold(p);
                ctx.xWaiting = true;
                return true;
            }
            return Arrivals.Visit(everyone, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) != null;
        }

        public override void Cleanup(HappeningContext ctx)
        {
            if (ctx.xWaiting)
                foreach (var p in ctx.group.Where(p => p != null && !p.Spawned && p.Faction != Faction.OfPlayer))
                    Arrivals.Release(p);
            base.Cleanup(ctx);
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var v = VendettaEnds.Of(ctx);
            if (v == null) return;
            if (v.kidnapped) tags.Add("taken");
            if (v.pet) tags.Add("pet");
            if (!v.heirOf.NullOrEmpty()) tags.Add("heir");
            if (ctx.faction != null && ctx.faction.HostileTo(Faction.OfPlayer)) tags.Add("atwar");
        }
    }

    /// <summary>
    /// The event's vendetta ends: <see cref="how"/> (ransom, apology). With <see cref="byAvenger"/>, the avenger decides,
    /// by their nature (the bloodthirsty and the cold refuse; the kind accept; the rest, mostly), and the message says
    /// which. <see cref="deed"/>: what the people remember of it, settled at once (the blood price paid, the apology made),
    /// and their old wrong (the life taken) settled with it.
    /// </summary>
    public class DilemmaEffect_EndVendetta : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var v = VendettaEnds.Of(ctx);
            if (v == null) return;
            string who = v.avenger?.LabelShort ?? "?";
            sheet.Add(ImpactKind.Goodwill, 1, (byAvenger ? "CC_Impact_VendettaAsk" : "CC_Impact_VendettaEnd").Translate(who, v.faction?.Name ?? "").Resolve(),
                "CC_Impact_VendettaBrief".Translate().Resolve());
        }

        public string how = "ransom";
        public bool byAvenger;
        public string deed;

        public override string Unavailable(DilemmaContext ctx) => VendettaEnds.Of(ctx) == null ? "CC_VendettaEnd_Gone".Translate().ToString() : null;

        /// <summary>Whether the avenger, left to decide, takes it.</summary>
        public static bool Accepts(Pawn p, string how)
        {
            var t = p?.story?.traits;
            if (t == null) return Rand.Chance(0.6f);
            if (t.HasTrait(TraitDefOf.Bloodlust) || t.HasTrait(TraitDefOf.Psychopath)) return false;
            if (t.HasTrait(TraitDefOf.Kind)) return true;
            float chance = how == "apology" ? 0.7f : 0.55f;
            if (t.HasTrait(TraitDefOf.Greedy) && how == "ransom") chance += 0.25f;
            if (t.HasTrait(TraitDefOf.Abrasive)) chance -= 0.2f;
            return Rand.Chance(chance);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var v = VendettaEnds.Of(ctx);
            var comp = CouncilGameComponent.Instance;
            if (v == null || comp == null) return;
            if (byAvenger && !Accepts(v.avenger, how))
            {
                Tell.Message(NewsKind.People, Importance.Notable, null, "CC_VendettaEnd_Refused".Translate(v.avenger.Named("PAWN"), (v.victim ?? "?").Named("LOST"), v.faction.Name.Named("FACTION")).Resolve(),
                    v.avenger, MessageTypeDefOf.NeutralEvent);
                var resolve = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Vendetta_NotForSale");
                if (resolve != null) CouncilGameComponent.GiveThought(v.avenger, resolve);
                return;
            }
            var f = v.faction;
            comp.vendettas.SettleBy(comp, v, how);
            if (!FactionMemory.On || f == null) return;
            if (!deed.NullOrEmpty()) FactionMemory.Settle(FactionMemory.Note(f, deed, v.victim));
            var mem = FactionMemory.Instance;
            if (mem != null)
                foreach (var d in mem.deeds.Where(d => d.faction == f && d.kind == "killedOurs" && d.name == v.victim && !d.settled).ToList())
                    FactionMemory.Settle(d);
        }
    }
}
