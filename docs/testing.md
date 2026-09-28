# Cài C3DTools để thử nghiệm

Yêu cầu: Windows 64-bit, Civil 3D 2021 (update 2021.3 trở lên).

1. Tải file `C3DTools-<phiên bản>.zip` ở mục **Assets** của bản mới nhất: https://github.com/baobeta/civil-utils/releases
2. Giải nén (chuột phải → Extract All).
3. Đóng Civil 3D, bấm đúp `install.cmd`.
4. Mở Civil 3D 2021, gõ lệnh `CTHELLO`.

Bộ lệnh mới được hướng dẫn từng bước tại [manual-test-command-suite.md](manual-test-command-suite.md).

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

## Các lệnh mới trong 0.5 — làm tuyến kiểu AND Design

Bốn lệnh dưới đây đi theo trình tự làm tuyến quen thuộc: tạo tuyến → bố trí cong, siêu cao → phát sinh cọc → đánh tên cọc. Tất cả nằm ở panel **Tuyến**.

### Tạo tuyến — `CTTUYEN`

1. Gõ `CTTUYEN`. Nhập **Tên đường tuyến**, **Tỉ lệ bình đồ 1/…** (chiều cao chữ cọc, khung cong sẽ là 2.5 mm theo tỉ lệ này), **Mô tả**, **Lý trình đầu**, **Vận tốc thiết kế**.
2. Chọn **Kiểu Alignment**, **Bộ nhãn**, **Layer**; chọn mặt phủ ở **Trắc dọc tự nhiên từ** nếu muốn có trắc dọc tự nhiên `<tên tuyến>-TN`.
3. **Tệp mặt cắt (DWG)** `…`: chọn một bản vẽ có sẵn các Assembly (mặt cắt ngang mẫu). **Tải toàn bộ mặt cắt trong tệp** nhập tất cả; bỏ chọn thì chỉ nhập mặt cắt chọn ở **Mặt cắt cho tuyến**.
4. Có tim tuyến bằng **Theo polyline…** (chọn polyline) hoặc **Chỉ điểm…** (bấm lần lượt các đỉnh, `Lui` để bỏ đỉnh vừa chọn, Enter để kết thúc).
5. **Xem trước** → `Giữ kết quả? [Co/Khong]`, hoặc **Áp dụng**. Nếu để **Bố trí cong ngay sau khi tạo**, `CTYTC` mở ngay trên alignment mới ở chế độ **Thiết kế lại cong**.

### Yếu tố cong, siêu cao, mở rộng — `CTYTC` (bổ sung)

Bảng có thêm cột **V** (tốc độ tại đỉnh, để trống = V của tuyến) và **SC** (độ dốc siêu cao). Bên phải là khung chi tiết của dòng đang chọn, giống hộp "Hiệu chỉnh yếu tố cong và thông số siêu cao":

- **< Trước / Tiếp >** chuyển đỉnh. **Góc chuyển hướng**: sửa góc tại đỉnh, phần tuyến phía sau xoay theo (chỉ khi thiết kế từ polyline; Áp dụng sẽ dời các đỉnh của polyline).
- **Rmin tối thiểu / Rmin thông thường** + **Tra yếu tố cong**: lấy R, L, W theo bảng TCVN trong preset cho riêng đỉnh này. **A1/A2**: nhập thông số clothoid thay cho L. **Rmax / Lmax**: bán kính / chiều dài chuyển tiếp lớn nhất còn vừa giữa hai đường cong bên cạnh.
- **Không bố trí / Siêu cao**, **i max**, **Bố trí theo chuyển tiếp**, **Mở rộng bụng / lưng**, **Nối đầu / Nối cuối** (chiều dài nối, lệch ngoài), **Tra siêu cao** (lấy isc từ bảng siêu cao của preset và chiều dài nối từ bảng Lct, một nửa đặt trên đường thẳng).
- Ô đầu ra mới: **Polyline các đoạn nối** (mép mặt đường mở rộng trên layer `YTC_MEP`), **Siêu cao → Alignment**, **Dồn dịch đỉnh trắc dọc phía sau** (chỉ khi thiết kế lại cong trên alignment). Khi có siêu cao, ô **CSV/Excel** xuất thêm `<tên bản vẽ>_SIEUCAO.csv/.xlsx`.
- Ô **B/2** (bề rộng nửa mặt đường) và **in (%)** (dốc ngang thường) ở hàng công cụ.

### Phát sinh cọc — `CTPHATCOC`

