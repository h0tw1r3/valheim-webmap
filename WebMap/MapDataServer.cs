using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using WebSocketSharp.Net;
using WebSocketSharp.Server;
using static WebMap.WebMapConfig;

namespace WebMap
{
    [Serializable]
    public struct MapMessage
    {
        public long id;
        public int type;
        public string name;
        public string message;
        public string ts;

        public MapMessage (long id, int type, string name, string message)
        {
            this.id = id;
            this.type = type;
            this.name = name;
            this.message = message;
            this.ts = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }

    public class WebSocketHandler : WebSocketBehavior
    {
        // protected override void OnOpen() {
        //     Context.WebSocket.Send("hi " + ID);
        // }

        // protected override void OnClose(CloseEventArgs e) {
        // }

        // protected override void OnMessage(MessageEventArgs e) {
        //     Sessions.Broadcast(e.Data);
        // }
    }

    public class MapDataServer
    {
        private static readonly Dictionary<string, string> contentTypes = new Dictionary<string, string> {
            {"html", "text/html"},
            {"js", "text/javascript"},
            {"css", "text/css"},
            {"png", "image/png"}
        };

        private readonly System.Threading.Timer broadcastTimer;
        private readonly Dictionary<string, byte[]> fileCache;
        public Texture2D fogTexture;
        private readonly HttpServer httpServer;

        public byte[] mapImageData;
        public List<string> pins = new List<string>();
        public List<MapMessage> sentMessages = new List<MapMessage>();
        public List<MapMessage> newMessages = new List<MapMessage>();
        public List<ZNetPeer> players = new List<ZNetPeer>();
        public string lastPlayerData = "init";
        private readonly string publicRoot;
        private readonly WebSocketServiceHost webSocketHandler;

        public MapDataServer()
        {
            httpServer = new HttpServer(SERVER_PORT);
            httpServer.AddWebSocketService<WebSocketHandler>("/");
            httpServer.KeepClean = true;

            webSocketHandler = httpServer.WebSocketServices["/"];

            broadcastTimer = new System.Threading.Timer(e =>
            {
                string dataString = "";
                players.ForEach(player =>
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
                        float maxHealth = zdoData.GetFloat("max_health", 25f);
                        float health = zdoData.GetFloat("health", maxHealth);
                        maxHealth = Mathf.Max(maxHealth, health);

                        if (player.m_publicRefPos)
                            dataString +=
                                $"{player.m_uid}\n{player.m_playerName}\n{pos.x:0.##},{pos.z:0.##}\n{health:0.##}\n{maxHealth:0.##}\n\n";
                        else
                            dataString += $"{player.m_uid}\n{player.m_playerName}\nhidden\n\n";
                    }
                });
                if (dataString != lastPlayerData) {
                    webSocketHandler.Sessions.Broadcast("players\n" + dataString.Trim());
                    lastPlayerData = dataString;
                }

                if (newMessages.Count > 0)
                {
                    List<string> tosend = new List<string>();

                    newMessages.ForEach(message => {
                        if (WebMapConfig.MAX_MESSAGES < sentMessages.Count) sentMessages.RemoveAt(0);
                        tosend.Add(message.ToJson());
                        sentMessages.Add(message);
                    });
                    if (tosend.Count > 0) webSocketHandler.Sessions.Broadcast("messages\n[" + string.Join(",", tosend) + "]");
                    newMessages.Clear();
                    newMessages.TrimExcess();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(PLAYER_UPDATE_INTERVAL));

            publicRoot = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "web");

            fileCache = new Dictionary<string, byte[]>();

            httpServer.OnGet += (sender, e) =>
            {
                HttpListenerRequest req = e.Request;
                // Debug.Log("~~~ Got GET Request for: " + req.RawUrl);

                if (ProcessSpecialRoutes(e)) return;

                ServeStaticFiles(e);
            };
        }

        public void Stop()
        {
            broadcastTimer.Dispose();
            httpServer.Stop();
        }

        private void ServeStaticFiles(HttpRequestEventArgs e)
        {
            HttpListenerRequest req = e.Request;
            HttpListenerResponse res = e.Response;

            string rawRequestPath = req.RawUrl;
            if (rawRequestPath == "/") rawRequestPath = "/index.html";

            string[] pathParts = rawRequestPath.Split('/');
            string requestedFile = pathParts[pathParts.Length - 1];
            string[] fileParts = requestedFile.Split('.');
            string fileExt = fileParts[fileParts.Length - 1];

            if (contentTypes.ContainsKey(fileExt))
            {
                byte[] requestedFileBytes = new byte[0];
                if (fileCache.ContainsKey(requestedFile))
                {
                    requestedFileBytes = fileCache[requestedFile];
                }
                else
                {
                    string filePath = Path.Combine(publicRoot, requestedFile);
                    try
                    {
                        requestedFileBytes = File.ReadAllBytes(filePath);
                        if (CACHE_SERVER_FILES) fileCache.Add(requestedFile, requestedFileBytes);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("WebMap: FAILED TO READ FILE! " + ex.Message);
                    }
                }

                if (requestedFileBytes.Length > 0)
                {
                    res.Headers.Add(HttpResponseHeader.CacheControl, "public, max-age=604800, immutable");
                    res.ContentType = contentTypes[fileExt];
                    res.StatusCode = 200;
                    res.ContentLength64 = requestedFileBytes.Length;
                    res.Close(requestedFileBytes, true);
                }
                else
                {
                    res.StatusCode = 404;
                    res.Close();
                }
            }
            else
            {
                res.StatusCode = 404;
                res.Close();
            }
        }

