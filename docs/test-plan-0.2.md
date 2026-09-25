# Kế hoạch thử nghiệm C3DTools 0.2 — Yếu tố cong (CTYTC)

Dành cho người thử trên **Windows 64-bit + Civil 3D 2021 (update 2021.3 trở lên)**. Phần mềm mới chỉ được biên dịch, **chưa chạy lần nào trong Civil 3D**, nên mọi kết quả ở đây, kể cả "không lỗi", đều có giá trị. Làm theo thứ tự; mỗi bài ghi **Đạt / Không đạt** và ghi chú vào bảng cuối.

Thời gian dự kiến: 60–90 phút cho toàn bộ.

## 0. Chuẩn bị

1. Tải `C3DTools-0.2.0.<số>.zip` từ mục **Assets** của bản mới nhất: https://github.com/baobeta/civil-utils/releases (hoặc file zip được gửi kèm).
2. Giải nén. Trong thư mục giải nén phải có: `install.cmd`, `uninstall.cmd`, `install.ps1`, `THIRD_PARTY.md` và thư mục `C3DTools.bundle`.
3. Kiểm tra `C3DTools.bundle\Contents\` có: `C3DTools.Civil2021.dll`, `C3DTools.Core.dll`, `Newtonsoft.Json.dll`, thư mục `Resources\tcvn4054.preset.json`. **Không được có** file nào tên bắt đầu bằng `Ac`, `Aec`, `Adw`, `AdUi`. Nếu có → báo ngay, dừng thử.
4. Đóng Civil 3D, bấm đúp `install.cmd`. Cửa sổ đen hiện xong là xong.
5. Ghi lại: phiên bản Windows, độ phân giải và tỉ lệ DPI (Settings → Display → Scale, ví dụ 100% / 125% / 150%), Civil 3D build (Help → About), **ngôn ngữ vùng của Windows** (Settings → Time & Language → Region; nếu là Tiếng Việt thì dấu phẩy là dấu thập phân, đây là trường hợp quan trọng cần thử).

## 1. Khởi động và ribbon

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 1.1 | Mở Civil 3D 2021 với bản vẽ trống (mẫu `_AutoCAD Civil 3D (Metric) NCS.dwt`). Xem dòng lệnh. | Không có thông báo lỗi tải add-in. Nếu có dòng `Không tạo được tab ribbon C3DTools: …` → chụp lại. |
| 1.2 | Nhìn ribbon. | Có tab **C3DTools**. Trong tab có panel **Tuyến** với nút lớn **Yếu tố cong** và hai nút nhỏ **Mẫu TCVN**, **Bảng cong**. Ghi lại nút có icon hay chỉ chữ. |
| 1.3 | Gõ `CTHELLO`. | Dòng `C3DTools 0.1 — bản vẽ có 0 tuyến. Ví dụ lý trình: Km1+234.50`. |
| 1.4 | Đổi workspace (góc dưới phải, ví dụ sang "Civil 3D" rồi quay lại). | Ghi lại tab C3DTools còn hay mất sau khi đổi. (Chưa xử lý trường hợp này, chỉ cần ghi nhận.) |
| 1.5 | Đang chạy lệnh `LINE`, bấm nút **Yếu tố cong** trên ribbon. | Lệnh LINE bị huỷ và `CTYTC` bắt đầu (dòng lệnh hiện `Chọn polyline hoặc alignment:`). |

## 2. Bài A — Cong tròn đơn (kiểm tra số liệu)

Vẽ polyline **đúng toạ độ** để đối chiếu số:

```
PLINE  ↵
0,0  ↵
100,0  ↵
100,100  ↵
↵
```

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 2.1 | Gõ `CTYTC`, chọn polyline. | Hộp thoại **Yếu tố cong – TCVN 4054** mở. Dòng trên cùng: `Tuyến: Polyline (3 đỉnh)`, **Lý trình đầu** = 0, **V** = 60, dòng Rmin: `Rmin giới hạn 125 m · thông thường 250 m`. Bảng có **1 dòng** Đ1, cột **A** = `90°00'00"`. |
| 2.2 | Xem giá trị mặc định dòng Đ1. | R = 250, L1 = L2 = 0, Wb = Wl = 0. Có cảnh báo màu vàng (T1 = 250 vượt điểm đầu → dòng **đỏ** "Đ1: tiếp tuyến T1 vượt quá điểm đầu tuyến"). Nút **Áp dụng** và **Xem trước** bị mờ. |
| 2.3 | Sửa **R** thành `50`. | Ngay khi gõ xong: **T1 = T2 = 50**, **P = 20.71**, **K = 78.54**. Dòng chuyển sang màu vàng (cảnh báo `R < Rmin giới hạn`), **Áp dụng** sáng lại. Dòng dưới bảng: `Dòng chọn: TĐ 0+050.00 · P 0+089.27 · TC 0+128.54`. |
| 2.4 | Nếu Windows tiếng Việt: xoá R, gõ `50,5` rồi gõ lại `50`. | `50,5` được chấp nhận (T1 đổi), không báo lỗi. Kết quả hiển thị dùng dấu **chấm** (50.5). |
| 2.5 | Gõ `abc` vào R. | Ô giữ chữ `abc`, dòng đỏ, **Áp dụng** mờ. Gõ lại `50` → hết lỗi. |
| 2.6 | Bấm **Phóng tới đỉnh**. | Hộp thoại ẩn, màn hình zoom tới đỉnh (100,0), có dấu **X** tạm ở đỉnh, hộp thoại mở lại với giá trị còn nguyên. |
| 2.7 | Để các ô: ☑ Vẽ đường cong, ☐ Tạo Alignment, ☑ Khung, ☑ Cọc, ☑ CSV, ☐ Bảng. Bấm **Xem trước**. | Hộp thoại ẩn; bản vẽ hiện cung tròn, khung, cọc. Dòng lệnh hỏi `Giữ kết quả? [Co/Khong] <Co>:`. |
| 2.8 | Gõ `Khong`. | Mọi thứ vừa vẽ biến mất, hộp thoại mở lại với giá trị còn nguyên. |
| 2.9 | Bấm **Áp dụng**. | Hộp thoại đóng. Dòng lệnh: `Hoàn thành: 1 đường cong, chiều dài tuyến = 178.54 m.` và dòng `Đã xuất bảng yếu tố cong: …_YEUTOCONG.csv` (nếu bản vẽ đã lưu; nếu chưa lưu sẽ hiện `Bản vẽ chưa được lưu: bỏ qua xuất CSV.` — hãy **lưu bản vẽ trước** để thử CSV). |
| 2.10 | Kiểm tra hình vẽ. | Layer `YTC_CONG` (màu xanh lá 3): một **ARC** tâm (50,50), bán kính 50, từ (50,0) đến (100,50). Layer `YTC_COC` (màu đỏ 1): 3 cọc TĐ1, P1, TC1 mỗi cọc gồm 1 vạch + 2 dòng chữ (tên cọc và lý trình), cùng 2 cọc đầu/cuối tuyến chỉ có lý trình `0+000.00` và `0+178.54`. Layer `YTC_BANG`: khung chữ nhật + 5 dòng chữ + đường dẫn từ giữa cung qua đỉnh tới khung. |
| 2.11 | Đọc khung. | 5 dòng, đúng thứ tự: `A=90°00'00"  P=20.71` / `R=50  K=78.54` / `T1=50  T2=50` / `L1=0  L2=0` / `Wb=0  Wl=0`. Khung nằm **phía ngoài** đỉnh (góc trên-phải của đỉnh (100,0)), xoay 45°, chữ **đọc được** (không lộn ngược). |
| 2.12 | Đọc cọc. | TĐ1 tại (50,0) ghi `0+050.00`; P1 tại giữa cung ghi `0+089.27`; TC1 tại (100,50) ghi `0+128.54`. Chữ cọc không lộn ngược. Chữ "Đ" hiện đúng dấu (không phải ô vuông). |
| 2.13 | Gõ `U` **một lần**. | Toàn bộ cung, cọc, khung biến mất cùng lúc. Polyline gốc còn nguyên. |
| 2.14 | Gõ `REDO` (hoặc Ctrl+Y). | Tất cả trở lại. |
| 2.15 | Mở file `<tên bản vẽ>_YEUTOCONG.csv` bằng Excel (bấm đúp). | Dòng đầu: `Dinh,A,R,L,T,P,K,Wb,Wl,Ly trinh ND,Ly trinh TD,Ly trinh P,Ly trinh TC,Ly trinh NC`. Dòng 2: `Đ1,90d00'00,50,0,50,20.71,78.54,0,0,0+050.00,0+050.00,0+089.27,0+128.54,0+128.54`. Chữ **Đ** hiện đúng trong Excel. |
| 2.16 | Chạy lại `CTYTC`, chọn **cùng polyline**. | Hộp thoại mở với **R = 50** đã nhớ (không phải 250). |
| 2.17 | Sửa R = `60`, **Áp dụng**. | Cung, cọc, khung cũ **bị thay** bằng bộ mới (không trùng đôi). K mới = 94.25. `U` một lần → quay về bộ R = 50. |

