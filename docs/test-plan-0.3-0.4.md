# Kế hoạch thử nghiệm C3DTools 0.3 / 0.4 — mười lệnh mới

Dành cho người thử trên **Windows 64-bit + Civil 3D 2021 (update 2021.3 trở lên)**. Mười lệnh trong tài liệu này mới chỉ được biên dịch và kiểm tra phần tính toán (Core) trên máy không có Civil 3D; **chưa chạy lần nào trong Civil 3D**. Vì vậy mọi kết quả, kể cả "không lỗi", đều có giá trị. Làm theo thứ tự; mỗi bài ghi **Đạt / Không đạt** và ghi chú vào bảng cuối.

Các số "mong đợi" dưới đây lấy từ bộ kiểm thử tự động của phần Core (cùng dữ liệu vào, cùng kết quả). Nếu Civil 3D cho số khác, ghi lại cả hai số.

Thời gian dự kiến: 2–3 giờ cho toàn bộ; có thể làm từng bài riêng.

Quy ước chung cho mọi lệnh (áp dụng ở mọi bài, không nhắc lại):

- Lệnh luôn mở **hộp thoại** trước. Dòng lệnh chỉ dùng để chọn đối tượng, chọn điểm chèn bảng và hỏi `Giữ kết quả? [Co/Khong] <Co>:` sau khi bấm **Xem trước** (gõ `Khong` → mọi thứ vừa vẽ biến mất, hộp thoại mở lại).
- Chân hộp thoại: dòng tóm tắt bên trái; **Xem trước**, **Áp dụng**, **Hủy** bên phải. **Áp dụng** mờ khi còn ô nhập sai (ô đỏ). **Esc** = Hủy.
- Ô số nhận cả dấu phẩy và dấu chấm (`2,5` = `2.5`).
- Mỗi lần Áp dụng (hoặc Xem trước + `Co`) là **một bước undo**: `U` một lần phải xoá hết thay đổi của lần đó.
- Chạy lại cùng lệnh cho cùng đối tượng thì **thay** kết quả cũ, không vẽ trùng.
- Chọn đối tượng trước rồi **gõ** lệnh thì đối tượng được dùng luôn. Nút ribbon gửi `^C^C` trước tên lệnh nên có thể bỏ chọn; khi đó bấm **Chọn trên bản vẽ…** trong hộp thoại.
- CSV/Excel ghi cạnh file DWG, tên `<tên bản vẽ>_<HẬU TỐ>.csv|.xlsx`. Bản vẽ **phải được lưu** trước, nếu không dòng lệnh báo `Bản vẽ chưa được lưu: bỏ qua xuất CSV/Excel.`
- Nếu dòng lệnh hiện bất kỳ dòng nào bắt đầu bằng `Không …`, `Lỗi …` hoặc có tên hàm tiếng Anh (ví dụ `FindXYAtStationAndElevation`, `DeleteLines`) → **chụp lại**: đó là chỗ phần mềm đã phải dùng cách thay thế.

## 0. Chuẩn bị

