# Báo cáo khuyến nghị kiến trúc cho ứng dụng .NET tích hợp AI

## 1. Mục tiêu

Tài liệu này tập trung duy nhất vào 5 khuyến nghị kiến trúc có tính quyết định khi xây dựng ứng dụng .NET sử dụng AI ở môi trường thực tế:

1. Không để client gọi trực tiếp AI provider.
2. Đặt AI tại backend orchestration layer.
3. Tách riêng inference, embeddings, retrieval và tools.
4. Bắt buộc có usage metering và cost tracking.
5. Thiết kế business model ngay từ đầu để tránh tăng trưởng lỗ.

Mục tiêu của tài liệu là cung cấp một khung kiến trúc ngắn gọn, có thể dùng để thống nhất định hướng kỹ thuật và mô hình vận hành ngay từ giai đoạn đầu.

---

## 2. Sơ đồ kiến trúc khuyến nghị

```text
+----------------------+
|   Web / Mobile App   |
+----------+-----------+
           |
           v
+----------------------+
|   ASP.NET Core API   |
| auth / quota / audit |
+----------+-----------+
           |
           v
+-------------------------------+
|   AI Orchestration Layer      |
| prompt / routing / policy     |
| fallback / parser / guardrail |
+-----+-----------+-------------+
      |           |            |
      v           v            v
+-----------+ +--------+ +------------+
| Inference | | Embed  | | Retrieval  |
| Service   | | Service| | Service    |
+-----+-----+ +---+----+ +------+-----+
      |           |             |
      v           v             v
+-----------+ +--------+ +-------------+
| LLM Model | | Vector | | Tool Layer  |
| Provider  | | Store  | | DB/API/ERP  |
+-----------+ +--------+ +-------------+

           +--------------------------+
           | Usage Metering / Billing |
           | Cost Tracking / Analytics|
           +--------------------------+
```

---

## 3. Khuyến nghị số 1: Không để client gọi trực tiếp AI provider

### 3.1. Kết luận

Client không nên gọi trực tiếp OpenAI, Azure OpenAI hoặc bất kỳ AI provider nào.

### 3.2. Lý do kiến trúc

| Vấn đề | Hệ quả nếu client gọi trực tiếp provider |
|---|---|
| Lộ API key hoặc credentials | Rủi ro bảo mật nghiêm trọng |
| Không kiểm soát được usage | Không giới hạn được lưu lượng theo user hoặc tenant |
| Không theo dõi được chi phí đầy đủ | Khó đo cost theo khách hàng, tính năng hoặc luồng nghiệp vụ |
| Không áp được business rules | Không thể chặn prompt, file, query hoặc workflow không hợp lệ |
| Khó thay đổi provider | Client bị khóa chặt vào một hãng AI |
| Khó kiểm soát chất lượng | Không có lớp parse, guardrail, fallback và post-processing |

### 3.3. Khuyến nghị triển khai

| Nội dung | Khuyến nghị |
|---|---|
| Điểm gọi AI duy nhất | Backend API hoặc backend worker |
| Quản lý credentials | Chỉ lưu tại server-side secret store |
| Kiểm soát truy cập | Bắt buộc thông qua auth, quota, RBAC |
| Logging | Ghi log đầy đủ tại backend |
| Provider abstraction | Bọc provider bằng interface nội bộ |

### 3.4. Kết luận quản trị

Việc để client gọi trực tiếp AI provider là lựa chọn phù hợp cho bản demo, nhưng không phù hợp cho sản phẩm vận hành thực tế.

---

## 4. Khuyến nghị số 2: Đặt AI tại backend orchestration layer

### 4.1. Kết luận

AI không nên chỉ là một lệnh gọi model rời rạc. AI cần được đặt trong một lớp orchestration chuyên trách ở backend.

### 4.2. Vai trò của backend orchestration layer

| Chức năng | Ý nghĩa |
|---|---|
| Prompt assembly | Xây dựng prompt theo ngữ cảnh nghiệp vụ |
| Model routing | Chọn model theo độ khó và chi phí |
| Retrieval orchestration | Lấy context phù hợp trước khi gọi model |
| Tool orchestration | Điều phối các lời gọi DB, API, ERP, workflow |
| Output normalization | Chuẩn hóa kết quả về dạng hệ thống có thể dùng |
| Guardrails | Kiểm soát nội dung vào/ra |
| Retry / fallback | Tăng độ ổn định khi provider lỗi hoặc timeout |
| Policy enforcement | Áp dụng quy định theo user, vai trò, tenant |