## 3. Bài B — Cong có chuyển tiếp (clothoid), lý trình đầu ≠ 0

```
PLINE  ↵
0,0  ↵
300,0  ↵
450,259.8076  ↵
↵
```

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 3.1 | `CTYTC`, chọn polyline. Đặt **Lý trình đầu** = `1000`. | A = `60°00'00"`. |
| 3.2 | Dòng Đ1: R = `200`, L1 = `50`, L2 = `50`, Wb = `0.6`, Wl = `0.3`. | **T1 = T2 = 140.76**, **P = 31.54**, **K = 259.44**. Dòng chọn: `NĐ 1+159.24 · TĐ 1+209.24 · P 1+288.96 · TC 1+368.68 · NC 1+418.68`. Dòng màu vàng (R 200 < 250 thông thường) nhưng **Áp dụng** sáng. |
| 3.3 | Sửa L1 = `500`. | Dòng **đỏ**, thông báo `L quá dài so với góc chuyển hướng`, Áp dụng mờ. Sửa lại `50`. |
| 3.4 | Sửa L2 = `40` (L1 vẫn 50). | **T1 = 140.54**, **T2 = 135.87**, K = 254.44. Sửa lại L2 = `50`. |
| 3.5 | Bấm **Gợi ý R, L theo TCVN**. | R đổi thành **250**; L giữ nguyên (bảng Lct chưa có trong preset); không lỗi. Sửa R về `200`. |
| 3.6 | **Áp dụng** (☑ Vẽ đường cong, ☑ Khung, ☑ Cọc, ☑ CSV). | `Hoàn thành: 1 đường cong, chiều dài tuyến = 577.92 m.` |
| 3.7 | Kiểm tra `YTC_CONG`. | **3 đối tượng**: 2 polyline chuyển tiếp (mỗi cái 21 đỉnh) và 1 ARC ở giữa. Nối liền nhau mượt, chuyển tiếp tiếp tuyến với đoạn thẳng tại NĐ và NC. |
| 3.8 | Kiểm tra cọc. | 5 cọc: **NĐ1** `1+159.24`, **TĐ1** `1+209.24`, **P1** `1+288.96`, **TC1** `1+368.68`, **NC1** `1+418.68`, cộng cọc đầu `1+000.00` và cuối `1+577.92`. Cọc NĐ/TĐ/TC/NC cắm **phía ngoài** cong, cọc P phía **trong** (như YTC.lsp). |
| 3.9 | Đọc khung. | `A=60°00'00"  P=31.54` / `R=200  K=259.44` / `T1=140.76  T2=140.76` / `L1=50  L2=50` / `Wb=0.6  Wl=0.3`. |
| 3.10 | **Đối chiếu YTC.lsp** (nếu có): `APPLOAD` file `YTC.lsp`, gõ `YTC`, chọn **bản sao** của polyline (copy sang chỗ khác), nhập lý trình 1000, chiều cao 2.5, V 60, R 200, L 50, Wb 0.6, Wl 0.3. | Cả T, P, K và 5 lý trình cọc trong CSV của LISP **giống** kết quả CTYTC (sai lệch ≤ 0.01). Vị trí cọc và khung trùng nhau khi chồng hai bản vẽ (Move về cùng gốc). Ghi lại mọi sai lệch. |