1. Tải `C3DTools-0.3.0.<số>.zip` từ mục **Assets** của bản mới nhất: https://github.com/baobeta/civil-utils/releases (hoặc file zip được gửi kèm).
2. Giải nén. Trong thư mục giải nén phải có: `install.cmd`, `uninstall.cmd`, `install.ps1`, `THIRD_PARTY.md` và thư mục `C3DTools.bundle`.
3. Kiểm tra `C3DTools.bundle\Contents\` có: `C3DTools.Civil2021.dll`, `C3DTools.Core.dll`, `Newtonsoft.Json.dll`, thư mục `Resources\tcvn4054.preset.json`. **Không được có** file nào tên bắt đầu bằng `Ac`, `Aec`, `Adw`, `AdUi`. Nếu có → báo ngay, dừng thử.
4. Mở `C3DTools.bundle\PackageContents.xml` bằng Notepad: `AppVersion="0.3.0…"` và có 14 dòng `<Command …>`: CTHELLO, CTYTC, CTYTCMAU, CTYTCBANG, CTFONT, CTLAYER, CTTOADO, CTCONGDUNG, CTTRACDOC, CTVN2000, CTMATDIA, CTBANGCONG, CTTRACNGANG, CTXEPTRANG.
5. Nếu đã cài 0.2: đóng Civil 3D, bấm đúp `uninstall.cmd` trước. Sau đó bấm đúp `install.cmd`.
6. Ghi lại: phiên bản Windows, độ phân giải và tỉ lệ DPI (Settings → Display → Scale), Civil 3D build (Help → About), **ngôn ngữ vùng của Windows** (nếu là Tiếng Việt thì dấu phẩy là dấu thập phân — trường hợp quan trọng cần thử).

### 0.1 Bản vẽ thử chung (dùng cho Bài B, C, D, E, H)

Bản vẽ mới từ mẫu `_AutoCAD Civil 3D (Metric) NCS.dwt`, **lưu ngay** thành `C3DT-thu.dwg` trong một thư mục riêng.

1. **Tuyến thẳng 1200 m:** `PLINE 0,0 ↵ 1200,0 ↵ ↵`. Home → Alignment → **Create Alignment from Objects**, chọn polyline, đặt tên `T1`, bỏ tích *Add curves between tangents*. Lý trình đầu 0.
2. **Mặt phủ tự nhiên `TN`, phẳng ở cao độ 30:** vẽ hai 3D polyline `3DPOLY -20,30,30 ↵ 1220,30,30 ↵ ↵` và `3DPOLY -20,-30,30 ↵ 1220,-30,30 ↵ ↵`. Tạo TIN surface tên `TN`, Definition → Breaklines → Add, chọn hai 3D polyline.
3. **Mặt phủ thiết kế `TK` có rãnh hình thang** (sâu 1 m, đáy rộng 4 m, miệng rộng 5 m, taluy 1:0.5) — sáu 3D polyline dọc tuyến, từ x = 0 đến x = 1200:

   | Offset (y) | Cao độ (z) |
   | --- | --- |
   | 10 | 30 |
   | 2.5 | 30 |
   | 2 | 29 |
   | −2 | 29 |
   | −2.5 | 30 |
   | −10 | 30 |

   Ví dụ dòng đầu: `3DPOLY 0,10,30 ↵ 1200,10,30 ↵ ↵`. Tạo TIN surface `TK`, Definition → Breaklines → Add, chọn sáu 3D polyline.
4. **Profile view + trắc dọc:** Create Profile from Surface cho `T1` với mặt `TN` → Draw in profile view. Rồi **Profile Creation Tools** trên profile view đó, tên `TK`, ba đỉnh (PVI):

   | Đỉnh | Lý trình | Cao độ |
   | --- | --- | --- |
   | đầu | 0+000 | 12.35 |
   | Đ1 | 1+000 | 42.35 |
   | cuối | 1+200 | 38.35 |

   Ở đỉnh 1+000 đặt **đường cong đứng parabol đối xứng dài L = 100** (Profile Grid View → cột *Profile Curve Length* = 100). Độ dốc: +3 % rồi −2 %.
5. **Cọc mặt cắt + trắc ngang:** Sample Lines cho `T1`, *By range of stations* từ 0 đến 120, mỗi **20 m** (7 cọc: 0, 20, 40, 60, 80, 100, 120), swath trái 10, phải 10, lấy cả `TN` và `TK`. Rồi **Create Multiple Section Views** cho cả nhóm.
6. Lưu bản vẽ (Ctrl+S).

Gợi ý: sau mỗi bài, `U` về trạng thái trước bài hoặc làm trên bản sao để các bài không ảnh hưởng nhau.

## 1. Khởi động và ribbon

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 1.1 | Mở Civil 3D 2021 với bản vẽ trống. Xem dòng lệnh. | Không có thông báo lỗi tải add-in. Nếu có dòng `Không tạo được tab ribbon C3DTools: …` → chụp lại. |
| 1.2 | Mở tab **C3DTools**. | Sáu panel, **đúng thứ tự**: **Tuyến**, **Trắc dọc**, **Trắc ngang**, **Địa hình**, **Thoát nước**, **Bản vẽ**. |
| 1.3 | Đọc các nút. | Tuyến: nút lớn **Yếu tố cong**; nút nhỏ **Mẫu TCVN**, **Bảng cong**, **Toạ độ cọc**. Trắc dọc: lớn **Bảng trắc dọc**; nhỏ **Cong đứng**. Trắc ngang: lớn **Bảng trắc ngang**; nhỏ **Xếp trang**. Địa hình: lớn **Mặt địa hình**; nhỏ **VN-2000**. Thoát nước: lớn **Bảng cống**. Bản vẽ: lớn **Chuyển font**; nhỏ **Chuẩn layer**. |
| 1.4 | Xem icon. | Mỗi panel một icon riêng: Tuyến — đường cong xanh lá; Trắc dọc — đường dốc xanh dương; Trắc ngang — rãnh hình thang cam; Địa hình — vòng đồng mức nâu; Thoát nước — ống tròn xanh ngọc; Bản vẽ — chồng layer tím. Nếu một nút chỉ có chữ, không có icon → ghi lại nút nào (phần mềm vẫn chạy được). |
| 1.5 | Rê chuột lên từng nút, chờ tooltip. | Tooltip có tiêu đề = tên nút, một câu tiếng Việt mô tả, và tên lệnh (ví dụ `CTTOADO`). Chữ Việt hiển thị đúng dấu. Ghi lại nút nào không có tooltip. |
| 1.6 | Bấm lần lượt từng nút (Esc/Hủy ngay khi hộp thoại mở). | Mỗi nút mở đúng lệnh (tên lệnh hiện trên dòng lệnh). Đang chạy `LINE` mà bấm nút thì `LINE` bị huỷ trước. |
| 1.7 | Gõ `CTHELLO`. | Dòng `C3DTools 0.1 — bản vẽ có 0 tuyến. Ví dụ lý trình: Km1+234.50` (chữ "0.1" trong câu này là bình thường). |
| 1.8 | Đổi workspace rồi quay lại. | Ghi lại tab C3DTools còn hay mất. |

## 2. Bài A — `CTTOADO` Toạ độ cọc

Bản vẽ mới. Tuyến có một cong tròn R = 50 (như Bài A của 0.2):

```
PLINE  ↵  0,0  ↵  100,0  ↵  100,100  ↵  ↵
FILLET  ↵  R  ↵  50  ↵  P  ↵  (chọn polyline)
```

Rồi **Create Alignment from Objects** trên polyline đã bo cong (bỏ tích *Add curves between tangents*), lý trình đầu 0. Tuyến dài **178.54 m**; cong: TĐ 0+050.00, P 0+089.27, TC 0+128.54. Lưu bản vẽ.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 2.1 | Chọn alignment rồi gõ `CTTOADO` (hoặc bấm **Toạ độ cọc**, rồi **Chọn trên bản vẽ…**, chọn alignment). | Hộp thoại **Bảng toạ độ cọc**. Dòng trên: `Tuyến: Alignment … (178.54 m)`. **Khoảng cách cọc (m)** = 20, ☑ **Cọc đường cong (NĐ, TĐ, P, TC, NC)**, ☐ **Điểm hình học của tuyến**, **Cao độ Z từ mặt phủ** = `(Không lấy Z)`, ☑ **X = Bắc (VN-2000)**; Xuất ra: ☑ Bảng, ☐ CSV, ☐ Excel, ☐ Điểm COGO. |
| 2.2 | Đọc bảng trong hộp thoại. | **13 dòng**, cột `STT, Tên cọc, Lý trình, X (Bắc), Y (Đông)` (không có cột Z). Thứ tự và toạ độ (X = Bắc = y bản vẽ, Y = Đông = x bản vẽ): |

   | Tên cọc | Lý trình | X (Bắc) | Y (Đông) |
   | --- | --- | --- | --- |
   | Km0 | 0+000.00 | 0.000 | 0.000 |
   | C1 | 0+020.00 | 0.000 | 20.000 |
   | C2 | 0+040.00 | 0.000 | 40.000 |
   | TĐ1 | 0+050.00 | 0.000 | 50.000 |
   | C3 | 0+060.00 | 0.997 | 59.933 |
   | C4 | 0+080.00 | 8.733 | 78.232 |
   | P1 | 0+089.27 | 14.645 | 85.355 |
   | H1 | 0+100.00 | 22.985 | 92.074 |
   | C5 | 0+120.00 | 41.502 | 99.272 |
   | TC1 | 0+128.54 | 50.000 | 100.000 |
   | C6 | 0+140.00 | 61.460 | 100.000 |
   | C7 | 0+160.00 | 81.460 | 100.000 |
   | C8 | 0+178.54 | 100.000 | 100.000 |

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 2.3 | Bỏ tích **X = Bắc (VN-2000)**. | Tiêu đề cột đổi thành `X (Đông)`, `Y (Bắc)` và hai cột số đổi chỗ (TĐ1: X 50.000, Y 0.000). Tích lại. |
| 2.4 | Bỏ tích **Cọc đường cong**, bấm **Xem trước**. | Mất TĐ1, P1, TC1; còn 10 dòng Km0, C1…C7, H1, C8 (các C đánh số lại liên tục). Dòng lệnh **không** hỏi `Giữ kết quả?` (Xem trước ở lệnh này chỉ cập nhật bảng trong hộp thoại). Tích lại, Xem trước. |
| 2.5 | Gõ `abc` vào Khoảng cách cọc. | Ô đỏ, **Áp dụng** mờ, dòng tóm tắt `Khoảng cách cọc phải là số lớn hơn 0`. Gõ `20,0` (dấu phẩy) → hết lỗi. |
| 2.6 | **Cọc thêm (lý trình)**: gõ `Km0+125.5; 0+010`. Xem trước. | Thêm hai dòng tại 0+010.00 và 0+125.50 đúng thứ tự; tên các cọc C đánh số lại. Gõ `0+500` → ô đỏ, tóm tắt `Cọc thêm: lý trình không hợp lệ hoặc nằm ngoài tuyến`. Xoá ô Cọc thêm, Xem trước. |
| 2.7 | Tích thêm CSV, Excel, Điểm COGO. **Áp dụng**, bấm điểm chèn bảng. | Dòng lệnh hỏi `Điểm chèn bảng toạ độ:`. Bảng AutoCAD **BẢNG TOẠ ĐỘ CỌC - <tên alignment>** trên layer `TOADO_BANG`, 13 dòng như 2.2. 13 điểm COGO trên layer `TOADO_DIEM`, tên dạng `TĐ1@0+050.00`. Hai file `…_TOADO.csv`, `…_TOADO.xlsx`. Dòng cuối: `Hoàn thành: bảng toạ độ 13 cọc, 13 điểm COGO.` |
| 2.8 | Mở `…_TOADO.xlsx` bằng Excel. | Mở không báo lỗi/sửa chữa; tiêu đề in đậm, dòng tiêu đề cố định; chữ **Đ** trong `TĐ1` đúng; cột số là số (căn phải). Mở CSV: chữ Việt đúng. |
| 2.9 | Chạy lại `CTTOADO` cho cùng alignment, **Áp dụng**. | Bảng và điểm COGO cũ bị thay (không trùng đôi: vẫn 13 điểm COGO). |
| 2.10 | `U` một lần. | Bảng và điểm COGO của lần chạy 2.9 biến mất cùng lúc; bảng/điểm của lần 2.7 trở lại. |
| 2.11 | Tạo một mặt phủ phẳng bất kỳ phủ nửa đầu tuyến; chọn nó ở **Cao độ Z từ mặt phủ**, Xem trước. | Có thêm cột **Z**; các cọc ngoài mặt phủ để trống Z, tóm tắt `… cọc, Z từ mặt phủ … (… cọc ngoài mặt phủ)`. |

## 3. Bài B — `CTCONGDUNG` Cong đứng

Bản vẽ thử chung (mục 0.1).

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 3.1 | Bấm **Cong đứng**, **Chọn trên bản vẽ…**, chọn profile view. | Hộp thoại **Yếu tố cong đứng**. `Trắc dọc: <tên profile view> (tuyến T1)`. **Trắc dọc thiết kế** = `TK` (profile mặt phủ `TN` không có trong danh sách). **V** = 60 km/h. Dòng Rmin: `Preset chưa có bảng cong đứng – không kiểm tra R, L, i`. Xuất ra: ☑ Khung, ☐ Bảng, ☐ CSV, ☐ Excel. |
| 3.2 | Đọc bảng. | **1 dòng**, không tô vàng: `Đỉnh` **Đ1**, `Lý trình` **1+000.00**, `CĐ đỉnh` **42.35**, `i1 (%)` **+3.00**, `i2 (%)` **-2.00**, `A (%)` **5.00**, `R` **2000.00**, `K` **100.00**, `T` **50.00**, `E` **0.63**, `Lý trình TĐ` **0+950.00**, `Lý trình TC` **1+050.00**, `Điểm cao/thấp` **1+010.00 / 41.75**, `Cảnh báo` trống. Tóm tắt: `1 đỉnh, 1 đường cong đứng`. |
| 3.3 | Nếu dòng lệnh có `Civil trả về độ dốc theo %; đã quy đổi` → chụp lại (số trong bảng vẫn phải như 3.2). Nếu i1/i2 hiện `+300.00` hoặc `+0.03` → **Không đạt**, chụp hộp thoại. | — |
| 3.4 | Bấm **Xem trước**. | Hộp thoại ẩn; phía trên đỉnh 1+000 trong profile view có một khung chữ trên layer `TD_YTC`. Dòng lệnh hỏi `Giữ kết quả?` → `Khong`: khung biến mất, hộp thoại mở lại. |
| 3.5 | Tích thêm Bảng, CSV, Excel. **Áp dụng**, bấm điểm chèn. | Hỏi `Điểm chèn bảng cong đứng:`. Khung 5 dòng, đúng thứ tự: `i1=+3.00%  i2=-2.00%` / `R=2000  K=100` / `T=50  E=0.63` / `CĐ đỉnh=42.35` / `Điểm cao=1+010.00  CĐ=41.75`. Khung nằm **đúng trên đỉnh** 1+000 của đường đỏ thiết kế (không lệch sang chỗ khác). Bảng **BẢNG YẾU TỐ CONG ĐỨNG - TK** với tiêu đề cột `Đỉnh, Lý trình, CĐ đỉnh, i1 (%), i2 (%), A (%), R, K, T, E, Lý trình TĐ, Lý trình TC, Điểm cao/thấp, Cảnh báo` và một dòng như 3.2. Files `…_CONGDUNG.csv/.xlsx`. `Hoàn thành: 1 đỉnh, 1 đường cong đứng.` |
| 3.6 | Nếu dòng lệnh có `Không lấy được toạ độ từ trắc dọc (FindXYAtStationAndElevation)…` → chụp lại và đo xem khung lệch bao nhiêu so với đỉnh. | — |
| 3.7 | Trong Profile Grid View đổi L của Đ1 thành 150; chạy lại `CTCONGDUNG`, Áp dụng. | Khung/bảng cũ bị thay (không trùng). R mới = 3000.00, T = 75.00, E = 0.94. |
| 3.8 | `U` một lần. | Khung và bảng lần 3.7 biến mất cùng lúc. |
| 3.9 | Bỏ tích mọi ô Xuất ra. | **Áp dụng** mờ, tóm tắt `Chọn ít nhất một đầu ra`. |

## 4. Bài C — `CTTRACDOC` Bảng trắc dọc

Bản vẽ thử chung, trắc dọc `TK` với L = 100 như mục 0.1.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 4.1 | Bấm **Bảng trắc dọc**, chọn profile view. | Hộp thoại **Bảng số liệu trắc dọc**. `Trắc dọc: <tên> (tuyến T1, Km0+000.00 – Km1+200.00)` (hoặc dạng lý trình tương tự). **Tự nhiên** = profile của mặt `TN`, **Thiết kế** = `TK`. **Cọc:** ● Tất cả cọc (chi tiết + cong). Khoảng cách cọc 20, Chiều cao chữ 2.5. Xuất ra ☑ Bảng. |
| 4.2 | Xem danh sách dòng (cột **Hiện**, **Dòng**, **Số lẻ**). | Theo thứ tự: Tên cọc, Khoảng cách lẻ, Khoảng cách cộng dồn, Lý trình, Cao độ tự nhiên, Cao độ thiết kế, Chênh cao, Độ dốc dọc — đều ☑; dòng cong đứng (nếu có) ☐ ở cuối. |
| 4.3 | Chọn dòng **Chênh cao**, bấm **Lên** hai lần; gõ `x` vào **Số lẻ** một dòng. | Dòng dời lên đúng; ô Số lẻ đỏ, Áp dụng mờ, tóm tắt `Số lẻ phải là số nguyên từ 0 đến 6`. Sửa lại `2`, đưa Chênh cao về chỗ cũ. |
| 4.4 | Chọn **Khoảng cách cọc** = `100`, **Xem trước**. | Bảng vẽ ngay **dưới** profile view (đường `TD_BANG`, chữ `TD_CHU`), mỗi cột cọc thẳng hàng với lý trình của trục trắc dọc. Hỏi `Giữ kết quả?` → `Co`. |
| 4.5 | Đọc vài cọc. | `Km0` (0+000.00): CĐ TN **30.00**, CĐ TK **12.35**, chênh cao **-17.65**. `H5` (0+500.00): TK **27.35**, chênh **-2.65**. `H1` sau Km1 (1+100.00): TK **40.35**, chênh **+10.35**. Cọc cuối (1+200.00): TK **38.35**, chênh **+8.35**. Không có cọc TĐ/TC (tuyến thẳng, không có cong nằm). Dòng độ dốc: `i=+3.00%` rồi `i=-2.00%`. |
| 4.6 | Chạy lại, chọn ● **Theo cọc mặt cắt (sample line)**, Áp dụng. | Bảng cũ bị thay; chỉ có 7 cọc 0, 20, …, 120, tên = tên sample line. |
| 4.7 | Tích CSV, Excel, Áp dụng. Mở `…_TRACDOC.xlsx`. | Tiêu đề `Tên cọc, Lý trình, KC lẻ, KC cộng dồn, CĐ TN, CĐ TK, Chênh cao, Dốc dọc`; mỗi cọc một dòng. |
| 4.8 | `U` một lần. | Bảng biến mất; profile view còn nguyên. |
| 4.9 | Chiều cao chữ = 10, Áp dụng. | Nếu chữ quá sát, dòng lệnh báo `… chữ trong ô gộp bị bỏ vì khoảng giữa hai cọc quá hẹp…`; chữ không chồng lên nhau. |

## 5. Bài D — `CTTRACNGANG` Bảng trắc ngang

Bản vẽ thử chung (mục 0.1), 7 section view.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 5.1 | Bấm **Bảng trắc ngang**, **Chọn trên bản vẽ…**, chọn **một** section view (cọc 0+020). | Hộp thoại **Bảng số liệu trắc ngang**. `Trắc ngang: <tên> (cọc <tên sample line>)`. **Tự nhiên** = `TN`, **Thiết kế** = `TK`. Chiều cao chữ 2.5, Xuất ra ☑ Bảng. Danh sách dòng: Cao độ tự nhiên, Cao độ thiết kế, Khoảng cách lẻ, Diện tích đào, Diện tích đắp ☑. |
| 5.2 | **Xem trước** → `Co`. | Bảng dưới trắc ngang (đường `TN_BANG`, chữ `TN_CHU`). Cột offset tại **-10, -2.5, -2, 2, 2.5, 10** (thẳng với lưới trắc ngang). CĐ tự nhiên **30.00** ở mọi cột; CĐ thiết kế **30.00 / 30.00 / 29.00 / 29.00 / 30.00 / 30.00**. Khoảng cách lẻ: **7.50, 0.50, 4.00, 0.50, 7.50**. **Diện tích đào 4.50**, **Diện tích đắp 0.00**. Tóm tắt/ dòng lệnh: `Hoàn thành: 1 trắc ngang, tổng diện tích đào 4.50, đắp 0.00.` |
| 5.3 | So với Civil 3D: Compute Materials (TN/TK) hoặc đo diện tích rãnh bằng `AREA`. | Diện tích đào của Civil 3D ≈ 4.50 m² (ghi lại số Civil 3D cho). |
| 5.4 | Chạy lại, tích **Cả nhóm (section view group)**, tích CSV + Excel, Áp dụng. | Bảng dưới **cả 7** trắc ngang; bảng cũ của 0+020 bị thay. `Hoàn thành: 7 trắc ngang, tổng diện tích đào 31.50, đắp 0.00.` File `…_TRACNGANG.xlsx`: tiêu đề `Tên cọc, Lý trình, Diện tích đào, Diện tích đắp, Chênh cao tim`, 7 dòng, đào 4.50, chênh cao tim **-1.00**. |
| 5.5 | `U` một lần. | Cả 7 bảng biến mất cùng lúc. REDO để dùng tiếp ở Bài E. |
| 5.6 | Nếu dòng lệnh có `Không lấy được toạ độ từ trắc ngang (FindXYAtOffsetAndElevation)…` → chụp lại, đo độ lệch của bảng so với lưới. Nếu cột offset không khớp lưới (ví dụ bảng bị lật trái/phải) → Không đạt. | — |

## 6. Bài E — `CTXEPTRANG` Xếp trang

Tiếp Bài D (7 section view đã có bảng).

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 6.1 | Bấm **Xếp trang**, **Chọn trên bản vẽ…**, quét chọn cả 7 section view, Enter. | Hộp thoại **Xếp trắc ngang vào tờ in**: Khổ giấy rộng (mm) (A3) **420**, Cao **297**; Lề trái 25, phải 10, trên 10, dưới 10; Số cột mỗi tờ **3**, Số hàng mỗi tờ **2**; Khoảng hở 10; Tỷ lệ 1: **200**; ☑ Khung. Tóm tắt: `7 trắc ngang → 2 tờ`. |
| 6.2 | Gõ `12,5` vào Lề trái. | Được chấp nhận (không đỏ). Gõ `0` vào Số cột → đỏ, Áp dụng mờ. Trả về 25 và 3. |
| 6.3 | **Xem trước** → quan sát → `Co`. | Hai khung tờ (đơn vị mét, tờ 84 × 59.4) trên layer `TN_KHUNG`, tờ 2 nằm bên phải tờ 1. Tờ 1: 6 trắc ngang 0, 20, 40 (hàng trên, trái → phải), 60, 80, 100 (hàng dưới); tờ 2: trắc ngang 120. **Bảng CTTRACNGANG đi theo trắc ngang của nó.** Tên tờ: `TRẮC NGANG – Tờ 1/2 – Km0+000.00 … Km0+100.00` và `TRẮC NGANG – Tờ 2/2 – Km0+120.00`. Dòng cuối có `khung polyline` (hoặc `khung từ A3.dwg` nếu có file `Resources\A3.dwg`). |
| 6.4 | Nếu dòng lệnh báo `… trắc ngang lớn hơn ô của tờ…` → ghi lại số lượng. | — |
| 6.5 | Kiểm tra trắc ngang sau khi dời: bấm vào một section view, xem lưới, nhãn, đường mặt cắt đi cùng. Chạy lại `CTTRACNGANG` cho trắc ngang đó. | Không có phần nào bị bỏ lại chỗ cũ; `CTTRACNGANG` vẽ bảng đúng dưới vị trí mới. |
| 6.6 | Chạy lại `CTXEPTRANG` với Số cột = 4, Áp dụng. | Khung cũ bị xoá, tờ 1 bắt đầu **đúng chỗ tờ 1 cũ**; `7 trắc ngang → 1 tờ` (4 × 2 = 8 ô). |
| 6.7 | `U` một lần. | Trắc ngang và bảng về vị trí trước 6.6, khung của 6.6 biến mất. |
| 6.8 | Nếu dòng lệnh báo `Không dời được trắc ngang (SectionView.Location)…` → chụp lại; bản vẽ phải **không đổi**. | — |

## 7. Bài F — `CTMATDIA` Mặt địa hình

Bản vẽ mới, lưu lại.

**Mặt `NGHIENG` (ghi cao độ):** 4 POINT: `POINT 0,0,100`, `POINT 100,0,110`, `POINT 100,100,110`, `POINT 0,100,100`; tạo TIN surface `NGHIENG`, Definition → Drawing Objects → Points, chọn 4 điểm. Style hiển thị đường đồng mức 1 m / 5 m. Vẽ `LINE 5,50 ↵ 95,50 ↵ ↵`.

**Mặt `RAC` (xoá tam giác):** 9 điểm lưới `POINT` (200,0,10), (210,0,10), (220,0,10), (200,10,10), (210,10,10), (220,10,10), (200,20,10), (210,20,10), (220,20,10) và 1 điểm xa (400,10,10). TIN surface `RAC` từ 10 điểm. Các tam giác nối tới điểm xa có cạnh dài hơn 50 m.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 7.1 | Bấm **Mặt địa hình**. | Hộp thoại **Mặt địa hình**, ô **Mặt phủ (TIN)** liệt kê `NGHIENG`, `RAC`. Hai thẻ **Xoá tam giác dài** và **Ghi cao độ đồng mức**. |
| 7.2 | Chọn `RAC`, thẻ **Xoá tam giác dài**: ☑ **Xoá tam giác có cạnh dài hơn** `50` m. Bấm **Đếm**. | Dòng bên phải nút: `x / y tam giác bị loại, z cạnh sẽ xoá` (y = tổng số tam giác; x > 0). Bản vẽ không đổi. Tóm tắt `Xoá tam giác có cạnh > 50 m trên RAC`. |
| 7.3 | **Xem trước**. | Dòng lệnh: `Đã xoá z cạnh (x / y tam giác bị loại) trên RAC.` Mặt phủ chỉ còn lưới 20 × 20 m, **không** còn tam giác tới điểm xa; lưới không bị thủng. Hỏi `Giữ kết quả?` → `Khong`: mặt phủ trở lại như cũ. |
| 7.4 | Nếu thay vào đó có `TinSurface.DeleteLines không chạy được (…); dùng cách thay thế.` và `Đã đặt chiều dài tam giác lớn nhất 50 m…` → chụp lại, ghi kết quả hình học. | — |
| 7.5 | Vẽ polyline kín quanh 4 điểm lưới (200,0)–(210,0)–(210,10)–(200,10) rộng ra 1 m; **Chọn trên bản vẽ…** chọn nó. Bỏ tích cạnh dài. Đếm rồi Áp dụng. | Chỉ các tam giác có đỉnh ngoài ranh giới bị xoá; còn lại ô 10 × 10 m bên trong. `U` một lần → mặt phủ trở lại. Nút **Bỏ ranh giới** xoá ranh giới đã chọn. |
| 7.6 | Chọn `NGHIENG`, thẻ **Ghi cao độ đồng mức**. | **Đồng mức chính** / **con** lấy từ kiểu mặt phủ (chữ xám `theo kiểu hiển thị của mặt phủ`), ví dụ 5 và 1. Chiều cao chữ 2.5, nhãn cách nhau ít nhất 5 m, ☑ **Ghi cả đồng mức con**. |
| 7.7 | **Chọn trên bản vẽ…** → chọn line (5,50)–(95,50), Enter. **Áp dụng**. | Tóm tắt trước đó: `1 đường, đồng mức 1 m (chính 5 m) trên NGHIENG`. Dòng lệnh: `9 nhãn cao độ trên 1 đường (… đường đồng mức).` 9 TEXT trên layer `DH_CAODO` tại x = 10, 20, …, 90 (y = 50), ghi **101 … 109**, chữ xoay dọc theo đường đồng mức (dọc bản vẽ), **đọc được** (không lộn ngược). Không còn polyline đồng mức thừa nào trong bản vẽ. |
| 7.8 | Bỏ tích **Ghi cả đồng mức con**, chạy lại, Áp dụng cho cùng line. | Nhãn cũ bị thay; chỉ còn **105**. |
| 7.9 | `U` một lần. | Nhãn của 7.8 biến mất, nhãn 7.7 trở lại. |

## 8. Bài G — `CTVN2000` Chuyển kinh tuyến trục

Bản vẽ mới. `POINT 600000,2300000` và một TEXT bất kỳ gần đó (`TEXT 600010,2300010 ↵ 2 ↵ 0 ↵ Thu ↵`).

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 8.1 | Chọn POINT, gõ `CTVN2000`. | Hộp thoại **Chuyển kinh tuyến trục VN-2000**. `Đối tượng: 1 đối tượng, 1 điểm`. ● **Đối tượng chọn (điểm, line, polyline, cung, block, text)**. **Từ kinh tuyến trục** `105°45'`, Múi 3°; **Sang kinh tuyến trục** `106°15'`, Múi 3°. Có ghi chú `Kinh tuyến theo tỉnh trong preset chưa được đối chiếu văn bản – hãy kiểm tra trước khi dùng.` |
| 8.2 | Đọc bảng. | Một dòng `POINT <handle>`: **X trước 600000.000**, **Y trước 2300000.000**, **X sau 547944.829**, **Y sau 2299770.840**, **Dịch chuyển (m) 52055.675** (sai lệch cho phép ±0.001). Tóm tắt `1 điểm: … → …`. |
| 8.3 | Gõ `106°75'` vào ô Sang. | Ô đỏ, Áp dụng mờ, tóm tắt `Kinh tuyến đích không hợp lệ (ví dụ 106°15')`. Gõ `106 15` → hợp lệ. Gõ `105.75` → tóm tắt `Kinh tuyến và múi chiếu nguồn, đích trùng nhau`, Áp dụng mờ. Đổi lại `106°15'`. |
| 8.4 | Mở danh sách ô Từ: chọn **TP. Hồ Chí Minh**. | Được hiểu là 105°45' (bảng không đổi). Chọn lại `105°45'` bằng cách gõ. |
| 8.5 | **Xem trước**. | Dòng lệnh: `Đã chuyển 1 đối tượng. Dịch chuyển lớn nhất 52055.675 m · biến dạng tỷ lệ lớn nhất … ppm · xoay …"` (ghi lại hai số ppm và giây). Hỏi `Giữ kết quả?` → `Co`. `Hoàn thành: 1 điểm: …`. `ID` điểm → X = 547944.829, Y = 2299770.840. |
| 8.6 | Chọn lại điểm, gõ `CTVN2000`, **Từ** `106°15'`, **Sang** `105°45'`, Áp dụng. | `ID` → **X = 600000.000, Y = 2300000.000** (sai lệch ≤ 0.001 m = 1 mm). |
| 8.7 | `U` một lần. | Điểm về 547944.829, 2299770.840 (chỉ lần 8.6 bị huỷ). |
| 8.8 | Chọn cả POINT và TEXT, chuyển 105°45' → 106°15'. | Dòng lệnh có `0 block, 1 TEXT/MTEXT được xoay thêm theo chênh lệch góc hội tụ kinh tuyến (tới …").`; TEXT dời theo điểm và xoay rất nhẹ. |
| 8.9 | Nếu bản vẽ có điểm COGO: chọn ● **Tất cả điểm COGO**, Áp dụng; `LIST` một điểm. | Easting/Northing đổi như 8.2 (với điểm cùng toạ độ). Điểm COGO bị khoá → `Không chuyển được đối tượng …` và điểm đó giữ nguyên. |
| 8.10 | Thử múi: Từ 105°00' Múi 6°, Sang 105°45' Múi 3°, với `POINT 588758.1399,2325536.1593`. | X sau **510827.117**, Y sau **2326000.143** (±0.001). |

