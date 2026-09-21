using HarmonyLib;
using System.Collections.Concurrent;
using System;

namespace WebMap.Patches
{

    [HarmonyPatch]
    internal class StringExtensionMethods_Patch
    {
        internal static ConcurrentDictionary<int, string> stablehashNames = new ConcurrentDictionary<int, string>();
        internal static ConcurrentDictionary<int, string> stablehashNamesAnim = new ConcurrentDictionary<int, string>();
        internal static ConcurrentDictionary<string, int> stablehashLookup = new ConcurrentDictionary<string, int>();
        internal static ConcurrentDictionary<string, int> stablehashLookupAnim = new ConcurrentDictionary<string, int>();

        [HarmonyPatch(typeof(StringExtensionMethods), nameof(StringExtensionMethods.GetStableHashCode))]
        [HarmonyPrefix]
        public static bool GetStableHashCode(string str, ref int __result)
        {
            if (stablehashLookup.TryGetValue(str, out __result))
            {
                return false;
            }

            /////////////////////////////////////////////////////////////////
            /// COPY PASTA THE ORIGINAL, ReversePatch wasn't working,
            /// cant be bothered to figure out why
            int num = 5381;
            int num2 = num;
            for (int i = 0; i < str.Length && str[i] != 0; i += 2)
            {
                num = ((num << 5) + num) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                {
                    break;
                }
                num2 = ((num2 << 5) + num2) ^ str[i + 1];
            }
            __result = num + num2 * 1566083941;
            /////////////////////////////////////////////////////////////////

            stablehashNames.TryAdd(__result, str);
            stablehashLookup[str] = __result;

            if (WebMapConfig.DEBUG)
            {
                ZLog.Log($"First GetStableHashCode: {str} -> {__result}");
            }

            return false;
        }

        [HarmonyPatch(typeof(ZSyncAnimation), "GetHash")]
        [HarmonyPrefix]
        public static void GetAnimHash(string name, ref int __result, ref bool __runOriginal)
        {
            if (stablehashLookupAnim.TryGetValue(name, out __result))
            {
                __runOriginal = false;
            }
            else
            {
                __runOriginal = true;
            }
        }

        [HarmonyPatch(typeof(ZSyncAnimation), "GetHash")]
        [HarmonyPostfix]
        public static void AddAnimHash(string name, ref int __result, ref bool __runOriginal)
        {
            if (__runOriginal)
            {
                stablehashNamesAnim.TryAdd(__result, name);
                stablehashLookupAnim[name] = __result;

                if (WebMapConfig.DEBUG)
                {
                    ZLog.Log($"First GetAnimHash: {name} -> {__result}");
                }
            }
        }

        public static string GetStableHashName(int code)
        {
            string str;
            if (stablehashNames.TryGetValue(code, out str))
            {
                return str;
            }

            if (stablehashNamesAnim.TryGetValue(code - 438569, out str))
            {
                return str + $" (A)";
            }

            return code.ToString();
        }
    }
}
