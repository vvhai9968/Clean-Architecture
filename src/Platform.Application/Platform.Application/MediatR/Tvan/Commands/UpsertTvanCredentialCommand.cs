using System.Net;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Domain.Platform.Tvan;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared.Common;

namespace Platform.Application.MediatR.Tvan.Commands;

/// <summary>
/// Lưu credential của một MST với một T-VAN. Secrets được mã hoá trước khi ghi DB
/// và không bao giờ được trả ngược ra API.
/// </summary>
public record UpsertTvanCredentialCommand(
    string ProviderCode,
    string TaxCode,
    Dictionary<string, string?> Secrets,
    string Environment = "UAT") : IRequest<IResult>
{
    internal sealed class Handler(
        PlatformDbContext db,
        ISecretProtector protector,
        ITvanMetadataCache cache) : IRequestHandler<UpsertTvanCredentialCommand, IResult>
    {
        public async Task<IResult> Handle(UpsertTvanCredentialCommand request, CancellationToken ct)
        {
            var provider = await db.TvanProviders.AsNoTracking().FirstOrDefaultAsync(
                x => x.Code == request.ProviderCode && x.Environment == request.Environment, ct);

            if (provider is null)
            {
                return Results.NotFound(new ApiResponse<string>
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Message = $"Chưa khai báo T-VAN {request.ProviderCode} ({request.Environment}).",
                });
            }

            var credential = await db.TvanCredentials.FirstOrDefaultAsync(
                x => x.ProviderId == provider.Id && x.TaxCode == request.TaxCode, ct);

            if (credential is null)
            {
                credential = new TvanCredential { ProviderId = provider.Id, TaxCode = request.TaxCode };
                db.TvanCredentials.Add(credential);
            }

            credential.ProtectedSecretsJson = protector.Protect(request.Secrets);
            credential.IsActive = true;

            await db.SaveChangesAsync(ct);
            cache.EvictProvider(request.ProviderCode);

            return Results.Ok(new ApiResponse<object>
            {
                StatusCode = HttpStatusCode.OK,
                IsSuccess = true,
                Message = "Đã lưu credential.",
                Data = new { credential.Id, request.ProviderCode, request.TaxCode, Keys = request.Secrets.Keys },
            });
        }
    }
}
