/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Diagnostics;
using dnlib.DotNet;

namespace GadgetExplorer.Application.Reporting
{
    internal static class ScanReportDocumentProjector
    {
        private const int SchemaVersion = 2;

        public static ScanReportDocument Project(ScanExecutionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            int resolvedSinkCount = result.Report.SinkEvaluationResults.Count(sink => sink.IsResolved);
            int ignoredSinkCount = result.Report.SinkEvaluationResults.Count(sink => sink.IsIgnored);
            int unresolvedSinkCount = result.Report.SinkEvaluationResults.Count - resolvedSinkCount - ignoredSinkCount;

            int inputRootAssemblyCount = result.LoadResult.AssemblyOriginsByPath.Values.Count(origin => origin == LoadedAssemblyOrigin.InputRoot);
            int loadedCandidateAssemblyCount = result.LoadResult.AssemblyOriginsByPath.Keys.Count(path =>
                result.LoadResult.LoadPlan.CandidateAssemblyFiles.Contains(path, StringComparer.OrdinalIgnoreCase));
            int loadedInputRootDependencyCount = inputRootAssemblyCount - loadedCandidateAssemblyCount;
            int inferredRuntimeAssemblyCount = result.LoadResult.AssemblyOriginsByPath.Values.Count(origin => origin == LoadedAssemblyOrigin.InferredInstalledRuntime);
            int hostFallbackAssemblyCount = result.LoadResult.AssemblyOriginsByPath.Values.Count(origin => origin == LoadedAssemblyOrigin.HostRuntimeFallback);
            int gacAssemblyCount = result.LoadResult.AssemblyOriginsByPath.Values.Count(origin => origin == LoadedAssemblyOrigin.GlobalAssemblyCache);

            IReadOnlyList<RenderedFindingProjector.RenderedFinding> renderedFindings = RenderedFindingProjector.Project(result.Report, result.Options.SortMode);

            return new ScanReportDocument(
                SchemaVersion,
                ProjectRecon(
                    result,
                    resolvedSinkCount,
                    ignoredSinkCount,
                    unresolvedSinkCount,
                    inputRootAssemblyCount,
                    loadedCandidateAssemblyCount,
                    loadedInputRootDependencyCount,
                    inferredRuntimeAssemblyCount,
                    hostFallbackAssemblyCount,
                    gacAssemblyCount),
                ProjectFindings(renderedFindings, result.Index, result.LoadResult));
        }

        private static ScanReconDocument ProjectRecon(
            ScanExecutionResult result,
            int resolvedSinkCount,
            int ignoredSinkCount,
            int unresolvedSinkCount,
            int inputRootAssemblyCount,
            int loadedCandidateAssemblyCount,
            int loadedInputRootDependencyCount,
            int inferredRuntimeAssemblyCount,
            int hostFallbackAssemblyCount,
            int gacAssemblyCount)
        {
            return new ScanReconDocument(
                result.Options.CommandLineArguments is null ? [] : [.. result.Options.CommandLineArguments],
                [.. result.Options.AssemblyInputs.Select(Path.GetFullPath)],
                new ScanConfigurationDocument(
                    result.Options.ResolvedIncludeSinkConfigPath,
                    result.Options.ResolvedIgnoreSinkConfigPath,
                    new ScanSerializerProfileDocument(
                        ScanReportValueFormatter.FormatSerializerProfileSource(result.Options.ProfileFilePath is not null),
                        result.SerializerProfile.Name,
                        result.Options.ResolvedProfileFilePath),
                    result.Options.SortModeDisplayText,
                    result.Options.InterfaceExpansionModeDisplayText,
                    result.Options.MaxPathLength,
                    result.Options.ResolvedOutputPath,
                    result.Options.OutputFormatDisplayText,
                    ScanReportValueFormatter.FormatAssemblyResolutionMode(result.Options.AssemblyResolutionMode)),
                new ScanSinkCountsDocument(
                    result.SinkDefinitions.Count,
                    result.IgnoreSinkDefinitions.Count,
                    resolvedSinkCount,
                    ignoredSinkCount,
                    unresolvedSinkCount),
                new ScanLoadCountsDocument(
                    result.LoadResult.LoadPlan.InputRoots.Count,
                    result.LoadResult.LoadPlan.CandidateAssemblyFiles.Count,
                    result.LoadResult.Modules.Count,
                    loadedCandidateAssemblyCount,
                    loadedInputRootDependencyCount,
                    inputRootAssemblyCount,
                    result.LoadResult.Diagnostics.CandidateAssemblyLoadFailureCount,
                    result.LoadResult.Diagnostics.UnresolvedReferenceCount,
                    inferredRuntimeAssemblyCount,
                    hostFallbackAssemblyCount,
                    gacAssemblyCount,
                    result.LoadResult.LoadPlan.RuntimeConfigPaths.Count,
                    result.LoadResult.Diagnostics.InvalidRuntimeConfigFileCount,
                    result.LoadResult.Diagnostics.RuntimeConfigFileWithoutUsableFrameworkRequestCount),
                new ScanRuntimeDocument(
                    [.. result.LoadResult.LoadPlan.RequestedFrameworks.Select(framework => new ScanRuntimeFrameworkDocument(
                        framework.Name,
                        framework.Version.ToString()))],
                    [.. result.LoadResult.LoadPlan.InferredInstalledRuntimeDirectories],
                    [.. result.LoadResult.LoadPlan.HostRuntimeSearchDirectories],
                    [.. result.LoadResult.LoadPlan.ResolverSearchDirectories],
                    result.LoadResult.LoadPlan.UsedHostRuntimeFallback,
                    [.. result.LoadResult.LoadPlan.Warnings]),
                new ScanDiagnosticsDocument(
                    [.. result.LoadResult.Diagnostics.CandidateAssemblyLoadFailures.Select(failure => new ScanCandidateAssemblyLoadFailureDocument(
                        failure.Path,
                        failure.Message))],
                    [.. result.LoadResult.Diagnostics.UnresolvedReferences.Select(reference => new ScanUnresolvedAssemblyReferenceDocument(
                        reference.RequestingModulePath,
                        reference.RequestingModuleDisplayName,
                        reference.ReferenceDisplayName,
                        reference.Message))],
                    [.. result.LoadResult.Diagnostics.InvalidRuntimeConfigFiles.Select(diagnostic => new ScanRuntimeConfigDiagnosticDocument(
                        diagnostic.Path,
                        diagnostic.Message))],
                    [.. result.LoadResult.Diagnostics.RuntimeConfigFilesWithoutUsableFrameworkRequests.Select(diagnostic => new ScanRuntimeConfigDiagnosticDocument(
                        diagnostic.Path,
                        diagnostic.Message))]),
                new ScanIndexCountsDocument(
                    result.Index.Types.Count,
                    new ScanClassCountsDocument(
                        result.Index.ClassTypeCount,
                        result.Index.ConcreteClassTypeCount,
                        result.Index.AbstractClassTypeCount),
                    result.Index.InterfaceTypeCount,
                    result.Index.ValueTypeCount,
                    result.Index.Methods.Count,
                    result.Index.PropertyCount,
                    result.Index.PublicInstancePropertySetterCount,
                    result.Index.Events.Count,
                    result.Index.Edges.Count,
                    result.Index.OverrideRelationshipCount,
                    result.Index.InterfaceImplementationRelationshipCount,
                    result.Index.InstantiatedTypeCount));
        }

