# Hướng dẫn sử dụng

Clean VaiLon có 9 trang ở menu bên trái. Trang nào có ô tích thì có thể chọn nhiều mục và làm một lần; dòng *"· đã chọn N · X GB [✕]"* cạnh tiêu đề cho biết đang chọn bao nhiêu – bấm vào đó để bỏ chọn tất cả.

Thanh bên dưới menu cho biết công cụ đang **SẴN SÀNG** hay **ĐANG LÀM**; khi đang quét, giữa trang có khung tiến trình kèm số giây đã chạy.

> Nhãn góc trên bên phải cho biết công cụ có đang chạy với **quyền quản trị** không. Thiếu quyền thì một số thông tin (Prefetch, một số dịch vụ, tiến trình hệ thống) sẽ trống.

## Hiệu năng

Đo trong 3 giây xem chương trình nào dùng CPU và RAM, gộp theo tên (ví dụ 32 tiến trình `msedge` thành một dòng).

- Dòng **vàng** là chương trình vượt ngưỡng "nặng" (mặc định 5%, đổi trong **Cài đặt**).
- Khung dưới cùng cho biết CPU, RAM còn trống và **bộ nhớ ảo** đã dùng. Khi bộ nhớ ảo trên 85%, máy dễ giật – nên đóng bớt ứng dụng hoặc tăng pagefile.

## Khởi động

Mọi thứ tự chạy khi bật máy: khoá Run trong registry, thư mục Startup, tác vụ đăng nhập và dịch vụ không phải của Microsoft. Mỗi mục có gợi ý:

| Gợi ý | Nghĩa |
|---|---|
| **Nên giữ** | Driver, bộ gõ, phần mềm cần cho phần cứng |
| **Có thể tắt** | Trình mở game, phần mềm chat, công cụ cập nhật… – mở tay khi cần |
| **Cần xác nhận** | Chạy ngầm bằng script (`.vbs`, `wscript`, `node`…) – chỉ giữ nếu bạn tự cài |
| **Chưa rõ** | Không có trong danh sách hiểu biết – tra tên trước khi tắt |

Tích chọn rồi bấm **tắt mục đã chọn** hoặc **bật lại mục đã chọn**. Không có gì bị xoá: mục khởi động được tắt giống như trong Task Manager, tác vụ bị vô hiệu hoá, dịch vụ chuyển sang "chạy khi cần". Thay đổi có hiệu lực từ lần bật máy sau.

## Ổ đĩa

Chọn ổ ở hàng **ổ:** rồi bấm **⟳ quét dung lượng**. Hàng **xem:** đổi giữa các danh sách:

- **20 tệp / 20 thư mục lớn nhất**
- **Bộ cài & file nén** – `.iso`, `.zip`, `.rar`, repack… có thể đã cài xong
- **Tải xuống cũ** – tệp trong thư mục Downloads (và thư mục của IDM) cũ hơn 90 ngày
- **Bộ nhớ đệm & file tạm** – thứ tự tạo lại được, và dung lượng Thùng rác
- **Ảnh trùng lặp** – bấm **tìm ảnh trùng lặp** để so sánh theo nội dung. Bản cũ nhất được giữ; bản nằm ở ổ khác được làm mờ vì có thể là bản sao lưu

