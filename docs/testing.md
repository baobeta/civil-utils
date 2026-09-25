# Cài C3DTools để thử nghiệm

Yêu cầu: Windows 64-bit, Civil 3D 2021 (update 2021.3 trở lên).

1. Tải file `C3DTools-<phiên bản>.zip` ở mục **Assets** của bản mới nhất: https://github.com/baobeta/civil-utils/releases
2. Giải nén (chuột phải → Extract All).
3. Đóng Civil 3D, bấm đúp `install.cmd`.
4. Mở Civil 3D 2021, gõ lệnh `CTHELLO`.

Gỡ cài đặt: đóng Civil 3D, bấm đúp `uninstall.cmd`.

Báo lỗi: gửi ảnh chụp dòng lệnh, file DWG (nếu được) và phiên bản trong tên file zip.

## Thử lệnh yếu tố cong

Có hai cách mở: trên ribbon, tab **C3DTools**, panel **Tuyến** có ba nút **Yếu tố cong** (lớn), **Mẫu TCVN** và **Bảng cong** (nhỏ); hoặc gõ thẳng lệnh `CTYTC`, `CTYTCMAU`, `CTYTCBANG`.

### Với một polyline

1. Gõ `CTYTC`, chọn polyline tuyến trên bản vẽ (hoặc bấm **Chọn trên bản vẽ…** trong hộp thoại nếu chưa chọn trước).
2. Hộp thoại **Yếu tố cong – TCVN 4054** hiện ra với một dòng cho mỗi đỉnh (PI). Cột **A** (góc chuyển hướng) tự tính, chỉ đọc. Nhập **R, L1, L2, Wb, Wl** cho từng dòng; T1, T2, P, K và các lý trình cập nhật ngay khi gõ.
3. Dòng nào lỗi (✖: chồng cong, hoặc L quá dài so với góc chuyển hướng) sẽ khoá nút **Áp dụng**; cảnh báo (⚠: R dưới giới hạn, R dưới bán kính thông thường) không khoá. Bấm **Gợi ý R, L theo TCVN** để tự sửa theo bảng TCVN 4054.
4. Chọn các ô cần vẽ ở dưới: **Vẽ đường cong (ARC/clothoid)**, **Tạo Alignment Civil 3D**, **Khung**, **Cọc**, **CSV**.
5. Bấm **Xem trước** để xem kết quả trong bản vẽ; dòng lệnh hỏi `Giữ kết quả? [Co/Khong]` — gõ `Khong` để quay lại hộp thoại chỉnh tiếp, hoặc **Áp dụng** để ghi luôn không hỏi.
6. Kết quả: đường cong và khung trên layer `YTC_CONG`/`YTC_BANG`, cọc NĐ/TĐ/P/TC/NC trên layer `YTC_COC`, và file CSV `<tên bản vẽ>_YEUTOCONG.csv` cạnh file DWG.

### Với một alignment đã có

1. Gõ `CTYTC`, chọn alignment. Hộp thoại hiện thêm mục **Chế độ**: mặc định là **● Chỉ cắm cọc + khung** (R, L, A, T, P, K đọc từ alignment, chỉ đọc; chỉ Wb/Wl sửa được; alignment không bị đổi). Chọn **○ Thiết kế lại cong** nếu muốn Áp dụng ghi lại đường cong.
2. Bấm **Đọc Wb/Wl từ Offset Alignment…** để lấy độ mở rộng bụng/lưng từ các Offset Alignment có vùng mở rộng, thay vì gõ tay.
3. Xem trước / Áp dụng như trên. Ở chế độ cắm cọc, chỉ cọc và khung được vẽ lại; alignment giữ nguyên.

### Hoàn tác và chạy lại

Mỗi lần Xem trước (khi giữ) hoặc Áp dụng chỉ tạo **một bước undo**: gõ `U` một lần là hết toàn bộ thay đổi. Chạy lại `CTYTC` trên cùng polyline/alignment sẽ cập nhật lại cọc, khung, CSV (ghi đè, không tạo trùng).

`CTYTCMAU` chỉ nhập kiểu (style), label set và bộ viết tắt TCVN, không đụng tới hình học — dùng khi muốn có nhãn native mà không qua hộp thoại. `CTYTCBANG` xuất bảng tổng hợp đường cong (AutoCAD Table) và CSV cho alignment hoặc polyline đã chạy `CTYTC`; cũng có thể bấm ô **CSV** ngay trong hộp thoại `CTYTC` thay vì chạy riêng.

