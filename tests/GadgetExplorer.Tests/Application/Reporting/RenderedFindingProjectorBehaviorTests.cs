/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Application.Reporting
{
    public sealed class RenderedFindingProjectorBehaviorTests
    {
        [Fact]
        public void Project_shortest_path_orders_findings_globally_by_path_length_then_sink_name()
        {
            var report = new ScanAnalysisReport(
            [
                CreateSinkEvaluationResult(
                    "Zulu::Sink",
                    CreateClassFinding(2, "ZuluRoot", "ZuluRoot, TestAssembly", CreateTrigger(20, "ZuluRoot::.ctor()", 201))),
                CreateSinkEvaluationResult(
                    "Alpha::Sink",
                    CreateClassFinding(1, "AlphaRoot", "AlphaRoot, TestAssembly", CreateTrigger(10, "AlphaRoot::.ctor()", 101))),
                CreateSinkEvaluationResult(
                    "Beta::Sink",
                    CreateClassFinding(3, "BetaRoot", "BetaRoot, TestAssembly", CreateTrigger(30, "BetaRoot::.ctor()", 301, 302)))
            ]);

            IReadOnlyList<RenderedFindingProjector.RenderedFinding> renderedFindings = RenderedFindingProjector.Project(report, FindingSortMode.ShortestPath);

            Assert.Collection(
                renderedFindings,
                finding =>
                {
                    Assert.Equal("Alpha::Sink", finding.SinkDisplayName);
                    Assert.Equal("AlphaRoot", finding.RootClassFullName);
                    Assert.Single(finding.Trigger.ReachabilityPath);
                },
                finding =>
                {
                    Assert.Equal("Zulu::Sink", finding.SinkDisplayName);
                    Assert.Equal("ZuluRoot", finding.RootClassFullName);
                    Assert.Single(finding.Trigger.ReachabilityPath);
                },
                finding =>
                {
                    Assert.Equal("Beta::Sink", finding.SinkDisplayName);
                    Assert.Equal("BetaRoot", finding.RootClassFullName);
                    Assert.Equal(2, finding.Trigger.ReachabilityPath.Count);
                });
        }

        [Fact]
        public void Project_per_sink_shortest_path_keeps_sink_groups_and_expands_triggers()
        {
            var report = new ScanAnalysisReport(
            [
                CreateSinkEvaluationResult(
                    "Alpha::Sink",
                    CreateClassFinding(1, "AlphaRoot", "AlphaRoot, TestAssembly", CreateTrigger(10, "AlphaRoot::.ctor()", 101, 102, 103))),
                CreateSinkEvaluationResult(
                    "Beta::Sink",
                    CreateClassFinding(
                        2,
                        "BetaRoot",
                        "BetaRoot, TestAssembly",
                        CreateTrigger(20, "BetaRoot::Longer()", 201, 202),
                        CreateTrigger(21, "BetaRoot::Shorter()", 211)))
            ]);

            IReadOnlyList<RenderedFindingProjector.RenderedFinding> renderedFindings = RenderedFindingProjector.Project(report, FindingSortMode.PerSinkShortestPath);

            Assert.Collection(
                renderedFindings,
                finding =>
                {
                    Assert.Equal("Alpha::Sink", finding.SinkDisplayName);
                    Assert.Equal("AlphaRoot", finding.RootClassFullName);
                    Assert.Equal(3, finding.Trigger.ReachabilityPath.Count);
                },
                finding =>
                {
                    Assert.Equal("Beta::Sink", finding.SinkDisplayName);
                    Assert.Equal("BetaRoot", finding.RootClassFullName);
                    Assert.Equal("BetaRoot::Shorter()", finding.Trigger.TriggerMethodDisplay);
                    Assert.Single(finding.Trigger.ReachabilityPath);
                },
                finding =>
                {
                    Assert.Equal("Beta::Sink", finding.SinkDisplayName);
                    Assert.Equal("BetaRoot", finding.RootClassFullName);
                    Assert.Equal("BetaRoot::Longer()", finding.Trigger.TriggerMethodDisplay);
                    Assert.Equal(2, finding.Trigger.ReachabilityPath.Count);
                });
        }

        private static SinkEvaluationResult CreateSinkEvaluationResult(string sinkDisplayName, params ClassFinding[] findings)
            => new(
                new SinkDefinition("TestSinkOwner", "TestSinkMethod"),
                [],
                sinkDisplayName,
                true,
                false,
                null,
                findings);

        private static ClassFinding CreateClassFinding(int rootClassId, string rootClassFullName, string rootClassAssemblyQualifiedName, params TriggerResult[] triggers)
            => new(new TypeId(rootClassId), rootClassFullName, rootClassAssemblyQualifiedName, triggers);

        private static TriggerResult CreateTrigger(int triggerMethodId, string triggerMethodDisplay, params int[] edgeIds)
            => new(
                new MethodId(triggerMethodId),
                triggerMethodDisplay,
                TriggerKind.Constructor,
                [.. edgeIds.Select((edgeId, index) => new EdgeRecord(
                    new EdgeId(edgeId),
                    new MethodId(triggerMethodId + index),
                    new MethodId(triggerMethodId + index + 1000),
                    EdgeKind.DirectCall))],
                null,
                null);
    }
}