## 9. Bài H — `CTBANGCONG` Bảng cống

Bản vẽ thử chung (mục 0.1). Tạo **pipe network** `CONG` (Home → Pipe Network → Pipe Network Creation Tools, chỉ ống, không hố ga) với hai ống tròn:

- Ống 1: từ (300, −6) đến (300, 6) — vuông góc tuyến tại lý trình 0+300, dài 12 m. Pipe Properties: Start Invert **28.06**, End Invert **28.00**.
- Ống 2: từ (497, −6) đến (503, 6) — chéo, qua tim tại 0+500, dài 13.42 m.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 9.1 | Chọn alignment `T1`, gõ `CTBANGCONG`. | Hộp thoại **Bảng thống kê cống**. `Tuyến: Alignment T1 (1200.00 m)`. **Mạng cống**: ☑ `CONG`. **Cao độ mặt đất**: chọn `TN`. Ghi chú `Preset chưa có PipeRules: không kiểm tra độ dốc, chiều sâu chôn.` Xuất ra ☑ Bảng. |
| 9.2 | Bấm **Xem trước** (cập nhật bảng trong hộp thoại, không hỏi `Giữ kết quả?`). Đọc bảng. | 2 dòng theo lý trình. Dòng 1: Tên **C1**, Lý trình **Km0+300.00**, Vị trí **Tim**, Góc chéo **0.0°**, Loại (tự đặt theo hình dạng ống), Khẩu độ theo ống, Dài **12.00**, **CĐ đáy TL 28.06**, **CĐ đáy HL 28.00** (hoặc ngược lại tuỳ chiều dòng chảy — ghi lại), Dốc **0.50**, Chôn min = 30 − (28.06 + chiều cao ngoài của ống) — ghi lại số. Dòng 2: **C2**, **Km0+500.00**, Tim, Góc chéo **26.6°**, Dài **13.42**, Ghi chú **Chéo 26.6°**. |
| 9.3 | **Quan trọng:** so CĐ đáy với Pipe Properties (Start/End Invert Elevation). | Nếu bảng ghi **đáy ống** = Invert → Đạt. Nếu bảng ghi cao hơn Invert đúng **nửa đường kính trong** (đó là cao độ tim ống) → ghi "Z là tim ống": cần đặt `Culvert.EndpointIsCentreline = true` trong preset. |
| 9.4 | Sửa ô **Tên** dòng 2 thành `CB1`, ô **Loại** chọn/gõ `Cống bản`, **Ghi chú** `Cống cũ`. Tích CSV, Excel. **Áp dụng**, bấm điểm chèn. | Hỏi `Điểm chèn bảng thống kê cống:`. Bảng **BẢNG THỐNG KÊ CỐNG - T1** trên layer `BANGCONG_BANG` giữ các sửa đổi. Files `…_BANGCONG.csv/.xlsx`. `Hoàn thành: bảng thống kê 2 cống.` |
| 9.5 | Bỏ tích `CONG`. | Tóm tắt `Chọn ít nhất một mạng cống`, Áp dụng mờ. |
| 9.6 | Chạy lại, Áp dụng; rồi `U` một lần. | Bảng cũ bị thay (không trùng); `U` xoá bảng mới, bảng cũ trở lại. |

