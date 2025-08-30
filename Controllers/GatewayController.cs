using Microsoft.AspNetCore.Mvc;
using System.Text;
using FireFlyGateway.Services;


namespace FireFlyGateway.Controllers
{
    [ApiController]
    [Route("/")]
    public class GatewayController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly string _targetBaseUrl;
        private readonly IAiGatewayProcessor _requestProcessor;
        private readonly ILogger<GatewayController> _logger;

        public GatewayController(IHttpClientFactory httpClientFactory, IConfiguration configuration, 
            ILogger<GatewayController> logger, IAiGatewayProcessor requestProcessor)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _requestProcessor = requestProcessor;
            _targetBaseUrl = _configuration["AiEndpointBaseUrl"]
                ?? throw new InvalidOperationException("AI Endpoint Base URL is not configured in appsettings.json");
        }

        [HttpPost("{*path}")]
        [HttpGet("{*path}")]
        [HttpPut("{*path}")]
        [HttpDelete("{*path}")]
        public async Task ProxyRequest([FromRoute] string path)
        {

            _logger.LogInformation("====== New Request ======");
            _logger.LogInformation("Received proxy request for path: {Path}, Method: {Method}", path, Request.Method);

            var client = _httpClientFactory.CreateClient();
            var forwardRequest = new HttpRequestMessage();

            var targetUri = new Uri($"{_targetBaseUrl.TrimEnd('/')}/{path}{Request.QueryString}");
            forwardRequest.RequestUri = targetUri;
            forwardRequest.Method = new HttpMethod(Request.Method);

            if (HttpMethods.IsPost(Request.Method) || HttpMethods.IsPut(Request.Method))
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                var requestBody = await reader.ReadToEndAsync();
                //_logger.LogInformation("Original request body: {Body}", requestBody);

                string contentToSend = requestBody;
                if ( !string.IsNullOrEmpty(requestBody) && Request.ContentType?.Contains("application/json") == true)
                {
                    contentToSend = _requestProcessor.ProcessRequestBody(requestBody);
                    forwardRequest.Content = new StringContent(contentToSend, Encoding.UTF8, "application/json");
                    //_logger.LogInformation("Content to send: {contentToSend}", contentToSend);
                }
                else
                {
                    if(Request.ContentType == null)
                    {
                        forwardRequest.Content = new StringContent(requestBody, Encoding.UTF8);
                    }
                    else
                    {
                        forwardRequest.Content = new StringContent(requestBody, Encoding.UTF8, Request.ContentType);
                    }
                }
            }
            foreach (var header in Request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                forwardRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
            var responseFromTarget = await client.SendAsync(forwardRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
            HttpContext.Response.StatusCode = (int)responseFromTarget.StatusCode;
            var hopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
                "TE", "Trailers", "Transfer-Encoding", "Upgrade"
            };
            foreach (var header in responseFromTarget.Headers)
            {
                if (!hopByHopHeaders.Contains(header.Key))
                {
                    HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }
            foreach (var header in responseFromTarget.Content.Headers)
            {
                if (!hopByHopHeaders.Contains(header.Key))
                {
                    HttpContext.Response.Headers[header.Key] = header.Value.ToArray();
                }
            }

            await responseFromTarget.Content.CopyToAsync(HttpContext.Response.Body, HttpContext.RequestAborted);
        }
    }
}
