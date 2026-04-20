/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis
{
    /// <summary>
    /// Finds serializer triggers that can reach configured sink methods.
    /// </summary>
    public static partial class SinkAnalyzer
    {
        /// <summary>
        /// Analyzes the indexed graph against the configured sinks.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="sinkDefinitions">The configured sink definitions.</param>
        /// <param name="ignoreSinkDefinitions">The configured ignore-sink definitions.</param>
        /// <param name="profile">The serializer profile to apply.</param>
        /// <param name="maxPathLength">The optional maximum trigger-to-sink path length.</param>
        /// <param name="progress">The optional progress callback.</param>
        public static ScanAnalysisReport Analyze(
            AnalysisIndex index,
            IReadOnlyList<SinkDefinition> sinkDefinitions,
            IReadOnlyList<SinkDefinition> ignoreSinkDefinitions,
            SerializerProfile profile,
            int? maxPathLength = null,
            Action<string>? progress = null)
        {
            if (sinkDefinitions.Count == 0)
            {
                throw new InvalidOperationException("At least one sink definition is required.");
            }

            progress?.Invoke("Computing eligible root classes.");
            IReadOnlySet<TypeId> eligibleRootClassIds = ComputeEligibleRootClassIds(index, profile);
            progress?.Invoke($"Eligible root class filter retained {eligibleRootClassIds.Count} type(s).");

            progress?.Invoke("Resolving configured sink definitions.");
            IReadOnlyList<ResolvedSinkMethods> resolvedSinks = ResolveConfiguredSinks(index, sinkDefinitions, ignoreSinkDefinitions);
            IReadOnlySet<MethodId> allResolvedSinkMethodIds = resolvedSinks
                .SelectMany(resolvedSink => resolvedSink.ActiveMethodIds)
                .ToHashSet();

            var sinkReports = new List<SinkEvaluationResult>(resolvedSinks.Count);
            for (int i = 0; i < resolvedSinks.Count; i++)
            {
                ResolvedSinkMethods resolvedSink = resolvedSinks[i];
                progress?.Invoke($"Analyzing sink {i + 1}/{resolvedSinks.Count}: {resolvedSink.SinkDisplayName}");
                sinkReports.Add(AnalyzeResolvedSink(
                    index,
                    resolvedSink,
                    allResolvedSinkMethodIds,
                    ignoreSinkDefinitions,
                    eligibleRootClassIds,
                    profile,
                    maxPathLength));
            }

            SinkEvaluationResult[] orderedReports = [.. sinkReports.OrderBy(report => report.SinkDisplayName, StringComparer.Ordinal)];

            progress?.Invoke("Sink analysis complete.");

            return new ScanAnalysisReport(orderedReports);
        }

        /// <summary>
        /// Resolves all configured sinks into explicit loaded-method match sets.
        /// </summary>
        private static IReadOnlyList<ResolvedSinkMethods> ResolveConfiguredSinks(
            AnalysisIndex index,
            IReadOnlyList<SinkDefinition> sinkDefinitions,
            IReadOnlyList<SinkDefinition> ignoreSinkDefinitions)
        {
            IReadOnlyDictionary<SinkMethodLookupKey, MethodId[]> exactManagedPairLookup = BuildExactManagedSinkPairLookup(index);
            return [.. sinkDefinitions.Select(sinkDefinition => ResolveConfiguredSink(index, sinkDefinition, ignoreSinkDefinitions, exactManagedPairLookup))];
        }

        /// <summary>
        /// Resolves a single configured sink into its loaded and active method matches.
        /// </summary>
        private static ResolvedSinkMethods ResolveConfiguredSink(
            AnalysisIndex index,
            SinkDefinition sinkDefinition,
            IReadOnlyList<SinkDefinition> ignoreSinkDefinitions,
            IReadOnlyDictionary<SinkMethodLookupKey, MethodId[]> exactManagedPairLookup)
        {
            string sinkDisplayName = FormatSink(sinkDefinition);
            IReadOnlyList<MethodId> allMethodIds = FindMatchingSinkMethods(index, sinkDefinition, exactManagedPairLookup);
            MethodId[] activeMethodIds = [.. allMethodIds.Where(methodId => !IsIgnored(index.GetMethod(methodId), ignoreSinkDefinitions))];
            return new ResolvedSinkMethods(sinkDefinition, sinkDisplayName, allMethodIds, activeMethodIds);
        }

        /// <summary>
        /// Analyzes a single resolved sink definition.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="resolvedSink">The resolved sink methods to analyze.</param>
        /// <param name="allResolvedSinkMethodIds">The union of all resolved sink method identifiers across configured sinks.</param>
        /// <param name="ignoreSinkDefinitions">The configured ignore-sink definitions.</param>
        /// <param name="eligibleRootClassIds">The eligible root class identifiers.</param>
        /// <param name="profile">The serializer profile to apply.</param>
        /// <param name="maxPathLength">The optional maximum trigger-to-sink path length.</param>
        private static SinkEvaluationResult AnalyzeResolvedSink(
            AnalysisIndex index,
            ResolvedSinkMethods resolvedSink,
            IReadOnlySet<MethodId> allResolvedSinkMethodIds,
            IReadOnlyList<SinkDefinition> ignoreSinkDefinitions,
            IReadOnlySet<TypeId> eligibleRootClassIds,
            SerializerProfile profile,
            int? maxPathLength)
        {
            if (!resolvedSink.HasLoadedMatches)
            {
                return new SinkEvaluationResult(
                    resolvedSink.SinkDefinition,
                    [],
                    resolvedSink.SinkDisplayName,
                    false,
                    false,
                    $"No matching methods for '{resolvedSink.SinkDisplayName}' were present in the loaded assembly set.",
                    []);
            }

            if (!resolvedSink.HasActiveMatches)
            {
                return new SinkEvaluationResult(
                    resolvedSink.SinkDefinition,
                    [],
                    resolvedSink.SinkDisplayName,
                    false,
                    true,
                    $"All matches for '{resolvedSink.SinkDisplayName}' were ignored by the ignore-sinks configuration.",
                    []);
            }

            ReverseSinkSliceResult reverseSinkSlice = ComputeReverseSinkSlice(
                index,
                resolvedSink.SinkDefinition,
                resolvedSink.ActiveMethodIds,
                allResolvedSinkMethodIds,
                ignoreSinkDefinitions,
                maxPathLength);

            var rootClassesByTriggerState = new Dictionary<SliceState, IReadOnlyList<TypeRecord>>();
            IReadOnlyList<TriggerStateCandidate> positiveTriggerStates = FindPositiveTriggerStates(
                index,
                reverseSinkSlice.SliceStates,
                eligibleRootClassIds,
                profile,
                rootClassesByTriggerState);
            IReadOnlySet<MethodId> activeSinkMethodIds = resolvedSink.ActiveMethodIds.ToHashSet();
            TriggerFindingCandidate[] triggerFindings = [.. positiveTriggerStates
                .SelectMany(triggerState => BuildTriggerFindings(
                    index,
                    activeSinkMethodIds,
                    triggerState,
                    reverseSinkSlice.NextStepByState,
                    eligibleRootClassIds,
                    profile,
                    rootClassesByTriggerState))
                .DistinctBy(finding => finding.Identity)
                .OrderBy(finding => finding.RootClassFullName, StringComparer.Ordinal)
                .ThenBy(finding => finding.Trigger.TriggerMethodDisplay, StringComparer.Ordinal)];
            IReadOnlyList<ClassFinding> findings = BuildClassFindings(triggerFindings);

            return new SinkEvaluationResult(
                resolvedSink.SinkDefinition,
                resolvedSink.ActiveMethodIds,
                resolvedSink.SinkDisplayName,
                true,
                false,
                null,
                findings);
        }

        /// <summary>
        /// Groups flattened trigger findings back into root-class findings for the analysis result.
        /// </summary>
        private static IReadOnlyList<ClassFinding> BuildClassFindings(IReadOnlyList<TriggerFindingCandidate> triggerFindings)
            => [.. triggerFindings
                .GroupBy(finding => finding.RootClassId)
                .Select(group => new ClassFinding(
                    group.Key,
                    group.First().RootClassFullName,
                    group.First().RootClassAssemblyQualifiedName,
                    [.. group.Select(finding => finding.Trigger).OrderBy(trigger => trigger.TriggerMethodDisplay, StringComparer.Ordinal)]))
                .OrderBy(finding => finding.RootClassFullName, StringComparer.Ordinal)];

        /// <summary>
        /// Stores the resolved loaded-method matches for a configured sink definition.
        /// </summary>
        private sealed record ResolvedSinkMethods(
            SinkDefinition SinkDefinition,
            string SinkDisplayName,
            IReadOnlyList<MethodId> AllMethodIds,
            IReadOnlyList<MethodId> ActiveMethodIds)
        {
            public bool HasLoadedMatches => AllMethodIds.Count > 0;

            public bool HasActiveMatches => ActiveMethodIds.Count > 0;
        }
    }
}

