using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Their face is theirs alone: they are (or were) one of the colony's own people; and since when they have kept it.</summary>
        public bool reserved;
        /// <summary>Their face is theirs alone while they live and matter: a faction's leader, a titled noble, one of the colony's named contacts.</summary>
        public bool notable;
        public int since;
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
            Scribe_Values.Look(ref reserved, "reserved");
            Scribe_Values.Look(ref notable, "notable");
            Scribe_Values.Look(ref since, "since");
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

        /// <summary>The most animals and creatures whose picture was chosen by hand that are kept (the earliest let go first).</summary>
        public const int MaxCreaturePicks = 300;

        /// <summary>
        /// The pictures chosen by hand for animals and creatures (UI/Faces/CreatureLibrary.cs), by the creature's id; every
        /// other creature gets its picture by its id each time, so nothing else of theirs is kept.
        /// </summary>
        private Dictionary<int, string> creaturePicks = new Dictionary<int, string>();
        private List<int> creatureIds;
        private List<string> creaturePictures;

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
            // The library folder read again for the game that starts (UI/Faces/PortraitFolder.cs), on a worker.
            try
            {
                PortraitFolder.GameStarted();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading the library folder when a game starts", e);
            }
        }

        /// <summary>A new game started: each colonist keeps the picture the starting pawns' page showed them (UI/Faces/StartingPortraits.cs).</summary>
        public override void StartedNewGame()
        {
            base.StartedNewGame();
            StartingPortraits.KeepAll();
        }

        /// <summary>A game loaded: the trial pictures of a starting pawns' page left unstarted let go.</summary>
        public override void LoadedGame()
        {
            base.LoadedGame();
            StartingPortraits.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // Saving the game writes the library folder's missing list, the variants it wants and the people list too (on a worker thread).
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                PortraitNeeds.Write();
                PortraitVariants.Write();
                PortraitPeople.Write();
            }
            Scribe_Collections.Look(ref kept, "kept", LookMode.Deep);
            Scribe_Collections.Look(ref creaturePicks, "creaturePicks", LookMode.Value, LookMode.Value, ref creatureIds, ref creaturePictures);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                kept ??= new List<KeptPortrait>();
                creaturePicks ??= new Dictionary<int, string>();
                int now = Find.TickManager?.TicksGame ?? 0;
                // A colonist's face is kept while they are remembered, alive or dead, and a notable's while they matter: never
                // let go for not being pictured.
                kept.RemoveAll(k => k == null || !k.reserved && !k.notable && now - k.seen > ForgetAfterDays * GenDate.TicksPerDay);
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
            if (k.picture != (picture ?? "")) k.since = now;
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
            Version++;
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
            Version++;
        }

        /// <summary>Who keeps this picture now (their ids).</summary>
        public List<int> HoldersOfPicture(string picture)
        {
            var ids = new List<int>();
            if (picture.NullOrEmpty()) return ids;
            foreach (var k in kept)
                if (k.picture == picture)
                    ids.Add(k.id);
            return ids;
        }

        /// <summary>
        /// A picture deleted for good (UI/Faces/PortraitDelete.cs): everyone keeping it keeps no picture and no face now, so
        /// the face is no longer held for them, and each gets a new pick by the usual rules the next time they are shown
        /// (their pins first, then the closest free face, else the game's own drawing). Whether they are one of the
        /// colony's own stays, so a colonist's new face is theirs alone as the old one was. Returns their ids.
        /// </summary>
        public List<int> LetGoPicture(string picture)
        {
            var ids = new List<int>();
            if (picture.NullOrEmpty()) return ids;
            foreach (var k in kept)
            {
                if (k.picture != picture) continue;
                Free(k);
                ids.Add(k.id);
            }
            if (ids.Count > 0) Version++;
            return ids;
        }

        /// <summary>
        /// One person's picture let go (its record was corrected in the game and no longer fits them): they keep no picture and
        /// no face now and get a new pick by the usual rules the next time they are shown; whether they are one of the
        /// colony's own stays.
        /// </summary>
        public void LetGo(int id)
        {
            var k = Get(id);
            if (k == null || k.picture.NullOrEmpty()) return;
            Free(k);
            Version++;
        }

        private void Free(KeptPortrait k)
        {
            Use(k.character, -1);
            k.picture = "";
            k.character = "";
            k.pinned = false;
            k.byHand = false;
            // Not the library now: picked again when next shown (PortraitLibraryMath.PickAgain).
            k.library = 0;
        }

        /// <summary>The picture chosen by hand for an animal or creature, or null.</summary>
        public string CreaturePick(int id) => creaturePicks.TryGetValue(id, out var pic) ? pic : null;

        /// <summary>A picture chosen by hand for an animal or creature, kept for it; at most <see cref="MaxCreaturePicks"/>, the earliest let go.</summary>
        public void SetCreaturePick(int id, string picture)
        {
            creaturePicks.Remove(id);
            if (picture.NullOrEmpty()) return;
            creaturePicks[id] = picture;
            while (creaturePicks.Count > MaxCreaturePicks) creaturePicks.Remove(creaturePicks.Keys.First());
        }

        /// <summary>A creature picture deleted for good: whoever had it chosen by hand gets theirs by their id again. Returns how many.</summary>
        public int LetGoCreaturePicture(string picture)
        {
            var ids = creaturePicks.Where(kv => kv.Value == picture).Select(kv => kv.Key).ToList();
            foreach (int id in ids) creaturePicks.Remove(id);
            return ids.Count;
        }

        /// <summary>Everyone's faces let go (the dev action to pick everyone's again).</summary>
        public void DropAll()
        {
            kept.Clear();
            Index();
            Version++;
        }

        /// <summary>Changes whenever a picture is kept, let go or reserved, so the library reads whose each face is again.</summary>
        public int Version;

        /// <summary>A colonist's face made theirs alone (they joined, or were found among the colony's own); no one else is given it from now on.</summary>
        public void Reserve(KeptPortrait k)
        {
            if (k == null || k.reserved || k.picture.NullOrEmpty()) return;
            k.reserved = true;
            Version++;
        }

        /// <summary>A notable's face held for them while they matter, or let go (they died, or are no longer notable).</summary>
        public void SetNotable(KeptPortrait k, bool notable)
        {
            if (k == null || k.notable == notable) return;
            k.notable = notable;
            Version++;
        }

        /// <summary>The faces kept as someone's own: who, which face, since when, and whose (the player's own, or a notable's).</summary>
        public IEnumerable<(int id, string character, int since, PortraitRank rank)> Holders
        {
            get
            {
                foreach (var k in kept)
                    if ((k.reserved || k.notable) && !k.character.NullOrEmpty())
                        yield return (k.id, k.character, k.since, k.reserved ? PortraitRank.Own : PortraitRank.Notable);
            }
        }

        /// <summary>Everyone keeping a face, but <paramref name="besides"/>: who, and since when.</summary>
        public List<KeptPortrait> HoldersOf(string character, int besides)
        {
            var list = new List<KeptPortrait>();
            if (character.NullOrEmpty() || !uses.ContainsKey(character)) return list;
            foreach (var k in kept)
                if (k.id != besides && k.character == character)
                    list.Add(k);
            return list;
        }

        /// <summary>The notables' kept faces, for the library's look whether they still live and matter.</summary>
        public IEnumerable<KeptPortrait> Notables
        {
            get
            {
                foreach (var k in kept)
                    if (k.notable)
                        yield return k;
            }
        }

        /// <summary>The least lately pictured let go, a notable's face after a stranger's, a colonist's own last of all.</summary>
        private void LetGoOldest(int besides)
        {
            KeptPortrait oldest = null;
            int Rank(KeptPortrait k) => k.reserved ? 2 : k.notable ? 1 : 0;
            foreach (var k in kept)
                if (k.id != besides && (oldest == null || Rank(k) < Rank(oldest) || Rank(k) == Rank(oldest) && k.seen < oldest.seen))
                    oldest = k;
            if (oldest != null) Drop(oldest.id);
        }
    }
}
