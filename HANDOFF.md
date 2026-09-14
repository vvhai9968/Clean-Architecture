# HANDOFF — Integration Gateway Hoá đơn điện tử (T-VAN)

> Ghi lại toàn bộ ngữ cảnh phiên làm việc **2026-09-14** để phiên sau tiếp tục được ngay.
> Nhánh: `TN` · Solution: `Platform.sln` · .NET 8

---

## 1. Tóm tắt trong 30 giây

Xây một Integration Gateway truyền/nhận hoá đơn điện tử lên Cơ quan Thuế qua **3 nhà truyền nhận (T-VAN)** có đặc thù kỹ thuật không có điểm chung nào:

| NCC | Giao thức | Xác thực |
|---|---|---|
| **BKAV** | SOAP ASMX, XML, GZIP + AES-256-CBC + Base64 | Không có API login (PartnerGUID/PartnerToken tĩnh) |
| **HILO** | HTTPS REST/JSON | JWT Bearer, có API login |
| **MINVOICE** | HTTPS REST/JSON | Header `Bear {token};{ma_dvcs}` (đúng là `Bear`, không phải `Bearer`) |

Nguyên tắc: **metadata-driven** — thêm NCC mới chỉ cần INSERT vào DB, không sửa code lõi. Cả ba đi qua **đúng một code path**; toàn bộ khác biệt nằm trong các cột `jsonb`.

Tài liệu kiến trúc (trang web): https://claude.ai/code/artifact/8187aa57-8a20-4b9f-9a17-2560c4c43453

Tài liệu API gốc của 3 NCC: `docTN/BKAV-API-TomTat.md`, `docTN/Hilo-API-TomTat.md`, `docTN/Minvoice-API-TomTat.md`

---

## 2. Trạng thái hiện tại

- `dotnet build Platform.sln` → **thành công, 0 warning**
- `dotnet test Platform.sln` → **54/54 xanh**
- 4 migration: `InitialAuth` → `AddTvanIntegration` → `RemoveSsoAndRefreshToken` → `UsernameLoginAndMultiTvanFailover`

### ⚠️ Toàn bộ công việc CHƯA COMMIT

`git status` đang có ~45 file thay đổi/mới trên nhánh `TN`. Commit đầu phiên sau là việc đầu tiên nên làm.

### ⚠️ Chưa chạy thật lần nào

Chưa có Postgres nên chưa chạy `dotnet run`. Migration + seed + DI graph mới chỉ đúng trên lý thuyết và qua unit test. **Việc đầu tiên đáng làm là dựng DB và chạy thử.**

---

## 3. Bốn nguyên tắc cốt lõi (đọc trước khi sửa bất cứ gì)

### 3.1. Hai trục mở rộng

| Trục | Khi nào | Chi phí |
|---|---|---|
| **A — zero code** | NCC mới tái dùng primitive sẵn có (REST/JSON + login token, hoặc SOAP + AES/GZIP) | Chỉ INSERT DB |
| **B — one class** | NCC cần primitive thật sự mới (ký RSA, mTLS, HMAC, gRPC) | 1 class + 1 dòng `AddKeyedSingleton`. Lõi không đổi |

Không hứa "không bao giờ viết code nữa" — hứa "không bao giờ sửa code lõi".

### 3.2. Năm chặng trực giao

```
TvanMessage → [1] Transform → [2] BodyTemplate → [3] Auth → [4] Transport → [5] ResponseMap
(XML QĐ 1450)  gzip/aes/base64  SOAP hoặc JSON    DelegatingHandler  HttpClient   envelope → inverse → extract

BKAV     : [1] bkav-command-data→gzip→aes-cbc→base64  [2] SOAP  [3] none
HILO     : [1] (rỗng)                                  [2] JSON  [3] login-token → Bearer {t}
MINVOICE : [1] base64                                  [2] JSON  [3] login-token → Bear {t};{dvcs}
```

