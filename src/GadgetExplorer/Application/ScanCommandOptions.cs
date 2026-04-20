/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Globalization;
namespace GadgetExplorer.Application
{
    /// <summary>
    /// Describes one command-line scan request.
    /// </summary>
    public sealed record ScanCommandOptions(
        IReadOnlyList<string> AssemblyInputs,
        string? IncludeSinkConfigPath,
        string? IgnoreSinkConfigPath,
        FindingSortMode SortMode,
        InterfaceExpansionMode InterfaceExpansionMode,
        int? MaxPathLength,
        string? OutputPath,
        string? ProfileName,
        string? ProfileFilePath,
        AssemblyResolutionMode AssemblyResolutionMode,
        ScanOutputFormat OutputFormat = ScanOutputFormat.Text,
        IReadOnlyList<string>? CommandLineArguments = null)
    {
        /// <summary>
        /// Gets the formatted raw command-line arguments.
        /// </summary>
        public string CommandLineArgumentsLabel
            => CommandLineArguments is null || CommandLineArguments.Count == 0
                ? "<none>"
                : string.Join(" ", CommandLineArguments.Select(QuoteArgument));

        /// <summary>
        /// Gets the resolved output path, when one was supplied.
        /// </summary>
        public string? ResolvedOutputPath
            => OutputPath is null ? null : Path.GetFullPath(OutputPath);

        /// <summary>
        /// Gets the resolved include-sink configuration path.
        /// </summary>
        public string ResolvedIncludeSinkConfigPath
            => SinkConfigurations.ResolvePath(SinkConfigurationKind.Include, IncludeSinkConfigPath);

        /// <summary>
        /// Gets the resolved ignore-sink configuration path.
        /// </summary>
        public string ResolvedIgnoreSinkConfigPath
            => SinkConfigurations.ResolvePath(SinkConfigurationKind.Ignore, IgnoreSinkConfigPath);

        /// <summary>
        /// Gets the resolved serializer profile file path, when one was supplied.
        /// </summary>
        public string? ResolvedProfileFilePath
            => ProfileFilePath is null ? null : Path.GetFullPath(ProfileFilePath);

        /// <summary>
        /// Gets the requested finding-sort display text.
        /// </summary>
        public string SortModeDisplayText
            => ScanOptionValues.Format(SortMode);

        /// <summary>
        /// Gets the requested dispatch-mode display text.
        /// </summary>
        public string InterfaceExpansionModeDisplayText
            => ScanOptionValues.Format(InterfaceExpansionMode);

        /// <summary>
        /// Gets the requested output-format display text.
        /// </summary>
        public string OutputFormatDisplayText
            => ScanOptionValues.Format(OutputFormat);

        /// <summary>
        /// Gets the effective maximum path-length display text.
        /// </summary>
        public string MaxPathLengthLabel
            => MaxPathLength?.ToString(CultureInfo.InvariantCulture) ?? "<unbounded>";

        /// <summary>
        /// Gets the requested assembly-resolution mode display text.
        /// </summary>
        public string AssemblyResolutionModeDisplayText
            => ScanOptionValues.Format(AssemblyResolutionMode);

        private static string QuoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            return argument.Any(char.IsWhiteSpace) || argument.Contains('"')
                ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
                : argument;
        }
    }
}

