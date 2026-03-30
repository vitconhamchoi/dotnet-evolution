# Báo cáo kiến trúc tổng thể cho ứng dụng .NET tích hợp AI

## 1. Mục tiêu tài liệu

Tài liệu này trình bày kiến trúc tổng thể cho các ứng dụng .NET tích hợp AI theo định dạng ngắn gọn, dễ theo dõi và phù hợp với mục đích báo cáo quản lý.

Tài liệu tập trung vào bốn nội dung:
- phân loại mô hình ứng dụng
- xác định vị trí của AI trong kiến trúc
- xác định các điểm phát sinh chi phí
- xác định phương án kinh doanh để duy trì lợi nhuận

---

## 2. Mô hình kiến trúc tổng quát

### 2.1. Ứng dụng truyền thống có bổ sung AI

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

### 2.2. Ứng dụng AI-native

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

---

## 3. Bảng phân loại mô hình ứng dụng

| Tiêu chí | Ứng dụng truyền thống có AI | Ứng dụng AI-native |
|---|---|---|
| Bản chất sản phẩm | Nghiệp vụ truyền thống là lõi | AI là lõi của giá trị sản phẩm |
| Vai trò của AI | Tính năng bổ sung | Thành phần trung tâm |
| Nếu AI ngừng hoạt động | Hệ thống vẫn có thể vận hành phần lớn nghiệp vụ | Giá trị sản phẩm suy giảm nghiêm trọng |
| Ví dụ | CRM, support tool, booking system | RAG platform, legal AI search, document extraction |
| Mục tiêu triển khai | Tăng năng suất, tăng giá trị sử dụng | Xây dựng sản phẩm phụ thuộc trực tiếp vào AI |
| Mức độ phụ thuộc AI | Trung bình | Rất cao |

---

## 4. Bảng vị trí của AI trong kiến trúc

| Lớp kiến trúc | Vai trò chính | AI có nằm tại đây không | Ghi chú |
|---|---|---|---|
| UI / Client | Nhập liệu, hiển thị kết quả, stream phản hồi | Không nên đặt AI lõi tại đây | Không nên gọi trực tiếp model từ client |
| ASP.NET Core API | Nhận request, auth, validate, trả response | Không phải nơi xử lý AI lõi | Là lớp giao tiếp và kiểm soát |
| Business Services | Xử lý quy tắc nghiệp vụ | Có thể có logic quyết định việc gọi AI | Quyết định khi nào AI được phép chạy |
| AI Orchestrator | Prompting, routing, retrieval, parsing | Có | Đây là lớp AI cốt lõi |
| Embedding Service | Tạo vector cho văn bản và truy vấn | Có | Phục vụ semantic search và RAG |
| Vector DB | Lưu vector và metadata | Gián tiếp | Là hạ tầng phục vụ AI |
| Tool Executors | Gọi DB, ERP, API ngoài, workflow | Có | Quan trọng với agentic workflow |
| SQL / PostgreSQL | Lưu dữ liệu nghiệp vụ, log, usage | Không trực tiếp | Hỗ trợ quản trị và kiểm soát |
| Cache | Tăng tốc, giảm chi phí | Gián tiếp | Giảm số lần gọi model |
| Worker / Queue | Xử lý tác vụ nền, batch jobs | Có thể có | Quan trọng cho indexing và xử lý hàng loạt |

---

## 5. Bảng kiến trúc tham chiếu theo loại ứng dụng

| Loại ứng dụng | Mô hình kiến trúc chính | AI đảm nhiệm | Thành phần hạ tầng quan trọng |
|---|---|---|---|
| Hỏi đáp tài liệu nội bộ | API -> Retriever -> Context Builder -> LLM | Embedding, retrieval, answer synthesis | Vector DB, cache, worker |
| Trích xuất chứng từ/hợp đồng | Upload -> OCR -> AI Extraction -> Validation -> Output | OCR hậu xử lý, extraction, chuẩn hóa dữ liệu | File storage, queue, structured storage |
| CRM tích hợp AI | API -> Business Rules -> AI Assistant -> LLM | Soạn email, tóm tắt lịch sử, phân loại lead | SQL, auth, usage metering |
| Hỗ trợ chăm sóc khách hàng | Webhook/API -> AI Support Engine -> Agent UI | Summary, classification, suggested reply | Queue, cache, logging |
| Trợ lý tri thức nội bộ | API -> Orchestrator -> Tools/Retrieval -> LLM | Multi-step reasoning, search, retrieval | Vector DB, tool layer, RBAC |

---

## 6. Bảng thành phần kiến trúc bắt buộc

| Thành phần | Mục đích | Mức độ cần thiết |
|---|---|---|
| Authentication / Authorization | Bảo vệ dữ liệu, phân quyền theo vai trò | Bắt buộc |
| Usage Metering | Đo usage và chi phí theo user/team | Bắt buộc |
| AI Orchestrator | Điều phối prompt, model, retrieval, tool calling | Bắt buộc với AI-native app |
| Cache Layer | Giảm độ trễ và giảm chi phí AI | Rất nên có |
| Queue / Worker Layer | Xử lý indexing, OCR, batch jobs | Bắt buộc với workload lớn |
| Prompt Management | Quản lý phiên bản logic AI | Rất nên có |
| Observability | Theo dõi latency, fail rate, cost | Bắt buộc ở môi trường sản xuất |
| Vector Database | Hỗ trợ RAG và semantic search | Bắt buộc nếu dùng retrieval |
| Tool Execution Layer | Kết nối AI với hệ thống nghiệp vụ | Cần thiết với workflow phức tạp |

