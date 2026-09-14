using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.MediatR.Tvan.Commands;
using Platform.Infrastructure.Persistence.PlatformContext;

namespace Platform.APIs.Application.MigrateData;

/// <summary>
/// Nạp cấu hình T-VAN từ file JSON.
/// Đây cũng là minh hoạ cho nguyên tắc của module: thêm nhà truyền nhận thứ tư
/// chỉ là thêm một phần tử vào <c>AppSettings/tvan-providers.json</c> (hoặc gọi API admin),
/// không đụng tới một dòng C# nào.
/// </summary>
public static class TvanSeed
{
    private const string ProvidersFile = "AppSettings/tvan-providers.json";

    /// <summary>File chứa secret thật, KHÔNG commit. Thiếu file thì bỏ qua.</summary>
    private const string CredentialsFile = "AppSettings/tvan-credentials.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider, IWebHostEnvironment environment)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(TvanSeed));
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Đã có cấu hình thì không ghi đè: production thường chỉnh qua API admin,
        // seed chỉ để dựng môi trường trống.
        if (await db.TvanProviders.AnyAsync())
        {
            logger.LogInformation("Bỏ qua seed T-VAN: đã có cấu hình trong DB.");
            return;
        }

        await SeedProvidersAsync(environment, sender, logger);
        await SeedCredentialsAsync(environment, sender, logger);
    }

    private static async Task SeedProvidersAsync(
        IWebHostEnvironment environment, ISender sender, ILogger logger)
    {
        var path = Path.Combine(environment.ContentRootPath, ProvidersFile);
        if (!File.Exists(path))
        {
            logger.LogWarning("Không tìm thấy {File}, bỏ qua seed nhà truyền nhận.", ProvidersFile);
            return;
        }

        var providers = JsonSerializer.Deserialize<List<UpsertTvanProviderCommand>>(
            await File.ReadAllTextAsync(path), Options) ?? [];

        foreach (var provider in providers)
        {
            await sender.Send(provider);
            logger.LogInformation("Đã seed T-VAN {Code} ({Environment}) với {Count} nghiệp vụ.",
                provider.Code, provider.Environment, provider.Endpoints.Count);
        }
    }

    private static async Task SeedCredentialsAsync(
        IWebHostEnvironment environment, ISender sender, ILogger logger)
    {
        var path = Path.Combine(environment.ContentRootPath, CredentialsFile);
        if (!File.Exists(path))
        {
            logger.LogWarning(
                "Chưa có {File}. Hãy nạp credential qua POST /api/tvan/admin/credentials trước khi gửi hoá đơn.",
                CredentialsFile);
            return;
        }

        var credentials = JsonSerializer.Deserialize<List<UpsertTvanCredentialCommand>>(
            await File.ReadAllTextAsync(path), Options) ?? [];

        foreach (var credential in credentials)
        {
            await sender.Send(credential);
            logger.LogInformation("Đã seed credential {Provider}/{TaxCode}.",
                credential.ProviderCode, credential.TaxCode);
        }
    }
}
