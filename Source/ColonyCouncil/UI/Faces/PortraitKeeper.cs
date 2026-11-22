using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One person's picture from the portrait library, kept by their id: the picture (its def's name; "" for none that
    /// fitted) and its character (the face), their kind and sex and their looks in words when it was picked, their stage
    /// then (a baby, a toddler, a child, a grown-up; -1 unknown), whether it is pinned to them or chosen by hand, its
    /// distance from their looks, the library then, and when they were last pictured.
    /// </summary>
    public class KeptPortrait : IExposable
    {
        public int id;
        public string picture = "";
        public string character = "";
        public string kind = "";
        public string words = "";
        public int stage = -1;
        public bool pinned, byHand;
        public float distance;
        public int library;
        public int seen;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref picture, "picture", "");
            Scribe_Values.Look(ref character, "character", "");
            Scribe_Values.Look(ref kind, "kind", "");
            Scribe_Values.Look(ref words, "words", "");
            Scribe_Values.Look(ref stage, "stage", -1);
            Scribe_Values.Look(ref pinned, "pinned");
            Scribe_Values.Look(ref byHand, "byHand");
            Scribe_Values.Look(ref distance, "distance");
            Scribe_Values.Look(ref library, "library");
            Scribe_Values.Look(ref seen, "seen");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                picture ??= "";
                character ??= "";
                kind ??= "";
                words ??= "";
            }
        }
    }

    /// <summary>
    /// Each person's picture from the portrait library, kept for good in the save (UI/Faces/PortraitLibrary.cs; docs:
    /// DESIGN.md, "Portraits, phase 4"), so a face stays the same from one day and one load to the next: chosen once, the
    /// first time it is needed, and changed only when a child moves to the next stage
    /// (<see cref="PortraitLibraryMath.PickAgain"/>). Kept by the pawn's id; at most <see cref="MaxKept"/>, the least
    /// lately pictured let go first, and anyone not pictured for <see cref="ForgetAfterDays"/> days let go on load (a
    /// stranger seen once long ago). Someone let go who is pictured again is matched again by the same rules.
    /// </summary>
    public class PortraitKeeper : GameComponent
    {
        public const int MaxKept = 400;
        public const int ForgetAfterDays = 120;

        public List<KeptPortrait> kept = new List<KeptPortrait>();

        [Unsaved] private readonly Dictionary<int, KeptPortrait> byId = new Dictionary<int, KeptPortrait>();
        [Unsaved] private readonly Dictionary<string, int> uses = new Dictionary<string, int>();

        private static PortraitKeeper cached;
        private static Game cachedGame;

        public PortraitKeeper(Game game)
        {
        }

        public static PortraitKeeper Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PortraitKeeper>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            Index();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref kept, "kept", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                kept ??= new List<KeptPortrait>();
                int now = Find.TickManager?.TicksGame ?? 0;
                kept.RemoveAll(k => k == null || now - k.seen > ForgetAfterDays * GenDate.TicksPerDay);
                Index();
            }
        }

        private void Index()
        {
            byId.Clear();
            uses.Clear();
            kept.RemoveAll(k => k == null);
            foreach (var k in kept)
            {
                byId[k.id] = k;
                Use(k.character, 1);
            }
        }

        private void Use(string character, int by)
        {
            if (character.NullOrEmpty()) return;
            uses.TryGetValue(character, out int n);
            n += by;
            if (n > 0) uses[character] = n;
            else uses.Remove(character);
        }

        public KeptPortrait Get(int id) => byId.TryGetValue(id, out var k) ? k : null;

        /// <summary>How many people keep this face, leaving out <paramref name="besides"/>.</summary>
        public int UsesOf(string character, int besides)
        {
            if (character.NullOrEmpty() || !uses.TryGetValue(character, out int n)) return 0;
            var own = Get(besides);
            return own != null && own.character == character ? n - 1 : n;
        }

        /// <summary>
        /// Keeps a picture for a person ("" for none that fitted) and its face, with their kind, their looks, their stage, the
        /// library now, whether it is theirs by a pin or by the player's hand, and how far it is from their looks.
        /// </summary>
        public void Set(int id, string picture, string character, string kind, string words, int library, PortraitStage stage, bool pinned, bool byHand, float distance)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            var k = Get(id);
            if (k == null)
            {
                k = new KeptPortrait { id = id, seen = now };
                kept.Add(k);
                byId[id] = k;
                if (kept.Count > MaxKept) LetGoOldest(id);
            }
            else Use(k.character, -1);
            k.picture = picture ?? "";
            k.character = k.picture.Length > 0 ? character ?? "" : "";
            k.kind = kind ?? "";
            k.words = words ?? "";
            k.library = library;
            k.stage = (int)stage;
            k.pinned = pinned;
            k.byHand = byHand;
            k.distance = float.IsInfinity(distance) || float.IsNaN(distance) ? 999f : distance;
            k.seen = now;
            Use(k.character, 1);
        }

        /// <summary>They were pictured now (counted at most once a day, so nothing is written each frame).</summary>
        public void Seen(KeptPortrait k)
        {
            if (k == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now - k.seen >= GenDate.TicksPerDay) k.seen = now;
        }

        /// <summary>A person's face let go (the dev action to pick again).</summary>
        public void Drop(int id)
        {
            var k = Get(id);
            if (k == null) return;
            Use(k.character, -1);
            kept.Remove(k);
            byId.Remove(id);
        }

        /// <summary>Everyone's faces let go (the dev action to pick everyone's again).</summary>
        public void DropAll()
        {
            kept.Clear();
            Index();
        }

        private void LetGoOldest(int besides)
        {
            KeptPortrait oldest = null;
            foreach (var k in kept)
                if (k.id != besides && (oldest == null || k.seen < oldest.seen))
                    oldest = k;
            if (oldest != null) Drop(oldest.id);
        }
    }
}
