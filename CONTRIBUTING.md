# Đóng góp

Cảm ơn bạn muốn góp sức! Báo lỗi và đề xuất qua [Issues](https://github.com/azxje1506/CleanVaiLon/issues); sửa mã qua Pull Request.

## Cấu trúc mã

Công cụ là một ứng dụng C# WinForms (`src/CleanVaiLon`, .NET Framework 4.8, x64):

- `Program.cs` – điểm vào; `app.manifest` xin quyền quản trị.
- `Core.cs` – định dạng, thư mục dữ liệu, nhật ký (`nhatky.txt`), cài đặt (`caidat.json`, cùng định dạng với bản PowerShell).
- `Safety.cs` – bộ luật "xoá được?" (`Safety.Rules`, dạng dữ liệu) và `Recycle.Move`, hàm duy nhất được chuyển tệp vào Thùng rác.
- `Work/` – phần xử lý của từng trang (`Perf`, `Startup`, `Disk`, `Apps`, `Security`, `Signature`, `Backup`, `LogonTask`). Chạy ở luồng nền, không được đụng tới giao diện.
- `Controls.cs` – các lớp vốn nhúng trong bản PowerShell: cửa sổ không viền, thanh cuộn tự vẽ, treemap, quét cây thư mục.
- `UI/` – giao diện: `Theme.cs` (màu, phông, bảng dịch VI/EN), `Widgets.cs` (nút, bộ lọc, công tắc), `DarkList.cs` (danh sách tự vẽ), `MainForm.cs` (cửa sổ, chạy nền, hộp thoại), `Pages*.cs` (từng trang).

Thêm trang mới: dùng `NewPage`, `DarkList.Create`, `Ui.NewBtn`, `Ui.NewChipGroup`… như các trang có sẵn. Phiên bản chỉ đổi ở `Directory.Build.props`.

Bản PowerShell 1.x nằm trong `legacy/` cho tới khi bản C# được xác nhận; đừng thêm tính năng ở đó.

## Nguyên tắc bắt buộc

1. **Không bao giờ xoá vĩnh viễn.** Chỉ dùng `Recycle.Move`; mục quá lớn so với Thùng rác phải bị bỏ qua, mục `system` bị chặn ngay trong hàm này.
2. **Hỏi trước khi thay đổi.** Mọi thao tác thay đổi máy cần xác nhận (`Confirm`, `ConfirmRecycle`) và ghi `Log.Write`.
3. **Không kết nối mạng.** Công cụ không gửi hay tải gì từ Internet (kiểm tra chữ ký số cũng chỉ dùng bộ nhớ đệm, không kiểm tra thu hồi qua mạng).
4. **Đọc, không đụng vào dữ liệu riêng tư.** Không đọc mật khẩu, cookie, lịch sử duyệt web.
5. Luật mới cho "xoá được?" phải có ca kiểm thử trong `tests/CleanVaiLon.Tests/SafetyTests.cs`. Mục chưa chắc chắn để ở mức `review`, không để `safe`. Khi `legacy/` còn tồn tại, sửa luật ở cả hai nơi – test `RulesMatchLegacyScript` so sánh chúng.

## Yêu cầu kỹ thuật

- Build bằng **.NET SDK 8** (`dotnet build`), đích **.NET Framework 4.8** – có sẵn trong Windows nên exe nhỏ và không cần cài runtime. Không thêm gói NuGet vào ứng dụng.
- Chữ trên giao diện viết tiếng Việt; chữ cố định cần dịch thì thêm vào bảng `Lang` trong `UI/Theme.cs`.
- Tệp `.ps1` (bản legacy, script đóng gói) lưu **UTF-8 có BOM** để PowerShell 5.1 đọc đúng tiếng Việt (CI kiểm tra điều này).

## Kiểm thử

```powershell
dotnet test tests\CleanVaiLon.Tests --filter "Category!=Smoke"   # luật "xoá được?" – nhanh, chạy được ở mọi máy
dotnet test tests\CleanVaiLon.Tests --filter "Category=Smoke"    # chạy từng phần xử lý trên máy thật – chỉ đọc
```

Cả hai đều phải qua trước khi mở Pull Request.
