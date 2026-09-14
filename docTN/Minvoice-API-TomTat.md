# mTVAN — Minvoice — Tóm tắt API (rút từ Minvoice.docx)

> Tài liệu cập nhật: **2025-12-04**
> Giao thức: **HTTPS REST / JSON**. Auth: **token** trong header `Authorization`.
> Base URL (UAT): `https://testmtvan.minvoice.net`
> Wiki gốc: `https://wiki.minvoice.com.vn/s/427a0d5a-e03a-4279-8ca7-b734e11402c5`
> ⚠️ Tài liệu **không ghi base URL Production** — cần hỏi Minvoice.

---

## Danh sách API

| # | Chức năng | Method | Đường dẫn (UAT) |
|---|---|---|---|
| 1 | Đăng nhập | `POST` | `/Account/Login` |
| 2 | Tờ khai đăng ký/thay đổi HĐ thông thường | `POST` | `/Register/mau01` |
| 3 | Tờ khai đăng ký/thay đổi HĐ có mã từ máy tính tiền | `POST` | `/Register/mau01` |
| 4 | Hóa đơn **không mã** | `POST` | `/Invoice/Hdonkma` |
| 5 | Hóa đơn **có mã** | `POST` | `/Invoice/Hdoncma` |
| 6 | Hóa đơn có mã khởi tạo từ **máy tính tiền** | `POST` | `/Invoice/Maytinhtien` |
| 7 | Thông báo HĐĐT có sai sót (04/SS-HĐĐT) | `POST` | `/CancelInvoice/tbaohuy` |
| 8 | Thông báo sai sót cho HĐ máy tính tiền (04/SS-HĐĐT) | `POST` | `/CancelInvoice/TbaohuyMaytinhtien` |
| 9 | Bảng tổng hợp dữ liệu | `POST` | `/Invoice/bangtonghop` |

> Mục 2 và 3 **dùng chung endpoint** `/Register/mau01`, chỉ khác nội dung bên trong XML.

---

## 1. API Đăng nhập

Nguồn: `https://wiki.minvoice.com.vn/s/427a0d5a-e03a-4279-8ca7-b734e11402c5/doc/dang-nhap-jGw4HfukW5`

```
POST https://testmtvan.minvoice.net/Account/Login
Content-Type: application/json
```

### Tham số truyền vào

```json
{
  "username": "minvoice",
  "password": "nampvminvoice",
  "ma_dvcs": "0107726161"
}
```

| Tên trường | Chú thích |
|---|---|
| `username` | Tên đăng nhập |
| `password` | Mật khẩu (tối thiểu 6 ký tự) |
| `ma_dvcs` | Mã đơn vị cơ sở (mã số thuế) |

> ⚠️ Tài liệu không ghi kiểu dữ liệu / độ dài / bắt buộc cho từng trường.

### Response

```json
{
  "code": "00",
  "message": null,
  "token": "cnZNSUx3RVNtODksdaa0TGVldmJjZDNpa2ZaOGJsUG5Bb0FQa3NMR1ozQ1NuWT06TUlOVk9JQ0U6NjM3NzEzNjIxNjc0cjg2MjAz"
}
```

### Bảng mã lỗi đăng nhập

| Mã lỗi | Mô tả |
|---|---|
| `00` | Thành công |
| `01` | Mật khẩu phải từ 6 ký tự trở lên |
| `03` | Không tìm thấy đơn vị đăng nhập: xxxx |
| `04` | Không tìm thấy tên đăng nhập |
| `06` | Tên hoặc mật khẩu không đúng |

> Token khi decode base64 có dạng `<chuỗi>:MINVOICE:<số>` — không phải JWT.
> ⚠️ Tài liệu **không nói thời hạn token**. Cần hỏi Minvoice.

---

## 2. Header dùng chung cho tất cả API nghiệp vụ (mục 2–9)

```
Authorization: Bear {token};{ma_dvcs}
```

Cấu trúc theo tài liệu: `Bear` + **khoảng trắng** + `{chuỗi token}` + **dấu chấm phẩy (;)** + **mã đơn vị (`ma_dvcs`)**

Ví dụ:
```
Authorization: Bear cnZNSUx3RVNtODks...NjIwMw==;0107726161
```

