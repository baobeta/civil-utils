# Nghiên cứu: hỗ trợ làm trắc dọc và trắc ngang (2026-09-28)

Mục đích: quyết định C3DTools nên làm gì tiếp cho trắc dọc, trắc ngang và khối lượng, và bước nào gom được thành một cú bấm.

**Độ tin cậy của tài liệu này**

| Nội dung | Mức kiểm chứng |
| --- | --- |
| API Civil 3D 2021 (mục 4) | Đã kiểm bằng reflection trên `AeccDbMgd.dll` của gói `Civil3D2021.Base` 1.0.0. Chưa chạy trên Civil 3D, trừ các mục có ghi "spike". |
| Quy trình của Nova-TDN, ADS Civil, AND Design, V3Tools (mục 1, 2) | Do agent nghiên cứu đọc từ nguồn công khai, có dẫn link. Tôi chưa tự mở lại từng link. |
| Nội dung bảng trắc dọc, trắc ngang, khối lượng (mục 3) | Theo tài liệu của ADS Civil và thông lệ; **không có điều khoản TCVN công khai nào quy định danh sách hàng**. Mẫu của văn phòng là chuẩn cuối cùng. |
| Xếp hạng tình huống sử dụng (mục 2) | Ước lượng theo số bài viết, lượt xem diễn đàn. Không phải số liệu đo. |

---

## 1. Các phần mềm đang làm gì

### 1.1 Quy trình chung (Nova-TDN, ADS Civil, AND Design giống nhau ở mức bước)

| # | Bước | Nova-TDN | ADS Civil | AND Design | Tự động? |
| --- | --- | --- | --- | --- | --- |
| 1 | Khai báo mẫu bảng trắc dọc / trắc ngang | Khai báo mẫu bảng biểu | Khai báo mẫu bảng trắc dọc | `KBMMC` | Làm một lần, thủ công |
| 2 | Vẽ trắc dọc tự nhiên | `TD` | `TD` | `TD` | Một lệnh (nhập tỉ lệ, mức so sánh) |
| 3 | Kẻ đường đỏ | `DD` | `DD` | `DD` | Thủ công, tương tác |
| 4 | Bố trí cong đứng | `CD` | `CCD` | trên bản vẽ | Thủ công từng đỉnh |
| 5 | Điền thiết kế trắc dọc | `DTK` | `DTK` | `VL` | Một lệnh |
| 6 | Khai báo mẫu mặt cắt (nền, mặt, lề, taluy, rãnh, áo đường) | trong `TKTN` | Định nghĩa thiết kế trắc ngang | `KBMMC` | Thủ công, một lần cho mỗi loại đoạn |
| 7 | Vẽ trắc ngang tự nhiên | Trắc ngang tự nhiên | — | `TN` / `MC` | Một lệnh |
| 8 | Áp khuôn (thiết kế trắc ngang cho cả dải cọc) | `TKTN`, `APK` | `APK` | `TT` | Một lệnh, nhưng hộp thoại nhiều tham số; lỗi ở đây kéo theo sai khối lượng |
| 9 | Điền thiết kế trắc ngang | `DTKTN` | `DTKTN` | — | Một lệnh |
| 10 | Tính diện tích đào đắp | `TDT` rồi `DDT` | `TKL` | trong `TT` | 1–2 lệnh |
| 11 | Lập bảng khối lượng, xuất Excel | `LBGT`, `THB` → .txt → Excel | xuất báo cáo | — | Nhiều bước, phải rời bản vẽ |
| 12 | Xếp bản in | in hàng loạt | in hàng loạt | — | Tuỳ phần mềm |

Hai thao tác lặp lại tốn công nhất mà người dùng hay hỏi: **làm lại toàn bộ sau khi sửa đường đỏ** (chạy lại áp khuôn → diện tích → điền → bảng), và **cập nhật cao độ từ trắc ngang lên trắc dọc** (`TDTTN` của AND).

Dữ liệu khảo sát: Nova và AND dùng tệp `.ntd` / `.TDN` hoặc `.txt`; nhập từ Excel là câu hỏi thường gặp.

### 1.2 Civil 3D gốc

