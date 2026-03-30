# High-level design cho ứng dụng .NET tích hợp AI — bản có mô hình rõ ràng

Tài liệu này sửa lại theo đúng ý đại ca:
- **phải có mô hình / sơ đồ**
- phải tách rõ:
  - **app thường có gắn AI**
  - **AI-native app**
- phải chỉ ra:
  - **AI nằm ở đâu**
  - **AI là lõi hay chỉ là module phụ**
  - **AI tốn tiền ở đâu**
  - **kiếm tiền thế nào để còn lãi**

---

# 1) Phân loại đúng ngay từ đầu

## Loại A — App thường có gắn AI
Ví dụ:
- CRM có AI viết email
- support tool có AI reply
- booking app có AI chăm sóc khách

### Bản chất
- **core business vẫn là CRM / support / booking**
- AI là tính năng tăng giá trị

---

## Loại B — AI-native app
Ví dụ:
- internal knowledge assistant
- legal search AI
- AI document extraction
- AI support copilot
- AI agent workflow app

### Bản chất
- **AI là lõi hệ thống**
- không có AI thì app gần như mất lý do tồn tại

---

# 2) Mô hình HLD cho app thường có gắn AI

## Sơ đồ

```text
+----------------------+
|      Web / Mobile    |
+----------+-----------+
           |
           v
+----------------------+
|   ASP.NET Core API   |
+----------+-----------+
           |
  +--------+---------+
  |                  |
  v                  v
+---------+     +-----------+
| Business |     | AI Module |
| Services |     | (optional)|
+----+----+     +-----+-----+
     |                |
     v                v
+---------+     +-----------+
| SQL/PG  |     | LLM API   |
+---------+     +-----------+
```

## Câu chốt
Ở mô hình này:
- **business là lõi**
- AI là **module phụ trợ**

## Ví dụ
CRM:
- lõi là lead, customer, pipeline, activity
- AI chỉ giúp:
  - viết email
  - tóm tắt call
  - phân loại lead

### Nếu AI chết thì sao?
- app vẫn sống
- chỉ mất tính năng premium

---

# 3) Mô hình HLD cho AI-native app

## Sơ đồ tổng quát

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
| memory / retries / parse |
+-----+---------+----------+
      |         | 
      |         |
      v         v
+-----------+  +----------------+
| Vector DB |  | Tool Executors  |
| / Search  |  | DB, HTTP, ERP   |
+-----+-----+  +--------+-------+
      |                 |
      v                 v
+-----------+    +-------------+
| Embeddings|    | Business DB |
|  Service  |    | SQL / PG    |
+-----+-----+    +-------------+
      |
      v
+----------------+
|  LLM Provider   |
| OpenAI/Azure/...|
+----------------+
```

## Câu chốt
Ở mô hình này:
- **AI orchestration là lõi hệ thống**
- API, DB, auth chỉ là lớp đỡ phía ngoài

## Ví dụ
Internal knowledge assistant:
- không có retrieval + model + answer synthesis
- thì app gần như không còn giá trị

---

# 4) AI nằm ở đâu trong AI-native app?

## AI nằm ở 4 chỗ chính

### 1. **LLM inference layer**
- chat completion
- reasoning
- summarize
- extract
- classify

### 2. **Embedding layer**
- vector hóa tài liệu
- query embedding
- semantic similarity

### 3. **Retrieval / memory layer**
- tìm context liên quan
- inject context vào prompt
- giữ session memory nếu cần

### 4. **Tool orchestration layer**
- gọi database
- gọi API ngoài
- gọi internal service
- agent hành động nhiều bước

## Chốt
**Trong AI-native app, AI không nằm một chỗ. Nó ăn xuyên qua orchestration, retrieval, embedding và inference.**

---

# 5) Mô hình HLD cho AI RAG app

## Sơ đồ chia 2 luồng: indexing và query

```text
                 INDEXING FLOW

+---------+     +-----------+     +-----------+     +-----------+
| Upload  | --> | Chunking  | --> | Embedding | --> | Vector DB |
|  Docs   |     | Pipeline  |     |  Service  |     |  Storage  |
+---------+     +-----------+     +-----------+     +-----------+


                  QUERY FLOW

+---------+     +-----------+     +-----------+     +-----------+
|  User   | --> | ASP.NET   | --> | Retriever | --> | Context    |
| Question|     |   API     |     |           |     | Builder    |
+---------+     +-----------+     +-----------+     +-----------+
                                                        |
                                                        v
                                                  +-----------+
                                                  | LLM Model  |
                                                  +-----------+
                                                        |
                                                        v
                                                  +-----------+
                                                  |  Answer    |
                                                  +-----------+
```

## AI là lõi ở đâu?
- embedding
- retrieval
- prompt synthesis
- answer generation

## Cost phát sinh ở đâu?
- embedding khi index
- completion khi trả lời

## Muốn có lãi?
- tính **setup fee** cho indexing ban đầu
- tính **monthly subscription** theo số user / số tài liệu
- cache query phổ biến
- chỉ re-embed khi tài liệu thay đổi

---

# 6) Mô hình HLD cho AI document extraction app

```text
+-----------+
| Upload PDF|
+-----+-----+
      |
      v
+-----------+
| OCR Layer |
+-----+-----+
      |
      v
+------------------+
| Extraction Prompt|
| / Structured AI  |
+-----+------------+
      |
      v
+------------------+
| Validation Rules |
+-----+------------+
      |
      v