### Đối chiếu với YTC.lsp

Trên cùng một polyline, cùng R, L và lý trình đầu, so sánh với CSV xuất ra từ `YTC.lsp`:

- T1, T2, P, K sai lệch không quá 0.01 m.
- Lý trình các cọc NĐ/TĐ/P/TC/NC sai lệch không quá 0.01 m.
- Với alignment có Offset Alignment mở rộng, so `YTCA` và `CTYTC` (chế độ Chỉ cắm cọc + khung): Wb/Wl, T1/T2/P đo được và lý trình cọc phải khớp trong 0.01 m, khung phải cùng vị trí và góc xoay.

### Chưa kiểm tra trên Windows

Các mục sau cần người test trên Windows + Civil 3D 2021 xác nhận:

- [ ] Tạo alignment mới từ polyline (ô **Tạo Alignment Civil 3D**)
- [ ] `CTYTCMAU`: nhập kiểu/label set/viết tắt TCVN thực tế trong Civil 3D
- [ ] Icon các nút trên ribbon (tab C3DTools)
- [ ] Hộp thoại ở độ phân giải màn hình DPI 150%

Khi báo lỗi hoặc báo kết quả thử, gửi kèm: ảnh chụp dòng lệnh, file DWG (nếu được), và phiên bản trong tên file zip.

## Các lệnh mới trong 0.3 / 0.4

Từ bản 0.3 tab **C3DTools** có sáu panel: **Tuyến**, **Trắc dọc**, **Trắc ngang**, **Địa hình**, **Thoát nước**, **Bản vẽ**. Mỗi nút có tooltip một câu (rê chuột lên nút để xem). Lệnh nào cũng mở hộp thoại trước; dòng lệnh chỉ dùng để chọn đối tượng, chọn điểm chèn và hỏi `Giữ kết quả? [Co/Khong]` sau **Xem trước**. Hộp thoại nhớ kích thước và các ô đã tích cho lần sau. Kịch bản thử chi tiết, có số liệu mong đợi: [test-plan-0.3-0.4.md](test-plan-0.3-0.4.md).

### Toạ độ cọc — `CTTOADO` (panel Tuyến)

Lập bảng toạ độ cọc của một **alignment**: cọc chi tiết theo khoảng cách, cọc đường cong NĐ/TĐ/P/TC/NC, cọc thêm; Z lấy từ mặt phủ nếu chọn. Xuất **Bảng** (AutoCAD Table), **CSV**, **Excel** (`<tên bản vẽ>_TOADO`) và **Điểm COGO**.

Kiểm tra: tên cọc và lý trình đúng thứ tự, cột X/Y đổi chỗ khi tích/bỏ **X = Bắc (VN-2000)**, file Excel mở được. Chạy lại cho cùng tuyến sẽ thay bảng và điểm COGO cũ. `U` một lần xoá bảng và điểm COGO vừa tạo.

### Bảng trắc dọc — `CTTRACDOC` (panel Trắc dọc)

Vẽ bảng số liệu trắc dọc kiểu Việt Nam (tên cọc, khoảng cách, lý trình, cao độ tự nhiên/thiết kế, chênh cao, độ dốc) bằng đường và chữ ngay dưới profile view (layer `TD_BANG`, `TD_CHU`); xuất CSV/Excel `<tên bản vẽ>_TRACDOC`.

Kiểm tra: các cột cọc thẳng với lý trình trên trắc dọc, chữ không chồng nhau, bật/tắt và đổi thứ tự dòng bằng **Lên** / **Xuống**. `U` một lần xoá bảng.

### Cong đứng — `CTCONGDUNG` (panel Trắc dọc)

Đọc các đỉnh của trắc dọc thiết kế, tính i1, i2, A, R, K, T, E, lý trình TĐ/TC và điểm cao/thấp; kiểm tra theo bảng cong đứng của preset (nếu có). Xuất **Khung** trên mỗi đỉnh (layer `TD_YTC`), **Bảng**, **CSV**, **Excel** (`<tên bản vẽ>_CONGDUNG`).

Kiểm tra: số trong khung khớp với Profile Grid View; nếu dòng lệnh báo `Civil trả về độ dốc theo %; đã quy đổi` thì chụp lại. `U` một lần xoá khung và bảng.

### Bảng trắc ngang — `CTTRACNGANG` (panel Trắc ngang)