**HILO và Minvoice dùng chung một class `LoginTokenAuthStrategy`** — chỉ khác `headerFormat` trong `AuthConfigJson`. Đây là bằng chứng rõ nhất rằng thiết kế đứng vững.

### 3.3. Ba registry = toàn bộ bề mặt mở rộng

Khai báo trong `TvanServiceCollectionExtensions.cs` (keyed DI của .NET 8):

| Registry | Interface | Đã có |
|---|---|---|
| Transform | `IPayloadTransform` | `gzip` · `aes-cbc` · `base64` · `bkav-command-data` |
| Auth | `ITvanAuthStrategy` | `none` · `login-token` |
| Extractor | `IResponseValueExtractor` | `json` · `xml` |

### 3.4. Port nằm ở Domain, không phải Application

Repo này có sẵn chiều `Platform.Application → Platform.Infrastructure` (Application dùng `DbContext` trực tiếp). Nếu đặt port ở Application thì Infrastructure không implement được mà không tạo vòng lặp tham chiếu. **Port đặt ở `Platform.Domain/.../Abstractions`** — đúng tinh thần hexagonal: lõi định nghĩa cổng, hạ tầng cắm vào.

---

## 4. Bản đồ file

```
Platform.Domain/Platform/
  Auth/
    Abstractions/IJwtTokenService.cs      ← hợp đồng: token chứa gì
    TvanTokenBinding.cs                   ← value object kế hoạch định tuyến
    User.cs · UserTvanProvider.cs · Role.cs · UserRole.cs · UserIdentity.cs
  Tvan/
    Abstractions/  ITvanGateway · ITvanDispatcher · ITvanRouter
                   ITvanTransactionStore · ISecretProtector · ICorrelationKeyGenerator
    TvanProvider · TvanEndpoint · TvanHeaderTemplate · TvanCredential
    TvanTenantBinding · TvanTransaction · TvanTransactionAttempt

Platform.Application/MediatR/
  Auth/Queries/SignInQuery.cs             ← use case đầy đủ, không qua service trung gian
  Auth/Commands/CreateUserCommand.cs
  Tvan/Commands/  DispatchTvanMessage · ReceiveTvanCallback
                  UpsertTvanProvider · UpsertTvanCredential · AssignUserTvan
  Tvan/Queries/GetTvanTransactionQuery.cs

Platform.Infrastructure/
  Identity/JwtTokenService.cs             ← ký HS256, sắp claim
  Tvan/
    FailoverTvanDispatcher.cs             ← CHÍNH SÁCH chuyển nhà truyền nhận
    TvanGateway.cs                        ← gọi ĐÚNG MỘT nhà
    Metadata/   TvanMetadataStore (cache) · TvanChannelFactory
    Model/      TvanChannel · TvanResponseMap · TvanSecretBag
    Templating/ TokenTemplateRenderer     ← engine tự viết, zero dependency
    Transforms/ Gzip · AesCbc · Base64 · BkavCommandData · Pipeline
    Auth/       NoneAuthStrategy · LoginTokenAuthStrategy
    Http/       Retry · Auth · Audit DelegatingHandler
    Response/   JsonValueExtractor · XmlValueExtractor · ResponseInterpreter
    Outbox/     EfTvanTransactionStore · TvanOutboxDispatcher
    Routing/    ClaimsTvanRouter · MtDiepGenerator
    Security/   DataProtectionSecretProtector
    Callbacks/  TvanCallbackProcessor

Platform.APIs/
  Endpoints/Auth/AuthEndPoint.cs · Endpoints/Tvan/TvanEndPoint.cs
  Application/MigrateData/DataSeed.cs · TvanSeed.cs
  AppSettings/tvan-providers.json         ← TOÀN BỘ đặc tả 3 NCC nằm ở đây

tests/Platform.Tvan.Tests/               ← 54 test
```

