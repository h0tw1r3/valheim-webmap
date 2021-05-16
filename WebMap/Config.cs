using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace WebMap {
    internal static class WebMapConfig {
        public static int TEXTURE_SIZE = 2048;
        public static int PIXEL_SIZE = 12;
        public static float EXPLORE_RADIUS = 100f;
        public static float UPDATE_FOG_TEXTURE_INTERVAL = 1f;
        public static float SAVE_FOG_TEXTURE_INTERVAL = 30f;
        public static int MAX_PINS_PER_USER = 50;

        public static int SERVER_PORT = 3000;
        public static double PLAYER_UPDATE_INTERVAL = 0.5;
        public static bool CACHE_SERVER_FILES;

        public static string WORLD_NAME = "";
        public static Vector3 WORLD_START_POS = Vector3.zero;
        public static int DEFAULT_ZOOM = 200;

        public static void ReadConfigFile(ConfigFile config) {
            TEXTURE_SIZE = config.Bind("Texture", "texture_size", 2048,
                "How large is the map texture? Probably dont change this.").Value;

            PIXEL_SIZE = config.Bind("Texture", "pixel_size", 12,
                "How many in game units does a map pixel represent? Probably dont change this.").Value;

            EXPLORE_RADIUS = config.Bind<float>("Texture", "explore_radius", 100,
                "A larger explore_radius reveals the map more quickly.").Value;

            UPDATE_FOG_TEXTURE_INTERVAL = config.Bind<float>("Interval", "update_fog_texture_interval", 1,
                "How often do we update the fog texture on the server in seconds.").Value;

            SAVE_FOG_TEXTURE_INTERVAL = config.Bind<float>("Interval", "save_fog_texture_interval", 30,
                "How often do we save the fog texture in seconds.").Value;

            MAX_PINS_PER_USER = config.Bind("User", "max_pins_per_user", 50,
                "How many pins each client is allowed to make before old ones start being deleted.").Value;

            SERVER_PORT = config.Bind("Server", "server_port", 3000,
                "HTTP port for the website. The map will be display on this site.").Value;

            PLAYER_UPDATE_INTERVAL = config.Bind<float>("Interval", "player_update_interval", 0.5f,
                "How often do we send position data to web browsers in seconds.").Value;

            CACHE_SERVER_FILES = config.Bind<bool>("Server", "cache_server_files", true,
                "Should the server cache web files to be more performant?").Value;

            DEFAULT_ZOOM = config.Bind("Texture", "default_zoom", 200,
                "How zoomed in should the web map start at? Higher is more zoomed in.").Value;

            WORLD_START_POS = config.Bind<Vector3>("Server", "world_start_pos", Vector3.zero,
                "Set the position where the spawn is. y is ignored.").Value;
        }

        public static string GetWorldName() {
            if (WORLD_NAME != "") return WORLD_NAME;
            if (ZNet.instance != null) {
                WORLD_NAME = ZNet.instance.GetWorldName();
            } else {
                string[] arguments = Environment.GetCommandLineArgs();
                string worldName = "";
                for (int t = 0; t < arguments.Length; t++)
                    if (arguments[t] == "-world") {
                        worldName = arguments[t + 1];
                        break;
                    }
                WORLD_NAME = worldName;
            }
            return WORLD_NAME;
        }

        public static string MakeClientConfigJson() {
            Dictionary<string, object> config = new Dictionary<string, object>();

            config["world_name"] = GetWorldName();
            config["world_start_pos"] = WORLD_START_POS;
            config["default_zoom"] = DEFAULT_ZOOM;
            config["texture_size"] = TEXTURE_SIZE;
            config["pixel_size"] = PIXEL_SIZE;
            config["update_interval"] = PLAYER_UPDATE_INTERVAL;
            config["explore_radius"] = EXPLORE_RADIUS;

            string json = DictionaryToJson(config);

            Debug.Log("Config#: " + json);
            return json;
        }

        static string DictionaryToJson(Dictionary<string, object> dict) {
            var entries = dict.Select(d => {
                switch (d.Value) {
                    case float o:
                        return $"\"{d.Key}\": {o.ToString("F2", CultureInfo.InvariantCulture)}";
                    case double o:
                        return $"\"{d.Key}\": {o.ToString("F2", CultureInfo.InvariantCulture)}";
                    case string o:
                        return $"\"{d.Key}\": \"{o}\"";
                    case Vector3 o:
                        return $"\"{d.Key}\": \"{o.x.ToString("F2", CultureInfo.InvariantCulture)}," +
                               $"{o.y.ToString("F2", CultureInfo.InvariantCulture)}," +
                               $"{o.z.ToString("F2", CultureInfo.InvariantCulture)}\"";
                    default:
                        return $"\"{d.Key}\": {d.Value}";
                }
            });
            return "{" + string.Join(",", entries) + "}";
        }
    }
}