## 10. Bài I — `CTFONT` Chuyển font

Bản vẽ mới, lưu lại. Tạo hai TEXT (dán chuỗi từ tài liệu này vào lệnh `TEXT`, không gõ tay):

1. `Céng hoµ x· héi chñ nghÜa ViÖt Nam` (chữ TCVN3 — trên máy không có font .VnTime sẽ hiện "lạ", đó là bình thường).
2. `Ø600`

Nếu máy có font `.VnTime`, tạo kiểu chữ `VNTIME` dùng font đó và đặt TEXT 1 sang kiểu này (khuyến nghị); không có thì để kiểu Standard.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 10.1 | Không chọn gì, bấm **Chuyển font**. | Hộp thoại **Chuyển mã font tiếng Việt**: `Toàn bản vẽ`, ● **Toàn bản vẽ**. **Bảng mã nguồn** `Tự nhận dạng`, **Chuyển sang** `Unicode`. ☑ **Đổi font kiểu chữ (Text Style) sang Arial**, ☐ **Chuyển cả chữ trong định nghĩa block**. |
| 10.2 | Đọc `Nhận dạng:` và bảng **Loại / Trước / Sau**. | `Nhận dạng:` `DBText: TCVN3 1, Unicode 1` (chuỗi `Ø600` được nhận là Unicode, **không** phải TCVN3). Bảng **1 dòng**: DBText, Trước `Céng hoµ x· héi chñ nghÜa ViÖt Nam`, Sau **`Cộng hoà xã hội chủ nghĩa Việt Nam`**. **Không có dòng `Ø600`.** Tóm tắt `1 chuỗi sẽ được chuyển`. Nếu Nhận dạng ra `Unicode 1` (không nhận ra TCVN3) → ghi lại kiểu chữ/font của TEXT 1, rồi thử lại với kiểu `.VnTime`. |
| 10.3 | **Áp dụng**. | `Đã chuyển 1 đối tượng…` (thêm `, dùng 1 kiểu chữ *_UNI (font Arial)` nếu kiểu chữ của TEXT 1 chưa dùng Arial: TEXT 1 chuyển sang kiểu `<tên kiểu>_UNI`). TEXT 1 hiện đúng **Cộng hoà xã hội chủ nghĩa Việt Nam** với font Arial; TEXT 2 vẫn **Ø600**, kiểu chữ không đổi. |
| 10.4 | `U` một lần. | TEXT 1 trở lại chuỗi TCVN3 và kiểu chữ cũ (kiểu `*_UNI` mới tạo, nếu có, cũng mất). |
| 10.5 | Chọn riêng TEXT 1, gõ `CTFONT`; đổi **Chuyển sang** = `VNI Windows`. | `1 đối tượng đã chọn`, ● **Đối tượng đã chọn**; ô **Đổi font kiểu chữ** mờ (chỉ dùng khi chuyển sang Unicode); Sau = chuỗi VNI bắt đầu bằng `Coäng` (dạng chữ "lạ" là đúng). Đổi lại **Chuyển sang** = `Unicode` (lựa chọn được nhớ cho lần sau), rồi Hủy. |
| 10.6 | Đổi phạm vi sang **Toàn bản vẽ** trong hộp thoại. | Bảng trống, tóm tắt `Bấm Xem trước để quét lại`; bấm **Xem trước** → quét lại, bảng hiện lại. |
| 10.7 | Thêm một MTEXT, một thuộc tính block, một kích thước có chữ ghi đè (TCVN3), một MLeader, một ô bảng AutoCAD chứa chữ TCVN3; chạy lại trên toàn bản vẽ, Áp dụng. | Tất cả đổi đúng; ghi lại loại nào không đổi. Ô/đối tượng có **field** → `Bỏ qua … đối tượng có field.` và giữ nguyên. |

