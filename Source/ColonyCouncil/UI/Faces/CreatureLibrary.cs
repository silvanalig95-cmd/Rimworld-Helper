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
    /// The painted pictures of animals and creatures (the player, 23 November 2026: "also please create lists for animals
    /// and creatures and implement image display for them too"; docs: DESIGN.md, "Portraits: animals and creatures").
    /// <list type="bullet">
    /// <item>Read from the library folder's records.xml (<c>ColonyCouncil.CreaturePortraitDef</c>, the lists
    /// <c>creatures-NN</c>), never among the people's pictures and never given to a person.</item>
    /// <item>An animal, a mechanoid, an insect or any other creature that isn't humanlike gets a picture of its own kind
    /// (its race), its age (a baby a baby's, a juvenile its own or else an adult's, an adult an adult's) and its sex where
    /// the picture names one (<see cref="CreaturePortraitMath.Candidates"/>); the same one every time, by its id
    /// (<see cref="CreaturePortraitMath.Pick"/>), unless one was chosen for it by hand (kept in the save,
    /// <see cref="PortraitKeeper.CreaturePick"/>).</item>
    /// <item>Shown only on the large portrait at the inspect pane (<see cref="InspectPortrait"/>), in front of the backdrop
    /// of where it is, the way a person's painted picture is; the small portraits keep the game's own drawing, since they
    /// frame a person's head.</item>
    /// <item>A creature of a kind the library has no picture for is noted in missing.csv ("creature"), once a session.</item>
    /// </list>
    /// With "Painted portraits" or "Painted pictures of animals and creatures" off, none shows.
    /// </summary>
    public sealed class CreatureLibrary : IPortraitSource
    {
        public static readonly CreatureLibrary Source = new CreatureLibrary();

        private const float LookEvery = 5f;

        private struct Looked
        {
            public int index;
            public float until;
        }

        private readonly List<CreatureRecord> records = new List<CreatureRecord>();
        private readonly List<PortraitPicture> pictures = new List<PortraitPicture>();
        private readonly Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, Looked> looked = new Dictionary<int, Looked>();
        private int loadedVersion = -1;

        public string Name => "the creature pictures";

        public bool On => (ColonyCouncilMod.Settings?.portraitLibrary ?? true) && (ColonyCouncilMod.Settings?.portraitCreatures ?? true) && Load() && records.Count > 0;

        public int Count => Load() ? records.Count : 0;

        /// <summary>The library folder's creature pictures (again whenever the folder is read); nothing is loaded.</summary>
        private bool Load()
        {
            if (loadedVersion == PortraitFolder.Version) return true;
            loadedVersion = PortraitFolder.Version;
            records.Clear();
            pictures.Clear();
            byName.Clear();
            looked.Clear();
            try
            {
                foreach (var c in PortraitFolder.Creatures.OrderBy(x => x.record.name, StringComparer.Ordinal))
                {
                    if (byName.ContainsKey(c.record.name)) continue;
                    var file = PortraitFolder.FileOf(c.record.name, c.texPath, true);
                    if (file == null) continue;
                    byName[c.record.name] = records.Count;
                    records.Add(c.record);
                    pictures.Add(new PortraitPicture { name = c.record.name, file = file, loading = true, layout = Layout(file) });
                }
                if (records.Count > 0)
                    Log.Message("[Rimfolk] Portraits: " + records.Count + " painted pictures of " + records.Select(r => r.race).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                                + " kinds of animals and creatures in the library folder.");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading the creature pictures", e);
            }
            return true;
        }

        /// <summary>A creature's picture laid out whole: the head's place is a person's only, so the middle is taken.</summary>
        private static PictureLayout Layout(PictureFile f) => new PictureLayout(0.5f, 0.35f, 0.3f, FaceTurn.Front, f != null && f.height > 0 ? f.Shape : 1f);

        /// <summary>An animal or creature: anything alive or dead on the map that isn't humanlike.</summary>
        public static bool IsCreature(Pawn p) => p?.RaceProps != null && !p.RaceProps.Humanlike;

        /// <summary>Its age and sex as the pictures tell them.</summary>
        private static void AgeAndSex(Pawn p, out CreatureStage stage, out string sex)
        {
            int count = p.RaceProps?.lifeStageAges?.Count ?? 1;
            stage = CreatureStage.Adult;
            try
            {
                stage = CreaturePortraitMath.StageOf(p.ageTracker?.CurLifeStageIndex ?? count - 1, count);
            }
            catch (Exception)
            {
                // A creature without an age tracker the game can read is taken as grown.
            }
            sex = p.gender == Gender.Male ? "male" : p.gender == Gender.Female ? "female" : "";
        }

        private List<int> CandidatesOf(Pawn p)
        {
            AgeAndSex(p, out var stage, out string sex);
            var keys = GodKeysOf(p);
            if (keys == null) return CreaturePortraitMath.Candidates(records, p.def?.defName, stage, sex);
            // A god shown as itself is painted grown, whatever body it walks in.
            if (GodAvatar.Shown(p)) stage = CreatureStage.Adult;
            return CreaturePortraitMath.FirstCandidates(records, keys, stage, sex);
        }

        /// <summary>
        /// A god walking the map shown as itself (not in disguise): the names its own pictures go under, by the god the colony
        /// knows it as and its look's domain, in the colony's age; a god's beast on the land: its god's, then its kind's
        /// (CreaturePortraitMath.GodKeys, BeastKeys). Null for anyone else.
        /// </summary>
        public static List<string> GodKeysOf(Pawn p)
        {
            if (p == null || !Gods.On) return null;
            var c = GodAvatar.Of(p);
            if (c != null && !c.disguised)
            {
                var g = c.godName.NullOrEmpty() ? null : Gods.All.FirstOrDefault(x => x != null && (x.name == c.godName || x.Key == c.godName));
                string era = PortraitLibraryMath.EraOf((int)Avail.EffectiveTech).ToString();
                return CreaturePortraitMath.GodKeys(g?.Key, (!(c.look?.Domain).NullOrEmpty() ? c.look.Domain : (g?.domain ?? GodDomain.None).ToString()), era == "Unknown" ? "" : era);
            }
            if (IsCreature(p) && (SacredAnimal.Is(p) || SacredAnimal.IsKin(p)))
                return CreaturePortraitMath.BeastKeys(Gods.ById(GodsOnMapBook.Instance?.beast?.god ?? -1)?.Key, p.def?.defName);
            return null;
        }

        /// <summary>A person who is a god shown as itself, with a picture of that god in the library (else they keep a person's picture).</summary>
        public static bool GodPictured(Pawn p)
        {
            if (p == null || IsCreature(p) || !GodAvatar.Any) return false;
            var keys = GodKeysOf(p);
            if (keys == null || !Source.On) return false;
            return CreaturePortraitMath.FirstCandidates(Source.records, keys, CreatureStage.Adult, "").Count > 0;
        }

        // ---------------- A creature's picture ----------------

        public PortraitPicture PictureFor(Pawn p)
        {
            if (!IsCreature(p) && !GodPictured(p) || records.Count == 0) return null;
            int id = p.thingIDNumber;
            float now = Time.realtimeSinceStartup;
            if (!looked.TryGetValue(id, out var l) || now >= l.until)
            {
                int i = -1;
                try
                {
                    i = Decide(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("portraits: matching an animal or creature to its pictures", e);
                }
                l = new Looked { index = i, until = now + LookEvery };
                looked[id] = l;
            }
            return l.index >= 0 ? Ready(l.index) : null;
        }

        /// <summary>The picture for it now: the one chosen by hand where it still fits, else the one its id ranks first; -1 for none (noted as missing).</summary>
        private int Decide(Pawn p)
        {
            var list = CandidatesOf(p);
            if (list.Count == 0)
            {
                Note(p);
                return -1;
            }
            var names = CreaturePortraitMath.Names(records, list);
            string chosen = PortraitKeeper.Instance?.CreaturePick(p.thingIDNumber);
            int at = chosen != null ? names.IndexOf(chosen) : -1;
            if (at < 0) at = CreaturePortraitMath.Pick(p.thingIDNumber, names);
            return at >= 0 ? list[at] : -1;
        }

        private PortraitPicture Ready(int i)
        {
            var pic = pictures[i];
            var tex = PortraitStore.Get(pic.file);
            if (tex != null)
            {
                if (pic.texture != tex)
                {
                    pic.texture = tex;
                    pic.layout = Layout(pic.file);
                }
                pic.loading = false;
                return pic;
            }
            pic.texture = null;
            pic.loading = !PortraitStore.Failed(pic.file);
            return pic.loading ? pic : null;
        }

        /// <summary>No picture of its kind, age and sex: a line in missing.csv, once a session for each creature.</summary>
        private void Note(Pawn p)
        {
            AgeAndSex(p, out var stage, out string sex);
            bool kind = records.Any(r => string.Equals(r.race, p.def?.defName, StringComparison.OrdinalIgnoreCase));
            PortraitNeeds.NoteCreature(p, CreaturePortraitMath.Word(stage), sex, kind ? "CC_PortraitNeed_NoCreatureStage" : "CC_PortraitNeed_NoCreature");
        }

        /// <summary>The record of the picture it shows now, or null.</summary>
        public CreatureRecord ShownFor(Pawn p)
        {
            var pic = PictureFor(p);
            return pic != null && byName.TryGetValue(pic.name, out int i) ? records[i] : null;
        }

        // ---------------- Choosing another ----------------

        /// <summary>Another picture may stand for it: its kind, age and sex have more than one.</summary>
        public bool CanChoose(Pawn p) => On && IsCreature(p) && CandidatesOf(p).Count > 1;

        /// <summary>The next of the pictures that may stand for it, kept for it from now on. Says what happened.</summary>
        public string ChooseAnother(Pawn p)
        {
            string name = p.LabelShortCap;
            if (!On || !IsCreature(p)) return "CC_Portrait_CreatureChooseNone".Translate(name.Named("NAME")).Resolve();
            var list = CandidatesOf(p);
            var names = CreaturePortraitMath.Names(records, list);
            var keeper = PortraitKeeper.Instance;
            string now = ShownFor(p)?.name ?? "";
            int next = CreaturePortraitMath.Next(names, now);
            if (next < 0 || keeper == null) return "CC_Portrait_CreatureChooseNone".Translate(name.Named("NAME")).Resolve();
            keeper.SetCreaturePick(p.thingIDNumber, names[next]);
            Forget(p.thingIDNumber);
            FaceEngine.ClearStaged();
            return "CC_Portrait_CreatureChooseDone".Translate(name.Named("NAME"), (next + 1).Named("AT"), names.Count.Named("OF")).Resolve();
        }

        public void Forget(int pawnId) => looked.Remove(pawnId);

        public void NewGame() => looked.Clear();

        // ---------------- Dev mode ----------------

        [DebugAction("Rimfolk", "Portraits: the selected animals' and creatures' pictures (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugExplain()
        {
            var lines = new List<string>();
            foreach (var p in Find.Selector?.SelectedPawns?.Where(IsCreature) ?? Enumerable.Empty<Pawn>())
            {
                AgeAndSex(p, out var stage, out string sex);
                var list = Source.Load() ? Source.CandidatesOf(p) : new List<int>();
                lines.Add("  " + p.LabelShort + " (" + p.def.defName + ", " + CreaturePortraitMath.Word(stage) + (sex.Length > 0 ? ", " + sex : "") + "): "
                          + (list.Count == 0 ? "no picture of its kind, age and sex" : list.Count + " pictures may stand for it: " + string.Join(", ", CreaturePortraitMath.Names(Source.records, list)))
                          + "; shows " + (Source.ShownFor(p)?.name ?? "none") + (PortraitKeeper.Instance?.CreaturePick(p.thingIDNumber) != null ? " (chosen by hand)" : ""));
            }
            Log.Message("[Rimfolk] Portraits: " + Source.Count + " pictures of animals and creatures.\n" + string.Join("\n", lines));
            Messages.Message("Portraits: the selected animals' pictures are in the log.", MessageTypeDefOf.SilentInput, false);
        }
    }
}
