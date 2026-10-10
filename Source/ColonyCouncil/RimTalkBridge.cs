using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Optional integration with RimTalk (AI dialogue, packageId cj.rimtalk).
    ///
    /// Everything goes through reflection against RimTalk's public API, so this mod has no hard
    /// dependency: without RimTalk nothing here runs, and if RimTalk changes its API the integration
    /// switches itself off with one warning instead of breaking the game.
    ///
    /// What it does:
    ///  * Context: RimTalk's prompt for each colonist gets a "Colony Council" section (their open
    ///    wish, recently granted or ignored wishes, the ambition they proposed). The environment
    ///    section gets the colony's current ambition and top concerns. Colonists then bring these up
    ///    naturally in conversation.
    ///  * Template variables for custom RimTalk presets: {{pawn.council_wish}}, {{council_ambition}},
    ///    {{council_priorities}}.
    ///  * Talk requests at key moments: a wish is voiced or granted, the council meets, an ambition
    ///    is chosen or achieved.
    ///  * Our social interactions ("complained about the barracks") go to the vanilla play log, which
    ///    RimTalk already turns into dialogue by itself.
    /// </summary>
    public static class RimTalkBridge
    {
        public static bool Active { get; private set; }

        private static MethodInfo cacheGet;
        private static MethodInfo addTalkRequest;
        private static object talkTypeEvent;
        private static bool speechAvailable;

        public static void TryInit()
        {
            Active = false;
            var apiType = GenTypes.GetTypeInAnyAssembly("RimTalk.API.RimTalkPromptAPI");
            if (apiType == null) return;
            Active = true;
            string modId = ColonyCouncilMod.PackageId;

            try
            {
                RegisterContext(apiType, modId);
            }
            catch (Exception e)
            {
                Log.Warning("[Colony Council] RimTalk found, but its context API didn't match (" + e.Message + "). Context sharing is off; everything else still works.");
            }

            try
            {
                var cacheType = GenTypes.GetTypeInAnyAssembly("RimTalk.Data.Cache");
                var talkType = GenTypes.GetTypeInAnyAssembly("RimTalk.Source.Data.TalkType");
                cacheGet = cacheType?.GetMethod("Get", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Pawn) }, null);
                var stateType = GenTypes.GetTypeInAnyAssembly("RimTalk.Data.PawnState");
                if (talkType != null && stateType != null)
                {
                    addTalkRequest = stateType.GetMethod("AddTalkRequest", BindingFlags.Public | BindingFlags.Instance, null,
                        new[] { typeof(string), typeof(Pawn), talkType }, null);
                    string name = Enum.GetNames(talkType).Contains("Event") ? "Event" : "Other";
                    talkTypeEvent = Enum.Parse(talkType, name);
                }
                speechAvailable = cacheGet != null && addTalkRequest != null && talkTypeEvent != null;
                if (!speechAvailable)
                    Log.Warning("[Colony Council] RimTalk found, but its talk request API didn't match. Colonists won't be prompted to talk about wishes directly (social-log chats still work).");
            }
            catch (Exception e)
            {
                speechAvailable = false;
                Log.Warning("[Colony Council] RimTalk talk requests unavailable: " + e.Message);
            }

            Log.Message("[Colony Council] RimTalk detected: colonists will talk about their wishes and the colony's ambitions.");
        }

        private static void RegisterContext(Type api, string modId)
        {
            var pawnCats = GenTypes.GetTypeInAnyAssembly("RimTalk.API.ContextCategories+Pawn") ?? api.Assembly.GetType("RimTalk.API.ContextCategories+Pawn");
            var envCats = api.Assembly.GetType("RimTalk.API.ContextCategories+Environment");
            var posType = api.Assembly.GetType("RimTalk.API.ContextHookRegistry+InjectPosition");
            if (pawnCats == null || envCats == null || posType == null) throw new Exception("context category types missing");

            object thoughts = pawnCats.GetField("Thoughts", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            object season = envCats.GetField("Season", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                            ?? envCats.GetField("Time", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            object after = Enum.Parse(posType, "After");

            var injectPawn = api.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "InjectPawnSection" && m.GetParameters().Length == 6);
            var injectEnv = api.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "InjectEnvironmentSection" && m.GetParameters().Length == 6);
            if (injectPawn != null && thoughts != null)
                injectPawn.Invoke(null, new object[] { modId, "colony_council", thoughts, after, (Func<Pawn, string>)PawnContext, 100 });
            if (injectEnv != null && season != null)
                injectEnv.Invoke(null, new object[] { modId, "colony_council_plans", season, after, (Func<Map, string>)EnvironmentContext, 100 });

            var regPawnVar = api.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "RegisterPawnVariable" && m.GetParameters().Length == 5);
            var regEnvVar = api.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "RegisterEnvironmentVariable" && m.GetParameters().Length == 5);
            regPawnVar?.Invoke(null, new object[] { modId, "council_wish", (Func<Pawn, string>)WishVariable, "Colony Council: the colonist's current wish, if any", 100 });
            regEnvVar?.Invoke(null, new object[] { modId, "council_ambition", (Func<Map, string>)AmbitionVariable, "Colony Council: the colony's current ambition", 100 });
            regEnvVar?.Invoke(null, new object[] { modId, "council_priorities", (Func<Map, string>)PrioritiesVariable, "Colony Council: what the colony most needs right now", 100 });
        }

        // ---------------- Context providers (main thread, called by RimTalk) ----------------

        private static bool ContextOn => Active && ColonyCouncilMod.Settings.rimTalkContext;

        public static string PawnContext(Pawn pawn)
        {
            if (!ContextOn || pawn == null || !pawn.IsColonist) return null;
            try
            {
                var comp = CouncilGameComponent.Instance;
                if (comp == null) return null;
                var sb = new StringBuilder();
                var own = comp.OpenWishBy(pawn);
                if (own != null)
                {
                    int askedDays = (Find.TickManager.TicksGame - own.createdTick) / GenDate.TicksPerDay;
                    sb.Append("Current wish (asked the colony leaders ").Append(askedDays <= 0 ? "today" : askedDays + " days ago").Append("): ")
                        .Append(own.Worker.RimTalkTopic(own));
                    if (!own.reason.NullOrEmpty()) sb.Append(". Why: ").Append(own.reason.StripTags());
                    if (own.coSigners.Count > 0) sb.Append(". Others backing it: ").Append(string.Join(", ", own.coSigners.Select(p => p.LabelShort)));
                    sb.AppendLine(".");
                }
                foreach (var w in comp.OpenWishes)
                {
                    if (w == own) continue;
                    if (w.coSigners.Contains(pawn)) sb.AppendLine("Backs " + w.pawn.LabelShort + "'s petition: " + w.Worker.RimTalkTopic(w) + ".");
                    else if (w.ForSomeoneElse && w.Beneficiary == pawn) sb.AppendLine(w.pawn.LabelShort + " asked the leaders on their behalf: " + w.Worker.RimTalkTopic(w) + ".");
                }
                foreach (var e in comp.RecentFor(pawn, 4f).Take(3))
                {
                    if (e.topic.NullOrEmpty() || e.kind == ChronicleKind.WishMade) continue;
                    int days = (Find.TickManager.TicksGame - e.tick) / GenDate.TicksPerDay;
                    sb.AppendLine("Recently (" + (days <= 0 ? "today" : days + "d ago") + "): " + e.topic + ".");
                }
                var a = comp.ambitions?.active;
                if (a != null && a.proposer == pawn)
                    sb.AppendLine("Proposed the colony's current ambition \"" + a.def.label + "\" (" + a.Progress.ToStringPercent() + " done) and cares about it.");
                if (sb.Length == 0) return null;
                return "Colony Council:\n" + sb.ToString().TrimEnd();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimTalk pawn context", e);
                return null;
            }
        }

        public static string EnvironmentContext(Map map)
        {
            if (!ContextOn || map == null || !map.IsPlayerHome) return null;
            try
            {
                var sb = new StringBuilder();
                string amb = AmbitionVariable(map);
                if (!amb.NullOrEmpty()) sb.AppendLine("Colony ambition: " + amb);
                string pri = PrioritiesVariable(map);
                if (!pri.NullOrEmpty()) sb.AppendLine("Colony concerns: " + pri);
                return sb.Length == 0 ? null : sb.ToString().TrimEnd();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("RimTalk environment context", e);
                return null;
            }
        }

        private static string WishVariable(Pawn pawn)
        {
            var w = CouncilGameComponent.Instance?.OpenWishBy(pawn);
            return w == null ? "" : w.Worker.RimTalkTopic(w);
        }

        private static string AmbitionVariable(Map map)
        {
            var a = CouncilGameComponent.Instance?.ambitions?.active;
            if (a == null || a.map != map) return "";
            int left = (a.deadlineTick - Find.TickManager.TicksGame) / GenDate.TicksPerDay;
            return a.def.label + (a.def.rimTalkTopic.NullOrEmpty() ? "" : " (" + a.def.rimTalkTopic + ")") + ", proposed by " + (a.proposer?.LabelShort ?? "the council")
                   + ", " + a.Progress.ToStringPercent() + " done, " + Math.Max(0, left) + " days left";
        }

        private static string PrioritiesVariable(Map map)
        {
            if (!ColonyCouncilMod.Settings.enableAdvisor) return "";
            var signals = CouncilMapComponent.For(map)?.Signals;
            if (signals == null) return "";
            return string.Join("; ", signals.Where(s => s.Level >= AdviceLevel.Medium).Take(3).Select(s => s.title.StripTags()));
        }

        // ---------------- Talk requests ----------------

        private static bool SpeechOn => Active && speechAvailable && ColonyCouncilMod.Settings.rimTalkSpeech;

        public static void Say(Pawn pawn, string prompt, Pawn recipient = null)
        {
            if (!SpeechOn || pawn == null || !pawn.Spawned || prompt.NullOrEmpty()) return;
            try
            {
                var state = cacheGet.Invoke(null, new object[] { pawn });
                if (state == null) return;
                recipient = recipient ?? NearbyListener(pawn);
                addTalkRequest.Invoke(state, new object[] { prompt, recipient, talkTypeEvent });
            }
            catch (Exception e)
            {
                speechAvailable = false;
                CouncilLog.WarnOnce("RimTalk talk request failed, disabling: " + e.Message);
            }
        }

        private static Pawn NearbyListener(Pawn pawn)
        {
            Pawn best = null;
            float bestDist = 12f * 12f;
            foreach (var p in pawn.Map.mapPawns.FreeColonistsSpawned)
            {
                if (p == pawn || p.Downed || !p.Awake()) continue;
                float d = (p.Position - pawn.Position).LengthHorizontalSquared;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        public static void OnWishVoiced(Wish w)
        {
            if (w.ForSomeoneElse)
                Say(w.pawn, "speaks up on behalf of " + w.Beneficiary.LabelShort + ", asking the colony leaders for: " + w.Worker.RimTalkTopic(w) + ". Reason: " + w.reason.StripTags(), w.Beneficiary.Spawned ? w.Beneficiary : null);
            else
                Say(w.pawn, "has a wish for the colony leaders: " + w.Worker.RimTalkTopic(w) + ". Reason: " + w.reason.StripTags() + ". They say it out loud.");
        }

        public static void OnWishGranted(Wish w, string thanks)
        {
            Say(w.Beneficiary, "is delighted: the colony fulfilled their wish (" + w.Worker.RimTalkTopic(w) + ")" + (w.ForSomeoneElse ? ", which " + w.pawn.LabelShort + " had asked for on their behalf" : "") + ".",
                w.ForSomeoneElse && w.pawn.Spawned ? w.pawn : null);
        }

        public static void OnMeeting(List<(Pawn proposer, AmbitionDef def)> pitches)
        {
            foreach (var (p, def) in pitches)
                Say(p, "is pitching an ambition at the colony council meeting: \"" + def.label + "\"" + (def.rimTalkTopic.NullOrEmpty() ? "" : " (" + def.rimTalkTopic + ")") + ". They try to convince the others.");
        }

        public static void OnAmbitionChosen(AmbitionState a)
        {
            if (a?.proposer != null)
                Say(a.proposer, "is proud: the council chose their proposal \"" + a.def.label + "\" as the colony's next ambition.");
        }

        public static void OnAmbitionAchieved(AmbitionState a)
        {
            if (a?.proposer != null)
                Say(a.proposer, "celebrates: the colony achieved the ambition \"" + a.def.label + "\" that they proposed.");
        }
    }
}
