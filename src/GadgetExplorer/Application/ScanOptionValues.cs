/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Application
{
    internal static class ScanOptionValues
    {
        private static readonly ScanOptionValue<FindingSortMode>[] s_sortModes =
        [
            new(FindingSortMode.ShortestPath, "shortest-path"),
            new(FindingSortMode.PerSinkShortestPath, "per-sink-shortest-path"),
            new(FindingSortMode.TypeName, "type-name")
        ];

        private static readonly ScanOptionValue<InterfaceExpansionMode>[] s_interfaceExpansionModes =
        [
            new(InterfaceExpansionMode.Off, "off"),
            new(InterfaceExpansionMode.Strict, "strict"),
            new(InterfaceExpansionMode.Broad, "broad")
        ];

        private static readonly ScanOptionValue<ScanOutputFormat>[] s_outputFormats =
        [
            new(ScanOutputFormat.Text, "text"),
            new(ScanOutputFormat.Json, "json")
        ];

        private static readonly ScanOptionValue<AssemblyResolutionMode>[] s_assemblyResolutionModes =
        [
            new(AssemblyResolutionMode.Restricted, "restricted"),
            new(AssemblyResolutionMode.InferenceNoFallback, "inference-no-fallback"),
            new(AssemblyResolutionMode.InferenceWithFallback, "inference-with-fallback")
        ];

        public static string Format(FindingSortMode mode)
            => TryGetName(s_sortModes, mode, out string? name)
                ? name!
                : mode.ToString();

        public static string Format(InterfaceExpansionMode mode)
            => TryGetName(s_interfaceExpansionModes, mode, out string? name)
                ? name!
                : mode.ToString();

        public static string Format(ScanOutputFormat format)
            => TryGetName(s_outputFormats, format, out string? name)
                ? name!
                : format.ToString();

        public static string Format(AssemblyResolutionMode mode)
            => TryGetName(s_assemblyResolutionModes, mode, out string? name)
                ? name!
                : mode.ToString();

        public static bool TryParseSortMode(string value, out FindingSortMode mode)
            => TryParse(s_sortModes, value, out mode);

        public static bool TryParseInterfaceExpansionMode(string value, out InterfaceExpansionMode mode)
            => TryParse(s_interfaceExpansionModes, value, out mode);

        public static bool TryParseOutputFormat(string value, out ScanOutputFormat format)
            => TryParse(s_outputFormats, value, out format);

        public static bool TryParseAssemblyResolutionMode(string value, out AssemblyResolutionMode mode)
            => TryParse(s_assemblyResolutionModes, value, out mode);

        public static string GetSortModeChoiceList()
            => FormatChoiceList(s_sortModes);

        public static string GetInterfaceExpansionChoiceList()
            => FormatChoiceList(s_interfaceExpansionModes);

        public static string GetOutputFormatChoiceList()
            => FormatChoiceList(s_outputFormats);

        public static string GetAssemblyResolutionModeChoiceList()
            => FormatChoiceList(s_assemblyResolutionModes);

        public static string GetSortModePipeList()
            => FormatPipeList(s_sortModes);

        public static string GetInterfaceExpansionPipeList()
            => FormatPipeList(s_interfaceExpansionModes);

        public static string GetOutputFormatPipeList()
            => FormatPipeList(s_outputFormats);

        public static string GetAssemblyResolutionModePipeList()
            => FormatPipeList(s_assemblyResolutionModes);

        private static bool TryParse<T>(IReadOnlyList<ScanOptionValue<T>> values, string value, out T parsedValue)
        {
            string normalizedValue = value.Trim();
            foreach (ScanOptionValue<T> entry in values)
            {
                if (string.Equals(entry.Name, normalizedValue, StringComparison.OrdinalIgnoreCase))
                {
                    parsedValue = entry.Value;
                    return true;
                }
            }

            parsedValue = default!;
            return false;
        }

        private static bool TryGetName<T>(IReadOnlyList<ScanOptionValue<T>> values, T value, out string? name)
        {
            foreach (ScanOptionValue<T> entry in values)
            {
                if (EqualityComparer<T>.Default.Equals(entry.Value, value))
                {
                    name = entry.Name;
                    return true;
                }
            }

            name = null;
            return false;
        }

        private static string FormatChoiceList<T>(IReadOnlyList<ScanOptionValue<T>> values)
            => values.Count switch
            {
                0 => string.Empty,
                1 => $"'{values[0].Name}'",
                2 => $"'{values[0].Name}' or '{values[1].Name}'",
                _ => string.Join(", ", values.Take(values.Count - 1).Select(value => $"'{value.Name}'")) + $", or '{values[^1].Name}'"
            };

        private static string FormatPipeList<T>(IReadOnlyList<ScanOptionValue<T>> values)
            => string.Join(" | ", values.Select(value => value.Name));

        private sealed record ScanOptionValue<T>(T Value, string Name);
    }
}
