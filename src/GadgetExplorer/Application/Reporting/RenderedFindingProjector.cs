/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Application.Reporting
{
    /// <summary>
    /// Projects grouped sink findings into the flattened rendered-finding order used by reports.
    /// </summary>
    internal static class RenderedFindingProjector
    {
        /// <summary>
        /// Projects an entire scan report into rendered findings using the requested report sort mode.
        /// </summary>
        public static IReadOnlyList<RenderedFinding> Project(ScanAnalysisReport report, FindingSortMode sortMode)
        {
            if (sortMode == FindingSortMode.ShortestPath)
            {
                RenderedFinding[] globallyRenderedFindings = [.. report.SinkEvaluationResults
                    .Where(sinkReport => sinkReport is { IsResolved: true, Findings.Count: > 0 })
                    .SelectMany(ExpandRenderedFindings)];

                return [.. globallyRenderedFindings
                    .OrderBy(finding => finding.Trigger.ReachabilityPath.Count)
                    .ThenBy(finding => finding.SinkDisplayName, StringComparer.Ordinal)
                    .ThenBy(finding => finding.RootClassAssemblyQualifiedName, StringComparer.Ordinal)
                    .ThenBy(finding => finding.Trigger.TriggerMethodDisplay, StringComparer.Ordinal)
                    .ThenBy(finding => finding.RootClassFullName, StringComparer.Ordinal)];
            }

            var renderedFindings = new List<RenderedFinding>();
            foreach (SinkEvaluationResult sinkReport in report.SinkEvaluationResults
                .Where(sinkReport => sinkReport is { IsResolved: true, Findings.Count: > 0 })
                .OrderBy(sinkReport => sinkReport.SinkDisplayName, StringComparer.Ordinal))
            {
                renderedFindings.AddRange(Project(sinkReport, sortMode));
            }

            return renderedFindings;
        }

        /// <summary>
        /// Projects one sink evaluation result into rendered findings using sink-local ordering.
        /// </summary>
        private static IReadOnlyList<RenderedFinding> Project(SinkEvaluationResult sinkReport, FindingSortMode sortMode)
        {
            RenderedFinding[] renderedFindings = [.. ExpandRenderedFindings(sinkReport)];
            IOrderedEnumerable<RenderedFinding> ordered = sortMode switch
            {
                FindingSortMode.TypeName => renderedFindings
                    .OrderBy(finding => finding.RootClassAssemblyQualifiedName, StringComparer.Ordinal)
                    .ThenBy(finding => finding.Trigger.ReachabilityPath.Count)
                    .ThenBy(finding => finding.Trigger.TriggerMethodDisplay, StringComparer.Ordinal)
                    .ThenBy(finding => finding.RootClassFullName, StringComparer.Ordinal),
                _ => renderedFindings
                    .OrderBy(finding => finding.Trigger.ReachabilityPath.Count)
                    .ThenBy(finding => finding.RootClassAssemblyQualifiedName, StringComparer.Ordinal)
                    .ThenBy(finding => finding.Trigger.TriggerMethodDisplay, StringComparer.Ordinal)
                    .ThenBy(finding => finding.RootClassFullName, StringComparer.Ordinal)
            };

            return [.. ordered];
        }

        private static IEnumerable<RenderedFinding> ExpandRenderedFindings(SinkEvaluationResult sinkReport)
            => sinkReport.Findings.SelectMany(finding => finding.TriggerResults.Select(trigger => new RenderedFinding(
                sinkReport.SinkDisplayName,
                finding.RootClassId,
                finding.RootClassFullName,
                finding.RootClassAssemblyQualifiedName,
                trigger)));

        internal sealed record RenderedFinding(
            string SinkDisplayName,
            TypeId RootClassId,
            string RootClassFullName,
            string RootClassAssemblyQualifiedName,
            TriggerResult Trigger);
    }
}
