# .NET làm được gì trong mảng AI — bản thực dụng, tập trung vào code

Tài liệu này không nói lý thuyết dài dòng.
Mục tiêu là trả lời đúng câu hỏi thực dụng:

> **Nếu dùng .NET, cụ thể mình build được những gì liên quan AI?**

Câu trả lời ngắn:

**Rất nhiều.**

.NET làm tốt đặc biệt ở mảng:
- AI API backend
- chat app
- streaming response
- structured output
- RAG
- embeddings pipeline
- tool calling / function calling
- background AI job
- AI feature trong SaaS / CRM / ERP / internal tool
- auth + logging + rate limit + caching cho AI service

---

# 1) Gọi model API rất dễ

Đây là thứ cơ bản nhất: gọi OpenAI / Azure OpenAI / model gateway / local model.

## Ví dụ
```csharp
using System.Net.Http.Json;

public class AiClient(HttpClient http)
{
    public async Task<string> AskAsync(string prompt)
    {
        var response = await http.PostAsJsonAsync("/v1/chat/completions", new
        {
            model = "gpt-4.1",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        });

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ChatResponse>();
        return json?.choices?[0]?.message?.content ?? "";
    }
}

public class ChatResponse
{
    public List<Choice>? choices { get; set; }
}

public class Choice
{
    public Message? message { get; set; }
}

public class Message
{
    public string? content { get; set; }
}
```

## Thực tế dùng vào đâu?
- API hỏi đáp
- tóm tắt nội dung
- viết email
- sinh nội dung
- classify text

---

# 2) Build AI API bằng ASP.NET Core

Nếu đại ca muốn bọc model thành service thật, .NET làm cực hợp.

## Ví dụ endpoint đơn giản
```csharp
app.MapPost("/ask", async (AskRequest req, AiClient ai) =>
{
    var answer = await ai.AskAsync(req.Question);
    return Results.Ok(new { answer });
});

public record AskRequest(string Question);
```

## Thực tế dùng vào đâu?
- backend cho chatbot
- backend cho app mobile/web
- internal AI service
- AI microservice

---

# 3) Streaming chat response

AI app tử tế thường cần streaming token.
.NET làm SSE / streaming response tốt.

## Ví dụ SSE cơ bản
```csharp
app.MapGet("/stream", async (HttpContext ctx) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");

    var chunks = new[] { "Xin ", "chào ", "đại ca" };

    foreach (var chunk in chunks)
    {
        await ctx.Response.WriteAsync($"data: {chunk}\n\n");
        await ctx.Response.Body.FlushAsync();
        await Task.Delay(300);
    }
});
```

## Thực tế dùng vào đâu?
- chat UI kiểu ChatGPT
- agent status stream
- progress stream
- token-by-token answer

---

# 4) Structured output rất hợp với C# DTO

AI trả JSON mà không có type thì rất bẩn.
C# xử lý phần này ngon.

## Ví dụ
```csharp
public record InvoiceExtractResult(
    string InvoiceNumber,
    string Vendor,
    decimal Total,
    DateTime InvoiceDate
);
```

## Parse output về object
```csharp
var result = JsonSerializer.Deserialize<InvoiceExtractResult>(json);
```

## Thực tế dùng vào đâu?
- trích xuất hóa đơn
- trích xuất hợp đồng
- phân loại ticket
- AI trả object cho business flow

---

# 5) RAG backend

Đây là case thực tế nhất hiện nay.

Flow:
1. nạp tài liệu
2. cắt chunk
3. tạo embedding
4. lưu vector DB
5. query vector DB
6. lấy context
7. nhét vào prompt
8. model trả lời

## Một service RAG kiểu tối giản
```csharp
public class RagService(AiClient ai, IVectorStore store)
{
    public async Task<string> AskAsync(string question)
    {
        var docs = await store.SearchAsync(question, topK: 5);
        var context = string.Join("\n\n", docs.Select(x => x.Content));

        var prompt = $"""
        Trả lời câu hỏi dựa trên context sau:

        {context}

        Câu hỏi: {question}
        """;

        return await ai.AskAsync(prompt);
    }
}

public interface IVectorStore
{
    Task<List<DocumentChunk>> SearchAsync(string query, int topK);
}

public record DocumentChunk(string Id, string Content);
```

