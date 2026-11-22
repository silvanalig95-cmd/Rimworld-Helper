using System;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A whole picture of a person from a portrait source: the texture (null while it loads), where the head is in it, its
    /// name for the log, and for the painted library its file (the large portrait makes its own picture from it).
    /// </summary>
    public sealed class PortraitPicture
    {
        public Texture2D texture;
        public PictureLayout layout;
        public string name;
        /// <summary>Still loading: the portrait shows its ground only until it is ready.</summary>
        public bool loading;
        public PictureFile file;
    }

    /// <summary>
    /// A source of whole pictures of people, asked by the face engine before it draws the game's own face (docs:
    /// DESIGN.md, "Portraits, phase 3"). The library of painted portraits is one (<see cref="PortraitLibrary"/>); a later
    /// source (a portrait painted live from the pawn's own data) is added to <see cref="PortraitSources"/>' list and needs
    /// nothing else: the engine frames, poses, mirrors, greys and lights any source's picture the same way, by its layout.
    /// </summary>
    public interface IPortraitSource
    {
        /// <summary>For the log.</summary>
        string Name { get; }

        /// <summary>Its setting is on and it has anything to give.</summary>
        bool On { get; }

        /// <summary>This person's picture, or null where this source has none for them (the next source is asked, then the game's face is drawn). Called often: kept cheap.</summary>
        PortraitPicture PictureFor(Pawn p);

        /// <summary>The game marked the person's portrait dirty (new clothes, hair, grown up): look at them again.</summary>
        void Forget(int pawnId);

        /// <summary>A new game or a loaded one: nothing kept of the last holds.</summary>
        void NewGame();
    }

    /// <summary>The portrait sources in the order they are asked; the game's own face is drawn where none has a picture.</summary>
    public static class PortraitSources
    {
        private static readonly IPortraitSource[] Sources = { PortraitLibrary.Source };

        /// <summary>The first picture a source has for this person (its texture null while it loads), or null for the game's own face.</summary>
        public static PortraitPicture For(Pawn p)
        {
            if (p == null) return null;
            for (int i = 0; i < Sources.Length; i++)
            {
                var s = Sources[i];
                try
                {
                    if (!s.On) continue;
                    var pic = s.PictureFor(p);
                    if (pic != null && (pic.texture != null || pic.loading)) return pic;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("portraits: a picture from " + s.Name, e);
                }
            }
            return null;
        }

        public static void Forget(int pawnId)
        {
            foreach (var s in Sources) s.Forget(pawnId);
        }

        public static void NewGame()
        {
            foreach (var s in Sources) s.NewGame();
        }
    }
}
