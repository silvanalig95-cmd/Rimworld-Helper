using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The portrait library (Portraits, phase 3; docs: DESIGN.md, "Portraits, phase 3: a library of painted portraits",
    /// and <c>docs/portrait-art/PLAN.md</c>): painted head-and-shoulders pictures made with an image AI, each described by a
    /// <see cref="PortraitDef"/> (in Rimfolk's or any mod's Defs), its texture loaded by the game as any other.
    /// <list type="bullet">
    /// <item>Each person is matched by their looks (<see cref="PortraitReader"/>, <see cref="PortraitLibraryMath.Pick"/>):
    /// their kind first, then sex and age, then skin and hair colour, hair, beard, build, clothes and features.</item>
    /// <item>The character picked is kept for good (<see cref="PortraitKeeper"/>, in the save); its variant is chosen anew
    /// as the body changes (a lost eye, a scar, a burn, a bionic eye, old age). Picked again only when they become another
    /// kind or sex, the character is gone, or a grown library has one clearly closer.</item>
    /// <item>No picture close enough, or a kind far from a human's with no pictures of its own: the game's own face. With
    /// no pictures at all (as it ships), every portrait is the game's own face.</item>
    /// <item>Looked at again every few seconds per person shown and when the game marks their portrait dirty; nothing is
    /// read each frame.</item>
    /// </list>
    /// The setting "Painted portraits from the library" (on) switches it.
    /// </summary>
    public sealed class PortraitLibrary : IPortraitSource
    {
        public static readonly PortraitLibrary Source = new PortraitLibrary();

        /// <summary>How often a person's looks are read again while their portrait is shown, in seconds.</summary>
        private const float LookEvery = 5f;

        private struct Looked
        {
            public PortraitPicture picture;
            public float until;
        }

        private readonly List<PortraitDef> defs = new List<PortraitDef>();
        private readonly List<PortraitRecord> records = new List<PortraitRecord>();
        private readonly List<PortraitPicture> pictures = new List<PortraitPicture>();
        private readonly HashSet<string> characters = new HashSet<string>();
        private readonly Dictionary<int, Looked> looked = new Dictionary<int, Looked>();
        private bool loaded;
        private int signature;

        public string Name => "the portrait library";

        public bool On => (ColonyCouncilMod.Settings?.portraitLibrary ?? true) && Load() && records.Count > 0;

        public int Count => Load() ? records.Count : 0;

        // ---------------- The library ----------------

        /// <summary>Reads the pictures' defs once a session, those whose texture the game found; what is wrong across them in the log once.</summary>
        private bool Load()
        {
            if (loaded) return true;
            loaded = true;
            try
            {
                foreach (var d in DefDatabase<PortraitDef>.AllDefsListForReading.OrderBy(x => x.defName, StringComparer.Ordinal))
                {
                    if (d.Texture == null)
                    {
                        CouncilLog.WarnOnce("Portraits: the picture " + d.defName + " has no texture at " + d.texPath + ", so it is left out.");
                        continue;
                    }
                    defs.Add(d);
                    records.Add(d.Record);
                    pictures.Add(new PortraitPicture { texture = d.Texture, layout = d.Layout, name = d.defName });
                    characters.Add(d.Record.Character);
                }
                signature = PortraitLibraryMath.LibrarySignature(records.Select(r => r.name));
                foreach (var problem in PortraitLibraryMath.LibraryProblems(records)) CouncilLog.WarnOnce("Portraits: " + problem);
                if (records.Count > 0) Log.Message("[Rimfolk] Portraits: " + records.Count + " painted portraits of " + characters.Count + " faces in the library.");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading the portrait library", e);
            }
            return true;
        }

        // ---------------- A person's picture ----------------

        public PortraitPicture PictureFor(Pawn p)
        {
            if (p == null || records.Count == 0) return null;
            int id = p.thingIDNumber;
            float now = Time.realtimeSinceStartup;
            if (looked.TryGetValue(id, out var l) && now < l.until) return l.picture;
            PortraitPicture picture = null;
            try
            {
                int i = Decide(p);
                picture = i >= 0 ? pictures[i] : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: matching a person to the portrait library", e);
            }
            looked[id] = new Looked { picture = picture, until = now + LookEvery };
            return picture;
        }

        /// <summary>The picture for them now (an index, -1 for the game's own face): their kept character's variant for their body as it is, or a new pick.</summary>
        private int Decide(Pawn p)
        {
            if (!PortraitReader.LookOf(p, out var look)) return -1;
            int id = p.thingIDNumber;
            var keeper = PortraitKeeper.Instance;
            var k = keeper?.Get(id);
            bool hasCharacter = k != null && !k.character.NullOrEmpty();
            var variant = hasCharacter && characters.Contains(k.character) ? PortraitLibraryMath.Variant(look, records, k.character) : PortraitPick.None;
            string kind = look.Kind, words = look.Words;
            // The plain case, every few seconds for everyone shown: kept, the same kind and library.
            if (k != null && k.library == signature && (hasCharacter ? k.kind == kind && variant.Any : k.words == words))
            {
                keeper.Seen(k);
                return variant.index;
            }
            var pick = PortraitLibraryMath.Pick(look, records, id, c => keeper?.UsesOf(c, id) ?? 0);
            bool again = PortraitLibraryMath.PickAgain(k != null, hasCharacter, k?.kind, kind, k?.words, words, variant.Any, k?.library ?? 0, signature, variant.distance, pick.distance);
            if (!again)
            {
                k.library = signature;
                keeper.Seen(k);
                return variant.index;
            }
            if (!pick.Any)
            {
                keeper?.Set(id, "", kind, words, signature);
                return -1;
            }
            keeper?.Set(id, pick.character, kind, words, signature);
            // The character's variant for their body now (the pick is its nearest picture; the same unless marks differ).
            var now = PortraitLibraryMath.Variant(look, records, pick.character);
            return now.Any ? now.index : pick.index;
        }

        public void Forget(int pawnId) => looked.Remove(pawnId);

        public void NewGame() => looked.Clear();

        /// <summary>For the log: their looks, what they keep, and the nearest few pictures with their scores.</summary>
        private string Explain(Pawn p)
        {
            if (!PortraitReader.LookOf(p, out var look)) return p.LabelShort + ": no picture for them (not humanlike, a baby or a toddler)";
            var k = PortraitKeeper.Instance?.Get(p.thingIDNumber);
            var d = PortraitLibraryMath.Distances(look, records);
            var near = Enumerable.Range(0, records.Count).Where(i => !float.IsInfinity(d[i])).OrderBy(i => d[i]).Take(6)
                .Select(i => records[i].name + " (" + records[i].Character + "/" + records[i].variant + ") " + d[i].ToString("0.00"));
            string kept = k == null ? "nothing yet" : k.character.NullOrEmpty() ? "no picture close enough" : "the face " + k.character;
            return p.LabelShort + ": " + look.Words + (look.FarFromHuman ? " (far from a human's look)" : "") + "; keeps " + kept + "; nearest: "
                   + (near.Any() ? string.Join(", ", near) : "none that may stand for them");
        }

        // ---------------- Dev mode ----------------

        private static IEnumerable<Pawn> Selected => Find.Selector?.SelectedPawns?.Where(x => x?.RaceProps != null && x.RaceProps.Humanlike) ?? Enumerable.Empty<Pawn>();

        [DebugAction("Rimfolk", "Portraits: the selected people's best matches with their scores (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugExplain()
        {
            var lines = Selected.Select(p => "  " + Source.Explain(p)).ToList();
            Log.Message("[Rimfolk] Portraits: " + Source.Count + " pictures in the library, " + (PortraitKeeper.Instance?.kept.Count ?? 0) + " people keep a face (a lower score is closer; above "
                        + PortraitLibraryMath.CloseEnough.ToString("0") + " is the game's own face).\n" + string.Join("\n", lines));
            Messages.Message("Portraits: the selected people's best matches are in the log.", MessageTypeDefOf.SilentInput, false);
        }

        [DebugAction("Rimfolk", "Portraits: pick the selected people's painted portraits again", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPickAgain()
        {
            int n = 0;
            foreach (var p in Selected)
            {
                PortraitKeeper.Instance?.Drop(p.thingIDNumber);
                Source.Forget(p.thingIDNumber);
                n++;
            }
            Messages.Message("Portraits: " + n + " picked again from the library (" + Source.Count + " pictures).", MessageTypeDefOf.SilentInput, false);
        }

        [DebugAction("Rimfolk", "Portraits: pick everyone's painted portrait again", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPickAllAgain()
        {
            PortraitKeeper.Instance?.DropAll();
            Source.NewGame();
            Messages.Message("Portraits: everyone picked again from the library (" + Source.Count + " pictures).", MessageTypeDefOf.SilentInput, false);
        }
    }
}
