# Quy trình phát hành

1. **Đổi phiên bản** trong `CleanVaiLon.ps1`: dòng `$AppVersion = 'x.y.z'`.
2. **Ghi thay đổi** vào `CHANGELOG.md` dưới tiêu đề `## x.y.z – …`. Nội dung mục này được dùng làm ghi chú phát hành.
3. **Chạy kiểm thử** (chỉ đọc, không thay đổi gì trên máy):
   ```powershell
   .\tests\Test-Safety.ps1
   .\tests\Test-Functions.ps1
   ```
4. **Đóng gói thử trên máy** (cần Inno Setup 6: `winget install JRSoftware.InnoSetup`):
   ```powershell
   .\tools\Build-Release.ps1
   ```
   Cài thử `dist\CleanVaiLon-Setup-x.y.z.exe`, mở công cụ, rồi gỡ trong *Cài đặt → Ứng dụng*.
5. **Kiểm tra trên VirusTotal**: tải `CleanVaiLon.ps1` và bộ cài lên virustotal.com.
6. **Commit và gắn tag**:
   ```powershell
   git commit -am "Release x.y.z"
   git tag vx.y.z
   git push origin main --tags
   ```
   GitHub Actions (`.github/workflows/release.yml`) sẽ kiểm tra tag khớp `$AppVersion`, chạy kiểm thử, dựng bộ cài + bản portable + `SHA256SUMS` và tạo bản phát hành.
7. Mở trang Releases, kiểm tra đủ 3 tệp và ghi chú phát hành.

Tag sai (không khớp `$AppVersion`) sẽ làm workflow dừng trước khi phát hành – xoá tag bằng `git push origin :refs/tags/vx.y.z`, sửa rồi gắn lại.
