# Cài đặt và gỡ bỏ

Clean VaiLon chạy trên **Windows 10 / 11 (64-bit)** với Windows PowerShell 5.1 có sẵn trong Windows. Không cần cài .NET hay thư viện nào khác.

Có hai cách cài, tải ở trang [Releases](https://github.com/azxje1506/CleanVaiLon/releases/latest):

| Tệp | Dùng khi |
|---|---|
| `CleanVaiLon-Setup-<phiên bản>.exe` | Muốn cài như phần mềm bình thường: có lối tắt trong Start Menu, gỡ được trong **Cài đặt → Ứng dụng** |
| `CleanVaiLon-<phiên bản>-portable.zip` | Không muốn cài, giải nén ra là chạy (ví dụ chép vào USB) |
| `SHA256SUMS` | Để kiểm tra tệp tải về không bị sửa |

## Kiểm tra tệp tải về (khuyến nghị)

Mở PowerShell ở thư mục chứa tệp vừa tải và so mã với dòng tương ứng trong `SHA256SUMS`:

```powershell
Get-FileHash .\CleanVaiLon-Setup-1.3.0.exe
```

## Cách 1 – Bộ cài

1. Chạy `CleanVaiLon-Setup-<phiên bản>.exe`.
2. Nếu Windows SmartScreen hiện *"Windows protected your PC"*: bấm **More info → Run anyway**. Cảnh báo này xuất hiện vì bộ cài chưa được ký số, không phải vì phát hiện mã độc.
3. Chọn cài **cho riêng tài khoản này** (mặc định, không cần quyền quản trị) hoặc **cho mọi tài khoản**.
4. Tích *Create a desktop shortcut* nếu muốn có lối tắt ngoài màn hình nền.

Mặc định công cụ được cài vào `%LOCALAPPDATA%\Programs\CleanVaiLon`. Khi mở, công cụ sẽ xin quyền quản trị – bấm **Yes** để đọc được đầy đủ thông tin.

## Cách 2 – Bản portable

1. Giải nén vào một thư mục cố định, ví dụ `D:\Tools\CleanVaiLon`.
2. Mở PowerShell trong thư mục đó và chạy **một lần** để bỏ dấu "tải từ Internet":
   ```powershell
   Get-ChildItem -Recurse | Unblock-File
   ```
3. Bấm đúp `CleanVaiLon.bat`.
4. Tuỳ chọn – tạo lối tắt có icon trên Desktop và Start Menu:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\CreateShortcut.ps1
   ```

## Gỡ bỏ

**Nếu đã bật "khởi động cùng Windows"**, hãy tắt nó trong trang **Cài đặt** trước khi gỡ. Bộ gỡ cũng thử xoá tác vụ này, nhưng có thể không đủ quyền nếu nó được tạo với quyền quản trị.

- **Bản cài:** *Cài đặt → Ứng dụng → Clean VaiLon → Gỡ cài đặt*. Bộ gỡ sẽ hỏi có xoá luôn cài đặt và nhật ký không.
- **Bản portable:** xoá thư mục công cụ, rồi xoá thư mục dữ liệu `%LOCALAPPDATA%\CleanVaiLon` nếu không cần nữa.

Gỡ công cụ **không** hoàn tác những thay đổi bạn đã làm bằng nó (mục khởi động đã tắt, ứng dụng đã gỡ). Xem lại trang **Nhật ký** trước khi gỡ nếu muốn bật lại gì đó.

## Phần mềm diệt virus cảnh báo?

Xem mục *"Vì sao phần mềm diệt virus có thể cảnh báo?"* trong [README](../README.md). Toàn bộ mã nguồn nằm trong `CleanVaiLon.ps1`, bạn có thể đọc trước khi chạy.
