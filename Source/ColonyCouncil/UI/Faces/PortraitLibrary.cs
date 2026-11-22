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
    /// The portrait library (Portraits, phases 3 and 4; docs: DESIGN.md, "Portraits, phase 3: a library of painted
    /// portraits" and "Portraits, phase 4: the painted library in the game, with backdrops"): painted pictures of people,
    /// each described by a <see cref="PortraitDef"/> in any mod's Defs (the player's own pack, "Rimfolk Portraits"), the
    /// picture read only when it is shown (<see cref="PortraitStore"/>).
    /// <list type="bullet">
    /// <item>A picture pinned to a colonist (its def's <c>pawn</c>: their nickname, the name on the colonist bar, first, last
    /// or full name) is theirs while its stage fits them, ahead of the matcher, and never anyone else's.</item>
    /// <item>Everyone else (colonists, visitors, raiders, anyone a portrait is drawn for) is matched by their looks
    /// (<see cref="PortraitReader"/>, <see cref="PortraitLibraryMath.Pick"/>): their kind first, then sex and age, then
    /// skin and hair colour, hair, beard, build, clothes, features and marks. "Portraits from the library" shows the
    /// closest picture, or only close ones.</item>
    /// <item>Chosen once, the first time it is needed, and kept for good (<see cref="PortraitKeeper"/>, in the save; the
    /// player, 22 November 2026): no injury, new hair or clothes changes it; a child moving to the next stage gets a new
    /// one; a new picture in the library never replaces a kept one, but someone with none gets one when a fitting picture
    /// comes, and a pin made for a colonist takes effect.</item>
    /// <item>"Choose another picture" on the large portrait's menu steps through their pins and the nearest faces.</item>
    /// <item>A kind far from a human's with no pictures of its own, or no picture at all that may stand for them: the game's own face.</item>
    /// </list>
    /// Looked at again every few seconds per person shown and when the game marks their portrait dirty; nothing is read each frame.
    /// </summary>
    public sealed class PortraitLibrary : IPortraitSource
    {
        public static readonly PortraitLibrary Source = new PortraitLibrary();

        /// <summary>How often a person shown is looked at again, in seconds.</summary>
        private const float LookEvery = 5f;

        /// <summary>How often the colonists' names are read again for the pins, in seconds.</summary>
        private const float NamesEvery = 10f;

        private struct Looked
        {
            public int index;
            public float until;
        }

        /// <summary>A picture of the library: its record, its file, and the head's place where its record gives it (else measured).</summary>
        private sealed class Entry
        {
            public PortraitRecord record;
            public PictureFile file;
            public float headX = float.NaN, headY = float.NaN, headSize = float.NaN;

            public PictureLayout Layout => PortraitFiles.LayoutOf(record, file, headX, headY, headSize);
        }

        private readonly List<Entry> entries = new List<Entry>();
        private List<Entry> fromDefs;
        private readonly List<PortraitRecord> records = new List<PortraitRecord>();
        private readonly List<PortraitPicture> pictures = new List<PortraitPicture>();
        private readonly Dictionary<string, int> byName = new Dictionary<string, int>();
        private readonly HashSet<string> characters = new HashSet<string>();
        private readonly Dictionary<int, Looked> looked = new Dictionary<int, Looked>();
        /// <summary>The people already looked at for the missing list since the library was read (once each).</summary>
        private readonly HashSet<int> noted = new HashSet<int>();
        /// <summary>When someone kept with no picture is looked at again for a free one (real seconds), by id.</summary>
        private readonly Dictionary<int, float> retryAt = new Dictionary<int, float>();
        /// <summary>The people on any map now (their ids), read again every few seconds: strangers sharing a face are never seen together.</summary>
        private readonly HashSet<int> onMaps = new HashSet<int>();
        private float onMapsUntil = -1f;
        /// <summary>The colony's named contacts alive (Rimfolk's notables: heads, heirs, envoys, traders, acquaintances), by id, read again when their book changes.</summary>
        private readonly HashSet<int> notableIds = new HashSet<int>();
        private int notablesVersion = -1;
        private NotableBook notablesBook;
        /// <summary>The notables seen this session, for the look every minute whether they still live and matter.</summary>
        private readonly Dictionary<int, Pawn> notablePawns = new Dictionary<int, Pawn>();
        private float sweepUntil = -1f;

        /// <summary>How often someone kept with no picture is looked at again for a free one, how often who is on a map is read, and the notables looked at, in real seconds.</summary>
        private const float RetryEvery = 30f, OnMapsEvery = 3f, SweepEvery = 60f;
        private readonly Dictionary<string, int> pinOwners = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private float namesUntil = -1f;
        /// <summary>Whose each face is (a colonist's own, a pinned one's): read again when the keeper or the pins change.</summary>
        private Dictionary<string, int> owners = new Dictionary<string, int>(StringComparer.Ordinal);
        private int ownersVersion = -1;
        private PortraitKeeper ownersKeeper;
        private int loadedVersion = -1;
        private int signature;

        public string Name => "the portrait library";

        public bool On => (ColonyCouncilMod.Settings?.portraitLibrary ?? true) && Load() && records.Count > 0;

        public int Count => Load() ? records.Count : 0;

        private static PortraitPickMode Mode => ColonyCouncilMod.Settings?.portraitPick ?? PortraitPickMode.Closest;

        private static PortraitShareMode ShareMode => ColonyCouncilMod.Settings?.portraitShare ?? PortraitShareMode.NotTogether;

        // ---------------- The library ----------------

        /// <summary>The library: every mod's picture defs (those whose file is there; read once a session) and the library folder's records (again whenever the folder is read); nothing is loaded. What is wrong across them in the log once.</summary>
        private bool Load()
        {
            if (loadedVersion == PortraitFolder.Version) return true;
            loadedVersion = PortraitFolder.Version;
            try
            {
                if (fromDefs == null)
                {
                    fromDefs = new List<Entry>();
                    foreach (var d in DefDatabase<PortraitDef>.AllDefsListForReading.OrderBy(x => x.defName, StringComparer.Ordinal))
                    {
                        var file = d.File;
                        if (file != null) fromDefs.Add(new Entry { record = d.Record, file = file, headX = d.headX, headY = d.headY, headSize = d.headSize });
                    }
                }
                entries.Clear();
                records.Clear();
                pictures.Clear();
                byName.Clear();
                characters.Clear();
                looked.Clear();
                noted.Clear();
                retryAt.Clear();
                namesUntil = -1f;
                void Add(Entry e)
                {
                    if (byName.ContainsKey(e.record.name))
                    {
                        CouncilLog.WarnOnce("Portraits: two pictures named " + e.record.name + "; the one from the library folder is left out.");
                        return;
                    }
                    byName[e.record.name] = records.Count;
                    entries.Add(e);
                    records.Add(e.record);
                    pictures.Add(new PortraitPicture { layout = e.Layout, name = e.record.name, file = e.file, loading = true });
                    characters.Add(e.record.Character);
                }
                foreach (var e in fromDefs) Add(e);
                foreach (var p in PortraitFolder.People.OrderBy(x => x.record.name, StringComparer.Ordinal))
                    Add(new Entry { record = p.record, file = PortraitFolder.FileOf(p.record.name, p.texPath, true), headX = p.headX, headY = p.headY, headSize = p.headSize });
                signature = PortraitLibraryMath.LibrarySignature(records.Select(r => r.name));
                foreach (var problem in PortraitLibraryMath.LibraryProblems(records)) CouncilLog.WarnOnce("Portraits: " + problem);
                if (records.Count > 0)
                    Log.Message("[Rimfolk] Portraits: " + records.Count + " painted portraits of " + characters.Count + " faces in the library (" + fromDefs.Count + " from mods, "
                                + PortraitFolder.People.Count + " from the library folder), " + records.Count(r => r.Pinned) + " of them pinned to colonists; " + Backdrops.Count + " backdrops.");
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
            if (!looked.TryGetValue(id, out var l) || now >= l.until)
            {
                int i = -1;
                try
                {
                    i = Decide(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("portraits: matching a person to the portrait library", e);
                }
                l = new Looked { index = i, until = now + LookEvery };
                looked[id] = l;
            }
            return l.index >= 0 ? Ready(l.index) : null;
        }

        /// <summary>The picture with its texture as the store has it now (null while it loads, and its loading asked for); null where its file can't be read.</summary>
        private PortraitPicture Ready(int i)
        {
            var pic = pictures[i];
            var tex = PortraitStore.Get(pic.file);
            if (tex != null)
            {
                if (pic.texture != tex)
                {
                    pic.texture = tex;
                    pic.layout = entries[i].Layout;
                }
                pic.loading = false;
                return pic;
            }
            pic.texture = null;
            if (PortraitStore.Failed(pic.file)) return null;
            pic.loading = true;
            return pic;
        }

        /// <summary>The colony's own people's names, for the pins: read again every few seconds at most.</summary>
        private void ReadNames()
        {
            float now = Time.realtimeSinceStartup;
            if (now < namesUntil) return;
            namesUntil = now + NamesEvery;
            pinOwners.Clear();
            var pins = records.Where(r => r.Pinned).Select(r => r.pawn).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (pins.Count == 0) return;
            var colonists = new List<KeyValuePair<int, PortraitLibraryMath.PinNames>>();
            foreach (var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
            {
                if (c?.Name == null || c.RaceProps == null || !c.RaceProps.Humanlike) continue;
                var n = c.Name;
                var t = n as NameTriple;
                colonists.Add(new KeyValuePair<int, PortraitLibraryMath.PinNames>(c.thingIDNumber,
                    new PortraitLibraryMath.PinNames(t?.Nick, n.ToStringShort, t?.First, t?.Last, n.ToStringFull)));
            }
            foreach (var pin in pins) pinOwners[pin] = PortraitLibraryMath.PinOwner(pin, colonists);
        }

        /// <summary>
        /// One of the player's own people (a colonist, a slave or a prisoner of the player's; not a guest or a lodger): their
        /// picture is theirs alone (the player, 22 November 2026: "once a pawn is part of my colony, their image should no
        /// longer be used for anyone else", and "once I capture a raider, make him a slave, or even recruit him ... that kind
        /// of locks in the image").
        /// </summary>
        private static bool ColonyOwn(Pawn p) => p.Faction != null && p.Faction == Faction.OfPlayerSilentFail && !p.IsQuestLodger() || p.IsPrisonerOfColony;

        /// <summary>
        /// Someone who matters to the colony beyond its walls (the player: "your diplomatic contacts, maybe they also get not
        /// that their images are repeated"): a faction's leader, a royal title of knight or higher, or one of the colony's
        /// named contacts Rimfolk keeps (heads, heirs, envoys, traders, acquaintances; World/Notables.cs); alive.
        /// </summary>
        private bool IsNotable(Pawn p)
        {
            if (p == null || p.Dead || p.Discarded) return false;
            if (p.Faction != null && p.Faction.leader == p || PortraitReader.Titled(p)) return true;
            var book = NotableBook.Instance;
            if (book != null && (NotableBook.Version != notablesVersion || book != notablesBook))
            {
                notablesVersion = NotableBook.Version;
                notablesBook = book;
                notableIds.Clear();
                foreach (var n in book.notables)
                    if (n != null && n.Alive)
                        notableIds.Add(n.pawn.thingIDNumber);
            }
            return book != null && notableIds.Contains(p.thingIDNumber);
        }

        /// <summary>Whose a person's face may be: the player's own, a notable's, or a stranger's.</summary>
        private PortraitRank RankOf(Pawn p) => ColonyOwn(p) ? PortraitRank.Own : IsNotable(p) ? PortraitRank.Notable : PortraitRank.Stranger;

        /// <summary>Who is on any map now, read again every few seconds.</summary>
        private void ReadOnMaps()
        {
            float now = Time.realtimeSinceStartup;
            if (now < onMapsUntil) return;
            onMapsUntil = now + OnMapsEvery;
            onMaps.Clear();
            foreach (var map in Find.Maps)
            foreach (var q in map.mapPawns.AllPawnsSpawned)
                if (q?.RaceProps != null && q.RaceProps.Humanlike)
                    onMaps.Add(q.thingIDNumber);
        }

        /// <summary>
        /// Every minute at most: a notable's face is let go once they have died or no longer matter (looked at for those seen
        /// this session, the colony's contacts and the factions' leaders; anyone else keeps it until seen again).
        /// </summary>
        private void SweepNotables(PortraitKeeper keeper)
        {
            float now = Time.realtimeSinceStartup;
            if (keeper == null || now < sweepUntil) return;
            sweepUntil = now + SweepEvery;
            var known = new Dictionary<int, Pawn>(notablePawns);
            var book = NotableBook.Instance;
            if (book != null)
                foreach (var n in book.notables)
                    if (n?.pawn != null)
                        known[n.pawn.thingIDNumber] = n.pawn;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
                if (f?.leader != null)
                    known[f.leader.thingIDNumber] = f.leader;
            foreach (var k in keeper.Notables.ToList())
                if (known.TryGetValue(k.id, out var p) && (k.reserved || !IsNotable(p)))
                {
                    keeper.SetNotable(k, false);
                    notablePawns.Remove(k.id);
                }
        }

        /// <summary>
        /// Whether a stranger gives up the face they keep to another stranger who had it first (the setting "Strangers may
        /// share a picture"; <see cref="PortraitLibraryMath.GivesWay"/>); never for a picture with a special look, or one
        /// chosen by hand.
        /// </summary>
        private bool GivesWay(KeptPortrait k, int id, PortraitRecord r, PortraitRank rank, PortraitKeeper keeper)
        {
            if (rank != PortraitRank.Stranger || k.byHand || k.character.Length == 0 || keeper.UsesOf(k.character, id) == 0) return false;
            ReadOnMaps();
            var others = keeper.HoldersOf(k.character, id).Select(h => (h.id, h.since, onMaps.Contains(h.id)));
            return PortraitLibraryMath.GivesWay(ShareMode, r.specials.Count > 0, id, k.since, onMaps.Contains(id), others);
        }

        /// <summary>Whether a face others keep may be given to this person: the player's own and notables take it (its stranger gives way); a stranger as the setting says (<see cref="PortraitLibraryMath.MayShare"/>).</summary>
        private bool MayShare(string character, PortraitRecord r, int id, PortraitRank rank, PortraitKeeper keeper)
        {
            if (rank != PortraitRank.Stranger) return true;
            ReadOnMaps();
            bool seen = keeper != null && keeper.HoldersOf(character, id).Any(h => onMaps.Contains(h.id));
            return PortraitLibraryMath.MayShare(ShareMode, r.specials.Count > 0, seen);
        }

        /// <summary>Whose each face is now: the keeper's colonists' and notables' (the higher rank, then the first to have it) and the pinned pictures' (their colonist's).</summary>
        private void ReadOwners(PortraitKeeper keeper)
        {
            int v = (keeper?.Version ?? 0) * 31 + signature;
            if (v == ownersVersion && keeper == ownersKeeper) return;
            ReadNames();
            ownersVersion = v;
            ownersKeeper = keeper;
            var pinned = records.Where(r => r.Pinned).Select(r => new KeyValuePair<string, int>(r.Character, pinOwners.TryGetValue(r.pawn, out int o) ? o : -1));
            var was = owners;
            owners = PortraitLibraryMath.Owners(keeper?.Holders, pinned);
            // A face newly someone's own: everyone shown is looked at again now, so whoever else had it gets another.
            if (was.Count != owners.Count || owners.Any(kv => !was.TryGetValue(kv.Key, out int o) || o != kv.Value)) looked.Clear();
        }

        /// <summary>Whether a face is another's own (a colonist's, or pinned to someone else).</summary>
        private bool Taken(string character, int id) => !character.NullOrEmpty() && owners.TryGetValue(character, out int owner) && owner != id;

        /// <summary>Whether a pinned picture is this person's (a colonist of the player's the pin names).</summary>
        private bool Theirs(PortraitRecord r, Pawn p)
        {
            if (!r.Pinned || p.Faction != Faction.OfPlayerSilentFail) return false;
            ReadNames();
            return pinOwners.TryGetValue(r.pawn, out int owner) && owner == p.thingIDNumber;
        }

        /// <summary>
        /// The picture for them now (an index, -1 for the game's own face): what they keep, or a new pick where
        /// <see cref="PortraitLibraryMath.PickAgain"/> says so (their pin for their stage first, else the matcher).
        /// </summary>
        private int Decide(Pawn p)
        {
            int id = p.thingIDNumber;
            var keeper = PortraitKeeper.Instance;
            var k = keeper?.Get(id);
            var rank = RankOf(p);
            bool colony = rank == PortraitRank.Own;
            if (rank == PortraitRank.Notable) notablePawns[id] = p;
            // A notable's face held for them at once (and let go when they no longer matter); the player's own outrank them.
            if (k != null && !k.reserved && k.picture.Length > 0) keeper.SetNotable(k, rank == PortraitRank.Notable);
            SweepNotables(keeper);
            ReadOwners(keeper);
            // The plain case, every few seconds for everyone shown: kept, the same library and stage, theirs to keep, no
            // special look gained that the picture doesn't show, and no stranger who had it first in sight with it.
            if (k != null && k.picture.Length > 0 && k.library == signature && !Taken(k.character, id) && (k.reserved || !colony) && byName.TryGetValue(k.picture, out int keptAt)
                && PortraitReader.AgeOf(p, out var ageNow) && (int)PortraitLibraryMath.StageOf(ageNow) == k.stage
                && !PortraitLibraryMath.LacksSpecial(records[keptAt], PortraitLooks.SpecialsOf(p)) && !GivesWay(k, id, records[keptAt], rank, keeper))
            {
                keeper.Seen(k);
                NoteOnce(p, null, keptAt, k.distance, k.pinned || k.byHand);
                return Shown(k) ? keptAt : -1;
            }
            if (!PortraitReader.LookOf(p, out var look)) return -1;
            var stage = PortraitLibraryMath.StageOf(look.age);
            string words = look.Words;
            var pin = PortraitLibraryMath.PinFor(look, records, r => Theirs(r, p));
            int pinFit = pin.Any ? PortraitLibraryMath.PinFit(records[pin.index], look) : 0;
            // Kept from before this round (no stage written): as if nothing were kept.
            bool kept = k != null && k.stage >= 0;
            bool there = kept && byName.ContainsKey(k.picture);
            int keptFit = there && k.pinned ? PortraitLibraryMath.PinFit(records[byName[k.picture]], look) : 0;
            bool lacks = there && PortraitLibraryMath.LacksSpecial(records[byName[k.picture]], look.specials);
            bool givesWay = there && GivesWay(k, id, records[byName[k.picture]], rank, keeper);
            bool again = PortraitLibraryMath.PickAgain(kept, kept ? (PortraitStage)k.stage : stage, stage, k?.picture, there, keptFit, k?.byHand ?? false,
                k?.library ?? 0, signature, k?.words, words, pinFit, there && Taken(k.character, id) || givesWay, lacks);
            // Kept with no picture: looked at again every half minute for one that has come free.
            if (!again && kept && k.picture.Length == 0)
            {
                float now = Time.realtimeSinceStartup;
                if (!retryAt.TryGetValue(id, out float at0) || now >= at0)
                {
                    retryAt[id] = now + RetryEvery;
                    again = at0 > 0f;
                }
            }
            if (!again)
            {
                k.library = signature;
                keeper.Seen(k);
                if (colony) keeper.Reserve(k);
                int at = k.picture.Length > 0 ? byName[k.picture] : -1;
                NoteOnce(p, look, at, k.distance, k.pinned || k.byHand);
                return at >= 0 && Shown(k) ? at : -1;
            }
            var pick = pin.Any ? pin : PortraitLibraryMath.Pick(look, records, id, c => keeper?.UsesOf(c, id) ?? 0, c => Taken(c, id), (c, r) => MayShare(c, r, id, rank, keeper));
            NoteOnce(p, look, pick.index, pick.distance, pin.Any);
            if (keeper == null) return pick.Any && PortraitLibraryMath.Shown(Mode, pin.Any, pick.distance) ? pick.index : -1;
            keeper.Set(id, pick.Any ? records[pick.index].name : "", pick.character, look.Kind, words, signature, stage, pin.Any, false, pick.distance);
            k = keeper.Get(id);
            if (colony) keeper.Reserve(k);
            else keeper.SetNotable(k, rank == PortraitRank.Notable && k.picture.Length > 0);
            return pick.Any && Shown(k) ? pick.index : -1;
        }

        /// <summary>
        /// For the missing list (the save's and the library folder's), once a person each time the library is read: no
        /// picture of their own kind, none of their sex and stage, or none close to their looks (not for a picture pinned
        /// to them or chosen by hand); and each of their looks no picture that may stand for them shows
        /// (<see cref="PortraitLibraryMath.LooksLacking"/>), special and ordinary alike. <paramref name="at"/> is the
        /// picture they have (-1 none), <paramref name="look"/> null where it isn't read yet.
        /// </summary>
        private void NoteOnce(Pawn p, PortraitLook look, int at, float distance, bool ownPicture)
        {
            if (!PortraitNeeds.On || noted.Contains(p.thingIDNumber)) return;
            noted.Add(p.thingIDNumber);
            if (look == null && !PortraitReader.LookOf(p, out look)) return;
            string instead = at >= 0 ? records[at].name : "";
            if (!ownPicture)
            {
                if (!PortraitLibraryMath.HasOwnKind(look, records)) PortraitNeeds.NotePerson(p, look, "CC_PortraitNeed_NoKind", instead);
                // Pictures that would fit them, all someone else's: more of their kind are wanted.
                else if (at < 0 && PortraitLibraryMath.Distances(look, records).Any(d => !float.IsInfinity(d))) PortraitNeeds.NotePerson(p, look, "CC_PortraitNeed_NoFree", "");
                else if (at < 0) PortraitNeeds.NotePerson(p, look, "CC_PortraitNeed_NoStage", "");
                else if (distance > PortraitLibraryMath.CloseEnough) PortraitNeeds.NotePerson(p, look, "CC_PortraitNeed_NoneClose", instead);
            }
            foreach (var w in PortraitLibraryMath.LooksLacking(look, records, r => Theirs(r, p)))
            {
                bool special = look.specials.Contains(w);
                PortraitNeeds.NoteLook(p, look, w, special, special ? "" : instead);
            }
        }

        private static bool Shown(KeptPortrait k) => k != null && PortraitLibraryMath.Shown(Mode, k.pinned || k.byHand, k.distance);

        public void Forget(int pawnId) => looked.Remove(pawnId);

        public void NewGame()
        {
            looked.Clear();
            noted.Clear();
            retryAt.Clear();
            notablePawns.Clear();
            notablesVersion = -1;
            onMapsUntil = sweepUntil = -1f;
            namesUntil = -1f;
        }

        // ---------------- Choose another picture ----------------

        /// <summary>Whether "Choose another picture" has anything to offer this person (the library is on and has a picture that may stand for them).</summary>
        public bool CanChoose(Pawn p) => On && PortraitReader.LookOf(p, out var look) && PortraitLibraryMath.Choices(look, records, r => Theirs(r, p), Mode, c => Taken(c, p.thingIDNumber)).Count > 0;

        /// <summary>
        /// The next of the pictures that may stand for them (their pins, then the nearest faces), kept for them as chosen by
        /// hand (a pin made later doesn't replace it; a child's next stage does). Says what happened.
        /// </summary>
        public string ChooseAnother(Pawn p)
        {
            if (!On || !PortraitReader.LookOf(p, out var look)) return "CC_Portrait_ChooseNone".Translate(p.Named("PAWN")).Resolve();
            var choices = PortraitLibraryMath.Choices(look, records, r => Theirs(r, p), Mode, c => Taken(c, p.thingIDNumber));
            var keeper = PortraitKeeper.Instance;
            var k = keeper?.Get(p.thingIDNumber);
            int next = PortraitLibraryMath.NextChoice(choices, records, k?.picture ?? "");
            if (next < 0 || keeper == null) return "CC_Portrait_ChooseNone".Translate(p.Named("PAWN")).Resolve();
            var r = records[next];
            float d = r.Pinned ? PortraitLibraryMath.Distance(look, r) : PortraitLibraryMath.Distances(look, records)[next];
            keeper.Set(p.thingIDNumber, r.name, r.Character, look.Kind, look.Words, signature, PortraitLibraryMath.StageOf(look.age), r.Pinned, true, d);
            if (ColonyOwn(p)) keeper.Reserve(keeper.Get(p.thingIDNumber));
            Forget(p.thingIDNumber);
            PortraitSources.Forget(p.thingIDNumber);
            return "CC_Portrait_ChooseDone".Translate(p.Named("PAWN"), (choices.IndexOf(next) + 1).Named("AT"), choices.Count.Named("OF")).Resolve();
        }

        // ---------------- The log ----------------

        /// <summary>For the log: their looks, what they keep, their pins, and the nearest few pictures with their scores.</summary>
        private string Explain(Pawn p)
        {
            if (!PortraitReader.LookOf(p, out var look)) return p.LabelShort + ": no picture for them (not humanlike)";
            var k = PortraitKeeper.Instance?.Get(p.thingIDNumber);
            var d = PortraitLibraryMath.Distances(look, records);
            var near = Enumerable.Range(0, records.Count).Where(i => !float.IsInfinity(d[i])).OrderBy(i => d[i]).Take(6)
                .Select(i => records[i].name + " (" + records[i].Character + ") " + d[i].ToString("0.00"));
            var pins = Enumerable.Range(0, records.Count).Where(i => Theirs(records[i], p)).Select(i => records[i].name + " (" + PortraitLibraryMath.Word(records[i].age)
                                                                                                    + (PortraitLibraryMath.PinFits(records[i], look) ? ", fits now" : ", not their stage") + ")");
            string kept = k == null ? "nothing yet"
                : k.picture.Length == 0 ? "no picture (none fitted)"
                : k.picture + (k.pinned ? ", pinned" : "") + (k.byHand ? ", chosen by hand" : "") + ", " + k.distance.ToString("0.00") + (Shown(k) ? "" : " (not shown: only close ones)");
            var rank = RankOf(p);
            return p.LabelShort + " (" + (rank == PortraitRank.Own ? "one of the colony's own" : rank == PortraitRank.Notable ? "a notable" : "a stranger") + "): " + look.Words + (PortraitLibraryMath.HasOwnKind(look, records) ? "" : " (no pictures of their kind)")
                   + (look.specials.Count > 0 ? " (special looks: " + string.Join(", ", look.specials) + ")" : "") + "; keeps " + kept + "; pinned to them: "
                   + (pins.Any() ? string.Join(", ", pins) : "none") + "; nearest: " + (near.Any() ? string.Join(", ", near) : "none that may stand for them");
        }

        // ---------------- Dev mode ----------------

        private static IEnumerable<Pawn> Selected => Find.Selector?.SelectedPawns?.Where(x => x?.RaceProps != null && x.RaceProps.Humanlike) ?? Enumerable.Empty<Pawn>();

        [DebugAction("Rimfolk", "Portraits: the selected people's pictures and best matches with their scores (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugExplain()
        {
            var lines = Selected.Select(p => "  " + Source.Explain(p) + "\n    backdrop: " + Backdrops.Explain(p)).ToList();
            Log.Message("[Rimfolk] Portraits: " + Source.Count + " pictures in the library, " + Backdrops.Count + " backdrops, " + (PortraitKeeper.Instance?.kept.Count ?? 0)
                        + " people keep a picture (a lower score is closer; for \"only close ones\" above " + PortraitLibraryMath.CloseEnough.ToString("0")
                        + " is the game's own face). Loaded this session: " + PortraitStore.Loaded + " (" + PortraitStore.FailedCount + " could not be read), "
                        + PortraitStore.Count + " kept now (" + (PortraitStore.Pixels / 1000000.0).ToString("0.0") + " million pixels), "
                        + PortraitStore.MainMs.ToString("0") + " ms on the game's thread in all.\n" + string.Join("\n", lines));
            Messages.Message("Portraits: the selected people's pictures are in the log.", MessageTypeDefOf.SilentInput, false);
        }

        [DebugAction("Rimfolk", "Portraits: pick the selected people's painted portraits again", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPickAgain()
        {
            int n = 0;
            foreach (var p in Selected)
            {
                PortraitKeeper.Instance?.Drop(p.thingIDNumber);
                Source.Forget(p.thingIDNumber);
                // Looked at again for the missing list too (a trait given in dev mode shows at once).
                Source.noted.Remove(p.thingIDNumber);
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