## 11. Bài J — `CTLAYER` Chuẩn layer

Bản vẽ mới. Tạo layer `KS_COC` (3 LINE), `KS_TEXT` (2 TEXT), `RONG` (không có đối tượng), layer `KHOA` bị **khoá** (1 LINE). Tạo block `B1` chứa 1 LINE trên `KS_COC` và 1 LINE trên layer `0`, chèn 1 lần.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 11.1 | Bấm **Chuẩn layer**. | Dòng lệnh `Preset chưa có LayerMap: nhập layer đích trong bảng rồi bấm "Lưu vào preset".`. Hộp thoại **Chuẩn hoá layer theo preset**: `Toàn bản vẽ: … layer · preset: TCVN 4054:2005`. Cột **Layer**, **Số đối tượng**, **Layer đích**, **Màu**. `KS_COC` = 3 (block definition không cộng khi chưa tích Áp dụng cho block). |
| 11.2 | **Chọn trên bản vẽ…**, bấm một TEXT. | Dòng `KS_TEXT` được chọn trong bảng. |
| 11.3 | `KS_COC` → đích `TK_COC`, màu `1`; `KS_TEXT` → `TK_CHU`, màu `7`; `KHOA` → `TK_COC`. | Dòng chuyển sang chữ đậm; tóm tắt `3 layer sẽ chuyển (6 đối tượng)`. Gõ màu `300` → ô đỏ, tóm tắt `Tên layer hoặc màu không hợp lệ (ô tô đỏ)`, Áp dụng mờ; sửa lại `1`. |
| 11.4 | Tích ☑ **Áp dụng cho block** (số đối tượng `KS_COC` thành 4, tóm tắt `(7 đối tượng)`), ☑ **Xoá layer rỗng**. **Xem trước**. | Hỏi `Giữ kết quả?` → `Co`. `Đã chuyển … đối tượng từ 3 layer, tạo 2 layer mới, xoá … layer rỗng.` |
| 11.5 | Kiểm tra. | Layer `TK_COC` (đỏ), `TK_CHU` (trắng) được tạo. LINE trên `KHOA` (layer khoá) đã sang `TK_COC`. Trong block B1: LINE `KS_COC` → `TK_COC`, LINE layer 0 **vẫn** ở layer 0. `RONG`, `KS_COC`, `KS_TEXT`, `KHOA` bị xoá (rỗng); layer `0`, `Defpoints`, layer hiện hành **còn**. |
| 11.6 | `U` một lần. | Mọi đối tượng về layer cũ; layer bị xoá trở lại; `TK_COC`, `TK_CHU` mất. |
| 11.7 | Lưu bản vẽ. Chạy lại, nhập như 11.3, bấm **Lưu vào preset**. | Chữ xám `Đã lưu 3 quy tắc vào <tên bản vẽ>.c3dtools.json.`; file có cạnh DWG. Đóng, chạy lại `CTLAYER`: cột Layer đích đã điền sẵn từ preset, dòng lệnh không còn báo "Preset chưa có LayerMap". |

