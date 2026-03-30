# Báo cáo kiến trúc tổng thể cho ứng dụng .NET tích hợp AI

## 1. Mục tiêu tài liệu

Tài liệu này trình bày ở mức **high-level design** cho các ứng dụng .NET có tích hợp AI, nhằm phục vụ định hướng kiến trúc và đánh giá mô hình kinh doanh ở cấp quản lý.

Tài liệu tập trung trả lời bốn câu hỏi:
1. Ứng dụng .NET có AI nên được tổ chức theo mô hình kiến trúc nào.
2. AI nằm ở đâu trong hệ thống.
3. Chi phí AI phát sinh tại những lớp nào.
4. Phương án kinh doanh nào giúp sản phẩm duy trì biên lợi nhuận dương.

---

## 2. Phân loại ứng dụng

### 2.1. Nhóm A — Ứng dụng truyền thống có bổ sung AI

Đây là nhóm sản phẩm mà **nghiệp vụ cốt lõi không phụ thuộc hoàn toàn vào AI**. AI được đưa vào để cải thiện hiệu suất hoặc tăng giá trị sử dụng.

Ví dụ:
- CRM có chức năng soạn email bằng AI
- hệ thống chăm sóc khách hàng có chức năng gợi ý trả lời
- nền tảng booking có chức năng nhắc lịch và chăm sóc lại khách hàng bằng AI

**Đặc điểm:**
- hệ thống vẫn vận hành được nếu lớp AI tạm thời không khả dụng
- AI đóng vai trò gia tăng năng suất và trải nghiệm

---

### 2.2. Nhóm B — Ứng dụng AI-native

Đây là nhóm sản phẩm mà **AI là lõi của giá trị sản phẩm**. Nếu bỏ AI, sản phẩm gần như mất đi chức năng trung tâm.

Ví dụ:
- hệ thống hỏi đáp tài liệu nội bộ
- nền tảng tìm kiếm và phân tích hồ sơ pháp lý
- hệ thống trích xuất dữ liệu từ chứng từ/hợp đồng bằng AI
- trợ lý tri thức nội bộ cho doanh nghiệp

**Đặc điểm:**
- AI không phải tính năng phụ, mà là thành phần cốt lõi
- kiến trúc phải được thiết kế xoay quanh orchestration, retrieval, model routing và cost control

---

## 3. Kiến trúc tổng thể đề xuất

### 3.1. Mô hình cho ứng dụng truyền thống có bổ sung AI

```text
+----------------------+
| Web / Mobile / Admin |
+----------+-----------+
           |
           v
+----------------------+
|   ASP.NET Core API   |
+----------+-----------+
           |
   +-------+--------+
   |                |
   v                v
+---------+    +-----------+
| Business |    | AI Module |
| Services |    |           |
+----+----+    +-----+-----+
     |               |
     v               v
+---------+    +-----------+
| SQL/PG  |    | LLM / AI  |
+---------+    +-----------+
```

**Nhận định:**
- Business Services vẫn là trung tâm điều phối chính.
- AI Module là lớp hỗ trợ, có thể bật/tắt hoặc thay đổi mà không làm phá vỡ lõi nghiệp vụ.

---

### 3.2. Mô hình cho ứng dụng AI-native

```text
+--------------------------+
|      Web / Chat UI       |
+------------+-------------+
             |
             v
+--------------------------+
|     ASP.NET Core API     |
+------------+-------------+
             |
             v
+--------------------------+
|      AI Orchestrator     |
| prompt / routing / tools |
| memory / retry / parser  |
+-----+---------+----------+
      |         |         |
      v         v         v
+-----------+ +---------+ +----------------+
| Vector DB | | Cache   | | Tool Executors |
+-----+-----+ +----+----+ +--------+-------+
      |              |             |
      v              |             v
+-----------+        |      +-------------+
| Embedding |        |      | SQL / PG    |
| Service   |        |      +-------------+
+-----+-----+        |
      |              v
      v        +-----------+
+-------------------------+
|      LLM Provider       |
| OpenAI / Azure / etc.   |
+-------------------------+
```

**Nhận định:**
- AI Orchestrator là trung tâm điều phối cốt lõi.
- API chỉ là lớp giao tiếp.
- Vector DB, Embedding Service, Tool Executors và LLM Provider cùng tạo nên năng lực sản phẩm.

---

## 4. Vị trí của AI trong hệ thống

Trong kiến trúc chuẩn, AI không nên xuất hiện trực tiếp ở lớp giao diện người dùng. AI cần được đặt trong các lớp backend có khả năng kiểm soát bảo mật, chi phí và chất lượng đầu ra.

### AI nên nằm tại các lớp sau

