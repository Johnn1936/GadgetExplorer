/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Loading
{
    internal static class AssemblyLoadPlanBuilder
    {
        public static AssemblyLoadPlan Build(
            AssemblyInputExpansion inputs,
            RuntimeConfigFrameworkInspection runtimeConfigInspection,
            AssemblyResolutionMode assemblyResolutionMode)
        {
            IReadOnlyList<RuntimeFrameworkRequest> requestedFrameworks = runtimeConfigInspection.RequestedFrameworks;
            IReadOnlyList<string> inferredInstalledRuntimeDirectories = assemblyResolutionMode == AssemblyResolutionMode.Restricted || requestedFrameworks.Count == 0
                ? []
                : InstalledRuntimeLocator.InferDirectories(requestedFrameworks);

            bool shouldUseHostRuntimeFallback = assemblyResolutionMode == AssemblyResolutionMode.InferenceWithFallback &&
                inferredInstalledRuntimeDirectories.Count == 0;
            IReadOnlyList<string> hostRuntimeSearchDirectories = shouldUseHostRuntimeFallback
                ? HostRuntimeDirectoryDiscovery.GetSearchDirectories()
                : [];

            string[] resolverSearchDirectories = [.. inputs.CandidateDirectories
                .Concat(inferredInstalledRuntimeDirectories)
                .Concat(hostRuntimeSearchDirectories)
                .Distinct(StringComparer.OrdinalIgnoreCase)];

            var warnings = new List<string>();
            if (runtimeConfigInspection.InvalidRuntimeConfigFiles.Count > 0)
            {
                warnings.Add($"{runtimeConfigInspection.InvalidRuntimeConfigFiles.Count} runtimeconfig file(s) failed to parse.");
            }

            if (runtimeConfigInspection.RuntimeConfigFilesWithoutUsableFrameworkRequests.Count > 0)
            {
                warnings.Add($"{runtimeConfigInspection.RuntimeConfigFilesWithoutUsableFrameworkRequests.Count} runtimeconfig file(s) produced no usable framework requests.");
            }

            if (assemblyResolutionMode != AssemblyResolutionMode.Restricted &&
                requestedFrameworks.Count > 0 &&
                inferredInstalledRuntimeDirectories.Count == 0)
            {
                warnings.Add(assemblyResolutionMode == AssemblyResolutionMode.InferenceWithFallback
                    ? "No matching installed runtime directories were inferred from the discovered runtimeconfig files; retaining host-runtime fallback for reference resolution."
                    : "No matching installed runtime directories were inferred from the discovered runtimeconfig files.");
            }

            return new AssemblyLoadPlan(
                inputs.InputRoots,
                inputs.CandidateAssemblyFiles,
                inputs.RuntimeConfigPaths,
                requestedFrameworks,
                inferredInstalledRuntimeDirectories,
                hostRuntimeSearchDirectories,
                resolverSearchDirectories,
                assemblyResolutionMode,
                hostRuntimeSearchDirectories.Count > 0,
                warnings);
        }
    }
}
