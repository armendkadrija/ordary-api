namespace Odary.Api.Extensions;

public static class ConfigurationExtensions
{
    public static T GetOrThrow<T>(this IConfiguration configuration, string key) {
        if (string.IsNullOrWhiteSpace(configuration[key]))
            throw new ConfigurationException($"Configuration value with the key {key} is null or empty");

        if (configuration[key] == "replaced_in_ci")
            throw new ConfigurationException($"Configuration value with the key {key} should be replaced in CI");

        return configuration.GetValue<T>(key)!;
    }

    public static string GetRuntimeKey(this IConfiguration configuration, IWebHostEnvironment environment) {
        var environmentKey = !environment.IsProduction()
            ? $"{(environment.IsStaging() ? "stg" : environment.IsDevelopment() ? "dev" : environment.EnvironmentName.ToLowerInvariant())}"
            : string.Empty;

        var serviceKey = configuration.GetOrThrow<string>("Service:Key");

        return environment.IsProduction() ? serviceKey : $"{serviceKey}.{environmentKey}";
    }
}

public class ConfigurationException(string message) : Exception(message);