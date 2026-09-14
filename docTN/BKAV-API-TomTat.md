# BKAV Truyền Nhận — Tóm tắt API (rút từ BKAV.docx)

> Chuẩn: Quyết định 1450/QĐ-TCT. Giao thức: **SOAP ASMX** (hỗ trợ cả XML/SOAP và JSON/REST).

---

## 1. Endpoint

| Môi trường | Web Service URL |
|---|---|
| **Production** | `https://wstn.ehoadon.vn/WSPublicETN.asmx/ExecuteCommand` |
| **Demo / Test** | `https://wstndemo.ehoadon.vn/WSPublicETN.asmx/ExecuteCommand` |

Portal (web UI, không phải API):
- Production: `https://tn.ehoadon.vn`
- Demo: `https://tndemo.ehoadon.vn`

**Chỉ có DUY NHẤT 1 API**: `ExecuteCommand`. Mọi nghiệp vụ (gửi tờ khai, hóa đơn, sai sót, bảng tổng hợp, lấy thông báo) đều đi qua endpoint này, phân biệt bằng trường `CmdType` trong payload.

---

## 2. Login / Xác thực

**Không có API login.** Tài liệu KHÔNG mô tả endpoint đăng nhập, không có username/password, không có access token theo phiên.

Xác thực bằng cặp thông tin tĩnh do Bkav cấp riêng cho từng Partner:

| Thông tin | Vai trò |
|---|---|
| `PartnerGUID` | Định danh Partner — gửi **plaintext** trong body request |
| `PartnerToken` | Khóa mã hóa dữ liệu trên đường truyền — **không gửi lên**, chỉ dùng để mã hóa/giải mã |

`PartnerToken` có dạng `<key_base64>:<iv_base64>`, ví dụ:

```
PartnerToken: 54dSxtErH+vsKKfL4PKaoerNYE6dwzmpzkLAxity8F4=:+bRSEW7FUEnzLy9xjuP5wA==

key = base64_decode("54dSxtErH+vsKKfL4PKaoerNYE6dwzmpzkLAxity8F4=")
iv  = base64_decode("+bRSEW7FUEnzLy9xjuP5wA==")
```

Ngoài ra tài liệu có nhắc khái niệm `User` / `User demo` (tài khoản khách hàng trên hệ thống truyền nhận) nhưng **không mô tả cách truyền user này qua API**.

---

## 3. Signature của API

```
string ExecuteCommand(string PartnerGUID, string EncryptedCommandData)
```

### Request

- **Method**: `POST`
- **Header**: `Content-Type: text/xml`

**Body (SOAP):**

```xml
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
               xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <ExecuteCommand xmlns="http://tempuri.org/">
      <PartnerGUID>8bd1e2d0-ae76-4aeb-a603-57eac7e2d159</PartnerGUID>
      <EncryptedCommandData>Dữ liệu đã mã hoá, nén và Encode Base64</EncryptedCommandData>
    </ExecuteCommand>
  </soap:Body>
</soap:Envelope>
```

**Tham số truyền vào:**

| Parameter | Type | Required | Mô tả |
|---|---|---|---|
| `PartnerGUID` | UUID | ✅ | GUID định danh Partner, Bkav cấp riêng |
| `EncryptedCommandData` | String | ✅ | Object `CommandData` (json/xml) → string → GZip → AES-256 bằng `PartnerToken` → Base64 |

### Response

```json
{
  "d": "Chuỗi Result đã được nén, mã hoá và encode Base64"
}
```

> ⚠️ Tài liệu **không mô tả cấu trúc chi tiết của object `Result`** (không có bảng field, không có mã lỗi / ErrorCode / Status). Mục "CommandData & Result" chỉ viết phần CommandData. **Cần hỏi Bkav bổ sung.**

---

## 4. Quy trình mã hóa / giải mã

### Chiều gửi — `EncryptedCommandData`