### Thứ tự đọc code lần đầu

1. `docTN/*.md` — hiểu 3 NCC khác nhau chỗ nào
2. `AppSettings/tvan-providers.json` — thấy khác biệt đó ở dạng dữ liệu
3. `Model/TvanChannel.cs` — dữ liệu đó biến thành object gì
4. `TvanGateway.cs` — object đó được dùng ra sao
5. `FailoverTvanDispatcher.cs` — chính sách chuyển nhà
6. `DispatchTvanMessageCommand.cs` — use case nhìn từ trên xuống

---

## 5. API hiện có

```
POST   api/auth/sign-in                      AllowAnonymous   (đăng nhập bằng username)
POST   api/auth/users                        RequireRole ADMIN

POST   api/tvan/messages                     (operationCode tự chọn)
POST   api/tvan/registrations                → send-registration
POST   api/tvan/invoices/coded               → send-invoice-coded
POST   api/tvan/invoices/uncoded             → send-invoice-uncoded
POST   api/tvan/invoices/pos                 → send-invoice-pos
POST   api/tvan/error-notices                → send-error-notice
POST   api/tvan/summaries                    → send-summary
GET    api/tvan/messages/{correlationKey}    ?refresh=true để hỏi lại T-VAN

POST   api/tvan/callbacks/{providerCode}     AllowAnonymous (webhook HILO)

POST   api/tvan/admin/providers              RequireRole ADMIN  ← thêm NCC mới ở đây
POST   api/tvan/admin/credentials            RequireRole ADMIN
POST   api/tvan/admin/users/assign           RequireRole ADMIN
```

---

## 6. Quyết định cố ý — ĐỪNG đảo ngược nếu chưa đọc lý do

| # | Quyết định | Vì sao |
|---|---|---|
| 1 | **Lỗi nghiệp vụ KHÔNG retry, KHÔNG chuyển nhà** | NCC trả mã lỗi nghiệp vụ là một *câu trả lời*, không phải sự cố. Gửi lại qua nhà khác chỉ nhận đúng lời từ chối ấy, kèm rủi ro **hoá đơn trùng trên hệ thống CQT**. Chỉ 5xx/408/429/lỗi mạng mới retry và chuyển nhà. |
| 2 | **Lỗi cấu hình của 1 NCC = coi như NCC đó chết** | Một nhà chưa khai báo nghiệp vụ thì với nghiệp vụ đó nó không khác gì đang sập — các nhà còn lại vẫn nên được thử. |
| 3 | **Admin ép `providerCode` thì tắt luôn failover** | Người vận hành đang muốn kiểm tra chính nhà đó; im lặng chuyển nhà sẽ che mất thứ họ đang xem. |
| 4 | **Idempotency key = `CorrelationKey` đơn lẻ**, không kèm mã NCC | Một thông điệp nghiệp vụ có thể đã chuyển qua nhiều nhà khi failover nhưng vẫn là một giao dịch. |
| 5 | **Ghi outbox trước, gửi sau**; gửi ngay để giảm độ trễ, hỏng thì tiến trình nền gửi lại | Sau khi commit, giao dịch không thể mất kể cả khi T-VAN hoặc service chết. |
| 6 | **Client login KHÔNG gắn `TvanAuthDelegatingHandler`** | Nếu gắn sẽ tự gọi lại chính mình vô hạn. |
| 7 | **`TvanChannel` truyền qua `HttpRequestMessage.Options`**, không dùng `AsyncLocal` | Handler vẫn là hàm thuần: test được, an toàn khi chạy song song. |
| 8 | **Tạo tài khoản KHÔNG phát token** | Người gọi là quản trị viên, không phải chủ tài khoản. |
| 9 | **Tài khoản mới mặc định role `USER`** | Code cũ gán `ADMIN` cho mọi tài khoản; giữ nguyên thì admin đầu tiên sẽ đẻ ra toàn admin. |
| 10 | **Bootstrap admin chỉ chạy khi bảng `Users` rỗng** | Chỉ admin mới tạo được tài khoản ⇒ DB trống thì không ai đăng nhập được. Cấu hình ở `Bootstrap` trong appsettings. |
| 11 | **`AuthService` bị hoà tan vào handler** | Với CQRS thì handler *đã là* use case; thêm một lớp service ở giữa là mắt xích không quyết định gì. |
| 12 | **Template engine tự viết, cố ý KHÔNG hỗ trợ vòng lặp/điều kiện** | Template lấy từ DB nên phải là dữ liệu thuần, không được thành nơi nhét logic. |
| 13 | **`.OrderBy().Join()` bị thay bằng query expression** trong `TvanMetadataStore` | EF không bảo đảm giữ thứ tự sau khi dịch sang SQL — mà thứ tự ở đây là thứ tự chuyển nhà. |