Vẽ bảng số liệu dưới mỗi section view (cao độ tự nhiên/thiết kế, khoảng cách, diện tích đào/đắp tính từ hai mặt cắt đã chọn), layer `TN_BANG`, `TN_CHU`; xuất CSV/Excel `<tên bản vẽ>_TRACNGANG`. Tích **Cả nhóm (section view group)** để làm cho cả nhóm.

Kiểm tra: diện tích đào/đắp so với Civil 3D (Compute Materials hoặc đo tay), bảng nằm đúng dưới trắc ngang. `U` một lần xoá mọi bảng vừa vẽ.

### Xếp trang — `CTXEPTRANG` (panel Trắc ngang)

Dời các section view (cùng bảng `CTTRACNGANG` của chúng) vào các ô của tờ in theo thứ tự lý trình, vẽ khung tờ và tên tờ trên layer `TN_KHUNG`. Khổ giấy, lề, số cột × hàng, khoảng hở và tỷ lệ lấy từ preset (mặc định A3 420 × 297, 3 × 2, 1:200).

Kiểm tra: thứ tự từ trái sang phải, trên xuống dưới; tên tờ `TRẮC NGANG – Tờ n/N – Km… … Km…`. `U` một lần đưa trắc ngang về chỗ cũ và xoá khung.

### Mặt địa hình — `CTMATDIA` (panel Địa hình)

Hai thẻ: **Xoá tam giác dài** (cạnh dài hơn giá trị nhập, hoặc ngoài polyline ranh giới; nút **Đếm** chỉ đếm, không sửa) và **Ghi cao độ đồng mức** (chọn line/polyline cắt qua đồng mức; chữ TEXT trên layer `DH_CAODO`, xoay theo đồng mức).

Kiểm tra: số tam giác bị xoá hợp lý, mặt phủ không bị thủng ở giữa; nhãn đọc được, không lộn ngược. `U` một lần hoàn tác.

### VN-2000 — `CTVN2000` (panel Địa hình)

Chuyển toạ độ đối tượng chọn (điểm, line, polyline, cung, block, text) hoặc tất cả điểm COGO từ kinh tuyến trục / múi chiếu này sang kinh tuyến trục / múi chiếu khác. Chọn tỉnh trong danh sách hoặc gõ kinh tuyến (`105°45'`, `105 45`, `105.75`). **Kinh tuyến theo tỉnh trong preset chưa được đối chiếu văn bản**: hãy kiểm tra trước khi dùng.

Kiểm tra: chuyển đi rồi chuyển ngược lại phải về đúng toạ độ cũ (sai lệch ≤ 1 mm). `U` một lần hoàn tác.

### Bảng cống — `CTBANGCONG` (panel Thoát nước)

Lập bảng thống kê cống của các mạng cống (pipe network) cắt qua một alignment: lý trình, vị trí trái/phải, góc chéo, khẩu độ, chiều dài, cao độ đáy thượng/hạ lưu, độ dốc, chiều sâu chôn. Sửa được cột **Tên**, **Loại**, **Ghi chú**. Xuất **Bảng**, **CSV**, **Excel** (`<tên bản vẽ>_BANGCONG`).

Kiểm tra: lý trình và cao độ đáy khớp với Pipe Properties. `U` một lần xoá bảng.

### Chuyển font — `CTFONT` (panel Bản vẽ)

Chuyển chữ tiếng Việt giữa **TCVN3 (ABC)**, **VNI Windows** và **Unicode** cho đối tượng chọn hoặc toàn bản vẽ (TEXT, MTEXT, thuộc tính, kích thước, MLeader, ô bảng); có thể đổi font kiểu chữ sang font Unicode của preset (mặc định Arial). Bảng dưới hộp thoại cho xem 20 chuỗi đầu **Trước** / **Sau**.

Kiểm tra: chữ đúng dấu sau khi chuyển; ký hiệu như `Ø600`, `2×3`, `½` **không** bị đổi; đối tượng có field được bỏ qua và báo số lượng. `U` một lần hoàn tác.

### Chuẩn layer — `CTLAYER` (panel Bản vẽ)

Liệt kê mọi layer với số đối tượng; nhập **Layer đích** (hoặc lấy từ `LayerMap` của preset) để chuyển đối tượng sang layer chuẩn, tạo layer mới với **Màu** đã nhập. Tuỳ chọn **Áp dụng cho block**, **Xoá layer rỗng**; nút **Lưu vào preset** ghi bảng vào `*.c3dtools.json` cạnh bản vẽ.

Kiểm tra: đối tượng chuyển đúng layer (kể cả trên layer khoá), layer 0 trong block giữ nguyên. `U` một lần hoàn tác.
