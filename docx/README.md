# Tài liệu skill 9Router cho AI Agent/Codex

Folder `docx/` này gom các file Markdown skill quan trọng từ project 9Router để AI Agent/Codex có thể đọc nhanh instruction cần thiết. Các file skill được copy nguyên nội dung từ `skills/`, chỉ đổi tên để tránh trùng nhiều file `SKILL.md`.

| Tên file trong folder `docx` | File gốc | Đây là skill gì? | Agent dùng để làm gì? | Có cần chạy 9Router server/CLI không? | Khi nào nên dùng? | Khi nào không cần dùng? |
| --- | --- | --- | --- | --- | --- | --- |
| `skills_README.md` | `skills/README.md` | File tổng quan/index skill. | Giúp Agent xem danh sách skill 9Router và chọn đúng skill theo nhiệm vụ. | Không bắt buộc; đây là tài liệu định hướng. | Khi cần hiểu toàn cảnh các skill 9Router hoặc chọn skill phù hợp trước khi làm việc. | Khi đã biết chính xác skill cần dùng hoặc task không liên quan 9Router. |
| `9router_SKILL.md` | `skills/9router/SKILL.md` | Skill nền để Agent hiểu cách dùng 9Router chung. | Hướng dẫn cách làm việc tổng quát với 9Router, cấu hình, routing model và các nguyên tắc sử dụng chung. | Có thể cần, tùy task có gọi server/CLI thật hay chỉ đọc hướng dẫn. | Khi task liên quan setup, cấu hình, kiểm tra hoặc dùng 9Router ở mức nền tảng. | Khi task chỉ liên quan một năng lực chuyên biệt đã có skill riêng hoặc không dùng 9Router. |
| `9router-chat_SKILL.md` | `skills/9router-chat/SKILL.md` | Skill cho chat/code generation. | Hướng dẫn Agent dùng 9Router cho hội thoại, sinh nội dung, phân tích văn bản hoặc sinh code. | Thường cần nếu phải chạy request chat/code generation thật qua 9Router. | Khi cần gọi model chat, tạo nội dung, viết code hoặc kiểm thử endpoint chat. | Khi task là image, TTS, STT, embeddings, web search/fetch hoặc thao tác file thuần túy. |
| `9router-image_SKILL.md` | `skills/9router-image/SKILL.md` | Skill tạo ảnh. | Hướng dẫn Agent dùng 9Router cho image generation. | Thường cần nếu phải tạo ảnh thật qua server/CLI hoặc provider image. | Khi cần tạo ảnh, asset bitmap, mockup hình ảnh hoặc kiểm thử endpoint image. | Khi không có yêu cầu tạo hoặc xử lý ảnh. |
| `9router-tts_SKILL.md` | `skills/9router-tts/SKILL.md` | Skill chuyển văn bản thành giọng nói. | Hướng dẫn Agent tạo audio từ text bằng 9Router TTS. | Thường cần nếu phải sinh file âm thanh thật. | Khi cần đọc văn bản thành giọng nói hoặc kiểm thử text-to-speech. | Khi task không tạo audio từ văn bản. |
| `9router-stt_SKILL.md` | `skills/9router-stt/SKILL.md` | Skill chuyển giọng nói thành văn bản. | Hướng dẫn Agent transcribe audio/speech thành text bằng 9Router STT. | Thường cần nếu phải xử lý audio thật. | Khi cần chuyển file audio thành transcript hoặc kiểm thử speech-to-text. | Khi task không có audio đầu vào cần nhận dạng. |
| `9router-embeddings_SKILL.md` | `skills/9router-embeddings/SKILL.md` | Skill tạo vector embeddings. | Hướng dẫn Agent tạo vector từ text để phục vụ semantic search, RAG, similarity hoặc clustering. | Thường cần nếu phải gọi endpoint embeddings thật. | Khi cần tạo embeddings, so khớp ngữ nghĩa, tìm kiếm semantic hoặc xây RAG. | Khi task không cần vector hóa văn bản. |
| `9router-web-search_SKILL.md` | `skills/9router-web-search/SKILL.md` | Skill tìm kiếm web. | Hướng dẫn Agent dùng 9Router để tìm kiếm web và lấy danh sách kết quả liên quan. | Thường cần nếu phải chạy web search thật qua 9Router/tool được cấu hình. | Khi cần tìm thông tin mới, so sánh nguồn hoặc truy vấn web bằng từ khóa. | Khi dữ liệu đã có trong repo hoặc không cần internet/search. |
| `9router-web-fetch_SKILL.md` | `skills/9router-web-fetch/SKILL.md` | Skill fetch nội dung web/URL. | Hướng dẫn Agent lấy nội dung từ URL cụ thể để đọc, tóm tắt, trích xuất hoặc xử lý. | Thường cần nếu phải fetch nội dung URL thật. | Khi đã có URL cụ thể và cần lấy nội dung trang/tài liệu web. | Khi chỉ cần tìm kiếm từ khóa hoặc nội dung đã nằm trong local repo. |

