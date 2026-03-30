# Book Summarizer (.NET)

Ứng dụng mẫu tóm tắt sách trên máy tính người dùng, triển khai theo đúng tư duy high-level design đã bàn trước đó.

## Kiến trúc chính

- **Client**: giao diện chat web
- **ASP.NET Core API**: nhận request, validate input, stream kết quả
- **Book Library Service**: tìm file gần đúng theo tên gợi nhớ
- **AI Orchestration Layer**: build prompt, giới hạn input, điều phối luồng
- **Inference Layer**: gọi OpenAI-compatible API
- **Usage Metering**: ghi nhận token usage và thời gian xử lý

## Hỗ trợ hiện tại

Phiên bản mẫu hiện hỗ trợ:
- `.txt`
- `.md`
- `.pdf`

Ứng dụng có thêm:
- giao diện chat web tại `/`
- người dùng chỉ cần nhập tên gợi nhớ sách
- backend tự tìm file gần đúng trong thư viện tài liệu
- streaming SSE tại `/api/chat/stream`
- usage metering tại `/api/usage`

## Cấu hình

Thiết lập biến môi trường:

```bash
export AI__ApiKey=your_api_key
export AI__BaseUrl=https://api.openai.com
export AI__Model=anthropic/claude-opus-4.6
```

Có thể cấu hình thêm thư mục tìm sách bằng appsettings hoặc environment cho `BookLibrary:Roots`.

## Chạy ứng dụng

```bash
cd BookSummarizer.Api
dotnet run
```

## Gọi API

### Tìm sách theo tên gợi nhớ

```bash
curl "http://localhost:5148/api/books/search?q=spring%20in%20action"
```

### Tóm tắt thường theo tên gợi nhớ

```bash
curl -X POST http://localhost:5148/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "query": "dotnet ai",
    "outputStyle": "executive-summary",
    "maxChars": 3000
  }'
```

### Tóm tắt dạng stream theo tên gợi nhớ

```bash
curl -N -X POST http://localhost:5148/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{
    "query": "spring in action",
    "outputStyle": "executive-summary",
    "maxChars": 2000
  }'
```

### Xem usage metering

```bash
curl http://localhost:5148/api/usage
```

## Ý nghĩa kiến trúc

Mẫu này thể hiện rõ 5 nguyên tắc:
1. client không gọi thẳng AI provider
2. AI nằm ở backend orchestration layer
3. tách library search, extraction, prompt building và inference
4. có usage metering / token cost logging
5. có thể mở rộng thành chat app tóm tắt tài liệu thực thụ
