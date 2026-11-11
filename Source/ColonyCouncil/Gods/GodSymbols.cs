using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' symbols (Textures/ColonyCouncil/GodSymbols/, drawn by Tools/make_god_symbols.py): one per domain, white on
    /// transparent and tinted in a god's colours when drawn, and the pantheon's own (a small temple) for the Gods page.
    /// Loaded once at startup; a missing one is simply not drawn.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GodSymbols
    {
        public const string Folder = "ColonyCouncil/GodSymbols/";

        private static readonly Dictionary<GodDomain, Texture2D> byDomain = new Dictionary<GodDomain, Texture2D>();

        /// <summary>The pantheon's own symbol (the Gods page's header and its "whole pantheon" row).</summary>
        public static readonly Texture2D Pantheon = ContentFinder<Texture2D>.Get(Folder + "Pantheon", false);

        static GodSymbols()
        {
            foreach (var d in GodsMath.Domains) byDomain[d] = ContentFinder<Texture2D>.Get(Folder + d, false);
        }

        /// <summary>A domain's symbol, or null.</summary>
        public static Texture2D For(GodDomain d) => byDomain.TryGetValue(d, out var t) ? t : null;

        /// <summary>A god's symbol in its colours: its second colour as a soft disc behind, its first on the symbol; faint for a rumour.</summary>
        public static void Draw(Rect r, God g, bool rumour = false)
        {
            if (g == null) return;
            var tex = For(g.domain);
            var c1 = rumour ? UiKit.Faint : g.colour;
            var c2 = rumour ? UiKit.Dim : g.colour2;
            UiKit.DiscFill(r, UiKit.WithAlpha(c2, rumour ? 0.18f : 0.3f));
            if (tex != null) UiKit.Icon(r.ContractedBy(r.width * 0.12f), tex, rumour ? UiKit.WithAlpha(c1, 0.6f) : c1);
        }
    }
}