| Việc | Cách làm gốc | Chỗ vướng |
| --- | --- | --- |
| Trắc dọc tự nhiên | Create Surface Profile, rồi wizard Create Profile View 9 trang | Dài; phải có band set đúng kiểu Việt Nam |
| Bảng số liệu trắc dọc | Band set + gán Profile 1 / Profile 2 cho **từng** band | Không sửa hàng loạt được; hay hiện sai profile |
| Đường đỏ | Profile Creation Tools, thêm từng PVI | Không nhập được bảng PVI |
| Trắc ngang | Sample Lines → Create Multiple Section Views (6–8 trang) | Thêm cọc mới phải tạo lại cả nhóm section view; chúng không tự sắp lại |
| Khối lượng | Compute Materials → material list → Volume Report | Bảng ra số 0 nếu thiếu nguồn; định dạng không giống bảng Việt Nam |
| Bản in | Plan Production: 2 wizard, 30–50 cú bấm | Cấu hình phức tạp |

### 1.3 Add-in Việt Nam cho Civil 3D

- **V-Template** (v3tech): bộ style, band set, assembly mẫu theo TCVN. Bán theo gói.
- **V3Tools** (v3tech, AutoLISP): tạo corridor hàng loạt (`40A1`), xuất nhiều tờ trắc ngang (`60A1`), cập nhật band set (`60A2`), thêm mặt phủ vào nhiều section view (`60A5`), xuất/nhập sample line ra Excel (`70A3`), chỉnh mức so sánh hàng loạt (`70A4`), phát sinh cọc có tên NĐ/TĐ/P/TC/NC (`50A1`).
- **Nova Road 2024**: bản kế nhiệm Nova-TDN, chạy trên Civil 3D.

Nhận xét: các add-in này đều đi theo hướng **dùng đối tượng gốc của Civil 3D + style Việt hoá**. C3DTools đi hướng khác: **vẽ hình học thường** (line, text) để kết quả không phụ thuộc style và mở được trên máy nào cũng giống nhau. Hướng này nên giữ.

---

## 2. Tình huống sử dụng, xếp theo tần suất

| # | Tình huống | C3DTools hiện tại |
| --- | --- | --- |
| 1 | Vẽ trắc dọc tự nhiên | `CTPROFILE` tạo profile + view (hỏi trên dòng lệnh); `CTTRACDOC` vẽ bảng. Hai lệnh rời nhau |
| 2 | Kẻ đường đỏ | **Chưa có** |
| 3 | Áp khuôn trắc ngang | **Chưa có** |
| 4 | Tính diện tích đào đắp, bảng khối lượng | `CTTRACNGANG` tính diện tích từ hai mặt cắt có sẵn; `CTKHOILUONG` lấy mẫu hai mặt phủ. Chưa có bảng khối lượng kiểu Việt Nam từ nhóm cọc |
| 5 | Điền thiết kế trắc dọc | `CTTRACDOC` (chạy lại là cập nhật) |
| 6 | Khai báo mẫu bảng | Preset JSON; chưa có hộp thoại |
| 7 | Khai báo mẫu mặt cắt | **Chưa có** (`CTTUYEN` chỉ nhập Assembly) |
| 8 | Đặt cống, cầu lên trắc dọc | **Chưa có** |
| 9 | Xếp bản in | `CTXEPTRANG` cho trắc ngang; chưa có khung tên; chưa có cho trắc dọc |
| 10 | Bố trí cong đứng | `CTCONGDUNG` chỉ đọc và kiểm tra, chưa sửa |
| 11 | Nhập số liệu khảo sát từ sổ đo / Excel / .ntd | **Chưa có** |
| 12 | Cập nhật lại tất cả sau khi sửa đường đỏ | **Chưa có** lệnh gom |
| 13 | Khối lượng theo lớp địa chất, vét hữu cơ, đánh cấp | **Chưa có** |
| 14 | Đổi bề rộng thiết kế cho một đoạn | **Chưa có** |
| 15 | Tạo section view hàng loạt | **Chưa có**; người dùng làm tay trong Civil 3D |