#### 4.1. AI Orchestrator Layer
Lớp này chịu trách nhiệm:
- xây dựng prompt
- chọn model phù hợp
- điều phối retrieval
- gọi tools hoặc services phụ trợ
- chuẩn hóa và kiểm soát đầu ra

#### 4.2. Embedding Layer
Dùng để:
- mã hóa tài liệu
- mã hóa truy vấn
- hỗ trợ semantic search và RAG

#### 4.3. Retrieval Layer
Dùng để:
- tìm kiếm dữ liệu liên quan
- xây dựng context
- giảm hallucination

#### 4.4. Inference Layer
Dùng để:
- sinh phản hồi
- tóm tắt
- phân loại
- trích xuất dữ liệu
- thực hiện reasoning khi cần

**Kết luận:**
Trong ứng dụng AI-native, AI là thành phần đa lớp, không chỉ đơn thuần là một lệnh gọi model.

---

## 5. Kiến trúc tham chiếu theo từng nhóm ứng dụng

### 5.1. Ứng dụng hỏi đáp tài liệu nội bộ (RAG)

#### Luồng indexing
```text
Upload Documents
   -> Chunking Pipeline
   -> Embedding Service
   -> Vector DB
```

#### Luồng truy vấn
```text
User Question
   -> API
   -> Retriever
   -> Context Builder
   -> LLM Provider
   -> Answer with Citation
```

#### Vai trò của AI
- tạo embedding
- tìm context
- tổng hợp câu trả lời

#### Điểm phát sinh chi phí
- embedding khi nạp dữ liệu
- completion khi trả lời câu hỏi

---

### 5.2. Ứng dụng trích xuất dữ liệu từ chứng từ/hợp đồng

```text
Upload File
   -> OCR / Text Extraction
   -> AI Extraction Engine
   -> Validation Rules
   -> Structured Output
   -> Database / Export
```

#### Vai trò của AI
- đọc dữ liệu phi cấu trúc
- chuyển sang dữ liệu có cấu trúc
- chuẩn hóa dữ liệu đầu ra

#### Điểm phát sinh chi phí
- OCR/vision
- extraction model

---

### 5.3. Ứng dụng CRM tích hợp AI

```text
CRM UI
   -> ASP.NET Core API
   -> Business Rules
   -> AI Assistant
   -> LLM Provider
   -> CRM Database
```

#### Vai trò của AI
- soạn email
- tóm tắt lịch sử tương tác
- phân loại lead
- gợi ý hành động tiếp theo

#### Điểm phát sinh chi phí
- content generation
- summarization
- classification

---

### 5.4. Ứng dụng hỗ trợ chăm sóc khách hàng

```text
Tickets / Messages
   -> API / Webhook
   -> AI Support Engine
   -> Classification / Summary / Suggested Reply
   -> Human Agent
```

#### Vai trò của AI
- phân loại ticket
- tóm tắt nội dung
- gợi ý phản hồi

#### Điểm phát sinh chi phí
- token xử lý từng ticket
- token sinh câu trả lời đề xuất

---

## 6. Các thành phần bắt buộc trong kiến trúc vận hành

Để ứng dụng AI có thể vận hành thực tế ở quy mô doanh nghiệp, kiến trúc cần có tối thiểu các lớp sau:

### 6.1. Authentication / Authorization
- bảo vệ dữ liệu
- phân quyền theo vai trò và phòng ban

### 6.2. Usage Metering
- đo số request
- đo token usage
- đo chi phí theo user/team/customer

### 6.3. Cache Layer
- giảm chi phí lặp lại
- tăng tốc phản hồi

### 6.4. Queue / Worker Layer
- xử lý batch jobs
- indexing nền
- OCR hàng loạt
- tác vụ tốn thời gian

### 6.5. Prompt Management
- kiểm soát phiên bản prompt
- giảm rủi ro khi thay đổi logic AI

### 6.6. Observability
- theo dõi latency
- fail rate
- provider errors
- chi phí theo luồng nghiệp vụ

---

## 7. Điểm phát sinh chi phí AI

Chi phí AI thường phát sinh tại bốn lớp chính:

### 7.1. Completion / Inference
Áp dụng cho:
- chat
- tóm tắt
- extraction
- classification
- agent reasoning

### 7.2. Embeddings
Áp dụng cho:
- indexing tài liệu
- semantic search
- retrieval pipeline

### 7.3. OCR / Vision
Áp dụng cho:
- hóa đơn ảnh
- hợp đồng scan
- ảnh chụp chứng từ

### 7.4. Background Batch Processing
Áp dụng cho:
- re-embedding
- indexing hàng loạt
- phân tích tập tài liệu lớn

