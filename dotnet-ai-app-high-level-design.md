# High-level design cho ứng dụng .NET tích hợp AI

Tài liệu này trả lời 3 câu hỏi thực dụng:

1. **Ứng dụng .NET có AI nên được thiết kế high-level thế nào?**
2. **AI nằm ở đâu trong hệ thống?**
3. **Nếu AI tốn tiền theo token / API cost thì làm sao mô hình kinh doanh vẫn có lãi?**

Tài liệu này không đi vào code chi tiết. Mục tiêu là nhìn hệ thống từ trên cao nhưng vẫn đủ thực dụng để bắt tay build.

---

# 1) Nguyên tắc lớn: AI không phải toàn bộ hệ thống

Sai lầm phổ biến là nghĩ:
- app AI = gọi model

Thực tế:
- model chỉ là **một thành phần**
- còn sản phẩm thật phải có:
  - auth
  - API
  - business rules
  - storage
  - caching
  - logging
  - billing
  - queue
  - observability
  - fallback / retry

## Câu chốt
**Trong app .NET tích hợp AI, AI nên là một service nằm trong kiến trúc, không phải toàn bộ kiến trúc.**

---

# 2) High-level design chuẩn cho app .NET có AI

## Sơ đồ tổng quát

```text
[Web / Mobile / Admin UI]
            |
            v
     [ASP.NET Core API]
            |
   -----------------------
   |         |           |
   v         v           v
[Auth]  [Business]   [AI Orchestrator]
   |         |           |
   |         |     ----------------------
   |         |     |         |          |
   |         |     v         v          v
   |         | [Prompt] [Vector DB] [LLM Provider]
   |         | [Tools ] [Cache    ] [Embeddings ]
   |         |
   v         v
[SQL/PG] [Queue/Worker]
            |
            v
   [Background AI Jobs]
```

---

# 3) AI nằm ở đâu?

## AI không nên nằm trong UI
UI chỉ nên:
- gửi request
- stream response
- hiển thị kết quả

## AI nên nằm ở backend
Cụ thể là trong lớp:
- **AI Orchestrator / AI Service Layer**

### Vì sao?
- giữ API key ở server
- kiểm soát cost
- thêm caching
- log token usage
- áp business rules
- fallback provider
- chặn abuse

## Chốt
**AI nên nằm ở backend service layer, không nằm trực tiếp ở client.**

---

# 4) Cấu trúc hệ thống hợp lý

## 4.1 Client Layer
Có thể là:
- React / Angular / Blazor / mobile app
- admin dashboard
- internal portal

### Vai trò
- nhập câu hỏi / file / yêu cầu
- xem kết quả
- chat / upload / dashboard

### Không nên làm
- không gọi trực tiếp LLM provider
- không giữ token model ở client

---

## 4.2 ASP.NET Core API Layer
Đây là entry point của hệ thống.

### Vai trò
- nhận request
- auth user
- validate input
- quota/rate limit
- route sang business flow hoặc AI flow
- trả output về client

### Endpoint ví dụ
- `/chat`
- `/summarize`
- `/extract`
- `/search`
- `/embed/reindex`
- `/admin/usage`

---

## 4.3 Business Service Layer
Đây là phần cực quan trọng nhưng nhiều app AI quên mất.

### Vai trò
- chứa rule nghiệp vụ
- quyết định khi nào gọi AI, khi nào không
- chuẩn hóa input/output
- áp policy theo từng loại user

### Ví dụ
- khách free chỉ được hỏi 5 lần/ngày
- file > 20MB phải vào queue, không xử lý sync
- legal docs phải bật citation bắt buộc
- CRM message chỉ gửi khi đã có review

## Chốt
**Business layer quyết định AI được dùng thế nào.**

---

## 4.4 AI Orchestrator Layer
Đây là trái tim của app AI.

### Vai trò
- build prompt
- chọn model
- gọi embeddings
- gọi vector search
- gọi tool/function
- retry/fallback
- normalize response
- đo token cost

### Các thành phần nhỏ bên trong
- Prompt Builder
- Model Router
- Embedding Service
- Vector Search Service
- Tool Router
- Output Parser
- Cost Meter

## Chốt
**Đây là nơi AI thực sự diễn ra.**

---

## 4.5 LLM Provider Layer
Đây là nơi gọi model thật:
- OpenAI
- Azure OpenAI
- Anthropic
- Gemini
- Ollama
- OpenRouter / gateway khác

### Vai trò
- infer text
- embeddings
- function/tool calling
- structured outputs

### Lưu ý
Nên bọc nó lại thành interface riêng.

## Ví dụ abstraction
```text
ILLMClient
  - ChatAsync
  - StreamAsync
  - EmbedAsync
  - CallToolsAsync
```

### Vì sao?
- dễ đổi provider
- dễ failover
- dễ test
- tránh khóa cứng vào 1 hãng

