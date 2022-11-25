using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using static ZRoutedRpc;

namespace WebMap.Patches
{
    [HarmonyPatch]
    internal class ZRoutedRpc_Patch
    {
        private static string[] ignoreRpc = {"DestroyZDO", "SetEvent", "OnTargeted"};

        [HarmonyPatch(typeof(ZRoutedRpc), "InvokeRoutedRPC", new Type[] { typeof(long), typeof(ZDOID), typeof(string), typeof(object[]) })]
        [HarmonyPrefix]
        private static void InvokeRoutedRPC(ref ZRoutedRpc __instance, ref long targetPeerID, ZDOID targetZDO, string methodName, params object[] parameters)
        {
            if (WebMapConfig.DEBUG)
                if (!Array.Exists(ignoreRpc, x => x == methodName)) {
                   ZLog.Log("RoutedRPC Invoking: " + methodName + " " + methodName.GetStableHashCode());
                }

            if (WebMapConfig.TEST && methodName == "DiscoverLocationRespons") {
                ZLog.Log("TEST: Sending discovered location to everyone: " + methodName + " " + parameters[0] + " " + parameters[1] + " " + parameters[2] + " " + parameters[3]);
                targetPeerID = ZRoutedRpc.Everybody;
            }
        }
    }
}
