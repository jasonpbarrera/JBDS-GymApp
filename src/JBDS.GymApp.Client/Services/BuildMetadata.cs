using System.Reflection;

namespace JBDS.GymApp.Client.Services;

public static class BuildMetadata
{
    public static string Version
    {
        get
        {
            var assembly = typeof(BuildMetadata).Assembly;
            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                return info.Split('+')[0];
            }
            return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        }
    }

    public static string DeployDate
    {
        get
        {
            var assembly = typeof(BuildMetadata).Assembly;
            var meta = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuildTimestamp")?.Value;
            return string.IsNullOrWhiteSpace(meta) ? DateTime.UtcNow.ToString("MMM d, yyyy") : meta;
        }
    }
}
