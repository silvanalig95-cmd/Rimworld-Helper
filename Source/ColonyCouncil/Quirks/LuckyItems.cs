using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How a lucky thing stopped being someone's.</summary>
    public enum LuckyEnd : byte
    {
        /// <summary>The quirk faded (or a story ended it): they stopped needing it.</summary>
        Faded,
        Died,
        /// <summary>They were lost to the colony for good (the game let them go).</summary>
        Gone
    }

    /// <summary>
    /// Someone's lucky thing that is theirs no longer: whose it was (by name, so it outlives them), for how long, and how
    /// it ended. Kept by the thing's id number, never a reference, so nothing is left dangling once the thing is gone;
    /// the record goes when the thing is destroyed (Patch_LuckyDestroyed).
    /// </summary>
    public class LuckyRecord : IExposable
    {
        public int thingId;
        public string owner;
        public Gender gender;
        public int fromTick, toTick;
        public LuckyEnd end;

        public void ExposeData()
        {
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref owner, "owner");
            Scribe_Values.Look(ref gender, "gender");
            Scribe_Values.Look(ref fromTick, "fromTick");
            Scribe_Values.Look(ref toTick, "toTick");
            Scribe_Values.Look(ref end, "end");
        }
    }

    /// <summary>
    /// Lucky things show it. A lucky-item quirk (Quirks.cs) belongs to one thing, the helmet or rifle they had on when
    /// they decided it was lucky (older saves pick the one of that kind they wear or carry, within the hour), and that
    /// thing says so: "Macy's lucky charm" in the inspector, and a line in its info card and its tooltip in the gear tab.
    /// When the quirk fades or its owner dies, the thing remembers: "Once Hector's lucky charm".
    /// <para>
    /// The game asks for these texts every frame, so they come from a dictionary by thing id, rebuilt only when a lucky
    /// thing is gained, bound, ended or destroyed; each thing's text is built at most once an hour.
    /// </para>
    /// </summary>
    public static class LuckyItems
    {
        /// <summary>Past owners kept at most (the oldest go first).</summary>
        public const int MaxPast = 200;

        private sealed class Entry
        {
            public Quirk now;
            public List<LuckyRecord> past;
            public string inspect, description;
            public int builtTick = -1;
        }

        private static readonly Dictionary<int, Entry> index = new Dictionary<int, Entry>();
        private static CouncilGameComponent builtFor;
        private static bool dirty = true;

        /// <summary>Whether any thing might be lucky (true while the index waits to be rebuilt). Cheap: asked every frame.</summary>
        public static bool Any => dirty || index.Count > 0;

        /// <summary>Something changed (a quirk gained, bound or ended, a game loaded): rebuilt when next asked.</summary>
        public static void MarkDirty() => dirty = true;

        private static bool Ensure()
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.quirks == null) return false;
            if (dirty || builtFor != comp) Rebuild(comp);
            return true;
        }

        private static void Rebuild(CouncilGameComponent comp)
        {
            dirty = false;
            builtFor = comp;
            index.Clear();
            foreach (var q in comp.quirks.quirks)
                if (IsLucky(q) && q.thingId != 0 && q.pawn != null)
                {
                    var e = Get(q.thingId);
                    if (e.now == null) e.now = q;
                }
            foreach (var r in comp.quirks.luckyPast)
                if (r != null && r.thingId != 0 && !r.owner.NullOrEmpty())
                    (Get(r.thingId).past ??= new List<LuckyRecord>()).Add(r);
            foreach (var e in index.Values) e.past?.Sort((a, b) => a.toTick.CompareTo(b.toTick));
        }

        private static Entry Get(int id)
        {
            if (!index.TryGetValue(id, out var e))
            {
                e = new Entry();
                index[id] = e;
            }
            return e;
        }

        public static bool IsLucky(Quirk q) => q?.def != null && q.def.condition == QuirkCondition.LuckyItem;

        // ---------------- Whose it is ----------------

        /// <summary>The thing someone gaining a lucky-item quirk decides is lucky: a hat or helmet first, else their weapon, else anything worn.</summary>
        public static Thing Pick(Pawn p)
        {
            var worn = p?.apparel?.WornApparel;
            if (worn != null)
                foreach (var a in worn)
                    if (a?.def?.apparel?.layers != null && a.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead))
                        return a;
            var primary = p?.equipment?.Primary;
            if (primary != null) return primary;
            return worn.NullOrEmpty() ? null : worn.RandomElement();
        }

        /// <summary>Whether this thing is already someone's lucky thing now.</summary>
        public static bool Claimed(QuirkTracker tracker, int thingId)
        {
            if (tracker?.quirks == null || thingId == 0) return false;
            foreach (var q in tracker.quirks)
                if (q != null && q.thingId == thingId && IsLucky(q))
                    return true;
            return false;
        }

        /// <summary>
        /// Hourly: a lucky quirk without a thing of its own (from an older save, or its thing was destroyed) takes the
        /// one of its kind they wear or wield now, if nobody else counts that one as theirs.
        /// </summary>
        public static void Bind(QuirkTracker tracker)
        {
            if (tracker?.quirks == null) return;
            foreach (var q in tracker.quirks)
            {
                if (!IsLucky(q) || q.thingId != 0 || q.item == null || q.pawn == null || q.pawn.Dead) continue;
                var t = Carried(q.pawn, q.item);
                if (t == null || t.thingIDNumber <= 0 || Claimed(tracker, t.thingIDNumber)) continue;
                q.thingId = t.thingIDNumber;
                MarkDirty();
            }
        }

        private static Thing Carried(Pawn p, ThingDef def)
        {
            var worn = p.apparel?.WornApparel;
            if (worn != null)
                for (int i = 0; i < worn.Count; i++)
                    if (worn[i]?.def == def)
                        return worn[i];
            var primary = p.equipment?.Primary;
            return primary != null && primary.def == def ? primary : null;
        }

        /// <summary>A lucky-item quirk is ending: its thing remembers whose it was.</summary>
        public static void Remember(QuirkTracker tracker, Quirk q, LuckyEnd end)
        {
            if (tracker == null || !IsLucky(q) || q.thingId == 0 || q.pawn == null) return;
            tracker.luckyPast ??= new List<LuckyRecord>();
            tracker.luckyPast.Add(new LuckyRecord
            {
                thingId = q.thingId,
                owner = q.pawn.LabelShort,
                gender = q.pawn.gender,
                fromTick = q.gainedTick,
                toTick = Find.TickManager?.TicksGame ?? q.gainedTick,
                end = end
            });
            if (tracker.luckyPast.Count > MaxPast) tracker.luckyPast.RemoveRange(0, tracker.luckyPast.Count - MaxPast);
            MarkDirty();
        }

        /// <summary>
        /// A thing is being destroyed (Patch_LuckyDestroyed): its past owners are forgotten with it, and whoever counted
        /// it as lucky now is free to settle on another of its kind.
        /// </summary>
        public static void Notify_Destroyed(Thing t)
        {
            if (t == null || !Any || !Ensure() || !index.ContainsKey(t.thingIDNumber)) return;
            int id = t.thingIDNumber;
            var tracker = builtFor.quirks;
            tracker.luckyPast?.RemoveAll(r => r == null || r.thingId == id);
            foreach (var q in tracker.quirks)
                if (q != null && q.thingId == id)
                    q.thingId = 0;
            MarkDirty();
        }

        // ---------------- What it says ----------------

        private static Entry EntryFor(Thing t)
        {
            if (t == null || !Any) return null;
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.quirks || Current.ProgramState != ProgramState.Playing || !Ensure()) return null;
            if (!index.TryGetValue(t.thingIDNumber, out var e)) return null;
            Refresh(e);
            return e;
        }

        /// <summary>"Macy's lucky charm", "Macy's lucky charm, once Hector's", "Once Hector's and Ana's lucky charm"; or null.</summary>
        public static string InspectLine(Thing t) => EntryFor(t)?.inspect;

        /// <summary>A line for each owner, now and before, for the info card and the gear tab's tooltip; or null.</summary>
        public static string Description(Thing t) => EntryFor(t)?.description;

        /// <summary>Adds the lucky lines to a thing's description (once, however many getters the game goes through).</summary>
        public static void AppendDescription(Thing t, ref string text)
        {
            if (!Any || t == null || t is Pawn) return;
            try
            {
                string part = Description(t);
                if (part.NullOrEmpty() || (text != null && text.Contains(part))) return;
                text = text.NullOrEmpty() ? part : text.TrimEndNewlines() + "\n\n" + part;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lucky things: description", e);
            }
        }

        private static void Refresh(Entry e)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (e.builtTick >= 0 && now >= e.builtTick && now - e.builtTick < GenDate.TicksPerHour) return;
            e.builtTick = now;
            var q = e.now;
            bool alive = q?.pawn != null && !q.pawn.Dead && q.def != null;
            string owner = alive ? q.pawn.LabelShort : null;
            var once = new List<string>();
            var lines = new List<string>();
            if (e.past != null)
                foreach (var r in e.past)
                {
                    // A feeling that faded and came back to the same person isn't a second owner.
                    if (r.owner != owner && !once.Contains(r.owner)) once.Add(r.owner);
                    lines.Add(PastLine(r.owner, r.gender, r.toTick - r.fromTick, r.end));
                }
            // Died since the last daily look: theirs no longer, though the record isn't written yet.
            if (q?.pawn != null && !alive && q.pawn.Dead)
            {
                string name = q.pawn.LabelShort;
                if (!once.Contains(name)) once.Add(name);
                lines.Add(PastLine(name, q.pawn.gender, now - q.gainedTick, LuckyEnd.Died));
            }
            lines.Reverse();
            string onceText = once.Count == 0 ? null : TextUtil.JoinAnd(once.Select(n => "CC_Lucky_Possessive".Translate(n).ToString()).ToList());
            if (owner != null)
            {
                e.inspect = onceText == null ? "CC_Lucky_Now".Translate(owner).Resolve() : "CC_Lucky_NowOnce".Translate(owner, onceText).Resolve();
                lines.Insert(0, "CC_Lucky_Desc_Now".Translate(owner, Days(now - q.gainedTick), Pronoun(owner, q.pawn.gender)).Resolve());
            }
            else e.inspect = onceText == null ? null : "CC_Lucky_Once".Translate(onceText).Resolve();
            e.description = lines.Count == 0 ? null : string.Join("\n", lines);
        }

        private static string PastLine(string owner, Gender gender, int ticks, LuckyEnd end) =>
            ("CC_Lucky_Desc_" + end).Translate(owner, Days(ticks), Pronoun(owner, gender)).Resolve();

        private static string Days(int ticks) => TextUtil.DaysString(Math.Max(GenDate.TicksPerHour, ticks));

        /// <summary>"she", "he", or their name (the game has no pronoun to use for everyone else).</summary>
        private static string Pronoun(string owner, Gender gender) => gender == Gender.Male || gender == Gender.Female ? gender.GetPronoun() : owner;
    }
}
