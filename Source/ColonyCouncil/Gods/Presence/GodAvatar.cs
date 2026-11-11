using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A god walking on the map (docs/design/gods.md, "Presence", the avatar): any person or animal made a god's by the
    /// hediff Rimfolk_GodAvatar and this comp, which keep its look. Shown, it is drawn larger and in its colours (a
    /// person's skin, hair and the light in their eyes, a god's robe and crown recoloured; an animal's whole body), with an
    /// aura under and behind it, motes rising off it, a light that moves with it, its trail on the ground, its name over
    /// it, a stately walk and its own low sound while the camera is near; the people and animals near it stop and turn to
    /// it (GodReactions.cs). In disguise it looks like anyone and shows only its tells. It is never sold, stripped,
    /// recruited, tamed, eaten or arrested (GodPresencePatches.cs). All the work is done here, for the gods only, a few
    /// times a second at most; nothing is looked at per pawn.
    /// </summary>
    public static class GodAvatar
    {
        public const string HediffName = "Rimfolk_GodAvatar";

        private static HediffDef def;

        public static HediffDef Def => def ??= DefDatabase<HediffDef>.GetNamedSilentFail(HediffName);

        // ---------------- The gods on the maps, by pawn ----------------

        private static readonly Dictionary<int, HediffComp_GodAvatar> byId = new Dictionary<int, HediffComp_GodAvatar>();
        private static readonly List<HediffComp_GodAvatar> all = new List<HediffComp_GodAvatar>();
        private static Game game;

        private static void Fresh()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            byId.Clear();
            all.Clear();
        }

        /// <summary>Any god is walking now (the patches look at this first, so a game without gods pays nothing).</summary>
        public static bool Any
        {
            get
            {
                Fresh();
                return all.Count > 0;
            }
        }

        /// <summary>Every god (shown or in disguise) on any map or off it.</summary>
        public static List<HediffComp_GodAvatar> All
        {
            get
            {
                Fresh();
                return all;
            }
        }

        internal static void Register(HediffComp_GodAvatar c)
        {
            Fresh();
            var p = c?.Pawn;
            if (p == null) return;
            // The gods whose bodies the game has let go of entirely are forgotten.
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var gone = all[i]?.Pawn;
                if (gone != null && !gone.Discarded) continue;
                if (gone != null) byId.Remove(gone.thingIDNumber);
                all.RemoveAt(i);
            }
            if (byId.TryGetValue(p.thingIDNumber, out var old) && old != c) all.Remove(old);
            byId[p.thingIDNumber] = c;
            if (!all.Contains(c)) all.Add(c);
        }

        internal static void Unregister(HediffComp_GodAvatar c)
        {
            Fresh();
            var p = c?.Pawn;
            if (p != null && byId.TryGetValue(p.thingIDNumber, out var old) && old == c) byId.Remove(p.thingIDNumber);
            all.Remove(c);
        }

        /// <summary>The god this pawn is, or null.</summary>
        public static HediffComp_GodAvatar Of(Pawn p)
        {
            if (p == null || all.Count == 0) return null;
            Fresh();
            return byId.TryGetValue(p.thingIDNumber, out var c) ? c : null;
        }

        public static bool Is(Pawn p) => Of(p) != null;

        /// <summary>A god shown as one (not in disguise).</summary>
        public static bool Shown(Pawn p) => Of(p) is HediffComp_GodAvatar c && !c.disguised;

        // ---------------- Making and unmaking ----------------

        /// <summary>
        /// Makes <paramref name="p"/> a god's: <paramref name="name"/> is its key, which the colony's knowledge names (HediffComp_GodAvatar.Label);
        /// <paramref name="disguised"/>: it hides as what it seems until it is revealed. <paramref name="colorA"/> and
        /// <paramref name="colorB"/>: the god's own colours where it has them (the look's otherwise). A pawn already a
        /// god's takes the new look and name. True when it is one.
        /// </summary>
        public static bool Make(Pawn p, GodLookDef look, string name, bool disguised, Color? colorA = null, Color? colorB = null)
        {
            if (p == null || p.Dead || look == null || Def == null || p.health == null) return false;
            try
            {
                var c = Of(p);
                if (c == null)
                {
                    var h = p.health.AddHediff(Def);
                    c = (h as HediffWithComps)?.TryGetComp<HediffComp_GodAvatar>();
                    if (c == null) return false;
                }
                c.Set(look, name, disguised, colorA, colorB);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: making an avatar", e);
                return false;
            }
        }

        /// <summary>
        /// A god in disguise shows who it is, in front of everyone: it grows, takes its colours, its robe and its light.
        /// <paramref name="moment"/>: with its moment too (the music, the sky, everyone turning, the card), with
        /// <paramref name="title"/> and <paramref name="text"/> for the card. False when it wasn't a god in disguise.
        /// </summary>
        public static bool Reveal(Pawn p, bool moment = true, string title = null, string text = null, bool veiled = false)
        {
            var c = Of(p);
            if (c == null || !c.disguised) return false;
            if (moment && p.Spawned)
                return GodPresence.Manifest(c.look, c.godName, null, p.Map, p.Position, PresenceKind.Reveal, p, title, text, veiled, c.ColorA, c.ColorB);
            c.Show(true);
            return true;
        }

        /// <summary>Hides a shown god again (the stranger's guise taken back).</summary>
        public static void Disguise(Pawn p)
        {
            var c = Of(p);
            if (c != null && !c.disguised) c.Hide();
        }

        /// <summary>The pawn is no god's any more: its own colours and clothes come back, the god's robe is gone.</summary>
        public static void Unmake(Pawn p)
        {
            var c = Of(p);
            if (c == null) return;
            try
            {
                var h = c.parent;
                c.Hide();
                p.health.RemoveHediff(h);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: unmaking an avatar", e);
            }
        }
    }

    public class HediffCompProperties_GodAvatar : HediffCompProperties
    {
        public HediffCompProperties_GodAvatar()
        {
            compClass = typeof(HediffComp_GodAvatar);
        }
    }

    /// <summary>The god a pawn is: its look, its name, whether it hides, and what it changed on the pawn (to give back).</summary>
    public class HediffComp_GodAvatar : HediffComp
    {
        public GodLookDef look;
        public string godName;
        public bool disguised;

        private bool ownColors;
        private Color colorA = Color.white, colorB = Color.white;

        // What it changed on a person, kept to give back.
        private bool changedBody;
        private bool hadSkinOverride;
        private Color oldSkin, oldHair;
        private List<Apparel> given = new List<Apparel>();
        private List<Apparel> stored = new List<Apparel>();

        // The day's trail, and when a tell was last remarked on, animals last followed it, a line was last said.
        private int trailDay = -1, trailToday;
        internal int lastTell = -999999, lastFollow = -999999, lastSpoken = -999999;

        // Not saved: the light that follows it, its sound, the reveal's growth.
        [Unsaved] private GodLight light;
        [Unsaved] private Sustainer humming;
        [Unsaved] private float revealedAt = -1f;
        [Unsaved] private float shownSize = -1f;
        [Unsaved] internal IntVec3 lastCell = IntVec3.Invalid;
        [Unsaved] internal int steps;

        public Color ColorA => ownColors ? colorA : look?.colorA ?? Color.white;

        public Color ColorB => ownColors ? colorB : look?.colorB ?? Color.white;

        /// <summary>
        /// What the colony calls it, as it sees it: the core's wiring sets this to the colony's knowledge (its name in the
        /// colony's book once someone has heard of it, else what people see: "a great white thrumbo"). Null: the name as given.
        /// </summary>
        public static Func<string, string> Label;

        /// <summary>Its name over it and in its label, as the colony knows it (<see cref="Label"/>); in disguise the name it gives at the door is the pawn's own.</summary>
        public string Name
        {
            get
            {
                string n = godName.NullOrEmpty() ? look?.sampleName ?? look?.label ?? "" : godName;
                try
                {
                    return Label?.Invoke(n) ?? n;
                }
                catch
                {
                    return n;
                }
            }
        }

        /// <summary>
        /// Its size as drawn now: growing for a moment after a reveal, its full size after; its mortal size in disguise.
        /// Only read here (the game draws pawns on several threads at once): <see cref="RefreshSize"/> works it out each
        /// frame on the game's own thread.
        /// </summary>
        public float DrawSize => disguised || look == null ? 1f : shownSize > 0f ? shownSize : GodPresenceMath.SizeFactor(look.size);

        /// <summary>Works out its size for this frame (GodAuraDrawer, each frame, for the gods on a map).</summary>
        internal void RefreshSize()
        {
            if (disguised || look == null)
            {
                shownSize = 1f;
                return;
            }
            float since = revealedAt < 0f ? -1f : Time.realtimeSinceStartup - revealedAt;
            shownSize = GodPresenceMath.SizeAt(look.size, since);
            if (since >= GodPresenceMath.GrowSeconds) revealedAt = -1f;
        }

        public override string CompLabelInBracketsExtra => disguised ? null : Name;

        public override string CompTipStringExtra => disguised || look == null ? null : "CC_GodP_AvatarTip".Translate(Name.Named("NAME")).Resolve();

        internal void Set(GodLookDef l, string name, bool hide, Color? a, Color? b)
        {
            bool wasShown = !disguised && look != null;
            if (wasShown) Hide();
            look = l;
            godName = name;
            ownColors = a.HasValue || b.HasValue;
            colorA = a ?? l.colorA;
            colorB = b ?? l.colorB;
            disguised = true;
            GodAvatar.Register(this);
            if (!hide) Show(false);
            else Stage();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            GodAvatar.Register(this);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            try
            {
                if (!disguised) Hide();
                DropLight();
                EndHum();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an avatar's end", e);
            }
            GodAvatar.Unregister(this);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            DropLight();
            EndHum();
        }

        // ---------------- Shown and hidden ----------------

        /// <summary>The hediff's stage: hidden in disguise (nothing on its health page), shown with the stately walk.</summary>
        private void Stage()
        {
            if (parent == null) return;
            parent.Severity = disguised ? 0.5f : 1f;
            // The game keeps a hediff it has once shown visible for good; a god gone back into hiding is hidden again.
            if (disguised)
            {
                try
                {
                    HediffVisible(parent) = false;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: hiding the avatar's hediff", e);
                }
            }
        }

        private static readonly HarmonyLib.AccessTools.FieldRef<Hediff, bool> HediffVisible = HarmonyLib.AccessTools.FieldRefAccess<Hediff, bool>("visible");

        /// <summary>Takes its colours, its robe and its light. <paramref name="grow"/>: grows into its size before everyone's eyes.</summary>
        internal void Show(bool grow)
        {
            if (look == null) return;
            var p = Pawn;
            disguised = false;
            Stage();
            revealedAt = grow ? Time.realtimeSinceStartup : -1f;
            shownSize = grow ? 1f : -1f;
            try
            {
                if (p.RaceProps.Humanlike && p.story != null)
                {
                    if (!changedBody)
                    {
                        hadSkinOverride = p.story.skinColorOverride.HasValue;
                        oldSkin = p.story.skinColorOverride ?? Color.white;
                        oldHair = p.story.HairColor;
                        changedBody = true;
                    }
                    p.story.skinColorOverride = look.skin;
                    p.story.HairColor = look.hair;
                    Dress(p);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an avatar's look", e);
            }
            Redraw(p);
        }

        /// <summary>Its own colours and clothes back, the god's robe gone, the light out.</summary>
        internal void Hide()
        {
            var p = Pawn;
            disguised = true;
            Stage();
            revealedAt = -1f;
            DropLight();
            EndHum();
            try
            {
                if (changedBody && p.story != null)
                {
                    p.story.skinColorOverride = hadSkinOverride ? oldSkin : (Color?)null;
                    p.story.HairColor = oldHair;
                    changedBody = false;
                }
                Undress(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an avatar's own look back", e);
            }
            Redraw(p);
        }

        private static void Redraw(Pawn p)
        {
            if (p == null) return;
            try
            {
                p.Drawer?.renderer?.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(p);
                GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: redrawing an avatar", e);
            }
        }

        // ---------------- Its robe and crown ----------------

        private static readonly string[] FineStuff = { "Gold", "Silver", "Hyperweave", "Synthread", "DevilstrandCloth", "Cloth", "Leather_Plain" };

        private void Dress(Pawn p)
        {
            if (p.apparel == null || look == null) return;
            given.RemoveAll(a => a == null || a.Destroyed || a.Wearer != p);
            if (given.Count > 0) return;
            Wear(p, look.BodyApparel, ColorA);
            Wear(p, look.HeadApparel, ColorB);
        }

        private void Wear(Pawn p, List<ThingDef> defs, Color color)
        {
            foreach (var d in defs)
            {
                if (d.apparel == null || !d.apparel.PawnCanWear(p) || !ApparelUtility.HasPartsToWear(p, d)) continue;
                ThingDef stuff = null;
                if (d.MadeFromStuff)
                {
                    var allowed = GenStuff.AllowedStuffsFor(d).ToList();
                    stuff = FineStuff.Select(n => allowed.FirstOrDefault(s => s.defName == n)).FirstOrDefault(s => s != null) ?? GenStuff.DefaultStuffFor(d);
                    if (stuff == null) continue;
                }
                var ap = ThingMaker.MakeThing(d, stuff) as Apparel;
                if (ap == null) continue;
                if (!Drawn(ap, p))
                {
                    ap.Destroy();
                    continue;
                }
                ap.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Masterwork, ArtGenerationContext.Outsider);
                ap.HitPoints = ap.MaxHitPoints;
                if (ap.TryGetComp<CompColorable>() != null) ap.SetColor(color, false);
                // What it covers is put away in their pack, to wear again when the god is gone.
                foreach (var worn in p.apparel.WornApparel.ToList())
                {
                    if (ApparelUtility.CanWearTogether(d, worn.def, p.RaceProps.body)) continue;
                    p.apparel.Remove(worn);
                    if (p.inventory != null && p.inventory.innerContainer.TryAdd(worn, false)) stored.Add(worn);
                    else if (p.Spawned) GenPlace.TryPlaceThing(worn, p.Position, p.Map, ThingPlaceMode.Near);
                }
                p.apparel.Wear(ap, false, true);
                given.Add(ap);
                return;
            }
        }

        /// <summary>The piece has a picture for this body (a robe for a woman only, or for some bodies, isn't tried on the others).</summary>
        private static bool Drawn(Apparel ap, Pawn p)
        {
            string path = ap.WornGraphicPath;
            if (path.NullOrEmpty() || p.story?.bodyType == null) return false;
            var last = ap.def.apparel.LastLayer;
            bool plain = last == ApparelLayerDefOf.Overhead || last == ApparelLayerDefOf.EyeCover || ap.RenderAsPack();
            string full = plain ? path : path + "_" + p.story.bodyType.defName;
            return ContentFinder<Texture2D>.Get(full + "_south", false) != null;
        }

        private void Undress(Pawn p)
        {
            if (p?.apparel == null) return;
            foreach (var ap in given.ToList())
            {
                if (ap == null) continue;
                if (ap.Wearer == p)
                {
                    p.apparel.Unlock(ap);
                    p.apparel.Remove(ap);
                }
                if (!ap.Destroyed) ap.Destroy();
            }
            given.Clear();
            foreach (var ap in stored.ToList())
            {
                if (ap == null || ap.Destroyed) continue;
                if (p.inventory != null && p.inventory.innerContainer.Contains(ap))
                {
                    p.inventory.innerContainer.Remove(ap);
                    if (ap.PawnCanWear(p, true)) p.apparel.Wear(ap, false);
                    else p.inventory.innerContainer.TryAdd(ap, false);
                }
            }
            stored.Clear();
        }

        /// <summary>One of the god's own pieces (never sold, stripped or taken off).</summary>
        public bool Gave(Apparel ap) => ap != null && given.Contains(ap);

        // ---------------- Each tick, for the gods only ----------------

        public override void CompPostTick(ref float severityAdjustment)
        {
            var p = Pawn;
            if (p == null || look == null) return;
            if (!p.Spawned || p.Dead)
            {
                DropLight();
                EndHum();
                return;
            }
            try
            {
                int now = Find.TickManager.TicksGame;
                if (disguised)
                {
                    DropLight();
                    EndHum();
                    if (p.IsHashIntervalTick(250)) GodReactions.Tells(this, p, now);
                    return;
                }
                if (p.IsHashIntervalTick(15)) KeepLight(p);
                if (p.IsHashIntervalTick(30)) Trail(p, now);
                if (p.IsHashIntervalTick(GodFx.MoteEvery(p.Map))) GodFx.Rise(this, p);
                if (p.IsHashIntervalTick(250)) GodReactions.Passing(this, p, now);
                Hum(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an avatar's tick", e);
            }
        }

        // ---------------- The light that follows it ----------------

        private void KeepLight(Pawn p)
        {
            if (!GodAura.On)
            {
                DropLight();
                return;
            }
            if (light != null && (light.Destroyed || light.Map != p.Map)) DropLight();
            if (light == null)
            {
                light = GodLight.Spawn(p, look, ColorA);
                return;
            }
            light.Follow(p.Position);
        }

        private void DropLight()
        {
            if (light != null && !light.Destroyed) light.Destroy();
            light = null;
        }

        // ---------------- Its trail ----------------

        private void Trail(Pawn p, int now)
        {
            var cell = p.Position;
            if (cell == lastCell) return;
            var from = lastCell;
            lastCell = cell;
            if (!from.IsValid) return;
            int day = now / 60000;
            if (day != trailDay)
            {
                trailDay = day;
                trailToday = 0;
            }
            steps++;
            bool lasting = steps % GodPresenceMath.LastingEvery == 0 && trailToday < GodPresenceMath.LastingADay;
            if (GodTrails.Step(look, p.Map, from, lasting, ColorA)) trailToday++;
        }

        // ---------------- Its sound ----------------

        private void Hum(Pawn p)
        {
            if (!GodPresence.MusicOn || p.Map != Find.CurrentMap)
            {
                EndHum();
                return;
            }
            if (humming != null)
            {
                if (humming.Ended) humming = null;
                else
                {
                    humming.Maintain();
                    if (p.IsHashIntervalTick(120) && !GodSounds.Near(p)) EndHum();
                    return;
                }
            }
            if (!p.IsHashIntervalTick(120) || !GodSounds.Near(p)) return;
            humming = GodSounds.Hum(look, p);
        }

        private void EndHum()
        {
            if (humming != null && !humming.Ended) humming.End();
            humming = null;
        }

        // ---------------- Saved ----------------

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Defs.Look(ref look, "look");
            Scribe_Values.Look(ref godName, "godName");
            Scribe_Values.Look(ref disguised, "disguised");
            Scribe_Values.Look(ref ownColors, "ownColors");
            Scribe_Values.Look(ref colorA, "colorA", Color.white);
            Scribe_Values.Look(ref colorB, "colorB", Color.white);
            Scribe_Values.Look(ref changedBody, "changedBody");
            Scribe_Values.Look(ref hadSkinOverride, "hadSkinOverride");
            Scribe_Values.Look(ref oldSkin, "oldSkin", Color.white);
            Scribe_Values.Look(ref oldHair, "oldHair", Color.white);
            Scribe_Collections.Look(ref given, "given", LookMode.Reference);
            Scribe_Collections.Look(ref stored, "stored", LookMode.Reference);
            Scribe_Values.Look(ref trailDay, "trailDay", -1);
            Scribe_Values.Look(ref trailToday, "trailToday");
            Scribe_Values.Look(ref lastTell, "lastTell", -999999);
            Scribe_Values.Look(ref lastFollow, "lastFollow", -999999);
            Scribe_Values.Look(ref lastSpoken, "lastSpoken", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                given ??= new List<Apparel>();
                stored ??= new List<Apparel>();
                given.RemoveAll(a => a == null);
                stored.RemoveAll(a => a == null);
                GodAvatar.Register(this);
            }
        }
    }
}
