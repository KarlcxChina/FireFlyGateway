using FireFlyGateway.Controllers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace FireFlyGateway.Services.ProviderProcessors
{
    public class OpenAiProcessor : IProviderProcessor
    {
        private readonly IConfiguration _configuration;

        public OpenAiProcessor(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Process(JsonObject requestBody)
        {
            var overWriteRules = _configuration.GetSection("OverWriteRole").Get<List<OverWriteRoleRule>>();
            if (overWriteRules == null || overWriteRules.Count == 0)
            {
                return;
            }

            if (!requestBody.TryGetPropertyValue("messages", out var messagesNode) || messagesNode is not JsonArray messages || messages.Count == 0)
            {
                return;
            }

            var firstMessage = messages[0]?.AsObject();
            if (firstMessage?["role"]?.GetValue<string>() != "system")
            {
                return;
            }

            string? systemContent = firstMessage["content"]?.GetValue<string>();
            if (string.IsNullOrEmpty(systemContent))
            {
                return;
            }

            foreach (OverWriteRoleRule rule in overWriteRules)
            {
                JsonArray? roleContent = JsonNode.Parse(JsonSerializer.Serialize(rule.Content))!.AsArray();
                if (string.IsNullOrEmpty(rule.Key) || !systemContent!.Contains(rule.Key))
                {
                    continue;
                }

                systemContent = systemContent.Replace(rule.Key, "");
                firstMessage["content"] = systemContent;

                switch (rule.Type)
                {
                    case "sys":
                        var newContentPart = roleContent?[0]?["text"]?.GetValue<string>();
                        if (newContentPart != null)
                        {
                            systemContent = systemContent + newContentPart;
                            firstMessage["content"] = systemContent;
                        }
                        break;

                    case "add_before":
                        if (roleContent is JsonArray contentBefore && contentBefore != null)
                        {
                            var formattedMessages = FormatMessages(contentBefore).Reverse();
                            foreach (var newMessage in formattedMessages)
                            {
                                messages.Insert(1, newMessage);
                            }
                        }
                        break;

                    case "add_after":
                        if (roleContent is JsonArray contentAfter)
                        {
                            var formattedMessages = FormatMessages(contentAfter);
                            foreach (var newMessage in formattedMessages)
                            {
                                messages.Add(newMessage);
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Trans default JsonArray to OpenAI standered IEnumerable<JsonObject>
        /// default: [{"user":"..."}, {"model":"..."}]
        /// OpenAI: [{"role":"user", "content":"..."}, {"role":"assistant", "content":"..."}]
        /// </summary>
        /// <param name="sourceArray">Source JsonArray</param>
        /// <returns>Formatted JsonObjects</returns>
        private IEnumerable<JsonObject> FormatMessages(JsonArray sourceArray)
        {
            foreach (var node in sourceArray)
            {
                if (node is not JsonObject obj || obj.Count != 1) continue;

                var property = obj.First();
                string role = property.Key;

                if (role.Equals("model", System.StringComparison.OrdinalIgnoreCase))
                {
                    role = "assistant";
                }

                if (property.Value != null)
                {
                    yield return new JsonObject
                    {
                        ["role"] = role,
                        ["content"] = JsonNode.Parse(property.Value.ToJsonString())
                    };
                }
            }
        }
    }
}
