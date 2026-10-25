# Clean VaiLon

Công cụ kiểm tra và dọn dẹp máy Windows, giao diện tối kiểu terminal. Viết bằng PowerShell + WinForms, **không cần cài thêm gì** – chạy được ngay trên Windows 10/11.

> English version: [see below](#english).

## Tính năng

| Trang | Làm gì |
|---|---|
| **Hiệu năng** | Chương trình nào đang dùng CPU/RAM, giải thích từng cái bằng tiếng Việt, cảnh báo khi bộ nhớ ảo gần đầy |
| **Khởi động** | Mọi thứ tự chạy khi bật máy (Run, thư mục Startup, tác vụ, dịch vụ) kèm gợi ý *nên giữ / có thể tắt*. Tắt và bật lại được |
| **Ổ đĩa** | 20 tệp / thư mục lớn nhất, bộ cài đã dùng, tải xuống cũ, bộ nhớ đệm, ảnh trùng lặp |
| **Phân tích** | Bản đồ dung lượng kiểu WizTree (treemap), đi sâu vào từng thư mục, tích nhiều mục để dọn một lần |
| **Ứng dụng** | Lần dùng cuối của từng ứng dụng (Steam, Prefetch, UserAssist), gỡ bằng trình gỡ của chính ứng dụng |
| **Bảo mật** | Trạng thái Windows Defender, tường lửa, tiến trình không có chữ ký số, kết nối mạng, extension trình duyệt rủi ro – **chỉ đọc** |
| **Sao lưu** | Máy đã được sao lưu chưa, mở File History, chép thư mục sang ổ khác |
| **Nhật ký** | Mọi thay đổi công cụ đã làm |
| **Cài đặt** | Ngôn ngữ VI/EN, trang mở đầu, ngưỡng, khởi động cùng Windows… |

## Nguyên tắc an toàn

- **Không bao giờ xoá vĩnh viễn.** Chỉ chuyển vào Thùng rác; mục lớn hơn chỗ trống của Thùng rác sẽ bị bỏ qua để bạn tự quyết.
- Mỗi tệp / thư mục được đánh giá **"xoá được?"** theo 4 mức: *an toàn, cân nhắc, không nên xoá, cấm xoá*. Mục *cấm xoá* (Windows, pagefile, gốc ổ đĩa…) bị chặn hẳn.
- Mọi thao tác thay đổi máy đều **hỏi xác nhận** và được **ghi nhật ký**.
- Tắt khởi động là đảo ngược được; dịch vụ chỉ chuyển sang "chạy khi cần", không bị vô hiệu hoá.
- Không gửi dữ liệu đi đâu cả: công cụ **không kết nối Internet**.

## Cài đặt

Tải ở trang **[Releases](https://github.com/azxje1506/CleanVaiLon/releases/latest)**:

- **`CleanVaiLon-Setup-<phiên bản>.exe`** – bộ cài: có lối tắt trong Start Menu, gỡ được trong *Cài đặt → Ứng dụng*.
- **`CleanVaiLon-<phiên bản>-portable.zip`** – giải nén là chạy, đọc `READ FIRST.txt`.
- **`SHA256SUMS`** – để kiểm tra tệp tải về.

Nếu SmartScreen hiện *"Windows protected your PC"*, bấm **More info → Run anyway** (bộ cài chưa được ký số). Khi mở, công cụ xin quyền quản trị để đọc đủ thông tin – bấm **Yes**.

**Yêu cầu:** Windows 10 / 11 (64-bit), Windows PowerShell 5.1 có sẵn trong Windows.

Chi tiết cài đặt, bản portable và cách gỡ: **[docs/install.md](docs/install.md)**.

## Tài liệu

- [Hướng dẫn sử dụng](docs/user-guide.md) – từng trang, từng nút, đánh giá an toàn khi xoá
- [Cài đặt và gỡ bỏ](docs/install.md)
- [Quy trình phát hành](docs/release-checklist.md)
- [Đóng góp](CONTRIBUTING.md) · [Bảo mật](SECURITY.md) · [Lịch sử thay đổi](CHANGELOG.md)

## Vì sao phần mềm diệt virus có thể cảnh báo?

Công cụ làm nhiều việc giống phần mềm hệ thống, nên một số phần mềm diệt virus có thể **báo nhầm**. Mã nguồn để mở để bạn tự kiểm tra từng việc:

| Việc công cụ làm | Để làm gì |
|---|---|
| Đọc `Preferences` / `manifest.json` của Edge, Chrome, Brave, Cốc Cốc | Liệt kê extension và quyền của chúng ở trang **Bảo mật**. Không đọc mật khẩu, cookie hay lịch sử duyệt web |
| Đọc Prefetch, UserAssist, cấu hình Steam | Biết lần dùng cuối của ứng dụng ở trang **Ứng dụng** |
| Đọc kết nối mạng và bộ nhớ đệm DNS | Liệt kê chương trình đang kết nối Internet ở trang **Bảo mật** |
| Sửa khoá `StartupApproved`, tác vụ, chế độ dịch vụ | Tắt / bật lại mục khởi động – chỉ khi bạn bấm |
| Tạo tác vụ đăng nhập với quyền cao nhất | Chỉ khi bạn bật **Cài đặt → khởi động cùng Windows** |
| Gọi API Windows qua `Add-Type` | Giao diện tối, thanh cuộn tự vẽ, bản đồ dung lượng |
| Chạy PowerShell với `-ExecutionPolicy Bypass` | Để chạy script chưa được ký số |

File tải từ Internet chưa được ký số nên Windows SmartScreen cũng có thể hiện "Windows protected your PC" – bấm **More info → Run anyway** nếu bạn tin mã nguồn.

## Dữ liệu và gỡ bỏ

- Cài đặt và nhật ký nằm ở `%LOCALAPPDATA%\CleanVaiLon`.
- Gỡ bỏ: tắt **khởi động cùng Windows** trong trang Cài đặt (nếu đã bật), xoá thư mục công cụ và thư mục dữ liệu trên.

## Kiểm thử

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\Test-Functions.ps1
powershell -ExecutionPolicy Bypass -File .\tests\Test-Safety.ps1
```

Các bài kiểm thử chỉ đọc thông tin, không thay đổi gì trên máy.

## Giấy phép

[MIT](LICENSE) © 2026 [azxje1506](https://github.com/azxje1506)

---

## English

**Clean VaiLon** is a Windows checkup and cleanup tool with a dark terminal-style UI, written in PowerShell + WinForms. Nothing to install – it runs on stock Windows 10/11 (PowerShell 5.1).

**Pages:** Performance, Startup, Disks, Analyzer (WizTree-style treemap), Apps (last-used dates, uninstall), Security (read-only: Defender, firewall, unsigned processes, connections, risky browser extensions), Backup, Log, Settings (VI/EN UI language, start page, thresholds, start with Windows).

**Safety:** never deletes permanently – items only go to the Recycle Bin, and items too large for it are skipped. Every file/folder gets a *safe / review / shouldn't delete / blocked* rating; system locations are blocked. Every change asks for confirmation and is logged. The tool makes no network requests.

**Install:** grab `CleanVaiLon-Setup-<version>.exe` (installer with Start Menu shortcut and uninstaller) or `CleanVaiLon-<version>-portable.zip` from [Releases](https://github.com/azxje1506/CleanVaiLon/releases/latest), and verify it against `SHA256SUMS`. The installer is not code-signed, so SmartScreen may ask you to confirm (*More info → Run anyway*). Details: [docs/install.md](docs/install.md) (Vietnamese).

**Antivirus warnings:** because it reads browser extension manifests, Prefetch/UserAssist and network connections, edits startup entries and can register a logon task, some antivirus products may flag it as a false positive. See the table above for what each behavior is used for – all source code is here to review.

The UI language can be switched to English; item descriptions and explanations are currently Vietnamese only.

License: [MIT](LICENSE).
