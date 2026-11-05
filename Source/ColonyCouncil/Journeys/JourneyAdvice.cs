using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>What a journey is for, read from where it goes: its aim, the danger there, and what it asks of the people who go.</summary>
    public sealed class JourneyPurpose
    {
        public JourneyAim aim = JourneyAim.Travel;
        public JourneyDanger danger;
        /// <summary>Who or what waits there isn't known (a distress signal, a site whose parts are hidden).</summary>
        public bool unknown;
        /// <summary>The errand's own number of grown colonists, its named one, the skill the place calls for.</summary>
        public int minPeople;
        public Pawn named;
        public int skill = -1;
        /// <summary>Someone hurt to bring home: a doctor along.</summary>
        public bool rescue;
        /// <summary>A town's trade request: what it asks for and how many.</summary>
        public ThingDef requestDef;
        public int requestCount;
        /// <summary>The place's people, and the place.</summary>
        public Faction people;
        public WorldObject place;
        /// <summary>What it is, in one sentence.</summary>
        public string what;
    }

    /// <summary>One colonist in the planner's overview: as the advice reads them, their group and reason, their chips and the whole of it on hover.</summary>
    public sealed class JourneyPersonRow
    {
        public Pawn pawn;
        public JourneyAdviceMath.Person person;
        public AdviceGroup group;
        public AdviceWhy why;
        public string reason, now, tip;
        public readonly List<UiKit.PillSpec> state = new List<UiKit.PillSpec>();
        public readonly List<UiKit.PillSpec> skills = new List<UiKit.PillSpec>();
        /// <summary>The row's height, measured once for a width.</summary>
        public float height = -1f, heightFor = -1f;
    }

    /// <summary>One animal in the planner's overview.</summary>
    public sealed class JourneyBeastRow
    {
        public Pawn pawn;
        public AdviceGroup group;
        public AdviceWhy why;
        public string reason, tip;
        public readonly List<UiKit.PillSpec> chips = new List<UiKit.PillSpec>();
    }

    /// <summary>A thing recommended for the list, how many more and why; where it comes from (the stores, else a stall).</summary>
    public sealed class JourneyItemRec
    {
        public ThingDef def;
        public string kind;
        public int count;
        public string why;
        /// <summary>How many of it come off a colony stall, and the stall's name (RimMarket).</summary>
        public int fromStall;
        public string stallName;
    }

    /// <summary>The planner's advice at a moment: built when the planner opens and then at most once a second while it is open.</summary>
    public sealed class JourneyAdviceModel
    {
        public JourneyPurpose purpose;
        public JourneyWants wants;
        public JourneyAdviceMath.Advice advice = new JourneyAdviceMath.Advice();
        public readonly List<JourneyPersonRow> people = new List<JourneyPersonRow>();
        public readonly List<JourneyBeastRow> beasts = new List<JourneyBeastRow>();
        public readonly List<Pawn> recommendedPeople = new List<Pawn>();
        public readonly List<Pawn> recommendedBeasts = new List<Pawn>();
        public readonly List<JourneyItemRec> items = new List<JourneyItemRec>();
        public readonly List<string> notes = new List<string>();
        /// <summary>What the player's own picks leave the home without.</summary>
        public List<HomeWarning> picked = new List<HomeWarning>();
        public int grown;
        /// <summary>The caravan's own speed with this load, in tiles a day (0 when unknown).</summary>
        public float tilesPerDay;
        public string aimWord, dangerWord, partyLine, recLeaves;
        public Color dangerColor;
    }

    /// <summary>
    /// The journey planner's advice (UI/JourneyDialog.cs; the numbers are JourneyAdviceMath.cs): what the journey is for,
    /// from where it goes (a fight at a site with hostiles, a raiders' camp, a hostile town or a call to arms; trade at a
    /// town or a trading post; talks; an escort of settlers or a pilgrim; a mission's own errand; or a journey to look
    /// around); each colonist at home read for it (health, mind, the skills a journey uses, gear, the weather on the way,
    /// needs on the road, what the home loses while they are away) and placed with one reason; the animals by the load;
    /// and what to bring besides the planner's own list. Read from the game when the planner asks, never while it is shut.
    /// </summary>
    public static class JourneyAdvice
    {
        // ---------------- What the journey is for ----------------

        private static string T(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        /// <summary>What a journey is for, from its destination (the colony's own missions and calls first, then the place itself).</summary>
        public static JourneyPurpose Purpose(JourneyPlan plan)
        {
            var pp = new JourneyPurpose();
            var map = plan?.map;
            if (map == null || !plan.HasDestination)
            {
                pp.what = T("CC_JA_What_Nowhere");
                return pp;
            }
            string place = plan.Dest;
            try
            {
                var tile = plan.DestTile;
                WorldObject wo = null;
                if (plan.destObject >= 0) wo = Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.ID == plan.destObject && !o.Destroyed);
                wo ??= Find.WorldObjects.ObjectsAt(tile).FirstOrDefault(o => o != null && !(o is Caravan));
                pp.place = wo;
                pp.people = wo?.Faction;
                if (wo is Settlement town && (MissionAt(town, pp, place) || ArmsAt(town, map, pp, place))) return pp;
                if (SettlersFor(tile, map, pp, place)) return pp;
                switch (wo)
                {
                    case Site site:
                        SitePurpose(site, map, pp, place);
                        break;
                    case PeaceTalks _:
                        pp.aim = JourneyAim.Talk;
                        pp.what = T("CC_JA_What_PeaceTalks", place.Named("PLACE"), People(pp.people).Named("PEOPLE"));
                        break;
                    case Settlement s:
                        TownPurpose(s, map, pp, place);
                        break;
                    default:
                        pp.what = T("CC_JA_What_Place", place.Named("PLACE"));
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey advice: what the journey is for", e);
                pp.aim = JourneyAim.Travel;
                pp.what = T("CC_JA_What_Place", place.Named("PLACE"));
            }
            return pp;
        }

        private static string People(Faction f) => f?.Name ?? T("CC_JA_NoPeople");

        /// <summary>A mission of the colony's taken on at this town: a pilgrim walked home, a feast, word carried, hands for a harvest, an apprentice.</summary>
        private static bool MissionAt(Settlement s, JourneyPurpose pp, string place)
        {
            foreach (var m in Missions.All)
            {
                if (m == null || m.state != MissionState.Active) continue;
                var town = m.kind == "word" ? Find.WorldObjects.Settlements.FirstOrDefault(x => x != null && x.ID == m.target) : MissionPeoples.TownOf(m);
                if (town != s) continue;
                var named = m.a != null && !m.a.Dead ? m.a : null;
                switch (m.kind)
                {
                    case "escort":
                        pp.aim = JourneyAim.Escort;
                        pp.minPeople = 2;
                        pp.what = T("CC_JA_What_Pilgrim", place.Named("PLACE"));
                        return true;
                    case "invitation":
                        pp.aim = JourneyAim.Talk;
                        pp.minPeople = Math.Max(2, m.target);
                        pp.named = named;
                        pp.what = T("CC_JA_What_Feast", place.Named("PLACE"), People(m.faction).Named("PEOPLE"), pp.minPeople.Named("COUNT"));
                        return true;
                    case "word":
                        pp.aim = JourneyAim.Talk;
                        pp.named = named;
                        pp.what = named != null ? T("CC_JA_What_WordNamed", place.Named("PLACE"), named.LabelShort.Named("PAWN")) : T("CC_JA_What_Word", place.Named("PLACE"));
                        return true;
                    case "harvest":
                        pp.aim = JourneyAim.Errand;
                        pp.skill = JourneyAdviceMath.Plants;
                        pp.minPeople = Math.Max(2, m.target);
                        pp.what = T("CC_JA_What_Harvest", place.Named("PLACE"), pp.minPeople.Named("COUNT"));
                        return true;
                    case "apprentice":
                        pp.aim = JourneyAim.Errand;
                        pp.named = named;
                        pp.what = named != null ? T("CC_JA_What_Apprentice", place.Named("PLACE"), named.LabelShort.Named("PAWN")) : T("CC_JA_What_Place", place.Named("PLACE"));
                        return true;
                }
            }
            return false;
        }

        /// <summary>A people's call to arms at this town, the colony's word on it given (a defence pact's, or a debt's fighters).</summary>
        private static bool ArmsAt(Settlement s, Map map, JourneyPurpose pp, string place)
        {
            Faction caller = null;
            try
            {
                foreach (var t in Pacts.Live())
                    if (Pacts.KindOf(t) == PactKind.Defence && PactMath.ParseArms(t.terms.goods, out int town, out _) && town == s.ID)
                    {
                        caller = Pacts.PeopleOf(t);
                        break;
                    }
                if (caller == null)
                    foreach (var t in FavourAsks.Waiting())
                        if (FavourMath.ParseNeed(t.terms.goods, out var need) && need == FavourNeed.Fighters && t.terms.about.kind == RefKind.Town && Refs.Town(t.terms.about) == s)
                        {
                            caller = Refs.Faction(t.to) ?? s.Faction;
                            break;
                        }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey advice: a call to arms", e);
            }
            if (caller == null) return false;
            pp.aim = JourneyAim.Fight;
            pp.danger = JourneyDanger.Some;
            pp.minPeople = PactMath.MinFighters;
            pp.people = caller;
            pp.what = T("CC_JA_What_Arms", place.Named("PLACE"), People(caller).Named("PEOPLE"), PactMath.MinFighters.Named("COUNT"));
            return true;
        }

        /// <summary>Settlers waiting at this home for the colony's caravan to their hamlet's place.</summary>
        private static bool SettlersFor(PlanetTile tile, Map map, JourneyPurpose pp, string place)
        {
            if (!Hamlets.On) return false;
            try
            {
                foreach (var (home, h) in Hamlets.All())
                    if (home == map && h != null && h.stage == HamletStage.Waiting && Hamlets.TileOf(h) == tile)
                    {
                        pp.aim = JourneyAim.Escort;
                        pp.minPeople = HamletMath.MinEscort;
                        pp.people = h.people;
                        pp.what = T("CC_JA_What_Settlers", place.Named("PLACE"), HamletMath.MinEscort.Named("COUNT"));
                        return true;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey advice: the settlers' road", e);
            }
            return false;
        }

        /// <summary>A site: hostiles there (their strength against the home's, or unknown), a raiders' camp, a lump to dig out, someone to bring home, or a place to look over.</summary>
        private static void SitePurpose(Site site, Map map, JourneyPurpose pp, string place)
        {
            var camp = RaidersCamps.CampAt(site, out _);
            bool distress = false, lump = false, refugee = false, wants = false;
            foreach (var part in site.parts)
            {
                var w = part?.def?.Worker;
                if (w is SitePartWorker_DistressCall) distress = true;
                if (w is SitePartWorker_PreciousLump) lump = true;
                if (w is SitePartWorker_DownedRefugee) refugee = true;
                if (part?.def?.wantsThreatPoints == true) wants = true;
            }
            float points = camp != null ? camp.points : site.ActualThreatPoints;
            bool hostile = site.Faction != null && site.Faction.HostileTo(Faction.OfPlayer);
            pp.unknown = distress || !site.sitePartsKnown;
            bool threat = camp != null || points > 0f || wants || distress || hostile;
            pp.danger = !threat ? JourneyDanger.None : pp.unknown ? JourneyDanger.Some : (JourneyDanger)Math.Max((int)JourneyDanger.Low, (int)JourneyAdviceMath.DangerOf(points, Mathf.Max(1f, StorytellerUtility.DefaultThreatPointsNow(map))));
            pp.rescue = refugee;
            if (lump)
            {
                pp.aim = JourneyAim.Errand;
                pp.skill = JourneyAdviceMath.Mining;
                pp.what = T(threat ? "CC_JA_What_LumpGuarded" : "CC_JA_What_Lump", place.Named("PLACE"));
                return;
            }
            if (camp != null)
            {
                pp.aim = JourneyAim.Fight;
                pp.what = T("CC_JA_What_Camp", place.Named("PLACE"));
                return;
            }
            if (threat)
            {
                pp.aim = JourneyAim.Fight;
                pp.what = T(distress ? "CC_JA_What_Distress" : refugee ? "CC_JA_What_RescueGuarded" : pp.unknown ? "CC_JA_What_Unknown" : "CC_JA_What_Hostiles", place.Named("PLACE"));
                return;
            }
            pp.what = T(refugee ? "CC_JA_What_Rescue" : "CC_JA_What_Place", place.Named("PLACE"));
        }

        /// <summary>A town: hostile (a hard fight), or one to trade with (a trading post, a trade pact, a trade request), or one to visit.</summary>
        private static void TownPurpose(Settlement s, Map map, JourneyPurpose pp, string place)
        {
            var f = s.Faction;
            if (f != null && f.HostileTo(Faction.OfPlayer))
            {
                pp.aim = JourneyAim.Fight;
                pp.danger = JourneyDanger.High;
                pp.what = T("CC_JA_What_HostileTown", place.Named("PLACE"), People(f).Named("PEOPLE"));
                return;
            }
            var req = s.GetComponent<TradeRequestComp>();
            if (req != null && req.ActiveRequest && req.requestThingDef != null)
            {
                pp.requestDef = req.requestThingDef;
                pp.requestCount = req.requestCount;
            }
            bool post = false, pact = false;
            try
            {
                post = TradingPosts.On && TradingPosts.IsPost(s.ID);
                pact = Pacts.On && f != null && Pacts.Of(f, map, PactKind.Trade) != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey advice: a town's trade", e);
            }
            if (s.CanTradeNow || post || pp.requestDef != null)
            {
                pp.aim = JourneyAim.Trade;
                string what = T(post ? "CC_JA_What_Post" : "CC_JA_What_Trade", place.Named("PLACE"), People(f).Named("PEOPLE"));
                if (pact) what += " " + T("CC_JA_What_TradePact", People(f).Named("PEOPLE"));
                if (pp.requestDef != null)
                    what += " " + T("CC_JA_What_Request", pp.requestCount.Named("COUNT"), Find.ActiveLanguageWorker.Pluralize(pp.requestDef.label, pp.requestCount).Named("THING"));
                pp.what = what;
                return;
            }
            pp.aim = JourneyAim.Talk;
            pp.what = T("CC_JA_What_Visit", place.Named("PLACE"), People(f).Named("PEOPLE"));
        }

        // ---------------- The colonists ----------------

        private static readonly SkillDef[] Skills = new SkillDef[JourneyAdviceMath.SkillCount];

        private static SkillDef SkillOf(int i)
        {
            if (Skills[0] == null)
            {
                Skills[JourneyAdviceMath.Shooting] = SkillDefOf.Shooting;
                Skills[JourneyAdviceMath.Melee] = SkillDefOf.Melee;
                Skills[JourneyAdviceMath.Medicine] = SkillDefOf.Medicine;
                Skills[JourneyAdviceMath.Social] = SkillDefOf.Social;
                Skills[JourneyAdviceMath.Animals] = SkillDefOf.Animals;
                Skills[JourneyAdviceMath.Plants] = SkillDefOf.Plants;
                Skills[JourneyAdviceMath.Mining] = SkillDefOf.Mining;
            }
            return i >= 0 && i < Skills.Length ? Skills[i] : null;
        }

        private static WorkTypeDef cooking;
        private static bool cookingLooked;

        private static WorkTypeDef Cooking
        {
            get
            {
                if (!cookingLooked)
                {
                    cookingLooked = true;
                    cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
                }
                return cooking;
            }
        }

        /// <summary>Does this work at home: able to, and set to it.</summary>
        private static bool Does(Pawn p, WorkTypeDef w)
        {
            if (w == null || p.workSettings == null || !p.workSettings.EverWork || p.WorkTypeIsDisabled(w)) return false;
            return p.workSettings.WorkIsActive(w);
        }

        /// <summary>Who could go from this home: its free colonists, not lodgers or babies.</summary>
        public static List<Pawn> HomePeople(Map map) =>
            map?.mapPawns?.FreeColonistsSpawned.Where(p => p != null && !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).OrderBy(p => p.LabelShort).ToList() ?? new List<Pawn>();

        /// <summary>Animals of the colony's at this home.</summary>
        public static List<Pawn> HomeAnimals(Map map) =>
            map?.mapPawns?.SpawnedColonyAnimals.Where(a => a != null && !a.Dead && a.RaceProps.Animal).OrderByDescending(a => a.RaceProps.packAnimal).ThenBy(a => a.LabelShort).ToList()
            ?? new List<Pawn>();

        /// <summary>A colonist read for the advice.</summary>
        private static JourneyAdviceMath.Person Read(Pawn p, JourneyPlan plan, Dictionary<Pawn, List<Apparel>> clothes, List<Pawn> home)
        {
            var map = plan.map;
            var r = new JourneyAdviceMath.Person { id = p.thingIDNumber, grown = !p.DevelopmentalStage.Juvenile(), downed = p.Downed, mental = p.InMentalState };
            var hs = p.health?.hediffSet;
            if (p.health != null && hs != null)
            {
                r.health = p.health.summaryHealth?.SummaryHealthPercent ?? 1f;
                r.pain = hs.PainTotal;
                r.moving = p.health.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 1f;
                r.sick = hs.AnyHediffMakesSickThought;
                r.wounds = p.health.HasHediffsNeedingTend();
                r.inBed = p.InBed() && HealthAIUtility.ShouldSeekMedicalRest(p);
                r.injuries = hs.hediffs.Count(h => h is Hediff_Injury);
                r.missing = hs.GetMissingPartsCommonAncestors().Count;
                r.pregnant = ModsConfig.BiotechActive && PregnancyUtility.GetPregnancyHediff(p) != null;
                r.addicted = hs.hediffs.Any(h => h is Hediff_Addiction) || (p.genes?.GenesListForReading?.Any(g => g is Gene_ChemicalDependency d && d.Active) ?? false);
            }
            var mood = p.needs?.mood;
            if (mood != null) r.mood = mood.CurLevelPercentage;
            var br = p.mindState?.mentalBreaker;
            if (br != null)
            {
                r.minor = br.BreakThresholdMinor;
                r.major = br.BreakThresholdMajor;
            }
            r.band = (int)(Heart.ForView(p)?.Band ?? StrainBand.Steady);
            for (int i = 0; i < JourneyAdviceMath.SkillCount; i++)
            {
                var sk = p.skills?.GetSkill(SkillOf(i));
                r.skills[i] = sk == null || sk.TotallyDisabled ? -1 : sk.Level;
            }
            var weapon = p.equipment?.Primary;
            r.armed = weapon != null;
            r.ranged = weapon != null && weapon.def.IsRangedWeapon;
            r.quality = weapon != null && weapon.TryGetQuality(out var qc) ? (int)qc : -1;
            r.violent = !p.WorkTagIsDisabled(WorkTags.Violent);
            if (p.apparel != null)
                foreach (var a in p.apparel.WornApparel)
                    r.armour = Mathf.Max(r.armour, a.GetStatValue(StatDefOf.ArmorRating_Sharp));
            if (!float.IsNaN(plan.low) && p.apparel != null)
            {
                clothes.TryGetValue(p, out var picks);
                var range = JourneyPack.ComfyWith(p, picks);
                r.clothesShort = CampWeatherMath.Shortfall(range.min, range.max, plan.low, plan.high);
            }
            r.capacity = MassUtility.Capacity(p);
            r.doctor = Does(p, WorkTypeDefOf.Doctor);
            r.cook = Does(p, Cooking);
            r.grower = Does(p, WorkTypeDefOf.Growing);
            r.warden = Does(p, WorkTypeDefOf.Warden);
            var office = OfficeTracker.HeldBy(p);
            r.office = office == null ? JourneyAdviceMath.NoOffice
                : office.defName == "Council_Office_Marshal" ? JourneyAdviceMath.Marshal
                : office.defName == "Council_Office_Speaker" ? JourneyAdviceMath.Speaker : JourneyAdviceMath.OtherOffice;
            try
            {
                r.babyAlone = p.relations != null && p.relations.Children.Any(c => c != null && !c.Dead && c.Spawned && c.Map == map && c.DevelopmentalStage.Baby()
                                                                                  && !c.relations.DirectRelations.Any(d => d.def == PawnRelationDefOf.Parent && d.otherPawn != p
                                                                                      && d.otherPawn != null && !d.otherPawn.Dead && d.otherPawn.Spawned && d.otherPawn.Map == map));
                r.loverStays = LovePartnerRelationUtility.ExistingLovePartners(p, false).Any(d => d.otherPawn != null && home.Contains(d.otherPawn) && !plan.travellers.Contains(d.otherPawn));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey advice: family at home", e);
            }
            return r;
        }

        // ---------------- Building it ----------------

        private static readonly AdviceGroup[] GroupOrder = { AdviceGroup.Recommended, AdviceGroup.CanGo, AdviceGroup.BetterNot };

        /// <summary>
        /// The advice for the plan as it stands: what it is for, each colonist and animal placed, the recommended, what the
        /// player's picks leave the home without, and what to bring. <paramref name="held"/> is what the stores hold and
        /// the travellers carry, <paramref name="stalls"/> what lies on the colony's stalls.
        /// </summary>
        public static JourneyAdviceModel Build(JourneyPlan plan, JourneyRoute route, Dictionary<ThingDef, (int stores, int carried)> held,
            Dictionary<ThingDef, List<(Thing stall, int count)>> stalls, Dictionary<Pawn, List<Apparel>> clothes, float load, float room)
        {
            var m = new JourneyAdviceModel();
            var map = plan?.map;
            if (map == null) return m;
            m.purpose = Purpose(plan);
            var pp = m.purpose;
            var home = HomePeople(map);
            var season = GenLocalDate.Season(map);
            m.wants = new JourneyWants
            {
                aim = pp.aim, danger = pp.danger, skill = pp.skill, named = pp.named?.thingIDNumber ?? -1, minPeople = pp.minPeople, days = plan.days,
                growing = season == Season.Spring || season == Season.Summer || season == Season.PermanentSummer,
                prisoners = map.mapPawns.PrisonersOfColonySpawnedCount > 0
            };
            m.wants.doctor = pp.rescue || JourneyAdviceMath.WantsDoctor(pp.aim, pp.danger, plan.days);
            m.aimWord = T("CC_JA_Aim_" + pp.aim);
            m.dangerWord = pp.danger == JourneyDanger.None ? null : T(pp.unknown ? "CC_JA_Danger_Unknown" : "CC_JA_Danger_" + pp.danger);
            m.dangerColor = pp.unknown ? UiKit.Soon : pp.danger == JourneyDanger.High ? UiKit.Danger : pp.danger == JourneyDanger.Some ? UiKit.Soon : UiKit.Warm;

            // The colonists.
            var reads = new List<JourneyAdviceMath.Person>();
            var byId = new Dictionary<int, Pawn>();
            foreach (var p in home)
            {
                try
                {
                    reads.Add(Read(p, plan, clothes ?? new Dictionary<Pawn, List<Apparel>>(), home));
                    byId[p.thingIDNumber] = p;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("journey advice: reading a colonist", e);
                }
            }
            m.grown = reads.Count(r => r.grown);
            m.advice = JourneyAdviceMath.Pick(reads, m.wants);
            foreach (int id in m.advice.recommended)
                if (byId.TryGetValue(id, out var p))
                    m.recommendedPeople.Add(p);
            var going = new HashSet<int>(plan.travellers.Where(p => p != null).Select(p => p.thingIDNumber));
            m.picked = JourneyAdviceMath.HomeLeft(reads, going, m.wants);
            var best = new int[JourneyAdviceMath.SkillCount];
            for (int i = 0; i < best.Length; i++) best[i] = JourneyAdviceMath.Best(reads, i);
            foreach (var r in reads)
            {
                var p = byId[r.id];
                var placed = m.advice.Of(r.id);
                var row = new JourneyPersonRow { pawn = p, person = r, group = placed.group, why = placed.why };
                row.reason = Reason(placed.why, p);
                Chips(row, reads, plan, pp, best, clothes);
                m.people.Add(row);
            }
            m.people.Sort((a, b) =>
            {
                int g = Array.IndexOf(GroupOrder, a.group).CompareTo(Array.IndexOf(GroupOrder, b.group));
                if (g != 0) return g;
                if (a.group == AdviceGroup.Recommended)
                    return m.advice.recommended.IndexOf(a.person.id).CompareTo(m.advice.recommended.IndexOf(b.person.id));
                return string.Compare(a.pawn.LabelShort, b.pawn.LabelShort, StringComparison.CurrentCultureIgnoreCase);
            });
            m.partyLine = T(m.advice.min < m.advice.ideal ? "CC_JA_Party" : "CC_JA_PartyOne", m.advice.recommended.Count.Named("COUNT"), m.grown.Named("GROWN"),
                m.advice.min.Named("MIN"));
            if (m.advice.warnings.Count > 0) m.recLeaves = T("CC_JA_RecLeaves", Warnings(m.advice.warnings).Named("WHAT"));

            // The animals.
            Beasts(m, plan, map, load, room);

            // The road's speed with this load, by the game's own caravan reckoning.
            try
            {
                var pawns = plan.travellers.Concat(plan.animals).Where(x => x != null && !x.Dead).ToList();
                if (pawns.Count > 0)
                {
                    int perMove = JourneyPack.TicksPerMove(pawns, JourneyPack.Mass(plan.travellers, plan.packing));
                    m.tilesPerDay = TilesPerDayCalculator.ApproxTilesPerDay(perMove, map.Tile, PlanetTile.Invalid);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey advice: the caravan's speed", e);
            }

            // What to bring.
            try
            {
                Items(m, plan, route, held, stalls, clothes, load, room);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journey advice: what to bring", e);
            }
            return m;
        }

        /// <summary>The warnings in words, joined.</summary>
        public static string Warnings(IEnumerable<HomeWarning> list) => TextUtil.JoinAnd(list.Select(w => T("CC_JA_Home_" + w)).ToList());

        private static string Reason(AdviceWhy why, Pawn p) => T("CC_JA_Why_" + why, p.Named("PAWN"));

        /// <summary>A pill: a word in a token's colour, its tooltip.</summary>
        private static UiKit.PillSpec Pill(string label, Color color, string tip = null) => new UiKit.PillSpec(label, color, null, tip);

        private static string Temp(float t) => float.IsNaN(t) ? "?" : t.ToStringTemperature("F0");

        /// <summary>A colonist's chips: health, mind, gear, the weather on the way, needs on the road, what the home loses, and the journey's skills (the colony's best marked).</summary>
        private static void Chips(JourneyPersonRow row, List<JourneyAdviceMath.Person> all, JourneyPlan plan, JourneyPurpose pp, int[] best, Dictionary<Pawn, List<Apparel>> clothes)
        {
            var p = row.pawn;
            var r = row.person;
            var s = row.state;
            // Now.
            if (p.Drafted) row.now = T("CC_JA_Now_Drafted");
            else if (p.GetLord() is Lord lord && !(lord.LordJob is LordJob_FormAndSendCaravan)) row.now = T("CC_JA_Now_Busy");
            // Health.
            string healthTip = T("CC_JA_Tip_Health", r.health.ToStringPercent().Named("HEALTH"), r.pain.ToStringPercent().Named("PAIN"), r.moving.ToStringPercent().Named("MOVING"),
                r.injuries.Named("INJURIES"), r.missing.Named("MISSING"));
            if (r.downed) s.Add(Pill(T("CC_JA_Chip_Downed"), UiKit.Danger, healthTip));
            else if (r.mental) s.Add(Pill(p.MentalStateDef?.label ?? T("CC_JA_Chip_Mental"), UiKit.Danger, healthTip));
            else if (r.wounds) s.Add(Pill(T("CC_JA_Chip_Wounds"), UiKit.Sour, healthTip));
            else if (r.inBed) s.Add(Pill(T("CC_JA_Chip_InBed"), UiKit.Sour, healthTip));
            else if (r.sick) s.Add(Pill(T("CC_JA_Chip_Sick"), UiKit.Sour, healthTip));
            else if (r.moving < 0.6f) s.Add(Pill(T("CC_JA_Chip_Slow"), UiKit.Sour, healthTip));
            else if (r.health < 0.6f) s.Add(Pill(T("CC_JA_Chip_Hurt"), UiKit.Sour, healthTip));
            else if (r.pain > 0.35f) s.Add(Pill(T("CC_JA_Chip_Pain"), UiKit.Soon, healthTip));
            else if (r.injuries > 0 || r.missing > 0 || r.health < 0.95f) s.Add(Pill(T("CC_JA_Chip_Scratched"), UiKit.Soon, healthTip));
            else s.Add(Pill(T("CC_JA_Chip_Well"), UiKit.Warm, healthTip));
            if (r.pregnant) s.Add(Pill(T("CC_JA_Chip_Pregnant"), UiKit.Soon));
            if (!r.grown) s.Add(Pill(T("CC_JA_Chip_Child", p.ageTracker.AgeBiologicalYears.Named("AGE")), UiKit.Cool));
            // Mind.
            var band = (StrainBand)Mathf.Clamp(r.band, 0, 3);
            string mindTip = T("CC_JA_Tip_Mind", r.mood.ToStringPercent().Named("MOOD"), r.minor.ToStringPercent().Named("MINOR"), HeartView.BandLabel(band).Named("BAND"));
            if (!r.mental && r.mood < r.minor + 0.05f) s.Add(Pill(T("CC_JA_Chip_NearBreak"), UiKit.Sour, mindTip));
            else s.Add(Pill(HeartView.BandLabel(band), HeartView.BandColor(band), mindTip));
            // Gear.
            var weapon = p.equipment?.Primary;
            if (!r.violent) s.Add(Pill(T("CC_JA_Chip_NoViolence"), pp.aim == JourneyAim.Fight ? UiKit.Soon : UiKit.Faint));
            else if (weapon != null)
            {
                string q = weapon.TryGetQuality(out var qc) ? T("CC_JA_Chip_WeaponQ", weapon.def.label.Named("WEAPON"), qc.GetLabel().Named("QUALITY")) : weapon.def.label;
                s.Add(Pill(q, UiKit.Faint, weapon.LabelCap));
            }
            else s.Add(Pill(T("CC_JA_Chip_Unarmed"), pp.aim == JourneyAim.Fight || pp.danger >= JourneyDanger.Some ? UiKit.Soon : UiKit.Faint));
            if (r.armour >= 0.4f) s.Add(Pill(T("CC_JA_Chip_Armour"), UiKit.Faint, T("CC_JA_Tip_Armour", r.armour.ToStringPercent().Named("SHARP"))));
            // The weather on the way.
            if (!float.IsNaN(plan.low) && r.clothesShort > 0.5f && p.apparel != null)
            {
                bool traveller = plan.travellers.Contains(p);
                List<Apparel> picks = null;
                clothes?.TryGetValue(p, out picks);
                var range = JourneyPack.ComfyWith(p, picks);
                bool cold = range.min - plan.low >= plan.high - range.max;
                string word = cold ? T("CC_JA_Chip_Cold", Temp(range.min).Named("TEMP")) : T("CC_JA_Chip_Hot", Temp(range.max).Named("TEMP"));
                s.Add(Pill(word, UiKit.Soon, T(traveller ? "CC_JA_Tip_ClothesGoing" : "CC_JA_Tip_ClothesHome", Temp(plan.low).Named("LOW"), Temp(plan.high).Named("HIGH"),
                    Temp(range.min).Named("MIN"), Temp(range.max).Named("MAX"))));
            }
            // Needs on the road.
            if (r.addicted) s.Add(Pill(T("CC_JA_Chip_Addicted", Chemicals(p).Named("WHAT")), UiKit.Soon, T("CC_JA_Tip_Addicted")));
            s.Add(Pill(T("CC_JA_Chip_Carries", r.capacity.ToString("0").Named("KG")), UiKit.Faint, T("CC_JA_Tip_Carries")));
            // What the home loses.
            if (r.doctor && !all.Any(o => o.id != r.id && o.doctor)) s.Add(Pill(T("CC_JA_Chip_OnlyDoctor"), UiKit.Soon));
            if (r.cook && !all.Any(o => o.id != r.id && o.cook)) s.Add(Pill(T("CC_JA_Chip_OnlyCook"), UiKit.Soon));
            if (r.grower && !all.Any(o => o.id != r.id && o.grower)) s.Add(Pill(T("CC_JA_Chip_OnlyGrower"), UiKit.Faint));
            if (r.warden && !all.Any(o => o.id != r.id && o.warden) && plan.map.mapPawns.PrisonersOfColonySpawnedCount > 0) s.Add(Pill(T("CC_JA_Chip_OnlyWarden"), UiKit.Soon));
            if (r.office != JourneyAdviceMath.NoOffice) s.Add(Pill(OfficeTracker.HeldBy(p)?.label ?? T("CC_JA_Chip_Office"), UiKit.Gold, T("CC_JA_Tip_Office")));
            if (r.babyAlone) s.Add(Pill(T("CC_JA_Chip_Baby"), UiKit.Soon));
            if (r.loverStays) s.Add(Pill(T("CC_JA_Chip_Lover"), UiKit.Faint, T("CC_JA_Tip_Lover")));
            // The journey's skills, the colony's best marked.
            var shown = new List<int> { JourneyAdviceMath.Shooting, JourneyAdviceMath.Melee, JourneyAdviceMath.Medicine, JourneyAdviceMath.Social, JourneyAdviceMath.Animals };
            if (pp.skill >= 0 && !shown.Contains(pp.skill)) shown.Add(pp.skill);
            foreach (int i in shown)
            {
                int level = r.Skill(i);
                if (level < 0) continue;
                bool top = JourneyAdviceMath.IsBest(level, best[i]);
                var def = SkillOf(i);
                row.skills.Add(Pill(T("CC_JA_Skill", def.skillLabel.CapitalizeFirst().Named("SKILL"), level.Named("LEVEL")), top ? UiKit.Gold : UiKit.Faint,
                    top ? T("CC_JA_Tip_Best", def.skillLabel.Named("SKILL")) : null));
            }
            row.tip = T("CC_JA_Tip_Row", p.LabelShortCap.Named("PAWN"), row.reason.Named("REASON")) + "\n\n" + healthTip + "\n" + mindTip;
        }

        /// <summary>The drugs someone depends on, by their chemicals' names.</summary>
        private static string Chemicals(Pawn p)
        {
            var names = new List<string>();
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs != null)
                foreach (var a in hediffs.OfType<Hediff_Addiction>())
                    if (a.Chemical != null && !names.Contains(a.Chemical.label))
                        names.Add(a.Chemical.label);
            var genes = p.genes?.GenesListForReading;
            if (genes != null)
                foreach (var g in genes.OfType<Gene_ChemicalDependency>())
                    if (g.Active && g.def?.chemical != null && !names.Contains(g.def.chemical.label))
                        names.Add(g.def.chemical.label);
            return names.Count == 0 ? "?" : TextUtil.JoinAnd(names);
        }

        // ---------------- Animals ----------------

        private static void Beasts(JourneyAdviceModel m, JourneyPlan plan, Map map, float load, float room)
        {
            var animals = HomeAnimals(map);
            var reads = new List<JourneyAdviceMath.Beast>();
            var byId = new Dictionary<int, Pawn>();
            foreach (var a in animals)
            {
                try
                {
                    bool hurt = a.Downed || a.health.HasHediffsNeedingTend() || a.health.hediffSet.AnyHediffMakesSickThought || a.health.summaryHealth.SummaryHealthPercent < 0.6f
                                || (a.health.capacities?.GetLevel(PawnCapacityDefOf.Moving) ?? 1f) < 0.6f;
                    bool pregnant = a.health.hediffSet.HasHediff(HediffDefOf.Pregnant);
                    bool young = a.ageTracker.CurLifeStageIndex < a.RaceProps.lifeStageAges.Count - 1;
                    float ride = 1f;
                    bool rideable = false;
                    if (!a.Downed && a.ageTracker.CurLifeStage?.caravanRideable == true)
                    {
                        ride = a.GetStatValue(StatDefOf.CaravanRidingSpeedFactor);
                        rideable = ride > 1f;
                    }
                    reads.Add(new JourneyAdviceMath.Beast
                    {
                        id = a.thingIDNumber, pack = a.RaceProps.packAnimal, rideable = rideable, ride = ride, young = young, pregnant = pregnant, hurt = hurt,
                        capacity = MassUtility.CanEverCarryAnything(a) ? MassUtility.Capacity(a) : 0f
                    });
                    byId[a.thingIDNumber] = a;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("journey advice: reading an animal", e);
                }
            }
            // The people's own room: of the travellers, or of the recommended while nobody is picked.
            var people = plan.travellers.Count > 0 ? plan.travellers : m.recommendedPeople;
            float peopleRoom = people.Where(p => p != null && !p.Dead).Sum(p => Mathf.Max(0f, MassUtility.FreeSpace(p)));
            // The load against the people's room alone: the animals picked aren't counted, so the advice stands whoever is picked.
            float total = Mathf.Max(load, 0f);
            var placed = JourneyAdviceMath.PickBeasts(reads, total, peopleRoom, people.Count);
            foreach (var b in placed)
            {
                if (!byId.TryGetValue(b.id, out var a)) continue;
                var row = new JourneyBeastRow { pawn = a, group = b.group, why = b.why, reason = Reason(b.why, a) };
                var read = reads.First(x => x.id == b.id);
                if (read.pack) row.chips.Add(Pill(T("CC_JA_Chip_Pack", read.capacity.ToString("0").Named("KG")), UiKit.Faint));
                if (read.rideable) row.chips.Add(Pill(T("CC_JA_Chip_Mount", (read.ride - 1f).ToStringPercent("F0").Named("FASTER")), UiKit.Faint, T("CC_JA_Tip_Mount")));
                if (read.pregnant) row.chips.Add(Pill(T("CC_JA_Chip_WithYoung"), UiKit.Soon));
                if (read.hurt) row.chips.Add(Pill(T("CC_JA_Chip_AnimalHurt"), UiKit.Sour));
                var bond = a.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
                if (bond != null) row.chips.Add(Pill(T("CC_JA_Chip_Bonded", bond.LabelShort.Named("PAWN")), UiKit.Faint));
                row.tip = row.reason;
                m.beasts.Add(row);
                if (b.group == AdviceGroup.Recommended) m.recommendedBeasts.Add(a);
            }
            m.beasts.Sort((x, y) =>
            {
                int g = Array.IndexOf(GroupOrder, x.group).CompareTo(Array.IndexOf(GroupOrder, y.group));
                return g != 0 ? g : string.Compare(x.pawn.LabelShort, y.pawn.LabelShort, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        // ---------------- What to bring ----------------

        /// <summary>How many of a thing the list holds (taken lines of any kind).</summary>
        private static int OnList(JourneyPlan plan, ThingDef def, Func<JourneyLine, bool> also = null) =>
            plan.packing.Where(l => l.Taken && l.def == def && (also == null || also(l))).Sum(l => l.count);

        private static int Stores(Dictionary<ThingDef, (int stores, int carried)> held, ThingDef def) => def != null && held.TryGetValue(def, out var h) ? h.stores + h.carried : 0;

        private static int OnStalls(Dictionary<ThingDef, List<(Thing stall, int count)>> stalls, ThingDef def, out string first)
        {
            first = null;
            if (def == null || stalls == null || !stalls.TryGetValue(def, out var list) || list.Count == 0) return 0;
            first = list[0].stall.LabelShortCap;
            return list.Sum(x => x.count);
        }

        /// <summary>A recommendation for this many more of a thing, the stores first and then the stalls; none when neither has any.</summary>
        private static void Recommend(JourneyAdviceModel m, JourneyPlan plan, Dictionary<ThingDef, (int stores, int carried)> held,
            Dictionary<ThingDef, List<(Thing stall, int count)>> stalls, ThingDef def, string kind, int more, string why)
        {
            if (def == null || more <= 0) return;
            int stores = Stores(held, def);
            int onStall = OnStalls(stalls, def, out string stallName);
            int listed = OnList(plan, def);
            // What the list holds already comes out of the same stores and stalls, the stores first.
            int can = Mathf.Min(more, Mathf.Max(0, stores + onStall - listed));
            if (can <= 0) return;
            var stallCounts = new[] { onStall };
            int stallPart = JourneyAdviceMath.TakeFrom(listed + can, stores, stallCounts).stalls[0] - JourneyAdviceMath.TakeFrom(listed, stores, stallCounts).stalls[0];
            m.items.Add(new JourneyItemRec { def = def, kind = kind, count = can, why = why, fromStall = stallPart, stallName = stallPart > 0 ? stallName : null });
        }

        private static void Items(JourneyAdviceModel m, JourneyPlan plan, JourneyRoute route, Dictionary<ThingDef, (int stores, int carried)> held,
            Dictionary<ThingDef, List<(Thing stall, int count)>> stalls, Dictionary<Pawn, List<Apparel>> clothes, float load, float room)
        {
            var map = plan.map;
            var pp = m.purpose;
            var people = plan.travellers.Where(p => p != null && !p.Dead).ToList();
            int n = people.Count;
            string dest = plan.Dest;
            if (n == 0) return;
            bool fight = pp.aim == JourneyAim.Fight;

            // Medicine by the party and the danger, the plainest kind the list or the stores have first.
            int medWant = JourneyAdviceMath.MedicineFor(n, plan.days, pp.danger, fight || pp.rescue);
            int medListed = plan.packing.Where(l => l.Taken && l.def.IsMedicine).Sum(l => l.count);
            if (medWant > medListed)
            {
                var med = plan.packing.FirstOrDefault(l => l.Taken && l.kind == "medicine")?.def
                          ?? held.Keys.Concat(stalls.Keys).Where(d => d.IsMedicine && Stores(held, d) + OnStalls(stalls, d, out _) > 0)
                              .OrderBy(d => d.GetStatValueAbstract(StatDefOf.MedicalPotency) >= 1.5f ? 1 : 0).ThenByDescending(d => d.GetStatValueAbstract(StatDefOf.MedicalPotency))
                              .FirstOrDefault();
                Recommend(m, plan, held, stalls, med, "medicine", medWant - medListed,
                    T(fight || pp.danger >= JourneyDanger.Some ? "CC_JA_Item_MedicineDanger" : pp.rescue ? "CC_JA_Item_MedicineRescue" : "CC_JA_Item_Medicine", n.Named("COUNT")));
            }

            // Trade: silver to buy with, and goods to sell, within the room left.
            if (pp.aim == JourneyAim.Trade)
            {
                int silver = held.TryGetValue(ThingDefOf.Silver, out var cash) ? cash.stores : 0;
                int want = JourneyAdviceMath.SilverFor(silver);
                Recommend(m, plan, held, stalls, ThingDefOf.Silver, "other", JourneyAdviceMath.StillToAdd(want, OnList(plan, ThingDefOf.Silver)), T("CC_JA_Item_Silver", dest.Named("PLACE")));
                if (pp.requestDef != null)
                    Recommend(m, plan, held, stalls, pp.requestDef, JourneyPack.KindFor(pp.requestDef) == "food" ? "other" : JourneyPack.KindFor(pp.requestDef),
                        JourneyAdviceMath.StillToAdd(pp.requestCount, OnList(plan, pp.requestDef)), T("CC_JA_Item_Request", dest.Named("PLACE"), pp.requestCount.Named("COUNT")));
                var defs = new List<ThingDef>();
                var goods = new List<JourneyAdviceMath.Goods>();
                foreach (var kv in stalls)
                {
                    var d = kv.Key;
                    if (d == null || d == ThingDefOf.Silver || defs.Contains(d)) continue;
                    defs.Add(d);
                    goods.Add(new JourneyAdviceMath.Goods { mass = JourneyPack.UnitMass(d), value = d.BaseMarketValue, count = kv.Value.Sum(x => x.count) - OnList(plan, d), stall = true });
                }
                foreach (var kv in held)
                {
                    var d = kv.Key;
                    if (d == null || defs.Contains(d) || !d.IsArt || kv.Value.stores <= 0) continue;
                    defs.Add(d);
                    goods.Add(new JourneyAdviceMath.Goods { mass = JourneyPack.UnitMass(d), value = d.BaseMarketValue, count = kv.Value.stores - OnList(plan, d) });
                }
                foreach (var (i, count) in JourneyAdviceMath.PickGoods(goods, room - load))
                    Recommend(m, plan, held, stalls, defs[i], "other", count, T(goods[i].stall ? "CC_JA_Item_GoodsStall" : "CC_JA_Item_GoodsArt", dest.Named("PLACE")));
            }

            // A weapon for each traveller who has none, where there will be fighting.
            if (fight || pp.aim == JourneyAim.Escort || pp.danger >= JourneyDanger.Some)
            {
                var unarmed = people.Where(p => p.equipment != null && p.equipment.Primary == null && !p.WorkTagIsDisabled(WorkTags.Violent) && !p.DevelopmentalStage.Juvenile()).ToList();
                int armedAlready = plan.packing.Where(l => l.Taken && l.kind == "arm").Sum(l => l.count);
                var stored = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                    .Where(t => t.Spawned && t.def.IsWeapon && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage() && !t.Position.Fogged(map)
                                && t.TryGetComp<CompBiocodable>()?.Biocoded != true).ToList();
                var taken = new HashSet<Thing>();
                var picks = new Dictionary<ThingDef, List<Pawn>>();
                foreach (var p in unarmed.Skip(armedAlready))
                {
                    bool shooter = (p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0) >= (p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0);
                    var w = stored.Where(t => !taken.Contains(t) && EquipmentUtility.CanEquip(t, p))
                        .OrderByDescending(t => t.def.IsRangedWeapon == shooter).ThenByDescending(t => t.MarketValue).FirstOrDefault();
                    if (w == null) continue;
                    taken.Add(w);
                    if (!picks.TryGetValue(w.def, out var who)) picks[w.def] = who = new List<Pawn>();
                    who.Add(p);
                }
                foreach (var kv in picks)
                    Recommend(m, plan, held, stalls, kv.Key, "arm", kv.Value.Count, T("CC_JA_Item_Arm", RoadFood.Names(kv.Value).Named("WHO")));
            }

            // Notes without an act: bedrolls to make, the weather the stores can't dress for, an addiction the stores can't feed.
            if (plan.days >= JourneyMath.NightFrom && !plan.packing.Any(l => l.Taken && (l.kind == "bed" || l.kind == "tent")))
            {
                var bedroll = ThingDefOf.Bedroll;
                if (bedroll != null && Stores(held, bedroll) + OnStalls(stalls, bedroll, out _) > 0)
                    Recommend(m, plan, held, stalls, bedroll, "bed", n, T("CC_JA_Item_Bedrolls"));
                else if (bedroll != null && Avail.CanBuild(bedroll, map)) m.notes.Add(T("CC_JA_Note_Bedrolls"));
            }
            if (!float.IsNaN(plan.low))
                foreach (var p in people.Where(p => p.apparel != null && p.RaceProps.Humanlike))
                {
                    List<Apparel> picks = null;
                    clothes?.TryGetValue(p, out picks);
                    var range = JourneyPack.ComfyWith(p, picks);
                    if (CampWeatherMath.Shortfall(range.min, range.max, plan.low, plan.high) <= 0.5f) continue;
                    bool cold = range.min - plan.low >= plan.high - range.max;
                    bool dressing = ColonyCouncilMod.Settings?.journeyClothes ?? true;
                    m.notes.Add(T(dressing ? cold ? "CC_JA_Note_Cold" : "CC_JA_Note_Hot" : "CC_JA_Note_DressingOff", p.LabelShort.Named("PAWN"),
                        Temp(cold ? plan.low : plan.high).Named("TEMP")));
                }
            foreach (var p in people)
            {
                var hediffs = p.health?.hediffSet?.hediffs;
                if (hediffs == null) continue;
                foreach (var a in hediffs.OfType<Hediff_Addiction>())
                {
                    var chem = a.Chemical;
                    if (chem == null) continue;
                    bool listed = plan.packing.Any(l => l.Taken && l.def.GetCompProperties<CompProperties_Drug>()?.chemical == chem);
                    if (!listed) m.notes.Add(T("CC_JA_Note_NoDrug", p.LabelShort.Named("PAWN"), chem.label.Named("CHEM")));
                }
            }
        }

        /// <summary>
        /// The recommended things put on the list, by the player's press: a line of the planner's raised to the number (the
        /// player's number then), or a new line of the player's. Never fewer than the list holds.
        /// </summary>
        public static void Apply(JourneyPlan plan, JourneyItemRec rec)
        {
            if (plan == null || rec?.def == null || rec.count <= 0) return;
            var line = plan.packing.FirstOrDefault(l => l.def == rec.def && l.kind == rec.kind && !l.removed) ?? plan.packing.FirstOrDefault(l => l.def == rec.def && l.kind == rec.kind);
            if (line == null)
            {
                plan.packing.Add(new JourneyLine(rec.def, rec.count, rec.kind) { added = true });
                return;
            }
            if (line.removed)
            {
                line.removed = false;
                line.count = 0;
            }
            line.count += rec.count;
            if (!line.added) line.edited = true;
        }
    }
}
