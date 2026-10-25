# Bản PowerShell (1.x) – chỉ để đối chiếu

Đây là Clean VaiLon 1.3.0, viết bằng PowerShell 5.1 + WinForms. Từ 2.0.0 công cụ là chương trình C# trong [`src/`](../src/).

Thư mục này được giữ lại cho tới khi bản C# được xác nhận chạy tốt trên máy thật, để:

- so sánh hành vi và giao diện khi kiểm tra bản mới;
- test `RulesMatchLegacyScript` kiểm tra bộ luật "xoá được?" của bản C# giống hệt `$SafetyRules` ở đây;
- CI vẫn chạy `tests\Test-Safety.ps1` trên bản này.

Chạy thử (không xin quyền quản trị):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -STA -File .\legacy\CleanVaiLon.ps1 -NoElevate
```

Không thêm tính năng ở đây. Khi xoá thư mục này, xoá luôn các bước "Legacy script" trong `.github/workflows/ci.yml`.
