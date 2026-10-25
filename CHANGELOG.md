# Changelog

## 2.0.0 – chương trình .exe thật

- Viết lại bằng C# WinForms (.NET Framework 4.8 có sẵn trong Windows): bộ cài và lối tắt chạy thẳng `CleanVaiLon.exe` (khoảng 250 KB), không còn qua powershell.exe.
- Giữ nguyên 9 trang, giao diện, bộ luật "xoá được?", cách chuyển vào Thùng rác, xác nhận và nhật ký.
- Giữ định dạng `caidat.json`, `nhatky.txt`, `dichvu_da_tat.txt`: nâng cấp từ 1.x không mất cài đặt. Tác vụ "khởi động cùng Windows" cũ được chuyển sang exe.
- Kiểm tra chữ ký số không còn kết nối mạng (không kiểm tra thu hồi chứng chỉ).
- Unit test (xUnit) cho bộ luật an toàn và định dạng tệp; CI build bằng `dotnet`.
- Bản PowerShell được giữ trong `legacy/` cho tới khi bản mới được xác nhận.
- Giao diện tiếng Anh dịch đầy đủ: giải thích, nhãn trạng thái, luật an toàn, thông báo và nhật ký (dữ liệu vẫn lưu tiếng Việt, chỉ dịch khi hiển thị). Đổi ngôn ngữ là danh sách đổi theo ngay.
- Trang Khởi động chỉ hiện nút phù hợp: chọn mục đang bật thì chỉ có "tắt", chọn mục đã tắt thì chỉ có "bật lại".
- Bộ cài luôn cho chọn thư mục cài đặt.
- Mở lại cửa sổ sau khi thu nhỏ nhanh hơn khoảng gấp đôi (không sắp xếp lại bố cục khi thu nhỏ, lưới nền vẽ bằng ảnh lát).

## 1.3.0 – bản công khai đầu tiên

- 9 trang: Hiệu năng, Khởi động, Ổ đĩa, Phân tích (treemap), Ứng dụng, Bảo mật, Sao lưu, Nhật ký, Cài đặt.
- Đánh giá "xoá được?" 4 mức cho mọi tệp / thư mục; chặn hẳn mục hệ thống.
- Chỉ chuyển vào Thùng rác, không bao giờ xoá vĩnh viễn.
- Giao diện tối kiểu terminal, thanh tiêu đề và thanh cuộn tự vẽ, đổi ngôn ngữ VI/EN.
- Khởi động cùng Windows qua Task Scheduler (không hỏi quyền quản trị mỗi lần).
- Bộ cài CleanVaiLon-Setup.exe (Inno Setup) có gỡ cài đặt, bản portable .zip và SHA256SUMS, phát hành tự động bằng GitHub Actions.
- Tài liệu trong docs/: hướng dẫn sử dụng, cài đặt và gỡ bỏ, quy trình phát hành.