## Thực tế dùng vào đâu?
- hỏi đáp tài liệu nội bộ
- knowledge base cho công ty
- search thông minh
- trợ lý nội bộ

---

# 6) Embedding pipeline

.NET hoàn toàn làm được pipeline tạo embedding.

## Ví dụ
```csharp
public class EmbeddingClient(HttpClient http)
{
    public async Task<float[]> CreateAsync(string text)
    {
        var response = await http.PostAsJsonAsync("/v1/embeddings", new
        {
            model = "text-embedding-3-large",
            input = text
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
        return result!.data[0].embedding;
    }
}

public class EmbeddingResponse
{
    public List<EmbeddingItem> data { get; set; } = [];
}

public class EmbeddingItem
{
    public float[] embedding { get; set; } = [];
}
```

## Thực tế dùng vào đâu?
- semantic search
- RAG
- similar document
- clustering nhẹ

---

# 7) Tool calling / function calling

Đây là phần rất thực dụng: AI không chỉ trả text mà còn gọi tool.

## Ví dụ tool local
```csharp
public class WeatherTool
{
    public Task<string> GetWeatherAsync(string city)
        => Task.FromResult($"{city}: 32°C, nắng");
}
```

## Orchestrator đơn giản
```csharp
public class ToolRouter(WeatherTool weatherTool)
{
    public async Task<string> HandleAsync(string intent, string arg)
    {
        return intent switch
        {
            "weather" => await weatherTool.GetWeatherAsync(arg),
            _ => "Không hỗ trợ"
        };
    }
}
```

## Thực tế dùng vào đâu?
- agent gọi tool
- AI assistant nội bộ
- chatbot gọi service thật
- AI thao tác business function

---

# 8) Semantic Kernel / orchestration

Nếu không muốn tự ráp workflow tay, .NET có thể dùng Semantic Kernel.

## Ý nghĩa thực dụng
Nó giúp làm:
- prompt orchestration
- plugins
- memory
- planner
- agent workflow

## Dùng khi nào?
- nhiều tool
- nhiều bước xử lý
- cần AI workflow có cấu trúc

**Chốt:**
Nếu app AI đơn giản thì HTTP là đủ.
Nếu agent/workflow phức tạp hơn thì có thể lên Semantic Kernel.

---

# 9) Background AI job

Nhiều việc AI không nên chạy trực tiếp trong request-response.
Ví dụ:
- xử lý 1.000 file PDF
- tóm tắt hàng loạt ticket
- re-embed toàn bộ tài liệu
- OCR + extract dữ liệu

.NET làm background worker tốt.

## Ví dụ `BackgroundService`
```csharp
public class EmbeddingWorker(IServiceProvider services) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<DocumentProcessor>();
            await processor.ProcessPendingAsync();
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
```

## Thực tế dùng vào đâu?
- indexing nền
- AI batch processing
- queue consumer
- async enrichment pipeline

---

# 10) File upload + AI extract

Một case cực thực tế trong doanh nghiệp:
- user upload file
- backend đọc file
- AI trích xuất dữ liệu
- lưu DB

## Ví dụ endpoint
```csharp
app.MapPost("/extract-invoice", async (IFormFile file, InvoiceExtractor extractor) =>
{
    using var reader = new StreamReader(file.OpenReadStream());
    var text = await reader.ReadToEndAsync();
    var result = await extractor.ExtractAsync(text);
    return Results.Ok(result);
});
```

## Thực tế dùng vào đâu?
- OCR pipeline hậu xử lý
- hợp đồng
- hóa đơn
- CV/resume parsing
- tài liệu hành chính

---

# 11) AI feature trong SaaS

Đây là chỗ .NET rất hợp.

