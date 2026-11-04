using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What a bottom bar button made in XML opens: a section of the council window or a window by name, or one page.</summary>
    public class PageButtonExtension : DefModExtension
    {
        public string section;
        public string page;
    }

    /// <summary>
    /// A bottom bar button made in XML alone, with no window of its own, that opens one of Rimfolk's windows: a section
    /// of the council window on its last page ("People"), a window as its button would ("Planning"), or one page by
    /// name ("Calendar", "Map"), wherever it lives now (UI/Pages.cs). Clicked again while that is showing, it closes
    /// the window. Rimfolk's own four buttons have windows of their own; this is for a patch that wants another.
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
            if (!ext.section.NullOrEmpty()) Pages.OpenOrCloseSection(ext.section);
            else if (!ext.page.NullOrEmpty()) Pages.OpenOrClose(ext.page);
        }
    }
}