1. Gõ `CTPHATCOC`, chọn alignment. Chọn **Nhóm cọc** có sẵn hoặc **(Nhóm mới)** và đặt tên.
2. **Phát sinh**: nhập **Từ / Tới khoảng dồn** (hoặc bấm `…` để chỉ điểm trên tuyến), **Khoảng cách trong đoạn thẳng** và **trong đoạn cong**. Cọc chi tiết đặt ở lý trình chẵn theo khoảng cách; luôn có cọc H, Km, NĐ/TĐ/P/TC/NC. Cọc ngoài khoảng giữ nguyên; tên toàn nhóm được đánh lại theo quy tắc mặc định.
3. **Chèn**: nhập danh sách lý trình hoặc **Chỉ điểm…**; **Kiểu cọc phụ** đặt tên C5a, C5b theo cọc đứng trước, bỏ chọn thì tên là lý trình.
4. **Xem trước** hiện danh sách cọc (mới / đổi tên / giữ); **Áp dụng** ghi Sample Line trong một bước undo.

### Đánh lại tên cọc — `CTDANHCOC`

1. Gõ `CTDANHCOC`, chọn một cọc (Sample Line) hoặc alignment (nếu có nhiều nhóm cọc, chọn nhóm trên dòng lệnh).
2. Các tuỳ chọn giống AND Design: **Từ / Tới cọc**, **Để lại các cọc có tiếp đầu**, **Đánh lại cọc cắm cong, siêu cao** + **Số thứ tự đỉnh đầu**, **Tên cọc theo kiểu lý trình**, **Tiếp đầu của cọc**, **Số thứ tự cọc đầu**, **Không tạo cọc H**, **Cọc H liên tục** (số C đánh liền qua cọc H), **Thứ tự cọc quay lại theo KM**, **Không đánh số quay lại khi TT>=100**.
3. Bảng bên dưới cập nhật ngay khi đổi tuỳ chọn (dòng vàng = đổi tên). **Xem trước** / **Áp dụng** như các lệnh khác.

### Quy tắc cọc (từ 0.5.8)

- **Cọc Km** mỗi 1000 m: `Km0` tại 0+000.00, `Km1` tại 1+000.00…
- **Cọc H** mỗi 100 m: `H1`…`H9`, lặp lại sau mỗi cọc Km.
- **Cọc C** theo khoảng cách, từ **Cọc C bắt đầu từ lý trình**: khoảng cách 20 m bắt đầu ở Km0+020, khoảng cách 100 m bắt đầu ở Km0+050 (50, 150, 250…). Đánh số liên tục `C1, C2…` tới hết tuyến, không quay lại sau Km (bật **Thứ tự cọc quay lại theo KM** trong `CTDANHCOC` nếu cần).
- Cọc C và cọc H không đặt trùng cọc Km. Tại các lý trình chẵn trăm có ba lựa chọn (từ 0.5.9):
  - **Cọc C bỏ qua vị trí cọc H** bật (mặc định): `C4 (80), H1 (100), C5 (120)`.
  - **Cọc C bỏ qua vị trí cọc H** tắt: vị trí cọc H vẫn được đếm, `C4 (80), H1 (100), C6 (120)`.
  - **Không tạo cọc H**: `C4 (80), C5 (100), C6 (120)`.
- Từ 0.5.10: **Khoảng cách cọc C** là ô chọn (20, 100, hoặc gõ số khác). Cọc C chạy liên tục tới hết tuyến, **qua cả đường cong**. **Chêm thêm cọc trong đoạn cong** (mặc định tắt) thêm cọc ở các lý trình chẵn theo khoảng cách riêng giữa NĐ và NC; các cọc chủ yếu luôn có.

### Cọc đặc biệt tại đường cong (từ 0.5.11)

`CTPHATCOC` tạo tại mỗi đường cong của alignment các cọc **TĐ** (tiếp đầu), **P** (giữa cung), **TC** (tiếp cuối), và thêm **NĐ**, **NC** nếu đường cong có đoạn chuyển tiếp. Số theo thứ tự đường cong dọc tuyến: đường cong có lý trình nhỏ nhất là `TĐ1, P1, TC1`, kế tiếp là `TĐ2, P2, TC2`… Các cọc này luôn được ghi tên như mọi cọc khác, kể cả khi `CTYTC` đã vẽ cọc (muốn tránh hai bộ chữ, bỏ ô **Cọc** trong `CTYTC`). **Tên cọc cong không dấu** đổi thành `TD, ND`. Cọc đặc biệt không chiếm số của cọc C.

### Tên cọc trên bình đồ (từ 0.5.6)