Điểm chung của các lệnh hiện có: chúng **trang trí thứ Civil 3D đã tạo**. Các bước tạo ra profile view, section view, đường đỏ, mặt cắt thiết kế thì người dùng vẫn làm tay.

---

## 3. Nội dung bản vẽ thường dùng

**Trắc dọc.** Tỉ lệ ngang 1/1000–1/2000, đứng 1/100–1/200 (đứng gấp 10 lần ngang); có mức so sánh. Các hàng: sơ hoạ tuyến (đoạn thẳng, đoạn cong), tên cọc, lý trình, khoảng cách lẻ, khoảng cách cộng dồn, độ dốc dọc và chiều dài đoạn dốc, cao độ thiết kế, cao độ tự nhiên, chênh cao đào/đắp. Tuỳ mẫu: siêu cao, cao độ rãnh, độ dốc rãnh, địa chất, công trình.

**Trắc ngang.** Tỉ lệ thường 1/200. Hình: mặt đường, lề, rãnh biên, taluy đào/đắp, kết cấu áo đường, đánh cấp, vét hữu cơ. Bảng: khoảng cách lẻ, khoảng cách cộng dồn từ tim, cao độ tự nhiên, cao độ thiết kế, diện tích đào, diện tích đắp.

**Bảng khối lượng đào đắp.** Phương pháp bình quân hai đầu, V = L × (S₁ + S₂) / 2. Cột: STT, lý trình, khoảng cách, diện tích đào, diện tích đắp, khối lượng đào, khối lượng đắp, cộng dồn đào, cộng dồn đắp.

`CTTRACDOC` đã có: tên cọc, khoảng cách lẻ, cộng dồn, lý trình, cao độ tự nhiên, cao độ thiết kế, chênh cao, dốc dọc, cong đứng. Thiếu: sơ hoạ tuyến, siêu cao, cao độ và độ dốc rãnh.

---

## 4. API Civil 3D 2021 cho phép gì

