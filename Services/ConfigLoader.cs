using System;
using System.IO;

namespace AutomationContent.Services;

public static class ConfigLoader
{
    /// <summary>
    /// Loads key-value pairs from a .env file into environment variables.
    /// Format: KEY=VALUE
    /// </summary>
    public static void LoadEnv()
    {
        var root = AppContext.BaseDirectory;
        // Try to find .env in current directory or project root
        var envPath = Path.Combine(root, ".env");
        
        // If not found in bin/Debug, look up a few levels (for development)
        for (int i = 0; i < 4; i++)
        {
            if (File.Exists(envPath)) break;
            root = Path.GetDirectoryName(root);
            if (root == null) break;
            envPath = Path.Combine(root, ".env");
        }

        if (!File.Exists(envPath)) return;

        foreach (var line in File.ReadAllLines(envPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            
            // Remove optional quotes
            if (value.StartsWith("\"") && value.EndsWith("\""))
                value = value.Substring(1, value.Length - 2);
            else if (value.StartsWith("'") && value.EndsWith("'"))
                value = value.Substring(1, value.Length - 2);

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