---

## 7. Đã làm gì trong phiên (theo thứ tự)

1. **Thiết kế + scaffold toàn bộ module T-VAN** vào repo: 8 entity, 6 command/query, 27 file Infrastructure, migration 7 bảng, seed JSON 3 NCC.
2. **Outbox + `BackgroundService`**: `SELECT … FOR UPDATE SKIP LOCKED` trong transaction tường minh, backoff mũ chặn trần 30 phút, lease quá hạn thì worker khác nhặt lại.
3. **Xuất bản tài liệu kiến trúc** thành trang web (link ở mục 1).
4. **Bỏ SSO + refresh token**, kèm `IRestHttpClient`/`AzureAd`/`Jwt.MasterKey` (chỉ tồn tại để phục vụ SSO). Chỉ role `ADMIN` được tạo tài khoản.
5. **Đăng nhập bằng username** thay email; **một user nhiều nhà truyền nhận** (bảng `UserTvanProviders`, phát nhiều claim `tvan_provider` giữ thứ tự); **failover** qua `FailoverTvanDispatcher`.
6. **Sắp xếp lại tầng**: `JwtTokenService` → Infrastructure, `IJwtTokenService` + `TvanTokenBinding` → Domain, `AuthQueryExtensions` → Infrastructure, xoá `Services/` và `Common/` của Application.

### Bug thật đã tìm và sửa

- `BkavCommandDataTransform.BackwardAsync` dùng `Root.Value` nên nối cả `CmdType` vào payload khi giải mã ngược. Test round-trip bắt được. Đã sửa thành lấy đúng phần tử `CommandObject`.
- Migration `UsernameLoginAndMultiTvanFailover` thêm cột `UserName` NOT NULL rồi dựng unique index → vỡ ngay nếu DB có dữ liệu. Đã thêm SQL backfill (lấy phần trước `@` của email, nối hậu tố cho tên trùng).

---

## 8. Nợ kỹ thuật đã phát hiện, CHƯA sửa (theo thứ tự ưu tiên)

### 8.1. 🔴 Thiếu kiểm tra MST — lỗ hổng bảo mật

Handler chưa kiểm **MST bên trong XML có khớp claim `tvan_taxcode` của token không**. Một account hoàn toàn có thể nộp XML mang MST của đơn vị khác, và hệ thống sẽ gửi lên CQT bằng credential của mình.

**Chặn bởi câu hỏi ở mục 9.1.**

### 8.2. 🔴 Ba tầng mã thông điệp bị gộp làm hai

Soát lại tài liệu thì **mã thông điệp thuế do bên thứ 3 sinh**, không phải mình:

| Tầng | Ai sinh | Ở đâu |
|---|---|---|
| Mã tham chiếu TCGP | **Mình** | Gửi: `MTDiep` (HILO), `transId` (Minvoice). Nhận lại: `MTDTChieu` |
| Mã thông điệp T-VAN | **Bên thứ 3** | `Data.MTDiep` (HILO), `data.maThongdiep` (Minvoice) |
| Mã thông điệp CQT | **Tổng cục Thuế** | `Response[].MTDiep` tiền tố `TCT...` |

