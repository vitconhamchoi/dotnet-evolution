using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using BookSummarizer.Api.Models;
using BookSummarizer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IBookTextExtractor, BookTextExtractor>();
builder.Services.AddSingleton<ISummaryPromptBuilder, SummaryPromptBuilder>();
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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/books/summarize", async (
    SummarizeBookRequest request,
    IBookTextExtractor extractor,
    ISummaryPromptBuilder promptBuilder,
    IAiSummaryClient aiClient,
    CancellationToken cancellationToken) =>
{
    var prepared = await PrepareSummaryInputAsync(request, extractor, promptBuilder, cancellationToken);
    var summary = await aiClient.SummarizeAsync(prepared.Prompt, cancellationToken);

    return Results.Ok(new SummarizeBookResponse(
        prepared.Book.FileName,
        prepared.Book.CharacterCount,
        summary,
        DateTimeOffset.UtcNow));
});

app.MapPost("/api/books/summarize/stream", async (
    HttpContext httpContext,
    SummarizeBookRequest request,
    IBookTextExtractor extractor,
    ISummaryPromptBuilder promptBuilder,
    IAiSummaryClient aiClient,
    CancellationToken cancellationToken) =>
{
    var prepared = await PrepareSummaryInputAsync(request, extractor, promptBuilder, cancellationToken);

    httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
    httpContext.Response.Headers.Append("Cache-Control", "no-cache");

    await httpContext.Response.WriteAsync($"event: metadata\ndata: {JsonSerializer.Serialize(new
    {
        prepared.Book.FileName,
        prepared.Book.CharacterCount
    })}\n\n", cancellationToken);
    await httpContext.Response.Body.FlushAsync(cancellationToken);

    await foreach (var chunk in aiClient.StreamSummaryAsync(prepared.Prompt, cancellationToken))
    {
        var payload = JsonSerializer.Serialize(new { content = chunk });
        await httpContext.Response.WriteAsync($"event: chunk\ndata: {payload}\n\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }

    await httpContext.Response.WriteAsync("event: done\ndata: {}\n\n", cancellationToken);
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
        DateTimeOffset GeneratedAt);

    public sealed record ExtractedBook(string FileName, string Content, int CharacterCount);

    public sealed record SummaryPromptInput(string FileName, string Content, string OutputStyle, int MaxChars);
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
        public async Task<ExtractedBook> ExtractAsync(string filePath, CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension is not ".txt" and not ".md")
            {
                throw new InvalidOperationException("Current sample supports .txt and .md files only.");
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return new ExtractedBook(Path.GetFileName(filePath), content, content.Length);
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

    public interface IAiSummaryClient
    {
        Task<string> SummarizeAsync(string prompt, CancellationToken cancellationToken);
        IAsyncEnumerable<string> StreamSummaryAsync(string prompt, CancellationToken cancellationToken);
    }

    public sealed class OpenAiCompatibleSummaryClient(HttpClient httpClient, IConfiguration configuration) : IAiSummaryClient
    {
        public async Task<string> SummarizeAsync(string prompt, CancellationToken cancellationToken)
        {
            using var response = await SendRequestAsync(prompt, stream: false, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            return document.RootElement
                       .GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString()
                   ?? "No summary returned.";
        }

        public async IAsyncEnumerable<string> StreamSummaryAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
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

                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    continue;
                }

                var delta = choices[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentElement))
                {
                    var content = contentElement.GetString();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        yield return content;
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
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
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
    }
}