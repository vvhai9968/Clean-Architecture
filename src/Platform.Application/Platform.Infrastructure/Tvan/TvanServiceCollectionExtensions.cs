using Microsoft.Extensions.DependencyInjection;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Tvan.Auth;
using Platform.Infrastructure.Tvan.Callbacks;
using Platform.Infrastructure.Tvan.Http;
using Platform.Infrastructure.Tvan.Metadata;
using Platform.Infrastructure.Tvan.Outbox;
using Platform.Infrastructure.Tvan.Response;
using Platform.Infrastructure.Tvan.Routing;
using Platform.Infrastructure.Tvan.Security;
using Platform.Infrastructure.Tvan.Templating;
using Platform.Infrastructure.Tvan.Transforms;
using Platform.Shared.Constants;

namespace Platform.Infrastructure.Tvan;

/// <summary>
/// Composition root của module T-VAN.
/// Ba registry keyed bên dưới là toàn bộ "bề mặt mở rộng" của hệ thống:
/// thêm nhà cung cấp dùng primitive sẵn có thì không đụng file này,
/// thêm primitive mới thì chỉ thêm đúng một dòng vào registry tương ứng.
/// </summary>
public static class TvanServiceCollectionExtensions
{
    public static IServiceCollection AddTvanIntegration(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddDataProtection();

        // --- Lõi ---
        services.AddScoped<ITvanMetadataStore, TvanMetadataStore>();
        services.AddScoped<ITvanChannelFactory, TvanChannelFactory>();
        services.AddScoped<ITvanGateway, TvanGateway>();

        // Gateway gọi đúng một nhà truyền nhận; dispatcher mới là nơi giữ chính sách chuyển nhà.
        services.AddScoped<ITvanDispatcher, FailoverTvanDispatcher>();

        services.AddScoped<ITvanRouter, ClaimsTvanRouter>();
        services.AddScoped<ITvanTransactionStore, EfTvanTransactionStore>();
        services.AddScoped<ITvanCallbackProcessor, TvanCallbackProcessor>();

        services.AddSingleton<ITvanMetadataCache, TvanMetadataCache>();
        services.AddSingleton<ITemplateRenderer, TokenTemplateRenderer>();
        services.AddSingleton<IPayloadTransformPipeline, PayloadTransformPipeline>();
        services.AddSingleton<IResponseInterpreter, ResponseInterpreter>();
        services.AddSingleton<IResponseValueExtractorRegistry, ResponseValueExtractorRegistry>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<ICorrelationKeyGenerator, MtDiepGenerator>();

        // --- Registry 1: biến đổi payload ---
        services.AddKeyedSingleton<IPayloadTransform, GzipTransform>(TvanTransforms.Gzip);
        services.AddKeyedSingleton<IPayloadTransform, AesCbcTransform>(TvanTransforms.AesCbc);
        services.AddKeyedSingleton<IPayloadTransform, Base64Transform>(TvanTransforms.Base64);
        services.AddKeyedSingleton<IPayloadTransform, BkavCommandDataTransform>(TvanTransforms.BkavCommandData);

        // --- Registry 2: cơ chế xác thực ---
        services.AddKeyedSingleton<ITvanAuthStrategy, NoneAuthStrategy>(TvanAuthSchemes.None);
        services.AddKeyedSingleton<ITvanAuthStrategy, LoginTokenAuthStrategy>(TvanAuthSchemes.LoginToken);

        // --- Registry 3: bóc tách phản hồi ---
        services.AddKeyedSingleton<IResponseValueExtractor, JsonValueExtractor>(TvanResponseFormats.Json);
        services.AddKeyedSingleton<IResponseValueExtractor, XmlValueExtractor>(TvanResponseFormats.Xml);

        // --- HTTP ---
        services.AddTransient<TvanAuthDelegatingHandler>();
        services.AddTransient<TvanAuditDelegatingHandler>();
        services.AddTransient<TvanRetryDelegatingHandler>();

        // Client login KHÔNG gắn auth handler, nếu không sẽ tự gọi lại chính mình vô hạn.
        services.AddHttpClient(TvanHttpClients.Login);

        // Thứ tự handler là ngoài vào trong: retry bọc ngoài để mỗi lần thử đều được gắn lại token,
        // audit nằm trong cùng để log đúng từng lần gọi thật sự đi ra mạng.
        services.AddHttpClient(TvanHttpClients.Dispatch)
            .AddHttpMessageHandler<TvanRetryDelegatingHandler>()
            .AddHttpMessageHandler<TvanAuthDelegatingHandler>()
            .AddHttpMessageHandler<TvanAuditDelegatingHandler>();

        return services;
    }

    /// <summary>Bật tiến trình nền gửi lại outbox. Tách riêng để job runner có thể bật, còn API thì không.</summary>
    public static IServiceCollection AddTvanOutboxDispatcher(this IServiceCollection services)
    {
        services.AddHostedService<TvanOutboxDispatcher>();
        return services;
    }
}
