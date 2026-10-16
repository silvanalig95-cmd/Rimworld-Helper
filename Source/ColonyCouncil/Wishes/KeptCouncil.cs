using System;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Promises kept (Wishes/Kept.cs): where the watch meets the council's own wish rules.</summary>
    public partial class CouncilGameComponent
    {
        /// <summary>Granted lasting wishes being watched, and what came of them.</summary>
        public KeptTracker kept = new KeptTracker();

        /// <summary>
        /// What they were given was taken back: they may bring the wish up again in a couple of days, whatever was left of
        /// its usual pause after being granted.
        /// </summary>
        internal void AllowRenewal(Pawn p, WishDef def)
        {
            if (p == null || def == null) return;
            cooldowns[CooldownKey(p, def)] = Find.TickManager.TicksGame + (int)(KeptMath.RenewDays * KeptMath.TicksPerDay);
        }

        /// <summary>A wish asking again for what was taken back opens with a line about it.</summary>
        private void NoteRenewed(Wish w)
        {
            try
            {
                string line = kept.Notify_Voiced(w);
                if (!line.NullOrEmpty()) w.quote = w.Format(line) + " " + w.quote;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("renewed wish", e);
            }
        }
    }
}
