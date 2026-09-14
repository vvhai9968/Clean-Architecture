using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Domain.Platform.Tvan;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Tvan.Commands;

/// <summary>
/// Khai báo hoặc cập nhật một nhà truyền nhận. Đây chính là cách thêm T-VAN mới mà không sửa code.
/// Endpoints/headers gửi kèm sẽ thay thế toàn bộ khai báo cũ của provider.
/// </summary>
public record UpsertTvanProviderCommand : IRequest<IResult>
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string BaseUrl { get; init; }
    public string Environment { get; init; } = "UAT";
    public bool IsActive { get; init; } = true;
    public int Priority { get; init; }
    public int TimeoutSeconds { get; init; } = 60;
    public required string AuthSchemeKey { get; init; }
    public JsonElement? AuthConfig { get; init; }
    public JsonElement? CallbackMap { get; init; }
    public JsonElement? ResilienceConfig { get; init; }
    public List<TvanEndpointDefinition> Endpoints { get; init; } = [];
    public List<TvanHeaderDefinition> Headers { get; init; } = [];

    internal sealed class Handler(
        PlatformDbContext db,
        ITvanMetadataCache cache) : IRequestHandler<UpsertTvanProviderCommand, IResult>
    {
        public async Task<IResult> Handle(UpsertTvanProviderCommand request, CancellationToken ct)
        {
            var provider = await db.TvanProviders
                .Include(x => x.Endpoints)
                .Include(x => x.Headers)
                .FirstOrDefaultAsync(x => x.Code == request.Code && x.Environment == request.Environment, ct);

            if (provider is null)
            {
                provider = new TvanProvider { Code = request.Code, Environment = request.Environment };
                db.TvanProviders.Add(provider);
            }
            else
            {
                db.TvanEndpoints.RemoveRange(provider.Endpoints);
                db.TvanHeaderTemplates.RemoveRange(provider.Headers);
            }

            provider.Name = request.Name;
            provider.BaseUrl = request.BaseUrl;
            provider.IsActive = request.IsActive;
            provider.Priority = request.Priority;
            provider.TimeoutSeconds = request.TimeoutSeconds;
            provider.AuthSchemeKey = request.AuthSchemeKey;
            provider.AuthConfigJson = Raw(request.AuthConfig) ?? "{}";
            provider.CallbackMapJson = Raw(request.CallbackMap);
            provider.ResilienceConfigJson = Raw(request.ResilienceConfig);

            foreach (var endpoint in request.Endpoints)
            {
                db.TvanEndpoints.Add(new TvanEndpoint
                {
                    ProviderId = provider.Id,
                    OperationCode = endpoint.OperationCode,
                    HttpMethod = endpoint.HttpMethod,
                    PathTemplate = endpoint.PathTemplate,
                    ContentType = endpoint.ContentType,
                    SoapAction = endpoint.SoapAction,
                    BodyTemplate = endpoint.BodyTemplate,
                    RequestTransformsJson = Raw(endpoint.RequestTransforms) ?? "[]",
                    ResponseMapJson = Raw(endpoint.ResponseMap) ?? "{}",
                    ArgsJson = Raw(endpoint.Args) ?? "{}",
                });
            }

            foreach (var header in request.Headers)
            {
                db.TvanHeaderTemplates.Add(new TvanHeaderTemplate
                {
                    ProviderId = provider.Id,
                    Name = header.Name,
                    ValueTemplate = header.ValueTemplate,
                    Order = header.Order,
                });
            }

            await db.SaveChangesAsync(ct);
            cache.EvictProvider(request.Code);

            return Results.Ok(new ApiResponse<object>
            {
                StatusCode = HttpStatusCode.OK,
                IsSuccess = true,
                Message = $"Đã lưu cấu hình T-VAN {request.Code} ({request.Environment}).",
                Data = new { provider.Id, provider.Code, Endpoints = request.Endpoints.Count },
            });
        }

        private static string? Raw(JsonElement? element) =>
            element is null || element.Value.ValueKind == JsonValueKind.Undefined
                ? null
                : element.Value.GetRawText();
    }
}

public sealed record TvanEndpointDefinition
{
    public required string OperationCode { get; init; }
    public string HttpMethod { get; init; } = "POST";
    public required string PathTemplate { get; init; }
    public string ContentType { get; init; } = "application/json";
    public string? SoapAction { get; init; }
    public string BodyTemplate { get; init; } = string.Empty;
    public JsonElement? RequestTransforms { get; init; }
    public JsonElement? ResponseMap { get; init; }
    public JsonElement? Args { get; init; }
}

public sealed record TvanHeaderDefinition
{
    public required string Name { get; init; }
    public required string ValueTemplate { get; init; }
    public int Order { get; init; }
}
