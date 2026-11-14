using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' signs (docs/gods-next-proposals.md, section A; the player, 14 November 2026: "the god signs are awesome, go
    /// ahead! should be seen in the book and on cards"): the colony's own holy sign for a god, what its people draw, carve or
    /// stitch for it, a belief made visible and never a portrait of the hidden god. Drawn by Tools/make_god_signs.py into
    /// Textures/ColonyCouncil/Gods/Signs (GodSignsMath): the plain sign in the god's colours, and the sign in the form of each
    /// age, which the colony's own age picks (its means: scratched on a stone, painted on a shield, stamped on a badge, lit in
    /// a ring).
    /// <para>
    /// Where it shows: in a chapter of the colony's book, beside its name, once someone has told what the god is under that
    /// name or the colony saw it plainly (GodSignsMath.Believed: the god the chapter holds itself to be about); and on a
    /// god's card, only when the card names the god and the colony knows its sign so (<see cref="Known"/>). Never on a shrine, never over a god on
    /// the map, and nowhere for a god the colony has not heard of.
    /// </para>
    /// </summary>
    public static class GodSigns
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        private static Texture2D Get(string path)
        {
            if (path == null) return null;
            if (!cache.TryGetValue(path, out var tex))
            {
                try
                {
                    tex = ContentFinder<Texture2D>.Get(path, false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a sign's texture", e);
                    tex = null;
                }
                cache[path] = tex;
            }
            return tex;
        }

        /// <summary>A god's plain sign in its colours, by its key ("Hollin"), or null.</summary>
        public static Texture2D Plain(string key) => key.NullOrEmpty() ? null : Get(GodSignsMath.PlainPath(key));

        /// <summary>A god's sign in the form of the colony's own age (its means), by its key; the plain sign where that form is missing; or null.</summary>
        public static Texture2D OfColony(string key)
        {
            if (key.NullOrEmpty()) return null;
            int age;
            try
            {
                age = (int)Ages.Ours;
            }
            catch
            {
                age = 1;
            }
            return Get(GodSignsMath.AgePath(key, age)) ?? Plain(key);
        }

        /// <summary>A god's key, when it is one of Rimfolk's own with a sign.</summary>
        public static string KeyOf(God g) => g?.def != null && GodLoreMath.Keys.Contains(g.Key) ? g.Key : null;

        /// <summary>
        /// The colony knows a god's sign: someone has told it what the god is, or its people saw it plainly (a verse in its
        /// book that tells it, GodSignsMath.Tells, under any of the god's names). A god it has never heard of has none.
        /// </summary>
        public static bool Known(God g)
        {
            if (KeyOf(g) == null || !Gods.On) return false;
            var lore = GodLore.Instance;
            return lore?.verses != null && lore.verses.Any(v => v != null && v.god == g.id && GodSignsMath.Tells(v));
        }

        /// <summary>A sign drawn into a square, whole, as it was made.</summary>
        public static void Draw(Rect r, Texture2D tex, float alpha = 1f)
        {
            if (tex == null) return;
            var was = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
            GUI.color = was;
        }
    }
}
