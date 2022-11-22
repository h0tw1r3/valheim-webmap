using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using static ZRoutedRpc;
using Random = UnityEngine.Random;

namespace WebMap
{
    //This attribute is required, and lists metadata for your plugin.
    //The GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config). I like to use the java package notation, which is "com.[your name here].[your plugin name here]"
    //The name is the name of the plugin that's displayed on load, and the version number just specifies what version the plugin is.
    [BepInPlugin(GUID, NAME, VERSION)]

    //This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class WebMap : BaseUnityPlugin
    {
        public const string GUID = "com.github.h0tw1r3.valheim.webmap";
        public const string NAME = "WebMap";
        public const string VERSION = "2.0.0";

        private static readonly string[] ALLOWED_PINS = { "dot", "fire", "mine", "house", "cave" };

        private static MapDataServer mapDataServer;
        private static string worldDataPath;

        private static readonly int sayMethodHash = "Say".GetHashCode();
        private static readonly int chatMessageMethodHash = "ChatMessage".GetHashCode();

        private bool fogTextureNeedsSaving;

        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            Harmony harmony = new Harmony(GUID);
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            string pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            WebMapConfig.ReadConfigFile(Config);

            string mapDataPath = Path.Combine(pluginPath ?? string.Empty, "map_data");
            Directory.CreateDirectory(mapDataPath);
            worldDataPath = Path.Combine(mapDataPath, WebMapConfig.GetWorldName());
            Directory.CreateDirectory(worldDataPath);

            mapDataServer = new MapDataServer();

            string mapImagePath = Path.Combine(worldDataPath, "map");
            try
            {
                mapDataServer.mapImageData = File.ReadAllBytes(mapImagePath);
            }
            catch (Exception e)
            {
                Debug.Log("WebMap: Failed to read map image data from disk. " + e.Message);
            }

            string fogImagePath = Path.Combine(worldDataPath, "fog.png");
            try
            {
                Texture2D fogTexture = new Texture2D(WebMapConfig.TEXTURE_SIZE, WebMapConfig.TEXTURE_SIZE);
                byte[] fogBytes = File.ReadAllBytes(fogImagePath);
                fogTexture.LoadImage(fogBytes);
                mapDataServer.fogTexture = fogTexture;
            }
            catch (Exception e)
            {
                Debug.Log("WebMap: Failed to read fog image data from disk... Making new fog image..." + e.Message);
                Texture2D fogTexture = new Texture2D(WebMapConfig.TEXTURE_SIZE, WebMapConfig.TEXTURE_SIZE,
                    TextureFormat.RGB24, false);
                Color32[] fogColors = new Color32[WebMapConfig.TEXTURE_SIZE * WebMapConfig.TEXTURE_SIZE];
                for (int t = 0; t < fogColors.Length; t++) fogColors[t] = Color.black;

                fogTexture.SetPixels32(fogColors);
                byte[] fogPngBytes = fogTexture.EncodeToPNG();

                mapDataServer.fogTexture = fogTexture;
                try
                {
                    File.WriteAllBytes(fogImagePath, fogPngBytes);
                }
                catch
                {
                    Debug.Log("WebMap: FAILED TO WRITE FOG FILE!");
                }
            }

            InvokeRepeating("UpdateFogTexture", WebMapConfig.UPDATE_FOG_TEXTURE_INTERVAL,
                WebMapConfig.UPDATE_FOG_TEXTURE_INTERVAL);
            InvokeRepeating("SaveFogTexture", WebMapConfig.SAVE_FOG_TEXTURE_INTERVAL,
                WebMapConfig.SAVE_FOG_TEXTURE_INTERVAL);

            string mapPinsFile = Path.Combine(worldDataPath, "pins.csv");
            try
            {
                string[] pinsLines = File.ReadAllLines(mapPinsFile);
                mapDataServer.pins = new List<string>(pinsLines);
            }
            catch (Exception e)
            {
                Debug.Log("WebMap: Failed to read pins.csv from disk. " + e.Message);
            }
        }

