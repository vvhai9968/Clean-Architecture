using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Platform.DotnetExtensions;

public static class EnvironmentExtensions
{
    public static void SetupEnvs<TEnv>(this IServiceCollection services,
        IWebHostEnvironment environment, out TEnv env,
        string subPath = "AppSettings") where TEnv : class, new()
    {
        SetupEnvs(environment, out env, subPath);
        services.AddSingleton(env);
    }

    private static void SetupEnvs<TEnv>(IHostEnvironment environment,
        out TEnv env, string subPath = "AppSettings") where TEnv : class, new()
    {
        var appSettingsJson = $"appsettings.{environment.EnvironmentName}.json";
        if (!string.IsNullOrEmpty(subPath))
            appSettingsJson = Path.Combine(environment.ContentRootPath, subPath, appSettingsJson);
        env = new TEnv();
        var builder = new ConfigurationBuilder()
            .SetBasePath(environment.ContentRootPath)
            .AddJsonFile(appSettingsJson)
            .AddEnvironmentVariables();

        var configuration = builder.Build();
        configuration.Bind(env);
    }
}
