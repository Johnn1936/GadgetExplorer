/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Text.Json;

namespace GadgetExplorer.Analysis.Loading
{
    internal static class RuntimeConfigFrameworkInspector
    {
        public static RuntimeConfigFrameworkInspection Inspect(IReadOnlyList<string> runtimeConfigPaths)
        {
            RuntimeConfigParseResult[] parseResults = [.. runtimeConfigPaths
                .Select(ParseRequestedFrameworks)
                .OrderBy(result => result.Path, StringComparer.OrdinalIgnoreCase)];

            RuntimeConfigDiagnostic[] invalidRuntimeConfigFiles = [.. parseResults
                .Where(result => result.FailureMessage is not null)
                .Select(result => new RuntimeConfigDiagnostic(result.Path, result.FailureMessage!))];

            RuntimeConfigDiagnostic[] runtimeConfigFilesWithoutUsableFrameworkRequests = [.. parseResults
                .Where(result => result.FailureMessage is null && result.RequestedFrameworks.Count == 0)
                .Select(result => new RuntimeConfigDiagnostic(result.Path, "No usable framework requests were found."))];

            RuntimeFrameworkRequest[] requestedFrameworks = [.. parseResults
                .SelectMany(result => result.RequestedFrameworks)
                .Distinct()
                .OrderBy(framework => framework.Name, StringComparer.Ordinal)
                .ThenBy(framework => framework.Version)];

            return new RuntimeConfigFrameworkInspection(
                requestedFrameworks,
                invalidRuntimeConfigFiles,
                runtimeConfigFilesWithoutUsableFrameworkRequests);
        }

        private static RuntimeConfigParseResult ParseRequestedFrameworks(string runtimeConfigPath)
        {
            try
            {
                using FileStream stream = File.OpenRead(runtimeConfigPath);
                using var document = JsonDocument.Parse(stream);
                if (!document.RootElement.TryGetProperty("runtimeOptions", out JsonElement runtimeOptions))
                {
                    return new RuntimeConfigParseResult(runtimeConfigPath, [], null);
                }

                var requests = new List<RuntimeFrameworkRequest>();
                AddFrameworkRequests(runtimeOptions, "includedFrameworks", requests);
                AddFrameworkRequests(runtimeOptions, "frameworks", requests);
                AddFrameworkRequests(runtimeOptions, "framework", requests);
                return new RuntimeConfigParseResult(
                    runtimeConfigPath,
                    [.. requests
                        .Distinct()
                        .OrderBy(request => request.Name, StringComparer.Ordinal)
                        .ThenBy(request => request.Version)],
                    null);
            }
            catch (Exception ex)
            {
                return new RuntimeConfigParseResult(runtimeConfigPath, [], GetExceptionSummary(ex));
            }
        }

        private static void AddFrameworkRequests(JsonElement runtimeOptions, string propertyName, List<RuntimeFrameworkRequest> requests)
        {
            if (!runtimeOptions.TryGetProperty(propertyName, out JsonElement property))
            {
                return;
            }

            if (property.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in property.EnumerateArray())
                {
                    if (TryParseFrameworkRequest(element, out RuntimeFrameworkRequest request))
                    {
                        requests.Add(request);
                    }
                }

                return;
            }

            if (property.ValueKind == JsonValueKind.Object && TryParseFrameworkRequest(property, out RuntimeFrameworkRequest singleRequest))
            {
                requests.Add(singleRequest);
            }
        }

        private static bool TryParseFrameworkRequest(JsonElement element, out RuntimeFrameworkRequest request)
        {
            request = null!;
            if (!element.TryGetProperty("name", out JsonElement nameProperty) ||
                !element.TryGetProperty("version", out JsonElement versionProperty))
            {
                return false;
            }

            string? name = nameProperty.GetString();
            string? versionText = versionProperty.GetString();
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(versionText) ||
                !Version.TryParse(versionText, out Version? version))
            {
                return false;
            }

            request = new RuntimeFrameworkRequest(name, version);
            return true;
        }

        private static string GetExceptionSummary(Exception ex)
        {
            Exception current = ex;
            while (current.InnerException is not null)
            {
                current = current.InnerException;
            }

            return string.IsNullOrWhiteSpace(current.Message)
                ? current.GetType().Name
                : $"{current.GetType().Name}: {current.Message}";
        }

        private sealed record RuntimeConfigParseResult(
            string Path,
            IReadOnlyList<RuntimeFrameworkRequest> RequestedFrameworks,
            string? FailureMessage);
    }

    internal sealed record RuntimeConfigFrameworkInspection(
        IReadOnlyList<RuntimeFrameworkRequest> RequestedFrameworks,
        IReadOnlyList<RuntimeConfigDiagnostic> InvalidRuntimeConfigFiles,
        IReadOnlyList<RuntimeConfigDiagnostic> RuntimeConfigFilesWithoutUsableFrameworkRequests);
}