1. Khởi tạo object `CommandData`
2. Serialize thành string (JSON/XML) → mảng byte
3. Nén bằng **GZIP**
4. Mã hóa **AES-256 mode CBC** với `key` + `iv` tách từ `PartnerToken`
5. **Base64 encode** → được `EncryptedCommandData`

### Chiều nhận — `EncryptedResult`

1. **Base64 decode** chuỗi `d`
2. Giải mã **AES-256** bằng `PartnerToken`
3. **Giải nén GZIP**
4. Convert sang string → deserialize thành object `Result`

---

## 5. Cấu trúc `CommandData`

```xml
<?xml version="1.0" encoding="utf-8"?>
<CommandData xmlns:xsd="http://www.w3.org/2001/XMLSchema"
             xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CmdType>111</CmdType>
  <CommandObject xmlns:q1="http://www.w3.org/2001/XMLSchema"
                 p3:type="q1:string"
                 xmlns:p3="http://www.w3.org/2001/XMLSchema-instance">
    Dữ liệu Hoá đơn/Tờ khai/Thông điệp dạng XML đã escape ký tự đặc biệt
  </CommandObject>
</CommandData>
```

| Field | Mô tả |
|---|---|
| `CmdType` | Mã lệnh — quyết định nghiệp vụ (xem mục 7) |
| `CommandObject` | Payload XML đã **escape ký tự đặc biệt** |

### Quy tắc escape trong `CommandObject`

| Ký tự | Diễn giải | Thay bằng |
|---|---|---|
| `'` | Nháy đơn | `&apos;` |
| `"` | Nháy kép | `&quot;` |
| `&` | Và | `&amp;` |
| `<` | Nhỏ hơn | `&lt;` |
| `>` | Lớn hơn | `&gt;` |

> **Lưu ý escape 2 lần**: nếu dữ liệu KH nhập đã chứa ký tự đặc biệt (VD tên đơn vị `Công ty TNHH & DV ABC`) thì phải escape thêm 1 lần trước khi tạo hóa đơn → `Công ty TNHH &amp; DV ABC`, và sau khi escape vào `CommandObject` sẽ thành `&amp;amp;`.

---

## 6. Ba Mode truyền dữ liệu

| Mode | TCGP gửi gì | Bkav làm gì |
|---|---|---|
| **Mode 1** | Thông điệp đã đóng gói **đầy đủ** theo QĐ 1450 (đã ký số) | Gửi thẳng lên CQT |
| **Mode 2** | Chỉ nội dung **Tờ khai/Hóa đơn XML đã ký số** | Bkav đóng gói thành thông điệp rồi gửi CQT |
| **Mode 3** | Chỉ **nội dung các phần tử** của Tờ khai/Hóa đơn (chưa ký) | Bkav tạo file XML → TCGP lấy về ký số → gửi lại → Bkav đóng gói & gửi CQT |

**Mode 3 — luồng 5 bước:**

1. TCGP gửi nội dung chi tiết Tờ khai/Hóa đơn lên Bkav
2. Bkav tạo file XML chuẩn QĐ 1450
3. TCGP gửi yêu cầu lấy file XML về
4. TCGP ký số lên file XML, gửi lại Bkav
5. Bkav đóng gói thành thông điệp và gửi lên CQT

> ⚠️ Tài liệu chỉ liệt kê `CmdType` cho **Mode 1 và Mode 2**. **Không có bảng CmdType riêng cho Mode 3.** Cần hỏi Bkav.

---

## 7. Bảng mã `CmdType`

### Mode 1 — gửi thông điệp đã đóng gói đầy đủ

| CmdType | MLTDiep | Nghiệp vụ |
|---|---|---|
| `300` | 100 | Thông điệp gửi tờ khai (đăng ký/thay đổi TT) |
| `301` | 200 | Thông điệp cấp mã Hóa đơn |
| `302` | 203 | Thông điệp chuyển dữ liệu hóa đơn **không mã** |
| `302` | 300 | Thông điệp sai sót |
| `305` | 400 | Thông điệp gửi bảng tổng hợp hóa đơn |

