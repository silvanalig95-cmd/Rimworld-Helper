using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A god's moment (docs/design/gods.md, "Presence", the moment; brainstorm section F): never just another event.
    /// <list type="bullet">
    /// <item>An omen is small: a soft sound and a glimmer where it is, those close by look up and one says so, a line in
    /// the news; nothing else.</item>
    /// <item>Every other kind (a coming, an act, a reveal, a departure, a blessing, a wrath): the game drops to normal
    /// speed and the camera goes there; the music fades and a beat of silence follows; then the sting (the look's sounds,
    /// layered, the game's start sting under them where it has nothing of its own), the sky flashes in the god's colour,
    /// its effecters and circle go off, a disguised god grows into itself, the presence takes the sky and stills the wind,
    /// everyone on the map turns toward it and the devout pray, and the mark is left on the ground; a moment later the
    /// god's track (the player's own from the god's folder first, else the look's); then its card, dark paper and gold
    /// with the god's symbol, plain or "some say".</item>
    /// </list>
    /// The music's part (the fade, the silence, the track and a god's hum) has its own setting, "Gods' music"; the sounds
    /// always play. The moment runs on real seconds, as the music does, from one small update a frame while it lasts.
    /// The core's seam (Gods/GodPresenceSeam.cs) calls <see cref="Manifest"/>.
    /// </summary>
    public static class GodPresence
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting "Gods' music" (on by default): the music falls silent for a god and its track plays; a god near the camera hums.</summary>
        public static bool MusicOn => S?.godMusic ?? true;

        private sealed class Moment
        {
            public GodLookDef look;
            public string name, epithet, title, text;
            public Map map;
            public IntVec3 at;
            public PresenceKind kind;
            public Pawn avatar;
            public bool veiled, music;
            public Color a, b;
            public float start;
            public SongDef track;
            public bool stung, played, carded;
            public readonly List<(Sustainer s, float until)> held = new List<(Sustainer, float)>();
        }

        private static readonly List<Moment> moments = new List<Moment>();
        private static Game game;

        /// <summary>
        /// A god's moment of <paramref name="kind"/> at <paramref name="at"/> (or where <paramref name="avatar"/> stands):
        /// <paramref name="godName"/> as it is known ("the Storm-father") and <paramref name="epithet"/> under it on its card
        /// (may be null), <paramref name="title"/> and <paramref name="text"/> for the card (an omen's text is its line in
        /// the news), <paramref name="veiled"/> for "some say". <paramref name="colorA"/> and <paramref name="colorB"/>: the
        /// god's own colours (the look's otherwise). True when it began.
        /// </summary>
        public static bool Manifest(GodLookDef look, string godName, string epithet, Map map, IntVec3 at, PresenceKind kind, Pawn avatar, string title, string text,
            bool veiled, Color? colorA = null, Color? colorB = null)
        {
            if (look == null || map == null) return false;
            if (avatar != null && avatar.Spawned && avatar.Map == map) at = avatar.Position;
            if (!at.IsValid || !at.InBounds(map)) return false;
            Fresh();
            var c = GodAvatar.Of(avatar);
            var m = new Moment
            {
                look = look,
                name = godName.NullOrEmpty() ? c?.Name ?? look.sampleName ?? look.label : godName,
                epithet = epithet,
                title = title,
                text = text,
                map = map,
                at = at,
                kind = kind,
                avatar = avatar,
                veiled = veiled,
                music = MusicOn,
                a = colorA ?? c?.ColorA ?? look.colorA,
                b = colorB ?? c?.ColorB ?? look.colorB,
                start = Time.realtimeSinceStartup
            };
            try
            {
                if (kind == PresenceKind.Omen)
                {
                    Omen(m);
                    return true;
                }
                Begin(m);
                moments.Add(m);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's moment", e);
                return false;
            }
        }

        private static void Fresh()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            moments.Clear();
        }

        // ---------------- An omen ----------------

        private static void Omen(Moment m)
        {
            foreach (var s in GodSounds.StingFor(m.look, PresenceKind.Omen))
                GodSounds.Play(s, m.map, m.at, 0.8f)?.End();
            GodFx.Flash(m.look, m.map, m.at, m.a, m.b, false);
            GodReactions.Moment(m.map, m.at, m.avatar, m.veiled ? null : m.name, m.look.Domain, PresenceKind.Omen);
            string line = m.text ?? m.title;
            if (!line.NullOrEmpty()) Tell.Message(NewsKind.Land, Importance.Notable, m.map, line, new LookTargets(m.at, m.map), MessageTypeDefOf.NeutralEvent);
        }

        // ---------------- A coming: the beginning ----------------

        private static void Begin(Moment m)
        {
            // Normal speed, and the camera there.
            var tm = Find.TickManager;
            if (GodPresenceMath.SlowsTime(m.kind, (int)tm.CurTimeSpeed)) tm.CurTimeSpeed = TimeSpeed.Normal;
            tm.slower.SignalForceNormalSpeed();
            CameraJumper.TryJump(new GlobalTargetInfo(m.at, m.map));
            // The music fades into a beat of silence.
            m.track = m.music ? GodSounds.TrackFor(m.look, m.kind, (m.name ?? "").GetHashCode() ^ GenTicks.TicksAbs / 2500) : null;
            if (m.music)
            {
                try
                {
                    Find.MusicManagerPlay.ForceFadeoutAndSilenceFor(GodPresenceMath.SilenceFor(m.track != null), GodPresenceMath.FadeSeconds, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: the music's fade", e);
                }
            }
        }

        // ---------------- Each frame while a moment lasts ----------------

        internal static void Update()
        {
            if (moments.Count == 0) return;
            Fresh();
            float now = Time.realtimeSinceStartup;
            for (int i = moments.Count - 1; i >= 0; i--)
            {
                var m = moments[i];
                try
                {
                    if (m.map == null || m.map.Disposed || Find.Maps.IndexOf(m.map) < 0)
                    {
                        moments.RemoveAt(i);
                        continue;
                    }
                    float t = now - m.start;
                    if (!m.stung && t >= GodPresenceMath.StingAt(m.kind, m.music)) Sting(m, now);
                    float trackAt = GodPresenceMath.TrackAt(m.kind, m.music);
                    if (!m.played && m.stung && (trackAt < 0f || t >= trackAt))
                    {
                        m.played = true;
                        if (m.track != null && trackAt >= 0f) Play(m.track);
                    }
                    if (!m.carded && m.stung && t >= GodPresenceMath.CardAt(m.kind, m.music))
                    {
                        m.carded = true;
                        GodCard.Send(m.look, m.name, m.epithet, m.map, m.at, m.kind, m.avatar, m.title, m.text, m.veiled, m.a, m.b);
                    }
                    for (int k = m.held.Count - 1; k >= 0; k--)
                        if (now >= m.held[k].until)
                        {
                            if (m.held[k].s != null && !m.held[k].s.Ended) m.held[k].s.End();
                            m.held.RemoveAt(k);
                        }
                    if (m.stung && m.played && m.carded && m.held.Count == 0) moments.RemoveAt(i);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a moment going on", e);
                    moments.RemoveAt(i);
                }
            }
        }

        private static void Play(SongDef song)
        {
            try
            {
                Find.MusicManagerPlay.ForcePlaySong(song, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's track", e);
            }
        }

        // ---------------- The sting ----------------

        private static void Sting(Moment m, float now)
        {
            m.stung = true;
            var map = m.map;
            // Its sounds, layered; a sustained one held a few seconds.
            foreach (var s in GodSounds.StingFor(m.look, m.kind))
            {
                var held = GodSounds.Play(s, map, m.at);
                if (held != null) m.held.Add((held, now + 7f));
            }
            // A disguised god grows into itself in front of everyone.
            var c = GodAvatar.Of(m.avatar);
            if (c != null && m.kind == PresenceKind.Reveal && c.disguised) c.Show(true);
            if (m.avatar != null && m.avatar.Spawned) m.at = m.avatar.Position;
            GodFx.Flash(m.look, map, m.at, m.a, m.b, true);
            GameCondition_GodPresence.Begin(map, m.kind, m.a, m.look.still, m.name, m.veiled);
            GodReactions.Moment(map, m.at, m.avatar, m.veiled ? null : m.name, m.look.Domain, m.kind);
            GodTrails.Mark(GodPresenceMath.MarkFor(m.kind, m.look.mark, m.look.wrathMark), map, m.at, m.a);
        }

        /// <summary>Every moment ends now (a dev action, a game left).</summary>
        internal static void StopAll()
        {
            foreach (var m in moments)
                foreach (var h in m.held)
                    if (h.s != null && !h.s.Ended)
                        h.s.End();
            moments.Clear();
        }

        /// <summary>A moment is going on (one at a time is plenty; the dev actions wait for it).</summary>
        public static bool Busy
        {
            get
            {
                Fresh();
                return moments.Count > 0;
            }
        }
    }

    /// <summary>Runs the gods' moments on real seconds, a frame at a time (nothing saved: a moment lasts a few seconds).</summary>
    public class GodPresenceComponent : GameComponent
    {
        public GodPresenceComponent(Game game)
        {
        }

        public override void GameComponentUpdate()
        {
            GodPresence.Update();
        }
    }
}
