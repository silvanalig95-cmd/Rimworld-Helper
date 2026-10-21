using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One on-screen panel's place and options, kept in the mod settings (CouncilSettings.hudPanels) so they hold across
    /// games: whether it shows, where (as fractions of the screen, from the corner nearest the edges; see
    /// <see cref="HudMath.ToFraction"/>), compact or full, its scale and background, how many rows, whom it lists, its
    /// order, the trend graph's days, the rows or columns switched off, its style, and the inventory's pins and folds
    /// and the alerts hidden in the alerts panel. Written when the panel is moved or changed.
    /// </summary>
    public class HudPanelState : IExposable
    {
        public string id = "";
        public bool on = true;
        /// <summary>The anchor corner's place as fractions of the screen; below 0 for the panel's own default place.</summary>
        public float x = -1f, y = -1f;
        /// <summary>The anchor is the right edge (else the left) and the bottom edge (else the top).</summary>
        public bool right, bottom;
        public bool compact;
        public float scale = 1f;
        public float opacity = 1f;
        /// <summary>Rows shown before "and N more" (0 for all); the critical ones always show.</summary>
        public int maxRows = 10;
        public HudWho who = HudWho.Home;
        public HudSort sort = HudSort.Risk;
        /// <summary>The trend graph's range in days (3 or 7).</summary>
        public int days = 3;
        /// <summary>Rows or columns switched off, by name (see each panel's parts).</summary>
        public List<string> hidden = new List<string>();
        /// <summary>The panel's style or layout, by name: the inventory's look, the health panel's rows ("" for the panel's own default).</summary>
        public string style = "";
        /// <summary>The inventory's icon grid: tiles in a row.</summary>
        public int columns = 8;
        /// <summary>The inventory: the items pinned to the top, by def name.</summary>
        public List<string> pins = new List<string>();
        /// <summary>The inventory: the categories folded away in the grouped style, by def name.</summary>
        public List<string> folds = new List<string>();
        /// <summary>The alerts: alerts hidden in the panel, by the alert's class (its full type name).</summary>
        public List<string> alertsHidden = new List<string>();
        /// <summary>The alerts: alerts hidden by default that the player chose to see (the colonist wishes, while the council panel shows).</summary>
        public List<string> alertsShown = new List<string>();

        public bool Shows(string part) => hidden == null || !hidden.Contains(part);

        public void Toggle(string part)
        {
            hidden ??= new List<string>();
            if (!hidden.Remove(part)) hidden.Add(part);
        }

        public bool Placed => x >= 0f && y >= 0f;

        public HudPanelState Copy()
        {
            var c = (HudPanelState)MemberwiseClone();
            c.hidden = hidden == null ? new List<string>() : new List<string>(hidden);
            c.pins = pins == null ? new List<string>() : new List<string>(pins);
            c.folds = folds == null ? new List<string>() : new List<string>(folds);
            c.alertsHidden = alertsHidden == null ? new List<string>() : new List<string>(alertsHidden);
            c.alertsShown = alertsShown == null ? new List<string>() : new List<string>(alertsShown);
            return c;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", "");
            Scribe_Values.Look(ref on, "on", true);
            Scribe_Values.Look(ref x, "x", -1f);
            Scribe_Values.Look(ref y, "y", -1f);
            Scribe_Values.Look(ref right, "right", false);
            Scribe_Values.Look(ref bottom, "bottom", false);
            Scribe_Values.Look(ref compact, "compact", false);
            Scribe_Values.Look(ref scale, "scale", 1f);
            Scribe_Values.Look(ref opacity, "opacity", 1f);
            Scribe_Values.Look(ref maxRows, "maxRows", 10);
            Scribe_Values.Look(ref who, "who", HudWho.Home);
            Scribe_Values.Look(ref sort, "sort", HudSort.Risk);
            Scribe_Values.Look(ref days, "days", 3);
            Scribe_Collections.Look(ref hidden, "hidden", LookMode.Value);
            Scribe_Values.Look(ref style, "style", "");
            Scribe_Values.Look(ref columns, "columns", 8);
            Scribe_Collections.Look(ref pins, "pins", LookMode.Value);
            Scribe_Collections.Look(ref folds, "folds", LookMode.Value);
            Scribe_Collections.Look(ref alertsHidden, "alertsHidden", LookMode.Value);
            Scribe_Collections.Look(ref alertsShown, "alertsShown", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
            {
                hidden ??= new List<string>();
                style ??= "";
                columns = columns < 2 ? 2 : columns > 30 ? 30 : columns;
                pins ??= new List<string>();
                folds ??= new List<string>();
                alertsHidden ??= new List<string>();
                alertsShown ??= new List<string>();
                scale = HudMath.ClampScale(scale);
                opacity = HudMath.ClampOpacity(opacity);
                if (days != 7) days = 3;
            }
        }
    }
}
