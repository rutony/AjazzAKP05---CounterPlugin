using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CounterPlugin
{
    public class Program
    {
        private static ClientWebSocket webSocket;
        private static string pluginUUID;
        private static Dictionary<string, int> counters = new Dictionary<string, int>();

        public static async Task Main(string[] args)
        {
            string port = null;
            string registerEvent = null;
            string info = null;

            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-port":
                        port = args[++i];
                        break;
                    case "-pluginUUID":
                        pluginUUID = args[++i];
                        break;
                    case "-registerEvent":
                        registerEvent = args[++i];
                        break;
                    case "-info":
                        info = args[++i];
                        break;
                }
            }

            if (port == null || pluginUUID == null || registerEvent == null)
            {
                Console.WriteLine("Missing required arguments");
                return;
            }

            await ConnectToStreamDeck(port, registerEvent, pluginUUID);
        }

        private static async Task ConnectToStreamDeck(string port, string registerEvent, string uuid)
        {
            webSocket = new ClientWebSocket();
            
            try
            {
                await webSocket.ConnectAsync(new Uri($"ws://localhost:{port}"), CancellationToken.None);
                
                // Register plugin
                var registerMessage = new
                {
                    @event = registerEvent,
                    uuid = uuid
                };
                
                await SendMessage(registerMessage);
                
                // Start listening for messages
                await ReceiveMessages();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task ReceiveMessages()
        {
            var buffer = new byte[1024 * 4];
            
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(message);
                }
            }
        }

        private static void ProcessMessage(string jsonMessage)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonMessage);
                var root = document.RootElement;
                
                if (root.TryGetProperty("event", out JsonElement eventElement))
                {
                    string eventType = eventElement.GetString();
                    
                    switch (eventType)
                    {
                        case "keyDown":
                            HandleKeyDown(root);
                            break;
                        case "willAppear":
                            HandleWillAppear(root);
                            break;
                        case "didReceiveSettings":
                            HandleDidReceiveSettings(root);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        private static async void HandleKeyDown(JsonElement root)
        {
            string context = root.GetProperty("context").GetString();
            string action = root.GetProperty("action").GetString();
            
            if (action == "com.yourname.counter.action")
            {
                // Increment counter
                if (!counters.ContainsKey(context))
                {
                    counters[context] = 0;
                }
                
                counters[context]++;
                
                // Update title on the button
                var setTitleMessage = new
                {
                    @event = "setTitle",
                    context = context,
                    payload = new
                    {
                        title = counters[context].ToString(),
                        target = 0  // Both hardware and software
                    }
                };
                
                await SendMessage(setTitleMessage);
                
                // Save settings
                var setSettingsMessage = new
                {
                    @event = "setSettings",
                    context = context,
                    payload = new
                    {
                        count = counters[context]
                    }
                };
                
                await SendMessage(setSettingsMessage);
            }
        }

        private static async void HandleWillAppear(JsonElement root)
        {
            string context = root.GetProperty("context").GetString();
            string action = root.GetProperty("action").GetString();
            
            if (action == "com.yourname.counter.action")
            {
                // Request settings for this context
                var getSettingsMessage = new
                {
                    @event = "getSettings",
                    context = context
                };
                
                await SendMessage(getSettingsMessage);
            }
        }

        private static async void HandleDidReceiveSettings(JsonElement root)
        {
            string context = root.GetProperty("context").GetString();
            
            if (root.TryGetProperty("payload", out JsonElement payloadElement))
            {
                if (payloadElement.TryGetProperty("settings", out JsonElement settingsElement))
                {
                    if (settingsElement.TryGetProperty("count", out JsonElement countElement))
                    {
                        counters[context] = countElement.GetInt32();
                    }
                    else
                    {
                        counters[context] = 0;
                    }
                }
                else
                {
                    counters[context] = 0;
                }
            }
            else
            {
                counters[context] = 0;
            }
            
            // Set initial title
            var setTitleMessage = new
            {
                @event = "setTitle",
                context = context,
                payload = new
                {
                    title = counters[context].ToString(),
                    target = 0
                }
            };
            
            await SendMessage(setTitleMessage);
        }

        private static async Task SendMessage(object message)
        {
            try
            {
                string json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }
    }
}