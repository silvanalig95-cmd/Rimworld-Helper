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
        private Season nextSeason;
        private int seasonDays = -1;
        private float seasonTemp;

        public HudVitals() : base(HudLayout.Vitals)
        {
        }

        public override string[] PartNames => Parts;

        protected override string Empty => "CC_Pnl_NoMap".Translate();

        /// <summary>The map the vitals are for: the one on screen if it's a home, else the busiest home.</summary>
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
            string tip = Tip("CC_Pnl_FoodTip".Translate(days.ToString("0.#"), warn.ToString("0"), bad.ToString("0")).Resolve(), "CC_Pnl_ClickAdvisor".Translate());
            Add(PartFood, Icons.CatFood, "CC_Pnl_Food".Translate(), value, none ? 10f : HudRank.Food(days, warn, bad), tip, () => MainTabWindow_Council.OpenTab("Priorities"));
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
            string tip = Tip("CC_Pnl_MedicineTip".Translate(meds, colonists * MedicinePerColonist).Resolve() + (tending ? "\n" + "CC_Pnl_MedicineTending".Translate() : ""),
                "CC_Pnl_ClickAdvisor".Translate());
            Add(PartMedicine, Icons.CatHealth, "CC_Pnl_Medicine".Translate(), meds.ToString(), HudRank.Medicine(meds, colonists, tending, MedicinePerColonist), tip,
                () => MainTabWindow_Council.OpenTab("Priorities"));
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
            string value = (watts >= 0f ? "+" : "−") + Mathf.Abs(watts).ToString("0") + " W";
            string tip = Tip("CC_Pnl_PowerTip".Translate(watts.ToString("0"), stored.ToString("0")).Resolve(), null);
            Add(PartPower, HudIcons.Power ?? Icons.Warning, "CC_Pnl_Power".Translate(), value, HudRank.Power(watts, stored), tip, null);
        }

        private void Temperature(Map map)
        {
            float t = map.mapTemperature.OutdoorTemp;
            int hour = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerHour;
            if (hour != seasonHour)
            {
                seasonHour = hour;
                NextSeason(map);
            }
            string text = "CC_Pnl_Outside".Translate();
            string seasonLine = null;
            if (seasonDays > 0 && nextSeason != Season.Undefined)
            {
                seasonLine = "CC_Pnl_SeasonIn".Translate(SeasonUtility.LabelCap(nextSeason), seasonDays).Resolve();
                text = seasonLine;
            }
            string tip = "CC_Pnl_TempTip".Translate(t.ToStringTemperature("F0")).Resolve();
            if (seasonLine != null) tip += "\n" + "CC_Pnl_SeasonTip".Translate(SeasonUtility.Label(nextSeason), seasonDays, seasonTemp.ToStringTemperature("F0")).Resolve();
            Add(PartTemperature, HudIcons.Temperature ?? Icons.World, text, t.ToStringTemperature("F0"), HudRank.Temperature(t), Tip(tip, "CC_Pnl_ClickOverlay".Translate()),
                () =>
                {
                    var ps = Find.PlaySettings;
                    if (ps != null) ps.showTemperatureOverlay = !ps.showTemperatureOverlay;
                });
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
                tip = "CC_Pnl_RaidNone".Translate();
            }
            else
            {
                float days = (now - last) / (float)GenDate.TicksPerDay;
                value = "CC_Pnl_DaysValue".Translate(days < 10f ? days.ToString("0.#") : days.ToString("0")).Resolve();
                tip = "CC_Pnl_RaidTip".Translate(days.ToString("0.#")).Resolve();
            }
            Add(PartRaid, Icons.Raid, "CC_Pnl_Raid".Translate(), value, 10f, Tip(tip, "CC_Pnl_ClickDefenses".Translate()), () => MainTabWindow_Council.OpenTab("Defenses"));
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
                Add(PartResearch, HudIcons.Research ?? Icons.Almanac, proj.LabelCap, proj.ProgressPercent.ToStringPercent(), HudRank.Research(true, available),
                    Tip("CC_Pnl_ResearchTip".Translate(proj.LabelCap, proj.ProgressPercent.ToStringPercent()).Resolve(), "CC_Pnl_ClickResearch".Translate()), open);
            else if (available)
                Add(PartResearch, HudIcons.Research ?? Icons.Almanac, "CC_Pnl_NoResearch".Translate(), null, HudRank.Research(false, true),
                    Tip("CC_Pnl_NoResearchTip".Translate(), "CC_Pnl_ClickResearch".Translate()), open);
        }

        protected override void MenuOptions(System.Collections.Generic.List<FloatMenuOption> options)
        {
            options.Add(new FloatMenuOption("CC_Pnl_OpenAdvisor".Translate(), () => MainTabWindow_Council.OpenTab("Priorities")));
        }
    }
}
