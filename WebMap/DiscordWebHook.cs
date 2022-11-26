using System;
using System.Collections.Specialized;
using System.Net;
using WebSocketSharp;

namespace WebMap
{
    public class DiscordWebHook : IDisposable
    {
        private readonly WebClient webClient;
        private readonly static NameValueCollection values = new NameValueCollection();
        private readonly string webHookUrl;

        public DiscordWebHook(string url)
        {
            webHookUrl = url;
            webClient = new WebClient();
        }

        public void SendMessage(string msgSend)
        {
            values.Remove("content");
            values.Add("content", msgSend);

            if (webHookUrl.IsNullOrEmpty())
            {
                ZLog.Log($"WebMap::DiscordWebHook::SendMessage: {values}");
                return;
            }
            webClient.UploadValues(webHookUrl, values);
        }

        public void Dispose()
        {
            webClient.Dispose();
        }
    }
}