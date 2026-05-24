using System;
using System.Text.Json;
public class AppSettings { public string NovelAiKey { get; set; } }
class Program {
    static void Main() {
        var json = "{\"NovelAiKey\":\"123\", \"novelAiKey\":\"456\"}";
        try {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var obj = JsonSerializer.Deserialize<AppSettings>(json, options);
            Console.WriteLine("SUCCESS: " + obj.NovelAiKey);
        } catch(Exception e) { Console.WriteLine("ERROR: " + e.Message); }
    }
}
