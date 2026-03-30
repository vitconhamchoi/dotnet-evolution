using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;
using UglyToad.PdfPig;
using BookSummarizer.Api.Models;
using BookSummarizer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IBookTextExtractor, BookTextExtractor>();
builder.Services.AddSingleton<ISummaryPromptBuilder, SummaryPromptBuilder>();
builder.Services.AddSingleton<IUsageMeter, InMemoryUsageMeter>();
builder.Services.AddHttpClient<IAiSummaryClient, OpenAiCompatibleSummaryClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/usage", (IUsageMeter meter) => Results.Ok(meter.GetSnapshot()));

app.MapPost("/api/books/summarize", async (
    SummarizeBookRequest request,
    IBookTextExtractor extractor,
    ISummaryPromptBuilder promptBuilder,
    IAiSummaryClient aiClient,
    IUsageMeter usageMeter,
    CancellationToken cancellationToken) =>
{
    var prepared = await PrepareSummaryInputAsync(request, extractor, promptBuilder, cancellationToken);
    var response = await aiClient.SummarizeAsync(prepared.Prompt, cancellationToken);

    usageMeter.Record(new UsageRecord(
        prepared.Book.FileName,
        prepared.Book.CharacterCount,
        response.Usage?.PromptTokens ?? 0,
        response.Usage?.CompletionTokens ?? 0,
        response.Usage?.TotalTokens ?? 0,
        response.Model,
        response.ElapsedMs,
        DateTimeOffset.UtcNow));

    return Results.Ok(new SummarizeBookResponse(
        prepared.Book.FileName,
        prepared.Book.CharacterCount,
        response.Content,
        response.Model,
        response.Usage,
        DateTimeOffset.UtcNow));
});

app.MapPost("/api/books/summarize/stream", async (
    HttpContext httpContext,
    SummarizeBookRequest request,
    IBookTextExtractor extractor,
    ISummaryPromptBuilder promptBuilder,
    IAiSummaryClient aiClient,
    IUsageMeter usageMeter,
    CancellationToken cancellationToken) =>
{
    var prepared = await PrepareSummaryInputAsync(request, extractor, promptBuilder, cancellationToken);

    httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
    httpContext.Response.Headers.Append("Cache-Control", "no-cache");
    httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

    await httpContext.Response.WriteAsync($"event: metadata\ndata: {JsonSerializer.Serialize(new
    {
        prepared.Book.FileName,
        prepared.Book.CharacterCount
    })}\n\n", cancellationToken);
    await httpContext.Response.Body.FlushAsync(cancellationToken);

    var contentBuilder = new StringBuilder();
    SummaryUsage? finalUsage = null;
    string? finalModel = null;
    long elapsedMs = 0;

    await foreach (var item in aiClient.StreamSummaryAsync(prepared.Prompt, cancellationToken))
    {
        if (!string.IsNullOrEmpty(item.ContentChunk))
        {
            contentBuilder.Append(item.ContentChunk);
            var payload = JsonSerializer.Serialize(new { content = item.ContentChunk });
            await httpContext.Response.WriteAsync($"event: chunk\ndata: {payload}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }

        if (item.IsCompleted)
        {
            finalUsage = item.Usage;
            finalModel = item.Model;
            elapsedMs = item.ElapsedMs;
        }
    }

    usageMeter.Record(new UsageRecord(
        prepared.Book.FileName,
        prepared.Book.CharacterCount,
        finalUsage?.PromptTokens ?? 0,
        finalUsage?.CompletionTokens ?? 0,
        finalUsage?.TotalTokens ?? 0,
        finalModel ?? "unknown",
        elapsedMs,
        DateTimeOffset.UtcNow));

    await httpContext.Response.WriteAsync($"event: done\ndata: {JsonSerializer.Serialize(new
    {
        model = finalModel,
        usage = finalUsage,
        summaryLength = contentBuilder.Length,
        elapsedMs
    })}\n\n", cancellationToken);
    await httpContext.Response.Body.FlushAsync(cancellationToken);
});

app.Run();

