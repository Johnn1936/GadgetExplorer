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
        public void Resolves_framework_file_delete_call_from_finalizer_to_loaded_sink()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.IO.File", "Delete")],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "FinalizerFileDeletePositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Finalizer, trigger.TriggerKind);
            Assert.Equal("FinalizerFileDeletePositive::Finalize()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.IO.File::Delete(System.String)"));
        }

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
        public void Exact_sink_overload_matching_supports_assembly_load_from_string()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Reflection.Assembly", "LoadFrom", ["System.String"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "AssemblyLoadFromPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("AssemblyLoadFromPositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.Reflection.Assembly::LoadFrom(System.String)"));
        }

        [Fact]
        public void Sink_can_ignore_constant_string_arguments_for_configured_parameter_positions()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Reflection.Assembly", "LoadFrom", [new SinkParameterDefinition("System.String", true)])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.DoesNotContain(sinkReport.Findings, entry => entry.RootClassFullName == "AssemblyLoadFromPositive");

            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "AssemblyLoadFromVariablePositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("AssemblyLoadFromVariablePositive::set_PluginPath(System.String)", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.Reflection.Assembly::LoadFrom(System.String)"));
        }

        [Fact]
        public void Sink_can_ignore_constant_type_arguments_for_configured_parameter_positions()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Activator", "CreateInstance", [new SinkParameterDefinition("System.Type", true)])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.DoesNotContain(sinkReport.Findings, entry => entry.RootClassFullName == "ActivatorCreateInstanceConstantNegative");

            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "ActivatorCreateInstanceVariablePositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("ActivatorCreateInstanceVariablePositive::set_TargetType(System.Type)", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.Activator::CreateInstance(System.Type)"));
        }

        [Fact]
        public void Exact_sink_overload_matching_still_supports_assembly_load_by_name_when_explicitly_requested()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Reflection.Assembly", "Load", ["System.String"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var constantFinding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "AssemblyLoadNameConstantNegative");
            var constantTrigger = Assert.Single(constantFinding.TriggerResults);
            Assert.Equal(TriggerKind.Constructor, constantTrigger.TriggerKind);
            Assert.Equal("AssemblyLoadNameConstantNegative::.ctor()", constantTrigger.TriggerMethodDisplay);

            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "AssemblyLoadNameVariablePositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("AssemblyLoadNameVariablePositive::set_AssemblyName(System.String)", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.Reflection.Assembly::Load(System.String)"));
        }

        [Fact]
        public void Sink_can_ignore_constant_request_targets_for_web_request_create()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Net.WebRequest", "Create", [new SinkParameterDefinition("System.String", true)])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.DoesNotContain(sinkReport.Findings, entry => entry.RootClassFullName == "WebRequestCreateConstantNegative");

            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "WebRequestCreateVariablePositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("WebRequestCreateVariablePositive::set_Url(System.String)", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.Net.WebRequest::Create(System.String)"));
        }

        [Fact]
        public void Exact_sink_overload_matching_supports_explicit_parameterless_signatures()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Diagnostics.Process", "Start", Array.Empty<string>())],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "ProcessStartParameterlessPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("ProcessStartParameterlessPositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.Diagnostics.Process::Start()"));
        }

        [Fact]
        public void Exact_sink_overload_matching_supports_specific_process_start_string_overloads()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Diagnostics.Process", "Start", ["System.String"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "ProcessStartStringPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("ProcessStartStringPositive::set_FileName(System.String)", trigger.TriggerMethodDisplay);
            Assert.DoesNotContain(sinkReport.Findings, entry => entry.RootClassFullName == "ProcessStartParameterlessPositive");
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.Diagnostics.Process::Start(System.String)"));
        }

        [Fact]
        public void Finds_method_base_invoke_sink_with_array_parameter_signature()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Reflection.MethodBase", "Invoke", ["System.Object", "System.Object[]"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "MethodBaseInvokePositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("MethodBaseInvokePositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "System.Reflection.MethodBase::Invoke(System.Object, System.Object[])"));
        }

        [Fact]
        public void Finds_virtual_property_descriptor_getter_path_in_broad_mode()
        {
            AnalysisIndex broadIndex = fixture.GetIndex(InterfaceExpansionMode.Broad);
            var report = SinkAnalyzer.Analyze(
                broadIndex,
                [new SinkDefinition("System.ComponentModel.ReflectPropertyDescriptor", "GetValue", ["System.Object"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "PropertyDescriptorGetterPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("PropertyDescriptorGetterPositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(broadIndex, edge, EdgeKind.VirtualDispatch, "System.ComponentModel.ReflectPropertyDescriptor::GetValue(System.Object)"));
        }

        [Fact]
        public void Canonical_generic_filesystem_sink_signature_resolves_loaded_framework_method()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.IO.File", "AppendAllLines", ["System.String", "System.Collections.Generic.IEnumerable`1<System.String>"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);

            Assert.True(sinkReport.IsResolved);
            Assert.Null(sinkReport.ResolutionNote);
            Assert.Contains(
                sinkReport.SinkMethodIds.Select(fixture.Index.GetMethod),
                method => string.Equals(method.DisplayName, "System.IO.File::AppendAllLines(System.String, System.Collections.Generic.IEnumerable`1<System.String>)", StringComparison.Ordinal));
        }

        [Fact]
        public void Canonical_generic_ssrf_sink_signature_resolves_loaded_framework_method()
        {
            var report = SinkAnalyzer.Analyze(
                fixture.Index,
                [new SinkDefinition("System.Net.Sockets.Socket", "SendAsync", ["System.ArraySegment`1<System.Byte>", "System.Net.Sockets.SocketFlags", "System.Boolean"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);

            Assert.True(sinkReport.IsResolved);
            Assert.Null(sinkReport.ResolutionNote);
            Assert.Contains(
                sinkReport.SinkMethodIds.Select(fixture.Index.GetMethod),
                method => string.Equals(method.DisplayName, "System.Net.Sockets.Socket::SendAsync(System.ArraySegment`1<System.Byte>, System.Net.Sockets.SocketFlags, System.Boolean)", StringComparison.Ordinal));
        }

        [Fact]
        public void Resolves_external_framework_sinks_when_assembly_resolution_mode_is_restricted()
        {
            var sampleAssemblyPath = Path.GetFullPath(typeof(MySpecialObject).Assembly.Location);
            var loadResult = AssemblyInputLoader.LoadAssemblySet(
                [sampleAssemblyPath],
                assemblyResolutionMode: AssemblyResolutionMode.Restricted);
            var index = AnalysisIndex.Build(loadResult.Modules);

            var report = SinkAnalyzer.Analyze(
                index,
                [new SinkDefinition("System.Reflection.Assembly", "LoadFrom", ["System.String"])],
                [],
                JsonDotNet);

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            var finding = Assert.Single(sinkReport.Findings, entry => entry.RootClassFullName == "AssemblyLoadFromPositive");
            var trigger = Assert.Single(finding.TriggerResults);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("AssemblyLoadFromPositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge =>
                {
                    Assert.Equal(EdgeKind.DirectCall, edge.Kind);
                    Assert.EndsWith("System.Reflection.Assembly::LoadFrom(System.String)", index.GetMethod(edge.TargetId).DisplayName, StringComparison.Ordinal);
                });
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