### 4.3. Sơ đồ vị trí orchestration layer

```text
Client
  -> ASP.NET Core API
  -> AI Orchestration Layer
     -> Retrieval Service
     -> Inference Service
     -> Tool Layer
     -> Output Parser
```

### 4.4. Kết luận quản trị

Nếu không có orchestration layer, sản phẩm AI sẽ nhanh chóng trở thành tập hợp các lời gọi model rời rạc, khó kiểm soát, khó mở rộng và khó vận hành.

---

## 5. Khuyến nghị số 3: Tách riêng inference, embeddings, retrieval và tools

### 5.1. Kết luận

Bốn năng lực này không nên bị trộn lẫn trong một service duy nhất.

### 5.2. Phân tách chức năng

| Thành phần | Chức năng | Không nên làm |
|---|---|---|
| Inference | Sinh câu trả lời, tóm tắt, phân loại, trích xuất | Không gánh retrieval hoặc tool execution trực tiếp |
| Embeddings | Mã hóa tài liệu và truy vấn thành vector | Không kiêm nhiệm logic trả lời |
| Retrieval | Tìm ngữ cảnh liên quan từ vector store hoặc search index | Không trộn với prompt generation |
| Tools | Thực thi hành động qua DB, API, ERP, service nội bộ | Không gộp chung vào inference logic |

### 5.3. Lý do phải tách

| Lý do | Tác động tích cực |
|---|---|
| Dễ tối ưu độc lập | Mỗi lớp có thể tối ưu riêng theo latency và cost |
| Dễ thay thế công nghệ | Có thể đổi vector DB hoặc model mà không phá toàn hệ thống |
| Dễ đo chi phí | Biết rõ cost nằm ở inference, embeddings hay retrieval |
| Dễ kiểm thử | Có thể test từng thành phần theo vai trò |
| Dễ mở rộng | Scale theo đúng điểm nghẽn thực tế |

### 5.4. Sơ đồ phân tách thành phần

```text
AI Orchestration Layer
   |
   +--> Inference Service  ------> LLM Provider
   |
   +--> Embedding Service  ------> Embedding Model / API
   |
   +--> Retrieval Service  ------> Vector DB / Search Index
   |
   +--> Tool Layer         ------> SQL / ERP / External APIs
```

### 5.5. Kết luận quản trị

Nếu gộp bốn lớp này làm một, hệ thống sẽ khó bảo trì, khó đo chi phí và khó thích nghi khi bài toán mở rộng.

---

## 6. Khuyến nghị số 4: Bắt buộc có usage metering và cost tracking

### 6.1. Kết luận

Mọi hệ thống AI dùng trong sản xuất đều phải đo usage và cost ở cấp user, tenant, tính năng và workflow.

### 6.2. Cần đo những gì

| Nhóm chỉ số | Nội dung tối thiểu cần theo dõi |
|---|---|
| Usage | số request, số query, số file, số batch jobs |
| Token | input tokens, output tokens, embedding tokens |
| Cost | cost theo request, user, team, tenant, feature |
| Vận hành | latency, timeout, retry, fail rate |
| Hiệu quả | cost per successful outcome, cost per customer |

### 6.3. Kiến trúc đo usage và cost

```text
Request
  -> API Gateway / ASP.NET Core API
  -> AI Orchestration Layer
  -> AI Services
  -> Metering Pipeline
  -> Usage Store / Cost Dashboard / Billing Engine
```

### 6.4. Lý do bắt buộc

| Nếu không có metering | Hệ quả |
|---|---|
| Không biết ai đang dùng nhiều | Không kiểm soát được abuse hoặc usage bất thường |
| Không biết tính năng nào đốt tiền | Không thể tối ưu đúng chỗ |
| Không biết khách hàng nào lỗ | Dễ tăng trưởng doanh thu nhưng âm margin |
| Không đo cost per workflow | Không thể thiết kế pricing chính xác |

### 6.5. Khuyến nghị triển khai

| Nội dung | Khuyến nghị |
|---|---|
| Mức đo tối thiểu | Theo request, feature, tenant |
| Mức đo tốt hơn | Theo từng workflow và từng provider |
| Lưu trữ | Usage store riêng hoặc schema riêng trong hệ thống phân tích |
| Dashboard | Bắt buộc có dashboard usage và dashboard cost |
| Billing hooks | Chuẩn bị từ đầu để kết nối vào pricing và invoice |

---

## 7. Khuyến nghị số 5: Thiết kế business model ngay từ đầu để tránh tăng trưởng lỗ

### 7.1. Kết luận

