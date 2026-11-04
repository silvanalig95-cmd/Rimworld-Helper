using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Vanilla Outposts Expanded (vanillaexpanded.outposts): the colony's outposts as its holdings, a view and nothing
    /// more. Its outposts are world objects of the framework's class <c>Outposts.Outpost</c> (a MapParent; VOE's
    /// <c>VOE.Outpost_Mining</c> and the rest derive from it), matched by class name. Read from each: its name (the
    /// game's own Label, which the outpost gives as its <c>Name</c>), its kind (its def, "Outpost_Mining"), the public
    /// field <c>deliveryMap</c> (the colony it delivers to, which it is counted under while that is a home; else the
    /// nearest home, as the outpost itself falls back to) and the property <c>PawnCount</c> (how many are posted there).
    /// </summary>
    public sealed class OutpostsAdapter : WorldAdapter
    {
        public const string PackageId = "vanillaexpanded.outposts";

        private static readonly Dictionary<Type, (FieldInfo delivery, PropertyInfo count)> members = new Dictionary<Type, (FieldInfo, PropertyInfo)>();

        public override string Name => "Vanilla Outposts Expanded";

        public override bool Ready() => People.ModActive(PackageId) && GenTypes.GetTypeInAnyAssembly("Outposts.Outpost") != null;

        private static (FieldInfo delivery, PropertyInfo count) MembersOf(Type t)
        {
            if (members.TryGetValue(t, out var m)) return m;
            var delivery = t.GetField("deliveryMap", BindingFlags.Public | BindingFlags.Instance);
            if (delivery != null && delivery.FieldType != typeof(Map)) delivery = null;
            var count = t.GetProperty("PawnCount", BindingFlags.Public | BindingFlags.Instance);
            if (count != null && count.PropertyType != typeof(int)) count = null;
            return members[t] = (delivery, count);
        }

        public override void Holdings(List<Holding> into)
        {
            foreach (var o in Find.WorldObjects.AllWorldObjects)
            {
                if (o == null || o.Destroyed || o.Faction == null || !o.Faction.IsPlayer || !HomeLife.ClassIs(o.GetType(), "Outposts", "Outpost")) continue;
                var m = MembersOf(o.GetType());
                var delivery = m.delivery?.GetValue(o) as Map;
                string label = o.Label;
                into.Add(new Holding
                {
                    kind = HoldingKind.Outpost, thing = o, id = o.ID, tile = o.Tile, name = label.NullOrEmpty() ? o.def?.label : label, what = OutpostLife.KindOf(o),
                    posted = m.count != null ? (int)m.count.GetValue(o) : 0, namedHome = delivery != null ? Homes.KeyOf(delivery) : HomesMath.None
                });
            }
        }
    }
}
