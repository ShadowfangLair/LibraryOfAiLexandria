using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var models = new[] { "xialong-v1", "xialong", "llama-3-erato-v1", "erato", "erato-v1", "kayra-v1", "kayra", "clio-v1" };
        using var client = new HttpClient();
        
        foreach(var m in models)
        {
            var json = $"{{\"input\":\"test\", \"model\":\"{m}\", \"parameters\":{{\"use_string\":true}}}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.novelai.net/ai/generate", content);
            var result = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"{m}: {(int)response.StatusCode} - {result}");
        }
    }
}
