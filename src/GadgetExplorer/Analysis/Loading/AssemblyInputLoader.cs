/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Loading
{
    /// <summary>
    /// Expands user inputs into assemblies and loads the reachable module set for analysis.
    /// </summary>
    public static class AssemblyInputLoader
    {
        /// <summary>
        /// Loads assemblies from file and directory inputs.
        /// </summary>
        /// <param name="inputs">The file or directory inputs to scan.</param>
        /// <param name="progress">An optional progress callback.</param>
        /// <param name="assemblyResolutionMode">Controls how assembly resolution expands beyond the supplied input roots.</param>
        /// <returns>The loaded modules available for analysis.</returns>
        public static IReadOnlyList<ModuleDefMD> LoadModules(
            IEnumerable<string> inputs,
            Action<string>? progress = null,
            AssemblyResolutionMode assemblyResolutionMode = AssemblyResolutionMode.InferenceNoFallback)
            => LoadAssemblySet(inputs, progress, assemblyResolutionMode).Modules;

        /// <summary>
        /// Loads assemblies from file and directory inputs together with load-plan provenance.
        /// </summary>
        /// <param name="inputs">The file or directory inputs to scan.</param>
        /// <param name="progress">An optional progress callback.</param>
        /// <param name="assemblyResolutionMode">Controls how assembly resolution expands beyond the supplied input roots.</param>
        /// <returns>The loaded modules and the inferred load plan.</returns>
        public static AssemblyLoadResult LoadAssemblySet(
            IEnumerable<string> inputs,
            Action<string>? progress = null,
            AssemblyResolutionMode assemblyResolutionMode = AssemblyResolutionMode.InferenceNoFallback)
        {
            progress?.Invoke("Expanding input paths and locating candidate assembly files.");
            AssemblyInputExpansion expandedInputs = AssemblyInputExpander.Expand(inputs);

            if (expandedInputs.CandidateAssemblyFiles.Count == 0)
            {
                throw new InvalidOperationException("No candidate assembly files were found in the provided inputs.");
            }

            progress?.Invoke($"Discovered {expandedInputs.CandidateAssemblyFiles.Count} candidate assembly file(s) across {expandedInputs.InputRoots.Count} input root(s).");

            RuntimeConfigFrameworkInspection runtimeConfigInspection = RuntimeConfigFrameworkInspector.Inspect(expandedInputs.RuntimeConfigPaths);
            AssemblyLoadProgressReporter.ReportRuntimeConfigDiagnostics(progress, runtimeConfigInspection);

            AssemblyLoadPlan loadPlan = AssemblyLoadPlanBuilder.Build(
                expandedInputs,
                runtimeConfigInspection,
                assemblyResolutionMode);
            AssemblyLoadProgressReporter.ReportLoadPlanProgress(progress, loadPlan, runtimeConfigInspection);

            AssemblyModuleLoadResult loadedModules = AssemblyModuleLoader.Load(loadPlan, progress);
            var diagnostics = new AssemblyLoadDiagnostics(
                loadedModules.CandidateAssemblyLoadFailures,
                loadedModules.UnresolvedReferences,
                runtimeConfigInspection.InvalidRuntimeConfigFiles,
                runtimeConfigInspection.RuntimeConfigFilesWithoutUsableFrameworkRequests);
            AssemblyLoadProgressReporter.ReportLoadingDiagnosticsSummary(progress, diagnostics);
            progress?.Invoke($"Assembly loading complete. {loadedModules.Modules.Count} module(s) available for analysis.");

            return new AssemblyLoadResult(
                loadedModules.Modules,
                loadPlan,
                loadedModules.AssemblyOriginsByPath,
                diagnostics);
        }
    }
}
