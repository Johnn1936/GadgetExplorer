/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [Collection(SmokeSampleCollection.Name)]
    public sealed class SinkAnalyzerBehaviorTests(SmokeSampleAnalysisFixture fixture) : SmokeSampleAnalysisTestBase
    {
        [Fact]
        public void Analyze_rejects_empty_sink_definition_lists()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                SinkAnalyzer.Analyze(fixture.Index, [], [], JsonDotNet));

            Assert.Equal("At least one sink definition is required.", ex.Message);
        }

        [Fact]
        public void Unresolved_sink_produces_unresolved_sink_report()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("DoesNotExist", "Nope")],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.False(sinkReport.IsResolved);
            Assert.False(sinkReport.IsIgnored);
            Assert.Empty(sinkReport.SinkMethodIds);
            Assert.Empty(sinkReport.Findings);
            Assert.Contains("No matching methods", sinkReport.ResolutionNote, StringComparison.Ordinal);
        }

        [Fact]
        public void Fully_ignored_sink_produces_ignored_sink_report()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("Helper", "InvokeSink")],
                [new SinkDefinition("Helper", "InvokeSink")],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.False(sinkReport.IsResolved);
            Assert.True(sinkReport.IsIgnored);
            Assert.Empty(sinkReport.Findings);
            Assert.Contains("were ignored by the ignore-sinks configuration", sinkReport.ResolutionNote, StringComparison.Ordinal);
        }

        [Fact]
        public void Declaring_type_matching_accepts_simple_type_names()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("MySpecialObject", "SayHello")],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.True(sinkReport.IsResolved);
            Assert.Contains(sinkReport.Findings, finding => finding.RootClassFullName == "ConstructorPositive");
        }

        [Fact]
        public void Declaring_type_matching_rejects_assembly_qualified_type_names()
        {
            string simpleAssemblyQualifiedName = $"MySpecialObject, {typeof(MySpecialObject).Assembly.GetName().Name}";

            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [
                    new SinkDefinition(simpleAssemblyQualifiedName, "SayHello"),
                    new SinkDefinition(typeof(MySpecialObject).AssemblyQualifiedName!, "SayHello")
                ],
                [],
                JsonDotNet);

            Assert.All(
                report.SinkEvaluationResults,
                sinkReport =>
                {
                    Assert.False(sinkReport.IsResolved);
                    Assert.Empty(sinkReport.SinkMethodIds);
                    Assert.Empty(sinkReport.Findings);
                    Assert.Contains("No matching methods", sinkReport.ResolutionNote, StringComparison.Ordinal);
                });
        }
    }

}
