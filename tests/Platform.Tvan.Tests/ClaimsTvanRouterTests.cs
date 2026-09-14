using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Domain.Platform.Tvan;
using Platform.Domain.Platform.Tvan.Abstractions;
using Platform.Infrastructure.Tvan.Metadata;
using Platform.Infrastructure.Tvan.Routing;
using Platform.Shared.Constants;

namespace Platform.Tvan.Tests;

/// <summary>
/// Danh sách nhà truyền nhận của từng account nằm trong access token theo đúng thứ tự ưu tiên,
/// nên gateway không phải tra DB cho mỗi hoá đơn và phần mềm Invoice không cần biết
/// nó đang được phục vụ bởi ai.
/// </summary>
public class ClaimsTvanRouterTests
{
    [Fact]
    public async Task Reads_the_whole_provider_list_from_jwt_claims_in_order()
    {
        var router = Build(User(
            (TvanClaimTypes.ProviderCode, "hilo"),
            (TvanClaimTypes.ProviderCode, "minvoice"),
            (TvanClaimTypes.ProviderCode, "bkav"),
            (TvanClaimTypes.TaxCode, "0106713804")));

        var plan = await router.ResolveAsync(null, null, default);

        Assert.Equal(["HILO", "MINVOICE", "BKAV"], plan.ProviderCodes);
        Assert.Equal("HILO", plan.Primary);
        Assert.Equal("0106713804", plan.TaxCode);
        Assert.Equal("jwt-claim", plan.Source);
    }

    [Fact]
    public async Task Drops_duplicate_provider_claims()
    {
        var router = Build(User(
            (TvanClaimTypes.ProviderCode, "HILO"),
            (TvanClaimTypes.ProviderCode, "hilo"),
            (TvanClaimTypes.TaxCode, "0106713804")));

        var plan = await router.ResolveAsync(null, null, default);

        Assert.Equal(["HILO"], plan.ProviderCodes);
    }

    [Fact]
    public async Task Falls_back_to_tenant_bindings_when_token_has_no_provider_claim()
    {
        var router = Build(
            User((TvanClaimTypes.TaxCode, "0101360697")),
            bindings: new Dictionary<string, string[]> { ["0101360697"] = ["BKAV", "HILO"] });

        var plan = await router.ResolveAsync(null, null, default);

        Assert.Equal(["BKAV", "HILO"], plan.ProviderCodes);
        Assert.Equal("tenant-binding", plan.Source);
    }

    [Fact]
    public async Task Fails_clearly_when_account_has_no_provider_at_all()
    {
        var router = Build(User((TvanClaimTypes.TaxCode, "0000000000")));

        var error = await Assert.ThrowsAsync<TvanRoutingException>(
            () => router.ResolveAsync(null, null, default));

        Assert.Contains(TvanClaimTypes.ProviderCode, error.Message);
    }

    [Fact]
    public async Task Fails_when_tax_code_is_missing_everywhere()
    {
        var router = Build(User((TvanClaimTypes.ProviderCode, "HILO")));

        var error = await Assert.ThrowsAsync<TvanRoutingException>(
            () => router.ResolveAsync(null, null, default));

        Assert.Contains("tvan_taxcode", error.Message);
    }

    [Fact]
    public async Task Non_admin_cannot_force_a_provider()
    {
        var router = Build(User(
            (TvanClaimTypes.ProviderCode, "HILO"),
            (TvanClaimTypes.TaxCode, "0106713804")));

        await Assert.ThrowsAsync<TvanRoutingException>(
            () => router.ResolveAsync("BKAV", null, default));
    }

    [Fact]
    public async Task Admin_override_pins_exactly_one_provider_and_disables_failover()
    {
        var router = Build(User(
            (TvanClaimTypes.ProviderCode, "HILO"),
            (TvanClaimTypes.ProviderCode, "BKAV"),
            (TvanClaimTypes.TaxCode, "0106713804"),
            (ClaimTypes.Role, AuthIdentityConstants.Admin)));

        var plan = await router.ResolveAsync("bkav", null, default);

        // Người vận hành đang muốn kiểm tra đúng nhà đó — im lặng chuyển sang nhà khác
        // sẽ che mất chính thứ họ đang kiểm tra.
        Assert.Equal(["BKAV"], plan.ProviderCodes);
        Assert.Equal("override", plan.Source);
    }

    // --- helpers -------------------------------------------------------------

    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Test", ClaimTypes.Name, ClaimTypes.Role));

    private static ITvanRouter Build(ClaimsPrincipal user, Dictionary<string, string[]>? bindings = null)
    {
        var context = new DefaultHttpContext { User = user };
        return new ClaimsTvanRouter(
            new HttpContextAccessor { HttpContext = context },
            new StubMetadataStore(bindings ?? []),
            NullLogger<ClaimsTvanRouter>.Instance);
    }

    private sealed class StubMetadataStore(Dictionary<string, string[]> bindings) : ITvanMetadataStore
    {
        public Task<TvanProvider?> GetProviderAsync(string code, CancellationToken ct) =>
            Task.FromResult<TvanProvider?>(null);

        public Task<TvanCredential?> GetCredentialAsync(Guid providerId, string taxCode, CancellationToken ct) =>
            Task.FromResult<TvanCredential?>(null);

        public Task<IReadOnlyList<string>> GetProviderCodesForTaxCodeAsync(string taxCode, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(bindings.GetValueOrDefault(taxCode) ?? []);
    }
}