Cột **xoá được?** đánh giá từng mục (xem [Đánh giá an toàn](#đánh-giá-an-toàn-khi-xoá)). Bấm đúp một dòng để mở vị trí trong File Explorer.

## Phân tích

Bản đồ dung lượng kiểu WizTree. Chọn ổ ở hàng **quét:** (hoặc **chọn thư mục…**) để bắt đầu.

- Danh sách trên cùng là các thư mục / tệp con, với thanh **% thư mục cha**.
- Bản đồ bên dưới: ô càng to càng chiếm nhiều chỗ, màu theo loại tệp (xem chú thích **màu:**). Tệp dưới 1 MB được gộp thành một ô *[N tệp nhỏ]*.
- **Bấm** để chọn, **bấm đúp** vào thư mục để đi vào trong, **↑ lên trên** để quay ra.
- Tích nhiều dòng rồi bấm **→ thùng rác** để dọn một lần. Chưa tích dòng nào thì nút sẽ dùng mục đang chọn trên bản đồ.
- **kiểm tra một tệp…** cho biết một tệp bất kỳ có nên xoá không.

## Ứng dụng

Danh sách ứng dụng đã cài và **lần dùng cuối**:

- Game Steam: lấy từ lịch sử chơi của Steam – chính xác.
- Ứng dụng khác: từ lịch sử chạy của Windows (Prefetch) và lần mở bằng biểu tượng (UserAssist). Windows chỉ giữ lịch sử này một thời gian, nên cột **nguồn dữ liệu** ghi rõ từ mốc nào.

Bộ lọc **không dùng trong N tháng** (mặc định 6, đổi trong **Cài đặt**). Dòng mờ là thành phần hệ thống (Visual C++, .NET, driver…) – nên giữ.

Tích chọn rồi bấm **gỡ mục đã chọn**: công cụ mở trình gỡ của chính từng ứng dụng để bạn xác nhận; game Steam được gỡ qua Steam.

## Bảo mật

Chỉ đọc, không thay đổi gì:

- Windows Defender có đang bật không, có bị tắt bằng chính sách không, tường lửa.
- Tiến trình không có chữ ký số hợp lệ.
- Chương trình đang kết nối Internet, chương trình mở cổng chờ kết nối, proxy, file hosts.
- Extension của Edge, Chrome, Brave, Cốc Cốc (mọi hồ sơ) và mức rủi ro theo quyền của chúng: đọc được mọi trang, đọc cookie, điều khiển proxy, ví tiền mã hoá, extension "đào airdrop"…

Mức **Cao** (đỏ) nên xem ngay, **Trung bình** (vàng) nên xem lại.

## Sao lưu

Cho biết máy có Windows Backup, File History, điểm khôi phục, OneDrive hay phần mềm sao lưu nào không, và ổ cắm ngoài đang có.

- **mở file history** – mở trang cài đặt File History của Windows.
- **chép thư mục…** – chép một thư mục sang ổ khác bằng `robocopy` (chỉ thêm và cập nhật tệp, không xoá gì ở nơi lưu).

## Nhật ký

Mọi thay đổi công cụ đã làm: tắt / bật khởi động, chuyển vào Thùng rác, gỡ ứng dụng, sao lưu, đổi cài đặt. Nhật ký nằm ở `%LOCALAPPDATA%\CleanVaiLon\nhatky.txt`.

## Cài đặt

| Cài đặt | Tác dụng |
|---|---|
| ngôn ngữ | VI / EN cho phần giao diện (nội dung giải thích hiện chỉ có tiếng Việt) |
| khởi động cùng windows | Mở công cụ thu nhỏ khi đăng nhập, qua Task Scheduler nên không hỏi quyền quản trị mỗi lần |
| trang mở đầu | Trang hiện ra khi mở công cụ |
| tự tải dữ liệu khi mở trang | Tắt đi thì chỉ quét khi bạn bấm nút |
| ngưỡng "nặng" | Phần trăm CPU / RAM để tô vàng ở trang Hiệu năng |
| số tháng "không dùng" | Mặc định cho bộ lọc ở trang Ứng dụng |
| hỏi lại lần 2 | Xác nhận thêm lần nữa khi xoá mục "không nên xoá" |
| lưới nền | Bật / tắt lưới mờ phía sau |

Cài đặt được lưu ngay vào `%LOCALAPPDATA%\CleanVaiLon\caidat.json`.

## Đánh giá an toàn khi xoá

Mọi tệp / thư mục ở trang **Ổ đĩa** và **Phân tích** được xếp vào một trong 4 mức:

| Mức | Ví dụ | Khi xoá |
|---|---|---|
| **An toàn** | File tạm, bộ nhớ đệm, file ghi lỗi, phần tải dở của Steam | Hỏi xác nhận như thường |
| **Cân nhắc** | Bộ cài, file nén, Downloads, `node_modules`, nội dung Workshop | Hỏi xác nhận |
| **Không nên xoá** | Dữ liệu cá nhân, save game, game đã cài, Program Files, AppData, `.git` | Cảnh báo rõ, hỏi lại lần 2 |
| **Cấm xoá** | `C:\Windows`, pagefile, System Volume Information, gốc ổ đĩa | **Bị chặn**, kể cả khi đã tích |

Mục không thuộc nhóm nào mặc định là **Cân nhắc**, không bao giờ tự cho là an toàn. Với tệp, công cụ còn kiểm tra tệp có **đang được mở** không và đã lâu chưa sửa.

Mọi thứ chỉ được **chuyển vào Thùng rác**. Mục lớn hơn chỗ trống của Thùng rác sẽ bị bỏ qua – nếu chắc chắn, bạn tự xoá bằng Shift + Delete. Nhớ dọn Thùng rác để lấy lại dung lượng.
