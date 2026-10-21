namespace ColonyCouncil
{
    /// <summary>The on-screen panels' background (the settings' "Look").</summary>
    public enum HudTheme
    {
        Dark,
        Darker,
        Clear
    }

    /// <summary>The panels' text size.</summary>
    public enum HudFont
    {
        Small,
        Normal
    }

    /// <summary>A key to hold for the hover card to show.</summary>
    public enum HudModifier
    {
        None,
        Alt,
        Shift,
        Ctrl
    }

    /// <summary>Whom the hover card is for.</summary>
    public enum HoverWho
    {
        /// <summary>The colony's free colonists.</summary>
        Colonists,
        /// <summary>Colonists, the colony's slaves and its prisoners.</summary>
        Everyone
    }

    /// <summary>Where the hover card shows.</summary>
    public enum HoverPlace
    {
        /// <summary>Beside the mouse.</summary>
        Cursor,
        /// <summary>In the bottom-left corner, above the inspect pane's place.</summary>
        Corner
    }

    /// <summary>Whom a panel lists.</summary>
    public enum HudWho
    {
        /// <summary>Free colonists on the colony's home maps.</summary>
        Home,
        /// <summary>Free colonists anywhere: other maps, caravans, shuttles.</summary>
        Everywhere,
        /// <summary>Everywhere, and the colony's prisoners too.</summary>
        WithPrisoners
    }

    /// <summary>The order of a panel's rows.</summary>
    public enum HudSort
    {
        /// <summary>Most at risk first (the shared ranking in HudRank); for the alerts, the game's priority and its own order.</summary>
        Risk,
        /// <summary>The colonist bar's order.</summary>
        Bar,
        /// <summary>The alerts: the newest first.</summary>
        Newest,
        /// <summary>The inventory: by the game's categories, as its resource list.</summary>
        Category,
        /// <summary>The inventory: by name.</summary>
        Name,
        /// <summary>The inventory: the most first.</summary>
        Count,
        /// <summary>The inventory: the biggest change since yesterday first.</summary>
        Change
    }
}
