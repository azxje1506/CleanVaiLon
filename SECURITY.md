# Bảo mật

## Báo lỗ hổng

Nếu bạn tìm thấy lỗi có thể làm hỏng dữ liệu hoặc bị lợi dụng (ví dụ công cụ xoá nhầm thứ không nên xoá), hãy báo **riêng** qua *Security → Report a vulnerability* trên trang repo thay vì mở Issue công khai. Mình sẽ phản hồi sớm nhất có thể.

## Công cụ làm gì trên máy bạn

- **Không kết nối Internet**, không gửi dữ liệu đi đâu.
- **Chỉ đọc** ở các trang Hiệu năng, Bảo mật, Sao lưu và khi quét.
- **Chỉ thay đổi khi bạn bấm và xác nhận**: tắt / bật mục khởi động, chuyển tệp vào Thùng rác, chạy trình gỡ của ứng dụng, chép thư mục sao lưu, tạo / xoá tác vụ "khởi động cùng Windows".
- **Không bao giờ xoá vĩnh viễn**; mục hệ thống (Windows, pagefile, gốc ổ đĩa…) bị chặn.
- Dữ liệu của công cụ (cài đặt, nhật ký) nằm ở `%LOCALAPPDATA%\CleanVaiLon`.

Danh sách đầy đủ những gì công cụ đọc và vì sao có thể bị phần mềm diệt virus cảnh báo nhầm nằm trong [README](README.md#vì-sao-phần-mềm-diệt-virus-có-thể-cảnh-báo).

## Kiểm tra tệp phát hành

Mỗi bản phát hành có `SHA256SUMS`. So mã của tệp bạn tải với `Get-FileHash <tệp>` trước khi chạy.
