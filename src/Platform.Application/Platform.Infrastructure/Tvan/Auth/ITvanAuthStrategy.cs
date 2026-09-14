using Platform.Infrastructure.Tvan.Model;

namespace Platform.Infrastructure.Tvan.Auth;

/// <summary>
/// Cơ chế xác thực với một T-VAN. Đây là điểm mở rộng: cần kiểu auth hoàn toàn mới
/// (mTLS, HMAC signature, OAuth2 client_credentials) thì viết thêm một class ở đây,
/// đăng ký keyed DI, và khai báo AuthSchemeKey trong DB. Core không đổi.
/// </summary>
public interface ITvanAuthStrategy
{
    ValueTask ApplyAsync(HttpRequestMessage request, TvanAuthDescriptor auth, CancellationToken ct);

    /// <summary>Gọi khi NCC trả 401/403 — xoá token cache để lần sau login lại.</summary>
    ValueTask InvalidateAsync(TvanAuthDescriptor auth, CancellationToken ct);
}

/// <summary>BKAV: xác thực bằng PartnerGUID nằm trong body và PartnerToken dùng để mã hoá, không có header auth.</summary>
public sealed class NoneAuthStrategy : ITvanAuthStrategy
{
    public ValueTask ApplyAsync(HttpRequestMessage request, TvanAuthDescriptor auth, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public ValueTask InvalidateAsync(TvanAuthDescriptor auth, CancellationToken ct) =>
        ValueTask.CompletedTask;
}