## 4. Bài C — Tuyến nhiều đỉnh, có đỉnh thẳng hàng

```
PLINE  ↵
0,0  ↵
200,0  ↵
200,150  ↵
200,300  ↵
350,400  ↵
↵
```

Đỉnh (200,150) thẳng hàng với 2 đỉnh kề → **không** phải đỉnh cong.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 4.1 | `CTYTC`, chọn polyline, **Lý trình đầu** = `250`. | Bảng có **2 dòng** Đ1, Đ2 (không phải 3). Đ1: A = `90°00'00"`; Đ2: A = `56°18'36"`. |
| 4.2 | Đ1: R `120`, L1 `40`, L2 `40`, Wb `0.5`, Wl `0.2`. Đ2: R `250`, L 0. | Đ1: T = **140.54**, P = **50.49**, K = **228.5**. Đ2: T = **133.8**, P = **33.55**, K = **245.7**. Đ1 lý trình: NĐ `0+309.46`, TĐ `0+349.46`, P `0+423.71`, TC `0+497.96`, NC `0+537.96`. Đ2: TĐ `0+563.63`, P `0+686.48`, TC `0+809.33`. |
| 4.3 | Sửa Đ1 R = `120` → `200`. | **Đ2 lý trình cũng thay đổi** (vì K của Đ1 đổi), không cần bấm gì. Sửa về 120. |
| 4.4 | Chọn dòng Đ1, bấm **Áp dòng này cho các dòng dưới**. | Đ2 nhận R 120, L 40/40, Wb 0.5, Wl 0.2. Sửa lại Đ2 như 4.2. |
| 4.5 | Sửa Đ2 R = `800`. | Đ2 **đỏ**: `chồng lên đường cong trước` (T2 của Đ1 + T1 của Đ2 > 150). Áp dụng mờ. Sửa về 250. |
| 4.6 | **Áp dụng** với ☑ Bảng thêm vào. | `Hoàn thành: 2 đường cong, chiều dài tuyến = 605.81 m.` Có thêm **bảng AutoCAD** "BẢNG YẾU TỐ CONG" gần đỉnh cuối, 2 dòng dữ liệu, tiêu đề cột `Đỉnh, A, R, L1, L2, T1, T2, P, K, Wb, Wl, Lý trình NĐ…`. Tiếng Việt hiển thị đúng trong bảng. |
| 4.7 | Đọc CSV. | 2 dòng dữ liệu, Đ1 và Đ2, đúng số như 4.2. |