> ⚠️ Tài liệu ghi `Bear` **chứ không phải `Bearer`** — lặp lại nhất quán ở cả 8 mục, nên nhiều khả năng đúng là `Bear`. **Nên confirm với Minvoice trước khi code.**

---

## 3. Body dùng chung cho tất cả API nghiệp vụ (mục 2–9)

**Tất cả 8 API nghiệp vụ đều dùng CHUNG một cấu trúc request:**

```json
{
  "xmlData": "Chuỗi base64 xml hóa đơn",
  "mstNnt": "mã số thuế người nộp thuế",
  "mstTcgp": "mã số thuế của tổ chức giải pháp đã đăng ký với Minvoice",
  "transId": "mã giao dịch id tổ chức giải pháp truyền lên TVAN"
}
```

| Tên trường | Chú thích |
|---|---|
| `xmlData` | Chuỗi **Base64** của XML hóa đơn/tờ khai/thông điệp |
| `mstNnt` | Mã số thuế người nộp thuế |
| `mstTcgp` | Mã số thuế của tổ chức giải pháp (đã đăng ký với Minvoice) |
| `transId` | Mã giao dịch do TCGP sinh, truyền lên TVAN |

> ⚠️ Tài liệu không ghi kiểu dữ liệu / độ dài / bắt buộc / quy tắc sinh `transId`.

---

## 4. Response dùng chung cho tất cả API nghiệp vụ (mục 2–9)

**Tất cả 8 API nghiệp vụ đều trả CHUNG một cấu trúc:**

- `code != "00"` → `data` sẽ là `null` (có lỗi)
- `code == "00"` → trả về mã thông điệp `maThongdiep`

```json
{
  "code": "00",
  "message": null,
  "data": {
    "maThongdiep": "V0106026495AC9708DFB6D64D0896B3F0D62CC27119"
  }
}
```

> ⚠️ **Không có bảng mã lỗi cho các API nghiệp vụ** — chỉ có bảng mã lỗi cho API đăng nhập. Cần hỏi Minvoice.

---

## 5. Chi tiết từng API nghiệp vụ

### 5.1. Tờ khai đăng ký/thay đổi hóa đơn thông thường

- Endpoint: `POST /Register/mau01`
- Nguồn: `.../doc/to-khai-dang-kythay-doi-hoa-don-thong-thuong-tpKOSb9cvZ`
- Request/Response: theo cấu trúc chung (mục 3, 4)

### 5.2. Tờ khai đăng ký/thay đổi hóa đơn có mã từ máy tính tiền

- Endpoint: `POST /Register/mau01` (**trùng với 5.1**)
- Nguồn: `.../doc/to-khai-dang-ky-thay-doi-hoa-don-co-ma-tu-may-tinh-tien-g3BQTliGry`
- **Khác biệt:** cấu trúc JSON giữ nguyên, chỉ khác nội dung bên trong XML:
  - Phiên bản tờ khai **2.0.1**
  - **Bổ sung thẻ `CMTMTTien` trong thẻ `HThuc`**
- **xmlData mẫu dạng Base64:** `https://newtextdocument.com/t/466f147bd3`

### 5.3. Hóa đơn không mã

- Endpoint: `POST /Invoice/Hdonkma`
- Nguồn: `.../doc/hoa-don-khong-ma-HN54S4Vgin`

### 5.4. Hóa đơn có mã

- Endpoint: `POST /Invoice/Hdoncma`
- Nguồn: `.../doc/hoa-don-co-ma-yZkBr9NqFU`

### 5.5. Hóa đơn có mã khởi tạo từ máy tính tiền

- Endpoint: `POST /Invoice/Maytinhtien`
- Nguồn: `.../doc/hoa-don-co-ma-khoi-tao-tu-may-tinh-tien-LzXPlSPthN`

**Khác biệt quan trọng:**
- JSON đầu vào/đầu ra **giống hóa đơn có mã thông thường**, chỉ khác dữ liệu XML.
- Hóa đơn máy tính tiền **KHÔNG ký vào từng hóa đơn** mà **ký bao bên ngoài thông điệp** trước khi gửi lên TVAN.
- Có thể đóng gói **nhiều hóa đơn trong một thông điệp**, nhưng **không vượt quá 2 MB**.