`CTPHATCOC` và `CTDANHCOC` có ô **Ghi tên cọc lên bình đồ** (mặc định bật): tên cọc nằm ngay ngoài **đầu trái** của trắc ngang (Sample Line) và lý trình ngay ngoài **đầu phải** (trái, phải theo chiều đi của tuyến), chữ viết dọc theo tuyến, trên layer `COC_TEN` (từ 0.5.8). Ô chọn **Lý trình** (từ 0.5.12) có ba mức: *Không ghi lý trình*, *Lý trình chỉ tại cọc Km* (mặc định), *Lý trình tại mọi cọc*; sau khi chạy, dòng lệnh báo số tên và số lý trình đã ghi; **Tên cọc xen kẽ trái phải** (mặc định tắt) đổi chỗ tên và lý trình ở mỗi cọc thứ hai. Chữ có cỡ chữ 2.5 mm theo tỉ lệ bình đồ đã nhập ở `CTTUYEN`. Chạy lại sẽ thay chữ cũ của nhóm cọc đó. Tên ghi trên bình đồ không có hậu tố "(Km1)"; hậu tố chỉ có trong tên Sample Line, vì Civil 3D cần tên không trùng. Với nhóm cọc đã đánh tên từ bản cũ, chạy `CTDANHCOC` rồi **Áp dụng** để ghi tên, kể cả khi không tên nào đổi.

- [ ] Chưa kiểm tra trên Windows: vị trí, góc xoay và cỡ chữ của tên cọc; `U` một lần xoá cả chữ lẫn thay đổi Sample Line.

### Cần xác nhận khi thử

- [ ] **Không đánh số quay lại khi TT>=100**: đang hiểu là "khi số C trong Km đã tới 100 thì Km sau đánh tiếp, không về C1". Nếu AND Design làm khác, báo lại.
- [ ] **Tuyến kênh** (AND Design) chưa làm: cần biết quy tắc đặt tên cọc tuyến kênh.
- [ ] **Tra siêu cao** đặt "Lệch ngoài" = 1/2 chiều dài nối (một nửa trên đường thẳng, một nửa trên đường cong). Nếu văn phòng dùng 2/3 trên đường thẳng, báo lại để đổi mặc định.
- [ ] Bảng **siêu cao** (TCVN 4054 Bảng 13) và **L nối siêu cao** (Bảng 14) trong `bundle/Resources/tcvn4054.preset.json` được nhập theo trí nhớ, **chưa đối chiếu văn bản gốc**: một kỹ sư cần soát từng dải R, isc và L trước khi dùng cho hồ sơ. Từ 0.5, **Gợi ý R, L theo TCVN** cũng điền L1 = L2 từ bảng này.

### Chưa kiểm tra trên Windows (0.5)

- [ ] `CTTUYEN`: tạo alignment từ điểm chỉ, đặt lý trình đầu, ghi vận tốc thiết kế, trắc dọc tự nhiên, nhập Assembly từ DWG (`ImportAssembly`)
- [ ] `CTTUYEN` → `CTYTC` tự mở với alignment mới được chọn sẵn
- [ ] `CTYTC` **Siêu cao → Alignment**: điểm tới hạn và độ dốc ngang trong Superelevation của alignment (xem bằng Superelevation Tabular Editor)
- [ ] `CTYTC` **Dồn dịch đỉnh trắc dọc phía sau**: PVI của trắc dọc thiết kế dời đúng khi R thay đổi
- [ ] `CTYTC` **Góc chuyển hướng** trên polyline: đỉnh polyline dời theo
- [ ] `CTPHATCOC` / `CTDANHCOC`: tạo, đổi tên, xoá Sample Line trong một bước undo; tên trùng giữa các Km có hậu tố "(Km1)"

## Bản 0.6 — dùng dễ hơn

Bản này không đổi phép tính nào; nó đổi cách dùng. Chưa có phần nào của bản 0.6 được chạy trên Civil 3D trước khi phát hành: mọi mục dưới đây cần người thử xác nhận.

### Tuyến hiện hành

Bản vẽ nhớ một **tuyến hiện hành**. `CTPHATCOC`, `CTDANHCOC`, `CTTOADO`, `CTBANGCONG` mở ra là dùng ngay tuyến đó, không hỏi chọn alignment. Dòng lệnh báo `Tuyến hiện hành: <tên>`; nếu bản vẽ chỉ có một alignment thì báo `Bản vẽ có một tuyến: <tên>`; nếu đã chọn sẵn alignment trước khi gõ lệnh thì không báo gì. Các lệnh khác (`CTYTC`, `CTTRACDOC`…) chưa dùng tuyến hiện hành.

