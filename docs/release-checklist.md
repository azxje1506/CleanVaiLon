# Quy trình phát hành

1. **Đổi phiên bản** trong `Directory.Build.props`: dòng `<Version>x.y.z</Version>` (exe, bộ cài và bản portable đều đọc từ đây).
2. **Ghi thay đổi** vào `CHANGELOG.md` dưới tiêu đề `## x.y.z – …`. Nội dung mục này được dùng làm ghi chú phát hành.
3. **Chạy kiểm thử** (chỉ đọc, không thay đổi gì trên máy):
   ```powershell
   dotnet test tests\CleanVaiLon.Tests --filter "Category!=Smoke"
   dotnet test tests\CleanVaiLon.Tests --filter "Category=Smoke"
   ```
4. **Đóng gói thử trên máy** (cần .NET SDK 8 và Inno Setup 6: `winget install Microsoft.DotNet.SDK.8 JRSoftware.InnoSetup`):
   ```powershell
   .\tools\Build-Release.ps1
   ```
   Cài thử `dist\CleanVaiLon-Setup-x.y.z.exe`, mở công cụ, bấm qua từng trang, rồi gỡ trong *Cài đặt → Ứng dụng*.
5. **Kiểm tra trên VirusTotal**: tải `publish\CleanVaiLon.exe` và bộ cài lên virustotal.com.
6. **Commit và gắn tag**:
   ```powershell
   git commit -am "Release x.y.z"
   git tag vx.y.z
   git push origin main --tags
   ```
   GitHub Actions (`.github/workflows/release.yml`) sẽ kiểm tra tag khớp `<Version>`, chạy kiểm thử, dựng bộ cài + bản portable + `SHA256SUMS` và tạo bản phát hành.
7. Mở trang Releases, kiểm tra đủ 3 tệp và ghi chú phát hành.

Tag sai (không khớp `<Version>`) sẽ làm workflow dừng trước khi phát hành – xoá tag bằng `git push origin :refs/tags/vx.y.z`, sửa rồi gắn lại.
