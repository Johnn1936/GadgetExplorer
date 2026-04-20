/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Loading
{
    internal static class AssemblyLoadProgressReporter
    {
        public static void ReportRuntimeConfigDiagnostics(Action<string>? progress, RuntimeConfigFrameworkInspection inspection)
        {
            foreach (RuntimeConfigDiagnostic diagnostic in inspection.InvalidRuntimeConfigFiles)
            {
                progress?.Invoke($"Warning: Invalid runtimeconfig '{diagnostic.Path}': {diagnostic.Message}");
            }

            foreach (RuntimeConfigDiagnostic diagnostic in inspection.RuntimeConfigFilesWithoutUsableFrameworkRequests)
            {
                progress?.Invoke($"Warning: Runtimeconfig '{diagnostic.Path}': {diagnostic.Message}");
            }
        }

        public static void ReportLoadPlanProgress(
            Action<string>? progress,
            AssemblyLoadPlan loadPlan,
            RuntimeConfigFrameworkInspection inspection)
        {
            if (loadPlan.AssemblyResolutionMode == AssemblyResolutionMode.Restricted)
            {
                progress?.Invoke("Assembly resolution mode 'restricted' limits dependency resolution to the supplied assemblies and directories.");
                return;
            }

            if (loadPlan.RuntimeConfigPaths.Count == 0)
            {
                progress?.Invoke(loadPlan.UsedHostRuntimeFallback
                    ? "No runtimeconfig files were found; retaining host-runtime fallback for reference resolution."
                    : "No runtimeconfig files were found; analysis will stay within the discovered input roots.");
                return;
            }

            if (loadPlan.RequestedFrameworks.Count == 0)
            {
                progress?.Invoke(BuildNoUsableRuntimeConfigProgressMessage(
                    loadPlan.AssemblyResolutionMode,
                    loadPlan.RuntimeConfigPaths.Count,
                    inspection.InvalidRuntimeConfigFiles.Count,
                    inspection.RuntimeConfigFilesWithoutUsableFrameworkRequests.Count,
                    loadPlan.UsedHostRuntimeFallback));
                return;
            }

            progress?.Invoke($"Inferred {loadPlan.RequestedFrameworks.Count} requested runtime framework(s) from {loadPlan.RuntimeConfigPaths.Count} runtimeconfig file(s).");
            progress?.Invoke(loadPlan.InferredInstalledRuntimeDirectories.Count > 0
                ? $"Resolved {loadPlan.InferredInstalledRuntimeDirectories.Count} matching installed runtime director{(loadPlan.InferredInstalledRuntimeDirectories.Count == 1 ? "y" : "ies")}."
                : loadPlan.UsedHostRuntimeFallback
                    ? "No matching installed runtime directories were inferred; retaining host-runtime fallback for reference resolution."
                    : "No matching installed runtime directories were inferred; analysis will stay within the discovered input roots.");
        }

        public static void ReportLoadingDiagnosticsSummary(Action<string>? progress, AssemblyLoadDiagnostics diagnostics)
        {
            if (diagnostics.CandidateAssemblyLoadFailureCount > 0)
            {
                progress?.Invoke($"Skipped {diagnostics.CandidateAssemblyLoadFailureCount} unreadable candidate assembly file(s) during loading.");
            }

            if (diagnostics.UnresolvedReferenceCount > 0)
            {
                progress?.Invoke($"Encountered {diagnostics.UnresolvedReferenceCount} unresolved assembly reference(s) while loading reachable modules.");
            }
        }

        public static void ReportCandidateFileProgress(
            Action<string>? progress,
            bool isCandidateFile,
            ref int processedCandidateFileCount,
            int totalCandidateFileCount,
            ref int lastReportedProgressStep,
            int loadedModuleCount,
            string? latestModuleDisplayName)
        {
            if (!isCandidateFile)
            {
                return;
            }

            processedCandidateFileCount++;
            if (!ScanProgress.TryGetStepPercentage(processedCandidateFileCount, totalCandidateFileCount, ref lastReportedProgressStep, out int percentage))
            {
                return;
            }

            string latestModuleSuffix = latestModuleDisplayName is null
                ? string.Empty
                : $"; latest: {latestModuleDisplayName}";
            progress?.Invoke($"Processed {processedCandidateFileCount}/{totalCandidateFileCount} candidate assembly file(s) ({percentage}%). Loaded {loadedModuleCount} module(s) so far{latestModuleSuffix}");
        }

        private static string BuildNoUsableRuntimeConfigProgressMessage(
            AssemblyResolutionMode assemblyResolutionMode,
            int runtimeConfigPathCount,
            int invalidRuntimeConfigFileCount,
            int runtimeConfigFilesWithoutUsableFrameworkRequestCount,
            bool usedHostRuntimeFallback)
        {
            string suffix = usedHostRuntimeFallback
                ? "retaining host-runtime fallback for reference resolution."
                : assemblyResolutionMode == AssemblyResolutionMode.Restricted
                    ? "assembly resolution mode 'restricted' stays within the supplied input roots."
                    : "analysis will stay within the discovered input roots.";

            if (invalidRuntimeConfigFileCount > 0 && runtimeConfigFilesWithoutUsableFrameworkRequestCount > 0)
            {
                return $"Discovered {runtimeConfigPathCount} runtimeconfig file(s), but none produced usable framework requests ({invalidRuntimeConfigFileCount} failed to parse, {runtimeConfigFilesWithoutUsableFrameworkRequestCount} parsed without usable framework requests); {suffix}";
            }

            if (invalidRuntimeConfigFileCount > 0)
            {
                return $"Discovered {runtimeConfigPathCount} runtimeconfig file(s), but all usable framework discovery failed during parsing; {suffix}";
            }

            return $"Discovered {runtimeConfigPathCount} runtimeconfig file(s), but none produced usable framework requests; {suffix}";
        }
    }
}
