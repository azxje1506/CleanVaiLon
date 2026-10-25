# Đóng góp

Cảm ơn bạn muốn góp sức! Báo lỗi và đề xuất qua [Issues](https://github.com/azxje1506/CleanVaiLon/issues); sửa mã qua Pull Request.

## Cấu trúc mã

Toàn bộ công cụ nằm trong `CleanVaiLon.ps1`, chia hai phần:

- **Phần xử lý** – chuỗi `$WorkerCode`. Các hàm đọc thông tin và thực hiện thay đổi (`Get-StartupItems`, `Invoke-DiskScan`, `Get-DeleteSafety`, `Move-ToRecycleBin`…). Đoạn này được nạp cả vào giao diện lẫn vào luồng chạy nền (`Start-Work`), nên không được dùng biến của giao diện.
- **Giao diện** – WinForms, phần cuối tệp. Các lớp C# (`MTC.*`: cửa sổ không viền, thanh cuộn, treemap, quét cây thư mục) được biên dịch bằng `Add-Type` lúc khởi động.

Thêm trang mới: dùng `New-Page`, `New-DarkList`, `New-Btn`, `New-ChipGroup`… như các trang có sẵn.

## Nguyên tắc bắt buộc

1. **Không bao giờ xoá vĩnh viễn.** Chỉ dùng `Move-ToRecycleBin`; mục quá lớn so với Thùng rác phải bị bỏ qua.
2. **Hỏi trước khi thay đổi.** Mọi thao tác thay đổi máy cần xác nhận và ghi `Write-Log`.
3. **Không kết nối mạng.** Công cụ không gửi hay tải gì từ Internet.
4. **Đọc, không đụng vào dữ liệu riêng tư.** Không đọc mật khẩu, cookie, lịch sử duyệt web.
5. Luật mới cho "xoá được?" phải có ca kiểm thử trong `tests\Test-Safety.ps1`. Mục chưa chắc chắn để ở mức `review`, không để `safe`.

## Yêu cầu kỹ thuật

- Chạy được trên **Windows PowerShell 5.1** (không dùng `??`, `?.`, toán tử ba ngôi…).
- Tệp `.ps1` lưu **UTF-8 có BOM** để PowerShell 5.1 đọc đúng tiếng Việt (CI kiểm tra điều này).
- Biến PowerShell không phân biệt hoa thường: đừng đặt tên trùng kiểu `$C` và `$c`.

## Kiểm thử

```powershell
.\tests\Test-Safety.ps1      # luật "xoá được?" – nhanh, chạy được ở mọi máy
.\tests\Test-Functions.ps1   # chạy từng phần xử lý trên máy thật – chỉ đọc
```

Cả hai đều phải qua trước khi mở Pull Request.
