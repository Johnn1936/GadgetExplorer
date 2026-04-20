/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Loading
{
    /// <summary>
    /// Captures the loaded modules together with the inferred resolution plan and assembly origins.
    /// </summary>
    /// <param name="Modules">The loaded modules.</param>
    /// <param name="LoadPlan">The inferred assembly load plan.</param>
    /// <param name="AssemblyOriginsByPath">The classified origins of loaded module paths.</param>
    /// <param name="Diagnostics">The diagnostics captured while building and loading the assembly set.</param>
    public sealed record AssemblyLoadResult(
        IReadOnlyList<ModuleDefMD> Modules,
        AssemblyLoadPlan LoadPlan,
        IReadOnlyDictionary<string, LoadedAssemblyOrigin> AssemblyOriginsByPath,
        AssemblyLoadDiagnostics Diagnostics)
    {
        /// <summary>
        /// Gets the classified origin for a loaded path when available.
        /// </summary>
        /// <param name="path">The path to classify.</param>
        /// <returns>The classified origin.</returns>
        public LoadedAssemblyOrigin GetAssemblyOrigin(string? path)
            => path is not null && AssemblyOriginsByPath.TryGetValue(path, out LoadedAssemblyOrigin origin)
                ? origin
                : LoadedAssemblyOrigin.External;
    }

    /// <summary>
    /// Captures diagnostic detail gathered while discovering and loading assemblies.
    /// </summary>
    /// <param name="CandidateAssemblyLoadFailures">Candidate assembly files that could not be loaded.</param>
    /// <param name="UnresolvedReferences">Assembly references that could not be resolved while walking reachable modules.</param>
    /// <param name="InvalidRuntimeConfigFiles">Runtimeconfig files that could not be parsed.</param>
    /// <param name="RuntimeConfigFilesWithoutUsableFrameworkRequests">Runtimeconfig files that parsed but did not yield usable framework requests.</param>
    public sealed record AssemblyLoadDiagnostics(
        IReadOnlyList<CandidateAssemblyLoadFailure> CandidateAssemblyLoadFailures,
        IReadOnlyList<UnresolvedAssemblyReference> UnresolvedReferences,
        IReadOnlyList<RuntimeConfigDiagnostic> InvalidRuntimeConfigFiles,
        IReadOnlyList<RuntimeConfigDiagnostic> RuntimeConfigFilesWithoutUsableFrameworkRequests)
    {
        /// <summary>
        /// Gets the number of candidate assembly files that could not be loaded.
        /// </summary>
        public int CandidateAssemblyLoadFailureCount => CandidateAssemblyLoadFailures.Count;

        /// <summary>
        /// Gets the number of unresolved assembly references encountered while loading.
        /// </summary>
        public int UnresolvedReferenceCount => UnresolvedReferences.Count;

        /// <summary>
        /// Gets the number of runtimeconfig files that failed to parse.
        /// </summary>
        public int InvalidRuntimeConfigFileCount => InvalidRuntimeConfigFiles.Count;

        /// <summary>
        /// Gets the number of runtimeconfig files that parsed but yielded no usable framework requests.
        /// </summary>
        public int RuntimeConfigFileWithoutUsableFrameworkRequestCount => RuntimeConfigFilesWithoutUsableFrameworkRequests.Count;
    }

    /// <summary>
    /// Describes a candidate assembly file that could not be loaded.
    /// </summary>
    /// <param name="Path">The candidate assembly path.</param>
    /// <param name="Message">The short diagnostic message.</param>
    public sealed record CandidateAssemblyLoadFailure(string Path, string Message);

    /// <summary>
    /// Describes an unresolved assembly reference encountered while loading reachable modules.
    /// </summary>
    /// <param name="RequestingModulePath">The path of the module that requested the reference.</param>
    /// <param name="RequestingModuleDisplayName">The display name of the requesting module.</param>
    /// <param name="ReferenceDisplayName">The unresolved reference display name.</param>
    /// <param name="Message">The short diagnostic message.</param>
    public sealed record UnresolvedAssemblyReference(
        string RequestingModulePath,
        string RequestingModuleDisplayName,
        string ReferenceDisplayName,
        string Message);

    /// <summary>
    /// Describes a runtimeconfig-specific diagnostic.
    /// </summary>
    /// <param name="Path">The runtimeconfig path.</param>
    /// <param name="Message">The short diagnostic message.</param>
    public sealed record RuntimeConfigDiagnostic(string Path, string Message);

    /// <summary>
    /// Describes the inferred resolution plan for a scan target.
    /// </summary>
    /// <param name="InputRoots">The normalized input roots.</param>
    /// <param name="CandidateAssemblyFiles">The candidate assembly files discovered from the inputs.</param>
    /// <param name="RuntimeConfigPaths">The runtimeconfig files discovered from the inputs.</param>
    /// <param name="RequestedFrameworks">The framework requests parsed from runtimeconfig files.</param>
    /// <param name="InferredInstalledRuntimeDirectories">The installed runtime directories inferred from the requested frameworks.</param>
    /// <param name="HostRuntimeSearchDirectories">The host-runtime fallback directories admitted for resolution when the selected mode enables fallback and runtime inference did not succeed.</param>
    /// <param name="ResolverSearchDirectories">The final resolver search directories.</param>
    /// <param name="AssemblyResolutionMode">The selected assembly-resolution mode.</param>
    /// <param name="UsedHostRuntimeFallback">Whether host-runtime fallback was used.</param>
    /// <param name="Warnings">Any warnings emitted while building the load plan.</param>
    public sealed record AssemblyLoadPlan(
        IReadOnlyList<string> InputRoots,
        IReadOnlyList<string> CandidateAssemblyFiles,
        IReadOnlyList<string> RuntimeConfigPaths,
        IReadOnlyList<RuntimeFrameworkRequest> RequestedFrameworks,
        IReadOnlyList<string> InferredInstalledRuntimeDirectories,
        IReadOnlyList<string> HostRuntimeSearchDirectories,
        IReadOnlyList<string> ResolverSearchDirectories,
        AssemblyResolutionMode AssemblyResolutionMode,
        bool UsedHostRuntimeFallback,
        IReadOnlyList<string> Warnings);

    /// <summary>
    /// Describes a requested shared framework from a runtimeconfig file.
    /// </summary>
    /// <param name="Name">The framework name.</param>
    /// <param name="Version">The requested version.</param>
    public sealed record RuntimeFrameworkRequest(string Name, Version Version);

    /// <summary>
    /// Describes where a loaded assembly came from.
    /// </summary>
    public enum LoadedAssemblyOrigin
    {
        InputRoot,
        InferredInstalledRuntime,
        HostRuntimeFallback,
        GlobalAssemblyCache,
        External
    }
}
