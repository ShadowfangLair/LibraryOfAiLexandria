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
        private string _apiKey;

        public NovelAiClient(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
        }

        public void UpdateApiKey(string newKey)
        {
            _apiKey = newKey;
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
        }

        public async Task<string> GenerateResponseAsync(string prompt, string model, double temperature, string[] stopSequences = null)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return "*[NovelAI API key not configured]*";
            }

            // Map UI shorthand model names to actual NovelAI API enum identifiers
            var apiModel = model?.Trim().ToLower() ?? "kayra-v1";
            if (apiModel == "xialong") apiModel = "xialong-v1";
            else if (apiModel == "erato" || apiModel == "llama-3-erato") apiModel = "llama-3-erato-v1";
            else if (apiModel == "kayra") apiModel = "kayra-v1";
            else if (apiModel == "clio") apiModel = "clio-v1";

            bool isOpenAiModel = apiModel.Contains("xialong") || apiModel.Contains("glm");

            string requestUrl;
            string jsonPayload;

            if (isOpenAiModel)
            {
                requestUrl = "https://text.novelai.net/oa/v1/chat/completions";
                var oaBody = new
                {
                    model = apiModel,
                    messages = new[] { new { role = "user", content = prompt } },
                    temperature = temperature,
                    max_tokens = 200,
                    stop = stopSequences
                };
                jsonPayload = JsonSerializer.Serialize(oaBody);
            }
            else
            {
                requestUrl = "https://text.novelai.net/ai/generate";
                var naiBody = new
                {
                    input = prompt,
                    model = apiModel,
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
                jsonPayload = JsonSerializer.Serialize(naiBody);
            }

            var jsonContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(requestUrl, jsonContent);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return $"*[NovelAI Error: {response.StatusCode} - {responseString}]*";
                }

                using var doc = JsonDocument.Parse(responseString);
                
                if (isOpenAiModel)
                {
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("message", out var messageProp) && messageProp.TryGetProperty("content", out var contentProp))
                        {
                            return contentProp.GetString()?.Trim() ?? string.Empty;
                        }
                    }
                }
                else
                {
                    if (doc.RootElement.TryGetProperty("output", out var outputProp))
                    {
                        return outputProp.GetString()?.Trim() ?? string.Empty;
                    }
                }
                
                return $"*[NovelAI Unexpected Response Format: {responseString}]*";
            }
            catch (Exception ex)
            {
                return $"*[NovelAI Connection Error: {ex.Message}]*";
            }
        }
    }
}
