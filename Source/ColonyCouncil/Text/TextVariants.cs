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

        /// <summary>
        /// One of a key's fellows (CC_X_2 ... _6) picked by <paramref name="seed"/>, the same each time for the same seed: for
        /// words shown more than once that must read alike (an answer's sheet in the letter and on the Dilemmas page).
        /// </summary>
        public static string VaryBy(string key, int seed)
        {
            if (key == null || !(key + "_2").CanTranslate()) return key;
            int count = 2;
            while (count < 6 && (key + "_" + (count + 1)).CanTranslate()) count++;
            int i = (int)((uint)seed % (uint)count);
            return i == 0 ? key : key + "_" + (i + 1);
        }
    }
}
