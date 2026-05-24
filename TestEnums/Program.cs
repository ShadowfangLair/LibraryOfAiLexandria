using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var key = "pst-Zm9Kgu0pe3UVw3YUxDVnB4K5hLUDgF7WPNbeQ6Z0jE0ysX7y8dYG0k5qsJ9zQ2r2";
        var models = new[] { "llama-3-erato-v1", "llama-3-erato", "erato-v1", "kayra-v1", "xialong-v1" };
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
        
        foreach(var m in models)
        {
            var requestBody = new { input = "test", model = m, parameters = new { use_string = true } };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://text.novelai.net/ai/generate", content);
            var result = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"text.novelai.net /ai/generate {m}: {(int)response.StatusCode}");
            if (response.StatusCode != System.Net.HttpStatusCode.OK) Console.WriteLine(result);
        }
    }
}
