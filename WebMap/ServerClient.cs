using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Splatform;
using Steamworks;
using UnityEngine;

// Authored by Jere Kuusela <https://github.com/JereKuusela>
// https://github.com/JereKuusela/valheim-expand_world_prefabs/blob/main/ExpandWorldPrefabs/service/ServerClient.cs (public domain)

namespace WebMap
{
    public class ServerClient
    {
        public static ZNet.PlayerInfo Client => client ??= CreatePlayerInfo();
        private static ZNet.PlayerInfo? client;

        // Server client is only sent to clients, so this is needed for the server to recognize it.
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.TryGetPlayerByPlatformUserID))]
        public class RecognizeServerClient
        {
            static bool Postfix(bool result, PlatformUserID platformUserID, ref ZNet.PlayerInfo playerInfo)
            {
                if (result) return result;
                if (platformUserID != Client.m_userInfo.m_id) return result;

                playerInfo = Client;
                return true;
            }
        }

        // Disabled pending a 1.0 fix: 1.0 moved this method's body into a new
        // WritePlayerInfo helper, so SendPlayerList itself no longer has the
        // m_players field-access this transpiler's insertion point depends
        // on, and the injected AddServer() call now runs with an unassigned
        // (null) ZPackage local, throwing on every periodic player-list send
        // and corrupting the connect handshake for real players.
        // [HarmonyPatch(typeof(ZNet), nameof(ZNet.SendPlayerList))]
        public class AddExtraPlayer
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return new CodeMatcher(instructions).End().MatchStartBackwards(new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ZNet), nameof(ZNet.m_players))))
                  .Advance(-1)
                  .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                  .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_0))
                  .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AddExtraPlayer), nameof(AddServer))))
                  .InstructionEnumeration();
            }

            static void AddServer(ZNet net, ZPackage pkg)
            {
                // This is needed in case multiple mods are adding extra players.
                var prev = pkg.GetPos();
                pkg.SetPos(0);
                if (IsExtraPlayerAdded(net, pkg.ReadInt()))
                {
                  pkg.SetPos(prev);
                }
                else
                {
                  pkg.SetPos(0);
                  pkg.Write(net.m_players.Count + 1);
                  Write(pkg);
                }
            }

            static bool IsExtraPlayerAdded(ZNet net, int count) => count >= net.m_players.Count + 1;
        }

        private static ZNet.PlayerInfo CreatePlayerInfo() => new()
        {
            m_name = "Server",
            // Receiving chat messages requires a valid character ID.
            m_characterID = new ZDOID(ZDOMan.GetSessionID(), uint.MaxValue),
            // 1.0 moved m_serverAssignedDisplayName from PlayerInfo onto PlayerInfo.m_userInfo (CrossNetworkUserInfo).
            m_userInfo = new() { m_id = new(ZNet.instance.m_steamPlatform, GetId()), m_displayName = "Server", m_serverAssignedDisplayName = "Server", m_playfabId = "" },
            m_publicPosition = false,
            m_position = Vector3.zero,
        };

        private static string GetId()
        {
            try
            {
                return SteamGameServer.GetSteamID().ToString();
            }
            catch (InvalidOperationException)
            {
                return "0";
            }
        }

        public static void Write(ZPackage pkg)
        {
            pkg.Write(Client.m_name);
            pkg.Write(Client.m_characterID);
            pkg.Write(Client.m_userInfo.m_id.ToString());
            pkg.Write(Client.m_userInfo.m_displayName);
            pkg.Write(Client.m_userInfo.m_serverAssignedDisplayName);
            pkg.Write(Client.m_userInfo.m_playfabId);
            // Server position is never public.
            pkg.Write(false);
        }
    }
}
