using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The council's questions about faith in the colony (Defs/DilemmaDefs/Rimfolk_Dilemmas_Faith.xml), with Ideology and
    // "Faith in the colony" on: when the colony's faiths clash (share the altar, give each faith its own corner, or name
    // one faith the colony's), and when one of the devout asks for an evening of faith (Faith/FaithEvenings.cs).

    /// <summary>
    /// A faith question as it happens: the faith it's about ({FAITH}: the colony's largest when faiths clash, the asker's
    /// for an evening), the other side's ({OTHERFAITH}), the altar or ideogram that stands ({ALTAR}), and a sentence on
    /// what sharpened it ({WHY}: a quarrel lately, the altar in plain sight). Faiths are kept by name, not by reference,
    /// so an archived letter never holds on to a faith the game has let go.
    /// </summary>
    public class FaithDilemmaContext : DilemmaContext
    {
        public string faithName;
        public string otherFaithName;
        public string altarLabel;
        public string why;

        [Unsaved] private Ideo faithCache;
        [Unsaved] private Ideo otherCache;

        public Ideo Faith
        {
            get => faithCache != null && faithCache.name == faithName ? faithCache : faithCache = FaithEvenings.Named(faithName);
            set
            {
                faithCache = value;
                faithName = value?.name;
            }
        }

        public Ideo OtherFaith
        {
            get => otherCache != null && otherCache.name == otherFaithName ? otherCache : otherCache = FaithEvenings.Named(otherFaithName);
            set
            {
                otherCache = value;
                otherFaithName = value?.name;
            }
        }

        protected override void AddArgs(List<NamedArgument> args)
        {
            args.Add((faithName ?? "").Named("FAITH"));
            args.Add((otherFaithName ?? "").Named("OTHERFAITH"));
            args.Add((altarLabel ?? "CC_FaithClash_NoAltar".Translate().ToString()).Named("ALTAR"));
            args.Add((why ?? "").Named("WHY"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref faithName, "faithName");
            Scribe_Values.Look(ref otherFaithName, "otherFaithName");
            Scribe_Values.Look(ref altarLabel, "altarLabel");
            Scribe_Values.Look(ref why, "why");
        }
    }

    /// <summary>When the colony's faiths clash, and what sharpens it.</summary>
    public static class FaithClash
    {
        /// <summary>The memories that say two people quarrelled (the game's insults and fights, Rimfolk's frictions).</summary>
        public static readonly List<string> Quarrels = new List<string> { "Insulted", "Slighted", "HadAngeringFight", "HadCatharticFight", "Council_Friction_*" };

        public const float QuarrelDays = 5f;

        /// <summary>The colony is split enough for its faiths to clash (FaithCouncilMath.Clashing).</summary>
        public static bool Clashing()
        {
            if (!Faiths.On) return false;
            var census = Faiths.Census();
            return census.Count >= 2 && FaithCouncilMath.Clashing(census.Count(f => f.believers.Count >= 2), Faiths.Split());
        }

        /// <summary>
        /// Two colonists of different faiths who quarrelled in the last few days (the one with the memory first), preferring
        /// a quarrel with a believer of <paramref name="faith"/>; null when there's been none.
        /// </summary>
        public static (Pawn a, Pawn b)? Quarrel(Map map, Ideo faith = null)
        {
            if (map == null) return null;
            (Pawn, Pawn)? any = null;
            int window = (int)(QuarrelDays * GenDate.TicksPerDay);
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (!Faiths.Holds(p)) continue;
                var memories = p.needs?.mood?.thoughts?.memories?.Memories;
                if (memories == null) continue;
                for (int i = 0; i < memories.Count; i++)
                {
                    var m = memories[i];
                    var o = m?.otherPawn;
                    if (o == null || m.def == null || m.age > window || !o.Spawned || o.Map != map || !Faiths.Holds(o) || Faiths.Same(p, o)) continue;
                    if (!Named(m.def.defName)) continue;
                    if (faith == null || Faiths.Of(p) == faith || Faiths.Of(o) == faith) return (p, o);
                    any ??= (p, o);
                }
            }
            return any;
        }

        private static bool Named(string defName)
        {
            foreach (var n in Quarrels)
                if (n.EndsWith("*") ? defName.StartsWith(n.Substring(0, n.Length - 1), StringComparison.Ordinal) : defName == n) return true;
            return false;
        }

        /// <summary>The sentence on what sharpened it ({WHY}): a quarrel lately, else the altar in plain sight, else nothing.</summary>
        public static string Why((Pawn a, Pawn b)? quarrel, Thing altar)
        {
            if (quarrel is (Pawn x, Pawn y)) return "CC_FaithClash_WhyQuarrel".Translate(x.Named("PAWN"), y.Named("OTHER")).Resolve();
            if (altar != null) return "CC_FaithClash_WhyAltar".Translate(altar.LabelShort.Named("ALTAR")).Resolve();
            return "";
        }
    }

    /// <summary>
    /// When faiths clash: two or more faiths each with two believers or more, or a split of one half or more. A is a
    /// believer of the largest faith (the devout first), B one of another (one who quarrelled lately with A's faith
    /// first, else the surest of the next largest). Half as likely to come up in a quiet split colony as after a quarrel
    /// between faiths, or while a faith's altar stands.
    /// </summary>
    public class DilemmaWorker_FaithClash : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!Faiths.On || !base.CanHappenNow(map) || !FaithClash.Clashing()) return false;
            var largest = Faiths.Census().FirstOrDefault()?.ideo;
            return Rand.Chance(FaithCouncilMath.ClashChance(FaithClash.Quarrel(map, largest) != null, FaithEvenings.AnyPlace(map) != null));
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var census = Faiths.Census();
            if (census.Count < 2) return false;
            var faith = census[0].ideo;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var quarrel = FaithClash.Quarrel(map, faith);
            var a = pawns.Where(p => Faiths.Of(p) == faith && def.roleA.Allows(p, null))
                .OrderByDescending(p => quarrel.HasValue && (quarrel.Value.a == p || quarrel.Value.b == p))
                .ThenByDescending(Faiths.Devout).ThenByDescending(Faiths.Certainty).FirstOrDefault();
            if (a == null) return false;
            var roleB = def.roleB ?? def.roleA;
            var b = pawns.Where(p => p != a && Faiths.Holds(p) && Faiths.Of(p) != faith && roleB.Allows(p, a))
                .OrderByDescending(p => quarrel.HasValue && (quarrel.Value.a == p || quarrel.Value.b == p))
                .ThenByDescending(p => census.FindIndex(f => f.ideo == Faiths.Of(p)) == 1)
                .ThenByDescending(Faiths.Devout).ThenByDescending(Faiths.Certainty).FirstOrDefault();
            if (b == null) return false;
            var altar = FaithEvenings.Place(map, faith) ?? FaithEvenings.AnyPlace(map);
            ctx = new FaithDilemmaContext
            {
                def = def,
                map = map,
                a = a,
                b = b,
                count = census.Count,
                Faith = faith,
                OtherFaith = Faiths.Of(b),
                altarLabel = altar?.LabelShort,
                why = FaithClash.Why(quarrel, altar)
            };
            return true;
        }
    }

    /// <summary>
    /// One of the devout asks for an evening of faith: A is the one who would speak for a faith that can hold one now
    /// (its moral guide, else its most devout believer with the best Social skill), and devout themselves.
    /// </summary>
    public class DilemmaWorker_FaithEvening : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => FaithEvenings.On && base.CanHappenNow(map) && FaithEvenings.CanCall(map, out _);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            if (map.mapPawns.FreeColonistsSpawned.Count(p => !p.IsQuestLodger()) < def.minColonists) return false;
            foreach (var f in Faiths.Census().InRandomOrder())
            {
                if (f?.ideo == null || !FaithEvenings.CanCall(map, f.ideo, out _)) continue;
                var speaker = FaithEvenings.Speaker(map, f.ideo);
                if (speaker == null || !Faiths.Devout(speaker) || !def.roleA.Allows(speaker, null)) continue;
                ctx = new FaithDilemmaContext { def = def, map = map, a = speaker, Faith = f.ideo, altarLabel = FaithEvenings.Place(map, f.ideo)?.LabelShort, count = f.believers.Count };
                return true;
            }
            return false;
        }
    }

    /// <summary>What the council answered when faiths clash.</summary>
    public enum FaithAnswer
    {
        /// <summary>The faiths take turns at the altar (or the fire).</summary>
        Share,
        /// <summary>Each faith gets a corner of its own.</summary>
        Corners,
        /// <summary>The largest faith is named the colony's.</summary>
        Name
    }

    /// <summary>
    /// The council's answer when faiths clash, in trust and certainty. Sharing the altar: the open-minded trust the council
    /// a little more, the devout a little less. Each its own corner: a little more trust all round, and everyone's
    /// certainty in their own faith steadied a little. Naming the largest faith the colony's: its believers trust the
    /// council more, everyone else less and is swayed a little toward it, and the devout of other faiths may take it hard
    /// (a lasting dent in their trust, not a mood).
    /// </summary>
    public class DilemmaEffect_FaithClash : DilemmaEffect
    {
        public FaithAnswer answer;

        public DilemmaEffect_FaithClash()
        {
            target = DilemmaTarget.Colony;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            var faith = (ctx as FaithDilemmaContext)?.Faith ?? Faiths.Census().FirstOrDefault()?.ideo;
            if (comp == null || !Faiths.On || faith == null) return;
            // apart: the council's answer, felt by each colonist wherever they are; what they do together plays out on the map.
            string why = ctx.def != null ? "CC_Log_Dilemma".Translate(ctx.def.label).ToString() : ctx.why;
            string cause = "CC_FaithClash_Cause".Translate(faith.name.Named("FAITH")).Resolve();
            foreach (var p in Targets(ctx).Distinct().ToList())
            {
                if (!Faiths.Holds(p)) continue;
                bool ours = Faiths.Of(p) == faith;
                bool open = Persona.Get(p, PersonaAxis.Openness) >= FaithCouncilMath.OpenFrom;
                switch (answer)
                {
                    case FaithAnswer.Share:
                        if (open) comp.standings.Change(p, 2f, why);
                        else if (Faiths.Devout(p)) comp.standings.Change(p, -1f, why);
                        break;
                    case FaithAnswer.Corners:
                        comp.standings.Change(p, 1f, why);
                        Faiths.Steady(p, 0.03f);
                        break;
                    case FaithAnswer.Name:
                        if (ours)
                        {
                            comp.standings.Change(p, 3f, why);
                            break;
                        }
                        comp.standings.Change(p, -2f, why);
                        Faiths.SwayToward(p, faith, 0.03f, cause);
                        if (Faiths.Devout(p) && Faiths.Of(p) != faith && Rand.Chance(Faiths.DislikesPreaching(p) ? 0.9f : 0.6f))
                        {
                            comp.standings.Change(p, -4f, "CC_Log_FaithNamed".Translate(faith.name.Named("FAITH")).Resolve());
                            comp.journals.Write(p, "Council_Journal_FaithNamed", null, faith.name.Named("FAITH"));
                        }
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Each faith its own corner: a letter suggests where each might keep it (a suggestion, nothing more): by its own
    /// altar or ideogram, in its most devout believer's room, in the rec room or the dining room, or a quiet spot outdoors.
    /// </summary>
    public class DilemmaEffect_FaithCorners : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null || !Faiths.On) return;
            // apart: a letter to the player, suggesting places; nobody is sent anywhere by it.
            var lines = new List<string>();
            var used = new HashSet<Room>();
            foreach (var f in Faiths.Census())
            {
                if (f?.ideo == null || !f.believers.Any(b => b.Spawned && b.Map == map)) continue;
                lines.Add("CC_FaithClash_CornerLine".Translate(f.ideo.name.Named("FAITH"), Corner(map, f, used).Named("WHERE")).Resolve());
            }
            if (lines.Count == 0) return;
            Find.LetterStack.ReceiveLetter("CC_FaithClash_CornersTitle".Translate(), "CC_FaithClash_CornersText".Translate(string.Join("\n", lines)).Resolve(),
                LetterDefOf.NeutralEvent);
        }

        private static string Corner(Map map, Faiths.Faith f, HashSet<Room> used)
        {
            try
            {
                var altar = FaithEvenings.Place(map, f.ideo);
                var room = altar?.GetRoom();
                if (altar != null && (room == null || room.PsychologicallyOutdoors || used.Add(room)))
                    return "CC_FaithClash_CornerAltar".Translate(altar.LabelShort.Named("ALTAR")).Resolve();
                var devout = f.believers.Where(b => b.Spawned && b.Map == map).OrderByDescending(Faiths.Certainty).FirstOrDefault();
                var own = RoomUtil.PrivateRoom(devout);
                if (own != null && !own.PsychologicallyOutdoors && used.Add(own))
                    return "CC_FaithClash_CornerRoom".Translate(devout.Named("PAWN")).Resolve();
                foreach (var r in RoomUtil.RoomsWithRole(map, RoomUtil.RecRoom))
                    if (used.Add(r)) return "CC_FaithClash_CornerRec".Translate();
                foreach (var r in RoomUtil.RoomsWithRole(map, RoomUtil.DiningRoom))
                    if (used.Add(r)) return "CC_FaithClash_CornerDining".Translate();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("faith corners", e);
            }
            return "CC_FaithClash_CornerOutside".Translate();
        }
    }

    /// <summary>Certainty steadied (Faiths.Steady): for the believers of the question's faith among the targets, or for everyone with a faith (<see cref="allFaiths"/>).</summary>
    public class DilemmaEffect_FaithSteady : DilemmaEffect
    {
        public float amount = 0.03f;
        public bool allFaiths;

        public DilemmaEffect_FaithSteady()
        {
            target = DilemmaTarget.Colony;
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer, felt by each believer wherever they are.
            var faith = (ctx as FaithDilemmaContext)?.Faith;
            if (!Faiths.On || amount <= 0f || (faith == null && !allFaiths)) return;
            foreach (var p in Targets(ctx).Distinct().ToList())
                if (allFaiths ? Faiths.Holds(p) : Faiths.Of(p) == faith)
                    Faiths.Steady(p, amount * Interplay.FaithFactor);
        }
    }

    /// <summary>The council says yes to an evening of faith: it's called now (Faith/FaithEvenings.cs), when it still can be.</summary>
    public class DilemmaEffect_FaithEvening : DilemmaEffect
    {
        private static Ideo FaithOf(DilemmaContext ctx) => (ctx as FaithDilemmaContext)?.Faith ?? Faiths.Of(ctx?.a);

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.map == null ? "CC_FaithEve_Why_NoMap".Translate().ToString() : FaithEvenings.CanCall(ctx.map, FaithOf(ctx), out string why) ? null : why;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            FaithEvenings.Call(ctx.map, FaithOf(ctx));
        }
    }

    /// <summary>
    /// Words said for a faith (a gathering, PlayOut/): with <see cref="speaker"/>, the faith's own speaker leads it (its
    /// moral guide, else its most devout believer); with <see cref="atAltar"/>, it's held at the faith's altar or
    /// ideogram when one stands; with <see cref="onlyFaith"/>, only the faith's own believers are asked. The faith is
    /// the question's ({FAITH}), else the one the decree names.
    /// </summary>
    public class DilemmaEffect_FaithWords : DilemmaEffect_Words
    {
        public bool speaker;
        public bool atAltar;
        /// <summary>With <see cref="atAltar"/>: any faith's altar will do when the faith has none of its own (the faiths sharing one).</summary>
        public bool anyAltar;
        public bool onlyFaith;

        private static Ideo FaithOf(DilemmaContext ctx) => (ctx as FaithDilemmaContext)?.Faith ?? DecreeFaith.Faith;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (!onlyFaith || ctx?.map == null) return null;
            var faith = FaithOf(ctx);
            int here = faith == null ? 0 : ctx.map.mapPawns.FreeColonistsSpawned.Count(p => Faiths.Of(p) == faith && PlayOuts.OurPerson(p));
            return here >= 2 ? null : "CC_FaithEve_Why_FewBelievers".Translate((faith?.name ?? "").Named("FAITH")).Resolve();
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            var faith = FaithOf(ctx);
            if (faith == null || !Faiths.On) return true;
            var lead = speaker ? FaithEvenings.Speaker(ctx.map, faith) : null;
            if (onlyFaith) p.invited = p.invited.Where(x => Faiths.Of(x) == faith).ToList();
            if (lead != null)
            {
                p.organizer = lead;
                if (!p.invited.Contains(lead)) p.invited.Add(lead);
            }
            else if (p.organizer != null && !p.invited.Contains(p.organizer)) p.organizer = null;
            if (p.invited.Count == 0) return false;
            if (atAltar)
            {
                var altar = FaithEvenings.Place(ctx.map, faith) ?? (anyAltar ? FaithEvenings.AnyPlace(ctx.map) : null);
                if (altar != null && altar.Spawned && altar.Map == ctx.map)
                {
                    p.place = altar;
                    p.spots.Remove(PlaySpot.Place);
                    p.spots.Insert(0, PlaySpot.Place);
                }
            }
            return true;
        }
    }
}
