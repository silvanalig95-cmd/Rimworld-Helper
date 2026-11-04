using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Following, on the screen beside the other panels (docs/design/interface.md, "Following"): each thing the player
    /// follows with its newest change in one line (UI/FollowWords.cs, the same lines as the top of This week), then the
    /// wishes pinned. A click opens the thing's card (the floating card while Rimfolk's windows are shut). Off at first;
    /// the play settings' panel button, the corner's following list and Rimfolk's settings switch it on.
    /// </summary>
    public class HudFollowing : HudLinesPanel
    {
        public const string PartFollowed = "followed", PartWishes = "pinnedwishes";
        public static readonly string[] Parts = { PartFollowed, PartWishes };

        public HudFollowing() : base(HudLayout.Following)
        {
        }

        public override string[] PartNames => Parts;

        protected override float FullWidth => 260f;

        protected override string Empty => HudKit.T("CC_Pnl_FollowNone");

        protected override void Gather()
        {
            foreach (var l in FollowWords.Lines())
            {
                var thing = l.thing;
                string name = LinkTextMath.Plain(l.name);
                string text = l.plainNews.NullOrEmpty() ? name : "CC_Pnl_FollowLine".Translate(name.Named("NAME"), l.plainNews.Named("NEWS")).Resolve();
                var line = Add(PartFollowed, l.icon ?? (l.faction != null ? Icons.People : Icons.Pin), text, null, 0f,
                    Tip(name + (l.plainNews.NullOrEmpty() ? "" : "\n" + l.plainNews), HudKit.T("CC_Pnl_FollowClick")), () => Cards.Open(thing));
                line.quiet = l.gone ? UiKit.Dim : UiKit.HeaderColor;
            }
            var comp = CouncilGameComponent.Instance;
            var memory = WeekMemory.Instance;
            if (comp == null || memory == null) return;
            foreach (int id in memory.pinnedWishes)
            {
                var w = comp.wishes.FirstOrDefault(x => x.id == id && x.state == WishState.Open);
                if (w?.pawn == null) continue;
                var line = Add(PartWishes, Icons.Wishes, "CC_Week_Wish".Translate(w.pawn.LabelShort, w.Label).Resolve(), null, 0f,
                    Tip("\"" + w.quote + "\"", "CC_Week_ClickPage".Translate(MainTabWindow_Council.TabName("Wishes")).Resolve()), () => MainTabWindow_Council.OpenTab("Wishes"));
                line.quiet = UiKit.Gold;
            }
        }

        protected override void MenuOptions(List<FloatMenuOption> options)
        {
            options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_OpenWeek"), () => MainTabWindow_Council.OpenTab("Week")));
        }
    }
}