        private static IReadOnlyList<ScanFindingDocument> ProjectFindings(
            IReadOnlyList<RenderedFindingProjector.RenderedFinding> renderedFindings,
            AnalysisIndex index,
            AssemblyLoadResult loadResult)
        {
            return [.. renderedFindings.Select((finding, sortIndex) =>
            {
                TypeRecord rootClass = index.GetType(finding.RootClassId);
                string? rootAssemblyPath = TryGetModuleLocation(rootClass.Module);
                string? rootAssemblyVersion = rootClass.Module.Assembly?.Version?.ToString();
                string? rootFileVersion = GetFileVersion(rootAssemblyPath);
                string rootAssemblyOrigin = ScanReportValueFormatter.FormatLoadedAssemblyOrigin(loadResult.GetAssemblyOrigin(rootAssemblyPath));
                IReadOnlyList<ScanPathStepDocument> path = ProjectPathSteps(finding.Trigger.ReachabilityPath, index);

                return new ScanFindingDocument(
                    sortIndex,
                    finding.SinkDisplayName,
                    new ScanRootTypeDocument(
                        finding.RootClassFullName,
                        finding.RootClassAssemblyQualifiedName),
                    new ScanRootAssemblyDocument(
                        rootAssemblyPath,
                        rootAssemblyVersion,
                        rootFileVersion,
                        rootAssemblyOrigin),
                    new ScanTriggerDocument(
                        ScanReportValueFormatter.FormatTriggerKind(finding.Trigger.TriggerKind),
                        finding.Trigger.TriggerMethodDisplay,
                        finding.Trigger.TriggerDeclaredOnTypeName,
                        finding.Trigger.TriggerAnnotation),
                    path.Count,
                    path);
            })];
        }

        private static IReadOnlyList<ScanPathStepDocument> ProjectPathSteps(
            IReadOnlyList<EdgeRecord> reachabilityPath,
            AnalysisIndex index)
        {
            return [.. reachabilityPath.Select((edge, stepIndex) =>
            {
                MethodRecord sourceMethod = index.GetMethod(edge.SourceId);
                MethodRecord targetMethod = index.GetMethod(edge.TargetId);
                string? receiverTypeConstraint = edge.ReceiverTypeConstraintId is null
                    ? null
                    : index.GetType(edge.ReceiverTypeConstraintId.Value).FullName;

                return new ScanPathStepDocument(
                    stepIndex,
                    ScanReportValueFormatter.FormatEdgeKind(edge.Kind),
                    sourceMethod.DisplayName,
                    targetMethod.DisplayName,
                    receiverTypeConstraint,
                    edge.PreservesCallerInstanceReceiver,
                    [.. edge.ArgumentSummaries
                        .OrderBy(summary => summary.ArgumentIndex)
                        .Select(summary => new ScanPathArgumentDocument(
                            summary.ArgumentIndex,
                            summary.IsProvablyConstant,
                            summary.ConstantKind is null
                                ? null
                                : ScanReportValueFormatter.FormatConstantValueKind(summary.ConstantKind.Value),
                            summary.DisplayValue))]);
            })];
        }

        private static string? TryGetModuleLocation(ModuleDefMD module)
        {
            try
            {
                string? location = module.Location;
                if (string.IsNullOrWhiteSpace(location))
                {
                    return null;
                }

                return Path.IsPathRooted(location)
                    ? location
                    : Path.GetFullPath(location);
            }
            catch
            {
                return null;
            }
        }

        private static string? GetFileVersion(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return FileVersionInfo.GetVersionInfo(path).FileVersion;
            }
            catch
            {
                return null;
            }
        }
    }
}
