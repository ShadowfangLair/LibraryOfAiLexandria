using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LibraryOfAiLexandria
{
    public class NovelAiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public NovelAiClient(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> GenerateResponseAsync(string prompt, string model, double temperature, string[] stopSequences = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return "*[NovelAI API key not configured]*";
            }

            var requestBody = new
            {
                input = prompt,
                model = model,
                parameters = new
                {
                    use_string = true,
                    temperature = temperature,
                    max_length = 200,
                    min_length = 1,
                    tail_free_sampling = 1,
                    repetition_penalty = 1.15,
                    repetition_penalty_range = 2048,
                    repetition_penalty_slope = 0.09,
                    top_a = 1,
                    top_p = 1,
                    top_k = 0,
                    typical_p = 1,
                    stop_sequences = stopSequences
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("https://api.novelai.net/ai/generate", jsonContent);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"*[NovelAI Error: {response.StatusCode} - {responseString}]*";
                }

                using var doc = JsonDocument.Parse(responseString);
                var output = doc.RootElement.GetProperty("output").GetString();
                return output?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"*[NovelAI Connection Error: {ex.Message}]*";
            }
        }
    }
}
