# Book Summarizer (.NET)

Ứng dụng mẫu tóm tắt sách trên máy tính người dùng, triển khai theo đúng tư duy high-level design đã bàn trước đó:

- **Client**: gọi HTTP API nội bộ
- **ASP.NET Core API**: nhận request, validate input
- **AI Orchestration Layer**: build prompt, giới hạn input, điều phối luồng
- **Inference Layer**: gọi OpenAI-compatible API
- **Local File Layer**: đọc file sách từ máy người dùng

## Hỗ trợ hiện tại

Phiên bản mẫu hiện hỗ trợ:
- `.txt`
- `.md`

Muốn hỗ trợ `.pdf`, bước tiếp theo là bổ sung PDF text extraction service.

## Cấu hình

Thiết lập biến môi trường:

```bash
export AI__ApiKey=your_api_key
export AI__BaseUrl=https://api.openai.com
export AI__Model=gpt-4.1-mini
```

## Chạy ứng dụng

```bash
cd BookSummarizer.Api
dotnet run
```

## Gọi API

```bash
curl -X POST http://localhost:5148/api/books/summarize \
  -H "Content-Type: application/json" \
  -d '{
    "filePath": "/absolute/path/to/book.txt",
    "outputStyle": "executive-summary",
    "maxChars": 12000
  }'
```

## Ý nghĩa kiến trúc

Mẫu này thể hiện rõ 5 nguyên tắc:
1. client không gọi thẳng AI provider
2. AI nằm ở backend orchestration layer
3. tách input extraction, prompt building và inference
4. dễ bổ sung metering/cost tracking về sau
5. business model có thể đặt theo số lượt tóm tắt hoặc subscription
