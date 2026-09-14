using Microsoft.Extensions.Logging.Abstractions;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Tvan;

namespace Platform.Tvan.Tests;

/// <summary>
/// Chính sách chuyển nhà truyền nhận. Ranh giới quan trọng nhất được kiểm ở đây:
/// nhà <em>không hoạt động</em> thì chuyển tiếp, nhà <em>đã trả lời nhưng từ chối</em> thì dừng.
/// </summary>
public class FailoverTvanDispatcherTests
{
    [Fact]
    public async Task Uses_the_primary_provider_when_it_works()
    {
        var gateway = new ScriptedGateway { ["BKAV"] = _ => Accepted("BKAV", "REF-1") };
        var result = await Dispatch(gateway, "BKAV", "HILO", "MINVOICE");

        Assert.True(result.Accepted);
        Assert.Equal("BKAV", result.ProviderCode);
        Assert.Equal(["BKAV"], gateway.Called);
        Assert.Single(result.Attempts);
    }

    [Fact]
    public async Task Switches_to_the_next_provider_on_a_network_failure()
    {
        var gateway = new ScriptedGateway
        {
            ["BKAV"] = _ => throw new HttpRequestException("connection refused"),
            ["HILO"] = _ => Accepted("HILO", "REF-2"),
        };

        var result = await Dispatch(gateway, "BKAV", "HILO", "MINVOICE");

        Assert.True(result.Accepted);
        Assert.Equal("HILO", result.ProviderCode);
        Assert.Equal(["BKAV", "HILO"], gateway.Called);
        Assert.True(result.Attempts[0].Skipped);
        Assert.False(result.Attempts[1].Skipped);
    }

    [Fact]
    public async Task Switches_on_5xx_and_on_timeout()
    {
        var gateway = new ScriptedGateway
        {
            ["BKAV"] = _ => Transient("BKAV", 503),
            ["HILO"] = _ => throw new TaskCanceledException("timeout"),
            ["MINVOICE"] = _ => Accepted("MINVOICE", "REF-3"),
        };

        var result = await Dispatch(gateway, "BKAV", "HILO", "MINVOICE");

        Assert.Equal("MINVOICE", result.ProviderCode);
        Assert.Equal(["BKAV", "HILO", "MINVOICE"], gateway.Called);
        Assert.Contains("Hết thời gian chờ", result.Attempts[1].Reason);
    }

    [Fact]
    public async Task Skips_a_provider_that_cannot_log_in()
    {
        var gateway = new ScriptedGateway
        {
            ["HILO"] = _ => throw new TvanAuthenticationException("sai mật khẩu đối tác"),
            ["MINVOICE"] = _ => Accepted("MINVOICE", "REF-4"),
        };

        var result = await Dispatch(gateway, "HILO", "MINVOICE");

        Assert.True(result.Accepted);
        Assert.Contains("Không đăng nhập được", result.Attempts[0].Reason);
    }

    [Fact]
    public async Task Skips_a_provider_that_has_not_declared_this_operation()
    {
        // Một nhà chưa khai báo nghiệp vụ thì với nghiệp vụ đó nó không khác gì đang chết.
        var gateway = new ScriptedGateway
        {
            ["MINVOICE"] = _ => throw new TvanConfigurationException("chưa khai báo query-result"),
            ["HILO"] = _ => Accepted("HILO", "REF-5"),
        };

        var result = await Dispatch(gateway, "MINVOICE", "HILO");

        Assert.True(result.Accepted);
        Assert.Equal("HILO", result.ProviderCode);
    }

    [Fact]
    public async Task Stops_at_a_business_rejection_and_never_tries_the_next_provider()
    {
        var gateway = new ScriptedGateway
        {
            ["BKAV"] = _ => Rejected("BKAV", "1034", "Thông tin người đại diện pháp luật không khớp"),
            ["HILO"] = _ => Accepted("HILO", "REF-6"),
        };

        var result = await Dispatch(gateway, "BKAV", "HILO");

        // Gửi lại hoá đơn đó qua HILO chỉ nhận đúng lời từ chối ấy, kèm rủi ro
        // tạo hoá đơn trùng trên hệ thống Cơ quan Thuế.
        Assert.False(result.Accepted);
        Assert.False(result.AllProvidersUnavailable);
        Assert.Equal("BKAV", result.ProviderCode);
        Assert.Equal("1034", result.Final!.ResultCode);
        Assert.Equal(["BKAV"], gateway.Called);
    }