        public void UpdateFogTexture()
        {
            int pixelExploreRadius = (int)Mathf.Ceil(WebMapConfig.EXPLORE_RADIUS / WebMapConfig.PIXEL_SIZE);
            int pixelExploreRadiusSquared = pixelExploreRadius * pixelExploreRadius;
            int halfTextureSize = WebMapConfig.TEXTURE_SIZE / 2;

            mapDataServer.players.ForEach(player =>
            {
                if (player.m_publicRefPos)
                {
                    ZDO zdoData = null;
                    try
                    {
                        zdoData = ZDOMan.instance.GetZDO(player.m_characterID);
                    }
                    catch { }

                    if (zdoData != null)
                    {
                        Vector3 pos = zdoData.GetPosition();
                        int pixelX = Mathf.RoundToInt(pos.x / WebMapConfig.PIXEL_SIZE + halfTextureSize);
                        int pixelY = Mathf.RoundToInt(pos.z / WebMapConfig.PIXEL_SIZE + halfTextureSize);
                        for (int y = pixelY - pixelExploreRadius; y <= pixelY + pixelExploreRadius; y++)
                        {
                            for (int x = pixelX - pixelExploreRadius; x <= pixelX + pixelExploreRadius; x++)
                                if (y >= 0 && x >= 0 && y < WebMapConfig.TEXTURE_SIZE &&
                                    x < WebMapConfig.TEXTURE_SIZE)
                                {
                                    int xDiff = pixelX - x;
                                    int yDiff = pixelY - y;
                                    int currentExploreRadiusSquared = xDiff * xDiff + yDiff * yDiff;
                                    if (currentExploreRadiusSquared < pixelExploreRadiusSquared)
                                    {
                                        Color fogTexColor = mapDataServer.fogTexture.GetPixel(x, y);
                                        if (fogTexColor != Color.white)
                                        {
                                            fogTextureNeedsSaving = true;
                                            mapDataServer.fogTexture.SetPixel(x, y, Color.white);
                                        }
                                    }
                                }
                        }
                    }
                }
            });
        }

        public void SaveFogTexture()
        {
            if (mapDataServer.players.Count > 0 && fogTextureNeedsSaving)
            {
                byte[] pngBytes = mapDataServer.fogTexture.EncodeToPNG();

                // Debug.Log("Saving fog file...");
                try
                {
                    File.WriteAllBytes(Path.Combine(worldDataPath, "fog.png"), pngBytes);
                    fogTextureNeedsSaving = false;
                }
                catch
                {
                    Debug.Log("WebMap: FAILED TO WRITE FOG FILE!");
                }
            }
        }

