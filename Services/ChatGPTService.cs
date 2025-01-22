using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public class ChatGPTService : IChatGPTService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public ChatGPTService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> GetResponseFromChatGPTAsync(string prompt)
        {
            var requestData = new
            {
                model = "gpt-4o-mini",  // Modelo definido en el ejemplo
                store = true,           // Esta opción depende de la configuración de tu API Key
                messages = new[]
                {
                new { role = "user", content = prompt }  // Prompt del usuario
                }
            };

            var jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

            var content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();

                dynamic jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);

                return jsonResponse.choices[0].message.content;
            }
            else
            {
                var errorDetails = await response.Content.ReadAsStringAsync();

                return $"Error: {response.StatusCode}\nDetalles: {errorDetails}";
            }
        }
    }


}