**Hàm ý quản trị:**
Nếu không xác định rõ chi phí nằm ở lớp nào, doanh nghiệp rất dễ triển khai sản phẩm AI nhưng không kiểm soát được biên lợi nhuận.

---

## 8. Mô hình kinh doanh để đảm bảo có lãi

### 8.1. Subscription + Quota
Áp dụng tốt cho:
- chatbot nội bộ
- AI search
- support assistant
- CRM assistant

#### Ví dụ
- gói cơ bản: giới hạn số query/tháng
- gói cao hơn: tăng quota, mở model tốt hơn
- gói doanh nghiệp: custom usage và SLA

---

### 8.2. Setup Fee + Recurring Fee
Áp dụng tốt cho:
- internal knowledge base
- enterprise RAG
- legal/compliance AI

#### Lý do
- giai đoạn indexing ban đầu có chi phí riêng
- doanh nghiệp có xu hướng chấp nhận phí triển khai nếu giá trị rõ ràng

---

### 8.3. Pay-per-Document / Pay-per-Batch
Áp dụng tốt cho:
- document extraction
- OCR + AI parsing
- khối lượng chứng từ lớn

#### Lý do
- dễ gắn trực tiếp vào chi phí xử lý đơn vị tài liệu
- dễ đo lường ROI

---

### 8.4. AI Add-on Premium
Áp dụng tốt cho:
- CRM
- support tools
- booking systems
- internal SaaS tools

#### Lý do
- AI không phải lõi sản phẩm
- AI có thể được bán như tính năng gia tăng giá trị

---

## 9. Biện pháp kiểm soát chi phí để duy trì margin

### 9.1. Model Tiering
Không sử dụng cùng một model cho mọi loại tác vụ.

#### Gợi ý
- model chi phí thấp: tagging, classification, short summary
- model tầm trung: RAG answer, support reply, content drafting
- model mạnh: legal reasoning, extraction phức tạp, multi-step agent

---

### 9.2. Caching
Áp dụng cho:
- câu hỏi lặp lại
- kết quả search phổ biến
- embedding trùng lặp

---

### 9.3. Rule-based trước, AI sau
Áp dụng cho:
- parsing có cấu trúc
- kiểm tra điều kiện rõ ràng
- tiền xử lý dữ liệu

Mục tiêu là chỉ dùng AI cho phần thật sự cần trí tuệ ngôn ngữ.

---

### 9.4. Background Processing
Không xử lý tất cả trong request đồng bộ.
Nên đẩy các tác vụ nặng sang worker để giảm tải và kiểm soát chi phí hiệu quả hơn.

---

### 9.5. Cost Tracking theo khách hàng
Mỗi tổ chức/khách hàng nên có:
- usage dashboard
- quota control
- cảnh báo vượt ngưỡng

Điều này cần thiết để tránh tăng trưởng doanh thu nhưng không tăng lợi nhuận.

---

## 10. Kết luận và khuyến nghị

### 10.1. Kết luận

- Với **ứng dụng truyền thống có tích hợp AI**, AI nên được triển khai dưới dạng module gia tăng giá trị.
- Với **ứng dụng AI-native**, AI phải được xem là lõi của kiến trúc, không thể thiết kế như một plugin phụ.
- .NET phù hợp để xây dựng lớp API, orchestration, background processing, logging, metering, security và tích hợp nghiệp vụ xung quanh AI.

### 10.2. Khuyến nghị kiến trúc

1. Không để client gọi trực tiếp AI provider.
2. Đặt AI tại backend orchestration layer.
3. Tách riêng inference, embeddings, retrieval và tools.
4. Bắt buộc có usage metering và cost tracking.
5. Thiết kế business model ngay từ đầu để tránh tăng trưởng lỗ.

### 10.3. Khuyến nghị kinh doanh

1. Chỉ triển khai vào những nghiệp vụ có ROI rõ ràng.
2. Ưu tiên mô hình subscription + quota hoặc setup fee + recurring fee.
3. Dùng AI như premium feature khi lõi sản phẩm không phụ thuộc vào AI.
4. Với AI-native products, cần kiểm soát chặt token cost, embedding cost và batch processing cost.

---

## 11. Kết luận cuối cùng

Một ứng dụng .NET tích hợp AI thành công không chỉ cần mô hình tốt, mà cần một kiến trúc đủ trưởng thành để:
- kiểm soát chất lượng đầu ra,
- kiểm soát chi phí,
- kiểm soát quyền truy cập,
- và chuyển đổi năng lực AI thành giá trị kinh doanh thực tế.

**Nếu AI là lõi sản phẩm, kiến trúc phải xoay quanh AI. Nếu AI chỉ là tính năng phụ, kiến trúc phải bảo đảm lõi nghiệp vụ không phụ thuộc vào nó.**
