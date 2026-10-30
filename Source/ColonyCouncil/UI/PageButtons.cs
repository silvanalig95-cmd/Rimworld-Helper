using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What a bottom bar button of the council's opens: a page of the council window by name, or a tool ("rearrange").</summary>
    public class PageButtonExtension : DefModExtension
    {
        public string page;
        public string tool;
    }

    /// <summary>
    /// A bottom bar button that opens one page of the council window (Plan, Base, Making), or starts a tool
    /// (rearranging a room). The page's own key does the same; the button is for the mouse. Shown by the setting
    /// "Bottom bar buttons for the Plan, Base and Making pages and Rearrange a room".
    /// </summary>
    public class MainButtonWorker_Page : MainButtonWorker
    {
        private PageButtonExtension Ext => def.GetModExtension<PageButtonExtension>();

        public override bool Visible => def.buttonVisible && base.Visible;

        public override bool Disabled => Find.CurrentMap == null;

        public override void Activate()
        {
            var ext = Ext;
            if (ext == null) return;
            if (ext.tool == "rearrange")
            {
                Designator_RearrangeRoom.Begin();
                return;
            }
            if (!ext.page.NullOrEmpty()) MainTabWindow_Council.OpenOrClose(ext.page);
        }
    }
}
