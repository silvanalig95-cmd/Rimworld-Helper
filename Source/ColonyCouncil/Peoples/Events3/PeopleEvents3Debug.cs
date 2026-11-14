using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 3 ("Rimfolk", "Peoples' events 3: ..."). What the record keeps; one of their
    /// camps here now (the road gang, the field sickhouse, the spring calving), whatever its own trigger; the hospitallers'
    /// litters for the raiders lying here now; the herders' riders on a thief's trail now; a patient for the sickhouse now;
    /// the sick room's, the drove's days over now; the calving's hard birth now; a pilgrim at the shrine now; the shrine worn
    /// to ruin now. The events themselves are fired with the game's own incident tool (Rimfolk_Evt_RedInk and the rest).
    /// </summary>
    public static class PeopleEvents3Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 3: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events 3: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 3: ");
            sb.AppendLine($"order carried {b.carried}, saw kept {b.kept}; sick room map {b.sickRoomMap} until {b.sickRoomUntil}; two faiths settled until {b.settledUntil}, table play {b.tablePlay} ({(b.tableSplit ? "split" : "settle")}).");
            foreach (var r in b.redInk)
                sb.AppendLine($"  red ink {r.id}: {r.league?.Name ?? "?"} for {r.wronged?.Name ?? "?"}, deed {r.deed} ({r.kind}), owed {r.owed}, {r.state}{(r.done ? ", done" : "")}.");
            foreach (var r in b.shrines)
                sb.AppendLine($"  shrine of {r.faction?.Name ?? "?"} at {r.cell} by the {r.holy} ({r.holyName}), {(r.shrine != null ? r.shrine.HitPoints + "/" + r.shrine.MaxHitPoints : "gone")}, last stop {r.lastStop}{(r.ruined ? ", ruined" : "")}{(r.gone ? ", gone" : "")}.");
            foreach (var r in b.thefts)
                sb.AppendLine($"  theft {r.id} by {r.thieves?.Name ?? "?"}: {r.animals.Count} beasts ({ThiefTrail.Kinds(r.animals)}), {r.stage}, due {r.due}, site {r.site}{(r.done ? ", done" : "")}.");
            var visits = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && v.kind != null && v.kind.StartsWith("pev:") && !v.done).ToList();
            if (visits != null)
                foreach (var v in visits)
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, stage {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            var map = Find.CurrentMap;
            if (map != null)
            {
                OldShrine.Holy(map, out string kind, out string name, out var at);
                sb.AppendLine($"  here: the new road {RoadGang.NewRoad() ?? "not running"}; something holy {(at.IsValid ? kind + " (" + name + ") at " + at : "none")}; downed raiders {Litters.Downed(map).Count}; "
                              + $"spreading sick {SickRoom.Sick(map).Count}, a sick room {SickRoom.RoomFor(map)?.Role?.label ?? "none"}; the sickhouse's reason {FieldSickhouse.Why(out _) ?? "none"}; "
                              + $"the palmer's reason {TwoFaiths.Why(map) ?? "none"}.");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 3: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_RoadGang", "Rimfolk_Camp_FieldSickhouse", "Rimfolk_Camp_SpringCalving" })
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(n);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.label, () =>
                {
                    var f = Arrivals.Friendly(x => kind.FitsFaction(x));
                    if (f == null || !CampTracker.Enabled)
                    {
                        Say("no camp: " + (f == null ? "no people of that calling at peace" : "camps are off"));
                        return;
                    }
                    var ctx = new HappeningContext { map = map, faction = f };
                    var c = n == "Rimfolk_Camp_RoadGang" ? RoadGang.Start(ctx, false, false) : n == "Rimfolk_Camp_FieldSickhouse" ? FieldSickhouse.Start(ctx, false) : SpringCalving.Start(ctx);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: no spot or way for it here.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events 3: the order's litters for those lying here now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLitters()
        {
            var map = Find.CurrentMap;
            if (Litters.Downed(map).Count == 0)
            {
                Say("no raider lies downed here.");
                return;
            }
            Litters.Soon(map);
            Say("the hospitallers come within the hour, if the fight is over and the order is at peace.");
        }

        [DebugAction("Rimfolk", "Peoples' events 3: the herders' riders on a thief's trail now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTrail() =>
            Say(ThiefTrail.Soon(Find.CurrentMap) ? "the riders come within the hour, between six and ten in the morning (a beast of the colony's taken, when none was)." : "no beast to take and no theft open.");

        [DebugAction("Rimfolk", "Peoples' events 3: a patient for the field sickhouse now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPatient() => Say(FieldSickhouse.PatientNow(Find.CurrentMap) ? "a stranger comes within the hour, by day." : "no field sickhouse on this map.");

        [DebugAction("Rimfolk", "Peoples' events 3: the sick room's, the drove's days over now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDays()
        {
            bool room = SickRoom.EndNow(), drove = Drove.OnNow();
            Say(room || drove ? (room ? "the sick room ends within the hour. " : "") + (drove ? "the drove moves on within the hour." : "") : "no sick room and no drove.");
        }

        [DebugAction("Rimfolk", "Peoples' events 3: the calving's hard birth now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHardBirth()
        {
            var c = SpringCalving.On(Find.CurrentMap);
            var st = SpringCalving.StateOf(c);
            var mother = st?.inCalf.FirstOrDefault(a => a != null && !a.Dead && SpringCalving.Pregnancy(a) != null);
            if (mother == null)
            {
                Say("no calving camp with a beast in calf here.");
                return;
            }
            st.hard = mother;
            st.hardDone = st.hardAsked = false;
            st.hardSince = -1;
            SpringCalving.Pregnancy(mother).Severity = PeopleEvents3Math.HardHeld;
            Say("the birth goes hard within the hour; a herder comes to the gate.");
        }

        [DebugAction("Rimfolk", "Peoples' events 3: a pilgrim at the shrine now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStop() => Say(OldShrine.StopNow(Find.CurrentMap) ? "a pilgrim walks in to the shrine." : "no shrine here, or no pilgrims at peace.");

        [DebugAction("Rimfolk", "Peoples' events 3: the shrine worn to ruin now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRuin()
        {
            var r = OldShrine.On(Find.CurrentMap);
            if (r?.shrine == null || !r.shrine.Spawned)
            {
                Say("no shrine here.");
                return;
            }
            r.shrine.HitPoints = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.FloorToInt(r.shrine.MaxHitPoints * PeopleEvents3Math.RuinBelow) - 1);
            Say("worn below a third: the fellowship mourns it within the hour; mend it above three fifths to keep it again.");
        }
    }
}