Bằng chứng: HILO trả `MTDiep` khác hẳn cái mình gửi, và echo cái mình gửi ra `MTDTChieu`. Minvoice trả `maThongdiep` tiền tố `V0106026495` = MST của chính Minvoice. BKAV thì Mode 2 (CmdType 500–505, đúng cấu hình hiện tại) do BKAV đóng gói ⇒ BKAV sinh.

Cần sửa:
- Đổi tên cho đúng: `MtDiepGenerator` → sinh *mã tham chiếu TCGP*; `ProviderReference` → `TvanMessageCode`; thêm `TaxAuthorityMessageCode`.
- **Generator đang dùng MST khách hàng, nhiều khả năng phải là MST đối tác** (HILO: `MNGui = V + MST đối tác`, và `MTDiep` mẫu có đúng tiền tố ấy). Dữ liệu demo trùng MST nên lỗi bị che. → lấy từ cấu hình `Tvan:PartnerTaxCode`.
- `ProviderReference` đơn trị nhưng failover có thể sinh nhiều mã → cần ghi theo từng nhà đã thử.

**Chặn một phần bởi câu hỏi ở mục 10.**

### 8.3. 🟠 Timeout → failover có thể tạo hoá đơn trùng

`IsUnavailable` coi `TaskCanceledException` là "nhà này chết", nhưng timeout **không chứng minh được** họ chưa nhận. Nếu BKAV đã nhận rồi mới timeout, mình chuyển sang HILO ⇒ CQT có hai thông điệp.

Hướng xử: trước khi chuyển nhà vì timeout, gọi API tra cứu của nhà đó (BKAV `CmdType 801`, HILO `/get`). **Minvoice không có API tra cứu** → chặn bởi câu hỏi ở mục 9.2.

### 8.4. 🟠 Không có `IPipelineBehavior` nào

Chưa có validation behavior, chưa có logging/correlation behavior. Kiểm tra đầu vào hiện đúng một dòng `IsNullOrWhiteSpace(Xml)`. Còn thiếu: XML well-formed, `SLuong` khớp số hoá đơn thực, `MTDiep` đúng độ dài, kích thước gói ≤ 2 MB (giới hạn Minvoice).

Đây là chỗ tầng Application **nên** dày lên (hiện chỉ 809 dòng so với 2539 của Infrastructure).

### 8.5. 🟡 Ba thứ nằm sai tầng (~170 dòng)

| Thứ | Hiện ở | Đúng ra |
|---|---|---|
| `FailoverTvanDispatcher` (136 dòng) | Infrastructure | **Application** — `using` chỉ có `System.Diagnostics` + `ILogger` + `Domain.Abstractions`, không một dependency hạ tầng nào. Nội dung là chính sách thuần. |
| `MtDiepGenerator` (~20) | Infrastructure/Routing | **Domain** — quy tắc định dạng là đặc tả nghiệp vụ |
| `TvanExceptions` (10) | Infrastructure | **Domain** — taxonomy lỗi là một phần hợp đồng, cả hai tầng đều bắt |

Hệ quả thật: muốn test chính sách failover, project test buộc phải tham chiếu Infrastructure ⇒ kéo theo EF Core + Npgsql + DataProtection.

### 8.6. 🟡 Chưa chạy thật với Postgres

---

## 9. Câu hỏi đang chờ anh chốt

### 9.1. MST trong XML lệch với claim `tvan_taxcode` thì xử lý thế nào?

- (a) Từ chối thẳng **403**, hay
- (b) Cho phép nếu account có role `ADMIN` (trường hợp đại lý thuế nộp hộ nhiều MST)?

### 9.2. Minvoice timeout thì làm gì?

