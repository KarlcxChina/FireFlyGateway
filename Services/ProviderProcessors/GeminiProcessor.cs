using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FireFlyGateway.Services.ProviderProcessors
{
    public class GeminiProcessor : IProviderProcessor
    {

        private readonly IOptionsMonitor<List<OverWriteRoleRule>> _rulesMonitor;


        public GeminiProcessor(IOptionsMonitor<List<OverWriteRoleRule>> rulesMonitor)
        {
            _rulesMonitor = rulesMonitor;
        }

        public void Process(JsonObject jsonObject)
        {
            var overWriteRules = _rulesMonitor.CurrentValue;
            if (overWriteRules == null || overWriteRules.Count == 0)
            {
                return;
            }

            if (!(jsonObject.TryGetPropertyValue("systemInstruction", out var systemInstructionNode) &&
                  systemInstructionNode is JsonObject systemInstructionObject &&
                  systemInstructionObject.TryGetPropertyValue("parts", out var partsNode) &&
                  partsNode is JsonArray partsArray &&
                  partsArray.Count > 0 &&
                  partsArray[0] is JsonObject partObject &&
                  partObject.TryGetPropertyValue("text", out var systemTextNode) &&
                  systemTextNode is JsonValue))
            {
                return;
            }

            string? systemContent = partObject["text"]?.GetValue<string>();
            if (string.IsNullOrEmpty(systemContent))
            {
                return;
            }

            if (!jsonObject.TryGetPropertyValue("contents", out var messagesNode) || messagesNode is not JsonArray)
            {
                messagesNode = new JsonArray();
                jsonObject["contents"] = messagesNode;
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
                partObject["text"] = systemContent;

                switch (rule.Type)
                {
                    case "sys":
                        var newContentPart = roleContent?[0]?["text"]?.GetValue<string>();
                        if (newContentPart != null)
                        {
                            systemContent = systemContent + newContentPart;
                            partObject["text"] = systemContent;
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

                if (property.Value != null)
                {
                    yield return new JsonObject
                    {
                        ["parts"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["text"] = JsonNode.Parse(property.Value.ToJsonString())
                            }
                        },
                        ["role"] = role
                    };
                }
            }
        }
    }
}
