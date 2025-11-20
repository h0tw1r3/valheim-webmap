using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace WebMap
{
    public class ServerSideMapIntegration
    {
        private const string SERVER_SIDE_MAP_GUID = "eu.mydayyy.plugins.serversidemap";
        private static readonly object fileLock = new object();
        
        private static bool? _isPluginInstalled;
        private static string _worldsLocalPath;
        private static string _exploredFilePath;

        public static bool IsPluginInstalled()
        {
            if (_isPluginInstalled.HasValue)
                return _isPluginInstalled.Value;

            try
            {
                _isPluginInstalled = Chainloader.PluginInfos.ContainsKey(SERVER_SIDE_MAP_GUID);
                if (_isPluginInstalled.Value)
                {
                    ZLog.Log("WebMap: ServerSideMap plugin detected");
                }
            }
            catch (Exception e)
            {
                ZLog.LogWarning($"WebMap: Error checking for ServerSideMap plugin: {e.Message}");
                _isPluginInstalled = false;
            }

            return _isPluginInstalled.Value;
        }

        public static string GetExploredFilePath(string worldName)
        {
            if (!IsPluginInstalled())
                return null;

            if (_exploredFilePath != null && _exploredFilePath.Contains(worldName))
                return _exploredFilePath;

            try
            {
                // Try to find worlds_local directory
                string worldsLocalPath = GetWorldsLocalPath();
                if (string.IsNullOrEmpty(worldsLocalPath))
                {
                    ZLog.LogWarning("WebMap: Could not locate worlds_local directory");
                    return null;
                }

                // ServerSideMap file pattern from config
                string fileName = WebMapConfig.SERVERSIDEMAP_FILE_PATTERN.Replace("{worldName}", worldName);
                _exploredFilePath = Path.Combine(worldsLocalPath, fileName);

                if (File.Exists(_exploredFilePath))
                {
                    ZLog.Log($"WebMap: Found ServerSideMap explored file: {_exploredFilePath}");
                }
                else
                {
                    ZLog.LogWarning($"WebMap: ServerSideMap plugin detected but .explored file not found: {_exploredFilePath}");
                }

                return _exploredFilePath;
            }
            catch (Exception e)
            {
                ZLog.LogError($"WebMap: Error locating ServerSideMap explored file: {e.Message}");
                return null;
            }
        }

        private static string GetWorldsLocalPath()
        {
            if (!string.IsNullOrEmpty(_worldsLocalPath))
                return _worldsLocalPath;

            try
            {
                // Try to get Valheim's save path
                // On Linux: usually /config/worlds_local
                // On Windows: usually in AppData or similar
                
                // First, try to get it from ZNet if available
                if (ZNet.instance != null)
                {
                    // Valheim stores worlds in a specific location
                    // Try common paths
                    string[] possiblePaths = {
                        "/config/worlds_local",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "..", "LocalLow", "IronGate", "Valheim", "worlds_local"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "unity3d", "IronGate", "Valheim", "worlds_local")
                    };

                    foreach (string path in possiblePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            _worldsLocalPath = path;
                            ZLog.Log($"WebMap: Found worlds_local at: {_worldsLocalPath}");
                            return _worldsLocalPath;
                        }
                    }
                }

                // If config path is set, use it
                if (!string.IsNullOrEmpty(WebMapConfig.SERVERSIDEMAP_WORLDS_LOCAL_PATH))
                {
                    if (Directory.Exists(WebMapConfig.SERVERSIDEMAP_WORLDS_LOCAL_PATH))
                    {
                        _worldsLocalPath = WebMapConfig.SERVERSIDEMAP_WORLDS_LOCAL_PATH;
                        return _worldsLocalPath;
                    }
                }

                ZLog.LogWarning("WebMap: Could not auto-detect worlds_local path. Please set it in config.");
                return null;
            }
            catch (Exception e)
            {
                ZLog.LogError($"WebMap: Error getting worlds_local path: {e.Message}");
                return null;
            }
        }

        public static bool LoadExploredData(string worldName, out Texture2D fogTexture)
        {
            fogTexture = null;

            if (!IsPluginInstalled())
                return false;

            string exploredFilePath = GetExploredFilePath(worldName);
            if (string.IsNullOrEmpty(exploredFilePath) || !File.Exists(exploredFilePath))
            {
                if (IsPluginInstalled())
                {
                    ZLog.LogError($"WebMap: ServerSideMap plugin detected but .explored file not found at: {exploredFilePath}");
                }
                return false;
            }

            try
            {
                lock (fileLock)
                {
                    byte[] fileData = File.ReadAllBytes(exploredFilePath);
                    if (fileData == null || fileData.Length == 0)
                    {
                        ZLog.LogWarning("WebMap: ServerSideMap .explored file is empty");
                        return false;
                    }

                    // ServerSideMap stores data in ZPackage format
                    ZPackage pkg = new ZPackage(fileData);
                    
                    // Read version or format identifier (if present)
                    int version = pkg.ReadInt();
                    
                    // Read explored texture
                    // ServerSideMap likely stores it as Texture2D data
                    int textureSize = pkg.ReadInt();
                    byte[] textureData = pkg.ReadByteArray();
                    
                    if (textureData == null || textureData.Length == 0)
                    {
                        ZLog.LogWarning("WebMap: No texture data in ServerSideMap .explored file");
                        return false;
                    }

                    // Create texture from data
                    // ServerSideMap likely uses R8 format (single channel grayscale)
                    fogTexture = new Texture2D(textureSize, textureSize, TextureFormat.R8, false);
                    fogTexture.LoadRawTextureData(textureData);
                    fogTexture.Apply();

                    // If texture size doesn't match WebMap's, we need to scale it
                    if (textureSize != WebMapConfig.TEXTURE_SIZE)
                    {
                        Texture2D scaledTexture = ScaleTexture(fogTexture, WebMapConfig.TEXTURE_SIZE, WebMapConfig.TEXTURE_SIZE);
                        UnityEngine.Object.Destroy(fogTexture);
                        fogTexture = scaledTexture;
                        ZLog.Log($"WebMap: Scaled ServerSideMap texture from {textureSize}x{textureSize} to {WebMapConfig.TEXTURE_SIZE}x{WebMapConfig.TEXTURE_SIZE}");
                    }

                    ZLog.Log("WebMap: Successfully loaded explored data from ServerSideMap");
                    return true;
                }
            }
            catch (Exception e)
            {
                ZLog.LogError($"WebMap: Error reading ServerSideMap .explored file: {e.Message}");
                if (WebMapConfig.DEBUG)
                {
                    ZLog.LogError($"WebMap: Stack trace: {e.StackTrace}");
                }
                return false;
            }
        }

        public static bool SaveExploredData(string worldName, Texture2D fogTexture)
        {
            if (!IsPluginInstalled() || !WebMapConfig.SERVERSIDEMAP_ENABLED)
                return false;

            string exploredFilePath = GetExploredFilePath(worldName);
            if (string.IsNullOrEmpty(exploredFilePath))
                return false;

            try
            {
                lock (fileLock)
                {
                    // Read existing file to preserve pin data
                    ZPackage pkg = new ZPackage();
                    byte[] existingData = null;
                    
                    if (File.Exists(exploredFilePath))
                    {
                        try
                        {
                            existingData = File.ReadAllBytes(exploredFilePath);
                            ZPackage existingPkg = new ZPackage(existingData);
                            int version = existingPkg.ReadInt();
                            int textureSize = existingPkg.ReadInt();
                            existingPkg.ReadByteArray(); // Skip texture data
                            
                            // Write version and texture
                            pkg.Write(version);
                            pkg.Write(WebMapConfig.TEXTURE_SIZE);
                            
                            // Convert fog texture to R8 format if needed
                            byte[] textureData = GetTextureData(fogTexture);
                            pkg.Write(textureData);
                            
                            // Copy remaining data (pins) from existing file
                            while (existingPkg.GetPos() < existingPkg.Size())
                            {
                                pkg.Write(existingPkg.ReadByte());
                            }
                        }
                        catch
                        {
                            // If reading fails, create new file
                            pkg = new ZPackage();
                        }
                    }

                    // If we couldn't read existing data, create new
                    if (pkg.Size() == 0)
                    {
                        pkg.Write(1); // Version
                        pkg.Write(WebMapConfig.TEXTURE_SIZE);
                        byte[] textureData = GetTextureData(fogTexture);
                        pkg.Write(textureData);
                        // Empty pin list
                        pkg.Write(0); // Pin count
                    }

                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(exploredFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllBytes(exploredFilePath, pkg.GetArray());
                    
                    if (WebMapConfig.DEBUG)
                    {
                        ZLog.Log("WebMap: Successfully saved explored data to ServerSideMap");
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                ZLog.LogError($"WebMap: Error writing ServerSideMap .explored file: {e.Message}");
                if (WebMapConfig.DEBUG)
                {
                    ZLog.LogError($"WebMap: Stack trace: {e.StackTrace}");
                }
                return false;
            }
        }

        public static bool LoadPins(string worldName, out List<string> pins)
        {
            pins = new List<string>();

            if (!IsPluginInstalled() || !WebMapConfig.SERVERSIDEMAP_ENABLED)
                return false;

            string exploredFilePath = GetExploredFilePath(worldName);
            if (string.IsNullOrEmpty(exploredFilePath) || !File.Exists(exploredFilePath))
                return false;

            try
            {
                lock (fileLock)
                {
                    byte[] fileData = File.ReadAllBytes(exploredFilePath);
                    if (fileData == null || fileData.Length == 0)
                        return false;

                    ZPackage pkg = new ZPackage(fileData);
                    
                    // Skip version, texture size, and texture data
                    int version = pkg.ReadInt();
                    int textureSize = pkg.ReadInt();
                    pkg.ReadByteArray(); // Skip texture
                    
                    // Read pin count
                    int pinCount = pkg.ReadInt();
                    
                    for (int i = 0; i < pinCount; i++)
                    {
                        // Read pin data
                        // ServerSideMap likely stores: name, pos (Vector3), type, icon
                        string pinName = pkg.ReadString();
                        Vector3 pinPos = pkg.ReadVector3();
                        int pinType = pkg.ReadInt();
                        bool pinChecked = pkg.ReadBool();
                        
                        // Convert to WebMap format: {steamid},{pinId},{type},{name},{x},{z},{text}
                        // Use "serversidemap" as steamid and generate pinId
                        string pinId = $"ssm-{i}-{pinName.GetHashCode()}";
                        string pinTypeStr = GetPinTypeString(pinType);
                        
                        // WebMap format: steamid,pinId,type,name,x,z,text
                        string webMapPin = $"serversidemap,{pinId},{pinTypeStr},Server,{pinPos.x:F2},{pinPos.z:F2},{pinName}";
                        pins.Add(webMapPin);
                    }

                    ZLog.Log($"WebMap: Loaded {pins.Count} pins from ServerSideMap");
                    return true;
                }
            }
            catch (Exception e)
            {
                ZLog.LogError($"WebMap: Error reading pins from ServerSideMap: {e.Message}");
                if (WebMapConfig.DEBUG)
                {
                    ZLog.LogError($"WebMap: Stack trace: {e.StackTrace}");
                }
                return false;
            }
        }

        public static bool SavePin(string worldName, string steamid, string pinId, string type, string name, Vector3 position, string pinText)
        {
            if (!IsPluginInstalled() || !WebMapConfig.SERVERSIDEMAP_ENABLED)
                return false;

            string exploredFilePath = GetExploredFilePath(worldName);
            if (string.IsNullOrEmpty(exploredFilePath))
                return false;

            try
            {
                lock (fileLock)
                {
                    ZPackage pkg = new ZPackage();
                    byte[] existingData = null;
                    
                    if (File.Exists(exploredFilePath))
                    {
                        existingData = File.ReadAllBytes(exploredFilePath);
                        ZPackage existingPkg = new ZPackage(existingData);
                        
                        // Copy version, texture size, and texture
                        int version = existingPkg.ReadInt();
                        int textureSize = existingPkg.ReadInt();
                        byte[] textureData = existingPkg.ReadByteArray();
                        
                        pkg.Write(version);
                        pkg.Write(textureSize);
                        pkg.Write(textureData);
                        
                        // Read existing pins
                        int pinCount = existingPkg.ReadInt();
                        List<PinData> existingPins = new List<PinData>();
                        
                        for (int i = 0; i < pinCount; i++)
                        {
                            PinData pin = new PinData
                            {
                                name = existingPkg.ReadString(),
                                pos = existingPkg.ReadVector3(),
                                type = existingPkg.ReadInt(),
                                checked_ = existingPkg.ReadBool()
                            };
                            existingPins.Add(pin);
                        }
                        
                        // Add new pin
                        existingPins.Add(new PinData
                        {
                            name = pinText,
                            pos = position,
                            type = GetPinTypeInt(type),
                            checked_ = false
                        });
                        
                        // Write updated pin list
                        pkg.Write(existingPins.Count);
                        foreach (var pin in existingPins)
                        {
                            pkg.Write(pin.name);
                            pkg.Write(pin.pos);
                            pkg.Write(pin.type);
                            pkg.Write(pin.checked_);
                        }
                    }
                    else
                    {
                        // Create new file
                        pkg.Write(1); // Version
                        pkg.Write(WebMapConfig.TEXTURE_SIZE);
                        // Empty texture
                        byte[] emptyTexture = new byte[WebMapConfig.TEXTURE_SIZE * WebMapConfig.TEXTURE_SIZE];
                        pkg.Write(emptyTexture);
                        
                        // Add pin
                        pkg.Write(1); // Pin count
                        pkg.Write(pinText);
                        pkg.Write(position);
                        pkg.Write(GetPinTypeInt(type));
                        pkg.Write(false);
                    }

                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(exploredFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllBytes(exploredFilePath, pkg.GetArray());
                    
                    if (WebMapConfig.DEBUG)
                    {
                        ZLog.Log($"WebMap: Saved pin to ServerSideMap: {pinText} at {position}");
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                ZLog.LogError($"WebMap: Error saving pin to ServerSideMap: {e.Message}");
                if (WebMapConfig.DEBUG)
                {
                    ZLog.LogError($"WebMap: Stack trace: {e.StackTrace}");
                }
                return false;
            }
        }

        public static bool RemovePin(string worldName, string pinId)
        {
            if (!IsPluginInstalled() || !WebMapConfig.SERVERSIDEMAP_ENABLED)
                return false;

            string exploredFilePath = GetExploredFilePath(worldName);
            if (string.IsNullOrEmpty(exploredFilePath) || !File.Exists(exploredFilePath))
                return false;

            try
            {
                lock (fileLock)
                {
                    byte[] fileData = File.ReadAllBytes(exploredFilePath);
                    ZPackage pkg = new ZPackage(fileData);
                    
                    // Read version, texture size, and texture
                    int version = pkg.ReadInt();
                    int textureSize = pkg.ReadInt();
                    byte[] textureData = pkg.ReadByteArray();
                    
                    // Read pins
                    int pinCount = pkg.ReadInt();
                    List<PinData> pins = new List<PinData>();
                    
                    for (int i = 0; i < pinCount; i++)
                    {
                        PinData pin = new PinData
                        {
                            name = pkg.ReadString(),
                            pos = pkg.ReadVector3(),
                            type = pkg.ReadInt(),
                            checked_ = pkg.ReadBool()
                        };
                        pins.Add(pin);
                    }
                    
                    // Find and remove pin by matching pinId pattern
                    // pinId format: ssm-{i}-{hash}
                    int indexToRemove = -1;
                    for (int i = 0; i < pins.Count; i++)
                    {
                        string expectedPinId = $"ssm-{i}-{pins[i].name.GetHashCode()}";
                        if (expectedPinId == pinId)
                        {
                            indexToRemove = i;
                            break;
                        }
                    }
                    
                    if (indexToRemove >= 0)
                    {
                        pins.RemoveAt(indexToRemove);
                    }
                    else
                    {
                        // Try to find by name if pinId doesn't match
                        // This is a fallback
                        return false;
                    }
                    
                    // Write updated file
                    ZPackage newPkg = new ZPackage();
                    newPkg.Write(version);
                    newPkg.Write(textureSize);
                    newPkg.Write(textureData);
                    newPkg.Write(pins.Count);
                    
                    foreach (var pin in pins)
                    {
                        newPkg.Write(pin.name);
                        newPkg.Write(pin.pos);
                        newPkg.Write(pin.type);
                        newPkg.Write(pin.checked_);
                    }
                    
                    File.WriteAllBytes(exploredFilePath, newPkg.GetArray());
                    
                    if (WebMapConfig.DEBUG)
                    {
                        ZLog.Log($"WebMap: Removed pin from ServerSideMap: {pinId}");
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                ZLog.LogError($"WebMap: Error removing pin from ServerSideMap: {e.Message}");
                if (WebMapConfig.DEBUG)
                {
                    ZLog.LogError($"WebMap: Stack trace: {e.StackTrace}");
                }
                return false;
            }
        }

        private static byte[] GetTextureData(Texture2D texture)
        {
            // Convert texture to R8 format if needed
            if (texture.format == TextureFormat.R8)
            {
                return texture.GetRawTextureData();
            }
            
            // Convert to R8
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.R8);
            Graphics.Blit(texture, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            Texture2D r8Texture = new Texture2D(texture.width, texture.height, TextureFormat.R8, false);
            r8Texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            r8Texture.Apply();
            
            byte[] data = r8Texture.GetRawTextureData();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(r8Texture);
            
            return data;
        }

        private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            
            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.R8, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            
            return result;
        }

        private static string GetPinTypeString(int pinType)
        {
            // Valheim pin types: 0=Icon0 (dot), 1=Icon1, 2=Icon2, etc.
            // WebMap types: dot, fire, mine, house, cave
            switch (pinType)
            {
                case 0: return "dot";
                case 1: return "fire";
                case 2: return "mine";
                case 3: return "house";
                case 4: return "cave";
                default: return "dot";
            }
        }

        private static int GetPinTypeInt(string pinType)
        {
            switch (pinType.ToLower())
            {
                case "dot": return 0;
                case "fire": return 1;
                case "mine": return 2;
                case "house": return 3;
                case "cave": return 4;
                default: return 0;
            }
        }

        private class PinData
        {
            public string name;
            public Vector3 pos;
            public int type;
            public bool checked_;
        }
    }
}

