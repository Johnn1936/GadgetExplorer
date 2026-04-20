/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Application.Reporting
{
    internal sealed record ScanReportDocument(
        int SchemaVersion,
        ScanReconDocument Recon,
        IReadOnlyList<ScanFindingDocument> Findings);

    internal sealed record ScanReconDocument(
        IReadOnlyList<string> CommandLineArguments,
        IReadOnlyList<string> Inputs,
        ScanConfigurationDocument Configuration,
        ScanSinkCountsDocument SinkCounts,
        ScanLoadCountsDocument LoadCounts,
        ScanRuntimeDocument Runtime,
        ScanDiagnosticsDocument Diagnostics,
        ScanIndexCountsDocument IndexCounts);

    internal sealed record ScanConfigurationDocument(
        string IncludeSinkConfigPath,
        string IgnoreSinkConfigPath,
        ScanSerializerProfileDocument SerializerProfile,
        string FindingSort,
        string DispatchMode,
        int? MaxPathLength,
        string? OutputPath,
        string OutputFormat,
        string AssemblyResolutionMode);

    internal sealed record ScanSerializerProfileDocument(
        string Source,
        string Name,
        string? FilePath);

    internal sealed record ScanSinkCountsDocument(
        int Configured,
        int IgnoredPatterns,
        int Resolved,
        int Ignored,
        int Unresolved);

    internal sealed record ScanLoadCountsDocument(
        int InputRoots,
        int CandidateAssemblyFiles,
        int LoadedAssemblies,
        int LoadedCandidateAssemblies,
        int LoadedInputRootDependencies,
        int LoadedAssembliesFromInputRoots,
        int SkippedCandidateAssemblies,
        int UnresolvedReferences,
        int InferredInstalledRuntimeAssemblies,
        int HostRuntimeFallbackAssemblies,
        int GlobalAssemblyCacheAssemblies,
        int RuntimeConfigFiles,
        int InvalidRuntimeConfigFiles,
        int RuntimeConfigFilesWithoutUsableFrameworkRequests);

    internal sealed record ScanRuntimeDocument(
        IReadOnlyList<ScanRuntimeFrameworkDocument> RequestedFrameworks,
        IReadOnlyList<string> InferredInstalledRuntimeDirectories,
        IReadOnlyList<string> HostRuntimeSearchDirectories,
        IReadOnlyList<string> ResolverSearchDirectories,
        bool UsedHostRuntimeFallback,
        IReadOnlyList<string> Warnings);

    internal sealed record ScanRuntimeFrameworkDocument(
        string Name,
        string Version);

    internal sealed record ScanDiagnosticsDocument(
        IReadOnlyList<ScanCandidateAssemblyLoadFailureDocument> CandidateAssemblyLoadFailures,
        IReadOnlyList<ScanUnresolvedAssemblyReferenceDocument> UnresolvedAssemblyReferences,
        IReadOnlyList<ScanRuntimeConfigDiagnosticDocument> InvalidRuntimeConfigFiles,
        IReadOnlyList<ScanRuntimeConfigDiagnosticDocument> RuntimeConfigFilesWithoutUsableFrameworkRequests);

    internal sealed record ScanCandidateAssemblyLoadFailureDocument(
        string Path,
        string Message);

    internal sealed record ScanUnresolvedAssemblyReferenceDocument(
        string RequestingModulePath,
        string RequestingModuleDisplayName,
        string ReferenceDisplayName,
        string Message);

    internal sealed record ScanRuntimeConfigDiagnosticDocument(
        string Path,
        string Message);

    internal sealed record ScanIndexCountsDocument(
        int TypesProcessed,
        ScanClassCountsDocument Classes,
        int Interfaces,
        int ValueTypes,
        int MethodsIndexed,
        int PropertiesIndexed,
        int PublicInstancePropertySetters,
        int EventsIndexed,
        int GraphEdges,
        int OverrideRelationships,
        int InterfaceImplementations,
        int InstantiatedConcreteTypes);

    internal sealed record ScanClassCountsDocument(
        int Total,
        int Concrete,
        int Abstract);

    internal sealed record ScanFindingDocument(
        int SortIndex,
        string SinkDisplayName,
        ScanRootTypeDocument RootType,
        ScanRootAssemblyDocument RootAssembly,
        ScanTriggerDocument Trigger,
        int PathLength,
        IReadOnlyList<ScanPathStepDocument> Path);

    internal sealed record ScanRootTypeDocument(
        string FullName,
        string AssemblyQualifiedName);

    internal sealed record ScanRootAssemblyDocument(
        string? Path,
        string? AssemblyVersion,
        string? FileVersion,
        string Origin);

    internal sealed record ScanTriggerDocument(
        string Kind,
        string MethodDisplay,
        string? DeclaredOnTypeName,
        string? Annotation);

    internal sealed record ScanPathStepDocument(
        int StepIndex,
        string Kind,
        string SourceMethodDisplay,
        string TargetMethodDisplay,
        string? ReceiverTypeConstraint,
        bool PreservesCallerInstanceReceiver,
        IReadOnlyList<ScanPathArgumentDocument> Arguments);

    internal sealed record ScanPathArgumentDocument(
        int ArgumentIndex,
        bool IsProvablyConstant,
        string? ConstantKind,
        string? DisplayValue);
}