Minvoice **không có API tra cứu kết quả**, nên không kiểm được họ đã nhận hay chưa.

- (a) Không failover khi timeout với Minvoice (an toàn, nhưng hoá đơn kẹt lại), hay
- (b) Vẫn failover, chấp nhận rủi ro trùng?

### 9.3. Có muốn giữ route cũ `api/auth/sign-up` không?

Hiện đã đổi thành `POST api/auth/users` vì không còn là tự đăng ký. Nếu client đã tích hợp route cũ thì nói để đổi lại.

---

## 10. Phải hỏi 3 nhà cung cấp trước khi lên production

### BKAV
1. **Cấu trúc object `Result`** — tài liệu bỏ trống hoàn toàn, không field nào, không bảng mã lỗi. Hiện hệ thống chỉ dựa vào HTTP status. Chốt xong chỉ cần `UPDATE responseMap.result`, không sửa code.
2. `CmdType 302` xuất hiện hai lần cho `MLTDiep` 203 và 300 — nghi typo, có thể là `303`.
3. `CmdType` cho Mode 3 — tài liệu mô tả quy trình nhưng không có mã lệnh.

### HILO
4. **Tiền tố `MTDiep` mình gửi lên là `V` + MST đối tác hay `V` + MST người bán?** (dữ liệu demo trùng nên không phân biệt được)
5. **Độ dài `MTDiep` thật là bao nhiêu?** Bảng ghi 15, ví dụ dài 43 — tài liệu tự mâu thuẫn.
6. **Payload webhook `CallBackUrl`** — hoàn toàn không mô tả, không có cơ chế ký. `callbackMap` hiện tại là giả định.
7. **Thời hạn token** — token mẫu có `exp == iat`.
8. **Base URL Production** — chỉ có UAT.

### Minvoice
9. **`Bear` hay `Bearer`?** Tài liệu ghi `Bear` nhất quán ở cả 8 mục nghiệp vụ.
10. **API lấy kết quả / webhook** — hoàn toàn không có. Hiện `Accepted` chỉ có nghĩa *TVAN đã nhận*, **không** phải CQT đã chấp nhận.
11. **Quy tắc sinh `transId`** và ràng buộc duy nhất.
12. **Base URL Production** — chỉ có UAT.

---

## 11. Lệnh hay dùng

```bash
# Build + test
dotnet build Platform.sln
dotnet test  Platform.sln

# Migration
ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add <Ten> \
  --project    src/Platform.Application/Platform.Infrastructure/Platform.Infrastructure.csproj \
  --startup-project src/Platform.Application/Platform.APIs/Platform.APIs.csproj \
  --output-dir Persistence/PlatformContext/Migrations

# Chạy (cần Postgres ở localhost:5432, db platform_auth)
dotnet run --project src/Platform.Application/Platform.APIs/Platform.APIs.csproj
# Swagger: https://localhost:<port>/swagger
```

Đăng nhập lần đầu: `admin` / `CHANGE_ME_on_first_login` (cấu hình ở `AppSettings/appsettings.Development.json`, mục `Bootstrap`).

File `AppSettings/tvan-credentials.json` chứa secret thật — **đã ignore trong `.gitignore`, đừng commit**.

---

## 12. Việc nên làm đầu phiên sau

1. `git add -A && git commit` — cứu toàn bộ công việc đang treo.
2. Dựng Postgres, `dotnet run`, kiểm migration + seed 3 NCC + bootstrap admin chạy thật.
3. Trả lời hai câu hỏi ở mục 9.1 và 9.2.
4. Làm mục 8.5 (chuyển 3 file về đúng tầng — không đổi hành vi, 54 test phải xanh nguyên).
5. Làm mục 8.4 + 8.1 (`ValidationBehavior` + kiểm MST khớp token).
6. Làm mục 8.2 (tách ba tầng mã thông điệp).
7. Gửi mục 10 cho 3 nhà cung cấp.
