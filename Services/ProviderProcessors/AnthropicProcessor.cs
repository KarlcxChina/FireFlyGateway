using System.Text.Json;
using System.Text.Json.Nodes;

namespace FireFlyGateway.Services.ProviderProcessors
{
    public class AnthropicProcessor : IProviderProcessor
    {
        private readonly IConfiguration _configuration;

        public AnthropicProcessor(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Process(JsonObject jsonObject)
        {
            var overWriteRules = _configuration.GetSection("OverWriteRole").Get<List<OverWriteRoleRule>>();
            if (overWriteRules == null || overWriteRules.Count == 0)
            {
                return;
            }

            if (!(jsonObject.TryGetPropertyValue("system", out var systemNode) &&
                  systemNode is JsonArray systemArray &&
                  systemArray.Count > 0 &&
                  systemArray[0] is JsonObject systemObject &&
                  systemObject.TryGetPropertyValue("text", out var systemTextNode) &&
                  systemTextNode is JsonValue))
            {
                return;
            }

            string? systemContent = systemObject["text"]?.GetValue<string>();
            if (string.IsNullOrEmpty(systemContent))
            {
                return;
            }

            if (!jsonObject.TryGetPropertyValue("messages", out var messagesNode) || messagesNode is not JsonArray)
            {
                messagesNode = new JsonArray();
                jsonObject["messages"] = messagesNode;
            }
            var messages = (JsonArray)messagesNode;

            foreach (var rule in overWriteRules)
            {
                JsonArray? roleContent = JsonNode.Parse(JsonSerializer.Serialize(rule.Content))!.AsArray();

                if (string.IsNullOrEmpty(rule.Key) || !systemContent.Contains(rule.Key))
                {
                    continue;
                }

                systemContent = systemContent.Replace(rule.Key, "");
                systemObject["text"] = systemContent;

                switch (rule.Type)
                {
                    case "sys":
                        var newContentPart = roleContent?[0]?["text"]?.GetValue<string>();
                        if (newContentPart != null)
                        {
                            systemContent = systemContent + newContentPart;
                            systemObject["text"] = systemContent;
                        }
                        break;

                    case "add_before":
                        if (roleContent is JsonArray contentBefore)
                        {
                            var formattedMessages = FormatMessages(contentBefore).Reverse();
                            foreach (var newMessage in formattedMessages)
                            {
                                messages.Insert(0, newMessage);
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
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = JsonNode.Parse(property.Value.ToJsonString())
                            }
                        }
                    };
                }
            }
        }
    }
}
