using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules for a colonist who casts A RimWorld of Magic's damage spells on their own (Integrations/MagicAutoCast.cs;
    /// docs/magic-autocast.md), without game types (Tools/Tests try them): which of the mod's spells and techniques are
    /// used and how each one lands (fire, how far from the target its hits can fall, over the heads or along the line),
    /// the share of mana or stamina always kept back, the choice among those ready, the hunt's rules (no fire, Shadow
    /// Strike only on prey that won't fight back) and one opening cast per prey.
    /// </summary>
    public static class MagicAutoCastMath
    {
        /// <summary>What a spell is paid with: the magic component's mana or the might component's stamina.</summary>
        public enum Pays
        {
            Mana,
            Stamina
        }

        /// <summary>One of the mod's damage abilities, as Rimfolk uses it.</summary>
        public sealed class Spell
        {
            /// <summary>The mod's defName (its first level; the _I, _II and _III levels count as the same spell).</summary>
            public string name;
            public Pays pays;
            /// <summary>It sets things alight: never on a hunt.</summary>
            public bool fire;
            /// <summary>How far from the target, in cells, its hits can land (a scatter, shards, small blasts): nobody it shouldn't hit may stand that close.</summary>
            public int reach;
            /// <summary>It goes over the heads (or the caster steps there): nobody in the line between can be hit.</summary>
            public bool overhead;
            /// <summary>How hard it hits, to choose among those ready (the heaviest first).</summary>
            public int weight;
        }

        /// <summary>The mana or stamina always kept back: a cast only when at least this share of the pool is left after it.</summary>
        public const float Reserve = 0.3f;
        /// <summary>A caster's own casts in a fight are at least this far apart (two short passes), besides the spell's own cooldown.</summary>
        public const int FightGapTicks = 500;
        /// <summary>A cast asked for that hasn't begun or finished after this long (half an hour) is let go.</summary>
        public const int CastWaitTicks = 1250;
        /// <summary>After Shadow Strike: the mod brings the caster back where they cast from 300 ticks after the strike; Rimfolk looks again after this.</summary>
        public const int StrikeReturnTicks = 320;
        /// <summary>A prey opened on is remembered this long (a day).</summary>
        public const int OpenedKeepTicks = 60000;
        /// <summary>The colonists switched on, at most (one id each).</summary>
        public const int MaxOn = 60;
        /// <summary>The game's chance that a hurt animal turns on its hunter, below which the hunt designator gives no warning: safe prey for Shadow Strike is under it, from up close.</summary>
        public const float SafeManhunterChance = 0.015f;
        /// <summary>Safe prey for Shadow Strike is at most this many times the hunter's body size.</summary>
        public const float SafePreySize = 1.5f;
        /// <summary>Shadow Strike in a fight only on an enemy with no other enemy this close to it (the caster lands beside it).</summary>
        public const float StrikeAloneRadius = 6f;
        /// <summary>On a hunt Shadow Strike comes first on safe prey (the player's wish).</summary>
        public const int StrikeHuntWeight = 100;

        public const string ShadowStrike = "TM_ShadowStrike", Spite = "TM_Spite";

        /// <summary>
        /// Spite for a death knight of A RimWorld of Magic (Continued)'s class progression (its discipline rwom.deathknight):
        /// its own verb (ClassProgression.Verb_DeathKnightSpite) spends this much of their hate (the hediff TM_HateHD, or one
        /// of its levels) and does nothing without it; a death knight by the classic trait casts it as before.
        /// </summary>
        public const string KnightDiscipline = "rwom.deathknight", HateHediff = "TM_HateHD";

        public const float SpiteHate = 20f;

        /// <summary>Spite can land: not a death knight of the class progression, or one with the hate it spends.</summary>
        public static bool SpiteReady(bool knightPath, float hate) => !knightPath || !float.IsNaN(hate) && hate >= SpiteHate;

        private static Spell S(string name, Pays pays, int reach, int weight, bool fire = false, bool overhead = false) =>
            new Spell { name = name, pays = pays, reach = reach, weight = weight, fire = fire, overhead = overhead };

        /// <summary>
        /// The damage spells used, each read in the mod's defs and code (A RimWorld of Magic (Continued) 3.0): one target
        /// each, with what lands near it. Arcane Bolt's bolts wander up to about two cells and burst a cell wide; Icebolt's
        /// shards fall up to three cells from where it lands; Lightning Bolt's sparks up to two cells and their neighbours;
        /// Magic Missile's missiles scatter up to four and a half cells; Firebolt sets its target alight.
        /// </summary>
        public static readonly IReadOnlyList<Spell> Spells = new List<Spell>
        {
            S("TM_Headshot", Pays.Stamina, 1, 30),
            S("TM_LightningBolt", Pays.Mana, 3, 24),
            S("TM_Icebolt", Pays.Mana, 5, 22),
            S("TM_Firebolt", Pays.Mana, 2, 20, fire: true),
            S("TM_FrostRay", Pays.Mana, 1, 18),
            S(ShadowStrike, Pays.Stamina, 1, 16, overhead: true),
            S(Spite, Pays.Stamina, 1, 14),
            S("TM_ThrowingKnife", Pays.Stamina, 1, 12),
            S("TM_ArcaneBolt", Pays.Mana, 4, 11),
            S("TM_MagicMissile", Pays.Mana, 5, 10),
            S("TM_Poison", Pays.Mana, 1, 8, overhead: true)
        };

        /// <summary>
        /// Damage spells looked at and left out, with why (docs/magic-autocast.md): Shadow Bolt's orb hurts everyone not of
        /// the caster's faction along its way and cleaves three cones past its target, friends included.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Dropped = new Dictionary<string, string>
        {
            ["TM_ShadowBolt"] = "its orb hurts anyone not of the caster's faction along its way, then cleaves everyone in three cones past the target"
        };

        /// <summary>
        /// The mod's spells that hit an area (its own list of splash damage, the masters' storms, blasts and rains, and the
        /// like): never cast on their own (Tools/Tests check none is in <see cref="Spells"/>).
        /// </summary>
        public static readonly IReadOnlyList<string> AreaSpells = new List<string>
        {
            "TM_LightningStorm", "TM_Sunfire", "TM_LightBurst", "TM_SmokeCloud", "TM_SpiritStorm", "TM_PsionicStorm", "TM_PsionicBlast", "TM_ArrowStorm",
            "TM_WaveOfFear", "TM_Fireball", "TM_Firestorm", "TM_Fireclaw", "TM_Blizzard", "TM_Snowball", "TM_EyeOfTheStorm", "TM_LightningCloud",
            "TM_Meteor", "TM_OrbitalStrike", "TM_Explosion", "TM_BladeSpin", "TM_SeismicSlash", "TM_Whirlwind", "TM_Buckshot", "TM_SuppressingFire",
            "TM_DeathBolt", "TM_ShadowBolt"
        };

        private static readonly Dictionary<string, Spell> byName = Spells.ToDictionary(s => s.name);

        /// <summary>The spell an ability of the mod is (any of its levels), or null when Rimfolk doesn't use it.</summary>
        public static Spell Of(string defName)
        {
            string b = PeoplesMagicMath.BaseSpell(defName);
            return b != null && byName.TryGetValue(b, out var s) ? s : null;
        }

        /// <summary>At least the reserve's share of the pool is left after paying the cost.</summary>
        public static bool KeepsReserve(float cur, float max, float cost)
        {
            if (float.IsNaN(cur) || float.IsNaN(max) || float.IsNaN(cost) || float.IsInfinity(cost) || max <= 0f || cost < 0f) return false;
            return cur - cost >= Reserve * max - 0.0001f;
        }

        /// <summary>
        /// Prey Shadow Strike may be used on: the game's chance that it turns on the hunter from up close is under the hunt
        /// designator's warning, it isn't a predator, a person, or more than half again the hunter's size.
        /// </summary>
        public static bool SafePrey(float manhunterChance, bool predator, bool humanlike, float preySize, float hunterSize)
        {
            if (float.IsNaN(manhunterChance) || float.IsNaN(preySize) || float.IsNaN(hunterSize) || hunterSize <= 0f) return false;
            return manhunterChance < SafeManhunterChance && !predator && !humanlike && preySize <= SafePreySize * hunterSize;
        }

        /// <summary>A spell may open a hunt: no fire, and Shadow Strike only on safe prey.</summary>
        public static bool MayOnHunt(Spell s, bool safePrey) => s != null && !s.fire && (s.name != ShadowStrike || safePrey);

        /// <summary>A spell may be cast in a fight: Shadow Strike only on an enemy with no other enemy close to it.</summary>
        public static bool MayInFight(Spell s, bool targetAlone) => s != null && (s.name != ShadowStrike || targetAlone);

        /// <summary>How the choice weighs a spell: by how hard it hits, Shadow Strike first on a hunt.</summary>
        public static int Weight(Spell s, bool hunt) => s == null ? 0 : hunt && s.name == ShadowStrike ? StrikeHuntWeight : s.weight;

        /// <summary>One of the caster's abilities as it stands now, for the choice.</summary>
        public struct Option
        {
            /// <summary>The ability's defName (a level's own name is fine).</summary>
            public string name;
            /// <summary>The mod says it can be cast now (cooldown, cost, weapon).</summary>
            public bool ready;
            /// <summary>The reserve is kept after its cost.</summary>
            public bool reserve;
            /// <summary>The target is in its range and in sight.</summary>
            public bool inReach;
            /// <summary>Nobody it shouldn't hit is near the target or in the line.</summary>
            public bool clear;
        }

        /// <summary>
        /// The ability to cast, or null: of those ready, in reach, clear and keeping the reserve, the one the rules allow
        /// (a hunt's or a fight's) that hits hardest. Ties go to the first given.
        /// </summary>
        public static string Pick(IEnumerable<Option> options, bool hunt, bool safePrey, bool targetAlone)
        {
            string best = null;
            int bestWeight = int.MinValue;
            if (options == null) return null;
            foreach (var o in options)
            {
                var s = Of(o.name);
                if (s == null || !o.ready || !o.reserve || !o.inReach || !o.clear) continue;
                if (hunt ? !MayOnHunt(s, safePrey) : !MayInFight(s, targetAlone)) continue;
                int w = Weight(s, hunt);
                if (w > bestWeight)
                {
                    best = o.name;
                    bestWeight = w;
                }
            }
            return best;
        }

        /// <summary>A caster may cast again in a fight: never cast, or the gap since the last is over (or the clock went back).</summary>
        public static bool GapOver(int last, int now) => last < 0 || now < last || now - last >= FightGapTicks;

        /// <summary>This prey has had its opening cast (and it is still remembered).</summary>
        public static bool Opened(IDictionary<int, int> opened, int prey, int now) =>
            opened != null && opened.TryGetValue(prey, out int at) && at <= now && now - at < OpenedKeepTicks;

        /// <summary>Forgets the prey opened on more than a day ago (or in a future that a load took back).</summary>
        public static int Prune(IDictionary<int, int> opened, int now)
        {
            if (opened == null) return 0;
            var old = opened.Where(kv => kv.Value > now || now - kv.Value >= OpenedKeepTicks).Select(kv => kv.Key).ToList();
            foreach (int k in old) opened.Remove(k);
            return old.Count;
        }

        /// <summary>
        /// After Shadow Strike: the mod drafts a colonist whose target is still standing and sends them at it. One who wasn't
        /// drafted before the cast is let go of that draft once they are back, while they are still at the mod's own
        /// orders (fighting that target, or standing by).
        /// </summary>
        public static bool LetGoOfDraft(bool strike, bool wasDrafted, bool draftedNow, bool atTheModsOrders) =>
            strike && !wasDrafted && draftedNow && atTheModsOrders;

        /// <summary>
        /// After Shadow Strike, a colonist who was drafted before goes back to what they were doing: the mod's melee order
        /// on that target is ended, unless they were fighting hand to hand before.
        /// </summary>
        public static bool EndTheModsMelee(bool strike, bool wasDrafted, bool draftedNow, bool meleeOnTargetNow, bool meleeBefore) =>
            strike && wasDrafted && draftedNow && meleeOnTargetNow && !meleeBefore;

        /// <summary>The ids switched on kept within <see cref="MaxOn"/>, the oldest let go first.</summary>
        public static int Trim(List<int> on)
        {
            if (on == null || on.Count <= MaxOn) return 0;
            int n = on.Count - MaxOn;
            on.RemoveRange(0, n);
            return n;
        }
    }
}
