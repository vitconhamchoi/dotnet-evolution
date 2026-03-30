using System.Text;
using System.Text.Json;
using BookSummarizer.Api.Models;
using BookSummarizer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IBookTextExtractor, BookTextExtractor>();
builder.Services.AddSingleton<ISummaryPromptBuilder, SummaryPromptBuilder>();
builder.Services.AddHttpClient<IAiSummaryClient, OpenAiCompatibleSummaryClient>();

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
    if (string.IsNullOrWhiteSpace(request.FilePath))
    {
        return Results.BadRequest(new { error = "filePath is required" });
    }

    if (!File.Exists(request.FilePath))
    {
        return Results.NotFound(new { error = "File not found" });
    }

    var extracted = await extractor.ExtractAsync(request.FilePath, cancellationToken);

    if (string.IsNullOrWhiteSpace(extracted.Content))
    {
        return Results.BadRequest(new { error = "Could not extract text from file" });
    }

    var prompt = promptBuilder.Build(new SummaryPromptInput(
        extracted.FileName,
        extracted.Content,
        request.OutputStyle ?? "executive-summary",
        request.MaxChars is > 0 ? request.MaxChars.Value : 12000));

    var summary = await aiClient.SummarizeAsync(prompt, cancellationToken);

    return Results.Ok(new SummarizeBookResponse(
        extracted.FileName,
        extracted.CharacterCount,
        summary,
        DateTimeOffset.UtcNow));
});

app.Run();

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
    }

    public sealed class OpenAiCompatibleSummaryClient(HttpClient httpClient, IConfiguration configuration) : IAiSummaryClient
    {
        public async Task<string> SummarizeAsync(string prompt, CancellationToken cancellationToken)
        {
            var apiKey = configuration["AI:ApiKey"];
            var baseUrl = configuration["AI:BaseUrl"];
            var model = configuration["AI:Model"] ?? "gpt-4.1-mini";

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("Missing AI configuration. Set AI__ApiKey and AI__BaseUrl.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/v1/chat/completions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model,
                temperature = 0.2,
                messages = new object[]
                {
                    new { role = "system", content = "You summarize books for busy professionals." },
                    new { role = "user", content = prompt }
                }
            }), Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content ?? "No summary returned.";
        }
    }
}
