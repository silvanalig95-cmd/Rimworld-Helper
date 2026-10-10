using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A small blueprint a colonist placed in their own room.</summary>
    public class PersonalTouch : IExposable
    {
        public Pawn pawn;
        public ThingDef def;
        public IntVec3 cell;
        public Map map;
        public int createdTick;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref createdTick, "createdTick");
        }
    }

    /// <summary>
    /// Now and then a colonist plans a small personal touch for their own bedroom (a plant pot, an end
    /// table, a lamp). It's an ordinary blueprint: cancel it if you don't like it. When it's built, they're
    /// pleased.
    /// </summary>
    public class PersonalTouchTracker : IExposable
    {
        public List<PersonalTouch> pending = new List<PersonalTouch>();
        public int nextTick = -1;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref pending, "pending", LookMode.Deep);
            Scribe_Values.Look(ref nextTick, "nextTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pending == null) pending = new List<PersonalTouch>();
                pending.RemoveAll(p => p == null || p.pawn == null || p.def == null || p.map == null);
            }
        }

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        public void HourlyTick(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            CheckPending(comp, now);
            if (nextTick < 0) nextTick = now + Mathf.RoundToInt(Settings.daysBetweenPersonalTouches * GenDate.TicksPerDay * Rand.Range(0.5f, 1f));
            if (now < nextTick) return;
            nextTick = now + Mathf.RoundToInt(Settings.daysBetweenPersonalTouches * GenDate.TicksPerDay * Rand.Range(0.6f, 1.4f));
            TryNewTouch(comp);
        }

        private void CheckPending(CouncilGameComponent comp, int now)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var t = pending[i];
                if (t.pawn == null || t.pawn.Dead || t.map == null || !Find.Maps.Contains(t.map))
                {
                    pending.RemoveAt(i);
                    continue;
                }
                var built = t.cell.GetThingList(t.map).FirstOrDefault(x => x.def == t.def);
                if (built != null)
                {
                    pending.RemoveAt(i);
                    CouncilGameComponent.GiveThought(t.pawn, CouncilDefOf.Council_PersonalTouchDone);
                    string line = Line("CC_TouchDone_", t.pawn, t.def);
                    CouncilGameComponent.Speak(t.pawn, line, new Color(0.6f, 1f, 0.6f));
                    comp.AddChronicle(new ChronicleEntry(ChronicleKind.PersonalTouch,
                        "CC_Chronicle_TouchDone".Translate(t.pawn.LabelShort, t.def.label).Resolve(), t.pawn));
                    continue;
                }
                bool stillPlanned = t.cell.GetThingList(t.map).Any(x => (x is Blueprint || x is Frame) && x.def.entityDefToBuild == t.def);
                if (!stillPlanned || now - t.createdTick > GenDate.TicksPerDay * 20) pending.RemoveAt(i);
            }
        }

        public bool TryNewTouch(CouncilGameComponent comp, Pawn only = null)
        {
            var candidates = PawnsFinder.AllMaps_FreeColonistsSpawned
                .Where(p => (only == null || p == only) && CouncilGameComponent.CanVoiceWishes(p) && RoomUtil.PrivateRoom(p) != null
                            && !(p.story?.traits?.HasTrait(TraitDefOf.Ascetic) ?? false) && pending.All(t => t.pawn != p))
                .ToList();
            if (candidates.Count == 0) return false;
            var pawn = candidates.RandomElementByWeight(Weight);
            var room = RoomUtil.PrivateRoom(pawn);
            var map = pawn.Map;

            foreach (var def in DecorOptions(pawn, map, room))
            {
                if (!Avail.MaterialsWithinReach(def, map)) continue;
                var stuff = def.MadeFromStuff ? Avail.BestStuff(def, map) : null;
                if (!FindCell(room, map, def, stuff, out var cell, out var rot)) continue;
                GenConstruct.PlaceBlueprintForBuild(def, cell, map, rot, Faction.OfPlayer, stuff);
                pending.Add(new PersonalTouch { pawn = pawn, def = def, cell = cell, map = map, createdTick = Find.TickManager.TicksGame });
                string line = Line("CC_Touch_", pawn, def);
                Messages.Message("CC_Touch_Message".Translate(pawn.Named("PAWN"), Avail.Indefinite(def).Named("THING")).Resolve() + " \"" + line + "\"",
                    new LookTargets(new TargetInfo(cell, map)), MessageTypeDefOf.NeutralEvent, false);
                CouncilGameComponent.Speak(pawn, line, new Color(1f, 0.92f, 0.6f));
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.PersonalTouch,
                    "CC_Chronicle_Touch".Translate(pawn.LabelShort, def.label).Resolve(), pawn));
                return true;
            }
            return false;
        }

        private static float Weight(Pawn p)
        {
            float w = 1f;
            var traits = p.story?.traits;
            if (traits != null && traits.HasTrait(TraitDefOf.Greedy)) w += 1f;
            var art = p.skills?.GetSkill(SkillDefOf.Artistic);
            if (art != null && art.passion != Passion.None) w += 0.5f;
            var cons = p.skills?.GetSkill(SkillDefOf.Construction);
            if (cons != null && cons.passion != Passion.None) w += 0.5f;
            return w;
        }

        private static IEnumerable<ThingDef> DecorOptions(Pawn pawn, Map map, Room room)
        {
            var options = new List<(ThingDef def, float w)>();
            void Add(Func<ThingDef, bool> pred, float w, params string[] preferred)
            {
                if (RoomUtil.RoomContains(room, pred)) return;
                var d = Avail.BestBuildable(map, x => pred(x) && x.size.x <= 2 && x.size.z <= 1, preferred);
                if (d != null) options.Add((d, w));
            }
            var plants = pawn.skills?.GetSkill(SkillDefOf.Plants);
            Add(Avail.IsPlantPot, plants != null && plants.passion != Passion.None ? 3f : 1f, "PlantPot");
            Add(Avail.IsEndTable, 1.5f, "EndTable");
            Add(Avail.IsDresser, 1f, "Dresser");
            var nightOwl = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            bool owl = nightOwl != null && (pawn.story?.traits?.HasTrait(nightOwl) ?? false);
            Add(Avail.IsLight, owl ? 3f : 0.8f, "StandingLamp", "TorchLamp");
            while (options.Count > 0)
            {
                var pick = options.RandomElementByWeight(o => o.w);
                options.Remove(pick);
                yield return pick.def;
            }
        }

        /// <summary>A free cell along a wall, away from the door, that doesn't block anything.</summary>
        public static bool FindCell(Room room, Map map, ThingDef def, ThingDef stuff, out IntVec3 cell, out Rot4 rot)
        {
            cell = IntVec3.Invalid;
            rot = Rot4.North;
            var cells = room.Cells.InRandomOrder().ToList();
            foreach (var c in cells)
            {
                if (!c.Standable(map) || c.GetEdifice(map) != null) continue;
                if (c.GetThingList(map).Any(t => t is Blueprint || t is Frame || t is Pawn)) continue;
                bool nearDoor = false, nearWall = false;
                foreach (var adj in GenAdj.CardinalDirections)
                {
                    var n = c + adj;
                    if (!n.InBounds(map)) continue;
                    var ed = n.GetEdifice(map);
                    if (ed != null && ed.def.IsDoor) nearDoor = true;
                    if (ed != null && TemplatePlanner.IsWallLike(ed.def)) nearWall = true;
                }
                if (nearDoor || !nearWall) continue;
                foreach (var r in new[] { Rot4.North, Rot4.East, Rot4.South, Rot4.West })
                {
                    if (!GenConstruct.CanPlaceBlueprintAt(def, c, r, map, false, null, null, stuff).Accepted) continue;
                    bool ok = true;
                    foreach (var oc in GenAdj.OccupiedRect(c, r, def.size))
                    {
                        if (!room.ContainsCell(oc) || oc.GetEdifice(map) != null) ok = false;
                    }
                    if (!ok) continue;
                    cell = c;
                    rot = r;
                    return true;
                }
            }
            return false;
        }

        private static string Line(string prefix, Pawn pawn, ThingDef def)
        {
            var tone = ToneUtility.ToneFor(pawn);
            string key = prefix + tone;
            if (!key.CanTranslate()) key = prefix + "Plain";
            return key.Translate(pawn.Named("PAWN"), Avail.Indefinite(def).Named("THING"), def.label.Named("THINGBARE")).Resolve();
        }
    }
}
