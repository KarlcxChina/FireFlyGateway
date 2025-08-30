# FireflyGateway

![FireflyGateway](https://github.com/KarlcxChina/FireflyGateway/blob/main/Firefly%20Gateway.png)

FireflyGateway 是一个轻量的规则驱动 AI 请求网关，位于本机 AI 工具与上游 AI API 提供商之间。它通过在系统提示词与对话历史中自动注入内容，实现集中、可复用的对话定制，而无需改动客户端。

- 仓库：https://github.com/KarlcxChina/FireflyGateway
- 语言：C#（.NET 8）
- 支持 Docker 运行

## 主要功能

- 基于规则的提示与对话注入
  - 系统提示词注入（Type: `sys`）
  - 聊天开头注入（Type: `add_before`）
  - 聊天结尾注入（Type: `add_after`）
- 通过 Key 触发：在系统提示词中放入唯一 Key 即可触发对应规则；网关会在转发前移除该 Key，并按规则注入内容
- 规则热更新：`rules.json` 文件变更可自动生效，无需重启
- 同时支持 OpenAI、Gemini、Anthropic 格式的请求

## 工作机制

1. 客户端发送对话请求，系统提示词中可包含一个或多个“Key”。
2. 网关读取 `rules.json` 中配置的规则，匹配系统提示词内的 Key。
3. 对于每个命中的规则：
   - 从系统提示词中移除该 Key（即Key对模型不可见）
   - 根据规则类型，将内容注入到系统提示词末尾、对话开头或对话结尾
4. 将修改后的请求转发至上游提供商。

这种模式支持将复杂的系统提示或可复用的对话片段集中到网关统一维护，客户端仅需在系统提示词中放置 Key 即可按需启用。

## 规则配置

- 文件：`rules.json`（应用工作目录）
- 网关读取配置节 `OverWriteRole`，请将规则对象放入该数组中
- 文件可选，支持热加载

`rules.json` 示例（包含三种模式）：

```json
{
  "OverWriteRole": [
    {
      "Type": "sys",
      "Key": "{{sys}}",
      "Content": [
        { "text": "你是 FireflyGateway，一个简洁且安全的助手。遵循组织策略，不要泄露内部密钥。" }
      ]
    },
    {
      "Type": "add_before",
      "Key": "{{add_before}}",
      "Content": [
        { "user": "在开始之前，请先确认已理解上下文。" },
        { "model": "已确认，我将遵循所给约束。" }
      ]
    },
    {
      "Type": "add_after",
      "Key": "{{add_after}}",
      "Content": [
        { "model": "回答前请优先考虑安全、正确性与策略合规。" }
      ]
    }
  ]
}
```

### 注入模式说明

1）系统提示词模式（Type: `sys`）

规则格式：
```json
{
  "Type": "sys",
  "Key": "{{sys}}",
  "Content": [
    { "text": "" }
  ]
}
```

- 当系统提示词包含 `Key` 的值时触发
- 网关会移除系统提示词中的 `Key`，并将 `Content` 中的 `text` 追加到系统提示词末尾
- 适用于在网关预置复杂系统提示，客户端仅需在系统提示词中放置 Key 即可启用

2）聊天开头模式（Type: `add_before`）

规则格式：
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

- 当系统提示词包含 `Key` 的值时触发
- 网关会移除 `Key`，并将 `Content` 中的对话消息插入到整个对话历史的开头
- 适用于将模型自我认知或不便放在系统提示词中的内容，置于对话首部（如Google的Gemini模型对系统提示词的审查似乎更为严格，部分内容放在对话中不会导致屏蔽，但是无法在系统提示词中使用）

3）聊天结尾模式（Type: `add_after`）

规则格式：
```json
{
  "Type": "add_after",
  "Key": "{{add_after}}",
  "Content": [
    { "model": "" }
  ]
}
```

- 当系统提示词包含 `Key` 时触发
- 网关会移除 `Key`，并将 `Content` 中的对话消息插入到整个对话历史的结尾
- 注意：对话历史通常为 系统提示词 → 用户 → 模型 → ... → 模型 → 用户
  - 在结尾插入 `model` 消息意味着请求以模型轮次结束。不同提供商处理不一致：
    - Gemini：通常会从注入内容继续输出（官方文档未明确，但常见行为如此）
    - Claude：明确支持从注入内容继续输出
    - DeepSeek：文档要求额外参数才能继续，但实际往往在不加参数时也能继续
    - OpenAI：不支持
  - 个别中转代理也可能屏蔽这种行为，导致注入不生效。

### Key 使用建议

- 使用不易与自然语言冲突的 Key，如 `{{sys}}`、`{{add_before}}`、`{{add_after}}`
- 避免使用容易与正常文本混淆的词或短语

## 应用配置

- 应用设置：`appsettings.json`
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
- 将您的上游API提供商 BaseURL 填入 AiEndpointBaseUrl，FireflyGateway即可将请求转发至上游。

## 运行方式

环境依赖：
- .NET 8 SDK（本地构建）
- Docker（可选）

本地运行：
```bash
# 1）克隆项目
git clone https://github.com/KarlcxChina/FireflyGateway.git
cd FireflyGateway

# 2）配置上游地址
# - 编辑 appsettings.json：设置 AiEndpointBaseUrl
# - 可选：创建 rules.json 并添加 OverWriteRole 规则（见上文示例）

# 3）启动
dotnet restore
dotnet build -c Release
dotnet run
```

Docker：
```bash
# 构建镜像
docker build -t Firefly-gateway .

# 运行容器
# - 通过挂载 rules.json 便于热更新
# - 映射 8080 端口（镜像默认暴露 8080/8081）
docker run --rm \
  -p 8080:8080 \
  -v "$PWD/rules.json:/app/rules.json" \
  -e ASPNETCORE_ENVIRONMENT=Development \
  Firefly-gateway
```

说明：
- 镜像默认暴露 8080、8081 端口
- 容器以非 root 用户运行，请确保挂载的 `rules.json` 具备可读权限

## 用法提示

- 在客户端的系统提示词中加入一个或多个 Key 以激活规则，例如：
  ```
  系统提示词："你是一个有帮助的助手。{{sys}} {{add_before}} {{add_after}}"
  ```
- 网关会：
  - 在转发前移除这些 Key
  - 按规则将内容注入到系统提示词或对话历史
- 具体 API 路径取决于你的控制器与提供商处理器配置。通常做法是将客户端的请求基地址切换为网关地址，并保持与上游兼容的消息格式。

## 提供商处理器

项目内置处理器框架，支持：
- OpenAI
- Gemini
- Anthropic
- 默认直通

关于 `add_after`（结尾注入）的行为差异见前文说明。

## TODO / 规划

- 对传出的内容进行审查和过滤，替换敏感信息
- 支持在规则文件的内容部分加入动态值（运行时变量/占位符注入)