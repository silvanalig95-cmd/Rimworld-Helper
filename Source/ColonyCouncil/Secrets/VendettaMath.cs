using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Who takes up a vendetta its avenger died with (Secrets/VendettaEnds.cs). By the player's decision of 6 November 2026
    /// (the colony is the family, never a dynasty) it passes to whoever was closest to them, a partner, a friend or a
    /// relative, by how close they really were: never to blood kin first. Closeness is what the game and Rimfolk already
    /// know of the two: the game's opinion (which carries its own weight for a relation and everything that passed between
    /// them), the social web's bonds, and a partner or a relative by blood as one part among the others. Pure (no game
    /// types): Tools/Tests checks it (VendettaTests).
    /// </summary>
    public static class VendettaMath
    {
        /// <summary>What one colonist was to the dead, read while their bonds are still on record (at the death).</summary>
        public struct Bond
        {
            /// <summary>Their opinion of the dead, as the game has it.</summary>
            public int opinion;
            /// <summary>The social web's warmth between them (SocialWeb.Warmth): friends' strength, comrades' and a mentorship's, less a rivalry's.</summary>
            public float web;
            /// <summary>Spouse, lover or betrothed.</summary>
            public bool partner;
            /// <summary>A relative by blood.</summary>
            public bool kin;
            /// <summary>Their nature: the kind take up blood less readily, the bloodthirsty more.</summary>
            public bool kind, bloodlust;

            public Bond(int opinion, float web, bool partner = false, bool kin = false, bool kind = false, bool bloodlust = false)
            {
                this.opinion = opinion;
                this.web = web;
                this.partner = partner;
                this.kin = kin;
                this.kind = kind;
                this.bloodlust = bloodlust;
            }
        }

        /// <summary>A partner is this much closer than their opinion and bonds alone say.</summary>
        public const float PartnerWeight = 40f;
        /// <summary>A relative by blood, this much: less than a partner, and less than a close friendship's bond.</summary>
        public const float KinWeight = 20f;
        /// <summary>Close enough to take up someone's vendetta: as close as an opinion of 60 alone.</summary>
        public const float AtLeast = 60f;
        /// <summary>The kind need to be twice as close; the bloodthirsty, two thirds.</summary>
        public const float KindFactor = 0.5f, BloodlustFactor = 1.5f;

        /// <summary>How close they were: opinion, the web's bonds, a partner and a relative by blood, added.</summary>
        public static float Closeness(Bond b) => b.opinion + b.web + (b.partner ? PartnerWeight : 0f) + (b.kin ? KinWeight : 0f);

        /// <summary>How readily they take it up: closeness, by their nature.</summary>
        public static float Willing(Bond b)
        {
            float c = Closeness(b);
            if (c <= 0f) return c;
            return c * (b.kind ? KindFactor : 1f) * (b.bloodlust ? BloodlustFactor : 1f);
        }

        /// <summary>They would take it up: close enough, by their nature.</summary>
        public static bool WouldTakeUp(Bond b) => Willing(b) >= AtLeast;

        /// <summary>
        /// Who takes it up: the closest of those who would (an index into <paramref name="bonds"/>; where two are as close,
        /// the first), or -1 for nobody. Who may at all (grown colonists, never one already sworn on the same people) is
        /// the caller's to say.
        /// </summary>
        public static int Heir(IList<Bond> bonds)
        {
            if (bonds == null) return -1;
            int pick = -1;
            float best = float.MinValue;
            for (int i = 0; i < bonds.Count; i++)
            {
                if (!WouldTakeUp(bonds[i])) continue;
                float c = Closeness(bonds[i]);
                if (c <= best) continue;
                best = c;
                pick = i;
            }
            return pick;
        }

        /// <summary>The told lines' tag for what they were to the dead: partner, kin (a relative by blood), or friend.</summary>
        public static string Tag(Bond b) => b.partner ? "partner" : b.kin ? "kin" : "friend";

        /// <summary>The heir noted at the death (Vendetta.heirId): not yet looked (an older save), nobody close enough, or the colonist's id.</summary>
        public const int NotNoted = -1, Nobody = 0;
    }
}
