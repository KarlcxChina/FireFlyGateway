using FireflyGateway.Controllers;
using FireflyGateway.Services.ProviderProcessors;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;


namespace FireflyGateway.Services
{
    public class DefaultAiGatewayProcessor : IAiGatewayProcessor
    {
        private enum AiProvider
        {
            OpenAI,
            Gemini,
            Anthropic,
            Unknown
        }

        private readonly OpenAiProcessor _openAiProcessor;
        private readonly GeminiProcessor _geminiProcessor;
        private readonly AnthropicProcessor _anthropicProcessor;
        private readonly ILogger<GatewayController> _logger;

        public DefaultAiGatewayProcessor(ILogger<GatewayController> logger,
            OpenAiProcessor openAiProcessor,
            GeminiProcessor geminiProcessor,
            AnthropicProcessor anthropicProcessor)
        {
            _logger = logger;
            _openAiProcessor = openAiProcessor;
            _geminiProcessor = geminiProcessor;
            _anthropicProcessor = anthropicProcessor;
        }

        public string ProcessRequestBody(string requestBody)
        {
            try
            {
                var escapeOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    WriteIndented = true
                };
                var requestJsonNode = JsonNode.Parse(requestBody);
                if (requestJsonNode is JsonObject requestJsonObject)
                {
                    var provider = IdentifyProvider(requestJsonObject);

                    switch (provider)
                    {
                        case AiProvider.OpenAI:
                            _openAiProcessor.Process(requestJsonObject);
                            _logger.LogInformation("Using OpenAIprocesser");
                            break;
                        case AiProvider.Gemini:
                            _geminiProcessor.Process(requestJsonObject);
                            _logger.LogInformation("Using Gemini Processer");
                            break;
                        case AiProvider.Anthropic:
                            _anthropicProcessor.Process(requestJsonObject);
                            _logger.LogInformation("Using Anthropic Processer");
                            break;
                        case AiProvider.Unknown:
                            break;
                    }

                    return requestJsonObject.ToJsonString(escapeOptions);
                }
            }
            catch (JsonException)
            {
            }

            return requestBody;
        }

        /// <summary>
        /// Identify the API Type
        /// </summary>
        /// <param name="requestJsonObject">Request body's Json Object</param>
        /// <returns>API Type</returns>
        private AiProvider IdentifyProvider(JsonObject requestJsonObject)
        {
            if (requestJsonObject.ContainsKey("contents"))
            {
                return AiProvider.Gemini;
            }

            if (requestJsonObject.TryGetPropertyValue("messages", out var messagesNode)
                && messagesNode is JsonArray messages
                && messages.Count > 0)
            {
                if (messages[0] is JsonObject firstMessage)
                {
                    if (firstMessage.TryGetPropertyValue("content", out var contentNode))
                    {
                        if (contentNode is JsonArray) return AiProvider.Anthropic;
                        if (contentNode is JsonValue val && val.GetValue<JsonElement>().ValueKind == JsonValueKind.String)
                        {
                            return AiProvider.OpenAI;
                        }
                    }
                }

                if (requestJsonObject.ContainsKey("system"))
                {
                    return AiProvider.Anthropic;
                }
            }

            return AiProvider.Unknown;
        }
    }
}
