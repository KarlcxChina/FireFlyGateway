namespace FireflyGateway.Services.ProviderProcessors
{
    public class OverWriteRoleRule
    {
        public string Type { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public List<Dictionary<string, string>>? Content { get; set; }
    }
}