- Tuyến vừa tạo bằng `CTTUYEN` tự thành tuyến hiện hành.
- Bấm **Chọn trên bản vẽ…** trong một lệnh và chọn tuyến khác: các lệnh sau trong phiên làm việc này dùng tuyến đó. Tuyến được **ghi vào bản vẽ** khi lệnh thực sự ghi kết quả vào bản vẽ (**Áp dụng**, hoặc **Xem trước** rồi giữ kết quả) và khi dùng `CTTUYENHH`. Bấm **Hủy** thì bản vẽ không đổi. Chỉ xuất CSV/Excel thì tuyến chỉ được nhớ trong phiên làm việc, đóng bản vẽ là mất.
- `CTDANHCOC` khi không chọn sẵn gì: nạp nhóm cọc của tuyến hiện hành trước khi mở hộp thoại. Tuyến có nhiều nhóm cọc thì dòng lệnh hỏi nhóm nào; tuyến chưa có nhóm cọc thì dòng lệnh báo và hộp thoại mở với "chưa chọn". (Bản 0.5.12 mở hộp thoại trống.)
- Chọn alignment trước rồi mới gõ lệnh: lệnh dùng alignment đó.
- Bản vẽ chỉ có một alignment: lệnh dùng luôn alignment đó.
- `CTTUYENHH` (nút **Tuyến hiện hành**): xem hoặc đổi tuyến hiện hành.

### Nâng cao và Về mặc định

`CTTUYEN`, `CTPHATCOC`, `CTDANHCOC` chỉ hiện các ô hay dùng. Các ô còn lại nằm trong **Nâng cao**; bấm vào để mở, lần sau hộp thoại nhớ trạng thái mở/đóng.

Khi **Áp dụng** bị mờ, dòng chữ cạnh nút nói ô nào sai. Nếu ô đó không thấy trên hộp thoại thì nó nằm trong **Nâng cao**.

**Về mặc định** đặt lại mọi ô của hộp thoại về giá trị ban đầu. Tuyến hoặc nhóm cọc đang chọn, kích thước cửa sổ và trạng thái Nâng cao giữ nguyên. Ở `CTPHATCOC`, khoảng lý trình, nhóm cọc đã chọn và các lý trình chèn cũng về ban đầu.

### Ribbon

Sáu panel theo thứ tự làm việc: **Tuyến → Cọc → Trắc dọc → Trắc ngang → Địa hình → Tiện ích**.

### Gửi báo lỗi — `CTBAOLOI`

Tạo tệp `C3DTools-baoloi-<ngày-giờ>.zip` trên Desktop, gồm `trace.log`, `error.log`, `options.json` và `thong-tin.txt` (phiên bản C3DTools, Civil 3D, Windows, tên bản vẽ, số alignment, số nhóm cọc). **Tệp không chứa bản vẽ và C3DTools không tự gửi nó đi đâu.** Gửi tệp này kèm mô tả bước đang làm khi gặp lỗi.

Tệp có tên bản vẽ, tên tuyến, tên nhóm cọc, tên cọc, tên mặt phủ, tên trắc dọc, và nhật ký lỗi có thể chứa đường dẫn thư mục trên máy (kể cả tên người dùng Windows). Nhật ký gồm mọi bản vẽ đã làm, không riêng bản vẽ đang mở. Có thể mở tệp zip ra xem trước khi gửi. Nếu Desktop nằm trong OneDrive thì tệp sẽ được OneDrive đồng bộ như mọi tệp khác trên Desktop.

### Chưa kiểm tra trên Windows (0.6)

Tuyến hiện hành:

- [ ] Tuyến hiện hành còn sau khi lưu, đóng và mở lại bản vẽ
- [ ] Xoá alignment đang là tuyến hiện hành, bản vẽ còn từ 2 alignment: `CTPHATCOC` mở hộp thoại với "chưa chọn", không báo lỗi
- [ ] Xoá alignment đang là tuyến hiện hành, bản vẽ còn đúng 1 alignment: `CTPHATCOC` dùng alignment đó, dòng lệnh báo `Bản vẽ có một tuyến: <tên>`
- [ ] Bản vẽ có 2 alignment, chưa có tuyến hiện hành: lệnh mở hộp thoại với "chưa chọn"
- [ ] Mở lệnh, **Chọn trên bản vẽ…**, rồi **Hủy**: bản vẽ không bị đánh dấu đã sửa (`DBMOD` không đổi), không có bước undo mới
- [ ] `CTTOADO` chỉ xuất CSV (không vẽ bảng): chạy được, bản vẽ không đổi
- [ ] **Áp dụng** → `U` → chạy lại lệnh: lệnh vẫn mở đúng tuyến, một lệnh `U` hoàn tác cả cọc lẫn bản ghi tuyến hiện hành
- [ ] `CTTUYENHH`: Enter giữ tuyến đang có; Esc thoát không đổi gì; bấm trượt ra ngoài thì hỏi lại (tối đa 5 lần, sau đó báo "Đã hủy."); chọn alignment trước rồi gõ lệnh thì đề nghị đúng alignment đó
- [ ] `CTTUYEN` khi đang có tuyến A: **Xem trước** rồi trả lời không giữ, **Hủy**, chạy `CTPHATCOC`: lệnh mở với tuyến A
- [ ] `CTDANHCOC` khi chọn sẵn một line hoặc text: lệnh dùng tuyến hiện hành; chọn sẵn một Sample Line: lệnh dùng nhóm cọc của nó