        public static void SavePins()
        {
            string mapPinsFile = Path.Combine(worldDataPath, "pins.csv");
            try
            {
                File.WriteAllLines(mapPinsFile, mapDataServer.pins);
            }
            catch
            {
                Debug.Log("WebMap: FAILED TO WRITE PINS FILE!");
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), "Start")]
        private class ZoneSystemPatch
        {
            private static readonly Color DeepWaterColor = new Color(0.36105883f, 0.36105883f, 0.43137255f);
            private static readonly Color ShallowWaterColor = new Color(0.574f, 0.50709206f, 0.47892025f);
            private static readonly Color ShoreColor = new Color(0.1981132f, 0.12241901f, 0.1503943f);

            private static Color GetMaskColor(float wx, float wy, float height, Heightmap.Biome biome)
            {
                Color noForest = new Color(0f, 0f, 0f, 0f);
                Color forest = new Color(1f, 0f, 0f, 0f);

                if (height < ZoneSystem.instance.m_waterLevel) return noForest;

                if (biome == Heightmap.Biome.Meadows)
                {
                    if (!WorldGenerator.InForest(new Vector3(wx, 0f, wy))) return noForest;

                    return forest;
                }

                if (biome == Heightmap.Biome.Plains)
                {
                    if (WorldGenerator.GetForestFactor(new Vector3(wx, 0f, wy)) >= 0.8f) return noForest;

                    return forest;
                }

                if (biome == Heightmap.Biome.BlackForest || biome == Heightmap.Biome.Mistlands) return forest;

                return noForest;
            }

            private static Color GetPixelColor(Heightmap.Biome biome)
            {
                Color m_meadowsColor = new Color(0.573f, 0.655f, 0.361f);
                Color m_swampColor = new Color(0.639f, 0.447f, 0.345f);
                Color m_mountainColor = new Color(1f, 1f, 1f);
                Color m_blackforestColor = new Color(0.420f, 0.455f, 0.247f);
                Color m_heathColor = new Color(0.906f, 0.671f, 0.470f);
                Color m_ashlandsColor = new Color(0.690f, 0.192f, 0.192f);
                Color m_deepnorthColor = new Color(1f, 1f, 1f);
                Color m_mistlandsColor = new Color(0.325f, 0.325f, 0.325f);

                switch (biome)
                {
                    case Heightmap.Biome.Meadows:
                        return m_meadowsColor;
                    case Heightmap.Biome.Swamp:
                        return m_swampColor;
                    case Heightmap.Biome.Mountain:
                        return m_mountainColor;
                    case Heightmap.Biome.BlackForest:
                        return m_blackforestColor;
                    case Heightmap.Biome.Plains:
                        return m_heathColor;
                    case Heightmap.Biome.AshLands:
                        return m_ashlandsColor;
                    case Heightmap.Biome.DeepNorth:
                        return m_deepnorthColor;
                    case Heightmap.Biome.Ocean:
                        return Color.white;
                    case Heightmap.Biome.Mistlands:
                        return m_mistlandsColor;
                    default:
                        return Color.white;
                }
            }

            private static void Postfix(ZoneSystem __instance)
            {
                if (mapDataServer.mapImageData != null)
                {
                    Debug.Log("WebMap: MAP ALREADY BUILT!");
                    return;
                }

                Debug.Log("WebMap: BUILD MAP!");

                int num = WebMapConfig.TEXTURE_SIZE / 2;
                float num2 = WebMapConfig.PIXEL_SIZE / 2f;
                Color32[] colorArray = new Color32[WebMapConfig.TEXTURE_SIZE * WebMapConfig.TEXTURE_SIZE];
                Color32[] treeMaskArray = new Color32[WebMapConfig.TEXTURE_SIZE * WebMapConfig.TEXTURE_SIZE];
                float[] heightArray = new float[WebMapConfig.TEXTURE_SIZE * WebMapConfig.TEXTURE_SIZE];
                for (int i = 0; i < WebMapConfig.TEXTURE_SIZE; i++)
                {
                    for (int j = 0; j < WebMapConfig.TEXTURE_SIZE; j++)
                    {
                        float wx = (float)(j - num) * WebMapConfig.PIXEL_SIZE + num2;
                        float wy = (float)(i - num) * WebMapConfig.PIXEL_SIZE + num2;
                        Heightmap.Biome biome = WorldGenerator.instance.GetBiome(wx, wy);
                        float biomeHeight = WorldGenerator.instance.GetBiomeHeight(biome, wx, wy);
                        colorArray[i * WebMapConfig.TEXTURE_SIZE + j] = GetPixelColor(biome);
                        treeMaskArray[i * WebMapConfig.TEXTURE_SIZE + j] = GetMaskColor(wx, wy, biomeHeight, biome);
                        heightArray[i * WebMapConfig.TEXTURE_SIZE + j] = biomeHeight;
                    }
                }

                float waterLevel = ZoneSystem.instance.m_waterLevel;
                Vector3 sunDir = new Vector3(-0.57735f, 0.57735f, 0.57735f);
                Color[] newColors = new Color[colorArray.Length];

                for (int t = 0; t < colorArray.Length; t++)
                {
                    float h = heightArray[t];

                    int tUp = t - WebMapConfig.TEXTURE_SIZE;
                    if (tUp < 0) tUp = t;

                    int tDown = t + WebMapConfig.TEXTURE_SIZE;
                    if (tDown > colorArray.Length - 1) tDown = t;

                    int tRight = t + 1;
                    if (tRight > colorArray.Length - 1) tRight = t;

                    int tLeft = t - 1;
                    if (tLeft < 0) tLeft = t;

                    float hUp = heightArray[tUp];
                    float hRight = heightArray[tRight];
                    float hLeft = heightArray[tLeft];
                    float hDown = heightArray[tDown];

                    Vector3 va = new Vector3(2f, 0f, hRight - hLeft).normalized;
                    Vector3 vb = new Vector3(0f, 2f, hUp - hDown).normalized;
                    Vector3 normal = Vector3.Cross(va, vb);

                    float surfaceLight = Vector3.Dot(normal, sunDir) * 0.25f + 0.75f;

                    float shoreMask = Mathf.Clamp(h - waterLevel, 0, 1);
                    float shallowRamp = Mathf.Clamp((h - waterLevel + 0.2f * 12.5f) * 0.5f, 0, 1);
                    float deepRamp = Mathf.Clamp((h - waterLevel + 1f * 12.5f) * 0.1f, 0, 1);

                    Color32 mapColor = colorArray[t];
                    Color ans = Color.Lerp(ShoreColor, mapColor, shoreMask);
                    ans = Color.Lerp(ShallowWaterColor, ans, shallowRamp);
                    ans = Color.Lerp(DeepWaterColor, ans, deepRamp);

                    newColors[t] = new Color(ans.r * surfaceLight, ans.g * surfaceLight, ans.b * surfaceLight, ans.a);
                }

                Texture2D newTexture = new Texture2D(WebMapConfig.TEXTURE_SIZE, WebMapConfig.TEXTURE_SIZE,
                    TextureFormat.RGBA32, false);
                newTexture.SetPixels(newColors);
                byte[] pngBytes = newTexture.EncodeToPNG();

                mapDataServer.mapImageData = pngBytes;
                try
                {
                    File.WriteAllBytes(Path.Combine(worldDataPath, "map"), pngBytes);
                }
                catch
                {
                    Debug.Log("WebMap: FAILED TO WRITE MAP FILE!");
                }

                Debug.Log("WebMap: BUILDING MAP DONE!");
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), "Load")]
        private class ZoneSystemLoadPatch
        {
            private static void Postfix()
            {
                ZoneSystem.LocationInstance startLocation;
                if (ZoneSystem.instance.FindClosestLocation("StartTemple", Vector3.zero, out startLocation))
                {
                    var p = startLocation.m_position;
                    WebMapConfig.WORLD_START_POS = p;
                    Debug.Log("WebMap: starting point " + WebMapConfig.WORLD_START_POS.ToString());
                }
                else
                {
                    Debug.Log("WebMap: failed to find starting point");
                }

                mapDataServer.ListenAsync();
            }
        }

        [HarmonyPatch(typeof(ZNet), "Start")]
        private class ZNetPatch
        {
            private static void Postfix(List<ZNetPeer> ___m_peers)
            {
                mapDataServer.players = ___m_peers;
            }
        }

        [HarmonyPatch(typeof(ZRoutedRpc), "HandleRoutedRPC")]
        private class ZRoutedRpcPatch
        {
            private static void Prefix(RoutedRPCData data)
            {
                ZNetPeer peer = ZNet.instance.GetPeer(data.m_senderPeerID);
                string steamid = "";
                try
                {
                    steamid = peer.m_rpc.GetSocket().GetHostName();
                }
                catch
                {
                    // ignored
                }

                if (data?.m_methodHash == sayMethodHash)
                    try
                    {
                        ZDO zdoData = ZDOMan.instance.GetZDO(peer.m_characterID);
                        Vector3 pos = zdoData.GetPosition();
                        ZPackage package = new ZPackage(data.m_parameters.GetArray());
                        int messageType = package.ReadInt();
                        string userName = package.ReadString();
                        string message = package.ReadString();
                        message = (message == null ? "" : message).Trim();

                        if (message.StartsWith("!pin"))
                        {
                            string[] messageParts = message.Split(' ');
                            string pinType = "dot";
                            int startIdx = 1;
                            if (messageParts.Length > 1 && Array.Exists(ALLOWED_PINS, e => e == messageParts[1]))
                            {
                                pinType = messageParts[1];
                                startIdx = 2;
                            }

                            string pinText = "";
                            if (startIdx < messageParts.Length)
                                pinText = string.Join(" ", messageParts, startIdx, messageParts.Length - startIdx);

                            if (pinText.Length > 20) pinText = pinText.Substring(0, 20);

                            string safePinsText = Regex.Replace(pinText, "[^a-zA-Z0-9 ]", "");

                            long timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                            ;
                            string pinId = $"{timestamp}-{Random.Range(1000, 9999)}";
                            mapDataServer.AddPin(steamid, pinId, pinType, userName, pos, safePinsText);

                            List<string> usersPins = mapDataServer.pins.FindAll(pin => pin.StartsWith(steamid));
                            int numOverflowPins = usersPins.Count - WebMapConfig.MAX_PINS_PER_USER;
                            for (int t = numOverflowPins; t > 0; t--)
                            {
                                int pinIdx = mapDataServer.pins.FindIndex(pin => pin.StartsWith(steamid));
                                mapDataServer.RemovePin(pinIdx);
                            }

                            SavePins();
                        }
                        else if (message.StartsWith("!undoPin"))
                        {
                            int pinIdx = mapDataServer.pins.FindLastIndex(pin => pin.StartsWith(steamid));
                            if (pinIdx > -1)
                            {
                                mapDataServer.RemovePin(pinIdx);
                                SavePins();
                            }
                        }
                        else if (message.StartsWith("!deletePin"))
                        {
                            string[] messageParts = message.Split(' ');
                            string pinText = "";
                            if (messageParts.Length > 1)
                                pinText = string.Join(" ", messageParts, 1, messageParts.Length - 1);

                            int pinIdx = mapDataServer.pins.FindLastIndex(pin =>
                            {
                                string[] pinParts = pin.Split(',');
                                return pinParts[0] == steamid && pinParts[pinParts.Length - 1] == pinText;
                            });

                            if (pinIdx > -1)
                            {
                                mapDataServer.RemovePin(pinIdx);
                                SavePins();
                            }
                        }
                        else
                        {
                            mapDataServer.AddMessage(data.m_senderPeerID, messageType, userName, message);
                            Debug.Log("WebMap: (say) " + pos + " | " + messageType + " | " + userName + " | " + message);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                else if (data?.m_methodHash == chatMessageMethodHash)
                    try
                    {
                        ZPackage package = new ZPackage(data.m_parameters.GetArray());
                        Vector3 pos = package.ReadVector3();
                        int messageType = package.ReadInt();
                        string userName = package.ReadString();

                        if (messageType == (int)Talker.Type.Ping)
                            mapDataServer.BroadcastPing(data.m_senderPeerID, userName, pos);
                        else
                        {
                            string message = package.ReadString();
                            message = (message == null ? "" : message).Trim();

                            mapDataServer.AddMessage(data.m_senderPeerID, messageType, userName, message);
                            Debug.Log("WebMap: (shout) " + pos + " | " + messageType + " | " + userName + " | " + message);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
            }
        }
    }
}
