using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The item's picture (UiKit.ItemPortrait), without game types (Tools/Tests try it): the colour of each quality and the
    /// tile's size. The game has no colours for quality (QualityUtility has none, in 1.6 or 1.7), so Rimfolk's look gives
    /// its own, the order players know from other games: awful brown, poor grey, normal light, good green, excellent blue,
    /// masterwork violet, legendary gold. Each reads at 4.5:1 or better on the window's ground (it names the quality on the
    /// tile's strip) and at 3:1 on a raised card (it frames the tile).
    /// </summary>
    public static class ItemPortraitMath
    {
        /// <summary>The qualities' colours, awful (0) to legendary (6), as the game numbers QualityCategory.</summary>
        public static readonly uint[] QualityColours =
        {
            0x9a7b5c, // awful: brown
            0x9c968a, // poor: grey (the look's Dim)
            0xd9d3c6, // normal: light
            0x7cc47a, // good: green
            SkinMath.Ally, // excellent: blue
            SkinMath.World, // masterwork: violet
            SkinMath.Accent // legendary: gold
        };

        /// <summary>A quality's colour (0 awful to 6 legendary); a thing without quality takes none (0 back).</summary>
        public static uint QualityColour(int quality) => quality >= 0 && quality < QualityColours.Length ? QualityColours[quality] : 0u;

        /// <summary>The large tile on a card: a fifth of its width, 96 to 128 pixels, whole.</summary>
        public static float CardTile(float width) => (float)Math.Round(Math.Max(96f, Math.Min(128f, width * 0.2f)));

        /// <summary>A tile this size and up is framed twice and may name its quality on a strip at its foot; smaller ones get one fine edge.</summary>
        public const float LargeTile = 64f;

        /// <summary>The strip naming the quality at a large tile's foot.</summary>
        public const float StripH = 18f;

        /// <summary>The space around the picture inside its tile: a tenth of its side, two pixels at least.</summary>
        public static float Pad(float side) => Math.Max(2f, (float)Math.Round(side * 0.1f));
    }
}
