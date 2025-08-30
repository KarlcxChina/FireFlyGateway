using FireflyGateway.Services;
using FireflyGateway.Services.ProviderProcessors;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Configuration.AddJsonFile("rules.json", optional: true, reloadOnChange: true);
builder.Services.Configure<List<OverWriteRoleRule>>(builder.Configuration.GetSection("OverWriteRole"));

builder.Services.AddControllers();

builder.Services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName);


builder.Services.AddScoped<IAiGatewayProcessor, DefaultAiGatewayProcessor>();
builder.Services.AddScoped<OpenAiProcessor>();
builder.Services.AddScoped<GeminiProcessor>();
builder.Services.AddScoped<AnthropicProcessor>();
builder.Services.AddScoped<IAiGatewayProcessor, DefaultAiGatewayProcessor>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