## 12. Bài K — Giao diện chung (làm với 2–3 lệnh bất kỳ)

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 12.1 | Windows tiếng Việt: gõ `2,5` vào một ô số (chiều cao chữ, khoảng cách cọc…). | Được chấp nhận. Mọi số hiển thị/ghi ra CSV dùng dấu **chấm**. |
| 12.2 | Kéo rộng hộp thoại, đóng, mở lại cùng lệnh. | Mở lại đúng kích thước vừa kéo. Các ô Xuất ra đã tích vẫn giữ. |
| 12.3 | **Esc** trong hộp thoại. | Đóng, không vẽ gì, dòng lệnh không báo lỗi. |
| 12.4 | Bấm **Chọn trên bản vẽ…** rồi Esc khi đang chọn. | Hộp thoại mở lại, giá trị đã nhập còn nguyên. |
| 12.5 | DPI 125 % / 150 %. | Chữ rõ, nút không bị cắt. Chụp ảnh hộp thoại **Bảng thống kê cống** (rộng nhất). |
| 12.6 | Chạy từng lệnh trên **bản vẽ trống** (không có tuyến/mặt phủ/mạng cống). | Hộp thoại mở với tóm tắt kiểu `Chưa chọn tuyến`, `Bản vẽ không có mặt phủ TIN`, `Bản vẽ không có mạng cống`; **không** có hộp lỗi "Unhandled exception". |

