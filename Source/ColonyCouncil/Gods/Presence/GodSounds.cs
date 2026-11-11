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
    /// A god's sounds and music, only ever the game's, the DLCs' and the player's mods' own (no music is made), plus the
    /// player's own folder (docs/design/gods.md, "Sound and music"): each sound is looked up by name and played as what it
    /// is (a one-shot, heard on the camera when the game's sound is made for it and at the place otherwise; a sustainer for
    /// as long as it is wanted). The tracks a player puts in Sounds/Rimfolk/Gods/&lt;Domain&gt;/ (an .ogg, .wav or .mp3,
    /// read when the game starts) are played at that domain's moments before the look's own.
    /// </summary>
    public static class GodSounds
    {
        /// <summary>The player's own folder for a domain, under the mod's Sounds folder.</summary>
        public static string OwnFolder(string domain) => "Rimfolk/Gods/" + domain;

        public static SoundDef Sound(string name) => name.NullOrEmpty() ? null : DefDatabase<SoundDef>.GetNamedSilentFail(name);

        public static bool SoundExists(string name) => Sound(name) != null;

        public static bool SongExists(string name) => !name.NullOrEmpty() && DefDatabase<SongDef>.GetNamedSilentFail(name) != null;

        /// <summary>
        /// Plays a sound at a place, as what it is. A sustainer is returned for the caller to end (it plays until then);
        /// a one-shot returns null.
        /// </summary>
        public static Sustainer Play(SoundDef def, Map map, IntVec3 cell, float volume = 1f)
        {
            if (def == null || map == null) return null;
            try
            {
                bool camera = def.subSounds.Any(s => s.onCamera);
                if (def.sustain)
                {
                    var si = camera ? SoundInfo.OnCamera(MaintenanceType.None) : SoundInfo.InMap(new TargetInfo(cell, map), MaintenanceType.None);
                    si.volumeFactor = volume;
                    return def.TrySpawnSustainer(si);
                }
                var info = camera ? SoundInfo.OnCamera() : SoundInfo.InMap(new TargetInfo(cell, map));
                info.volumeFactor = volume;
                def.PlayOneShot(info);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a sound (" + def.defName + ")", e);
            }
            return null;
        }

        /// <summary>A shown god's own low sound, from its look's (a sustainer, held each tick by the god while the camera is near).</summary>
        internal static Sustainer Hum(GodLookDef look, Pawn p)
        {
            var def = look?.hum?.Select(Sound).FirstOrDefault(d => d != null && d.sustain);
            if (def == null) return null;
            try
            {
                var info = SoundInfo.InMap(p, MaintenanceType.PerTick);
                info.volumeFactor = GodPresenceMath.HumVolume;
                return def.TrySpawnSustainer(info);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's hum (" + def.defName + ")", e);
                return null;
            }
        }

        /// <summary>The camera is close enough to hear a god.</summary>
        internal static bool Near(Pawn p) =>
            p?.Map != null && p.Map == Find.CurrentMap && Find.CameraDriver.MapPosition.InHorDistOf(p.Position, GodPresenceMath.HumHearCells);

        // ---------------- The player's own tracks ----------------

        private static readonly Dictionary<string, List<SongDef>> own = new Dictionary<string, List<SongDef>>();

        /// <summary>The player's own tracks for a domain (made once into songs the game's music can play; none when the folder is empty or missing).</summary>
        public static List<SongDef> OwnTracks(string domain)
        {
            if (domain.NullOrEmpty()) return new List<SongDef>();
            if (own.TryGetValue(domain, out var list)) return list;
            list = new List<SongDef>();
            try
            {
                int i = 0;
                foreach (var clip in ContentFinder<AudioClip>.GetAllInFolder(OwnFolder(domain)))
                {
                    if (clip == null) continue;
                    list.Add(new SongDef { defName = "Rimfolk_OwnGodTrack_" + domain + "_" + i++, clip = clip, volume = 1f, playOnMap = false, commonality = 0f });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the player's own tracks", e);
            }
            own[domain] = list;
            return list;
        }

        /// <summary>The song for a moment: the player's own first, then the look's (GodPresenceMath.Track), or null.</summary>
        public static SongDef TrackFor(GodLookDef look, PresenceKind kind, int seed)
        {
            if (look == null) return null;
            var mine = OwnTracks(look.Domain);
            string name = GodPresenceMath.Track(mine.Select(s => s.defName).ToList(), look.tracks, look.darkTracks, SongExists, kind, seed);
            if (name == null) return null;
            return mine.FirstOrDefault(s => s.defName == name) ?? DefDatabase<SongDef>.GetNamedSilentFail(name);
        }

        /// <summary>The sounds of a moment's sting, its own kind's first (a wrath's, a departure's), two at most, layered.</summary>
        public static List<SoundDef> StingFor(GodLookDef look, PresenceKind kind)
        {
            var wanted = new List<string>();
            if (kind == PresenceKind.Wrath && look.wrathSting != null) wanted.AddRange(look.wrathSting);
            if (kind == PresenceKind.Depart && look.depart != null) wanted.AddRange(look.depart);
            if (kind == PresenceKind.Omen && look.omen != null) wanted.AddRange(look.omen);
            if (kind != PresenceKind.Omen && look.sting != null) wanted.AddRange(look.sting);
            // The game's own sting under all, for a coming with nothing of its own in the player's game.
            if (kind != PresenceKind.Omen) wanted.Add("GameStartSting");
            return GodPresenceMath.Mix(wanted, SoundExists, kind == PresenceKind.Omen ? 1 : 2).Select(Sound).ToList();
        }
    }
}
