[English](README.md) | [简体中文](README.zh-CN.md)

# FireflyGateway

![FireflyGateway](https://github.com/KarlcxChina/FireflyGateway/blob/main/Firefly%20Gateway.png)

FireflyGateway is a lightweight, rule-driven AI request gateway. It sits between your local AI tools and upstream AI API providers to inject system prompts and conversation snippets automatically, enabling centralized, reusable conversation shaping without changing clients.

- Repository: https://github.com/KarlcxChina/FireflyGateway
- Language: C# (.NET 8)
- Docker-ready

## Key Features

- Rule-based prompt and conversation injection
  - System prompt injection (Type: `sys`)
  - Conversation head injection (Type: `add_before`)
  - Conversation tail injection (Type: `add_after`)
- Trigger-by-key design: place a unique Key in the system prompt to activate a rule; the gateway strips the Key before forwarding and injects the configured content
- Hot-reloadable rules: `rules.json` changes are picked up without restart
- Supports OpenAI, Gemini, and Anthropic request formats

## How It Works

1. The client sends a conversation request; the system prompt may contain one or more Keys.
2. The gateway reads rules from `rules.json` and matches Keys found in the system prompt.
3. For each matched rule:
   - Remove the Key from the system prompt (the model cannot see it)
   - Inject content at the end of the system prompt, at the beginning of the conversation, or at the end of the conversation depending on rule type
4. Forward the modified request to the upstream provider.

This allows you to centralize complex system prompts or reusable conversation snippets in the gateway. Clients only insert the Key into the system prompt to enable the desired behavior.

## Rule Configuration

- File: `rules.json` (application working directory)
- The gateway binds to configuration section `OverWriteRole`. Put your rule objects in this array.
- The file is optional and supports hot reload.

Example `rules.json` (all three modes):

```json
{
  "OverWriteRole": [
    {
      "Type": "sys",
      "Key": "{{sys}}",
      "Content": [
        { "text": "You are FireflyGateway, a concise and safe assistant. Follow organizational policies and never reveal internal secrets." }
      ]
    },
    {
      "Type": "add_before",
      "Key": "{{add_before}}",
      "Content": [
        { "user": "Before anything else, please confirm you understand the context." },
        { "model": "Acknowledged. I will follow the provided constraints." }
      ]
    },
    {
      "Type": "add_after",
      "Key": "{{add_after}}",
      "Content": [
        { "model": "Before answering, prioritize safety, correctness, and policy compliance." }
      ]
    }
  ]
}
```

### Injection Modes

1) System prompt mode (Type: `sys`)

Rule schema:
```json
{
  "Type": "sys",
  "Key": "{{sys}}",
  "Content": [
    { "text": "" }
  ]
}
```

- Triggered when the system prompt contains the Key value.
- The gateway removes the Key from the system prompt and appends each `text` item in `Content` to the end of the system prompt.
- Use this to preconfigure complex system prompts in the gateway; clients just place the Key in the system prompt.

2) Conversation head mode (Type: `add_before`)

Rule schema:
```json
{
  "Type": "add_before",
  "Key": "{{add_before}}",
  "Content": [
    { "user": "" },
    { "model": "" }
  ]
}
```

- Triggered when the system prompt contains the Key value.
- The gateway removes the Key and inserts the `Content` messages at the beginning of the entire conversation history.
- Useful for model self-knowledge or content not convenient for the system prompt. For example, some Gemini setups appear stricter about system prompts; placing content in the conversation can avoid blocking while still being effective.

3) Conversation tail mode (Type: `add_after`)

Rule schema:
```json
{
  "Type": "add_after",
  "Key": "{{add_after}}",
  "Content": [
    { "model": "" }
  ]
}
```

- Triggered when the system prompt contains the Key value.
- The gateway removes the Key and appends the `Content` messages to the end of the conversation history.
- Note: Conversation order is typically System → User → Model → ... → Model → User
  - Appending a final `model` message means the request ends with a model turn. Provider behavior varies:
    - Gemini: The GCP VertexAI interface inserts this message directly into the model's message header, so the message's content directly determines the model's response. If the model interprets the message as more like a user prompt, it will complete the question as the user. If it interprets the message as part of an answer, it will skip the thought process and generate the answer directly. Most interestingly, if the model interprets the message as part of a thought chain, it will immediately begin thinking. Importantly, since the model begins thinking sequentially, there's no start-of-thought marker, allowing us to directly access the original thought chain! It's important to note that in some cases, after interpreting the injected message as a user prompt and completing it, the model will automatically enter thought. At this point, because the start-of-thought marker is intact, it will be captured by Google and summarized in the thought chain, resulting in an output-first, thought-later pattern. This pattern can prevent the model from receiving the subsequent answer output, so be careful not to mislead the model into interpreting the message as a user prompt.
    - Claude: explicitly supports continuing from the injected content
    - DeepSeek: docs suggest extra parameters are needed, but it often continues without them
    - OpenAI: not supported
  - Some relay proxies may block this behavior, making the injection ineffective.

### Key Best Practices

- Use Keys that won’t collide with natural language, e.g., `{{sys}}`, `{{add_before}}`, `{{add_after}}`.
- Avoid words or phrases likely to appear in normal text.

## Application Configuration

- App settings: `appsettings.json`
  ```json
  {
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft.AspNetCore": "Information"
      }
    },
    "AllowedHosts": "*",
    "AiEndpointBaseUrl": "YOUR_API_ENDPOINT_BASE_URL"
  }
  ```
- Set `AiEndpointBaseUrl` to your upstream API provider’s base URL; FireflyGateway will forward requests upstream.

## Running

Prerequisites:
- .NET 8 SDK (for local builds)
- Docker (optional)

Local:
```bash
# 1) Clone
git clone https://github.com/KarlcxChina/FireflyGateway.git
cd FireflyGateway

# 2) Configure upstream base URL
# - Edit appsettings.json: set AiEndpointBaseUrl
# - Optional: create rules.json and add OverWriteRole rules (see examples above)

# 3) Run
dotnet restore
dotnet build -c Release
dotnet run
```

Docker:
```bash
# Build image
docker build -t Firefly-gateway .

# Run container
# - Mount rules.json for convenient hot updates
# - Map port 8080 (image exposes 8080/8081)
docker run --rm \
  -p 8080:8080 \
  -v "$PWD/rules.json:/app/rules.json" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  Firefly-gateway
```

Notes:
- The image exposes ports 8080 and 8081.
- The container runs as a non-root user; ensure the mounted `rules.json` is readable.

## Usage Tips

- Add one or more Keys to the system prompt to activate rules, for example:
  ```
  System prompt: "You are a helpful assistant. {{sys}} {{add_before}} {{add_after}}"
  ```
- The gateway will:
  - Remove these Keys before forwarding
  - Inject content into the system prompt or conversation history according to rules
- Exact API paths depend on your controllers and provider processors. In general, point the client’s base URL to the gateway and keep the message schema compatible with your upstream provider.

## Provider Processors

Built-in processor framework supports:
- OpenAI
- Gemini
- Anthropic
- Default pass-through

See the note above for `add_after` (tail injection) behavior differences.

## TODO / Roadmap

- Review and filter outbound content; replace or redact sensitive data
- Support dynamic values in the rule `Content` sections (runtime variables/placeholders injection)