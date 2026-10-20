using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's buttons on the faiths page (UI/FaithsPanel.cs): on each faith's card, call an evening of faith for it
    /// (Faith/FaithEvenings.cs) and name it the colony's by decree (Decrees/DecreeFaith.cs). Both say why when they can't.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FaithButtons
    {
        static FaithButtons()
        {
            FaithsPanel.AddButton(new FaithsPanel.Button
            {
                key = "evening",
                perFaith = true,
                shows = _ => FaithEvenings.On,
                label = _ => "CC_FaithBtn_Evening".Translate(),
                tip = f => "CC_FaithBtn_Evening_Tip".Translate(f?.name ?? ""),
                blocked = f =>
                {
                    var map = Home();
                    if (map == null) return "CC_FaithBtn_NoHome".Translate();
                    return FaithEvenings.CanCall(map, f, out string why) ? null : why;
                },
                act = f =>
                {
                    var map = Home();
                    if (map != null && f != null) FaithEvenings.Call(map, f);
                }
            });
            FaithsPanel.AddButton(new FaithsPanel.Button
            {
                key = "decree",
                perFaith = true,
                shows = _ => DecreeFaith.Def != null && !DecreeFaith.Hidden,
                label = f => DecreeFaith.FaithOfColony == f && f != null ? "CC_FaithBtn_DecreeHeld".Translate() : "CC_FaithBtn_Decree".Translate(),
                tip = f => "CC_FaithBtn_Decree_Tip".Translate(f?.name ?? ""),
                blocked = f =>
                {
                    var comp = CouncilGameComponent.Instance;
                    var def = DecreeFaith.Def;
                    if (comp?.decrees == null || def == null || f == null) return "CC_FaithBtn_NoHome".Translate();
                    if (DecreeFaith.FaithOfColony == f) return "CC_FaithBtn_DecreeHeld_Tip".Translate(f.name);
                    if (comp.decrees.IsActive(def)) return "CC_FaithBtn_DecreeOther".Translate(DecreeFaith.FaithOfColony?.name ?? "");
                    return comp.decrees.CannotEnact(def);
                },
                act = f =>
                {
                    if (f == null) return;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_FaithBtn_Decree_Confirm".Translate(f.name), () =>
                    {
                        var comp = CouncilGameComponent.Instance;
                        var def = DecreeFaith.Def;
                        if (comp?.decrees == null || def == null || comp.decrees.CannotEnact(def) != null) return;
                        DecreeFaith.Choose(f);
                        comp.decrees.Enact(def, comp);
                    }));
                }
            });
        }

        private static Map Home()
        {
            var map = Find.CurrentMap;
            return map != null && map.IsPlayerHome ? map : Find.AnyPlayerHomeMap;
        }
    }
}
