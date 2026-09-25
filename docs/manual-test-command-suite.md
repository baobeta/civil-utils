# Manual test bộ lệnh C3DTools 0.2 (local)

Bộ file cần test:

```text
artifacts/C3DTools-0.2.0.1.zip
```

## 0. Chuẩn bị

1. Tạo bản sao của một DWG thử nghiệm; không dùng bản vẽ dự án thật.
2. Đóng toàn bộ Civil 3D/AutoCAD.
3. Giải nén `C3DTools-0.2.0.1.zip`.
4. Chạy `install.cmd`.
5. Mở Civil 3D 2021 Metric, tạo bản vẽ mới.
6. Chạy `CTHELLO`.

Kết quả mong đợi là version `0.2.0.1` và danh sách lệnh mới.

## 1. Cấu hình dự án — `CTCONFIG`

1. Chạy `CTCONFIG`.
2. Nhập tên dự án.
3. Giữ số lẻ mặc định hoặc nhập:
   - lý trình: `2`
   - cao độ: `3`
   - khối lượng: `2`
4. Chọn `Set` cho quy tắc cống.
5. Nhập:
   - độ dốc tối thiểu: `0.30%`
   - chiều sâu chôn tối thiểu: `0.70m`
6. Enter để lưu.

File phải được tạo tại:

```text
%APPDATA%\C3DTools\project-preset.json
```

Lệnh này không tạo đối tượng trong DWG nên không cần `U`.

## 2. Tạo Alignment — `CTALIGN`

### Tạo polyline thử nghiệm

Chạy:

```text
PLINE
0,0
50,0
100,0
Enter
```

Để kiểm tra arc, có thể dùng một polyline có đoạn cong bằng cách chọn option `Arc` khi `PLINE` yêu cầu (ví dụ đi qua một cung tròn), hoặc vẽ polyline có arc bằng giá trị bulge; `CTALIGN` phải tạo được Alignment có cả line và arc.

### Preview rồi Cancel

1. Chạy `CTALIGN`.
2. Chọn polyline.
3. Nhập tên `ALIGN-TEST`.
4. Chọn `No` để giữ chiều polyline hoặc `Yes` để đảo chiều.
5. Chọn `Cancel`.

Kết quả mong đợi: không có Alignment mới.

### Apply và kiểm tra undo

1. Chạy lại `CTALIGN`.
2. Chọn polyline, nhập `ALIGN-TEST`, chọn `No`/`Yes` cho chiều rồi chọn `Apply`.
3. Kiểm tra Alignment xuất hiện và chạy theo chiều đã chọn.
4. Chạy một lần `U`.

Alignment và mọi thay đổi của lệnh phải biến mất sau một `U`. Sau khi kiểm tra undo, chạy lại `CTALIGN` và chọn `Apply` để có `ALIGN-TEST` cho các bước tiếp theo.

## 3. Nhập điểm — `CTDIEM`

Dùng file mẫu `docs/fixtures/test-points.csv` (copy ra thư mục test nếu cần), nội dung UTF-8:

```csv
# P,N,E,Z,D
1,1200000.000,550000.000,10.000,MOC
2,1200050.000,550000.000,10.500,ĐIỂM-ĐO
3,1200100.000,550000.000,11.000,MOC
```

File đầu tiên có thể kiểm tra dấu phân cách khác `,`; Core sẽ báo warning nhưng vẫn đọc được số thập phân dấu phẩy.

1. Chạy `CTDIEM`.
2. Chọn file.
3. Chọn `Comma` và `PNEZD`.
4. Kiểm tra thông báo số điểm hợp lệ/cảnh báo.
5. Lần đầu chọn `Cancel`; không được tạo điểm.
6. Chạy lại và chọn `Apply`.
7. Kiểm tra COGO point số 1, 2, 3 và mô tả tiếng Việt.
8. Chạy `U`; các điểm phải biến mất.

## 4. Xuất bảng — `CTEXPORT`

1. Chạy `CTEXPORT`.
2. Chọn `Alignment` (lệnh xuất toàn bộ Alignment trong bản vẽ).
3. Chọn file CSV.
5. Enter để bỏ qua bảng AutoCAD.
6. Mở CSV; tiếng Việt phải hiển thị đúng.

Chạy lại, chọn điểm đặt bảng và `Apply`; bảng phải xuất hiện trong DWG và một `U` phải xóa bảng.

## 5. Nhãn lý trình — `CTNHAC`

1. Chạy `CTNHAC`.
2. Chọn `ALIGN-TEST`.
3. Nhập:
   - đầu `0`
   - cuối `100`
   - khoảng `20`
   - offset `2`
   - chiều cao `1.5`
4. Chọn `Apply`.
5. Kiểm tra nhãn `Km0+000.00`, `Km0+020.00`…`Km0+100.00`.
6. Chạy `U`; toàn bộ nhãn phải biến mất.

## 6. Sample line — `CTCOC`

1. Chạy `CTCOC`.
2. Chọn `ALIGN-TEST`.
3. Nhập:
   - đầu `0`
   - cuối `100`
   - khoảng `20`
   - nửa chiều rộng `10`
   - tên group `COC-TEST`
4. Chọn `Apply`.
5. Kiểm tra Sample Line Group và các sample line tại 0, 20, 40, 60, 80, 100.
6. Chạy `U`; group phải biến mất hoàn toàn.

## 7. Profile — `CTPROFILE`

Cần một TIN surface bao phủ Alignment.

1. Chạy `CTPROFILE`.
2. Chọn `ALIGN-TEST`.
3. Chọn mặt bằng.
4. Nhập tên Profile.
5. Chọn điểm đặt Profile View.
6. Chọn `Apply`.
7. Kiểm tra Profile và Profile View.
8. Chạy `U`; cả hai phải biến mất.

## 8. Khối lượng — `CTKHOILUUNG`

Cần hai TIN surface cùng bao phủ Alignment:

- một mặt bằng hiện trạng;
- một mặt bằng thiết kế cao/thấp khác hiện trạng.

1. Chạy `CTKHOILUONG`.
2. Chọn Alignment, mặt bằng hiện trạng và mặt bằng thiết kế.
3. Nhập khoảng cắt ngang `20`, nửa chiều rộng `10`, lấy mẫu ngang `1`.
4. Chọn điểm đặt bảng và file CSV.
5. Chọn `Apply`.
6. Kiểm tra các cột cắt/đắp và giá trị lũy kế.
7. Chạy `U`; bảng DWG biến mất. File CSV vẫn còn vì nó là file ngoài DWG.

## 9. Kiểm tra cống — `CTCONG`

Cần một DWG có Pipe Network thật, với Junction Structure/RimElevation hợp lệ ở cả hai đầu Pipe.

1. Chạy `CTCONG` khi chưa cấu hình preset: lệnh phải từ chối và hướng dẫn chạy `CTCONFIG`.
2. Sau khi cấu hình, chạy lại và chọn Pipe.
3. Xác nhận Z đầu/cuối là tim cống nếu đúng với dự án.
4. Chọn điểm bảng, CSV và `Apply`.
5. Kiểm tra độ dốc, chiều sâu chôn và kết quả.
6. Chạy `U`; bảng phải biến mất.

## 10. Gỡ cài đặt

1. Đóng Civil 3D.
2. Chạy `uninstall.cmd` trong thư mục đã giải nén.
3. Mở lại Civil 3D; tất cả lệnh `CT...` phải báo `Unknown command`.