---

## 4.6 Data Storage Layer
Ứng dụng AI thật thường cần ít nhất 3 lớp lưu trữ.

### A. SQL / PostgreSQL / SQL Server
Lưu:
- user
- team
- roles
- chat logs
- usage
- billing
- prompt history
- extracted data

### B. Vector DB
Lưu:
- embeddings
- chunked documents
- metadata cho RAG

Ví dụ:
- pgvector
- Qdrant
- Pinecone
- Weaviate

### C. Cache
Lưu:
- answer cache
- embedding cache
- session cache
- temporary job state

Ví dụ:
- Redis
- IMemoryCache

---

## 4.7 Queue / Worker Layer
Rất nhiều việc AI không nên làm trong request đồng bộ.

### Nên đưa vào background job
- index tài liệu
- OCR hàng loạt
- re-embedding
- summarize nhiều file
- nightly reports
- large document extraction

### Công nghệ phù hợp
- Hangfire
- Quartz
- background worker
- RabbitMQ / MassTransit / Azure Queue / Kafka nếu lớn hơn

## Chốt
**Task AI nặng nên tách ra background processing.**

---

# 5) Mẫu kiến trúc theo từng loại ứng dụng

## 5.1 AI Chat / Q&A App

```text
Client Chat UI
   -> ASP.NET Core API
   -> AI Orchestrator
   -> LLM Provider
   -> SQL lưu chat history
   -> Redis cache
```

### AI nằm ở đâu?
- trong AI Orchestrator
- model chỉ được gọi từ backend

### Phí AI phát sinh ở đâu?
- mỗi request chat
- mỗi token input/output

### Muốn có lãi thì làm gì?
- giới hạn message free
- cache câu hỏi lặp lại
- phân tầng model:
  - cheap model cho câu hỏi đơn giản
  - strong model cho case khó
- subscription theo quota

---

## 5.2 RAG cho tài liệu nội bộ

```text
Upload Docs
  -> File Store
  -> Background Worker
  -> Chunk + Embed
  -> Vector DB

User Question
  -> API
  -> Vector Search
  -> Context Builder
  -> LLM Provider
  -> Answer + Citation
```

### AI nằm ở đâu?
- embedding pipeline
- retrieval + answer synthesis

### Phí AI phát sinh ở đâu?
- lúc embed tài liệu
- lúc trả lời câu hỏi

### Muốn có lãi thì làm gì?
- tính setup fee cho indexing ban đầu
- monthly fee theo số tài liệu / số user
- giới hạn số query/tháng
- cache semantic results
- chunk hợp lý để giảm token

---

## 5.3 AI Extract Documents

```text
Upload PDF/Image
   -> API
   -> OCR (nếu cần)
   -> AI Extractor
   -> Validation Rules
   -> Save structured data
   -> Export/API
```

### AI nằm ở đâu?
- OCR hậu xử lý
- extraction step
- normalize output

### Phí AI phát sinh ở đâu?
- theo document
- theo số trang
- theo OCR + extraction

### Muốn có lãi thì làm gì?
- giá theo document volume
- gói doanh nghiệp theo số lượng chứng từ
- dùng pipeline 2 bước:
  - regex/rule trước
  - AI chỉ xử lý phần khó
- tránh gọi LLM cho toàn bộ mọi thứ nếu rule-based đủ

---

## 5.4 AI CRM Assistant

```text
CRM Data
   -> API
   -> Business Rules
   -> AI Message Generator / Lead Analyzer
   -> Review / Approval
   -> Send Email / SMS / Zalo
```

### AI nằm ở đâu?
- lead scoring
- message suggestion
- summary / classification

### Phí AI phát sinh ở đâu?
- generate content
- analyze conversation

### Muốn có lãi thì làm gì?
- tính theo seat/team
- AI là add-on premium
- quota theo số tin nhắn sinh ra
- bắt review trước khi gửi để giảm risk pháp lý

---

## 5.5 AI Support Assistant

```text
Ticket Source
   -> API / Webhook
   -> Summarizer
   -> Classifier
   -> Suggested Reply Generator
   -> Human Review / Auto-route
```

### AI nằm ở đâu?
- summarize
- classify
- suggest response

### Phí AI phát sinh ở đâu?
- mỗi ticket
- mỗi reply generation

### Muốn có lãi thì làm gì?
- giá theo ticket volume
- auto-route + summary dùng model rẻ
- reply chất lượng cao mới dùng model đắt

---

# 6) Phân lớp model để có lãi

Đây là chỗ sống còn.

## Sai lầm
Dùng model mạnh nhất cho mọi request.

## Cách đúng
### Tầng 1 — Cheap model
Dùng cho:
- classify
- summarize ngắn
- rewrite đơn giản
- moderation
- tagging

### Tầng 2 — Mid model
Dùng cho:
- support reply
- email drafting
- extraction vừa
- RAG answer thông thường