Hộp thoại:

- [ ] **Về mặc định**: các ô về giá trị ban đầu; tuyến, kích thước cửa sổ, trạng thái Nâng cao giữ nguyên
- [ ] `CTDANHCOC` trên tuyến có từ 2 nhóm cọc, chọn nhóm thứ hai, **Về mặc định**: dòng lệnh không hỏi lại, hộp thoại vẫn ghi nhóm thứ hai
- [ ] `CTDANHCOC`: Esc khi được hỏi nhóm cọc: dòng lệnh ghi "Đã hủy chọn nhóm cọc.", hộp thoại mở lại
- [ ] Mỗi hộp thoại: mở, **Về mặc định**, đổi Phát sinh/Chèn, mở và đóng Nâng cao, **Chọn trên bản vẽ…**, **Xem trước**, **Về mặc định**, **Áp dụng**: không treo, `trace.log` không có dòng "VÒNG LẶP BINDING"
- [ ] **Nâng cao**: mở/đóng được; chữ và mũi tên đọc được trên giao diện tối của Civil 3D
- [ ] Thu cửa sổ về nhỏ nhất với Nâng cao đang mở (thử cả tỉ lệ màn hình 125% và 150%): thấy đủ các nút ở chân hộp thoại, danh sách cọc còn ít nhất vài hàng, cuộn tới được mọi ô trong Nâng cao
- [ ] `CTTUYEN` với Nâng cao mở ở kích thước nhỏ nhất: chỉ có một thanh cuộn, lăn chuột cuộn được
- [ ] Phím Tab đi từ trên xuống, tới được tiêu đề Nâng cao (Space để mở), các nút chân hộp thoại đi sau cùng
- [ ] Dòng tóm tắt dài: bị cắt bằng "…" và rê chuột hiện đủ câu; dòng rỗng thì không hiện khung trống khi rê chuột
- [ ] `CTPHATCOC`: gõ "abc" vào khoảng cách cọc C, chuyển sang **Chèn**, thêm một lý trình: **Áp dụng** bấm được. Chuyển lại **Phát sinh**: bị chặn, dòng tóm tắt nêu khoảng cách cọc C
- [ ] `CTTUYEN`: chọn tệp mặt cắt lỗi: thông báo hiện trong Nâng cao

Ribbon và báo lỗi:

- [ ] Ribbon: 6 panel, không panel nào cao quá 3 hàng, icon Cọc và Tiện ích hiện đúng, bấm nút chạy lệnh
- [ ] `CTBAOLOI`: có tệp zip trên Desktop, Explorer mở và chọn sẵn tệp, trong zip không có DWG
- [ ] `CTBAOLOI` khi Desktop nằm trong OneDrive, và khi tên người dùng Windows có dấu tiếng Việt hoặc dấu cách
- [ ] `CTBAOLOI`: mở `thong-tin.txt` bằng Notepad, chữ tiếng Việt đọc được
- [ ] `CTBAOLOI` trên máy chưa từng chạy lệnh C3DTools nào: dòng lệnh nêu tệp nào chưa có
- [ ] `CTBAOLOI` trên bản vẽ tạo từ `acad.dwt`: vẫn ra tệp zip; phần Civil 3D trong `thong-tin.txt` ghi số 0 hoặc "không đọc được", lệnh không báo lỗi
- [ ] Mở Civil 3D bằng lối tắt "Civil 3D as AutoCAD" rồi gõ `CTBAOLOI`: nếu lệnh chạy thì vẫn ra tệp zip và phần Civil 3D ghi "không đọc được"; nếu lệnh không tồn tại thì ghi lại điều đó
- [ ] `CTBAOLOI` khi Desktop bị chặn ghi (Controlled Folder Access): tệp zip nằm trong thư mục tạm, dòng lệnh ghi đường dẫn
- [ ] Sau `CTBAOLOI`: bản vẽ không bị đánh dấu đã sửa