    [Fact]
    public async Task Reports_a_transmission_failure_only_after_every_provider_is_exhausted()
    {
        var gateway = new ScriptedGateway
        {
            ["BKAV"] = _ => throw new HttpRequestException("down"),
            ["HILO"] = _ => Transient("HILO", 502),
            ["MINVOICE"] = _ => throw new TaskCanceledException("timeout"),
        };

        var result = await Dispatch(gateway, "BKAV", "HILO", "MINVOICE");

        Assert.True(result.AllProvidersUnavailable);
        Assert.Null(result.Final);
        Assert.Equal(3, result.Attempts.Count);
        Assert.All(result.Attempts, a => Assert.True(a.Skipped));

        var description = result.Describe();
        Assert.Contains("BKAV", description);
        Assert.Contains("HILO", description);
        Assert.Contains("MINVOICE", description);
    }

    [Fact]
    public async Task Every_provider_receives_the_same_correlation_key()
    {
        var seen = new List<string>();
        var gateway = new ScriptedGateway
        {
            ["BKAV"] = r => { seen.Add(r.CorrelationKey); throw new HttpRequestException("down"); },
            ["HILO"] = r => { seen.Add(r.CorrelationKey); return Accepted("HILO", "REF-7"); },
        };

        await Dispatch(gateway, "BKAV", "HILO");

        // Một thông điệp nghiệp vụ giữ nguyên MTDiep dù đi qua nhà nào — đối soát mới lần ra được.
        Assert.Equal(["V0101360697ABC", "V0101360697ABC"], seen);
    }

    [Fact]
    public async Task An_empty_plan_is_a_configuration_error_not_a_transmission_error()
    {
        var dispatcher = new FailoverTvanDispatcher(
            new ScriptedGateway(), NullLogger<FailoverTvanDispatcher>.Instance);

        await Assert.ThrowsAsync<TvanConfigurationException>(
            () => dispatcher.DispatchAsync(RequestFor([]), default));
    }

    // --- helpers -------------------------------------------------------------

    private static Task<TvanFailoverResult> Dispatch(ITvanGateway gateway, params string[] providers) =>
        new FailoverTvanDispatcher(gateway, NullLogger<FailoverTvanDispatcher>.Instance)
            .DispatchAsync(RequestFor(providers), default);

    private static TvanFailoverRequest RequestFor(IReadOnlyList<string> providers) => new()
    {
        ProviderCodes = providers,
        TaxCode = "0101360697",
        OperationCode = "send-invoice-coded",
        Xml = TestData.InvoiceXml,
        CorrelationKey = "V0101360697ABC",
    };

    private static TvanDispatchResult Accepted(string provider, string reference) =>
        new(true, provider, reference, "00", "Đã tiếp nhận", 200, null);

    private static TvanDispatchResult Rejected(string provider, string code, string message) =>
        new(false, provider, null, code, message, 200, null);

    private static TvanDispatchResult Transient(string provider, int status) =>
        new(false, provider, null, null, "gateway lỗi", status, null);

    private sealed class ScriptedGateway : ITvanGateway
    {
        private readonly Dictionary<string, Func<TvanDispatchRequest, TvanDispatchResult>> _script = [];

        public List<string> Called { get; } = [];

        public Func<TvanDispatchRequest, TvanDispatchResult> this[string providerCode]
        {
            set => _script[providerCode] = value;
        }

        public Task<TvanDispatchResult> DispatchAsync(TvanDispatchRequest request, CancellationToken ct)
        {
            Called.Add(request.ProviderCode);

            if (!_script.TryGetValue(request.ProviderCode, out var behaviour))
                throw new HttpRequestException($"{request.ProviderCode} không được cấu hình trong test");

            return Task.FromResult(behaviour(request));
        }
    }
}
