using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A Hospitality guest joins the colony of their own will only when they would (the numbers are GuestWillMath.cs):
    /// they have a reason to stay, there is room for one more, and what someone of their passions and nature wants of a
    /// home is there (a passionate cook wants a decent kitchen). What holds them at home never bars them, but each tie
    /// asks for more friends here than Hospitality does, less one for each reason to stay beyond the first. Short of
    /// that, the guest tab's Recruit button offers only to recruit them by force, and its tooltip says why. Hospitality's
    /// own "a happy guest asks to join" and the council's "a guest who wants to stay" come only for a guest who would.
    /// What taking them costs their people goes by who they are (<see cref="StatPart_GuestCost"/>). Before someone is
    /// taken in, those of the colony who object have their say at the council (Council_DlmGuest_Invite), and their
    /// family may come weeks later to ask for them back (Council_DlmGuest_KinAsks; GuestKin). All by name, only with
    /// Hospitality and the deeper game hooks, and the "guestWill" setting.
    /// </summary>
    public static class GuestWill
    {
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.guestWill ?? true) && People.ModActive(Guests.Mod);

        /// <summary>What was found about a guest, worked out at most every few seconds (the guest tab draws every frame).</summary>
        public class Will
        {
            public readonly List<(string tie, string text)> ties = new List<(string, string)>();
            public readonly List<string> reasons = new List<string>();
            public readonly List<string> noRoom = new List<string>();
            public readonly List<(string want, bool met, string now)> wants = new List<(string, bool, string)>();
            /// <summary>Friends here (Hospitality's count; -1 when it can't be read), and what Hospitality itself asks for (-1 the same).</summary>
            public int friends = -1, required = -1;
            /// <summary>A titled guest: Hospitality goes by seniority, not friends, so the ties ask for no more friends.</summary>
            public bool titled;
            public int tick;

            public int TieWeight => ties.Sum(t => GuestWillMath.TieWeight(t.tie));

            /// <summary>The friends asked for beyond Hospitality's own, for what holds them at home.</summary>
            public int Extra => titled ? 0 : GuestWillMath.ExtraFriends(TieWeight, reasons.Count);

            /// <summary>A reason to stay, room for one more, and what they want of a home.</summary>
            public bool Willing => GuestWillMath.Willing(reasons.Count, noRoom.Count == 0, wants.Count(w => !w.met));

            /// <summary>Friends enough here for Hospitality and for what holds them at home (yes when Hospitality's count can't be read).</summary>
            public bool EnoughFriends => friends < 0 || required < 0 || friends >= required + Extra;

            /// <summary>
            /// They'd stay, where Hospitality asks for no friends (its happy guest who asks to join, the council's guest who
            /// wants to stay): only the friends their ties ask for.
            /// </summary>
            public bool WouldStay => Willing && (friends < 0 || friends >= Extra);
        }

        private static readonly Dictionary<Pawn, Will> cache = new Dictionary<Pawn, Will>();

        public static void Reset()
        {
            cache.Clear();
            pending = null;
        }

        /// <summary>They'd stay of their own will (always, with this off).</summary>
        public static bool WouldStay(Pawn guest) => !On || guest == null || Of(guest).WouldStay;

        public static Will Of(Pawn g)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (cache.TryGetValue(g, out var w) && now - w.tick < 250) return w;
            if (cache.Count > 60) cache.Clear();
            w = new Will { tick = now };
            try
            {
                Read(g, w);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest's will", e);
                w.ties.Clear();
                w.friends = w.required = -1;
                w.noRoom.Clear();
                w.wants.Clear();
                w.reasons.Add("?");
            }
            cache[g] = w;
            return w;
        }

        private static bool Here(Pawn k) => k.Faction == Faction.OfPlayer || k.IsPrisonerOfColony || k.IsSlaveOfColony;

        private static string Among(Pawn k) =>
            k.Faction != null && !k.Faction.IsPlayer ? "CC_GuestWill_Among".Translate(k.Faction.Name.Named("FACTION")).Resolve() : "CC_GuestWill_Away".Translate().Resolve();

        private static void Read(Pawn g, Will w)
        {
            var map = g.MapHeld;
            var colonists = map?.mapPawns.FreeColonistsSpawned.Where(c => !c.IsQuestLodger()).ToList() ?? new List<Pawn>();
            var primary = ModsConfig.IdeologyActive ? Faction.OfPlayer.ideos?.PrimaryIdeo : null;

            // A reason to stay. Love here first: it loosens what their parents hold.
            bool love = LovePartnerRelationUtility.ExistingLovePartners(g, false).ToList().Any(r => r.otherPawn != null && !r.otherPawn.Dead && Here(r.otherPawn))
                        || g.relations != null && !g.DevelopmentalStage.Juvenile()
                        && colonists.Any(c => !c.DevelopmentalStage.Juvenile() && g.relations.OpinionOf(c) >= GuestWillMath.SmittenOpinion && RelationsUtility.AttractedToGender(g, c.gender));
            if (love) w.reasons.Add("CC_GuestWill_Reason_Love".Translate(g.Named("PAWN")).Resolve());
            var kin = colonists.FirstOrDefault(c => Family.Of(g, c));
            if (kin != null) w.reasons.Add("CC_GuestWill_Reason_Family".Translate(g.Named("PAWN"), kin.Named("OTHER"), Family.Relation(g, kin).Named("RELATION")).Resolve());
            var home = g.Faction;
            if (home != null && (WorldSituations.Instance?.HardTimes(home) ?? false))
                w.reasons.Add("CC_GuestWill_Reason_HardTimes".Translate(g.Named("PAWN"), home.Name.Named("FACTION")).Resolve());
            float score = Guests.VisitScore(g);
            if (score >= GuestWillMath.GoodStay) w.reasons.Add("CC_GuestWill_Reason_Stay".Translate(g.Named("PAWN")).Resolve());
            if (primary != null && g.Ideo == primary) w.reasons.Add("CC_GuestWill_Reason_Faith".Translate(g.Named("PAWN"), primary.name.Named("IDEO")).Resolve());

            // What holds them at home: each tie asks for more friends here.
            foreach (var r in LovePartnerRelationUtility.ExistingLovePartners(g, false).ToList())
            {
                var o = r.otherPawn;
                if (o == null || o.Dead || Here(o)) continue;
                Tie(w, GuestWillMath.Tie.Partner, g.Named("PAWN"), o.Named("OTHER"), r.def.GetGenderSpecificLabel(o).Named("RELATION"), Among(o).Named("WHERE"));
            }
            var children = Family.ChildrenOf(g).Where(c => c != null && !c.Dead && c.DevelopmentalStage.Juvenile() && !Here(c)).ToList();
            if (children.Count > 0)
                Tie(w, children.Count == 1 ? GuestWillMath.Tie.Child : GuestWillMath.Tie.Children, g.Named("PAWN"), children[0].Named("OTHER"), Among(children[0]).Named("WHERE"));
            if (GuestWillMath.ParentsHold(g.ageTracker.AgeBiologicalYearsFloat, love))
            {
                var parent = Family.ParentsOf(g).FirstOrDefault(o => o != null && !o.Dead && !Here(o));
                if (parent != null)
                    Tie(w, GuestWillMath.Tie.Parent, g.Named("PAWN"), parent.Named("OTHER"), Family.Relation(g, parent).Named("RELATION"), Among(parent).Named("WHERE"));
            }
            var leader = home?.leader;
            if (leader != null && !leader.Dead && (leader == g || g.relations != null && (g.relations.FamilyByBlood.Contains(leader) || LovePartnerRelationUtility.LovePartnerRelationExists(g, leader))))
                Tie(w, leader == g ? GuestWillMath.Tie.Leader : GuestWillMath.Tie.LeaderKin, g.Named("PAWN"), leader.Named("OTHER"), home.Name.Named("FACTION"));
            if (primary != null && g.Ideo != null && g.Ideo != primary && g.ideo.Certainty >= GuestWillMath.DevoutCertainty)
                Tie(w, GuestWillMath.Tie.Faith, g.Named("PAWN"), g.Ideo.name.Named("IDEO"));

            // Friends here, as Hospitality counts them (titled guests go by its seniority instead).
            bool titled = g.royalty != null && g.royalty.AllTitlesInEffectForReading.Count > 0;
            w.titled = titled;
            if (!titled) HospitalityFriends(g, out w.friends, out w.required);

            if (map == null) return;

            // Room for one more.
            var traits = g.story?.traits?.allTraits?.Where(t => t?.def != null).Select(t => t.def.defName).ToList() ?? new List<string>();
            bool own = GuestWillMath.WantsOwnRoom(traits, titled);
            if (!FreeBed(map, g, false)) w.noRoom.Add("CC_GuestWill_NoBed".Translate(g.Named("PAWN")).Resolve());
            else if (own && !FreeBed(map, g, true)) w.noRoom.Add("CC_GuestWill_NoOwnRoom".Translate(g.Named("PAWN")).Resolve());
            int eaters = colonists.Count + map.mapPawns.PrisonersOfColonySpawnedCount + 1;
            float days = GuestWillMath.DaysOfFood(map.resourceCounter.TotalHumanEdibleNutrition, eaters);
            if (days < GuestWillMath.FoodDays) w.noRoom.Add("CC_GuestWill_NoFood".Translate(days.ToString("0.#").Named("DAYS"), GuestWillMath.FoodDays.ToString("0").Named("NEED")).Resolve());

            // What they want of a home, by their passions and nature: only what the colony could build.
            var skills = g.skills?.skills?.Where(s => s?.def != null && !s.TotallyDisabled).Select(s => (s.def.defName, People.PassionLevel(s), s.Level)).ToList();
            foreach (var want in GuestWillMath.Wants(skills, traits, g.DevelopmentalStage.Juvenile()))
            {
                if (!Unlocked(want)) continue;
                bool met = Met(map, want, out string now);
                w.wants.Add((want, met, now));
            }
        }

        private static void Tie(Will w, string tie, params NamedArgument[] args) =>
            w.ties.Add((tie, ("CC_GuestWill_Tie_" + tie).Translate(args).Resolve()));

        private static MethodInfo friendsIn, enemiesIn, friendsRequired;
        private static bool friendsLooked;

        /// <summary>The guest's friends here and the friends Hospitality asks for (its own sums, as its guest tab does them), or -1.</summary>
        public static void HospitalityFriends(Pawn g, out int friends, out int required)
        {
            friends = required = -1;
            if (!friendsLooked)
            {
                friendsLooked = true;
                var t = GenTypes.GetTypeInAnyAssembly("Hospitality.Utilities.GuestUtility");
                if (t != null)
                {
                    friendsIn = AccessTools.Method(t, "GetFriendsInColony", new[] { typeof(Pawn) });
                    enemiesIn = AccessTools.Method(t, "GetEnemiesInColony", new[] { typeof(Pawn) });
                    friendsRequired = AccessTools.Method(t, "FriendsRequired", new[] { typeof(Map) });
                }
            }
            if (friendsIn == null || enemiesIn == null || friendsRequired == null || g?.MapHeld == null) return;
            try
            {
                friends = (int)friendsIn.Invoke(null, new object[] { g });
                required = (int)friendsRequired.Invoke(null, new object[] { g.MapHeld }) + (int)enemiesIn.Invoke(null, new object[] { g });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest's friends", e);
                friends = required = -1;
            }
        }

        /// <summary>A free bed of the colony's for them (not a guest bed, not for the sick); with <paramref name="own"/>, alone in a room.</summary>
        private static bool FreeBed(Map map, Pawn g, bool own)
        {
            foreach (var b in map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>())
            {
                if (!b.Spawned || b.Medical || b.def.building == null || !b.def.building.bed_humanlike || b.ForOwnerType != BedOwnerType.Colonist) continue;
                if (GuestBeds.IsGuestBed(b) || !b.AnyUnownedSleepingSlot || b.def.building.bed_maxBodySize < g.BodySize) continue;
                if (!own) return true;
                var room = b.GetRoom();
                if (room == null || room.PsychologicallyOutdoors || b.OwnersForReading.Count > 0) continue;
                if (room.ContainedBeds.Count(x => !x.Medical) == 1) return true;
            }
            return false;
        }

        /// <summary>Only what the colony could have: no wish for art before there's a sculptor's table to be built.</summary>
        private static bool Unlocked(string want)
        {
            switch (want)
            {
                case GuestWillMath.Want.Art: return Buildable("TableSculpting");
                case GuestWillMath.Want.Study: return Buildable("SimpleResearchBench") || Buildable("HiTechResearchBench");
                case GuestWillMath.Want.Kitchen:
                case GuestWillMath.Want.Food: return Buildable("FueledStove") || Buildable("ElectricStove");
                default: return true;
            }
        }

        private static bool Buildable(string defName)
        {
            var d = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return d != null && Avail.CanBuild(d);
        }

        /// <summary>Whether the colony has what they want, and how it stands now (for the tooltip).</summary>
        private static bool Met(Map map, string want, out string now)
        {
            switch (want)
            {
                case GuestWillMath.Want.Kitchen:
                {
                    var best = RoomUtil.RoomsWithRole(map, RoomUtil.Kitchen).Select(RoomUtil.Impressiveness).DefaultIfEmpty(float.MinValue).Max();
                    now = best == float.MinValue ? "CC_GuestWill_NoneYet".Translate().Resolve() : RoomUtil.StageLabel(RoomStatDefOf.Impressiveness, best);
                    return best >= GuestWillMath.DecentRoom;
                }
                case GuestWillMath.Want.Fields:
                {
                    int cells = map.zoneManager.AllZones.OfType<Zone_Growing>().Sum(z => z.Cells.Count);
                    now = cells.ToString();
                    return cells >= GuestWillMath.FieldCells;
                }
                case GuestWillMath.Want.Hospital:
                {
                    int beds = map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Count(b => b.Spawned && b.Medical && b.GetRoom()?.Role == RoomUtil.Hospital);
                    now = beds.ToString();
                    return beds >= GuestWillMath.MedicalBeds;
                }
                case GuestWillMath.Want.Art:
                {
                    int art = map.listerBuildings.allBuildingsColonist.Count(b => b.Spawned && b.TryGetComp<CompArt>() is CompArt a && a.Active);
                    now = art.ToString();
                    return art >= GuestWillMath.ArtPieces;
                }
                case GuestWillMath.Want.Study:
                {
                    bool has = map.listerBuildings.allBuildingsColonist.Any(b => b is Building_ResearchBench);
                    now = (has ? "CC_GuestWill_There" : "CC_GuestWill_NoneYet").Translate().Resolve();
                    return has;
                }
                case GuestWillMath.Want.Workshop:
                {
                    bool has = RoomUtil.RoomsWithRole(map, RoomUtil.Workshop).Any();
                    now = (has ? "CC_GuestWill_There" : "CC_GuestWill_NoneYet").Translate().Resolve();
                    return has;
                }
                case GuestWillMath.Want.Animals:
                {
                    int n = map.mapPawns.SpawnedColonyAnimals.Count;
                    now = n.ToString();
                    return n >= GuestWillMath.TameAnimals;
                }
                case GuestWillMath.Want.Rec:
                {
                    bool has = RoomUtil.RoomsWithRole(map, RoomUtil.RecRoom).Any();
                    now = (has ? "CC_GuestWill_There" : "CC_GuestWill_NoneYet").Translate().Resolve();
                    return has;
                }
                case GuestWillMath.Want.Comrades:
                {
                    int n = map.mapPawns.FreeColonistsSpawned.Count(c => !c.DevelopmentalStage.Juvenile() && c.skills != null
                                                                         && Mathf.Max(c.skills.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, c.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0) >= GuestWillMath.ComradeSkill);
                    now = n.ToString();
                    return n >= GuestWillMath.Comrades;
                }
                case GuestWillMath.Want.Rooms:
                {
                    int n = RoomUtil.ColonyRooms(map).Count(r => !r.PsychologicallyOutdoors && RoomUtil.Impressiveness(r) >= GuestWillMath.DecentRoom);
                    now = n.ToString();
                    return n >= GuestWillMath.GoodRooms;
                }
                case GuestWillMath.Want.Food:
                {
                    int n = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                        .Where(t => t.def.ingestible != null && t.def.ingestible.preferability >= FoodPreferability.MealFine && !t.IsForbidden(Faction.OfPlayer)
                                    && (t.IsInAnyStorage() || map.areaManager.Home[t.Position]))
                        .Sum(t => t.stackCount);
                    now = n.ToString();
                    return n >= GuestWillMath.FineMeals;
                }
            }
            now = "";
            return true;
        }

        // ---------------- The guest tab's Recruit button ----------------

        /// <summary>What the Recruit button's tooltip adds: would they stay, and what's missing.</summary>
        public static string Tip(Pawn g)
        {
            var w = Of(g);
            var lines = new List<string> { (w.Willing && w.EnoughFriends ? "CC_GuestWill_Willing" : "CC_GuestWill_NotWilling").Translate(g.Named("PAWN")).Resolve() };
            if (w.ties.Count > 0)
                lines.Add((w.titled ? "CC_GuestWill_TiesTitled" : "CC_GuestWill_Ties").Translate(g.Named("PAWN")).Resolve() + "\n  "
                          + string.Join("\n  ", w.ties.Select(t => (t.text.CapitalizeFirst() + (w.titled ? "" : " " + "CC_GuestWill_TieFriends".Translate(GuestWillMath.TieWeight(t.tie).Named("COUNT")).Resolve()))
                              .Colorize(UiKit.Warn))));
            lines.Add(w.reasons.Count > 0
                ? "CC_GuestWill_Reasons".Translate(g.Named("PAWN")).Resolve() + "\n  " + string.Join("\n  ", w.reasons.Select(r => r.CapitalizeFirst().Colorize(UiKit.Good)))
                : "CC_GuestWill_NoReason".Translate(g.Named("PAWN")).Resolve().Colorize(UiKit.Bad));
            if (w.ties.Count > 0 && !w.titled && w.friends >= 0 && w.required >= 0)
            {
                int off = w.TieWeight - w.Extra;
                string f = "CC_GuestWill_Friends".Translate(w.friends.Named("HAVE"), (w.required + w.Extra).Named("NEED"), w.required.Named("BASE"), w.Extra.Named("EXTRA"), g.Named("PAWN")).Resolve();
                if (off > 0) f += " " + "CC_GuestWill_FriendsOff".Translate(off.Named("OFF")).Resolve();
                lines.Add(w.EnoughFriends ? f.Colorize(UiKit.Good) : f.Colorize(UiKit.Bad));
            }
            if (w.noRoom.Count > 0) lines.Add("CC_GuestWill_Room".Translate().Resolve() + "\n  " + string.Join("\n  ", w.noRoom.Select(r => r.CapitalizeFirst().Colorize(UiKit.Bad))));
            string stage = RoomUtil.StageLabel(RoomStatDefOf.Impressiveness, GuestWillMath.DecentRoom);
            if (w.wants.Count > 0)
                lines.Add("CC_GuestWill_Wants".Translate(g.Named("PAWN")).Resolve() + "\n  " + string.Join("\n  ", w.wants.Select(x =>
                {
                    string s = ("CC_GuestWill_Want_" + x.want).Translate(x.now.Named("NOW"), GuestWillMath.Need(x.want).Named("NEED"), stage.Named("STAGE"),
                        GuestWillMath.ComradeSkill.Named("SKILL")).Resolve();
                    s = s.CapitalizeFirst();
                    return x.met ? s.Colorize(UiKit.Good) : s.Colorize(UiKit.Bad);
                })));
            return string.Join("\n\n", lines);
        }

        // ---------------- What taking them costs ----------------

        /// <summary>The factor on Hospitality's recruiting price for who they are to their people, and why (for the stat's breakdown).</summary>
        public static float CostFactor(Pawn g, List<string> why)
        {
            var home = g.Faction;
            if (home == null || home.IsPlayer) return 1f;
            var family = new List<Pawn>();
            if (g.relations != null)
            {
                family.AddRange(g.relations.FamilyByBlood);
                family.AddRange(LovePartnerRelationUtility.ExistingLovePartners(g, false).Select(r => r.otherPawn));
            }
            bool there = family.Any(k => k != null && k != g && !k.Dead && k.Faction == home);
            var leader = home.leader;
            bool leaderKin = leader != null && !leader.Dead && (leader == g || family.Contains(leader));
            bool role = ModsConfig.IdeologyActive && g.Ideo?.GetRole(g) != null;
            why?.Add((there ? "CC_GuestWill_Cost_Family" : "CC_GuestWill_Cost_NoFamily").Translate(home.Name.Named("FACTION")).Resolve());
            if (leaderKin) why?.Add("CC_GuestWill_Cost_Leader".Translate(home.Name.Named("FACTION")).Resolve());
            if (role) why?.Add("CC_GuestWill_Cost_Role".Translate(g.Ideo.GetRole(g).LabelCap.Named("ROLE")).Resolve());
            return GuestWillMath.CostFactor(there, leaderKin, role);
        }

        // ---------------- The colony has its say ----------------

        /// <summary>A recruiting the council is asked about first (read by DilemmaWorker_GuestInvite).</summary>
        public class Invite
        {
            public Pawn guest, objector;
            public int penalty;
            public bool forced;
            public string objection;
        }

        public static Invite pending;

        /// <summary>While Rimfolk itself recruits (the council said yes): Hospitality goes ahead.</summary>
        private static bool bypass;

        /// <summary>Recruiting by force right now (Hospitality's Recruit, for the family who may come asking).</summary>
        public static bool ForcedNow;

        private static MethodInfo recruit;

        /// <summary>
        /// Hospitality is about to recruit a guest. Someone of the colony who objects (who can't stand them, who holds
        /// fast to a faith they don't share, who sees no bed or no food for them, who won't see them taken against their
        /// will) has their say at the council first: false, and the question comes. While another of the council's
        /// questions waits, or this one, false with a message. With nobody objecting, Hospitality goes ahead.
        /// </summary>
        public static bool MayRecruit(Pawn g, int penalty, bool forced)
        {
            if (bypass || !On || g == null || !g.Spawned) return true;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !(ColonyCouncilMod.Settings?.enableDilemmas ?? true)) return true;
            // apart: a click on the guest tab answered by a message at the top of the screen; nothing happens on the map.
            var open = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Dilemma>().Where(l => !l.resolved).ToList();
            // Asked already: the council's answer, not a second click, decides.
            if (open.Any(l => l.ctx is GuestInviteContext c && c.stranger == g))
            {
                Messages.Message("CC_GuestWill_StillAsking".Translate(g.Named("PAWN")).Resolve(), g, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            var objector = Objector(g, forced, out string objection);
            if (objector == null) return true;
            // One question at a time: the one waiting is answered first, then they can be asked about again.
            if (open.Count > 0)
            {
                Messages.Message("CC_GuestWill_AnswerFirst".Translate(objector.Named("OTHER"), g.Named("PAWN")).Resolve(), g, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            pending = new Invite { guest = g, objector = objector, penalty = penalty, forced = forced, objection = objection };
            bool asked = comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_GuestInvite);
            pending = null;
            return !asked;
        }

        /// <summary>The one of the colony with the strongest objection to taking this guest in, and what it is, or null.</summary>
        public static Pawn Objector(Pawn g, bool forced, out string objection)
        {
            objection = null;
            var map = g.Map;
            if (map == null) return null;
            var adults = map.mapPawns.FreeColonistsSpawned.Where(c => !c.IsQuestLodger() && !c.DevelopmentalStage.Juvenile() && People.HasInnerLife(c) && c.relations != null).ToList();
            var dislike = adults.Where(c => c.relations.OpinionOf(g) <= GuestWillMath.DislikeOpinion).OrderBy(c => c.relations.OpinionOf(g)).FirstOrDefault();
            if (dislike != null)
            {
                objection = "dislike";
                return dislike;
            }
            var primary = ModsConfig.IdeologyActive ? Faction.OfPlayer.ideos?.PrimaryIdeo : null;
            if (primary != null && g.Ideo != null && g.Ideo != primary)
            {
                var devout = adults.Where(c => c.Ideo == primary && c.ideo.Certainty >= GuestWillMath.DevoutCertainty).OrderByDescending(c => c.ideo.Certainty).FirstOrDefault();
                if (devout != null)
                {
                    objection = "faith";
                    return devout;
                }
            }
            if (forced)
            {
                var kind = adults.FirstOrDefault(c => c.story?.traits != null && c.story.traits.HasTrait(TraitDefOf.Kind));
                if (kind != null)
                {
                    objection = "unwilling";
                    return kind;
                }
            }
            if (!FreeBed(map, g, false))
            {
                var crowded = adults.Where(c => c.ownership?.OwnedBed == null || c.ownership.OwnedRoom == null).OrderBy(c => c.needs?.mood?.CurLevel ?? 1f).FirstOrDefault();
                if (crowded != null)
                {
                    objection = "crowded";
                    return crowded;
                }
            }
            int eaters = adults.Count + map.mapPawns.PrisonersOfColonySpawnedCount + 1;
            if (GuestWillMath.DaysOfFood(map.resourceCounter.TotalHumanEdibleNutrition, eaters) < GuestWillMath.FoodDays)
            {
                var cook = adults.Where(c => c.skills?.GetSkill(SkillDefOf.Cooking) is SkillRecord r && !r.TotallyDisabled).OrderByDescending(c => c.skills.GetSkill(SkillDefOf.Cooking).Level).FirstOrDefault();
                if (cook != null)
                {
                    objection = "food";
                    return cook;
                }
            }
            return null;
        }

        /// <summary>The council said take them in: Hospitality's own recruiting, as its button would have done.</summary>
        public static bool RecruitNow(Pawn g, int penalty, bool forced)
        {
            if (g == null || g.Dead || !g.Spawned || !Strangers.IsHospitalityGuest(g)) return false;
            recruit ??= AccessTools.Method("Hospitality.Utilities.GuestUtility:Recruit", new[] { typeof(Pawn), typeof(int), typeof(bool) });
            if (recruit == null) return false;
            bypass = true;
            try
            {
                recruit.Invoke(null, new object[] { g, penalty, forced });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("recruiting a guest after the council", e);
            }
            finally
            {
                bypass = false;
            }
            return g.Faction == Faction.OfPlayer;
        }
    }

    /// <summary>
    /// Someone who joined from a Hospitality visit and has family with the people they came from (CouncilGameComponent
    /// .guestKin): when the family may come to ask for them back, and whether they were taken by force.
    /// </summary>
    public class GuestKin : IExposable
    {
        public Pawn pawn;
        public Faction faction;
        public int due;
        public bool forced;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref due, "due");
            Scribe_Values.Look(ref forced, "forced");
        }

        /// <summary>Someone joined from a visit (Hospitality's Adopt): their family there may come asking, weeks from now.</summary>
        public static void Joined(Pawn p, Faction from)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || p == null || from == null || from.IsPlayer || !GuestWill.On) return;
            if (!Close(p).Any(k => k.Faction == from)) return;
            comp.guestKin.RemoveAll(k => k.pawn == p);
            comp.guestKin.Add(new GuestKin
            {
                pawn = p, faction = from, forced = GuestWill.ForcedNow,
                due = Find.TickManager.TicksGame + Rand.RangeInclusive(GuestWillMath.AskMinDays, GuestWillMath.AskMaxDays) * GenDate.TicksPerDay
            });
        }

        /// <summary>Their close family, alive: partners, parents, children and brothers and sisters.</summary>
        private static IEnumerable<Pawn> Close(Pawn p)
        {
            var seen = new HashSet<Pawn>();
            foreach (var r in LovePartnerRelationUtility.ExistingLovePartners(p, false).ToList())
                if (r.otherPawn != null && seen.Add(r.otherPawn)) yield return r.otherPawn;
            foreach (var k in Family.ParentsOf(p).Concat(Family.ChildrenOf(p)))
                if (k != null && !k.Dead && seen.Add(k)) yield return k;
            if (p.relations == null) yield break;
            foreach (var r in p.relations.DirectRelations.ToList())
                if (r.def == PawnRelationDefOf.Sibling && r.otherPawn != null && !r.otherPawn.Dead && seen.Add(r.otherPawn)) yield return r.otherPawn;
            foreach (var k in p.relations.FamilyByBlood.ToList())
                if (k != null && !k.Dead && seen.Add(k) && p.GetRelations(k).Any(d => d == PawnRelationDefOf.Sibling || d.defName == "HalfSibling")) yield return k;
        }

        /// <summary>What they are to them: "partner", "parent", "child", "sibling", or null.</summary>
        public static string Kind(Pawn p, Pawn k)
        {
            if (LovePartnerRelationUtility.LovePartnerRelationExists(p, k)) return "partner";
            var rel = p.GetRelations(k).ToList();
            if (rel.Contains(PawnRelationDefOf.Parent)) return "parent";
            if (rel.Contains(PawnRelationDefOf.Child)) return "child";
            if (rel.Contains(PawnRelationDefOf.Sibling) || rel.Any(d => d.defName == "HalfSibling")) return "sibling";
            return Family.ParentsOf(p).Contains(k) ? "parent" : Family.ChildrenOf(p).Contains(k) ? "child" : null;
        }

        /// <summary>The one of their family who'd come: grown, free to come, the closest first.</summary>
        public Pawn Who(out string kind)
        {
            kind = null;
            Pawn best = null;
            int rank = int.MaxValue;
            float opinion = float.MinValue;
            foreach (var k in Close(pawn))
            {
                if (k.DevelopmentalStage.Juvenile() || !MovingOnHeld.CanCome(k, faction)) continue;
                string kk = Kind(pawn, k);
                int r = GuestWillMath.KinRank(kk);
                float o = k.relations?.OpinionOf(pawn) ?? 0f;
                if (r > 3 || r > rank || r == rank && o <= opinion) continue;
                best = k;
                rank = r;
                opinion = o;
                kind = kk;
            }
            return best;
        }

        /// <summary>
        /// Each hour (Social/Family.cs): someone whose family's time has come, by day at home, and the question free to
        /// come: the one who'd come walks in and waits at the gate, and the council is asked. Each family comes once, by
        /// chance (GuestWillMath.AskChance); after a fortnight past its time, or with nobody who could come, never.
        /// </summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            var list = comp?.guestKin;
            if (list == null || list.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            list.RemoveAll(k => k.pawn == null || k.pawn.Dead || !k.pawn.IsColonist || now > k.due + GuestWillMath.AskWindowDays * GenDate.TicksPerDay);
            if (!GuestWill.On || !(ColonyCouncilMod.Settings?.enableDilemmas ?? true)) return;
            foreach (var k in list.Where(k => now >= k.due).ToList())
            {
                var p = k.pawn;
                var map = p.Map;
                if (map == null || !map.IsPlayerHome || !p.Spawned || GenLocalDate.HourOfDay(map) < 8 || GenLocalDate.HourOfDay(map) > 15) continue;
                if (!MovingOn.Friendly(k.faction) || Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Dilemma d && !d.resolved)) continue;
                try
                {
                    list.Remove(k);
                    var who = k.Who(out string kind);
                    if (who == null || !Rand.Chance(GuestWillMath.AskChance(kind, who.relations?.OpinionOf(p) ?? 0f, k.forced))) continue;
                    var gate = PlaySpots.GateCell(map, p);
                    var lord = Arrivals.Visit(new List<Pawn> { who }, map, k.faction, gate.IsValid ? gate : (IntVec3?)null, 30f);
                    if (lord == null)
                    {
                        Arrivals.Release(who);
                        continue;
                    }
                    DilemmaWorker_KinAsks.coming = new DilemmaWorker_KinAsks.Coming { pawn = p, kin = who, kind = kind, faction = k.faction, forced = k.forced };
                    bool asked = comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_KinAsks);
                    DilemmaWorker_KinAsks.coming = null;
                    // Nobody to ask after all: they visit, and go home again.
                    if (!asked) Arrivals.SendAway(new[] { who });
                    return;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("family asking for a guest back", e);
                }
            }
        }
    }

    // ---------------- The council's questions ----------------

    /// <summary>The council asked about taking a guest in: X the guest, A the one who objects, B the one X thinks best of.</summary>
    public class GuestInviteContext : DilemmaContext
    {
        public int penalty;
        public bool forced;
        /// <summary>"dislike", "faith", "unwilling", "crowded" or "food".</summary>
        public string objection;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref penalty, "penalty");
            Scribe_Values.Look(ref forced, "forced");
            Scribe_Values.Look(ref objection, "objection");
        }
    }

    /// <summary>
    /// Council_DlmGuest_Invite: only from GuestWill.MayRecruit, when Hospitality is about to recruit a guest someone of
    /// the colony objects to. Tags: the objection, and "forced".
    /// </summary>
    public class DilemmaWorker_GuestInvite : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var inv = GuestWill.pending;
            if (inv?.guest == null || inv.objector == null || !inv.guest.Spawned) return false;
            var g = inv.guest;
            var gmap = g.Map;
            var friend = gmap.mapPawns.FreeColonistsSpawned.Where(c => c != inv.objector && !c.IsQuestLodger() && g.relations != null)
                .OrderByDescending(c => g.relations.OpinionOf(c)).FirstOrDefault();
            ctx = new GuestInviteContext
            {
                def = def, map = gmap, a = inv.objector, b = friend, stranger = g, faction = g.Faction, penalty = inv.penalty, forced = inv.forced, objection = inv.objection
            };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is GuestInviteContext c)) return;
            if (!c.objection.NullOrEmpty()) tags.Add(c.objection);
            if (c.forced) tags.Add("forced");
        }
    }

    /// <summary>The guest is taken in after all, the way Hospitality's Recruit button does it, at its price.</summary>
    public class DilemmaEffect_GuestRecruit : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || x.Dead || !x.Spawned) return "CC_Stranger_Gone".Translate(x?.LabelShort ?? "?").ToString();
            if (!Strangers.IsHospitalityGuest(x)) return "CC_PM_Guest_CantStay".Translate(x.LabelShort).ToString();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: Hospitality's own recruiting; its letter says they've joined, and they're one of the colony at once.
            if (ctx is GuestInviteContext c) GuestWill.RecruitNow(c.stranger, c.penalty, c.forced);
        }
    }

    /// <summary>
    /// Council_DlmGuest_KinAsks: someone who joined from a visit, whose family has walked in from their people to take
    /// them home (GuestKin.Hourly). A the one who joined, X the one who came ({RELATION}: what they are to A). A
    /// MovingOnContext, so keeping A is the longing of someone kept from moving on (Social/MovingOnHeld.cs). Tags: what
    /// they are ("partner", "parent", "child", "sibling"), and "forced".
    /// </summary>
    public class DilemmaWorker_KinAsks : DilemmaWorker
    {
        public class Coming
        {
            public Pawn pawn, kin;
            public string kind;
            public Faction faction;
            public bool forced;
        }

        public static Coming coming;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var c = coming;
            if (c?.pawn == null || c.kin == null || !c.pawn.Spawned || !c.kin.Spawned) return false;
            float strength = GuestWillMath.Longing(c.kind, c.pawn.relations?.OpinionOf(c.kin) ?? 0f) * (c.forced ? 1f : 0.7f);
            ctx = new KinAsksContext
            {
                def = def, map = c.pawn.Map, a = c.pawn, stranger = c.kin, faction = c.faction, relative = c.kin, relativeName = c.kin.LabelShort,
                relation = Family.Relation(c.pawn, c.kin), kind = c.kind == "partner" ? "love" : "family", strength = strength, count = def.thingCount,
                who = c.kind, forced = c.forced
            };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is KinAsksContext c)) return;
            if (!c.who.NullOrEmpty()) tags.Add(c.who);
            if (c.forced) tags.Add("forced");
        }
    }

    /// <summary>Family come to ask for someone back: what they are to them ("partner", "parent", "child", "sibling"), and whether they were taken by force.</summary>
    public class KinAsksContext : MovingOnContext
    {
        public string who;
        public bool forced;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref who, "who");
            Scribe_Values.Look(ref forced, "forced");
        }
    }

    /// <summary>
    /// A goes home with X: when A sets off from the gate, X goes with them (DilemmaEffect_LeaveForGood). Before the
    /// leaving in the answer's effects.
    /// </summary>
    public class DilemmaEffect_GoHomeWith : DilemmaEffect
    {
        /// <summary>Never one of the colony's last four grown-ups (as with moving on).</summary>
        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.map != null && !MovingOn.Spare(ctx.map) ? "CC_GuestWill_CantSpare".Translate(ctx.a.Named("PAWN")).Resolve() : null;

        public override void Apply(DilemmaContext ctx)
        {
            // apart: who goes with whom; it shows when they set off together.
            if (ctx?.a != null && ctx.stranger != null) DilemmaEffect_LeaveForGood.GoesWith(ctx.a, ctx.stranger);
        }
    }

    // ---------------- Hospitality, by name ----------------

    /// <summary>Hospitality's recruiting price, for who they are to their people (GuestWill.CostFactor). Put on its two stats at startup.</summary>
    public class StatPart_GuestCost : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!GuestWill.On || !(req.Thing is Pawn p)) return;
            try
            {
                val *= GuestWill.CostFactor(p, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest's price", e);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!GuestWill.On || !(req.Thing is Pawn p)) return null;
            var why = new List<string>();
            float f = GuestWill.CostFactor(p, why);
            if (Mathf.Approximately(f, 1f) || why.Count == 0) return null;
            return "CC_GuestWill_CostStat".Translate(p.Named("PAWN")).Resolve() + ": x" + f.ToStringPercent() + "\n  " + string.Join("\n  ", why);
        }
    }

    /// <summary>Puts <see cref="StatPart_GuestCost"/> on Hospitality's two recruiting prices, once, when the game starts.</summary>
    [StaticConstructorOnStartup]
    public static class GuestWillStartup
    {
        static GuestWillStartup()
        {
            try
            {
                foreach (var name in new[] { "RecruitRelationshipDamage", "ForcedRecruitRelationshipDamage" })
                {
                    var stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
                    if (stat == null) continue;
                    stat.parts ??= new List<StatPart>();
                    if (!stat.parts.Any(x => x is StatPart_GuestCost)) stat.parts.Add(new StatPart_GuestCost { parentStat = stat });
                }
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] A guest's price can't go by who they are: " + e.Message);
            }
        }
    }

    /// <summary>The guest tab's Recruit button: only by force for a guest who wouldn't stay, and why in its tooltip (GuestWill).</summary>
    [HarmonyPatch]
    public static class Patch_GuestWillButton
    {
        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target() => GenTypes.GetTypeInAnyAssembly("Hospitality.Utilities.DialogUtility") is Type t
            ? AccessTools.Method(t, "DrawRecruitButton", new[] { typeof(Rect), typeof(Pawn), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })
            : null;

        public static void Prefix(Rect __0, Pawn __1, ref bool __2)
        {
            if (!GuestWill.On || __1 == null) return;
            try
            {
                if (__2 && !(GuestWill.Of(__1) is var w && w.Willing && w.EnoughFriends)) __2 = false;
                TooltipHandler.TipRegion(__0, new TipSignal(() => GuestWill.Tip(__1), __1.thingIDNumber ^ 0x5157));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the guest tab's recruit button", e);
            }
        }
    }

    /// <summary>
    /// The friends the guest tab asks for, with those what holds them at home asks for (GuestWill), so Hospitality's own
    /// bar and count show them. A small method the game may compile into its caller: then only the Recruit button
    /// (Patch_GuestWillButton) and its tooltip count them, which they do either way.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_GuestWillFriends
    {
        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target() => GenTypes.GetTypeInAnyAssembly("Hospitality.ITab_Pawn_Guest") is Type t ? AccessTools.Method(t, "GetRequiredFriends", new[] { typeof(Pawn) }) : null;

        public static void Postfix(Pawn __0, ref int __result)
        {
            if (!GuestWill.On || __0 == null) return;
            try
            {
                __result += GuestWill.Of(__0).Extra;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the friends a guest needs", e);
            }
        }
    }

    /// <summary>Hospitality's "a happy guest asks to join" only for a guest who'd stay (GuestWill).</summary>
    [HarmonyPatch]
    public static class Patch_GuestWillHappy
    {
        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target() => GenTypes.GetTypeInAnyAssembly("Hospitality.IncidentWorker_HappyGuestJoins") is Type t ? AccessTools.Method(t, "IsHappyGuest", new[] { typeof(Pawn) }) : null;

        public static void Postfix(Pawn __0, ref bool __result)
        {
            if (!__result) return;
            try
            {
                __result = GuestWill.WouldStay(__0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a happy guest asking to join", e);
            }
        }
    }

    /// <summary>Before Hospitality recruits a guest, the colony has its say (GuestWill.MayRecruit).</summary>
    [HarmonyPatch]
    public static class Patch_GuestWillRecruit
    {
        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target() => GenTypes.GetTypeInAnyAssembly("Hospitality.Utilities.GuestUtility") is Type t
            ? AccessTools.Method(t, "Recruit", new[] { typeof(Pawn), typeof(int), typeof(bool) })
            : null;

        public static bool Prefix(Pawn __0, int __1, bool __2)
        {
            try
            {
                if (!GuestWill.MayRecruit(__0, __1, __2)) return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the council on a guest joining", e);
            }
            GuestWill.ForcedNow = __2;
            return true;
        }

        public static void Postfix() => GuestWill.ForcedNow = false;
    }

    /// <summary>Someone taken in from a visit (Hospitality's Adopt, whichever way): their family may come asking (GuestKin).</summary>
    [HarmonyPatch]
    public static class Patch_GuestWillAdopt
    {
        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        private static MethodBase Target() => GenTypes.GetTypeInAnyAssembly("Hospitality.Utilities.GuestUtility") is Type t ? AccessTools.Method(t, "Adopt", new[] { typeof(Pawn) }) : null;

        public static void Prefix(Pawn __0, out Faction __state) => __state = __0?.Faction;

        public static void Postfix(Pawn __0, Faction __state)
        {
            if (__0 == null || __state == null || __0.Faction != Faction.OfPlayer) return;
            try
            {
                GuestKin.Joined(__0, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a guest's family", e);
            }
        }
    }
}
