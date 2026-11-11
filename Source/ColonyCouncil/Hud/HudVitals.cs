using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's vitals in short lines: days of food (the advisor's count: meals and raw food at the colony's rate),
    /// medicine, the power grid's balance, the outdoor temperature and the next season, days since the last raid, and
    /// the research under way. Each goes orange or red when it runs low (food by the settings' lines, which start at the
    /// advisor's own; medicine at the advisor's two per colonist). A click goes where it can be dealt with: the advisor,
    /// the temperature overlay, the defenses, the research tab.
    /// </summary>
    public class HudVitals : HudLinesPanel
    {
        public const string PartFood = "food", PartMedicine = "medicine", PartPower = "power", PartTemperature = "temperature", PartRaid = "raid",
            PartResearch = "research";
        public static readonly string[] Parts = { PartFood, PartMedicine, PartPower, PartTemperature, PartRaid, PartResearch };

        /// <summary>The advisor's line for medicine: two for each colonist (Advisor/AdvisorSignals.cs).</summary>
        public const int MedicinePerColonist = 2;

        private int seasonHour = -1;
        private Map seasonMap;
        private Season nextSeason;
        private int seasonDays = -1;
        private float seasonTemp;

        // The colonists' own comfort ranges, dressed as they are (the game's, which its cold and hot moods use), taken
        // once a rare tick. The human comfort range the scale is set around is TemperatureScale's.
        private int comfortTick = -1;
        private Map comfortMap;
        private readonly System.Collections.Generic.List<FloatRange> comfort = new System.Collections.Generic.List<FloatRange>();

        public HudVitals() : base(HudLayout.Vitals)
        {
        }

        public override string[] PartNames => Parts;

        protected override string Empty => HudKit.T("CC_Pnl_NoMap");

        /// <summary>The map the vitals are for: the one on screen if it's a home, else the colony (its seat, Homes).</summary>
        public static Map MapFor()
        {
            var cur = Find.CurrentMap;
            if (cur != null && cur.IsPlayerHome) return cur;
            return MapFacts.BusiestHome;
        }

        protected override void Gather()
        {
            var map = MapFor();
            if (map == null) return;
            var facts = MapFacts.Of(map);
            int colonists = facts.Colonists;
            Food(map, facts);
            Medicine(map, facts, colonists);
            Power(map);
            Temperature(map);
            Raid(map);
            Research();
        }

        private void Food(Map map, MapFacts facts)
        {
            float days = facts.FoodDays;
            float warn = Mathf.Max(S.hudFoodWarnDays, S.hudFoodBadDays), bad = S.hudFoodBadDays;
            bool none = days >= 900f;
            string value = none ? "-" : "CC_Pnl_DaysValue".Translate(days < 10f ? days.ToString("0.#") : days.ToString("0")).Resolve();
            string tip = Tip("CC_Pnl_FoodTip".Translate(days.ToString("0.#"), warn.ToString("0"), bad.ToString("0")).Resolve(), HudKit.T("CC_Pnl_ClickAdvisor"));
            var line = Add(PartFood, Icons.CatFood, HudKit.T("CC_Pnl_Food"), value, none ? 10f : HudRank.Food(days, warn, bad), tip, () => MainTabWindow_Council.OpenTab("Priorities"));
            // Rimfolk's look: the days as a bar, the warning line at its middle and the bad one before it.
            if (!none)
            {
                line.fill = HudLookMath.FoodFill(days, warn);
                line.tickA = HudLookMath.FoodLine(warn, warn);
                line.tickB = HudLookMath.FoodLine(bad, warn);
            }
        }

        private void Medicine(Map map, MapFacts facts, int colonists)
        {
            int meds = facts.Medicine;
            bool tending = false;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
                if (p.health != null && p.health.HasHediffsNeedingTend())
                {
                    tending = true;
                    break;
                }
            string tip = Tip("CC_Pnl_MedicineTip".Translate(meds, colonists * MedicinePerColonist).Resolve() + (tending ? "\n" + HudKit.T("CC_Pnl_MedicineTending") : ""),
                HudKit.T("CC_Pnl_ClickAdvisor"));
            var line = Add(PartMedicine, Icons.CatHealth, HudKit.T("CC_Pnl_Medicine"), meds.ToString(), HudRank.Medicine(meds, colonists, tending, MedicinePerColonist), tip,
                () => MainTabWindow_Council.OpenTab("Priorities"));
            // Rimfolk's look: the count as a bar, the advisor's line (two for each colonist) at its middle.
            if (colonists > 0)
            {
                line.fill = HudLookMath.StockFill(meds, colonists * MedicinePerColonist);
                line.tickA = 0.5f;
            }
        }

        private void Power(Map map)
        {
            var nets = map.powerNetManager?.AllNetsListForReading;
            if (nets == null) return;
            float gain = 0f, stored = 0f;
            bool any = false;
            foreach (var net in nets)
            {
                if (net == null || !net.hasPowerSource) continue;
                any = true;
                gain += net.CurrentEnergyGainRate();
                stored += net.CurrentStoredEnergy();
            }
            if (!any) return;
            float watts = gain / CompPower.WattsToWattDaysPerTick;
            string value = (watts >= 0f ? "+" : "-") + Mathf.Abs(watts).ToString("0") + " W";
            string tip = Tip("CC_Pnl_PowerTip".Translate(watts.ToString("0"), stored.ToString("0")).Resolve(), null);
            Add(PartPower, HudIcons.Power ?? Icons.Warning, HudKit.T("CC_Pnl_Power"), value, HudRank.Power(watts, stored), tip, null);
        }

        /// <summary>
        /// The outdoor temperature, its value coloured on the temperature scale from deep blue to deep red (TemperatureScale,
        /// around the game's human comfort range: the same colours as the game's own readout), the row orange or red as
        /// before; the tooltip names its band (HudRank.Band) and says how the colonists on the map, dressed as they are,
        /// would find it.
        /// </summary>
        private void Temperature(Map map)
        {
            float t = map.mapTemperature.OutdoorTemp;
            int ticks = Find.TickManager?.TicksGame ?? 0;
            int hour = ticks / GenDate.TicksPerHour;
            if (hour != seasonHour || map != seasonMap)
            {
                seasonHour = hour;
                seasonMap = map;
                NextSeason(map);
            }
            if (ticks / Sched.RareTicks != comfortTick || map != comfortMap)
            {
                comfortTick = ticks / Sched.RareTicks;
                comfortMap = map;
                Comfort(map);
            }
            string text = HudKit.T("CC_Pnl_Outside");
            string seasonLine = null;
            if (seasonDays > 0 && nextSeason != Season.Undefined)
            {
                seasonLine = "CC_Pnl_SeasonIn".Translate(SeasonUtility.LabelCap(nextSeason), seasonDays).Resolve();
                text = seasonLine;
            }
            var band = HudRank.Band(t, TemperatureScale.HumanMin, TemperatureScale.HumanMax);
            string tip = "CC_Pnl_TempTip".Translate(t.ToStringTemperature("F0"), HudKit.T("CC_Pnl_TempBand_" + band)).Resolve();
            string colonists = ColonistsComfort(t);
            if (colonists != null) tip += "\n" + colonists;
            if (seasonLine != null) tip += "\n" + "CC_Pnl_SeasonTip".Translate(SeasonUtility.Label(nextSeason), seasonDays, seasonTemp.ToStringTemperature("F0")).Resolve();
            var line = Add(PartTemperature, HudIcons.Temperature ?? Icons.World, text, t.ToStringTemperature("F0"), HudRank.Temperature(t), Tip(tip, HudKit.T("CC_Pnl_ClickOverlay")),
                () =>
                {
                    var ps = Find.PlaySettings;
                    if (ps != null) ps.showTemperatureOverlay = !ps.showTemperatureOverlay;
                });
            line.valueColor = TemperatureScale.Of(t);
        }

        /// <summary>Each free colonist's comfort range on the map, dressed as they are.</summary>
        private void Comfort(Map map)
        {
            comfort.Clear();
            try
            {
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (p?.RaceProps?.Humanlike == true)
                        comfort.Add(p.ComfortableTemperatureRange());
            }
            catch (Exception e)
            {
                comfort.Clear();
                CouncilLog.ErrorOnce("vitals: the colonists' comfort", e);
            }
        }

        /// <summary>"Too cold for 2 of the 6 colonists on the map, dressed as they are.", or comfortable for them all; null with nobody there.</summary>
        private string ColonistsComfort(float t)
        {
            int all = comfort.Count, cold = 0, hot = 0;
            if (all == 0) return null;
            foreach (var r in comfort)
            {
                if (t < r.min) cold++;
                else if (t > r.max) hot++;
            }
            if (cold == 0 && hot == 0) return HudKit.T("CC_Pnl_TempComfy");
            string s = null;
            if (cold > 0) s = cold == all ? HudKit.T("CC_Pnl_TempColdAll") : "CC_Pnl_TempColdSome".Translate(cold, all).Resolve();
            if (hot > 0) s = (s == null ? "" : s + "\n") + (hot == all ? HudKit.T("CC_Pnl_TempHotAll") : "CC_Pnl_TempHotSome".Translate(hot, all).Resolve());
            return s;
        }

        /// <summary>When the season changes next at the map's latitude, and the average temperature then (once an hour).</summary>
        private void NextSeason(Map map)
        {
            seasonDays = -1;
            nextSeason = Season.Undefined;
            try
            {
                var now = GenLocalDate.Season(map);
                if (now == Season.PermanentSummer || now == Season.PermanentWinter || now == Season.Undefined) return;
                var longLat = Find.WorldGrid.LongLatOf(map.Tile);
                long abs = Find.TickManager.TicksAbs;
                for (int d = 1; d <= GenDate.DaysPerYear / 2; d++)
                {
                    long at = abs + (long)d * GenDate.TicksPerDay;
                    var s = GenDate.Season(at, longLat);
                    if (s == now) continue;
                    nextSeason = s;
                    seasonDays = d;
                    seasonTemp = GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, GenDate.Twelfth(at, longLat.x));
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("vitals: next season", e);
            }
        }

        private void Raid(Map map)
        {
            int last = map.storyState?.LastThreatBigTick ?? -1;
            int now = Find.TickManager.TicksGame;
            string value, tip;
            if (last <= 0 || last > now)
            {
                value = "-";
                tip = HudKit.T("CC_Pnl_RaidNone");
            }
            else
            {
                float days = (now - last) / (float)GenDate.TicksPerDay;
                value = "CC_Pnl_DaysValue".Translate(days < 10f ? days.ToString("0.#") : days.ToString("0")).Resolve();
                tip = "CC_Pnl_RaidTip".Translate(days.ToString("0.#")).Resolve();
            }
            Add(PartRaid, Icons.Raid, HudKit.T("CC_Pnl_Raid"), value, 10f, Tip(tip, HudKit.T("CC_Pnl_ClickDefenses")), () => MainTabWindow_Council.OpenTab("Defenses"));
        }

        private void Research()
        {
            var rm = Find.ResearchManager;
            if (rm == null) return;
            ResearchProjectDef proj = null;
            bool available = false;
            try
            {
                proj = rm.GetProject();
                available = rm.AnyProjectIsAvailable;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("vitals: research", e);
            }
            Action open = () =>
            {
                var tab = MainButtonDefOf.Research;
                if (tab != null) Find.MainTabsRoot.SetCurrentTab(tab);
            };
            if (proj != null)
            {
                var line = Add(PartResearch, HudIcons.Research ?? Icons.Almanac, proj.LabelCap, proj.ProgressPercent.ToStringPercent(), HudRank.Research(true, available),
                    Tip("CC_Pnl_ResearchTip".Translate(proj.LabelCap, proj.ProgressPercent.ToStringPercent()).Resolve(), HudKit.T("CC_Pnl_ClickResearch")), open);
                // Rimfolk's look: how far along, as a bar.
                line.fill = proj.ProgressPercent;
                line.fillColor = UiKit.Gold;
            }
            else if (available)
                Add(PartResearch, HudIcons.Research ?? Icons.Almanac, HudKit.T("CC_Pnl_NoResearch"), null, HudRank.Research(false, true),
                    Tip(HudKit.T("CC_Pnl_NoResearchTip"), HudKit.T("CC_Pnl_ClickResearch")), open);
        }

        protected override void MenuOptions(System.Collections.Generic.List<FloatMenuOption> options)
        {
            options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_OpenAdvisor"), () => MainTabWindow_Council.OpenTab("Priorities")));
        }
    }
}