Ví dụ đại ca có SaaS CRM, có thể gắn thêm:
- AI viết email follow-up
- AI tóm tắt cuộc gọi
- AI phân loại lead
- AI sinh báo giá
- AI gợi ý phản hồi support

## Code service kiểu đơn giản
```csharp
public class LeadScoringService(AiClient ai)
{
    public async Task<string> ScoreAsync(string leadInfo)
    {
        var prompt = $"Hãy đánh giá lead sau: {leadInfo}. Trả về HOT/WARM/COLD.";
        return await ai.AskAsync(prompt);
    }
}
```

## Ý nghĩa
.NET không chỉ làm AI app riêng.
Nó rất mạnh ở kiểu **nhét AI vào sản phẩm đang có**.

---

# 12) Auth + rate limit + logging cho AI service

Đây là phần nhiều demo không có, nhưng sản phẩm thật thì bắt buộc có.

## Rate limiting
```csharp
builder.Services.AddRateLimiter(_ =>
    _.AddFixedWindowLimiter("ai", options =>
    {
        options.PermitLimit = 20;
        options.Window = TimeSpan.FromMinutes(1);
    }));
```

## Apply vào endpoint
```csharp
app.MapPost("/ask", async (AskRequest req, AiClient ai) =>
{
    var answer = await ai.AskAsync(req.Question);
    return Results.Ok(answer);
}).RequireRateLimiting("ai");
```

## Thực tế dùng vào đâu?
- chặn spam model cost
- giới hạn quota user
- bảo vệ AI endpoint nội bộ

---

# 13) Caching cho AI app

Rất nhiều câu hỏi lặp lại có thể cache.

## Ví dụ
```csharp
public class CachedAiService(AiClient ai, IMemoryCache cache)
{
    public async Task<string> AskAsync(string prompt)
    {
        if (cache.TryGetValue(prompt, out string? cached))
            return cached!;

        var answer = await ai.AskAsync(prompt);
        cache.Set(prompt, answer, TimeSpan.FromMinutes(10));
        return answer;
    }
}
```

## Thực tế dùng vào đâu?
- FAQ
- tóm tắt lặp lại
- prompt giống nhau
- giảm chi phí token

---

# 14) Database + AI metadata rất hợp

AI app thực tế thường phải lưu:
- chat history
- prompt logs
- token usage
- embedding metadata
- audit
- feedback user

.NET + EF Core làm phần này thuận.

## Ví dụ entity
```csharp
public class ChatLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Prompt { get; set; } = default!;
    public string Response { get; set; } = default!;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Thực tế dùng vào đâu?
- analytics
- audit
- billing
- debugging
- prompt improvement

---

# 15) SignalR + AI realtime UI

Nếu muốn web app realtime hơn, .NET có SignalR.

## Dùng vào đâu?
- AI chat realtime
- agent status
- progress step-by-step
- multi-user internal dashboard

Không bắt buộc phải dùng, nhưng nếu app cần realtime thì .NET có sẵn đường rất ngon.

---

# 16) Những thứ .NET làm tốt nhất trong AI

## Rất hợp để build
- AI backend API
- enterprise chat/search
- RAG service
- internal assistant
- AI workflow engine
- SaaS có AI feature
- document extraction service
- tool calling backend
- AI batch processing
- AI microservice có auth/logging/rate limit

## Không phải chỗ mạnh nhất
- training model từ đầu
- research notebook
- deep learning lab work
- ML experimentation nặng học thuật

---

# 17) Chốt cực ngắn: .NET show được gì trong AI?

**.NET làm được hết mấy thứ thực tế sau:**
- gọi model
- build AI API
- streaming chat
- structured output
- embeddings
- RAG
- tool calling
- background worker
- file extract
- auth/rate limit/cache/logging cho AI service
- nhét AI vào SaaS/backend doanh nghiệp

## Câu chốt cuối

**Nếu đại ca hỏi theo kiểu thực dụng “.NET làm được gì với AI?”**

Thì câu trả lời là:

**.NET rất mạnh ở việc biến model thành sản phẩm, thành API, thành workflow, thành feature chạy được ngoài đời.**
