using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace WebMap.Patches
{

    [HarmonyPatch]
    internal class ZDO_Patch
    {

        [HarmonyPatch(typeof(ZDO), "Print")]
        [HarmonyPrefix]
        private static bool Print(ref ZDO __instance)
        {
            ZDOID uid = __instance.m_uid;
            ZLog.Log("UID:" + uid.ToString());
            ZLog.Log("Persistent:" + __instance.m_persistent);
            ZLog.Log("Owner:" + __instance.m_owner);
            ZLog.Log("Revision:" + __instance.m_ownerRevision);
            foreach (KeyValuePair<int, float> @float in (Dictionary<int, float>)AccessTools.Field(typeof(ZDO), "m_floats").GetValue(__instance))
            {
                ZLog.Log("F:" + StringExtensionMethods_Patch.GetStableHashName(@float.Key) + " = " + @float.Value);
            }
            foreach (KeyValuePair<int, Vector3> item in (Dictionary<int, Vector3>)AccessTools.Field(typeof(ZDO), "m_vec3").GetValue(__instance))
            {
                ZLog.Log("V:" + StringExtensionMethods_Patch.GetStableHashName(item.Key) + " = " + item.Value.ToString());
            }
            foreach (KeyValuePair<int, Quaternion> quat in (Dictionary<int, Quaternion>)AccessTools.Field(typeof(ZDO), "m_quats").GetValue(__instance))
            {
                ZLog.Log("Q:" + StringExtensionMethods_Patch.GetStableHashName(quat.Key) + " = " + quat.Value.ToString());
            }
            foreach (KeyValuePair<int, int> @int in (Dictionary<int, int>)AccessTools.Field(typeof(ZDO), "m_ints").GetValue(__instance))
            {
                ZLog.Log("I:" + StringExtensionMethods_Patch.GetStableHashName(@int.Key) + " = " + @int.Value);
            }
            foreach (KeyValuePair<int, long> @long in (Dictionary<int, long>)AccessTools.Field(typeof(ZDO), "m_longs").GetValue(__instance))
            {
                ZLog.Log("L:" + StringExtensionMethods_Patch.GetStableHashName(@long.Key) + " = " + @long.Value);
            }
            foreach (KeyValuePair<int, string> @string in (Dictionary<int, string>)AccessTools.Field(typeof(ZDO), "m_strings").GetValue(__instance))
            {
                ZLog.Log("S:" + StringExtensionMethods_Patch.GetStableHashName(@string.Key) + " = " + @string.Value);
            }
            return false;
        }
    }
}