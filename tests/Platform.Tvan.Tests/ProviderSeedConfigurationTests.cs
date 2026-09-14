using System.Text.Json;
using Platform.Application.MediatR.Tvan.Commands;
using Platform.Infrastructure.Tvan.Auth;
using Platform.Infrastructure.Tvan.Model;
using Platform.Shared.Constants;

namespace Platform.Tvan.Tests;

/// <summary>
/// Kiểm tra chính file cấu hình sẽ chạy trên production, không phải bản chép tay của nó.
/// Sai một dấu ngoặc trong template hay một key transform không tồn tại sẽ bị bắt ở CI
/// thay vì lúc gửi hoá đơn thật lên Cơ quan Thuế.
/// </summary>
public class ProviderSeedConfigurationTests
{
    private static readonly List<UpsertTvanProviderCommand> Providers = Load();

    private static List<UpsertTvanProviderCommand> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tvan-providers.json");
        return JsonSerializer.Deserialize<List<UpsertTvanProviderCommand>>(
            File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    public void All_three_providers_are_declared()
    {
        Assert.Equal(
            new[] { "BKAV", "HILO", "MINVOICE" },
            Providers.Select(x => x.Code).OrderBy(x => x).ToArray());
    }

    [Theory]
    [InlineData("BKAV")]
    [InlineData("HILO")]
    [InlineData("MINVOICE")]
    public void Every_provider_covers_the_core_business_operations(string code)
    {
        var provider = Providers.Single(x => x.Code == code);
        var declared = provider.Endpoints.Select(x => x.OperationCode).ToHashSet();

        foreach (var required in new[]
                 {
                     TvanOperations.SendRegistration,
                     TvanOperations.SendInvoiceWithCode,
                     TvanOperations.SendInvoiceNoCode,
                     TvanOperations.SendErrorNotice,
                     TvanOperations.SendSummary,
                 })
        {
            Assert.Contains(required, declared);
        }
    }

    [Fact]
    public void Every_operation_code_is_one_the_application_layer_knows()
    {
        foreach (var endpoint in Providers.SelectMany(x => x.Endpoints))
            Assert.Contains(endpoint.OperationCode, TvanOperations.All);
    }

    [Fact]
    public void Every_auth_scheme_is_registered_in_code()
    {
        string[] registered = [TvanAuthSchemes.None, TvanAuthSchemes.LoginToken];

        foreach (var provider in Providers)
            Assert.Contains(provider.AuthSchemeKey, registered);
    }

    [Fact]
    public void Every_transform_key_is_registered_in_code()
    {
        string[] registered =
        [
            TvanTransforms.Gzip, TvanTransforms.AesCbc,
            TvanTransforms.Base64, TvanTransforms.BkavCommandData,
        ];

        foreach (var endpoint in Providers.SelectMany(x => x.Endpoints))
        {
            var steps = endpoint.RequestTransforms is null
                ? []
                : TvanResponseMap.ParseTransforms(endpoint.RequestTransforms.Value);

            foreach (var step in steps)
                Assert.Contains(step.Key, registered);
        }
    }

    [Fact]
    public void Every_response_map_parses()
    {
        foreach (var endpoint in Providers.SelectMany(x => x.Endpoints))
        {
            var map = TvanResponseMap.Parse(endpoint.ResponseMap?.GetRawText() ?? "{}");
            Assert.NotNull(map.Result.Format);
        }
    }

    [Fact]
    public void Login_token_configs_are_complete()
    {
        foreach (var provider in Providers.Where(x => x.AuthSchemeKey == TvanAuthSchemes.LoginToken))
        {
            var config = LoginTokenConfig.From(provider.AuthConfig!.Value);

            Assert.False(string.IsNullOrWhiteSpace(config.LoginPath));
            Assert.False(string.IsNullOrWhiteSpace(config.TokenPath));
            Assert.Contains("{{ msg.token }}", config.HeaderFormat);
        }
    }

    [Fact]
    public void Bkav_needs_no_login_because_it_has_no_login_api()
    {
        var bkav = Providers.Single(x => x.Code == "BKAV");
        Assert.Equal(TvanAuthSchemes.None, bkav.AuthSchemeKey);
    }

    [Fact]
    public void Bkav_encrypts_every_outbound_payload()
    {
        var bkav = Providers.Single(x => x.Code == "BKAV");

        foreach (var endpoint in bkav.Endpoints)
        {
            var keys = TvanResponseMap.ParseTransforms(endpoint.RequestTransforms!.Value)
                .Select(x => x.Key).ToArray();

            Assert.Equal(
                [TvanTransforms.BkavCommandData, TvanTransforms.Gzip, TvanTransforms.AesCbc, TvanTransforms.Base64],
                keys);
        }
    }

    [Fact]
    public void Bkav_endpoints_each_carry_a_distinct_command_type()
    {
        var bkav = Providers.Single(x => x.Code == "BKAV");

        var cmdTypes = bkav.Endpoints
            .Select(x => x.Args!.Value.GetProperty("CmdType").GetString())
            .ToArray();

        Assert.Equal(cmdTypes.Length, cmdTypes.Distinct().Count());
    }
}
