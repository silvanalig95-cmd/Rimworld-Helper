using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// More of Rimfolk's events seen on the map: a camp arriving raises a little dust on the road and gets a word from
    /// whoever sees them come; the first snow's flakes catch on the colonists out in it; a dream is told at breakfast by
    /// the one who had it, over the bowl; a wedding the council promised (or of one of its officers) is rung in with the
    /// council's bell, a few notes over the guests and a word from a councillor; and a Hospitality guest now and then
    /// says something about their stay (a bed, a feast, how it's going), from where they stand.
    /// </summary>
    /// <remarks>
    /// Run by <see cref="VisualTouches"/>' clock, so only with its setting on, only on the map being looked at and only in
    /// view; lines also need floating speech text, and share the touches' rate (one ambient line a colonist a day or
    /// so). At most two puffs or flakes a pulse. Nobody is moved or changed, a guest least of all. Nothing is saved.
    /// </remarks>
    internal sealed class EventTouches
    {
        private static readonly Color DustColor = new Color(0.92f, 0.86f, 0.74f, 0.85f);
        private static readonly Color FlakeColor = new Color(0.96f, 0.98f, 1f, 0.9f);
        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.88f);
        private static readonly Color GuestColor = new Color(0.86f, 0.9f, 0.98f);

        /// <summary>The camp someone already remarked on, by id.</summary>
        private int campSeen = -1;
        /// <summary>Whose dream was told at breakfast: the dreamer's id and the dream's tick.</summary>
        private readonly Dictionary<int, int> dreamsTold = new Dictionary<int, int>();
        /// <summary>When each guest (by id) may say something again.</summary>
        private readonly Dictionary<int, int> guestUntil = new Dictionary<int, int>();

        private static bool looked;
        private static FleckDef snowBits, music;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            snowBits = DefDatabase<FleckDef>.GetNamedSilentFail("SnowBits");
            music = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
        }

        private static bool Speech => ColonyCouncilMod.Settings?.speechText == true;

        // ---------------- Every pulse ----------------

        public void Pulse(VisualTouches vt, Map map, CellRect view, int now, int pulses)
        {
            Look();
            if (pulses % 2 == 0) Camp(vt, map, view, now);
            if (pulses % 3 == 1) Flakes(map, view);
            if (pulses % 8 == 3) Breakfast(vt, map, view, now);
        }

        // ---------------- A camp arriving ----------------

        /// <summary>Dust behind the camp's people as they walk in, and a word from a colonist who sees them come (once a camp).</summary>
        private void Camp(VisualTouches vt, Map map, CellRect view, int now)
        {
            var camp = CampTracker.Instance?.camp;
            if (camp == null || camp.map != map || !camp.Active || camp.members == null) return;
            bool coming = camp.phase == CampPhase.Coming;
            if (!coming && camp.phase != CampPhase.Building) return;
            bool seen = false;
            int made = 0;
            foreach (var p in camp.members)
            {
                if (p == null || p.Dead || !p.Spawned || p.Map != map || !view.Contains(p.Position)) continue;
                seen = true;
                if (!coming || made >= 2 || p.pather == null || !p.pather.Moving || !Rand.Chance(0.45f)) continue;
                if (!VisualTouches.Dusty(map, p.Position) || VisualTouches.Wet(map)) continue;
                FleckMaker.ThrowDustPuffThick(p.DrawPos + new Vector3(0f, 0f, -0.3f), map, Rand.Range(0.8f, 1.2f), DustColor);
                made++;
            }
            if (!seen || campSeen == camp.id || !Speech) return;
            campSeen = camp.id;
            var who = Someone(vt, map, view, now, p => !camp.Has(p));
            if (who == null) return;
            string kind = camp.kind?.defName ?? "";
            if (kind.StartsWith("Council_Camp_", StringComparison.Ordinal)) kind = kind.Substring("Council_Camp_".Length);
            string line = Flavor.FromPool("Council_Pool_TouchCampComing", who, null, null, "camp:" + kind);
            if (line.NullOrEmpty()) return;
            CouncilGameComponent.Speak(who, line.Formatted(who.Named("PAWN")).Resolve(), SpeechColor);
            vt.Spoke(who, now + GenDate.TicksPerDay);
        }

        // ---------------- The first snow ----------------

        /// <summary>In the first hours of the first snow, a flake now and then catches on someone out in it.</summary>
        private static void Flakes(Map map, CellRect view)
        {
            if (!WeatherTalk.FreshSnow(map)) return;
            var pawns = map.mapPawns.FreeColonistsSpawned;
            int count = pawns.Count;
            if (count == 0) return;
            int start = Rand.Range(0, count), made = 0;
            for (int i = 0; i < count && made < 2; i++)
            {
                var p = pawns[(start + i) % count];
                if (p == null || !view.Contains(p.Position) || p.Position.Roofed(map) || !Rand.Chance(0.5f)) continue;
                var at = p.DrawPos + new Vector3(Rand.Range(-0.25f, 0.25f), 0f, Rand.Range(0.35f, 0.75f));
                var data = FleckMaker.GetDataStatic(at, map, snowBits ?? FleckDefOf.AirPuff, snowBits != null ? Rand.Range(0.3f, 0.45f) : 0.3f);
                data.instanceColor = FlakeColor;
                data.velocityAngle = Rand.Range(160f, 200f);
                data.velocitySpeed = Rand.Range(0.05f, 0.2f);
                data.rotationRate = Rand.Range(-60f, 60f);
                map.flecks.CreateFleck(data);
                made++;
            }
        }

        // ---------------- A dream told at breakfast ----------------

        /// <summary>
        /// The morning after a dream, the dreamer at their breakfast with someone near tells it, in a line over the bowl:
        /// once a dream, and only for a dream still untold (the game's own talk about it can still follow).
        /// </summary>
        private void Breakfast(VisualTouches vt, Map map, CellRect view, int now)
        {
            if (!Speech || ColonyCouncilMod.Settings?.dreams != true) return;
            var untold = CouncilGameComponent.Instance?.dreams?.untold;
            if (untold == null || untold.Count == 0) return;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 5 || hour > 11) return;
            if (dreamsTold.Count > 60) dreamsTold.Clear();
            foreach (var kv in untold)
            {
                var p = kv.Key;
                var memo = kv.Value;
                if (p == null || memo == null || p.Map != map || now - memo.tick > GenDate.TicksPerHour * 12) continue;
                if (dreamsTold.TryGetValue(p.thingIDNumber, out int told) && told == memo.tick) continue;
                if (p.CurJobDef != JobDefOf.Ingest || !vt.Free(p, map, view, now)) continue;
                var listener = Near(p, map);
                if (listener == null) continue;
                string line = Flavor.FromPool("Council_Pool_SayDream", p, null, null, "dream:" + memo.kind);
                if (line.NullOrEmpty()) continue;
                var other = memo.other ?? listener;
                CouncilGameComponent.Speak(p, line.Formatted(p.Named("PAWN"), other.Named("OTHER")).Resolve(), SpeechColor);
                vt.Spoke(p, now + GenDate.TicksPerDay / 2);
                dreamsTold[p.thingIDNumber] = memo.tick;
                return;
            }
        }

        /// <summary>Someone awake within a few steps to tell it to.</summary>
        private static Pawn Near(Pawn p, Map map)
        {
            foreach (var o in map.mapPawns.FreeColonistsSpawned)
                if (o != p && o.Awake() && !o.Downed && o.Position.InHorDistOf(p.Position, 5f) && People.HasInnerLife(o)) return o;
            return null;
        }

        // ---------------- A wedding of the council ----------------

        /// <summary>
        /// Two people just married, and the council had a hand in it: their wedding was a wish it took up, or one of them
        /// holds an office. The council's bell, a few notes over the guests, and a word from a councillor (or a friend).
        /// </summary>
        public static void CouncilWedding(VisualTouches vt, Pawn a, Pawn b)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || a == null || b == null) return;
            var at = a.Spawned ? a : b.Spawned ? b : null;
            if (at == null || !VisualTouches.InView(out var map, out var view) || map != at.Map || !view.ExpandedBy(12).Contains(at.Position)) return;
            bool offices = ColonyCouncilMod.Settings?.offices == true && comp.offices != null;
            bool officer = offices && (comp.offices.OfficeOf(a) != null || comp.offices.OfficeOf(b) != null);
            bool promised = comp.wishes != null && comp.wishes.Any(w => w != null && w.state == WishState.Open && w.def?.defName == "Council_Wish_Wedding" && (w.Involves(a) || w.Involves(b)));
            if (!officer && !promised) return;
            Look();
            SoundDefOf.TinyBell?.PlayOneShot(SoundInfo.InMap(new TargetInfo(at.Position, map)));
            int now = Find.TickManager.TicksGame;
            Pawn speaker = null;
            int singers = 0;
            foreach (var o in map.mapPawns.FreeColonistsSpawned.InRandomOrder())
            {
                if (o == a || o == b || !o.Position.InHorDistOf(at.Position, 12f) || !VisualTouches.CanSpeak(o, map, view)) continue;
                if (music != null && singers < 3)
                {
                    vt.QueueFleck(o, music, 2, Rand.RangeInclusive(1, 4));
                    singers++;
                }
                bool councillor = offices && comp.offices.OfficeOf(o) != null;
                if (speaker == null || (councillor && !(offices && comp.offices.OfficeOf(speaker) != null))) speaker = o;
            }
            if (speaker == null || !Speech) return;
            bool speakerHolds = offices && comp.offices.OfficeOf(speaker) != null;
            string line = Flavor.FromPool("Council_Pool_TouchCouncilWedding", speaker, null, null, speakerHolds ? "councillor" : "friend", promised ? "promised" : "officer");
            if (line.NullOrEmpty()) return;
            CouncilGameComponent.Speak(speaker, line.Formatted(speaker.Named("PAWN"), a.Named("A"), b.Named("B")).Resolve(), SpeechColor);
            vt.Spoke(speaker, now + GenDate.TicksPerDay / 2);
        }

        // ---------------- A guest's word ----------------

        /// <summary>
        /// Once an hour at most: a Hospitality guest in view says something about their stay, from how it's going and the
        /// bed they were given, or about the gathering they're standing near. True when a line was said.
        /// </summary>
        public bool Hourly(Map map, CellRect view, int now)
        {
            if (!Speech || !People.ModActive(Guests.Mod) || !Rand.Chance(0.35f)) return false;
            if (guestUntil.Count > 0)
            {
                List<int> gone = null;
                foreach (var kv in guestUntil)
                    if (kv.Value <= now) (gone ??= new List<int>()).Add(kv.Key);
                if (gone != null) foreach (int k in gone) guestUntil.Remove(k);
            }
            foreach (var g in Strangers.On(map, StrangerKind.Visitor).InRandomOrder())
            {
                if (!Strangers.IsHospitalityGuest(g) || !VisualTouches.CanSpeak(g, map, view) || g.Drafted) continue;
                if (guestUntil.TryGetValue(g.thingIDNumber, out int until) && until > now) continue;
                var tags = new List<string>();
                float score = Guests.VisitScore(g);
                if (score >= 0.65f) tags.Add("happy");
                else if (score >= 0f && score < 0.35f) tags.Add("unhappy");
                var room = Guests.RoomOf(g);
                if (room == null) tags.Add("nobed");
                else
                {
                    float imp = RoomUtil.Impressiveness(room);
                    if (imp >= 50f) tags.Add("roomgood");
                    else if (imp < 25f) tags.Add("roompoor");
                }
                if (AtGathering(g, map)) tags.Add("gathering");
                string line = Flavor.FromPool("Council_Pool_GuestSays", g, null, null, tags.ToArray());
                if (line.NullOrEmpty()) continue;
                var faction = Strangers.FactionOf(g);
                CouncilGameComponent.Speak(g, line.Formatted(g.Named("PAWN"), (faction?.Name ?? "").Named("FACTION")).Resolve(), GuestColor);
                guestUntil[g.thingIDNumber] = now + GenDate.TicksPerDay;
                return true;
            }
            return false;
        }

        /// <summary>Within a few steps of a gathering the council's answer called (a feast, a toast, a bonfire).</summary>
        internal static bool AtGathering(Pawn g, Map map)
        {
            foreach (var lord in map.lordManager.lords)
                if (lord?.LordJob is LordJob_PlayOut job && job.Spot.IsValid && job.Spot.InHorDistOf(g.Position, 9f)) return true;
            return false;
        }

        /// <summary>Hospitality's guests standing near a gathering's spot (the toast's cups go up for them too).</summary>
        internal static IEnumerable<Pawn> GuestsNear(Map map, IntVec3 spot, float radius)
        {
            if (map == null || !spot.IsValid || !People.ModActive(Guests.Mod)) yield break;
            foreach (var g in Strangers.On(map, StrangerKind.Visitor))
                if (Strangers.IsHospitalityGuest(g) && g.Spawned && g.Position.InHorDistOf(spot, radius)) yield return g;
        }

        // ---------------- Who ----------------

        /// <summary>A colonist in view, free to say an ambient line, who fits.</summary>
        private static Pawn Someone(VisualTouches vt, Map map, CellRect view, int now, Predicate<Pawn> fits)
        {
            var pawns = map.mapPawns.FreeColonistsSpawned;
            int count = pawns.Count;
            if (count == 0) return null;
            int start = Rand.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                var p = pawns[(start + i) % count];
                if (vt.Free(p, map, view, now) && (fits == null || fits(p))) return p;
            }
            return null;
        }
    }
}