---

## 7. Bảng điểm phát sinh chi phí AI

| Lớp chi phí | Nội dung chi phí | Ứng dụng điển hình | Mức tác động chi phí |
|---|---|---|---|
| Completion / Inference | Chat, summary, extraction, reply generation | Chatbot, support AI, CRM AI | Cao |
| Embedding | Index tài liệu, query embedding | RAG, enterprise search | Trung bình đến cao |
| OCR / Vision | Đọc ảnh, scan chứng từ, PDF ảnh | Document extraction | Cao |
| Background Processing | Re-index, batch summarize, nightly jobs | RAG, document AI | Trung bình đến cao |
| Tool Invocation | Gọi workflow, external API, business service | AI agents, copilots | Phụ thuộc nghiệp vụ |

---

## 8. Bảng mô hình kinh doanh đề xuất

| Mô hình | Phù hợp với loại sản phẩm | Cách thu phí | Ưu điểm | Rủi ro |
|---|---|---|---|---|
| Subscription + Quota | Chatbot nội bộ, AI search, support AI | Thu phí tháng theo quota | Dễ dự báo doanh thu | Cần kiểm soát usage để tránh vượt cost |
| Setup Fee + Recurring Fee | Enterprise RAG, legal AI, internal knowledge system | Phí triển khai + phí duy trì | Phù hợp B2B, bù được chi phí triển khai ban đầu | Chu kỳ bán hàng dài hơn |
| Pay-per-Document | OCR, extraction, parsing tài liệu | Thu phí theo số file/số trang | Dễ gắn ROI trực tiếp | Cần tối ưu chi phí per unit |
| AI Add-on Premium | CRM, support tool, booking system | Thu thêm trên gói lõi | Tăng ARPU, dễ triển khai | AI phải đủ hữu ích để khách chịu trả thêm |
| Enterprise Contract | Nền tảng AI phục vụ nội bộ tập đoàn | Thu theo SLA, user, workload | Biên lợi nhuận tốt nếu kiểm soát cost chặt | Đòi hỏi năng lực triển khai và hỗ trợ cao |

---

## 9. Bảng biện pháp bảo vệ biên lợi nhuận

| Biện pháp | Mục tiêu | Tác động tài chính |
|---|---|---|
| Model Tiering | Dùng model phù hợp từng tác vụ | Giảm đáng kể chi phí inference |
| Caching | Tránh gọi lại cho cùng một nội dung | Giảm chi phí trực tiếp và tăng tốc độ |
| Embedding Reuse | Không tạo lại vector cho dữ liệu cũ | Giảm chi phí indexing |
| Rule-based trước, AI sau | Chỉ dùng AI cho phần thực sự cần | Tăng biên lợi nhuận |
| Background Processing | Tối ưu tải hệ thống và cost control | Giảm peak cost và cải thiện UX |
| Cost Tracking theo khách hàng | Theo dõi khách hàng nào tiêu tốn nhiều | Tránh bán lỗ ở nhóm usage cao |
| Quota theo gói | Giới hạn usage theo plan | Bảo vệ margin |

---

## 10. Bảng khuyến nghị theo cấp quản trị

| Câu hỏi quản trị | Khuyến nghị |
|---|---|
| Khi nào nên làm app AI-native? | Khi giá trị cốt lõi của sản phẩm phụ thuộc trực tiếp vào khả năng suy luận, tìm kiếm tri thức, trích xuất hoặc tự động hóa của AI |
| Khi nào chỉ nên bổ sung AI vào sản phẩm sẵn có? | Khi sản phẩm đã có lõi nghiệp vụ mạnh và AI chỉ cần đóng vai trò tăng năng suất hoặc tăng giá trị sử dụng |
| AI nên đặt ở đâu? | Tại backend orchestration layer, không đặt trực tiếp ở client |
| Cần đo gì ngay từ đầu? | Request volume, token usage, cost per customer, latency, fail rate |
| Mô hình doanh thu an toàn nhất là gì? | Subscription + quota cho sản phẩm tiêu chuẩn; setup fee + recurring fee cho B2B enterprise |
| Rủi ro lớn nhất khi triển khai AI là gì? | Doanh thu tăng nhưng chi phí AI tăng nhanh hơn, dẫn đến biên lợi nhuận âm |

---

## 11. Kết luận

| Nội dung | Kết luận |
|---|---|
| Vai trò của .NET | Phù hợp để xây dựng API, orchestration, security, background processing, integration và vận hành sản phẩm AI ở quy mô doanh nghiệp |
| Vai trò của AI trong kiến trúc | Với ứng dụng AI-native, AI là lõi; với ứng dụng truyền thống có AI, AI là lớp tăng giá trị |
| Trọng tâm kiến trúc | Không chỉ gọi model, mà phải kiểm soát orchestration, retrieval, metering, security và cost |
| Trọng tâm kinh doanh | Chỉ triển khai vào bài toán có ROI rõ ràng và có mô hình giá phù hợp với cost structure |
| Khuyến nghị cuối | Thiết kế kiến trúc và mô hình doanh thu đồng thời ngay từ đầu, không tách rời hai bài toán này |
