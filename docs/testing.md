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