**Cách đóng gói thẻ `TTChung`:**

| Thẻ | Ý nghĩa | Độ dài max | Giá trị |
|---|---|---|---|
| `PBan` | Phiên bản XML của gói dữ liệu | 5 | Máy tính tiền mặc định **`2.0.1`** |
| `MNGui` | Nơi gửi — mã cố định của TVAN để giao tiếp với TCT | 14 | Mặc định **`V0106026495`** |
| `MNNhan` | Nơi nhận — mã cố định của Tổng cục thuế | 3 | Mặc định **`TCT`** |
| `MLTDiep` | Mã loại thông điệp | 3 | Máy tính tiền cố định **`206`** |
| `MTDiep` | Mã thông điệp do TCGP tự sinh và lưu lại | 46 | `V0106026495` + GUID (bỏ dấu gạch ngang, **UPPERCASE**)<br>VD: `V0106026495F1D46C91708E476EB6EAF8AB51A654DA` |
| `MST` | Mã số thuế của khách hàng | 14 | Bắt buộc **10 hoặc 14 ký tự**. Chi nhánh phải có dấu gạch ngang |
| `SLuong` | Số lượng hóa đơn trong phần dữ liệu | 8 | Số hóa đơn trong thẻ `DLieu` |

### 5.6. Thông báo hóa đơn điện tử có sai sót (Mẫu 04/SS-HĐĐT)

- Endpoint: `POST /CancelInvoice/tbaohuy`
- Nguồn: `.../doc/thong-bao-hoa-don-dien-tu-co-sai-sot-mau-04ss-hddt-crT1AU881k`

### 5.7. Thông báo sai sót cho hóa đơn Máy tính tiền (04/SS-HĐĐT)

- Endpoint: `POST /CancelInvoice/TbaohuyMaytinhtien`
- Nguồn: `.../doc/thong-bao-hoa-don-sai-sot-cho-hoa-don-may-tinh-tien-04ss-hddt-G0fOEptwpA`

**Khác biệt:**
- Cấu trúc JSON **không khác gì hủy hóa đơn bình thường**.
- TVAN đóng gói và gửi lên thuế là **thông điệp `303`** thay vì `300` như hóa đơn thông thường.
- XML: phiên bản **`2.0.1`**; thẻ **`MCQTCap` đổi thành `MCCQT`**.

### 5.8. Bảng tổng hợp dữ liệu

- Endpoint: `POST /Invoice/bangtonghop`
- Nguồn: `.../doc/bang-tong-hop-du-lieu-Ccg72OTpJU`

---

## 6. Những gì tài liệu THIẾU (cần hỏi Minvoice)

1. **Base URL Production** — chỉ có UAT (`testmtvan.minvoice.net`).
2. **API lấy kết quả / tra cứu trạng thái** — hoàn toàn KHÔNG có. Sau khi gửi chỉ nhận `maThongdiep`, không biết lấy kết quả từ CQT về bằng cách nào.
3. **Cơ chế callback/webhook** — không hề nhắc tới.
4. **Bảng mã lỗi cho 8 API nghiệp vụ** — chỉ có bảng mã lỗi của API đăng nhập.
5. **Thời hạn token / refresh token.**
6. **`Bear` hay `Bearer`** trong header `Authorization` — tài liệu ghi `Bear` nhất quán, nhưng đây là cách viết bất thường.
7. **Kiểu dữ liệu / độ dài / bắt buộc** của `xmlData`, `mstNnt`, `mstTcgp`, `transId`.
8. **Quy tắc sinh `transId`** và ràng buộc duy nhất.
9. **Link XML mẫu bị thiếu ở 2 chỗ.** Chỉ mục 5.2 có hyperlink thật (`https://newtextdocument.com/t/466f147bd3`). Còn "xem xmlData mẫu dạng Base64:" (mục 5.5) và "Xem XML chi tiết:" (mục 5.7) **không gắn link nào** — chữ để trống. Các ảnh "Luồng tích hợp" là hình, không có nội dung text.
10. **Bảng `MLTDiep` đầy đủ** — chỉ thấy `206` (máy tính tiền), `300` và `303` (sai sót) rải rác trong mô tả.
