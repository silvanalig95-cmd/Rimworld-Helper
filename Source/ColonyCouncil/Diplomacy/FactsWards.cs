using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of wards and hostages (docs/design/stage2-diplomacy.md, item 7), read by the ties' provider (FactsLedger.cs).
    public static partial class Facts
    {
        /// <summary>
        /// Wards and hostages between the colony and the scene's people (theirs at the colony before the colony's with them,
        /// where both are): tie:ward (:theirs, :ours) with {WARD} and, for theirs, {GUARDIAN} their teacher; tie:hostage
        /// (:theirs, :ours, :due forfeit or held) with {HOSTAGE}; them:ward and them:hostage when the one spoken to is that ward
        /// or hostage (a lodger is one of the outsiders the colonists talk with); them:fostered when the one spoken to was the
        /// colony's ward once and went home. Only reads.
        /// </summary>
        private static void WardFacts(FactScene s, FactSet f)
        {
            if (s?.people == null || s.people.IsPlayer || DiplomacyBook.Instance == null) return;
            WardStay ward = null, hostage = null;
            foreach (var w in Wards.With(s.people))
            {
                if (w?.pawn == null) continue;
                bool live = w.Theirs ? w.stage == WardStage.Here || w.stage == WardStage.Forfeit : WardsAbroad.There(w) || w.stage == WardStage.Held;
                if (!live) continue;
                if (w.Hostage)
                {
                    if (hostage == null || w.Theirs && !hostage.Theirs) hostage = w;
                }
                else if (ward == null || w.Theirs && !ward.Theirs) ward = w;
            }
            if (ward != null)
            {
                foreach (var tag in WardMath.TagsOf(ward.kind, ward.stage)) f.Tag(tag);
                f.Value(WardMath.WardKey, ward.pawn.LabelShort);
                if (ward.Theirs && ward.guardian != null && !ward.guardian.Dead) f.Value(WardMath.GuardianKey, ward.guardian.LabelShort);
                if (s.listener != null && s.listener == ward.pawn)
                {
                    f.Tag(WardMath.ThemWardTag, 2f);
                    f.Tag(WardMath.MoodTag(Wards.Mood(ward)), 2f);
                }
            }
            if (hostage != null)
            {
                foreach (var tag in WardMath.TagsOf(hostage.kind, hostage.stage)) f.Tag(tag);
                f.Value(WardMath.HostageKey, hostage.pawn.LabelShort);
                if (s.listener != null && s.listener == hostage.pawn) f.Tag(WardMath.ThemHostageTag, 2f);
            }
            if (s.listener != null && Wards.Fostered(s.listener) != null) f.Tag(WardMath.FosteredTag);
        }
    }
}