static async Task<(ExtractedBook Book, string Prompt)> PrepareSummaryInputAsync(
    SummarizeBookRequest request,
    IBookTextExtractor extractor,
    ISummaryPromptBuilder promptBuilder,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.FilePath))
    {
        throw new BadHttpRequestException("filePath is required");
    }

    if (!File.Exists(request.FilePath))
    {
        throw new FileNotFoundException("File not found", request.FilePath);
    }

    var extracted = await extractor.ExtractAsync(request.FilePath, cancellationToken);

    if (string.IsNullOrWhiteSpace(extracted.Content))
    {
        throw new BadHttpRequestException("Could not extract text from file");
    }

    var prompt = promptBuilder.Build(new SummaryPromptInput(
        extracted.FileName,
        extracted.Content,
        request.OutputStyle ?? "executive-summary",
        request.MaxChars is > 0 ? request.MaxChars.Value : 3000));

    return (extracted, prompt);
}

namespace BookSummarizer.Api.Models
{
    public sealed record SummarizeBookRequest(string FilePath, string? OutputStyle, int? MaxChars);

    public sealed record SummarizeBookResponse(
        string FileName,
        int CharacterCount,
        string Summary,
        string? Model,
        SummaryUsage? Usage,
        DateTimeOffset GeneratedAt);

    public sealed record ExtractedBook(string FileName, string Content, int CharacterCount);

    public sealed record SummaryPromptInput(string FileName, string Content, string OutputStyle, int MaxChars);

    public sealed record SummaryUsage(int PromptTokens, int CompletionTokens, int TotalTokens);

    public sealed record SummaryResponse(string Content, string? Model, SummaryUsage? Usage, long ElapsedMs);

    public sealed record StreamingChunk(string? ContentChunk, bool IsCompleted, string? Model, SummaryUsage? Usage, long ElapsedMs);

    public sealed record UsageRecord(
        string FileName,
        int CharacterCount,
        int PromptTokens,
        int CompletionTokens,
        int TotalTokens,
        string Model,
        long ElapsedMs,
        DateTimeOffset CreatedAt);

    public sealed record UsageSnapshot(int TotalRequests, int TotalPromptTokens, int TotalCompletionTokens, int TotalTokens, List<UsageRecord> Records);
}

namespace BookSummarizer.Api.Services
{
    using BookSummarizer.Api.Models;

    public interface IBookTextExtractor
    {
        Task<ExtractedBook> ExtractAsync(string filePath, CancellationToken cancellationToken);
    }

    public sealed class BookTextExtractor : IBookTextExtractor
    {
        public Task<ExtractedBook> ExtractAsync(string filePath, CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".txt" or ".md" => ExtractTextFileAsync(filePath, cancellationToken),
                ".pdf" => ExtractPdfAsync(filePath),
                _ => throw new InvalidOperationException("Current sample supports .txt, .md and .pdf files.")
            };
        }