> ⚠️ `302` xuất hiện 2 lần (MLTDiep 203 và 300) — **nghi ngờ typo trong tài liệu**, cần confirm với Bkav (có thể đúng là `303` cho sai sót).

### Mode 2 — gửi Tờ khai/Hóa đơn đã ký số

| CmdType | MLTDiep | Nghiệp vụ |
|---|---|---|
| `500` | 100 | Thông điệp gửi tờ khai |
| `501` | 200 | Thông điệp cấp mã Hóa đơn |
| `502` | 203 | Thông điệp chuyển dữ liệu hóa đơn không mã |
| `503` | 300 | Thông điệp gửi thông báo sai sót |
| `505` | 400 | Thông điệp gửi bảng tổng hợp hóa đơn |

### Lấy thông báo / tra cứu

| CmdType | Nghiệp vụ | Format CommandObject |
|---|---|---|
| `801` | Lấy **toàn bộ thông điệp** theo GUID | XML |
| `802` | Lấy **thẻ dữ liệu** theo GUID | XML |

---

## 8. Cấu trúc thông điệp `TDiep` (dùng cho Mode 1)

```xml
<?xml version="1.0" encoding="utf-8"?>
<TDiep>
  <TTChung>
    <PBan>2.0.0</PBan>
    <MNGui>V0101360697</MNGui>
    <MNNhan>TCT</MNNhan>
    <MLTDiep>300</MLTDiep>
    <MTDiep>V01013606978D5C49198D4B4C27BBEF04B9A0188194</MTDiep>
    <MTDTChieu/>
    <MST>0101360697-999</MST>
    <SLuong>1</SLuong>
  </TTChung>
  <DLieu>Dữ liệu thông điệp</DLieu>
</TDiep>
```

| Tên trường | Độ dài | Dữ liệu |
|---|---|---|
| `PBan` | 6 | `2.0.0` |
| `MNGui` | 14 | Mã nơi gửi |
| `MNNhan` | 14 | Mã nơi nhận của CQT (mặc định `TCT`) |
| `MLTDiep` | 3 | Mã loại thông điệp |
| `MTDiep` | 46 | `V` + MST + GUID (bỏ dấu cách) |
| `MTDTChieu` | 46 | Mã thông điệp tham chiếu |
| `MST` | 14 | Mã số thuế đơn vị |
| `SLuong` | 7 | Số lượng |
| `DLieu` | Max | Dữ liệu Hóa đơn/tờ khai **đã ký số** |

---

## 9. Mã phản hồi từ Cơ quan Thuế (đọc `MLTDiep` của thông điệp phản hồi)

| MLTDiep | Ý nghĩa | Thẻ con cần đọc |
|---|---|---|
| `999` | Phản hồi kỹ thuật (ACK) | `TTTNhan`: `0` = hợp lệ, `1` = lỗi dữ liệu |
| `202` | Kết quả **cấp mã** hóa đơn | — |
| `204` | Kết quả kiểm tra dữ liệu hóa đơn | `LTBao`: `1` = lỗi HĐ có mã, `2` = hợp lệ, `3`/`9` = lỗi |
| `301` | Kết quả xử lý thông báo hóa đơn sai sót | `TTTNCCQT`: `1` = chấp nhận, `2` = không chấp nhận |
| `100` / `101` | Tờ khai đăng ký / ủy nhiệm lập HĐ | — |
| `200` | Cấp mã hóa đơn | — |
| `203` | Hóa đơn không mã | — |
| `300` | Thông báo sai sót | — |
| `400` | Bảng tổng hợp | — |

**Kết quả duyệt tờ khai:**

| Thẻ | Giá trị | Ý nghĩa |
|---|---|---|
| `THop` | `1` | Tiếp nhận tờ khai đăng ký sử dụng |
| `THop` | `2` | **Không** tiếp nhận tờ khai đăng ký sử dụng |
| `THop` | `3` | Tiếp nhận tờ khai đăng ký thay đổi |
| `THop` | `4` | **Không** tiếp nhận tờ khai đăng ký thay đổi |
| `TTXNCQT` | `1` | Tờ khai **được** CQT duyệt → trạng thái `ĐD` |
| `TTXNCQT` | `2` | Tờ khai **không được** duyệt → trạng thái `KD` |

