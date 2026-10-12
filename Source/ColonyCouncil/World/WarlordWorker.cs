using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A warlord rises among a hostile people ({FACTION}): a named leader ({X}) with a warband ({NAME}), kept in the
    /// world like a nemesis. While he rises his people's raids hit harder and come more often, he leads some of them,
    /// and his band raids the colony now and then. It never needs the colony to storm his town to end:
    /// <list type="bullet">
    /// <item>Neighbors at war with his people ({ENEMY} and the rest) may bring him down off-screen, a little likelier
    /// every day, with the neighbors' strength, a bounty or the colony's gifts to the hunters ("hunted", "bounty").</item>
    /// <item>The colony can pay him off (a truce that may not hold), post a price on his head, send silver to the
    /// hunters, ask an ally to march ("allies", or a defeat that costs them), or wait him out: a band that gets no
    /// plunder breaks up ("brokeup", also when time runs out).</item>
    /// <item>Killed at home ("slain") or taken alive ("captured"; or handed to his enemies, or ransomed home).</item>
    /// </list>
    /// Flags: hunters (someone hunts him), ally (an ally could march, set when the question comes), truce, broken,
    /// bounty, gifts, allies, alliesLost, captured, led (he led a raid here), takenAlive (how he was brought down),
    /// slayer and revenge (who killed him at home). Numbers: repelled (his people's raids beaten off), raidOn.
    /// Text keys (SituationText): nicknames, extra, raidLabels, raidTexts, brokenLabels, brokenTexts, raidNote.
    /// </summary>
    public class WarlordWorker : WorldSituationWorker
    {
        /// <summary>Share of the storyteller's raids (from whoever it would have picked) that come from his people instead.</summary>
        public const float RaidShare = 0.3f;
        private const float PointsFactor = 1.2f;

        private static Pawn Warlord(WorldSituation s) => s.ctx.stranger;

        /// <summary>He rises, his band raids, his people's raids are his: not once he's paid off or in our cells.</summary>
        private static bool Rising(WorldSituation s) => !s.Has("truce") && !s.Has("captured");

        /// <summary>Free and out in the world (where the hunters can reach him and his raids can bring him).</summary>
        private static bool Loose(Pawn x) =>
            x != null && !x.Dead && !x.Destroyed && !x.Spawned && !x.IsPrisoner && Find.WorldPawns.Contains(x) && !People.Kidnapped(x)
            && x.GetCaravan() == null && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x);

        /// <summary>Peoples at war with his, with towns of their own: the ones who hunt him.</summary>
        public static List<Faction> Hunters(WorldSituation s) =>
            Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def.humanlikeFaction
                                                                     && f != s.ctx.faction && s.ctx.faction != null && f.HostileTo(s.ctx.faction)
                                                                     && TownsOf(f, s.ctx.map).Any()).ToList();

        /// <summary>An ally of the colony who could march against him: at war with his people and able to fight.</summary>
        private static Faction Ally(WorldSituation s) =>
            Hunters(s).Where(f => f.PlayerRelationKind == FactionRelationKind.Ally && f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat))
                .OrderByDescending(f => TownsOf(f, s.ctx.map).Count()).FirstOrDefault();

        // ---------------- Who ----------------

        public override bool Cast(WorldSituation s, Map map)
        {
            if (!base.Cast(s, map)) return false;
            var f = s.ctx.faction;
            var kind = Kind(f);
            var x = MakePerson(s, f, kind, new FloatRange(30f, 55f), true);
            if (x == null) return false;
            string nick = Text("nicknames", s);
            if (!nick.NullOrEmpty() && x.Name is NameTriple nt) x.Name = new NameTriple(nt.First, nick.Trim(), nt.Last);
            s.ctx.stranger = x;
            // His seat: his people's town nearest the colony.
            s.ctx.placeName = NearestTown(f, map)?.Name ?? f.Name;
            // Who hunts him: a neighbor at peace with the colony, if there is one (the one the colony can help).
            var hunters = Hunters(s);
            s.ctx.enemy = hunters.Where(h => !h.HostileTo(Faction.OfPlayer)).OrderBy(h => Distance(map, NearestTown(h, map))).FirstOrDefault()
                          ?? hunters.OrderBy(h => Distance(map, NearestTown(h, map))).FirstOrDefault();
            if (s.ctx.enemy != null && !s.ctx.enemy.HostileTo(Faction.OfPlayer)) s.flags.Add("hunters");
            return true;
        }

        /// <summary>The strongest of his people's fighters (within reason: a war chief, not a war machine).</summary>
        private static PawnKindDef Kind(Faction f)
        {
            var kinds = f.def.pawnGroupMakers?.Where(m => m.kindDef == PawnGroupKindDefOf.Combat && m.options != null).SelectMany(m => m.options).Select(o => o.kind)
                .Where(k => k?.RaceProps?.Humanlike == true).Distinct().ToList() ?? new List<PawnKindDef>();
            return kinds.Where(k => k.combatPower <= 250f).OrderByDescending(k => k.combatPower).FirstOrDefault()
                   ?? kinds.OrderBy(k => k.combatPower).FirstOrDefault() ?? Arrivals.PersonKind(f);
        }

        protected override void AddTags(WorldSituation s, HashSet<string> tags)
        {
            if (s.ctx.enemy == null) tags.Add("nohunters");
            if (s.Num("repelled") > 0) tags.Add("repelled");
            if (s.ctx.other != null) tags.Add("named");
            var x = Warlord(s);
            if (x != null && x.gender == Gender.Female) tags.Add("female");
        }

        public override LookTargets StartLook(WorldSituation s)
        {
            var town = s.ctx.faction != null ? NearestTown(s.ctx.faction, s.ctx.map) : null;
            return town != null ? new LookTargets(town) : LookTargets.Invalid;
        }

        // ---------------- While he rises ----------------

        public override float RaidPointsFactor(WorldSituation s, Faction f) => f != null && f == s.ctx.faction && Rising(s) ? PointsFactor : 1f;

        public override Faction RaidFaction(WorldSituation s) => Rising(s) && s.ctx.faction != null && !s.ctx.faction.defeated ? s.ctx.faction : null;

        public override string Hourly(WorldSituation s)
        {
            var x = Warlord(s);
            var f = s.ctx.faction;
            if (x == null || x.Discarded || x.Destroyed && !x.Dead) return "vanished";
            if (x.Dead)
            {
                var slayer = Slayer(x);
                if (slayer != null && slayer.IsColonist)
                {
                    s.ctx.b = slayer;
                    s.flags.Add("slayer");
                    if (s.sufferers.Contains(slayer)) s.flags.Add("revenge");
                }
                if (s.Has("captured")) s.flags.Add("diedCaptive");
                return "slain";
            }
            if (x.Faction == Faction.OfPlayer && !x.IsPrisoner) return "captured";
            if (x.IsPrisonerOfColony)
            {
                if (!s.Has("captured"))
                {
                    s.Set("captured");
                    WorldSituations.Instance?.Call(s, "captive", 0.25f);
                }
                return null;
            }
            // He got out of our hands: he rises again.
            if (s.Has("captured")) s.Clear("captured");
            if (f == null || f.defeated) return "gone";
            if (!f.HostileTo(Faction.OfPlayer)) return "peace";
            if (x.Faction != f) return "brokeup";
            if (!x.Spawned && Find.WorldPawns.Contains(x)) Find.WorldPawns.ForcefullyKeptPawns.Add(x);
            if (x.Spawned && x.Map.IsPlayerHome) s.flags.AddDistinct("led");
            WatchRaids(s);
            return null;
        }

        /// <summary>His people's raids on the colony: who they hurt, and whether the colony beat them off.</summary>
        private static void WatchRaids(WorldSituation s)
        {
            var f = s.ctx.faction;
            bool on = false;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                if (!map.mapPawns.AllPawnsSpawned.Any(p => p.Faction == f && p.RaceProps.Humanlike && !p.Downed && !p.IsPrisoner && p.HostileTo(Faction.OfPlayer))) continue;
                on = true;
                int now = Find.TickManager.TicksGame;
                foreach (var c in map.mapPawns.FreeColonistsSpawned)
                    if (now - (c.mindState?.lastHarmTick ?? -999999) < GenDate.TicksPerHour * 2 && !s.sufferers.Contains(c) && People.HasInnerLife(c))
                        s.sufferers.Add(c);
            }
            if (on) s.SetNum("raidOn", 1);
            else if (s.Num("raidOn") == 1)
            {
                s.SetNum("raidOn", 0);
                s.AddNum("repelled");
            }
        }

        public override string Daily(WorldSituation s)
        {
            var x = Warlord(s);
            if (s.Has("captured") || !Loose(x)) return null;
            // The neighbors close in.
            if (Rand.Chance(HuntChance(s)))
            {
                bool bounty = s.Has("bounty") && (Hunters(s).Count == 0 || Rand.Chance(0.5f));
                return Brought(s, bounty ? "bounty" : "hunted");
            }
            // A band that gets nothing out of its raids doesn't stay a band for long.
            if (s.DaysIn > s.Duration / (float)GenDate.TicksPerDay * 0.5f)
            {
                float p = 0.02f + 0.04f * s.Num("repelled") + (s.Has("truce") ? 0.04f : 0f);
                if (Rand.Chance(p)) return "brokeup";
            }
            return null;
        }

        /// <summary>The daily chance that the neighbors (or a bounty hunter) bring him down: it grows with time and their strength.</summary>
        public static float HuntChance(WorldSituation s)
        {
            float days = s.DaysIn;
            if (days < 4f) return 0f;
            var hunters = Hunters(s);
            float p = 0f;
            if (hunters.Count > 0)
            {
                float theirs = hunters.Sum(h => Mathf.Min(6, TownsOf(h, s.ctx.map).Count()) * Mathf.Lerp(0.8f, 1.3f, Mathf.InverseLerp(1f, 5f, (int)h.def.techLevel)));
                float his = Mathf.Max(1f, TownsOf(s.ctx.faction, s.ctx.map).Count()) * 1.5f;
                p = 0.0025f * (days - 3f) * Mathf.Clamp(theirs / his, 0.4f, 2f);
            }
            if (s.Has("bounty")) p += 0.03f;
            p += 0.015f * Mathf.Min(3, s.Num("gifts"));
            return Mathf.Clamp(p, 0f, 0.25f);
        }

        /// <summary>He's brought down out in the world: where, and whether alive (the letter says which).</summary>
        private string Brought(WorldSituation s, string ending)
        {
            var hunter = s.ctx.enemy != null && !s.ctx.enemy.defeated ? s.ctx.enemy : Hunters(s).RandomElementWithFallback();
            if (hunter != null) s.ctx.enemy = hunter;
            // Somewhere between his seat and theirs.
            var town = hunter != null ? NearestTown(hunter, s.ctx.map) : null;
            if (town != null) s.ctx.placeName = town.Name;
            if (Rand.Chance(0.4f)) s.flags.AddDistinct("takenAlive");
            if (ending == "bounty")
            {
                // A name for the one who collects it, and the town they work out of.
                var anyone = Arrivals.Friendly(f => f != s.ctx.faction && TownsOf(f, s.ctx.map).Any());
                var p = anyone != null ? MakePerson(s, anyone, null, new FloatRange(25f, 50f), true) : null;
                if (p != null) s.ctx.other = p;
                var home = anyone != null ? NearestTown(anyone, s.ctx.map) : null;
                if (home != null) s.ctx.placeName = home.Name;
            }
            return ending;
        }

        // ---------------- The questions ----------------

        public override bool BeatCanHappen(WorldSituation s, SituationBeat beat, Map map, out bool retry)
        {
            if (!base.BeatCanHappen(s, beat, map, out retry)) return false;
            var x = Warlord(s);
            switch (beat.key)
            {
                // His band comes, or he breaks the truce: only while he's free.
                case "raid":
                case "broken":
                    if (!Loose(x))
                    {
                        retry = x != null && x.Spawned;
                        return false;
                    }
                    return Raids.CanRaid(s.ctx.faction);
                // The allies march: not while he's here at the colony.
                case "battle":
                    if (x == null || x.Dead) return false;
                    if (!Loose(x))
                    {
                        retry = true;
                        return false;
                    }
                    return true;
                case "captive":
                    return x != null && x.IsPrisonerOfColony;
                default:
                    return true;
            }
        }

        public override bool PrepareBeat(WorldSituation s, SituationBeat beat, SituationContext ctx)
        {
            var map = ctx.map;
            switch (beat.key)
            {
                case "raid":
                    SendRaid(s, ctx, "raidLabels", "raidTexts", Rand.Range(0.85f, 1.05f));
                    return true;
                case "broken":
                    s.Clear("truce");
                    s.Set("broken");
                    SendRaid(s, ctx, "brokenLabels", "brokenTexts", Rand.Range(0.9f, 1.1f), true);
                    return true;
                case "terms":
                    // His price, a bounty (a little over half), and a gift to the hunters (a third or so).
                    ctx.count = Silver(map, new IntRange(900, 1500));
                    ctx.item = WorldEffect_Pay.Amount(ctx.count, 0.6f).ToString();
                    ctx.days = WorldEffect_Pay.Amount(ctx.count, 0.35f).ToString();
                    var ally = Ally(s);
                    if (ally != null)
                    {
                        ctx.enemy = ally;
                        s.Set("ally");
                        s.Set("hunters");
                    }
                    else s.Clear("ally");
                    return true;
                case "battle":
                    {
                        var marching = s.ctx.enemy;
                        if (marching == null || marching.defeated) return false;
                        float chance = BattleOdds(s, marching);
                        if (Rand.Chance(chance))
                        {
                            if (Rand.Chance(0.4f)) s.flags.AddDistinct("takenAlive");
                            var town = NearestTown(marching, map);
                            if (town != null) s.ctx.placeName = town.Name;
                            s.pendingEnding = "allies";
                            s.pendingTick = Find.TickManager.TicksGame;
                            return false;
                        }
                        s.Set("alliesLost");
                        Goodwill(marching, -5, false);
                        return true;
                    }
                case "captive":
                    ctx.count = Silver(map, new IntRange(500, 1000));
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>How likely the allies are to win: their towns and era against his.</summary>
        private static float BattleOdds(WorldSituation s, Faction ally)
        {
            int theirs = TownsOf(ally, s.ctx.map).Count();
            int his = TownsOf(s.ctx.faction, s.ctx.map).Count();
            float p = 0.55f + 0.06f * Mathf.Clamp(theirs - his, -4, 4) + 0.1f * Mathf.Clamp((int)ally.def.techLevel - (int)s.ctx.faction.def.techLevel, -2, 2);
            if (s.Has("gifts")) p += 0.05f;
            return Mathf.Clamp(p, 0.3f, 0.85f);
        }

        /// <summary>His band comes for the colony (a share of the storyteller's strength), and sometimes he rides with it.</summary>
        private void SendRaid(WorldSituation s, SituationContext ctx, string labelKey, string textKey, float factor, bool leads = false)
        {
            var tracker = CouncilGameComponent.Instance?.happenings;
            var x = Warlord(s);
            if (tracker == null || ctx.map == null || !Raids.CanRaid(s.ctx.faction)) return;
            bool lead = leads || Rand.Chance(0.45f);
            var tags = Tags(s);
            if (lead) tags.Add("leads");
            var label = Flavor.Pick(def.GetModExtension<SituationText>()?.Get(labelKey), tags);
            var text = Flavor.Pick(def.GetModExtension<SituationText>()?.Get(textKey), tags);
            tracker.AddRaid(ctx.map, s.ctx.faction, factor, Rand.Range(2f, 6f), Sentences(ctx.Format(label ?? "")), Sentences(ctx.Format(text ?? "")), lead ? x : null);
            if (lead) s.flags.AddDistinct("led");
        }

        public override string Unavailable(WorldSituation s, SituationContext ctx, SituationBeat beat, int option)
        {
            if (beat.key == "captive" && Warlord(s)?.IsPrisonerOfColony != true) return "CC_WS_NoLongerCaptive".Translate(Warlord(s)?.LabelShort ?? "?");
            return null;
        }

        public override string Chosen(WorldSituation s, SituationContext ctx, SituationBeat beat, int option, bool risk, bool byDefault, string outcome)
        {
            if (beat.key == "captive" && option < beat.options.Count && beat.options[option].end == "handedOver" && ctx.enemy != null)
            {
                // Tried in the hunters' own town.
                var town = NearestTown(ctx.enemy, s.ctx.map);
                if (town != null) s.ctx.placeName = town.Name;
            }
            if (beat.key == "terms")
            {
                var o = beat.options[option];
                if (o.flags != null && o.flags.Contains("gifts")) s.AddNum("gifts");
                // The ally asked is the one who marches.
                if (o.flags != null && o.flags.Contains("allies") && ctx.enemy != null) s.ctx.enemy = ctx.enemy;
            }
            return outcome;
        }

        // ---------------- How it ends ----------------

        public override void Ended(WorldSituation s, SituationEnding ending)
        {
            var x = Warlord(s);
            var comp = CouncilGameComponent.Instance;
            string key = s.endingKey;
            if (x == null || x.Dead) return;
            string how = ending != null ? Flavor.Strip(Line(ending.texts, s) ?? "") : null;
            switch (key)
            {
                case "hunted":
                case "bounty":
                case "allies":
                case "handedOver":
                    comp?.nemeses.Settle(x, FirstLine(how));
                    if (s.Has("takenAlive") || key == "handedOver") Vanish(x);
                    else if (!DieOffscreen(x)) Vanish(x);
                    break;
                case "ransomed":
                    comp?.nemeses.Settle(x, FirstLine(how));
                    SendHome(x);
                    break;
                case "brokeup":
                case "gone":
                case "peace":
                    // Gone over the hills. Someone's nemesis stays out there; anyone else is gone for good.
                    if (!(comp?.nemeses.IsNemesis(x) ?? false) && !x.Spawned && !x.IsPrisoner) Vanish(x);
                    break;
            }
        }

        private static string FirstLine(string text)
        {
            if (text.NullOrEmpty()) return null;
            int cut = text.IndexOf('.');
            return cut > 0 ? text.Substring(0, cut + 1) : text;
        }

        /// <summary>His people paid: he walks out of the cell and goes home (and may come back one day).</summary>
        private static void SendHome(Pawn x)
        {
            try
            {
                if (x.guest != null && x.IsPrisoner) x.guest.SetGuestStatus(null);
                if (x.Spawned) x.DeSpawn();
                if (!Find.WorldPawns.Contains(x)) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Decide);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("warlord ransom", e);
            }
        }

        // ---------------- The game's own raids ----------------

        /// <summary>The running warlord situation whose people these are (or null).</summary>
        private static WorldSituation For(Faction f) =>
            f == null ? null : WorldSituations.Instance?.active.FirstOrDefault(s => !s.ended && s.def?.Worker is WarlordWorker && s.ctx.faction == f);

        /// <summary>A raid of his people has just arrived: sometimes he walks in at its head.</summary>
        public static void JoinArrivingRaid(IncidentParms parms, List<Pawn> raiders)
        {
            if (parms?.faction == null || raiders.NullOrEmpty() || !(parms.target is Map map) || !map.IsPlayerHome) return;
            var s = For(parms.faction);
            var x = s != null ? Warlord(s) : null;
            if (s == null || !Rising(s) || !Loose(x) || x.Faction != parms.faction || x.health.summaryHealth.SummaryHealthPercent < 0.6f) return;
            if (!Rand.Chance(0.4f)) return;
            var anchor = raiders.Where(r => r.Spawned && r.Map == map && r.Faction == parms.faction).RandomElementWithFallback();
            if (anchor == null) return;
            if (!CellFinder.TryFindRandomCellNear(anchor.Position, map, 6, c => c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null, out var cell, 30)) return;
            Find.WorldPawns.ForcefullyKeptPawns.Remove(x);
            Find.WorldPawns.RemovePawn(x);
            GenSpawn.Spawn(x, cell, map);
            raiders.Add(x);
            s.flags.AddDistinct("led");
        }

        /// <summary>A line for the game's raid letter when he rides with it.</summary>
        public static string RaidNote(IncidentParms parms, List<Pawn> pawns)
        {
            var s = For(parms?.faction);
            var x = s != null ? Warlord(s) : null;
            if (x == null || pawns == null || !pawns.Contains(x)) return null;
            return (s.def.Worker as WarlordWorker)?.Text("raidNote", s);
        }
    }
}