| Việc | API | Trạng thái |
| --- | --- | --- |
| Trắc dọc tự nhiên | `Profile.CreateFromSurface` | Đã chạy thử (spike #3) |
| Tạo profile view | `ProfileView.Create(alignmentId, điểm, tên, bandSetId, styleId)`; `CreateMultiple` để cắt khổ | Đã chạy thử bản đơn giản (spike #3) |
| Đường đỏ | `Profile.CreateByLayout`; `PVIs.AddPVI`, `AddPVISymParabola`, `AddPVIArc`, `AddPVIAsymParabola` | Có trong DLL. Lưu ý: thêm PVI theo thứ tự lý trình |
| Sửa PVI | `ProfilePVI.Station`, `Elevation` có setter | Có trong DLL |
| Gán band | `ProfileView.Bands.GetBottomBandItems()`, `ProfileViewBandItemCollection.Add(BandType, tên style)`, `Profile1Id` / `Profile2Id` | Có trong DLL; diễn đàn báo không ổn định. C3DTools không cần vì tự vẽ bảng |
| Tạo section view hàng loạt | `SampleLineGroup.SectionViewGroups.Add(điểm, từ, tới, rangeOptions, placementOptions)`; `UseDraftPlacement()` hoặc `UseProductionPlacement(template, layout)` | Có trong DLL. Tài liệu Autodesk ghi thêm ở bản **2021.3** |
| Một section view | `SectionView.Create(tên, sampleLineId, điểm)` | Có trong DLL |
| Nguồn lấy mẫu mặt cắt | `SampleLineGroup.GetSectionSources()`, `SectionSource.IsSampled` (get/set) | **Không có `Add`.** Chưa rõ danh sách có sẵn mọi mặt phủ của bản vẽ hay không: cần spike |
| Điểm mặt cắt | `Section.SectionPoints`, `SectionPoint.Location` | C3DTools đang dùng |
| Khối lượng theo vật liệu | `MaterialLists.Add`, `QTOMaterialList.Add`, `QTOMaterial.Add(surfaceId)` hoặc `Add(corridorId, shapeCode)`, `GetTotalVolumeResultDataForMaterialList` | Có trong DLL. Spike #5 trước đây chỉ thử overload corridor |
| Corridor | `CorridorCollection.Add(tên, baseline, alignmentId, profileId, region, assemblyId)`, `BaselineRegionCollection.Add`, `Corridor.Rebuild()` | Có trong DLL, chưa chạy thử |
| View frame, sheet (Plan Production) | — | **Không có API tạo.** Chỉ đọc được |

---

## 5. Đề xuất: các lệnh một cú bấm

Xếp theo giá trị chia cho rủi ro. "Rủi ro" là rủi ro API chưa chạy thử trên Civil 3D.

| Ưu tiên | Lệnh | Thay cho | Người dùng nhập | Rủi ro |
| --- | --- | --- | --- | --- |
| 1 | **Trắc dọc một chạm** `CTVETRACDOC` | Create Surface Profile + wizard 9 trang + gán band + `CTTRACDOC` | Chọn tuyến, mặt phủ, tỉ lệ, điểm đặt | Thấp: API đã chạy thử |
| 2 | **Bảng khối lượng đào đắp** `CTBANGKL` | Compute Materials + Volume Report + chép sang Excel | Chọn nhóm cọc, mặt cắt tự nhiên và thiết kế | Thấp: dùng diện tích `CTTRACNGANG` đã tính, `AverageEndArea` đã có |
| 3 | **Cập nhật tất cả** `CTCAPNHAT` | Chạy lại lần lượt bảng trắc dọc, cong đứng, bảng trắc ngang, khối lượng, tên cọc | Chọn tuyến | Thấp: chỉ vẽ lại sản phẩm của chính C3DTools |
| 4 | **Kẻ đường đỏ** `CTDUONGDO` | Profile Creation Tools | Bấm các đỉnh trên trắc dọc hoặc chọn polyline; bảng nhập R hoặc L cong đứng; kiểm tra TCVN | Trung bình: tạo và sửa PVI chưa chạy thử |
| 5 | **Trắc ngang một chạm** `CTVETRACNGANG` | Bật nguồn lấy mẫu + Create Multiple Section Views + `CTTRACNGANG` + `CTXEPTRANG` | Chọn nhóm cọc, mặt phủ, tỉ lệ, khổ giấy | Trung bình–cao: nguồn lấy mẫu chưa rõ; tạo hàng loạt cần 2021.3 |
| 6 | **Áp khuôn** `CTAPKHUON` | Assembly + corridor, hoặc `APK` của Nova/ADS | Mẫu mặt cắt: bề rộng mặt, lề, dốc ngang, taluy đào/đắp, rãnh, áo đường; dải cọc | Xem mục 6 |
| 7 | **Nhập số liệu khảo sát** `CTNHAPSOLIEU` | Gõ tay sổ đo | Tệp .txt / Excel / .ntd | Trung bình: cần tệp mẫu thật |
| 8 | Hộp thoại khai báo mẫu bảng; nút "nạp mẫu TCVN" | Sửa JSON | — | Thấp |
| 9 | Bổ sung hàng bảng trắc dọc: sơ hoạ tuyến, siêu cao, rãnh | — | — | Thấp; số liệu đã có từ `CTYTC` |
| 10 | Đặt cống, cầu trên trắc dọc; khung tên bản in; khối lượng theo lớp địa chất | — | — | Làm sau |

Đích đến: từ một tuyến đã có cọc, người dùng chạy **bốn lệnh** để ra hồ sơ: `CTVETRACDOC` → `CTDUONGDO` → `CTAPKHUON` → `CTVETRACNGANG` (kèm bảng khối lượng), và **một lệnh** `CTCAPNHAT` mỗi khi sửa thiết kế.

---

## 6. Quyết định cần anh/chị chọn: áp khuôn theo cách nào

| | A. Mẫu mặt cắt tham số, vẽ hình học thường | B. Corridor của Civil 3D |
| --- | --- | --- |
| Cách làm | C3DTools tự tính mặt cắt thiết kế tại mỗi cọc từ cao độ đường đỏ, siêu cao và mở rộng (đã có ở `CTYTC`), mẫu mặt cắt, rồi giao với mặt cắt tự nhiên | C3DTools tạo corridor từ tuyến + đường đỏ + Assembly, lấy mặt phủ corridor làm mặt cắt thiết kế |
| Giống | Nova-TDN, ADS Civil, AND Design | V3Tools, Nova Road 2024, cách dạy Civil 3D |
| Ưu | Không phụ thuộc Assembly hay style; toàn bộ phép tính nằm ở Core nên test được trên macOS; người dùng Nova/AND quen ngay | Có mô hình 3D; khối lượng theo vật liệu của Civil 3D; hợp với quy trình BIM |
| Nhược | Không có mô hình 3D; mặt cắt phức tạp (nhiều cấp taluy, tường chắn) phải tự viết | Rủi ro API cao, chưa chạy thử; cần thư viện Assembly Việt Nam; corridor lỗi rất khó chẩn đoán từ xa |
| Rủi ro | Thấp–trung bình | Cao |

Khuyến nghị của tôi: **làm A trước**. Nó khớp với cách C3DTools đã làm từ đầu (hình học thường, luật ở Core), và với vòng phát triển hiện tại (tôi không chạy được Civil 3D, mỗi lần thử là một vòng cài đặt của anh/chị). B để sau, khi có người thử thường xuyên trên Windows.

---

## 7. Việc cần thử trên Civil 3D trước khi làm (spike)

| # | Câu hỏi | Quyết định phụ thuộc |
| --- | --- | --- |
| S1 | `GetSectionSources()` của nhóm cọc do `CTPHATCOC` tạo có liệt kê sẵn các mặt phủ không? Đặt `IsSampled = true` có sinh ra mặt cắt không? | Lệnh 5 tự động hoàn toàn, hay người dùng phải bấm "Sample More Sources" một lần |
| S2 | `SectionViewGroups.Add(...)` có trên máy đang dùng không (cần 2021.3)? | Lệnh 5 tạo hàng loạt, hay tạo từng view bằng `SectionView.Create` |
| S3 | `Profile.CreateByLayout` + `AddPVISymParabola` / `AddPVIArc`; sửa `ProfilePVI.Station` | Lệnh 4 |
| S4 | `ProfileView.Create` với tỉ lệ đứng và mức so sánh chọn trước: đặt qua style hay thuộc tính nào? | Lệnh 1 |

Tôi có thể gom S1–S4 thành một lệnh thử `CTSPIKE` ghi kết quả vào `trace.log`, anh/chị chỉ cần chạy một lần và gửi lại file.

## 8. Cần từ anh/chị

1. Một bản vẽ trắc dọc và một tờ trắc ngang **đã hoàn thiện** của văn phòng (PDF hoặc DWG): đó là chuẩn cho bố cục, thứ tự hàng, cỡ chữ.
2. Một tệp số liệu khảo sát thật (sổ đo trắc dọc, trắc ngang) ở định dạng văn phòng đang dùng.
3. Bản cập nhật Civil 3D đang cài (Help → About): 2021.3 hay thấp hơn.
4. Chọn A hay B ở mục 6.
5. Thứ tự ưu tiên ở mục 5 có đúng với việc hằng ngày của văn phòng không.

## Nguồn

Phần mềm Việt Nam: andt.vn/forum (t=5052, 659, 88, 4785, 5235, 2941), andt.vn/services/thiet-ke-duong, thietkeduong.com (các bước Nova-TDN 2005), doc.edu.vn (giáo trình Nova-TDN), cadviet.com (diễn đàn Nova), adscivil.vn (blog: trắc dọc, điền thiết kế, áp khuôn, tính diện tích, bảng tổng hợp), forum.rdsic.edu.vn, v3tech.vn (V-Template, V3Tools), novabim.vn, kscongtrinh.com, viet-thanh.vn.
Civil 3D: help.autodesk.com (Create Profile View Wizard; Create Section Views 2021; New Features 2021 DevGuide; API overview), blog.autodesk.io (corridor, baseline region, multiple section views), forums.autodesk.com, civilanarchyden.com, resources.imaginit.com, civil3d.tv.
Tiêu chuẩn: TCVN 4054:2005, 22TCN 263:2000, TCVN 9436:2012.
API: reflection trên `~/.nuget/packages/civil3d2021.base/1.0.0/lib/AeccDbMgd.dll`.
