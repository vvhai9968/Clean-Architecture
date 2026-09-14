# HILO T-VAN — Tóm tắt API (rút từ Hilo.docx)

> Giao thức: **HTTPS REST / JSON**. Auth: **JWT Bearer token**.
> Base URL (UAT): `https://uatapitctn.hilo.com.vn`
> ⚠️ Tài liệu **không ghi base URL Production** — cần hỏi Hilo.

---

## Danh sách API

| # | Chức năng | Method | Đường dẫn |
|---|---|---|---|
| 1 | Đăng nhập (lấy token) | `POST` | `/api/authentication/gettoken` |
| 2 | Gửi dữ liệu XML **1 phần** | `POST` | `/api/einvoicesolution/send` |
| 3 | Gửi dữ liệu XML **toàn bộ** | `POST` | `/api/einvoicesolution/sendpos` |
| 4 | Lấy kết quả mới nhất | `GET` | `/api/einvoicesolution/get` |

---

## 1. API Đăng nhập

**Chức năng:** lấy token để dùng cho các đầu API khác.

```
POST /api/authentication/gettoken
Content-Type: application/json
```

### Tham số truyền vào (body)

| STT | Tên trường | Bắt buộc | Kiểu | Độ dài | Chú thích |
|---|---|---|---|---|---|
| 1 | `TaxCode` | ✅ | String | 15 | Mã số thuế |
| 2 | `UserName` | ✅ | String | 50 | Tài khoản cung cấp cho đối tác |
| 3 | `Password` | ✅ | String | 50 | Mật khẩu cung cấp cho đối tác |

### Request mẫu

```bash
curl -X 'POST' \
  'https://uatapitctn.hilo.com.vn/api/authentication/gettoken' \
  -H 'accept: application/json' \
  -H 'Content-Type: application/json' \
  -d '{
    "TaxCode": "0106713804",
    "UserName": "demo",
    "Password": "demo"
  }'
```

### Response

```json
{
  "StatusCode": 200,
  "Code": 200,
  "Information": "Lấy token thành công",
  "Messages": [
    {
      "Type": { "Name": "Success", "Description": "success", "Value": 2 },
      "Messages": ["Lấy token thành công"]
    }
  ],
  "TraceIdentifier": "80002844-1000-5a00-b63f-84710c7967bb",
  "Data": {
    "accessToken": "eyJhbGciOiJodHRwOi8vd3d3LnczLm9yZy8yMDAxLzA0L3htbGRzaWctbW9yZSNyc2Etc2hhMjU2...",
    "tokenType": "Bearer"
  }
}
```

**Payload JWT chứa** (decode từ token mẫu): `aud`, `iss`, `exp`, `iat`, `nbf`, `RemoteIpAddress`, `RemotePort`, `UserId`, `KeyProduct`, `UserName`, `UserType`, `FullName`, `CompanyId`, `CompanyTaxCode`, `CompanyType`.

> ⚠️ Trong token mẫu `iat = nbf = exp = 1780389531` → **thời hạn token bằng 0**. Tài liệu không nói token sống bao lâu / có refresh token không. **Cần hỏi Hilo.**

---

## 2. API gửi dữ liệu XML 1 phần

**Chức năng:** nhận một phần XML, Hilo đóng gói dữ liệu rồi gửi thuế.

```
POST /api/einvoicesolution/send
Content-Type: application/json
```

### Header

| STT | Tên trường | Bắt buộc | Kiểu | Độ dài | Chú thích |
|---|---|---|---|---|---|
| 1 | `Authorization` | ✅ | String | 1000 | Token trả về khi gọi API đăng nhập (`Bearer <token>`) |
| 2 | `CallBackUrl` | ✅ | String | 500 | Link webhook của đối tác để Hilo thông báo khi có kết quả mới |

```
-H 'Authorization: Bearer token'
-H 'CallbackUrl: https://example.com/webhook?id=1'
```

### Tham số truyền vào (body)

| STT | Tên trường | Bắt buộc | Kiểu | Độ dài | Chú thích |
|---|---|---|---|---|---|
| 1 | `PBan` | ✅ | String | 15 | Phiên bản XML gửi lên — `2.1.0` |
| 2 | `MNGui` | ✅ | String | 50 | Mã người gửi (`V` + MST đối tác), vd `V0106713804` |
| 3 | `MNNhan` | ✅ | String | 50 | Mã người nhận (`V` + MST Hilo), vd `V0106713804` |
| 4 | `MLTDiep` | ✅ | String | 50 | Loại thông điệp muốn gửi |
| 5 | `MTDiep` | ✅ | String | 50 | Mã thông điệp bên đối tác gửi qua HILO (**duy nhất**) |
| 6 | `MST` | ✅ | String | 15 | Mã số thuế người bán |
| 7 | `SLuong` | ✅ | int | — | Số lượng XML gửi lên |
| 8 | `XmlData` | ✅ | Array | — | Danh sách XML cần gửi |