**Trạng thái hóa đơn nhắc trong tài liệu:** `CXL` (chờ xử lý của CQT), `ĐD` (đã duyệt), `KD` (không được duyệt), `CML` (cấp mã không thành công).

---

## 10. Ký số theo QĐ 1450

**Quy trình ký (5 bước):**

1. Băm dữ liệu Hóa đơn cần ký → thông điệp đại diện `MD1`
2. Lấy thời gian ký từ hệ thống → đưa vào thẻ `<Timestamp>`
3. Băm thời gian ký → `MD2`; đưa `MD1` và `MD2` vào `<SignedInfo>`
4. Băm nội dung `<SignedInfo>` → `MD3` (**không** đưa vào file XML)
5. Mã hóa `MD3` bằng Private Key → `<SignatureValue>` đưa vào file XML

**Thuật toán theo mẫu tài liệu:**

- Canonicalization: `http://www.w3.org/TR/2001/REC-xml-c14n-20010315#WithComments`
- SignatureMethod: `rsa-sha1`
- DigestMethod: `sha1`
- 2 `<Reference>`: `URI=""` (enveloped-signature) và `URI="#Timestamp"`
- `<Object Id="Timestamp">` chứa `<SigningTime>2021-10-22T11:28:54</SigningTime>`

**Mẫu chữ ký:**

```xml
<Signature xmlns="http://www.w3.org/2000/09/xmldsig#">
  <SignedInfo>
    <CanonicalizationMethod Algorithm="http://www.w3.org/TR/2001/REC-xml-c14n-20010315#WithComments"/>
    <SignatureMethod Algorithm="http://www.w3.org/2000/09/xmldsig#rsa-sha1"/>
    <Reference URI="">
      <Transforms>
        <Transform Algorithm="http://www.w3.org/2000/09/xmldsig#enveloped-signature"/>
      </Transforms>
      <DigestMethod Algorithm="http://www.w3.org/2000/09/xmldsig#sha1"/>
      <DigestValue>kISRCkSi1FDoLaFulYxCX8XjSyM=</DigestValue>
    </Reference>
    <Reference URI="#Timestamp">
      <DigestMethod Algorithm="http://www.w3.org/2000/09/xmldsig#sha1"/>
      <DigestValue>dEkmzEeDvTl422dCZOC07jdga8E=</DigestValue>
    </Reference>
  </SignedInfo>
  <SignatureValue>q4aNeJnOsWlGeTKQCPUSWTp2PNzl8zM5GU7PBb2NZreB...</SignatureValue>
  <KeyInfo>
    <X509Data>
      <X509SubjectName>C=VN, CN=BAS_Client</X509SubjectName>
      <X509Certificate>MIIDqDCCApCgAwIBAgIQVANhPkrZL+b1nNFSTsScBDANBgkq...</X509Certificate>
    </X509Data>
  </KeyInfo>
  <Object Id="Timestamp">
    <SignatureProperties>
      <SignatureProperty Target="">
        <SigningTime>2021-10-22T11:28:54</SigningTime>
      </SignatureProperty>
    </SignatureProperties>
  </Object>
</Signature>
```

---

## 11. Ví dụ payload thực tế (Mode 2, CmdType 501 — cấp mã hóa đơn)

```xml
<?xml version="1.0" encoding="utf-8"?>
<CommandData xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CmdType>501</CmdType>
  <CommandObject ...>&lt;ArrayOfInvoiceDataWS&gt;&lt;InvoiceDataWS&gt;&lt;Invoice&gt;...&lt;/Invoice&gt;...&lt;/InvoiceDataWS&gt;&lt;/ArrayOfInvoiceDataWS&gt;</CommandObject>
</CommandData>
```

**Các field trong `<Invoice>` (từ ví dụ trong tài liệu):**