### Tầng 3 — Strong model
Dùng cho:
- legal reasoning
- complex doc analysis
- multi-step agent
- high-value enterprise query

## Chốt
**Muốn lời, phải route model theo độ khó.**

---

# 7) Công thức kinh doanh để AI vẫn có lãi

## 7.1 Không bán theo cảm giác, bán theo ROI

Người dùng không trả tiền vì “có AI”.
Họ trả tiền vì:
- tiết kiệm giờ làm
- giảm headcount
- tăng conversion
- tăng tốc xử lý
- giảm sai sót

### Ví dụ
- extract chứng từ: giảm 2 nhân sự nhập liệu
- support AI: giảm 30% thời gian xử lý ticket
- CRM AI: tăng phản hồi lead trong 5 phút đầu

---

## 7.2 Đừng để doanh thu tuyến tính với token cost

Nếu doanh thu tăng 1 thì cost model tăng 1 theo, biên lợi nhuận sẽ xấu.

### Cách tránh
- caching
- precompute embeddings
- batch processing
- rule-based trước, AI sau
- model routing
- quota per plan

---

## 7.3 Mô hình giá nên dùng

### A. Subscription + quota
Ví dụ:
- 29$/tháng: 1.000 query
- 99$/tháng: 10.000 query
- enterprise: custom

### B. Setup fee + monthly
Rất hợp cho:
- internal docs RAG
- legal/compliance search
- enterprise deployment

### C. Pay-per-use
Hợp cho:
- document extraction
- OCR + AI processing
- batch jobs

### D. AI là add-on premium
Hợp cho SaaS có sẵn.
Ví dụ:
- CRM 30$/seat
- AI assistant thêm 20$/seat

---

# 8) Những chỗ nên tối ưu để giữ biên lợi nhuận

## A. Cache output
Nếu câu hỏi lặp lại, không cần gọi model lại.

## B. Cache embedding
Văn bản giống nhau không nên embed lại.

## C. Rút ngắn prompt
Prompt càng dài càng đắt.

## D. Tách pipeline rule-based + AI
- regex / heuristic / rules trước
- AI chỉ xử lý phần khó

## E. Background hóa việc nặng
Request đồng bộ tốn cả cost lẫn UX.

## F. Logging cost theo user/team
Để biết khách nào đang đốt tiền.

---

# 9) Kiến trúc lợi nhuận theo loại sản phẩm

## Loại 1: AI là tính năng phụ trong SaaS

### Ví dụ
- CRM có AI email
- support tool có AI reply
- booking app có AI chăm sóc khách

### Ưu điểm
- biên lợi nhuận dễ hơn
- AI chỉ là phần add-on
- core product vẫn giữ khách

### Đây là mô hình ngon nhất
Vì đại ca không sống chết chỉ nhờ token.

---

## Loại 2: AI là lõi sản phẩm

### Ví dụ
- AI doc extraction
- AI internal knowledge search
- legal AI search

### Ưu điểm
- value proposition rõ
- dễ bán B2B

### Nhược điểm
- phải kiểm soát cost chặt
- chất lượng AI là thứ sống còn

---

# 10) Những nguyên tắc kiến trúc nếu muốn build lâu dài

## Phải có
- abstraction cho provider
- usage metering
- caching layer
- audit/logging
- retry/fallback
- prompt versioning
- background worker
- vector storage nếu dùng RAG
- auth và role-based access

## Không nên làm
- để client gọi model trực tiếp
- hardcode prompt khắp nơi
- không log token usage
- dùng model đắt cho mọi case
- không tách business rules khỏi AI rules

---

# 11) Mô hình HLD ngắn gọn nên dùng nhất

## Với hầu hết app .NET có AI

```text
Client
 -> ASP.NET Core API
 -> Business Services
 -> AI Orchestrator
 -> LLM / Embedding Provider
 -> SQL + Redis + Vector DB
 -> Worker / Queue cho task nặng
```

## AI nằm ở đâu?
- **AI Orchestrator layer**
- và **worker layer** cho batch/heavy processing

## Tiền AI phát sinh ở đâu?
- inference
- embeddings
- OCR + parsing
- reprocessing batch jobs

## Làm sao có lãi?
- route model theo độ khó
- giới hạn quota
- dùng AI như premium feature hoặc B2B solution
- cache + precompute + rule-based trước
- pricing theo ROI, không pricing theo cảm tính

---

# Kết luận cuối

## Một app .NET tích hợp AI tốt không phải là app “có model”.
Nó phải là app có:
- backend rõ ràng
- AI orchestration sạch
- cost control
- caching
- quota
- billing
- business value rõ

## Câu chốt cuối cùng

**AI nên nằm trong service layer của hệ thống .NET.**

**Muốn có lãi thì phải coi AI là một phần của business engine, không phải cái hố đốt token.**
