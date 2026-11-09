using ColonyCouncil;

/// <summary>
/// The README's table cells: the sub-ratings in a line, the age, text safe in a cell. The weapon's line in plain words is
/// the mod's own (Source/ColonyCouncil/Weapons/WeaponWordsMath.cs), so the review and the game's card say the same.
/// </summary>
public static class Words
{
    /// <summary>The distance sub-ratings as a short line.</summary>
    public static string Subs(WeaponRating r)
    {
        string F(string k) => r.Sub.TryGetValue(k, out var v) ? v.ToString("0.0") : "";
        if (r.Kind == "ranged")
            return $"{F("close")} / {F("medium")} / {F("long")} / {F("far")}";
        return "";
    }

    /// <summary>The sub-ratings against each enemy as a short line.</summary>
    public static string Armour(WeaponRating r)
    {
        string F(string k) => r.Sub.TryGetValue(k, out var v) ? v.ToString("0.0") : "";
        return $"{F("unarmoured")} / {F("armoured")} / {F("heavy")} / {F("mechanoids")}";
    }

    public static string Tech(string t) => string.IsNullOrEmpty(t) ? "" : t switch
    {
        "Neolithic" => "neolithic", "Medieval" => "medieval", "Industrial" => "industrial", "Spacer" => "spacer",
        "Ultra" => "ultra", "Archotech" => "archotech", "Animal" => "animal", _ => t.ToLowerInvariant()
    };

    /// <summary>Text safe in a Markdown table cell.</summary>
    public static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ");
}