`InvoiceTypeID`, `InvoiceDate`, `BuyerName`, `BuyerTaxCode`, `BuyerCode`, `BuyerUnitName`, `BuyerAddress`, `BuyerBankAccount`, `PayMethodID`, `ReceiveTypeID`, `ReceiverEmail`, `ReceiverMobile`, `ReceiverAddress`, `ReceiverName`, `Note`, `BillCode`, `CurrencyID`, `ExchangeRate`, `InvoiceForm`, `InvoiceSerial`, `InvoiceNo`, `SignedDate`, `OriginalInvoiceIdentify`

**`<ListInvoiceDetailsWS>` → `<InvoiceDetailsWS>`:**

`ItemName`, `UnitName`, `Qty`, `Price`, `Amount`, `TaxRateID`, `TaxRate`, `TaxAmount`, `DiscountRate`, `DiscountAmount`, `IsDiscount`

**Cấp `InvoiceDataWS`:** `PartnerInvoiceID`, `PartnerInvoiceStringID`

> ⚠️ Tài liệu **không có bảng mô tả kiểu dữ liệu / độ dài / bắt buộc** cho các field này — chỉ có 1 ví dụ XML. Cần xin Bkav file XSD hoặc bảng đặc tả.

---

## 12. Link tham chiếu trong tài liệu

**Sample code XML (Google Drive, cần quyền truy cập):**

- Thông điệp gửi tờ khai đăng ký/thay đổi TT: `https://drive.google.com/file/d/15PmBbMmsl6dfwRJlg-BR3iwIS4eeT088/view?usp=sharing`
- Thông điệp cấp mã hóa đơn: `https://drive.google.com/file/d/1FisDKVchOQx7fnjIU_7cV-mdMITnVzYx/view?usp=sharing`
- Thông điệp chuyển dữ liệu hóa đơn không mã: `https://drive.google.com/file/d/1H8o51n6b_S0n9z-Ox0_LHQA1T2zxfBrQ/view?usp=sharing`
- Thông báo hóa đơn có sai sót: `https://drive.google.com/file/d/1TZtpgXLfn2qodUxs6OvWnzAYIea68dF0/view?usp=sharing`
- (file thứ 5, chưa xác định mục): `https://drive.google.com/file/d/1VBWnFOXK7MTG83UoJuM2DxXYd30LJyGF/view?usp=sharing`

> Thứ tự map link ↔ mục là suy đoán theo thứ tự xuất hiện trong file; nên mở kiểm chứng.

**Bảng quy trình chuyển trạng thái Hóa đơn:**
`https://docs.google.com/spreadsheets/d/1tHRlmYiverUn3CccxaBRuYJ0JGE_Ao85zca4HMy2yDs/edit#gid=0`

**Tham chiếu kỹ thuật .NET:**

- GZip: `https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.gzipstream?view=netframework-4.8`
- AES/Rijndael: `https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.rijndael?view=netframework-4.8`
- Base64: `https://docs.microsoft.com/en-us/dotnet/api/system.convert.tobase64string?view=netframework-4.8`

---

## 13. Những gì tài liệu THIẾU (cần hỏi Bkav)

1. **Cấu trúc object `Result`** — không có field nào được mô tả (mục "CommandData & Result" bỏ trống phần Result).
2. **Bảng mã lỗi / ErrorCode** phía Bkav (khác với mã phản hồi của CQT).
3. **CmdType cho Mode 3** — mode được mô tả quy trình nhưng không có mã lệnh.
4. **CommandObject mẫu cho CmdType 801/802** — bảng ghi "XML" nhưng không có ví dụ.
5. **Đặc tả field hóa đơn** (type/length/required) — chỉ có 1 ví dụ XML.
6. **CmdType `302` trùng lặp** cho MLTDiep 203 và 300 — nghi typo.
7. **Cơ chế callback/webhook** — tài liệu nói "Bkav gửi thông báo tới TCGP" nhưng không mô tả Bkav push kiểu gì, hay TCGP phải poll bằng CmdType 801/802.
8. **Vai trò của `User` / `User demo`** trong API.
