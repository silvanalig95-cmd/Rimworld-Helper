using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Leaving on good terms, always as a question for the council, and only for a reason that's really there
    /// (Defs/DilemmaDefs/Rimfolk_Dilemmas_MovingOn.xml). Each question's answer if nobody answers is that they stay.
    /// <list type="bullet">
    /// <item>Family elsewhere (<see cref="DilemmaWorker_FamilyElsewhere"/>): a parent, brother or sister of theirs with
    /// a friendly people, old now, or a child of theirs growing up there. Going, they join that people.</item>
    /// <item>A love from outside (<see cref="DilemmaWorker_LoveOutside"/>): their partner from a friendly people is
    /// visiting. They can go to them, the partner can be asked to join (not a Hospitality guest: Hospitality has its own
    /// way), or things stay as they are. Asked when the partner is here.</item>
    /// <item>No room (<see cref="DilemmaWorker_Crowded"/>): someone who has slept crowded (barracks, the floor, the open)
    /// and been unhappy, with no partner or family here to keep them, wants to move on somewhere with room.</item>
    /// </list>
    /// Never one of the colony's last four grown-ups. Each person or couple is asked about once
    /// (<see cref="CouncilGameComponent.movingOnAsked"/>).
    /// </summary>
    public static class MovingOn
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.movingOn && S.enableDilemmas;

        /// <summary>One of ours who could choose to leave: a grown-up with an inner life, up and about at home, not a lodger.</summary>
        public static bool Could(Pawn p) =>
            p != null && !p.Dead && p.Spawned && p.IsColonist && !p.IsSlave && !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Juvenile()
            && Flavor.HumanAge(p) >= 18f && !p.Downed && !p.InMentalState && p.Map.IsPlayerHome;

        /// <summary>Enough grown-ups would be left: never one of the last four.</summary>
        public static bool Spare(Map map) => map != null && map.mapPawns.FreeColonistsSpawned.Count(c => !c.IsQuestLodger() && !c.DevelopmentalStage.Juvenile()) > 4;

        /// <summary>A people they could go to: friendly enough, still in the world, one the colony knows.</summary>
        public static bool Friendly(Faction f) => f != null && !f.IsPlayer && !f.defeated && !f.Hidden && !f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction;

        public static string Key(string kind, Pawn a, Pawn b = null) => kind + "|" + a.thingIDNumber + (b != null ? "|" + b.thingIDNumber : "");

        public static bool Asked(string key) => CouncilGameComponent.Instance?.movingOnAsked.Contains(key) ?? true;

        public static void MarkAsked(string key)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || comp.movingOnAsked.Contains(key)) return;
            comp.movingOnAsked.Add(key);
            if (comp.movingOnAsked.Count > 60) comp.movingOnAsked.RemoveRange(0, comp.movingOnAsked.Count - 60);
        }

        /// <summary>While the question comes for a partner just arrived, outside its turn.</summary>
        public static bool OnlyLove { get; private set; }

        /// <summary>The council's hour: a colonist's partner from outside is here, not asked about before: the question now.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (comp == null) return;
            // Those kept from going (MovingOnHeld): a relative sent for arrives, a partner back for a second asking.
            MovingOnHeld.Hourly(comp);
            if (!On) return;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                if (!DilemmaWorker_LoveOutside.Pairs(map).Any()) continue;
                OnlyLove = true;
                try
                {
                    comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_LoveOutside);
                }
                finally
                {
                    OnlyLove = false;
                }
                return;
            }
        }
    }

    /// <summary>
    /// A question about someone moving on: who they'd go to ({RELATIVE}, and what they are to them, {RELATION}) and the
    /// people they'd join ({FACTION}). Kept by name, so the letter reads the same if that person dies meanwhile.
    /// </summary>
    public class MovingOnContext : DilemmaContext
    {
        public string relativeName;
        public string relation;
        /// <summary>Why ("family", "love", "room"), how much they want to go (MovingOnMath), the relative who could be sent for, and a promised room never given.</summary>
        public string kind;
        public float strength = 0.5f;
        public Pawn relative;
        public bool broken;

        protected override void AddArgs(List<NamedArgument> args)
        {
            args.Add((relativeName ?? "").Named("RELATIVE"));
            args.Add((relation ?? "").Named("RELATION"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref relativeName, "relativeName");
            Scribe_Values.Look(ref relation, "relation");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref strength, "strength", 0.5f);
            Scribe_References.Look(ref relative, "relative");
            Scribe_Values.Look(ref broken, "broken");
        }
    }

    /// <summary>
    /// Family elsewhere: A has a parent, brother or sister with a friendly people who is old now (65 or more), or a
    /// child of theirs growing up there, and thinks well of them. Tags: "old" or "child" for which.
    /// </summary>
    public class DilemmaWorker_FamilyElsewhere : DilemmaWorker
    {
        private static bool Elsewhere(Pawn x) =>
            x != null && !x.Dead && MovingOn.Friendly(x.Faction) && !x.IsPrisoner && !x.IsSlave && x.RaceProps.Humanlike && !(x.MapHeld?.IsPlayerHome ?? false);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!MovingOn.On || !CanHappenNow(map) || !MovingOn.Spare(map)) return false;
            var options = new List<(Pawn a, Pawn x, string rel, bool child)>();
            foreach (var a in map.mapPawns.FreeColonistsSpawned.Where(MovingOn.Could))
            {
                if (a.relations == null) continue;
                foreach (var x in a.relations.FamilyByBlood)
                {
                    if (!Elsewhere(x) || a.relations.OpinionOf(x) < 20 || MovingOn.Asked(MovingOn.Key("family", a, x))) continue;
                    string rel = a.GetMostImportantRelation(x)?.defName;
                    bool child = rel == "Child" && x.DevelopmentalStage.Juvenile();
                    bool old = (rel == "Parent" || rel == "Sibling") && Flavor.HumanAge(x) >= 65f;
                    if (child || old) options.Add((a, x, rel, child));
                }
            }
            if (options.Count == 0) return false;
            var (pa, px, _, isChild) = options.RandomElement();
            ctx = new MovingOnContext
            {
                def = def, map = map, a = pa, faction = px.Faction, relativeName = px.LabelShort, relation = Family.Relation(pa, px), count = 150,
                kind = "family", relative = px, strength = MovingOnMath.Family(pa.relations.OpinionOf(px), isChild)
            };
            MovingOn.MarkAsked(MovingOn.Key("family", pa, px));
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is MovingOnContext m) || ctx.a?.relations == null) return;
            var x = ctx.a.relations.FamilyByBlood.FirstOrDefault(o => o.LabelShort == m.relativeName);
            if (x == null) return;
            tags.Add(ctx.a.GetMostImportantRelation(x)?.defName == "Child" ? "child" : "old");
        }
    }

    /// <summary>
    /// A love from outside: A's partner (a lover, betrothed or spouse) of a friendly people is here on a visit, the
    /// stranger X. Tags: "guest" when X is a Hospitality guest (who can't be asked to join through the council),
    /// "married" or "engaged" for what they are to each other.
    /// </summary>
    public class DilemmaWorker_LoveOutside : DilemmaWorker
    {
        public static IEnumerable<(Pawn a, Pawn x)> Pairs(Map map)
        {
            if (map == null || !MovingOn.Spare(map)) yield break;
            foreach (var a in map.mapPawns.FreeColonistsSpawned.Where(MovingOn.Could))
            {
                var x = LovePartnerRelationUtility.ExistingLovePartner(a, false);
                if (x == null || x.Dead || !x.Spawned || x.Map != map || x.IsColonist || x.IsPrisoner || x.IsSlave || !MovingOn.Friendly(x.Faction)) continue;
                if (MovingOn.Asked(MovingOn.Key("love", a, x))) continue;
                yield return (a, x);
            }
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!MovingOn.On || !CanHappenNow(map)) return false;
            var pairs = Pairs(map).ToList();
            if (pairs.Count == 0) return false;
            var (a, x) = pairs.RandomElement();
            bool married = a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, x), engaged = a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, x);
            ctx = new MovingOnContext
            {
                def = def, map = map, a = a, stranger = x, faction = x.Faction, relativeName = x.LabelShort, relation = Family.Relation(a, x),
                kind = "love", strength = MovingOnMath.Love(married, engaged, a.relations.OpinionOf(x))
            };
            MovingOn.MarkAsked(MovingOn.Key("love", a, x));
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var x = ctx?.stranger;
            if (ctx?.a?.relations == null || x == null) return;
            if (Strangers.IsHospitalityGuest(x)) tags.Add("guest");
            if (ctx.a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, x)) tags.Add("married");
            else if (ctx.a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, x)) tags.Add("engaged");
        }
    }

    /// <summary>
    /// No room: A has slept crowded (in barracks, on the floor or in the open) and is unhappy (mood under 35%), has no
    /// partner or family at home, and has been here at least twenty days.
    /// </summary>
    public class DilemmaWorker_Crowded : DilemmaWorker
    {
        private static readonly string[] Crowded = { "SleptInBarracks", "SleptOutside", "SleptOnGround" };

        private static bool SleptCrowded(Pawn p) =>
            p.needs?.mood?.thoughts?.memories?.Memories?.Any(m => m.def != null && Crowded.Contains(m.def.defName)) ?? false;

        private static bool Ties(Pawn p) =>
            p.Map.mapPawns.FreeColonistsSpawned.Any(o => o != p && (LovePartnerRelationUtility.LovePartnerRelationExists(p, o) || Family.Of(p, o)));

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!MovingOn.On || !CanHappenNow(map) || !MovingOn.Spare(map)) return false;
            var a = map.mapPawns.FreeColonistsSpawned
                .Where(p => MovingOn.Could(p) && (p.needs?.mood?.CurLevelPercentage ?? 1f) < 0.35f && SleptCrowded(p) && !Ties(p)
                            && (p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0) >= GenDate.TicksPerDay * 20
                            && !MovingOn.Asked(MovingOn.Key("room", p)))
                .OrderBy(p => p.needs.mood.CurLevelPercentage).FirstOrDefault();
            if (a == null) return false;
            ctx = new MovingOnContext { def = def, map = map, a = a, kind = "room", strength = MovingOnMath.Room(a.needs.mood.CurLevelPercentage) };
            MovingOn.MarkAsked(MovingOn.Key("room", a));
            return true;
        }
    }

    /// <summary>The council promises A a room of their own: the wish for one, where wishes can be raised for them now.</summary>
    public class DilemmaEffect_PromiseRoom : DilemmaEffect
    {
        public WishDef wish;

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's word; the room itself is the colony's to build.
            var comp = CouncilGameComponent.Instance;
            if (comp == null || wish == null || ctx?.a == null || ctx.a.Dead) return;
            try
            {
                comp.TryWishFromDream(ctx.a, wish);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("promised a room", e);
            }
        }
    }
}
