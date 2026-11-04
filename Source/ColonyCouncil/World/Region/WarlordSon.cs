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
    /// The warlord's son (docs/design/stage1-region.md, item 7; decision 12): a world event 60 to 180 days after a
    /// warlord's story ended, by how it ended (FollowUpsMath.Branch). His father slain by the colony: he raises a band, a
    /// raiders' camp near the home that he leads (on its map, at the head of its raids). Captured, handed over or
    /// ransomed: he comes to the gate for his father, his father's grave or weapon, or a peace. Hunted down by others: an
    /// exile asking shelter. His band broken up or gone: a young man with a few riders looking for work. He is his
    /// father's child by the game's relations while the game still has the father, else known as his son by name. The
    /// four defs are in Defs/WorldSituationDefs/Rimfolk_WS_WarlordSon.xml.
    /// </summary>
    public static class WarlordSons
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static WorldSituationDef Def(SonBranch b) => b == SonBranch.None ? null : DefDatabase<WorldSituationDef>.GetNamedSilentFail("Rimfolk_WS_Son" + b);

        // ---------------- Hooks (World/WarlordWorker.cs) ----------------

        /// <summary>The warlord is at a home (leading a raid): his weapon is noted, for a son who may ask for it back.</summary>
        public static void Watch(WorldSituation s, Pawn x)
        {
            var w = x?.equipment?.Primary;
            if (s != null && w != null) s.SetNum("blade", w.thingIDNumber);
        }

        /// <summary>The warlord's story ended (before his fate is settled): his son will come, by how it ended.</summary>
        public static void Remember(WorldSituation s, SituationEnding ending)
        {
            try
            {
                string key = s?.endingKey;
                if (s?.ctx == null || ending == null || key.NullOrEmpty() || key == WorldSituations.Quietly) return;
                if (FollowUpsMath.Branch(key) == SonBranch.None) return;
                var f = s.ctx.faction;
                if (!FactionMemory.Remembers(f) || f.defeated) return;
                int now = Now;
                var fu = new FollowUp
                {
                    kind = "son", people = f, other = s.ctx.enemy, ending = key, endTick = now, due = FollowUpsMath.DueTick(now, Rand.Value), tale = s.ctx.name,
                    flags = s.flags.ToList()
                };
                var x = s.ctx.stranger;
                if (x != null)
                {
                    // His name is kept even when the game has let him go; the pawn only while it has him.
                    fu.who = x.Discarded ? null : x;
                    fu.whoName = x.LabelShort;
                    fu.whoLast = (x.Name as NameTriple)?.Last;
                    fu.whoGender = x.gender;
                    fu.whoAge = x.ageTracker?.AgeBiologicalYearsFloat ?? -1f;
                }
                if (s.Has("slayer") && KinMemory.Ours(s.ctx.b))
                {
                    fu.by = s.ctx.b;
                    fu.byName = s.ctx.b.LabelShort;
                }
                int blade = s.Num("blade");
                if (blade > 0) fu.thing = blade;
                FollowUps.Add(FollowUps.HomeOf(s.ctx.map), fu);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("warlord's son: remembered", e);
            }
        }

        // ---------------- Starting ----------------

        /// <summary>Starts the son's world event at this home now, by the branch it can take today; null when it can't.</summary>
        public static WorldSituation Start(Map home, FollowUp f)
        {
            var ws = WorldSituations.Instance;
            if (ws == null || home == null || f?.people == null) return null;
            var branch = FollowUpsMath.Branch(f.ending);
            bool canCamp = branch == SonBranch.Band && CanCamp(home, f.people);
            branch = FollowUpsMath.Now(branch, canCamp, Banner(home, f) != null);
            var def = Def(branch);
            if (def == null) return null;
            SituationWorker_Son.pending = f;
            try
            {
                return ws.TryStart(home, d => d == def, false);
            }
            finally
            {
                SituationWorker_Son.pending = null;
            }
        }

        /// <summary>
        /// His people can raise a camp near the home: raiders' camps on, fights in world events allowed (the band's own
        /// event is a fight), none there now, his people at war with the colony and able to hold one.
        /// </summary>
        public static bool CanCamp(Map home, Faction people) =>
            RaidersCamps.On && IncidentWorker_Happening.FightsPossible && RaidersCamps.CampOf(home) == null && people != null && !people.defeated
            && people.HostileTo(Faction.OfPlayer) && RaidersCamps.CanOwn(people);

        /// <summary>
        /// The people he rides with to the gate: his own while they are at peace with the colony, else the people at peace
        /// with the town nearest the home (never those who hunted his father down). Null when there is none.
        /// </summary>
        public static Faction Banner(Map home, FollowUp f, bool othersFirst = false)
        {
            if (home == null || f?.people == null) return null;
            bool own = !f.people.defeated && Visitors.Usable(f.people) && WorldSituationWorker.NearestTown(f.people, home) != null;
            if (own && !othersFirst) return f.people;
            Faction best = null;
            float bestTiles = float.MaxValue;
            foreach (var p in Find.FactionManager.AllFactionsListForReading)
            {
                if (p == null || p == f.other || p == f.people || !FactionMemory.Remembers(p) || !Visitors.Usable(p)) continue;
                var town = WorldSituationWorker.NearestTown(p, home);
                if (town == null) continue;
                float d = WorldSituationWorker.Distance(home, town);
                if (d < bestTiles)
                {
                    best = p;
                    bestTiles = d;
                }
            }
            return best ?? (own ? f.people : null);
        }
    }

    /// <summary>
    /// The warlord's son as a world event: what every branch shares. It never starts by itself: the follow-ups start it
    /// (WarlordSons.Start) with what it follows. {X} the son, {DEAD} his father (or mother), {RELATION} "father" or
    /// "mother", {TALE} the warband, {B} the colonist who killed his father (while still with the colony, flag slayer),
    /// {OTHER} his father while the game has him. Flags: own (he rides with his own people) or among (with
    /// {FACTION}, his father's people being {ENEMY}), mother, kin (his parent by the game's relations), afterEnd:KEY.
    /// </summary>
    public abstract class SituationWorker_Son : WorldSituationWorker
    {
        /// <summary>What it follows, given just before it starts; none at any other time.</summary>
        internal static FollowUp pending;

        public override bool CanStart(Map map) => pending != null && base.CanStart(map);

        protected override bool CanHappen(Map map) => pending?.people != null && !pending.people.defeated;

        protected static Pawn Son(WorldSituation s) => s.ctx.stranger;

        /// <summary>The people he comes with: his father's for a band, else the one he rides with (WarlordSons.Banner).</summary>
        protected virtual Faction PeopleFor(Map map, FollowUp f) => WarlordSons.Banner(map, f);

        /// <summary>What only this branch does (a camp set up, what he will ask); false: it can't happen after all.</summary>
        protected virtual bool CastBranch(WorldSituation s, Map map, FollowUp f) => true;

        /// <summary>A fighter for a band, else someone of his people as they come.</summary>
        protected virtual bool Fighter => false;

        public override bool Cast(WorldSituation s, Map map)
        {
            var f = pending;
            if (f?.people == null || map == null) return false;
            var ctx = s.ctx;
            ctx.map = map;
            var banner = PeopleFor(map, f);
            if (banner == null) return false;
            ctx.faction = banner;
            if (banner != f.people)
            {
                ctx.enemy = f.people;
                s.flags.Add("among");
            }
            else s.flags.Add("own");
            s.At(NearestTown(banner, map), banner.Name);
            ctx.tale = !f.tale.NullOrEmpty() ? f.tale : f.people.Name;
            ctx.dead = !f.whoName.NullOrEmpty() ? f.whoName : "CC_Son_Unnamed".Translate().Resolve();
            bool mother = f.whoGender == Gender.Female;
            if (mother) s.flags.Add("mother");
            ctx.relation = (mother ? "CC_Son_Mother" : "CC_Son_Father").Translate().Resolve();
            var parent = f.who != null && !f.who.Discarded ? f.who : null;
            if (parent != null)
            {
                ctx.other = parent;
                if (FreeInWorld(parent)) s.Hold(parent);
            }
            if (KinMemory.Ours(f.by))
            {
                ctx.b = f.by;
                s.flags.Add("slayer");
            }
            FollowUps.TakeFlags(s, f, false);
            // His father's people's ways (a tribe's band has no trucks), whoever he rides with now.
            s.flags.Add(f.people.def.techLevel <= TechLevel.Medieval ? "hisLowtech" : "hisHightech");
            var son = MakeSon(s, f, parent);
            if (son == null) return false;
            if (son.Faction != banner) son.SetFaction(banner);
            ctx.stranger = son;
            if (parent != null && son.relations != null && son.relations.DirectRelationExists(PawnRelationDefOf.Parent, parent)) s.flags.Add("kin");
            ctx.a = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (!def.names.NullOrEmpty()) ctx.name = Pick(def.names, s);
            return CastBranch(s, map, f);
        }

        /// <summary>
        /// The son: of his father's people (a fighter for a band), a young man of an age to be his parent's son, with the
        /// family's name where his people go by two, and his parent's son by the game's relations while the game has them.
        /// </summary>
        private Pawn MakeSon(WorldSituation s, FollowUp f, Pawn parent)
        {
            float parentAge = f.whoAge > 0f ? f.whoAge + Mathf.Max(0, Now - f.endTick) / (float)GenDate.TicksPerYear : -1f;
            FollowUpsMath.SonAge(parentAge, out float min, out float max);
            var kind = Fighter ? FighterKind(f.people) : null;
            var son = MakePerson(s, f.people, kind, new FloatRange(min, max), Fighter, Gender.Male);
            if (son == null) return null;
            try
            {
                string last = f.whoLast ?? (parent?.Name as NameTriple)?.Last;
                if (!last.NullOrEmpty() && son.Name is NameTriple nt) son.Name = new NameTriple(nt.First, nt.Nick, last);
                if (parent != null && son.relations != null && parent.relations != null) son.relations.AddDirectRelation(PawnRelationDefOf.Parent, parent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("warlord's son: his family", e);
            }
            return son;
        }

        /// <summary>Of their fighters, the strongest within reason (a war leader's son, not a war machine).</summary>
        private static PawnKindDef FighterKind(Faction f)
        {
            var kinds = f?.def?.pawnGroupMakers?.Where(m => m.kindDef == PawnGroupKindDefOf.Combat && m.options != null).SelectMany(m => m.options).Select(o => o.kind)
                .Where(k => k?.RaceProps?.Humanlike == true).Distinct().ToList() ?? new List<PawnKindDef>();
            return kinds.Where(k => k.combatPower <= 180f).OrderByDescending(k => k.combatPower).FirstOrDefault() ?? kinds.OrderBy(k => k.combatPower).FirstOrDefault();
        }

        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            if (s.ctx.b != null && !KinMemory.Ours(s.ctx.b)) tags.Remove("slayer");
        }

        public override LookTargets StartLook(WorldSituation s)
        {
            var son = Son(s);
            if (son != null && son.Spawned) return new LookTargets(son);
            return base.StartLook(s);
        }

        /// <summary>Lost from the save, dead or taken: the story can't go on as it was.</summary>
        public override string Hourly(WorldSituation s)
        {
            var son = Son(s);
            if (son == null || son.Discarded) return WorldSituations.Quietly;
            if (son.Dead) return def.Ending("slain") != null ? Slain(s, son) : WorldSituations.Quietly;
            if ((son.IsPrisonerOfColony || son.Faction == Faction.OfPlayer) && def.Ending("taken") != null) return "taken";
            return base.Hourly(s);
        }

        /// <summary>Dead: by whose hand, when it was one of the colony's.</summary>
        protected static string Slain(WorldSituation s, Pawn son)
        {
            var killer = Slayer(son);
            if (killer != null && killer.IsColonist)
            {
                s.ctx.b = killer;
                s.flags.AddDistinct("killer");
            }
            return "slain";
        }

        /// <summary>The people the event is about may be at war with the colony (a band): only a people gone ends it.</summary>
        protected override bool StillPossible(WorldSituation s) => s.ctx.faction != null && !s.ctx.faction.defeated;

        /// <summary>He came to the gate and asked (the beat's letter is out).</summary>
        public override void BeatHappened(WorldSituation s, SituationBeat beat, SituationContext ctx) => s.Set("asked");

        /// <summary>Time ran out before he ever came to ask (the world's questions kept him waiting): it ends without a word.</summary>
        public override SituationEnding TimeoutEnding(WorldSituation s) => def.beats.Count > 0 && !s.Has("asked") ? null : base.TimeoutEnding(s);
    }

    /// <summary>
    /// His father slain by the colony: he raises a band among his father's people, a raiders' camp near the home
    /// (World/Region/RaidersCamp.cs, why "son"), hidden until found, that he leads: on its map when the colony attacks it,
    /// at the head of its raids. It ends with him dead ("slain", by whose hand), taken ("taken"), or quietly with the camp
    /// (the camp's own letter tells how it ended).
    /// </summary>
    public class SituationWorker_SonBand : SituationWorker_Son
    {
        protected override bool Fighter => true;

        protected override Faction PeopleFor(Map map, FollowUp f) => WarlordSons.CanCamp(map, f.people) ? f.people : null;

        protected override bool CastBranch(WorldSituation s, Map map, FollowUp f)
        {
            // Last of all: nothing after this can stop the event, so a camp is never set up for a story that doesn't start.
            var c = RaidersCamps.SetUp(map, f.people, s.ctx.stranger, f.whoGender == Gender.Female ? "sonOfMother" : "son");
            return c != null;
        }

        public override string Hourly(WorldSituation s)
        {
            string end = base.Hourly(s);
            if (end != null) return end;
            var son = Son(s);
            var camp = RaidersCamps.CampOf(s.ctx.map);
            // The camp is over and he lived through it: its own letter told how.
            if (camp == null || camp.leader != son) return WorldSituations.Quietly;
            return null;
        }
    }

    /// <summary>
    /// His father captured, handed over or ransomed: he comes to the gate (with one companion, under his people's banner
    /// or the one he rides with now) and asks, by how things stand: for his father, while the colony holds him
    /// (ask:father: let go by the colony's own release, a warden walking him out); to see him, when he is one of the
    /// colony (ask:meet); for someone to lay his token on his father's grave (ask:grave, {A} walks to it); for his
    /// father's weapon from the colony's stores (ask:blade, {ITEM}); or for a peace between his house and the colony
    /// (ask:peace: a promise on the world ledger).
    /// </summary>
    public class SituationWorker_SonGate : SituationWorker_Son
    {
        public const string PeaceOrigin = "warlordson";

        protected override bool CastBranch(WorldSituation s, Map map, FollowUp f)
        {
            var parent = s.ctx.other;
            bool prisoner = parent != null && !parent.Dead && parent.IsPrisonerOfColony && parent.Spawned && Homes.IsHome(parent.Map);
            bool ours = parent != null && KinMemory.Ours(parent) && parent.Spawned && parent.Map == map;
            var grave = parent != null && parent.Dead ? GraveOf(parent) : null;
            var blade = f.thing > 0 ? Blade(f.thing, map) : null;
            var ask = FollowUpsMath.Ask(prisoner, ours, grave != null, blade != null);
            s.flags.Add(FollowUpsMath.AskFlag(ask));
            // What the answer works on: his father walked out to him (DilemmaEffect_KinFree), or the grave (DilemmaEffect_Visit).
            if (ask == SonAsk.Father) s.ctx.place = parent;
            if (ask == SonAsk.Grave) s.ctx.place = grave;
            if (ask == SonAsk.Blade)
            {
                s.ctx.thing = blade.def;
                s.ctx.item = blade.LabelNoCount;
            }
            return true;
        }

        /// <summary>The grave his parent lies in, at one of the homes, or null.</summary>
        private static Building_Grave GraveOf(Pawn dead)
        {
            var corpse = dead.Corpse;
            var grave = corpse?.ParentHolder as Building_Grave;
            return grave != null && grave.Spawned && grave.Map != null && Homes.IsHome(grave.Map) ? grave : null;
        }

        /// <summary>His parent's weapon (by its thing id) at this home, lying in the colony's stores or on its ground; null when it's gone or carried.</summary>
        private static Thing Blade(int id, Map map)
        {
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
                if (t != null && t.thingIDNumber == id && t.Spawned && !t.IsForbidden(Faction.OfPlayer))
                    return t;
            return null;
        }

        /// <summary>The beat's {A}: his parent, when they are one of the colony and he asked to see them.</summary>
        public override bool CastBeat(WorldSituation s, SituationBeat beat, SituationContext ctx, Map map)
        {
            if (beat.key == "ask" && s.Has("ask:meet"))
            {
                var parent = s.ctx.other;
                if (!KinMemory.Ours(parent) || !parent.Spawned || parent.Map != map || parent.Downed) return false;
                ctx.a = parent;
                return true;
            }
            return base.CastBeat(s, beat, ctx, map);
        }

        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            if (!base.BeatCanHappen(s, beat, map, out retry)) return false;
            if (beat.key != "ask") return true;
            var parent = s.ctx.other;
            // What he came for is still here to ask about.
            if (s.Has("ask:father") && (parent == null || parent.Dead || !parent.IsPrisonerOfColony)) return Switch(s, "ask:peace");
            if (s.Has("ask:meet") && (!KinMemory.Ours(parent) || !parent.Spawned || parent.Map != map || parent.Downed)) return Switch(s, "ask:peace");
            if (s.Has("ask:grave") && (s.ctx.place == null || s.ctx.place.Destroyed || !s.ctx.place.Spawned)) return Switch(s, "ask:peace");
            if (s.Has("ask:blade") && (s.ctx.thing == null || VarietyUtil.InStores(map, s.ctx.thing) < 1)) return Switch(s, "ask:peace");
            return true;
        }

        private static bool Switch(WorldSituation s, string to)
        {
            foreach (var a in new[] { "ask:father", "ask:meet", "ask:grave", "ask:blade", "ask:peace" }) s.Clear(a);
            s.Set(to);
            return true;
        }

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            var o = option < beat.options.Count ? beat.options[option] : null;
            if (beat.key != "ask" || o?.flags == null || byDefault) return outcome;
            // His father's people remember it, and who of the colony did it (the release itself is the game's to count).
            var people = s.Has("among") ? s.ctx.enemy : s.ctx.faction;
            string deed = o.flags.Contains("peace") ? "madePeace" : o.flags.Contains("grave") ? "mourned" : o.flags.Contains("blade") ? "gaveGift" : null;
            if (deed != null && people != null) KinMemory.Note(people, deed, ctx.a, Son(s)?.LabelShort);
            if (o.flags.Contains("peace"))
            {
                try
                {
                    // His word on the world ledger: his house will not take up his father's quarrel with the colony.
                    var son = Son(s);
                    if (son != null)
                        WorldLedger.AddTie(Tie.Kind.Promise, WorldLedger.PersonOf(son), Refs.Us,
                            new TieTerms { key = PeaceOrigin, about = s.ctx.other != null ? WorldLedger.PersonOf(s.ctx.other) : Ref.None },
                            Find.TickManager.TicksGame + GenDate.TicksPerYear * 2, PeaceOrigin);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("warlord's son: his word", e);
                }
            }
            return outcome;
        }
    }

    /// <summary>
    /// His father hunted down by others ({ENEMY}, who hunt him now): he comes to the gate an exile, with one companion,
    /// asking shelter: taken in (they join; if his hunters are at war with the colony they may come for him), a few
    /// nights' rest, or turned away.
    /// </summary>
    public class SituationWorker_SonExile : SituationWorker_Son
    {
        /// <summary>Turned out by his own people, he rides with another people at peace with the colony when there is one.</summary>
        protected override Faction PeopleFor(Map map, FollowUp f) => WarlordSons.Banner(map, f, true);

        protected override bool CastBranch(WorldSituation s, Map map, FollowUp f)
        {
            if (f.other != null && !f.other.defeated && f.other != s.ctx.faction)
            {
                s.ctx.enemy = f.other;
                s.flags.Add("hunted");
                if (Raids.CanRaid(f.other)) s.flags.Add("huntersAtWar");
            }
            return true;
        }

        /// <summary>
        /// Taken in while his hunters are at war with the colony: three times in ten they come for him some days later (a
        /// raid of theirs with its own letter), at a little under half a raid's strength.
        /// </summary>
        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            var o = option < beat.options.Count ? beat.options[option] : null;
            if (o?.flags == null || !o.flags.Contains("takenIn")) return outcome;
            var hunters = s.ctx.enemy;
            var tracker = CouncilGameComponent.Instance?.happenings;
            if (hunters == null || tracker == null || ctx.map == null || !Raids.CanRaid(hunters) || !Rand.Chance(0.3f)) return outcome;
            var son = Son(s);
            string label = "CC_Son_HuntersLabel".Translate(hunters.Name.Named("ENEMY")).Resolve();
            string text = "CC_Son_HuntersText".Translate(hunters.Name.Named("ENEMY"), (son?.LabelShort ?? "?").Named("SON"), (ctx.dead ?? "").Named("DEAD")).Resolve();
            tracker.AddRaid(ctx.map, hunters, Rand.Range(0.4f, 0.55f), Rand.Range(48f, 120f), label, text);
            return outcome;
        }
    }

    /// <summary>
    /// His father's band broken up, or gone with his people: a young man with a few riders, looking for work. Taken on
    /// (they join), given a meal and the road, or sent on.
    /// </summary>
    public class SituationWorker_SonRiders : SituationWorker_Son
    {
        protected override bool Fighter => true;
    }
}
