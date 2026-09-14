using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Platform.Domain.Platform.Auth;
using Platform.Infrastructure.Identity;
using Platform.Shared;
using Platform.Shared.Constants;

namespace Platform.Tvan.Tests;

/// <summary>
/// Access token mang toàn bộ kế hoạch định tuyến của account, và sau khi bỏ refresh token
/// thì nó cũng là thứ duy nhất client cầm.
/// </summary>
public class JwtTokenServiceTests
{
    private static readonly AppSettings Settings = new()
    {
        Jwt = new JwtSettings
        {
            Issuer = "platform",
            Audience = "platform.api",
            SigningKey = "f7K2mQ9xL4pA6tZ8cR1vH3nW5yB0DgEJ",
            AccessTokenMinutes = 60,
        },
    };

    private static JwtSecurityToken Issue(TvanTokenBinding? tvan, params string[] roles)
    {
        var service = new JwtTokenService(Settings);
        var (token, _) = service.CreateAccessToken(
            Guid.NewGuid(), "ketoan.abc", "ke.toan@abc.vn", roles, tvan);

        return new JwtSecurityTokenHandler().ReadJwtToken(token);
    }

    [Fact]
    public void Carries_the_username_used_to_sign_in()
    {
        var jwt = Issue(TvanTokenBinding.None);

        Assert.Equal("ketoan.abc",
            jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.PreferredUsername).Value);
    }

    [Fact]
    public void Carries_every_provider_as_its_own_claim_in_priority_order()
    {
        var jwt = Issue(new TvanTokenBinding(["HILO", "MINVOICE", "BKAV"], "0106713804"));

        var providers = jwt.Claims
            .Where(c => c.Type == TvanClaimTypes.ProviderCode)
            .Select(c => c.Value)
            .ToArray();

        // Thứ tự này chính là thứ tự chuyển nhà khi nhà trước không hoạt động.
        Assert.Equal(["HILO", "MINVOICE", "BKAV"], providers);
        Assert.Equal("0106713804", jwt.Claims.Single(c => c.Type == TvanClaimTypes.TaxCode).Value);
    }

    [Fact]
    public void Omits_tvan_claims_when_the_account_has_no_provider_assigned()
    {
        var jwt = Issue(TvanTokenBinding.None, AuthIdentityConstants.User);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == TvanClaimTypes.ProviderCode);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == TvanClaimTypes.TaxCode);
    }

    [Fact]
    public void Carries_roles_so_admin_only_endpoints_can_be_enforced()
    {
        var jwt = Issue(TvanTokenBinding.None, AuthIdentityConstants.Admin, AuthIdentityConstants.User);

        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
        Assert.Equal([AuthIdentityConstants.Admin, AuthIdentityConstants.User], roles);
    }

    [Fact]
    public void Expires_according_to_configuration()
    {
        var before = DateTime.UtcNow;
        var jwt = Issue(TvanTokenBinding.None);

        Assert.InRange(jwt.ValidTo, before.AddMinutes(59), before.AddMinutes(61));
    }

    [Fact]
    public void Rejects_a_signing_key_shorter_than_HS256_requires()
    {
        var weak = new AppSettings { Jwt = new JwtSettings { SigningKey = "too-short" } };
        var service = new JwtTokenService(weak);

        Assert.Throws<InvalidOperationException>(
            () => service.CreateAccessToken(Guid.NewGuid(), "u", "a@b.vn", []));
    }
}