## Vì sao chỉ lấy các file này?

Chỉ các file trên là Markdown skill/agent instruction trực tiếp trong `skills/` và là phần cần thiết nhất để hướng dẫn AI Agent/Codex dùng 9Router theo các năng lực chính: nền tảng chung, chat/code generation, tạo ảnh, TTS, STT, embeddings, web search và web fetch. Không copy docs/gitbook, README khác, test docs hay source code vì chúng không phải skill instruction trực tiếp trong phạm vi yêu cầu.

## Đối với project CinemaSystem_BE nên dùng skill nào?

Project `CinemaSystem_BE` là backend ASP.NET Core Web API cho hệ thống đặt vé xem phim. Vì vậy, skill nên dùng chính là:

| Mức ưu tiên | Skill nên dùng | Vì sao phù hợp với project này? | Cách dùng ở đây |
| --- | --- | --- | --- |
| Chính | `9router-chat_SKILL.md` | Phù hợp nhất cho chat, phân tích yêu cầu, sinh code, sửa lỗi, viết DTO/service/controller, giải thích kiến trúc Clean Architecture và hỗ trợ tạo test. Đây là nhu cầu gần nhất với project backend. | Khi nhờ Agent làm việc với source code, yêu cầu Agent đọc `docx/9router-chat_SKILL.md` cùng `AGENTS.md` và tài liệu nghiệp vụ trong `KhoBauG2` trước khi sửa code. |
| Nền tảng | `9router_SKILL.md` | Giúp Agent hiểu cách dùng 9Router chung trước khi dùng các skill chuyên biệt. Nên dùng kèm nếu task có gọi 9Router server/CLI hoặc cần cấu hình model/router. | Dùng làm tài liệu nền: đọc `docx/9router_SKILL.md` trước, sau đó đọc skill chuyên biệt như `docx/9router-chat_SKILL.md`. |
| Hỗ trợ khi cần tra cứu | `9router-web-search_SKILL.md` | Chỉ hữu ích khi cần tìm thông tin mới bên ngoài repo, ví dụ tài liệu ASP.NET Core, EF Core, JWT, SMTP hoặc lỗi build mới. | Chỉ dùng nếu task cần web search. Nếu thông tin đã có trong repo hoặc tài liệu `KhoBauG2`, ưu tiên tài liệu local trước. |
| Hỗ trợ khi đã có URL | `9router-web-fetch_SKILL.md` | Dùng khi đã có URL cụ thể và cần Agent lấy nội dung trang đó để tóm tắt hoặc đối chiếu. | Dùng sau web search hoặc khi người dùng đưa link tài liệu cụ thể. |
| Ít dùng cho project này | `9router-embeddings_SKILL.md` | Có thể hữu ích nếu muốn xây semantic search/RAG trên tài liệu dự án, nhưng không cần cho Sprint 1 Auth thông thường. | Chỉ dùng khi có yêu cầu tạo embeddings, tìm kiếm ngữ nghĩa hoặc RAG cho tài liệu/source. |
| Không cần trong backend hiện tại | `9router-image_SKILL.md`, `9router-tts_SKILL.md`, `9router-stt_SKILL.md` | Project này hiện là backend auth/API, không có yêu cầu tạo ảnh, chuyển văn bản thành giọng nói hoặc chuyển giọng nói thành văn bản. | Không dùng trừ khi sau này project thêm tính năng AI media/audio. |

### Cách sử dụng tại project này

1. Đặt yêu cầu cho Agent rõ ràng, ví dụ: `Hãy dùng AGENTS.md, docx/9router_SKILL.md và docx/9router-chat_SKILL.md để hỗ trợ implement Sprint 1 Auth`.
2. Với mọi thay đổi feature-level của `CinemaSystem_BE`, Agent cần ưu tiên đọc `AGENTS.md` và các tài liệu trong `KhoBauG2` trước.
3. Nếu chỉ cần sửa code backend, dùng `9router-chat_SKILL.md` là chính; không cần image/TTS/STT.
4. Nếu cần gọi 9Router thật, hãy chạy hoặc cấu hình 9Router server/CLI theo hướng dẫn trong `9router_SKILL.md`, sau đó mới dùng skill chuyên biệt.
5. Nếu không gọi 9Router thật mà chỉ dùng Codex để sửa project local, các file trong `docx/` đóng vai trò tài liệu hướng dẫn cho Agent đọc và làm theo.
