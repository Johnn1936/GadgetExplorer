/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Configuration
{
    /// <summary>
    /// Loads sink-definition based configuration files for include and ignore lists.
    /// </summary>
    public static class SinkConfigurations
    {
        public static IReadOnlyList<SinkDefinition> Load(SinkConfigurationKind kind, string? configPath = null)
        {
            string resolvedPath = ResolvePath(kind, configPath);
            return kind switch
            {
                SinkConfigurationKind.Include => SinkDefinitionFileLoader.LoadRequired(resolvedPath, "Sink config", "*.sinks.json"),
                SinkConfigurationKind.Ignore => SinkDefinitionFileLoader.LoadOptional(resolvedPath, "Ignore-sink config", "*.ignore-sinks.json"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported sink configuration kind.")
            };
        }

        public static string ResolvePath(SinkConfigurationKind kind, string? configPath = null)
        {
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                return Path.GetFullPath(configPath);
            }

            return ShippedResourcePaths.GetDeployedResourceDirectory(AppContext.BaseDirectory, GetDefaultPathSegment(kind));
        }

        private static string GetDefaultPathSegment(SinkConfigurationKind kind)
            => kind switch
            {
                SinkConfigurationKind.Include => ShippedResourcePaths.SinksDirectoryName,
                SinkConfigurationKind.Ignore => ShippedResourcePaths.IgnoreSinksDirectoryName,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported sink configuration kind.")
            };
    }

    public enum SinkConfigurationKind
    {
        Include,
        Ignore
    }
}