> ⚠️ Bảng gốc ghi cột "Kiểu dữ liệu" của `XmlData` là "Có" (lỗi copy/paste trong tài liệu). Theo ví dụ thì đây là mảng object `[{ "Xml": "..." }]`.

### Request mẫu

```bash
curl -X 'POST' \
  'https://uatapitctn.hilo.com.vn/api/einvoicesolution/send' \
  -H 'accept: application/json' \
  -H 'Authorization: Bearer token' \
  -H 'CallBackUrl: https://example.com/webhook?id=1' \
  -H 'Content-Type: application/json' \
  -d '{
    "PBan": "2.1.0",
    "MNGui": "V0106713804",
    "MNNhan": "V0106713804",
    "MLTDiep": 100,
    "MTDiep": "V0106713804019E8770CB137DDE8F4BCB8F9A473780",
    "MST": "0106713804",
    "SLuong": 1,
    "XmlData": [
      { "Xml": "<HDon></HDon>" }
    ]
  }'
```

### Response

```json
{
  "StatusCode": 100,
  "Code": 200,
  "Error": "Thông báo khi thất bại",
  "Information": "Thông báo khi thành công",
  "Messages": [
    {
      "Type": { "Name": "", "Description": "", "Value": 0 },
      "Messages": [""]
    }
  ],
  "TraceIdentifier": "8000000a-0005-fb00-b63f-84710c7967bb",
  "Data": {
    "Id": "00000000000000000000000000000000",
    "PBan": "",
    "MNGui": "",
    "MNNhan": "",
    "MLTDiep": 100,
    "MTDiep": "",
    "MST": "",
    "SLuong": 0,
    "MNGuiTN": "",
    "MNNhanTN": "",
    "MTDiepTN": "",
    "TGNhan": "2026-06-02T15:36:38"
  }
}
```

---

## 3. API gửi dữ liệu XML toàn bộ

**Chức năng:** nhận toàn bộ XML thông điệp gửi thuế, Hilo edit lại một số thông tin trong thông điệp.

```
POST /api/einvoicesolution/sendpos
Content-Type: application/json
```

### Header

Giống mục 2: `Authorization` (✅, String, 1000) và `CallBackUrl` (✅, String, 500).

### Tham số truyền vào (body)

| STT | Tên trường | Bắt buộc | Kiểu | Độ dài | Chú thích |
|---|---|---|---|---|---|
| 1 | `XML` | ✅ | String | **1 MB** | XML của toàn bộ thông điệp gửi thuế |

> ⚠️ Bảng ghi tên trường là `XML` nhưng JSON mẫu lại dùng key `"Xml"`. **Dùng theo ví dụ (`Xml`), nên confirm lại với Hilo.**

### Request mẫu

```bash
curl -X 'POST' \
  'https://uatapitctn.hilo.com.vn/api/einvoicesolution/sendpos' \
  -H 'accept: application/json' \
  -H 'Authorization: Bearer token' \
  -H 'Content-Type: application/json' \
  -H 'CallBackUrl: https://example.com/webhook?id=1' \
  -d '{ "Xml": "<TDiep></TDiep>" }'
```

### Response

Cấu trúc **giống hệt** response của mục 2.

---

## 4. API lấy thông tin kết quả mới nhất

**Chức năng:** lấy lại thông tin kết quả sau khi thuế xử lý mới nhất.

```
GET /api/einvoicesolution/get?MTDiep={MTDiep}
```

### Header

| Tên trường | Bắt buộc | Chú thích |
|---|---|---|
| `Authorization` | ✅ | `Bearer <token>` |

### Tham số truyền vào (query string)

| STT | Tên trường | Bắt buộc | Kiểu | Độ dài | Chú thích |
|---|---|---|---|---|---|
| 1 | `MTDiep` | ✅ | String | 15 | Mã thông điệp duy nhất mà đối tác đã gửi qua |

> ⚠️ Bảng ghi độ dài `15` nhưng `MTDiep` thực tế trong ví dụ dài **43 ký tự** (`V0106713804019E8770CB137DDE8F4BCB8F9A473780`). **Lỗi tài liệu — cần confirm.**

### Request mẫu

```bash
curl -X 'GET' \
  'https://uatapitctn.hilo.com.vn/api/einvoicesolution/get?MTDiep=1' \
  -H 'accept: application/json' \
  -H 'Authorization: Bearer token'
```

### Response

