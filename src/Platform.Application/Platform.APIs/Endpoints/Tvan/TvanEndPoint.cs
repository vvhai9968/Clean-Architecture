using MediatR;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.MediatR.Tvan.Commands;
using Platform.Application.MediatR.Tvan.Queries;
using Platform.Shared.Constants;

namespace Platform.APIs.Endpoints.Tvan;

public static class TvanEndPoint
{
    public static void APIs(this WebApplication app)
    {
        var group = app.MapGroup("api/tvan").WithTags("T-VAN").RequireAuthorization();

        // Endpoint tổng quát: client tự chỉ định nghiệp vụ.
        group.MapPost("messages", (ISender sender, [FromBody] DispatchTvanMessageCommand command) =>
                sender.Send(command))
            .WithSummary("Gửi thông điệp T-VAN (tự chọn operationCode)");

        // Đường tắt theo nghiệp vụ — thân thiện với phần mềm Invoice, dùng chung một handler.
        MapOperation(group, "registrations", TvanOperations.SendRegistration, "Gửi tờ khai đăng ký/thay đổi");
        MapOperation(group, "invoices/coded", TvanOperations.SendInvoiceWithCode, "Gửi hóa đơn có mã");
        MapOperation(group, "invoices/uncoded", TvanOperations.SendInvoiceNoCode, "Gửi hóa đơn không mã");
        MapOperation(group, "invoices/pos", TvanOperations.SendPosInvoice, "Gửi hóa đơn máy tính tiền");
        MapOperation(group, "error-notices", TvanOperations.SendErrorNotice, "Gửi thông báo sai sót 04/SS-HĐĐT");
        MapOperation(group, "summaries", TvanOperations.SendSummary, "Gửi bảng tổng hợp dữ liệu");

        group.MapGet("messages/{correlationKey}",
                (ISender sender, string correlationKey, [FromQuery] bool refresh = false) =>
                    sender.Send(new GetTvanTransactionQuery(correlationKey, refresh)))
            .WithSummary("Tra trạng thái giao dịch (refresh=true để hỏi lại T-VAN)");

        // Webhook NCC gọi về. Anonymous vì NCC không có JWT của hệ thống này;
        // việc xác thực nằm trong ITvanCallbackProcessor theo cấu hình từng provider.
        app.MapPost("api/tvan/callbacks/{providerCode}", async (
                ISender sender, string providerCode, HttpRequest http, CancellationToken ct) =>
            {
                using var reader = new StreamReader(http.Body);
                var body = await reader.ReadToEndAsync(ct);
                var headers = http.Headers.ToDictionary(x => x.Key, x => x.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

                return await sender.Send(
                    new ReceiveTvanCallbackCommand(providerCode.ToUpperInvariant(), body, headers), ct);
            })
            .AllowAnonymous()
            .WithTags("T-VAN")
            .WithSummary("Webhook nhận kết quả từ nhà truyền nhận");

        // Quản trị metadata: đây chính là nơi "thêm nhà truyền nhận mới" diễn ra.
        var admin = app.MapGroup("api/tvan/admin").WithTags("T-VAN Admin")
            .RequireAuthorization(policy => policy.RequireRole(AuthIdentityConstants.Admin));

        admin.MapPost("providers", (ISender sender, [FromBody] UpsertTvanProviderCommand command) =>
                sender.Send(command))
            .WithSummary("Khai báo/cập nhật nhà truyền nhận kèm toàn bộ endpoint");

        admin.MapPost("credentials", (ISender sender, [FromBody] UpsertTvanCredentialCommand command) =>
                sender.Send(command))
            .WithSummary("Lưu credential (được mã hoá trước khi ghi DB)");

        admin.MapPost("users/assign", (ISender sender, [FromBody] AssignUserTvanCommand command) =>
                sender.Send(command))
            .WithSummary("Gán nhà truyền nhận + MST cho account (phát vào claim của token)");
    }

    private static void MapOperation(
        RouteGroupBuilder group, string route, string operationCode, string summary) =>
        group.MapPost(route, (ISender sender, [FromBody] DispatchTvanMessageRequest body) =>
                sender.Send(body.ToCommand(operationCode)))
            .WithSummary(summary);
}

/// <summary>DTO cho các đường tắt — không mang OperationCode vì route đã quyết định.</summary>
public sealed record DispatchTvanMessageRequest(
    string Xml,
    string? TaxCode = null,
    string? CorrelationKey = null,
    string? ProviderCode = null,
    Dictionary<string, string?>? Fields = null,
    bool Deferred = false)
{
    public DispatchTvanMessageCommand ToCommand(string operationCode) => new()
    {
        OperationCode = operationCode,
        Xml = Xml,
        TaxCode = TaxCode,
        CorrelationKey = CorrelationKey,
        ProviderCode = ProviderCode,
        Fields = Fields,
        Deferred = Deferred,
    };
}
