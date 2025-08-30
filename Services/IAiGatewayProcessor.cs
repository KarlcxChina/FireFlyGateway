namespace FireFlyGateway.Services
{
    /// <summary>
    /// Defines the contract for handling gateway request bodies
    /// </summary>
    public interface IAiGatewayProcessor
    {
        /// <summary>
        /// Processes the incoming JSON request body string and returns a modified version
        /// </summary>
        /// <param name="jsonBody">Original JSON request body</param>
        /// <returns>Modified JSON request body</returns>
        string ProcessRequestBody(string jsonBody);
    }
}