## 5. Bài D — Tạo Alignment Civil 3D (chức năng rủi ro nhất)

Dùng lại polyline Bài B (copy ra chỗ trống).

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 5.1 | `CTYTC`, chọn polyline, lý trình đầu 1000, R 200, L 50/50. Tích **☑ Tạo Alignment Civil 3D**, bỏ ☐ Vẽ đường cong. **Áp dụng**. | Một trong hai: (a) tạo được **Alignment** Civil 3D với 1 cong Spiral-Curve-Spiral, lý trình đầu 1000, **hoặc** (b) dòng lệnh báo `Không tạo được Alignment: … Đã vẽ hình học thường thay thế.` và vẽ ARC/clothoid thường. **Chụp toàn bộ dòng lệnh** (F2) trong cả hai trường hợp. |
| 5.2 | Nếu (a): Chọn alignment → Alignment Properties / Alignment Grid View. | R = 200, L1 = L2 = 50, lý trình NĐ/NC như Bài B (trong 0.01 m). Nếu có dòng `Cảnh báo: Đ1 lệch …` trên dòng lệnh → chụp lại. |
| 5.3 | Nếu (a): `U` một lần. | Alignment, cọc, khung cùng biến mất. |
| 5.4 | Nếu (a): REDO, chạy lại `CTYTC`, chọn **alignment** vừa tạo. | Hộp thoại mở với R 200, L 50/50, Wb/Wl đúng như đã nhập; **Chế độ** mặc định **Chỉ cắm cọc + khung**; cột R, L1, L2 **mờ, không sửa được**; ô **Cập nhật Alignment Civil 3D** mờ. |
| 5.5 | Chọn **Thiết kế lại cong**. | Cột R, L1, L2 sửa được; ô **Cập nhật Alignment Civil 3D** tự tích và sáng. Sửa R = `220`, **Áp dụng**. → Alignment cập nhật R = 220 (Grid View), cọc/khung đổi theo. Hoặc thông báo `Không cập nhật được Alignment: …` và alignment **không đổi** (kiểm tra R vẫn 200 — quan trọng: không được để alignment mất cong). |
| 5.6 | Thử một chuyển tiếp: Thiết kế lại cong, L1 = 50, L2 = 0. | Dòng lệnh báo rõ ràng cong này chỉ có một chuyển tiếp nên không ghi vào Alignment (vẽ hình học thường). Alignment không đổi. |

## 6. Bài E — Alignment có sẵn + Offset Alignment mở rộng (chế độ YTCA)

Chuẩn bị (thao tác Civil 3D thường):