        private bool ProcessSpecialRoutes(HttpRequestEventArgs e)
        {
            HttpListenerRequest req = e.Request;
            HttpListenerResponse res = e.Response;
            string rawRequestPath = req.RawUrl;
            byte[] textBytes;

            switch (rawRequestPath)
            {
                case "/config":
                    res.Headers.Add(HttpResponseHeader.CacheControl, "no-cache");
                    res.ContentType = "application/json";
                    res.StatusCode = 200;
                    textBytes = Encoding.UTF8.GetBytes(MakeClientConfigJson());
                    res.ContentLength64 = textBytes.Length;
                    res.Close(textBytes, true);
                    return true;
                case "/map":
                    // Doing things this way to make the full map harder to accidentally see.
                    res.Headers.Add(HttpResponseHeader.CacheControl, "public, max-age=604800, immutable");
                    res.ContentType = "application/octet-stream";
                    res.StatusCode = 200;
                    res.ContentLength64 = mapImageData.Length;
                    res.Close(mapImageData, true);
                    return true;
                case "/fog":
                    res.Headers.Add(HttpResponseHeader.CacheControl, "no-cache");
                    res.ContentType = "image/png";
                    res.StatusCode = 200;
                    byte[] fogBytes = fogTexture.EncodeToPNG();
                    res.ContentLength64 = fogBytes.Length;
                    res.Close(fogBytes, true);
                    return true;
                case "/messages":
                    res.Headers.Add(HttpResponseHeader.CacheControl, "no-cache");
                    res.ContentType = "applicaion/json";
                    res.StatusCode = 200;
                    List<string> tosend = new List<string>();
                    sentMessages.ForEach(message =>
                    {
                        tosend.Add(message.ToJson());
                    });
                    textBytes = Encoding.UTF8.GetBytes("[" + string.Join(", ", tosend) + "]");
                    res.ContentLength64 = textBytes.Length;
                    res.Close(textBytes, true);
                    return true;
                case "/pins":
                    res.Headers.Add(HttpResponseHeader.CacheControl, "no-cache");
                    res.ContentType = "text/csv";
                    res.StatusCode = 200;
                    string text = string.Join("\n", pins);
                    textBytes = Encoding.UTF8.GetBytes(text);
                    res.ContentLength64 = textBytes.Length;
                    res.Close(textBytes, true);
                    return true;
            }

            return false;
        }

        public void ListenAsync()
        {
            httpServer.Start();

            if (httpServer.IsListening)
                Debug.Log($"WebMap: HTTP Server Listening on port {SERVER_PORT}");
            else
                Debug.Log("WebMap: HTTP Server Failed To Start !!!");
        }

        public void BroadcastPing(long id, string name, Vector3 position)
        {
            webSocketHandler.Sessions.Broadcast($"ping\n{id}\n{name}\n{FixedValue(position.x)},{FixedValue(position.z)}");
        }

        public void BroadcastMessage(long id, int type, string name, string message)
        {
            webSocketHandler.Sessions.Broadcast($"message\n{id}\n{type}\n{name}\n{message}");
        }

        public void AddPin(string id, string pinId, string type, string name, Vector3 position, string pinText)
        {
            pins.Add($"{id},{pinId},{type},{name},{FixedValue(position.x)},{FixedValue(position.z)},{pinText}");
            webSocketHandler.Sessions.Broadcast(
                $"pin\n{id}\n{pinId}\n{type}\n{name}\n{FixedValue(position.x)},{FixedValue(position.z)}\n{pinText}");
        }

        public void RemovePin(int idx)
        {
            string pin = pins[idx];
            string[] pinParts = pin.Split(',');
            pins.RemoveAt(idx);
            webSocketHandler.Sessions.Broadcast($"rmpin\n{pinParts[1]}");
        }

        public void AddMessage(long id, int type, string name, string message)
        {
            newMessages.Add(new MapMessage(id, type, name, message));
        }

        private static string FixedValue(float f)
        {
            return f.ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