Với sản phẩm AI, bài toán kiến trúc và bài toán kinh doanh phải được thiết kế đồng thời. Nếu chỉ xây tính năng trước rồi mới nghĩ đến pricing, rủi ro tăng trưởng lỗ là rất cao.

### 7.2. Nguyên nhân của tăng trưởng lỗ

| Nguyên nhân | Hệ quả |
|---|---|
| Dùng model mạnh cho mọi request | Chi phí inference tăng quá nhanh |
| Không có quota | Khách hàng dùng vượt xa giá trị thu được |
| Không cache | Gọi lại model cho cùng một nội dung |
| Không đo cost theo tính năng | Không biết đâu là phần lỗ |
| Pricing không gắn với usage | Doanh thu không theo kịp chi phí |

### 7.3. Mô hình kinh doanh nên xem xét ngay từ đầu

| Mô hình | Phù hợp với | Ghi chú |
|---|---|---|
| Subscription + quota | AI search, chatbot, support AI | Dễ dự báo doanh thu |
| Setup fee + recurring fee | Enterprise knowledge base, legal AI | Phù hợp B2B triển khai riêng |
| Pay-per-use | Document extraction, OCR, batch jobs | Gắn trực tiếp vào chi phí đơn vị |
| Add-on premium | CRM AI, booking AI, support AI | Phù hợp khi AI là tính năng tăng giá trị |

### 7.4. Kiến trúc phải phục vụ business model

| Quyết định kinh doanh | Yêu cầu kiến trúc tương ứng |
|---|---|
| Bán theo quota | Phải có usage meter và quota engine |
| Bán theo tài liệu | Phải đo theo file, page, job |
| Bán theo seat + AI add-on | Phải tách usage AI khỏi usage lõi |
| Bán enterprise contract | Phải có metering, audit, SLA monitoring |

### 7.5. Kết luận quản trị

Một sản phẩm AI chỉ nên triển khai ở quy mô lớn khi đã chứng minh được rằng mô hình doanh thu tăng nhanh hơn hoặc ít nhất không chậm hơn mô hình chi phí.

---

## 8. Bảng tóm tắt khuyến nghị kiến trúc

| Khuyến nghị | Mục tiêu | Nếu không làm | Mức độ ưu tiên |
|---|---|---|---|
| Không để client gọi trực tiếp AI provider | Bảo mật, kiểm soát, đo usage | Lộ key, mất kiểm soát, khó tối ưu | Bắt buộc |
| Đặt AI tại backend orchestration layer | Điều phối tập trung, ổn định | AI logic phân tán, khó bảo trì | Bắt buộc |
| Tách inference, embeddings, retrieval, tools | Tối ưu độc lập, dễ mở rộng | Hệ thống rối, khó scale, khó đo cost | Bắt buộc |
| Có usage metering và cost tracking | Kiểm soát tài chính và vận hành | Tăng trưởng mù, dễ âm margin | Bắt buộc |
| Thiết kế business model từ đầu | Giữ biên lợi nhuận dương | Doanh thu tăng nhưng lỗ tăng | Bắt buộc |

---

## 9. Khuyến nghị triển khai thực tế cho hệ .NET

| Lớp | Công nghệ / hướng triển khai phù hợp |
|---|---|
| API Layer | ASP.NET Core API |
| Orchestration Layer | Application service riêng hoặc AI orchestration module riêng |
| Inference Layer | Provider adapter, ví dụ OpenAI/Azure wrapper |
| Embedding Layer | Service riêng cho indexing và query embedding |
| Retrieval Layer | pgvector, Qdrant, Pinecone, Weaviate hoặc search engine phù hợp |
| Tool Layer | Internal service adapters, DB adapters, ERP/API connectors |
| Metering | Middleware + event logging + usage store |
| Cost Tracking | Dashboard nội bộ, BI layer hoặc billing subsystem |
| Background Jobs | Hangfire, Quartz, worker service, queue-based processing |

---

## 10. Kết luận cuối

Năm nguyên tắc trong tài liệu này không phải là các khuyến nghị tùy chọn. Đây là các điều kiện nền tảng để một ứng dụng .NET sử dụng AI có thể:
- vận hành an toàn,
- mở rộng ổn định,
- đo lường được hiệu quả,
- và duy trì được lợi nhuận.

Nếu triển khai AI mà không có backend orchestration, không tách lớp chức năng, không đo usage/cost và không thiết kế pricing song song với kiến trúc, doanh nghiệp rất dễ đạt tăng trưởng người dùng nhưng không đạt hiệu quả tài chính.