1. Tạo alignment bằng **Alignment Creation Tools**: 3 tangent, 2 cong, bán kính tuỳ ý (ví dụ 150 và 300), có hoặc không có spiral.
2. Tạo 2 **Offset Alignment** (Home → Alignment → Create Offset Alignment) trái và phải, offset 3.5 m.
3. Trên offset alignment phải, thêm **Widening** (chọn offset alignment → Add Widening) rộng thêm 0.6 m qua cong thứ nhất.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 6.1 | `CTYTC`, chọn alignment tim. | Hộp thoại: `Tuyến: Alignment …`, **Lý trình đầu** đọc từ alignment (không sửa được), **2 dòng** Đ1, Đ2 với R, L1, L2, A đúng như alignment; chế độ **Chỉ cắm cọc + khung**. Wb = Wl = 0. |
| 6.2 | Bấm **Đọc Wb/Wl từ Offset Alignment…**. Chọn 2 offset alignment, Enter. | Dòng lệnh: `Mép trái (<tên offset>): bề rộng danh nghĩa = 3.5 m`, `Mép phải (<tên offset>): bề rộng danh nghĩa = 3.5 m`. Hộp thoại mở lại: cong 1 có **Wb hoặc Wl = 0.6** (Wb nếu mở rộng ở phía bụng — phía trong cong; Wl nếu phía lưng), cong 2 = 0. Ghi lại giá trị nào nhận 0.6 và phía mở rộng thực tế. |
| 6.3 | **Áp dụng**. | Chỉ cọc (`YTC_COC`) và khung (`YTC_BANG`) được vẽ; **alignment không đổi** (kiểm tra Grid View); không có gì trên `YTC_CONG`. `Hoàn thành: 2 đường cong…`. |
| 6.4 | So khung với Alignment. | R, K, L1, L2 trong khung = giá trị alignment. Lý trình cọc TĐ/TC = lý trình PC/PT (hoặc SC/CS) trong Alignment Grid View, sai lệch ≤ 0.01. |
| 6.5 | **Đối chiếu YTCA** (nếu có LISP): gõ `YTCA`, chọn cùng alignment, chọn 2 offset alignment. | Wb/Wl, T1/T2/P trong khung và lý trình cọc giống CTYTC (≤ 0.01). Khung cùng vị trí và góc xoay. |
| 6.6 | `U` một lần. | Cọc và khung biến mất, alignment và offset còn nguyên. |
| 6.7 | Sửa R cong 1 trong Alignment Grid View (ví dụ 150 → 180), chạy lại `CTYTC` chọn alignment, **Áp dụng**. | Khung và cọc cập nhật theo R mới, không trùng đôi. |

## 7. Bài F — `CTYTCMAU` (kiểu nhãn native)

Bản vẽ mới, trống.

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 7.1 | Gõ `CTYTCMAU`. | Dòng lệnh: `Không có mẫu Resources\C3DTools-TCVN.dwg: kiểu sẽ được tạo bằng mã.`, `Sẽ thêm: YTC_TCVN, TCVN_Tuyen; đã có: không có.`, dòng về viết tắt, rồi hỏi `Nhập kiểu TCVN [ThemMoi/GhiDe/Huy]`. Gõ `ThemMoi`. |
| 7.2 | Đọc dòng lệnh sau đó. | `Hoàn thành CTYTCMAU…`. **Chụp lại mọi dòng bắt đầu bằng "Không"** (đó là các thuộc tính chưa đặt được). |
| 7.3 | Toolspace → Settings → Alignment → Label Styles → Curve. | Có style **YTC_TCVN**. Mở (Edit) → tab Layout: có component text `YTC`, Border Visibility = True, Type = Rectangular. Tab General: Plan Readable = True, Readability Bias 110. Ghi lại mục nào không đúng. |
| 7.4 | Settings → Alignment → Label Sets. | Có **TCVN_Tuyen**, trong đó có Curves dùng YTC_TCVN và Major Geometry Points. |
| 7.5 | Settings → chuột phải tên bản vẽ → Edit Drawing Settings → Abbreviations → Alignment Geometry Point Text. | PC = TĐ, PT = TC, TS = NĐ, ST = NC, SC = TĐ, CS = TC, PI = Đ, Mid = P. |
| 7.6 | Tạo alignment bất kỳ (có cong), Alignment Properties → Labels → chọn Label Set TCVN_Tuyen. | Xuất hiện nhãn cong với khung; **ghi lại chữ trong nhãn**: các trường hiện đúng số (A, P, R, K, T) hay hiện chữ `???`/tên trường. Đây là điểm chưa chắc chắn. |
| 7.7 | `U` một lần sau 7.1. | Style, label set, viết tắt trở về như cũ. |
| 7.8 | Chạy `CTYTCMAU` lần 2, chọn `GhiDe`. | Không lỗi; style được ghi đè. `Huy` → không đổi gì. |

