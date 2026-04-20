/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [Collection(SmokeSampleCollection.Name)]
    public sealed class SinkResolutionBehaviorTests(SmokeSampleAnalysisFixture fixture) : SmokeSampleAnalysisTestBase
    {
        [Fact]
        public void Exact_sink_overload_matching_limits_results_to_the_requested_signature()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("OverloadedSinkTarget", "Invoke", ["System.String"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "OverloadedSinkStringPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("OverloadedSinkStringPositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.DoesNotContain(sinkReport.Findings, entry => entry.RootClassFullName == "OverloadedSinkParameterlessNegative");
        }

        [Fact]
        public void Exact_sink_overload_matching_distinguishes_closed_generic_parameter_types()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("ClosedGenericSinkTarget", "Invoke", ["System.Collections.Generic.List`1<System.String>"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "ClosedGenericSinkStringPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("ClosedGenericSinkStringPositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "ClosedGenericSinkTarget::Invoke(System.Collections.Generic.List`1<System.String>)"));

            Assert.DoesNotContain(sinkReport.Findings, entry => entry.RootClassFullName == "ClosedGenericSinkIntNegative");
        }

        [Fact]
        public void Reverse_sink_slice_stops_when_it_reaches_a_different_configured_sink()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [
                    new SinkDefinition("MySpecialObject", "SayHello"),
                    new SinkDefinition("Helper", "InvokeSink")
                ],
                [],
                JsonDotNet);

            var sayHelloReport = Assert.Single(report.SinkEvaluationResults, sinkReport => sinkReport.SinkDefinition.DeclaringType == "MySpecialObject");
            Assert.DoesNotContain(sayHelloReport.Findings, finding => finding.RootClassFullName == "ConstructorPositive");

            var invokeSinkEvaluationResult = Assert.Single(report.SinkEvaluationResults, sinkReport => sinkReport.SinkDefinition.DeclaringType == "Helper");
            var finding = Assert.Single(invokeSinkEvaluationResult.Findings, entry => entry.RootClassFullName == "ConstructorPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("ConstructorPositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"));
        }
    }

}