+------------------+
| SQL / Export API |
+------------------+
```

## AI là lõi ở đâu?
- OCR hậu xử lý
- extraction
- normalize output

## Cost phát sinh ở đâu?
- OCR cost
- token extract cost

## Muốn có lãi?
- bán theo **volume document**
- rule-based trước, AI sau
- chỉ dùng AI ở bước khó

### Ví dụ lãi kiểu gì?
Nếu 1 doc tốn:
- OCR + model = 500đ

Thì không bán 600đ/doc.
Phải bán theo:
- giá trị tiết kiệm nhập liệu
- ví dụ 2.000–5.000đ/doc hoặc package tháng

---

# 7) Mô hình HLD cho AI copilot trong CRM / SaaS

```text
+-----------+
| CRM UI    |
+-----+-----+
      |
      v
+-----------+
| API Layer |
+-----+-----+
      |
      v
+------------------+
| Business Rules   |
| lead / customer  |
| campaign / stage |
+-----+------------+
      |
      +----------------------+
      |                      |
      v                      v
+-------------+      +---------------+
| SQL / CRM DB|      | AI Assistant  |
+-------------+      | write/score   |
                     | summarize/reply|
                     +-------+-------+
                             |
                             v
                        +-----------+
                        | LLM Model |
                        +-----------+
```

## AI là gì trong case này?
- **không phải lõi sản phẩm**
- nó là **premium booster**

## Cost phát sinh ở đâu?
- mỗi lần generate nội dung
- mỗi lần summarize

## Muốn có lãi?
- AI là **add-on premium**
- giới hạn quota theo plan
- model mạnh chỉ cho gói cao

---

# 8) Mô hình HLD cho AI support assistant

```text
+-------------+
| Ticket / Msg |
+------+------+
       |
       v
+-------------+
| API/Webhook |
+------+------+
       |
       v
+-------------------+
| AI Support Engine |
| summarize         |
| classify          |
| suggest reply     |
+------+------------+
       |
       v
+-------------+
| Human Agent |
+-------------+
```

## AI là lõi hay phụ?
- nếu chỉ gợi ý reply → phụ
- nếu toàn bộ routing, triage, auto-answer là chính → gần thành lõi

## Cost phát sinh ở đâu?
- summarize ticket
- classify
- reply suggestion

## Muốn có lãi?
- dùng model rẻ cho classify/summarize
- model vừa cho reply
- enterprise plan theo ticket volume

---

# 9) Lớp kiến trúc bắt buộc phải có trong app AI nghiêm túc

## 1. Auth / RBAC
Không thể thiếu nếu là B2B.

## 2. Usage Metering
Phải log:
- ai dùng
- dùng bao nhiêu request
- bao nhiêu token
- cost bao nhiêu

## 3. Model Router
Không thể đốt model mạnh cho mọi case.

## 4. Prompt Management
Prompt phải version được.

## 5. Cache Layer
Để giảm cost.

## 6. Worker / Queue
Để đẩy task nặng ra nền.

## 7. Observability
Phải biết:
- latency
- lỗi provider
- cost theo user/team
- fail rate

---

# 10) AI cost đốt ở đâu?

## 4 nguồn cost lớn nhất

### A. Completion tokens
- chat
- summarize
- extract
- reply generation

### B. Embedding tokens
- index tài liệu
- re-index
- query embedding

### C. OCR / vision model
- scan ảnh
- đọc PDF ảnh
- parse document phức tạp

### D. Background batch jobs
- nightly processing
- re-embed
- summarize nhiều docs

## Chốt
Muốn lời thì phải biết app mình đốt tiền ở **khâu nào**.

---

# 11) Mô hình giá để vẫn có lãi

## Cách 1 — Subscription + quota
Ví dụ:
- Basic: 500 query/tháng
- Pro: 5.000 query/tháng
- Business: custom

## Cách 2 — Setup fee + monthly
Rất hợp cho:
- internal knowledge base
- enterprise AI search
- legal/compliance AI

## Cách 3 — Pay per document / pay per batch
Hợp cho:
- extraction
- OCR
- file processing

## Cách 4 — AI add-on
Ví dụ:
- CRM gốc 49$/seat
- AI add-on 19$/seat

---

# 12) Công thức giữ biên lợi nhuận

## Phải làm 6 thứ này

### 1. Model tiering
- cheap model: classify, tag, short summary
- mid model: RAG answer, email drafting
- strong model: legal, complex extraction, agent multi-step

### 2. Cache output
Câu lặp lại thì không gọi lại model.

### 3. Cache embeddings
Không embed lại nội dung cũ.

### 4. Rule-based trước, AI sau
Nếu regex / deterministic parse xử lý được thì không gọi AI.

### 5. Background hóa việc nặng
Đỡ tốn cost sync và UX tốt hơn.

### 6. Theo dõi cost per customer
Nếu không log, rất dễ bán lỗ.

---

# 13) Kết luận đúng theo từng loại app

## Nếu là app thường có gắn AI
- AI **không phải lõi**
- business mới là lõi
- AI là add-on tăng giá trị

## Nếu là AI-native app
- AI **chính là lõi**
- retrieval, prompt, tools, model routing là tim hệ thống
- không có AI thì app gần như mất lý do tồn tại

## Chốt cuối cùng

**Đại ca nói đúng:**
- với **AI-native app**, AI phải là lõi
- HLD phải vẽ rõ mô hình
- và phải chỉ ra chỗ đốt tiền + cách kiếm lời

---

# 14) Câu chốt cuối

## HLD chuẩn cho app .NET có AI phải trả lời đủ 5 câu:
1. **AI là lõi hay tính năng phụ?**
2. **AI nằm ở lớp nào?**
3. **Dữ liệu nằm ở đâu?**
4. **Cost bị đốt ở khâu nào?**
5. **Kiếm tiền thế nào để margin vẫn dương?**

Nếu không trả lời được 5 câu đó thì HLD chưa đủ tốt.
