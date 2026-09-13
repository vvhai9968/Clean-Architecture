using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace Platform.DotnetExtensions;

public static class WebHostExtensions
{
    public static void MigrateDatabase<TContext>(this WebApplication app,
        Action<TContext, IServiceProvider> seeder) where TContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<TContext>>();
        var context = services.GetRequiredService<TContext>();

        try
        {
            logger.LogInformation("Migrating database associated with context {DbContextName}", typeof(TContext));
            const int retries = 10;
            var retry = Policy.Handle<SqlException>()
                .WaitAndRetry(retryCount: retries,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, span) =>
                    {
                        logger.LogWarning(exception,
                            "[{Prefix}] Exception {ExceptionType} with message {Message} detected on attempt {Retry} of {Retries}",
                            nameof(TContext), exception.GetType().Name, exception.Message, span, retries);
                    });
            retry.Execute(() => InvokeSeeder(seeder, context, services));
            logger.LogInformation("Migrated database associated with context {DbContextName}", typeof(TContext));
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "An error occurred while migrating the database used on context {DbContextName}", typeof(TContext));
        }
    }

    private static void InvokeSeeder<TContext>(Action<TContext, IServiceProvider> seeder, TContext context,
        IServiceProvider serviceProvider) where TContext : DbContext
    {
        try
        {
            context.Database.Migrate();
            seeder(context, serviceProvider);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