```json
{
  "StatusCode": 200,
  "Code": 200,
  "Information": "Thành công",
  "Messages": [],
  "TraceIdentifier": "8000e9b5-0000-ec00-b63f-84710c7967bb",
  "Data": {
    "Id": "019e8770cb167f98ada78f271f323c6c",
    "MTDiep": "V0106713804C355D2C72B7F4E00BC17EBA2917B77EC",
    "MTDTChieu": "V0106713804019E8770CB137DDE8F4BCB8F9A473780",
    "MLTDiep": 100,
    "MNGui": "V0106713804",
    "MNNhan": "V0106713804",
    "MNGuiTN": "V0106713804",
    "MNNhanTN": "TCT",
    "TGNhan": "2026-06-02T15:26:16",
    "Response": [
      {
        "MLTDiep": 102,
        "Message": "TCT không tiếp nhận Tờ khai đăng ký thay đổi thông tin sử dụng hóa đơn điện tử/chứng từ điện tử",
        "MTDiep": "TCTC33A4955804A4428AECE4AC0509AF62B",
        "MTDiepTN": "",
        "XmlReceiveTN": "<TDiep>...</TDiep>",
        "XmlReceive": "<TDiep>...</TDiep>",
        "Status": 3,
        "CreateDate": "2026-06-02T15:26:44",
        "LastUpdateDate": "2026-06-02T15:26:45",
        "Order": 1,
        "CurrentStatus": 0,
        "Reason": [
          {
            "MLoi": "1034",
            "MTLoi": "Thông tin liên quan đến người đại diện pháp luật không khớp đúng so với Đăng ký doanh nghiệp/Đăng ký thuế.. Số CCCD/CMND, Ngày sinh, Tên đại diện pháp luật",
            "HDXLy": "",
            "GChu": ""
          }
        ],
        "KafkaIn": {},
        "KafkaOut": {
          "Topic": "tvan_hilo_out",
          "Date": "2026-06-02T15:27:07",
          "Offset": 3191535,
          "Partition": 0
        },
        "Id": "019e8771385a7cc882a00efc4f47869c"
      },
      {
        "MLTDiep": 999,
        "Message": "TCT đã tiếp nhận",
        "MTDiep": "TCT4CC711035B454F16B937DE9AE7FBCA78",
        "Status": -1,
        "Order": 0,
        "CurrentStatus": 1,
        "Reason": [],
        "KafkaOut": { "Topic": "tvan_hilo_out", "Offset": 3191534, "Partition": 0 },
        "Id": "019e87712e607b3081defb19b627a878"
      }
    ]
  }
}
```

### Mô tả các field trong `Data.Response[]`

| Field | Ý nghĩa |
|---|---|
| `MLTDiep` | Mã loại thông điệp phản hồi từ CQT (`999` = ACK đã tiếp nhận, `102` = không tiếp nhận tờ khai thay đổi TT, …) |
| `Message` | Diễn giải kết quả bằng tiếng Việt |
| `MTDiep` | Mã thông điệp do TCT sinh |
| `MTDiepTN` | Mã thông điệp phía truyền nhận |
| `XmlReceiveTN` | XML thông điệp nhận từ TCT (bản truyền nhận) |
| `XmlReceive` | XML thông điệp nhận từ TCT |
| `Status` | Trạng thái xử lý (thấy giá trị `3` và `-1` trong ví dụ) |
| `CurrentStatus` | Trạng thái hiện tại (thấy `0` và `1`) |
| `Order` | Thứ tự thông điệp trong luồng (0, 1, …) |
| `CreateDate` / `LastUpdateDate` | Thời điểm tạo / cập nhật |
| `Reason[]` | Danh sách lỗi: `MLoi` (mã lỗi), `MTLoi` (mô tả lỗi), `HDXLy` (hướng dẫn xử lý), `GChu` (ghi chú) |
| `KafkaIn` / `KafkaOut` | Metadata Kafka nội bộ Hilo (`Topic`, `Date`, `Offset`, `Partition`) |

> ⚠️ Tài liệu **không có bảng enum cho `Status` / `CurrentStatus`**, cũng không có danh mục mã lỗi `MLoi`. **Cần hỏi Hilo.**

---

## Cơ chế nhận kết quả

Hai đường:

1. **Webhook (push)** — khai báo `CallBackUrl` trong header khi gửi. Hilo gọi về khi có kết quả mới.
   > ⚠️ Tài liệu **không mô tả payload webhook**, không có cơ chế xác thực chữ ký webhook, không có chính sách retry. **Cần hỏi Hilo.**
2. **Polling (pull)** — gọi `GET /api/einvoicesolution/get?MTDiep=...`

---

## Những gì tài liệu THIẾU (cần hỏi Hilo)

1. **Base URL Production** — chỉ có UAT.
2. **Payload của webhook `CallBackUrl`** — hoàn toàn không mô tả.
3. **Thời hạn token / refresh token** — token mẫu có `exp == iat`.
4. **Enum `Status`, `CurrentStatus`** trong `Response[]`.
5. **Danh mục mã lỗi `MLoi`** — chỉ thấy `1034` trong ví dụ.
6. **Bảng `MLTDiep` hợp lệ** — chỉ thấy `100`, `102`, `999` rải rác trong ví dụ, không có bảng tổng hợp.
7. **`StatusCode` vs `Code`** — response mẫu có `StatusCode: 100` kèm `Code: 200`, không giải thích khác nhau chỗ nào.
8. **Bảng ghi `XML`, ví dụ dùng `Xml`** (mục 3) và độ dài `MTDiep` = 15 nhưng thực tế 43 (mục 4) — lỗi tài liệu.