## 13. Gỡ cài đặt

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 13.1 | Đóng Civil 3D, bấm đúp `uninstall.cmd`, mở lại Civil 3D. | Không còn tab C3DTools; `CTTOADO` → `Unknown command`. |

## Chưa kiểm tra trên Windows

Các mục dưới đây mới chỉ được kiểm tra khi biên dịch (tên hàm có trong thư viện Civil 3D 2021), **chưa chạy thật**. Mỗi mục đều có đường thoát: nếu hỏng, lệnh phải báo một dòng tiếng Việt và **bản vẽ không đổi**. Người thử hãy đánh dấu ☑ Chạy đúng / ☒ Hỏng (kèm dòng lệnh).

**Ribbon**
- [ ] Tooltip (`RibbonToolTip`: tiêu đề, câu mô tả, tên lệnh) hiện đúng, chữ Việt đúng dấu
- [ ] Icon 16/32 px của sáu panel hiện đúng (nếu hỏng: nút chỉ có chữ)

**CTTOADO**
- [ ] `Alignment.PointLocation` cho toạ độ đúng tại mọi cọc (so với Alignment Grid View / `ID`)
- [ ] Cọc cong TĐ/P/TC đọc từ alignment có sẵn (cùng cách `CTYTC` chế độ Chỉ cắm cọc)
- [ ] `Surface.FindElevationAtXY` ngoài mặt phủ → ô Z trống, không lỗi
- [ ] Tạo điểm COGO (`CogoPoints.Add`), đặt tên `…@lý trình`; tên trùng → báo `… điểm COGO không đặt được tên…`
- [ ] Chạy lại xoá điểm COGO cũ (`CogoPoints.Remove`)

**CTCONGDUNG**
- [ ] Đơn vị độ dốc Civil trả về (`GradeIn/GradeOut`: 0.03 hay 3.0) — xem 3.3
- [ ] `ProfileView.FindXYAtStationAndElevation`: khung đúng trên đỉnh (nếu không, cách thay thế theo gốc + tỷ lệ đứng)
- [ ] Đọc cong tròn và parabol không đối xứng (`ProfileCircular`, `ProfileParabolaAsymmetric`)