## 8. Bài G — `CTYTCBANG`

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 8.1 | Trên bản vẽ Bài C (polyline đã chạy CTYTC), gõ `CTYTCBANG`, chọn polyline. | Hỏi `Điểm chèn bảng:`; bấm điểm. Bảng hiện → `Giữ kết quả? [Co/Khong]` → `Co`. Bảng có tiêu đề **BẢNG YẾU TỐ CONG**, dòng tiêu đề cột, 2 dòng dữ liệu, chữ Việt đúng, cỡ chữ đều. |
| 8.2 | `CTYTCBANG` trên polyline **chưa** chạy CTYTC. | `Polyline này chưa được CTYTC xử lý…`, không vẽ gì. |
| 8.3 | `CTYTCBANG` trên alignment Bài E. | Bảng với số liệu đọc từ alignment; CSV được ghi. |
| 8.4 | `U` một lần. | Bảng biến mất. |

## 9. Bài H — Giao diện

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 9.1 | Trong hộp thoại: gõ số vào R rồi **Enter**. | Ô chuyển xuống dòng dưới (hoặc kết thúc sửa), không đóng hộp thoại. |
| 9.2 | **Tab** qua các ô. | Đi lần lượt R → L1 → L2 → Wb → Wl → dòng sau. |
| 9.3 | Nhấn **Esc**. | Hộp thoại đóng, không vẽ gì, dòng lệnh không báo lỗi. |
| 9.4 | Bấm **Chọn trên bản vẽ…**, chọn đối tượng khác (polyline khác). | Hộp thoại mở lại với tuyến mới. Bấm lại và chọn **đối tượng không hợp lệ** (Circle) → thông báo, hộp thoại mở lại với tuyến cũ, **giá trị đã nhập còn nguyên**. |
| 9.5 | Kéo cửa sổ rộng/hẹp. | Bảng co giãn, không cắt chữ tiêu đề cột. |
| 9.6 | Nếu màn hình DPI 125% hoặc 150%. | Chữ rõ, không mờ, nút không bị cắt. Chụp ảnh toàn hộp thoại. |
| 9.7 | Chụp **một ảnh hộp thoại** đầy đủ (Bài C sau bước 4.2) để gửi kèm. | — |

## 10. Gỡ cài đặt

| # | Bước | Kết quả mong đợi |
| --- | --- | --- |
| 10.1 | Đóng Civil 3D, bấm đúp `uninstall.cmd`, mở lại Civil 3D. | Không còn tab C3DTools; `CTHELLO` → `Unknown command`. |

## Bảng kết quả (gửi lại)

Sao chép, điền Đạt/Không đạt và ghi chú ngắn.

| Bài | Kết quả | Ghi chú / số liệu thực tế |
| --- | --- | --- |
| 1 Ribbon & khởi động | | icon có/không; tab còn sau đổi workspace? |
| 2 Cong tròn đơn (A) | | T/P/K, lý trình có đúng? CSV mở Excel? |
| 3 Clothoid (B) | | So với YTC.lsp sai lệch lớn nhất = ? |
| 4 Nhiều đỉnh (C) | | 2 dòng? chồng cong báo đỏ? bảng? |
| 5 Tạo Alignment (D) | | (a) tạo được / (b) fallback; dán thông báo |
| 6 Alignment + Offset (E) | | Wb hay Wl nhận 0.6? khớp YTCA? |
| 7 CTYTCMAU (F) | | các dòng "Không…"; nhãn native hiện số hay ??? |
| 8 CTYTCBANG (G) | | |
| 9 Giao diện (H) | | DPI = ?; ảnh chụp |
| 10 Gỡ cài đặt | | |

**Gửi kèm:** ảnh chụp dòng lệnh (F2, chọn hết, copy text tốt hơn ảnh), file DWG các bài B, D, E (nếu được), file CSV, ảnh hộp thoại, tên file zip đã cài, thông tin máy ở mục 0.5. Bất kỳ hộp thoại lỗi nào của AutoCAD (Unhandled exception) → chụp và bấm **Continue**, ghi bài đang làm.
