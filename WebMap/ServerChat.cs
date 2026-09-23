using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Splatform;
using UnityEngine;

// Authored by Jere Kuusela <https://github.com/JereKuusela>
// https://github.com/JereKuusela/valheim-dev/blob/main/ServerDevcommands/Features/ServerChat.cs

#nullable enable

namespace WebMap;

[HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
public class ServerChat
{
    public static ZNet.PlayerInfo ServerClient => serverClient ??= CreatePlayerInfo();
    private static ZNet.PlayerInfo? serverClient;
    public static UserInfo UserInfo => userInfo ??= new UserInfo { Name = ServerClient.m_userInfo.m_displayName, UserId = ServerClient.m_userInfo.m_id };
    private static UserInfo? userInfo;

    public static void Clear()
    {
        serverClient = null;
        userInfo = null;
    }

    private static ZNet.PlayerInfo CreatePlayerInfo() => new()
    {
        m_name = "Server",
        // Receiving chat messages requires a valid character ID.
        m_characterID = new ZDOID(ZDOMan.GetSessionID(), uint.MaxValue),
        m_userInfo = new() { m_id = GetServerUserId(), m_displayName = "Server", m_serverAssignedDisplayName = "Server" },
        m_publicPosition = false,
        m_position = Vector3.zero,
    };
    public static void RefreshPlayerInfo()
    {
        serverClient = null;
        userInfo = null;
    }
    private static PlatformUserID GetServerUserId()
    {
        try
        {
            // Steamworks is not available for Microsoft clients.
            var steamGameServer = Type.GetType("Steamworks.SteamGameServer, com.rlabrecque.steamworks.net", false);
            var getSteamId = steamGameServer?.GetMethod("GetSteamID");
            var steamId = getSteamId?.Invoke(null, null)?.ToString();
            if (!string.IsNullOrEmpty(steamId))
                return new PlatformUserID(ZNet.instance.m_steamPlatform, steamId);
        }
        catch
        {
        }

        // PlayFab may be selected while respawning locally, before matchmaking has server data.
        var playFabId = ZPlayFabMatchmaking.m_instance?.m_serverData?.remotePlayerId;
        if (ZNet.m_onlineBackend == OnlineBackendType.PlayFab && !string.IsNullOrEmpty(playFabId))
            return new PlatformUserID("playfab", playFabId);
        else if (ZNet.instance.m_hostSocket == null)
            return new PlatformUserID(ZNet.instance.m_steamPlatform, "Server");
        else
            return new PlatformUserID(ZNet.instance.m_steamPlatform, ZNet.instance.m_hostSocket.GetHostName());
    }
    public static void Write(ZPackage pkg)
    {
        pkg.Write(ServerClient.m_name);
        pkg.Write(ServerClient.m_characterID);
        ServerClient.m_userInfo.Write(pkg);
        // Server position is never public.
        pkg.Write(false);
    }
    static void Postfix(Talker.Type type, string text)
    {
        if (Player.m_localPlayer) return;
        if (ZRoutedRpc.instance == null) return;

        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ChatMessage", new object[] {
            Vector3.zero,
            (int)type,
            UserInfo,
            text
        });
    }
}

// Server client is only sent to clients, so this is needed for the server to recognize it.
[HarmonyPatch(typeof(ZNet), nameof(ZNet.TryGetPlayerByPlatformUserID))]
public class RecognizeServerClient
{
    static bool Postfix(bool result, PlatformUserID platformUserID, ref ZNet.PlayerInfo playerInfo)
    {
        if (result) return result;
        if (platformUserID != ServerChat.ServerClient.m_userInfo.m_id) return result;

        playerInfo = ServerChat.ServerClient;
        return true;
    }
}

// Valheim 1.0 moved the packet writing (and the Write(count) anchor) from SendPlayerList into WritePlayerInfo.
[HarmonyPatch(typeof(ZNet), nameof(ZNet.WritePlayerInfo))]
public class AddExtraPlayer
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ZPackage), nameof(ZPackage.Write), new[] { typeof(int) })))
            .Advance(1)
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
            pkg.Write(net.GetNrOfPlayers() + 1);
            ServerChat.Write(pkg);
        }
    }
    static bool IsExtraPlayerAdded(ZNet net, int count) => count >= net.GetNrOfPlayers() + 1;
}