**CTTRACDOC**
- [ ] `Profile.ElevationAt` ngoài phạm vi trắc dọc → ô trống, không lỗi
- [ ] Đọc cọc mặt cắt (`SampleLineGroup.GetSampleLineIds`, `SampleLine.Station`)
- [ ] Bảng thẳng hàng với trục lý trình (cùng `FindXYAtStationAndElevation`)

**CTTRACNGANG**
- [ ] `SectionView.ParentEntityId` là sample line (nếu không, tìm qua `SampleLine.GetSectionViewIds`)
- [ ] `SectionPoint.Location`: X = offset, Y = cao độ (kiểm tra cột offset/cao độ ở 5.2)
- [ ] `SectionView.FindXYAtOffsetAndElevation` (cách thay thế: theo `Location` của trắc ngang — chưa rõ `Location` là điểm nào)
- [ ] **Cả nhóm**: `SampleLineGroup.SectionViewGroups` → `GetSectionViewIds`

**CTXEPTRANG**
- [ ] Dời trắc ngang bằng `SectionView.Location` (lưới, nhãn, mặt cắt đi cùng)
- [ ] Kích thước trắc ngang (`GeometricExtents`) đúng để xếp ô
- [ ] Khung từ `Resources\A3.dwg` (khi có file này; bản 0.3 chưa kèm file, dùng khung polyline)

**CTMATDIA**
- [ ] `TinSurface.GetTriangles(false)` chỉ trả về tam giác đang hiện
- [ ] `TinSurface.DeleteLines` xoá đúng cạnh và `U` hoàn tác được
- [ ] Cách thay thế: `BoundariesDefinition.AddBoundaries` (ranh giới Outer) / `BuildOptions.MaximumTriangleLength` + `Rebuild`
- [ ] `TinSurface.ExtractContours` trả về polyline có `Elevation` đúng; polyline tạm bị xoá sau khi đọc
- [ ] Đọc khoảng cao đều từ kiểu mặt phủ (`SurfaceStyle.ContourStyle.MajorContourInterval/MinorContourInterval`)

**CTVN2000**
- [ ] Ghi `CogoPoint.Easting/Northing` (điểm bị khoá → báo, giữ nguyên)
- [ ] `CivilDocument.GetAllPointIds` liệt kê đủ điểm COGO
- [ ] Block, TEXT, MTEXT dời + xoay bằng `TransformBy`; thuộc tính block đi theo
- [ ] Polyline2d/3d: ghi đỉnh

**CTBANGCONG**
- [ ] `Pipe.StartPoint/EndPoint.Z` là **đáy** hay **tim** ống (xem 9.3; preset `Culvert.EndpointIsCentreline`)
- [ ] `Alignment.StationOffset` với ống ngoài hai đầu tuyến (ném lỗi hay trả lý trình bị kẹp) — ống đó phải bị bỏ qua có báo
- [ ] Chiều dòng chảy (`FlowDirection`) → cột thượng lưu/hạ lưu đúng
- [ ] Ống nối hố ga: cao độ mặt đất lấy từ `Structure.RimElevation`
- [ ] `CivilDocument.GetPipeNetworkIds` liệt kê đủ mạng

**CTFONT**
- [ ] MLeader: ghi lại `MText` sau khi đổi (`MLeader.MText = …`)
- [ ] Thuộc tính nhiều dòng (`AttributeReference.MTextAttribute`), định nghĩa thuộc tính (`MTextAttributeDefinition`, `Prompt`)
- [ ] Ô bảng: đổi chữ và kiểu chữ (`Cell.TextStyleId`); ô có field bị bỏ qua (`CellContentTypes.Field`)
- [ ] Kiểu chữ SHX → TrueType Arial (xoá `FileName`, đặt `FontDescriptor`)
- [ ] Chữ ghi đè kích thước (`Dimension.DimensionText`)

**CTLAYER**
- [ ] Chuyển đối tượng trên layer **khoá** (mở ghi với `forceOpenOnLockedLayer`)
- [ ] `Database.Purge` + xoá layer rỗng; layer 0, Defpoints, layer hiện hành, layer xref không bị xoá
- [ ] Block động: chỉ các block ẩn danh `*U` của block động được xử lý

## Bảng kết quả (gửi lại)

Sao chép, điền Đạt/Không đạt và ghi chú ngắn.

| Bài | Kết quả | Ghi chú / số liệu thực tế |
| --- | --- | --- |
| 0 Cài đặt, PackageContents | | 14 lệnh? AppVersion? |
| 1 Ribbon & khởi động | | 6 panel đúng thứ tự? icon? tooltip? |
| A CTTOADO | | 13 cọc? toạ độ P1, TC1 = ? Excel mở được? |
| B CTCONGDUNG | | R/T/E = ? có dòng "độ dốc theo %"? khung đúng đỉnh? |
| C CTTRACDOC | | chênh cao Km0 = ? bảng thẳng trục? |
| D CTTRACNGANG | | đào = ? (Civil 3D = ?) cột offset đúng? |
| E CTXEPTRANG | | 2 tờ? trắc ngang đi kèm bảng? U hoàn tác? |
| F CTMATDIA | | DeleteLines hay cách thay thế? 9 nhãn 101…109? |
| G CTVN2000 | | X/Y sau = ? quay về sai lệch = ? ppm = ? |
| H CTBANGCONG | | Z = đáy hay tim? góc chéo = ? |
| I CTFONT | | nhận dạng TCVN3? Ø600 giữ nguyên? loại nào không đổi? |
| J CTLAYER | | layer khoá? layer 0 trong block? purge? |
| K Giao diện | | DPI = ?; ảnh chụp |
| Chưa kiểm tra trên Windows | | số mục ☑ / ☒ |
| 13 Gỡ cài đặt | | |

**Gửi kèm:** nội dung dòng lệnh (F2, chọn hết, copy text tốt hơn ảnh) của từng bài, file DWG bản vẽ thử chung (mục 0.1) và các bản vẽ Bài A, F, I, J (nếu được), các file CSV/XLSX sinh ra, ảnh hộp thoại, tên file zip đã cài, thông tin máy ở mục 0.6, và danh sách **Chưa kiểm tra trên Windows** đã đánh dấu. Bất kỳ hộp thoại lỗi nào của AutoCAD (Unhandled exception) → chụp, bấm **Continue**, ghi bài và bước đang làm.

> **Ghi chú — các lệnh của bản 0.2**
>
> Tài liệu này chỉ gồm các lệnh mới của 0.3 / 0.4. Ba lệnh yếu tố cong nằm của bản 0.2 — `CTYTC`, `CTYTCMAU`, `CTYTCBANG` (panel **Tuyến**) — vẫn thử theo [test-plan-0.2.md](test-plan-0.2.md). Khi thử 0.2 trên bản cài 0.3, bỏ qua bước 1.2 của tài liệu đó (ribbon nay có sáu panel, xem mục 1 ở trên).