        private static async Task<ExtractedBook> ExtractTextFileAsync(string filePath, CancellationToken cancellationToken)
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return new ExtractedBook(Path.GetFileName(filePath), content, content.Length);
        }

        private static Task<ExtractedBook> ExtractPdfAsync(string filePath)
        {
            var sb = new StringBuilder();
            using var document = PdfDocument.Open(filePath);

            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            var content = sb.ToString();
            return Task.FromResult(new ExtractedBook(Path.GetFileName(filePath), content, content.Length));
        }
    }

    public interface ISummaryPromptBuilder
    {
        string Build(SummaryPromptInput input);
    }

    public sealed class SummaryPromptBuilder : ISummaryPromptBuilder
    {
        public string Build(SummaryPromptInput input)
        {
            var trimmed = input.Content.Length > input.MaxChars
                ? input.Content[..input.MaxChars]
                : input.Content;

            var sb = new StringBuilder();
            sb.AppendLine("You are a serious book summarization assistant.");
            sb.AppendLine("Write in Vietnamese.");
            sb.AppendLine("Be concise, structured, and useful for real readers.");
            sb.AppendLine("Output style: " + input.OutputStyle);
            sb.AppendLine();
            sb.AppendLine("Required sections:");
            sb.AppendLine("1. Tóm tắt ngắn");
            sb.AppendLine("2. Luận điểm/chủ đề chính");
            sb.AppendLine("3. Ý tưởng đáng nhớ");
            sb.AppendLine("4. Ứng dụng thực tế");
            sb.AppendLine();
            sb.AppendLine("Book file: " + input.FileName);
            sb.AppendLine("Book content:");
            sb.AppendLine(trimmed);
            return sb.ToString();
        }
    }

    public interface IUsageMeter
    {
        void Record(UsageRecord record);
        UsageSnapshot GetSnapshot();
    }

    public sealed class InMemoryUsageMeter : IUsageMeter
    {
        private readonly List<UsageRecord> _records = [];
        private readonly object _lock = new();

        public void Record(UsageRecord record)
        {
            lock (_lock)
            {
                _records.Insert(0, record);
                if (_records.Count > 100)
                {
                    _records.RemoveAt(_records.Count - 1);
                }
            }
        }

        public UsageSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new UsageSnapshot(
                    _records.Count,
                    _records.Sum(x => x.PromptTokens),
                    _records.Sum(x => x.CompletionTokens),
                    _records.Sum(x => x.TotalTokens),
                    _records.ToList());
            }
        }
    }

    public interface IAiSummaryClient
    {
        Task<SummaryResponse> SummarizeAsync(string prompt, CancellationToken cancellationToken);
        IAsyncEnumerable<StreamingChunk> StreamSummaryAsync(string prompt, CancellationToken cancellationToken);
    }

    public sealed class OpenAiCompatibleSummaryClient(HttpClient httpClient, IConfiguration configuration) : IAiSummaryClient
    {
        public async Task<SummaryResponse> SummarizeAsync(string prompt, CancellationToken cancellationToken)
        {
            var started = DateTimeOffset.UtcNow;
            using var response = await SendRequestAsync(prompt, stream: false, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = document.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "No summary returned.";
            var model = root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : null;
            var usage = ParseUsage(root);
            var elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;

            return new SummaryResponse(content, model, usage, elapsedMs);
        }

        public async IAsyncEnumerable<StreamingChunk> StreamSummaryAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var started = DateTimeOffset.UtcNow;
            using var response = await SendRequestAsync(prompt, stream: true, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
                {
                    continue;
                }

                var data = line[5..].Trim();
                if (data == "[DONE]")
                {
                    yield break;
                }

                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;

                var model = root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() : null;
                var usage = ParseUsage(root);
                var elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var contentElement))
                    {
                        var content = contentElement.GetString();
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            yield return new StreamingChunk(content, false, model, usage, elapsedMs);
                        }
                    }
                    else if (choice.TryGetProperty("finish_reason", out _))
                    {
                        yield return new StreamingChunk(null, true, model, usage, elapsedMs);
                    }
                }
            }
        }

        private async Task<HttpResponseMessage> SendRequestAsync(string prompt, bool stream, CancellationToken cancellationToken)
        {
            var apiKey = configuration["AI:ApiKey"];
            var baseUrl = configuration["AI:BaseUrl"];
            var model = configuration["AI:Model"] ?? "anthropic/claude-opus-4.6";

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("Missing AI configuration. Set AI__ApiKey and AI__BaseUrl.");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            if (stream)
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            }
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model,
                temperature = 0.2,
                stream,
                messages = new object[]
                {
                    new { role = "system", content = "You summarize books for busy professionals." },
                    new { role = "user", content = prompt }
                }
            }), Encoding.UTF8, "application/json");

            return await httpClient.SendAsync(
                request,
                stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                cancellationToken);
        }

        private static SummaryUsage? ParseUsage(JsonElement root)
        {
            if (!root.TryGetProperty("usage", out var usageElement))
            {
                return null;
            }

            var promptTokens = usageElement.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            var completionTokens = usageElement.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
            var totalTokens = usageElement.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : promptTokens + completionTokens;
            return new SummaryUsage(promptTokens, completionTokens, totalTokens);
        }
    }
}
