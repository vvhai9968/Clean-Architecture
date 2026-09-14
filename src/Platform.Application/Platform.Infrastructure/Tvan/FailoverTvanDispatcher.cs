using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Platform.Domain.Platform.Tvan.Abstractions;

namespace Platform.Infrastructure.Tvan;

/// <summary>
/// Thử lần lượt từng nhà truyền nhận theo thứ tự ưu tiên cho tới khi có một nhà trả lời.
///
/// <para>
/// Ranh giới quan trọng nhất của lớp này là phân biệt <em>không hoạt động</em> với
/// <em>đã trả lời nhưng từ chối</em>:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Chuyển nhà</b> khi nhà hiện tại không hoạt động — lỗi mạng, timeout, 5xx, 429,
/// không đăng nhập được, chưa khai báo nghiệp vụ hoặc thiếu credential.
/// </item>
/// <item>
/// <b>Dừng lại</b> khi nhà truyền nhận đã trả lời bằng mã lỗi nghiệp vụ. Đó là một câu trả lời,
/// không phải một sự cố: gửi lại cùng hoá đơn đó qua nhà khác sẽ nhận đúng lời từ chối ấy,
/// nhưng kèm rủi ro tạo hoá đơn trùng trên hệ thống Cơ quan Thuế.
/// </item>
/// </list>
/// </summary>
internal sealed class FailoverTvanDispatcher(
    ITvanGateway gateway,
    ILogger<FailoverTvanDispatcher> logger) : ITvanDispatcher
{
    public async Task<TvanFailoverResult> DispatchAsync(TvanFailoverRequest request, CancellationToken ct)
    {
        if (request.ProviderCodes.Count == 0)
            throw new TvanConfigurationException("Kế hoạch định tuyến rỗng: account chưa được cấp nhà truyền nhận nào.");

        var attempts = new List<TvanProviderAttempt>(request.ProviderCodes.Count);

        for (var index = 0; index < request.ProviderCodes.Count; index++)
        {
            ct.ThrowIfCancellationRequested();

            var providerCode = request.ProviderCodes[index];
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await gateway.DispatchAsync(new TvanDispatchRequest
                {
                    ProviderCode = providerCode,
                    TaxCode = request.TaxCode,
                    OperationCode = request.OperationCode,
                    Xml = request.Xml,
                    CorrelationKey = request.CorrelationKey,
                    Fields = request.Fields,
                    TransactionId = request.TransactionId,
                }, ct);

                stopwatch.Stop();

                if (result.Accepted || !result.IsTransient)
                {
                    // Nhà này đã trả lời thật sự — chấp nhận hoặc từ chối vì nghiệp vụ. Dừng tại đây.
                    attempts.Add(new TvanProviderAttempt(
                        providerCode, Skipped: false, result.ResultCode,
                        result.Accepted ? "Đã tiếp nhận" : result.Message ?? "Bị từ chối",
                        stopwatch.ElapsedMilliseconds));

                    if (!result.Accepted)
                    {
                        logger.LogWarning(
                            "T-VAN {Provider} từ chối {Key} ({Code}) — lỗi nghiệp vụ, KHÔNG chuyển nhà.",
                            providerCode, request.CorrelationKey, result.ResultCode);
                    }

                    return new TvanFailoverResult(result, providerCode, attempts);
                }

                attempts.Add(new TvanProviderAttempt(
                    providerCode, Skipped: true, result.ResultCode,
                    $"HTTP {result.HttpStatus}: {result.Message}", stopwatch.ElapsedMilliseconds));

                LogSwitch(providerCode, request, index, $"HTTP {result.HttpStatus}");
            }
            catch (Exception ex) when (IsUnavailable(ex))
            {
                stopwatch.Stop();
                attempts.Add(new TvanProviderAttempt(
                    providerCode, Skipped: true, null, Describe(ex), stopwatch.ElapsedMilliseconds));

                LogSwitch(providerCode, request, index, ex.GetType().Name);
            }
        }

        logger.LogError(
            "Không còn nhà truyền nhận nào khả dụng cho {Key}. Đã thử: {Providers}",
            request.CorrelationKey, string.Join(", ", request.ProviderCodes));

        return new TvanFailoverResult(null, null, attempts);
    }

    /// <summary>
    /// Những lỗi nghĩa là "nhà này đang không dùng được", chứ không phải "hoá đơn này sai".
    /// Lỗi cấu hình cũng nằm ở đây: một nhà chưa khai báo nghiệp vụ thì với nghiệp vụ đó
    /// nó không khác gì đang chết — các nhà còn lại vẫn nên được thử.
    /// </summary>
    private static bool IsUnavailable(Exception ex) => ex is
        TvanConfigurationException or
        TvanAuthenticationException or
        TvanProtocolException or
        HttpRequestException or
        TaskCanceledException or
        TimeoutException;

    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException or TimeoutException => "Hết thời gian chờ",
        TvanAuthenticationException => $"Không đăng nhập được: {ex.Message}",
        TvanConfigurationException => $"Cấu hình không dùng được: {ex.Message}",
        TvanProtocolException => $"Phản hồi không đọc được: {ex.Message}",
        _ => ex.Message,
    };

    private void LogSwitch(string providerCode, TvanFailoverRequest request, int index, string reason)
    {
        var isLast = index == request.ProviderCodes.Count - 1;

        if (isLast)
        {
            logger.LogWarning("T-VAN {Provider} không khả dụng ({Reason}) và là nhà cuối cùng trong danh sách.",
                providerCode, reason);
            return;
        }

        logger.LogWarning("T-VAN {Provider} không khả dụng ({Reason}), chuyển sang {Next} cho {Key}.",
            providerCode, reason, request.ProviderCodes[index + 1], request.CorrelationKey);
    }
}
