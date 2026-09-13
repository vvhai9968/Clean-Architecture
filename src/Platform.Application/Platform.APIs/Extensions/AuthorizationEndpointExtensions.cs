using Microsoft.AspNetCore.Authorization;

namespace Platform.APIs;

public static class AuthorizationEndpointExtensions
{
    public static TBuilder RequireRoles<TBuilder>(this TBuilder builder, params string[] roles)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(new AuthorizeAttribute { Roles = string.Join(",", roles) });
}
