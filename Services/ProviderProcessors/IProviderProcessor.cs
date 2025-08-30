using System.Text.Json.Nodes;
namespace FireFlyGateway.Services.ProviderProcessors
{
    /// <summary>
    /// A contract that defines the JSON processing logic for a specific AI provider
    /// </summary>
    public interface IProviderProcessor
    {
        /// <summary>
        /// Directly modify the incoming JsonObject
        /// </summary>
        /// <param name="jsonObject">Json Object needed to be processed</param>
        void Process(JsonObject jsonObject);
    }
}