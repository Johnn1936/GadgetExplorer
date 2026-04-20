/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [Collection(SmokeSampleCollection.Name)]
    public sealed class GraphTraversalBehaviorTests(SmokeSampleAnalysisFixture fixture) : SmokeSampleAnalysisTestBase
    {
        [Fact]
        public void Finds_virtual_dispatch_paths()
        {
            var trigger = fixture.GetSingleTrigger("VirtualCtorPositive");

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.VirtualDispatch, "SpecialVirtualWorker::Run()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Uses_concrete_receiver_type_to_narrow_virtual_dispatch()
        {
            var trigger = fixture.GetSingleTrigger("ReceiverAwareVirtualPositive");

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.VirtualDispatch, "LateVirtualWorker::Run()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Finds_virtual_dispatch_path_through_base_method_without_instantiation_pruning()
        {
            var trigger = fixture.GetSingleTrigger("SetterRefreshDerivedPositive");

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "SetterRefreshBase::Refresh()"),
                edge => AssertEdge(fixture, edge, EdgeKind.VirtualDispatch, "SetterRefreshDerivedPositive::BeginQuery()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Rejects_sibling_virtual_dispatch_paths_that_do_not_share_the_same_receiver()
        {
            Assert.False(fixture.HasFinding("SetterRefreshDerivedNegative"));
        }

        [Fact]
        public void Finds_one_hop_interface_argument_relays_in_broad_mode()
        {
            AnalysisIndex broadIndex = fixture.GetIndex(InterfaceExpansionMode.Broad);
            TriggerResult trigger = fixture.GetSingleTrigger("InterfaceCtorPositive", mode: InterfaceExpansionMode.Broad);

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "InterfaceBridge::Dispatch(IHelloStep)"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.InterfaceDispatch, "InterfaceHelloStep::Execute()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Finds_generic_interface_dispatch_path_in_broad_mode()
        {
            AnalysisIndex broadIndex = fixture.GetIndex(InterfaceExpansionMode.Broad);
            TriggerResult trigger = fixture.GetSingleTrigger("GenericSetterPositive", mode: InterfaceExpansionMode.Broad);

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "GenericRelay`1::Relay(TWorker)"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.InterfaceDispatch, "GenericHelloWorker::DoWork()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Preserves_exact_closed_generic_interface_instantiations_in_strict_mode()
        {
            var trigger = fixture.GetSingleTrigger("ClosedGenericEnumeratorConstraintPositive");

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.InterfaceDispatch, "SinkingCompatibleEnumeratorProbe::MoveNext()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Rejects_mismatched_closed_generic_interface_instantiations_in_strict_mode()
        {
            Assert.False(fixture.HasFinding("ClosedGenericEnumeratorConstraintNegative"));
        }

        [Fact]
        public void Broad_mode_keeps_closed_generic_interface_mismatch_exploration_opt_in()
        {
            AnalysisIndex broadIndex = fixture.GetIndex(InterfaceExpansionMode.Broad);
            TriggerResult trigger = fixture.GetSingleTrigger("ClosedGenericEnumeratorConstraintNegative", mode: InterfaceExpansionMode.Broad);

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(broadIndex, edge, EdgeKind.InterfaceDispatch, "SinkingIncompatibleMismatchedEnumeratorProbe::MoveNext()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Strict_mode_rejects_object_only_interface_cast_constraints()
        {
            Assert.False(fixture.HasFinding("ObjectOnlyCastConstraintNegative"));
        }

        [Fact]
        public void Broad_mode_keeps_object_only_interface_cast_exploration_opt_in()
        {
            AnalysisIndex broadIndex = fixture.GetIndex(InterfaceExpansionMode.Broad);
            TriggerResult trigger = fixture.GetSingleTrigger("ObjectOnlyCastConstraintNegative", mode: InterfaceExpansionMode.Broad);

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(broadIndex, edge, EdgeKind.InterfaceDispatch, "SinkingObjectOnlyCastProbe::Execute()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Finds_nested_call_chain_path_in_broad_mode()
        {
            AnalysisIndex broadIndex = fixture.GetIndex(InterfaceExpansionMode.Broad);
            TriggerResult trigger = fixture.GetSingleTrigger("NestedConstructorPositive", mode: InterfaceExpansionMode.Broad);

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "LayerOne::Start()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "LayerTwo::Continue(IHelloStep)"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "LayerThree::Finish(IHelloStep)"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "InterfaceBridge::Dispatch(IHelloStep)"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.InterfaceDispatch, "InterfaceHelloStep::Execute()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Rejects_unreachable_sink_call_after_unconditional_branch()
        {
            Assert.False(fixture.HasFinding("GotoSkippedSinkNegative"));
        }

        [Fact]
        public void Finds_sink_after_lock_finally_continuation()
        {
            var trigger = fixture.GetSingleTrigger("LockFinallyContinuationPositive");

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Finds_sink_after_out_parameter_branch_population()
        {
            var trigger = fixture.GetSingleTrigger("OutArrayBranchPositive");

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Max_path_length_filters_out_longer_nested_call_chains()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.GetIndex(InterfaceExpansionMode.Broad),
                [new SinkDefinition("MySpecialObject", "SayHello")],
                [],
                JsonDotNet,
                maxPathLength: 6);

            var finding = report.SinkEvaluationResults.Single().Findings.SingleOrDefault(candidate => candidate.RootClassFullName == "NestedConstructorPositive");

            Assert.Null(finding);
        }

        [Fact]
        public void Max_path_length_retains_nested_call_chain_when_limit_matches_path_length()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.GetIndex(InterfaceExpansionMode.Broad),
                [new SinkDefinition("MySpecialObject", "SayHello")],
                [],
                JsonDotNet,
                maxPathLength: 7);

            var finding = report.SinkEvaluationResults.Single().Findings.Single(candidate => candidate.RootClassFullName == "NestedConstructorPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(7, trigger.ReachabilityPath.Count);
        }

        [Fact]
        public void Broad_mode_keeps_virtual_fallback_opt_in()
        {
            Assert.False(fixture.HasFinding("BroadVirtualOpaqueNegative"));

            AnalysisIndex broadIndex = fixture.GetIndex(InterfaceExpansionMode.Broad);
            TriggerResult broadTrigger = fixture.GetSingleTrigger("BroadVirtualOpaqueNegative", mode: InterfaceExpansionMode.Broad);

            Assert.Collection(
                broadTrigger.ReachabilityPath,
                edge => AssertEdge(broadIndex, edge, EdgeKind.VirtualDispatch, "SinkingOpaqueVirtualWorker::Run()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(broadIndex, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Off_mode_preserves_virtual_dispatch_after_successful_abstract_class_cast()
        {
            AnalysisIndex offIndex = fixture.GetIndex(InterfaceExpansionMode.Off);
            TriggerResult trigger = fixture.GetSingleTrigger("AbstractCastVirtualPositive", mode: InterfaceExpansionMode.Off);

            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(offIndex, edge, EdgeKind.VirtualDispatch, "SinkingCastTrackedVirtualWorker::Run()"),
                edge => AssertEdge(offIndex, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(offIndex, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }
    }

}
