using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A keyed text told often, in more than one wording: the key and its numbered fellows (CC_X, CC_X_2 ... CC_X_6, as
    /// CampStory reads its lines), one picked at random among those that exist. A key with no CC_X_2 is itself.
    /// </summary>
    public static class TextVariants
    {
        public static string Vary(string key)
        {
            if (key == null || !(key + "_2").CanTranslate()) return key;
            var keys = new List<string> { key };
            for (int i = 2; i <= 6 && (key + "_" + i).CanTranslate(); i++) keys.Add(key + "_" + i);
            return keys.RandomElement();
        }
    }
}
