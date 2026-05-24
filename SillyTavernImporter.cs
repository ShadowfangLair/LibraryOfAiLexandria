using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LibraryOfAiLexandria
{
    public class SillyTavernImporter
    {
        public static BotConfig? ImportFromCard(string filePath)
        {
            try
            {
                var ext = Path.GetExtension(filePath).ToLower();
                string jsonString = "";

                if (ext == ".json")
                {
                    jsonString = File.ReadAllText(filePath);
                }
                else if (ext == ".png")
                {
                    jsonString = ExtractCharaFromPng(filePath);
                }

                if (string.IsNullOrWhiteSpace(jsonString))
                    return null;

                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                
                // SillyTavern V2 spec uses a 'data' object inside the root
                if (root.TryGetProperty("data", out var data))
                {
                    var config = new BotConfig();
                    
                    if (data.TryGetProperty("name", out var nameProp))
                        config.Name = nameProp.GetString() ?? "Unknown Character";
                        
                    if (data.TryGetProperty("description", out var descProp))
                        config.SystemPrompt = descProp.GetString() ?? "";

                    // For avatarUrl, if it's a PNG, we might not have a URL. We'd have to host the image somewhere.
                    // For now we leave AvatarUrl blank unless they manually fill it.

                    return config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Import error: {ex.Message}");
            }
            
            return null;
        }

        private static string ExtractCharaFromPng(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            
            // Check signature
            if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                return "";

            int offset = 8;
            while (offset < bytes.Length)
            {
                // Read 4-byte length
                if (offset + 4 > bytes.Length) break;
                int length = (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
                offset += 4;

                // Read 4-byte type
                if (offset + 4 > bytes.Length) break;
                string type = Encoding.ASCII.GetString(bytes, offset, 4);
                offset += 4;

                if (type == "tEXt")
                {
                    // Read data
                    var dataStr = Encoding.GetEncoding("iso-8859-1").GetString(bytes, offset, length);
                    
                    if (dataStr.StartsWith("chara\0"))
                    {
                        var base64Data = dataStr.Substring(6);
                        var jsonBytes = Convert.FromBase64String(base64Data);
                        return Encoding.UTF8.GetString(jsonBytes);
                    }
                }

                offset += length;
                offset += 4; // CRC
            }

            return "";
        }
    }
}
