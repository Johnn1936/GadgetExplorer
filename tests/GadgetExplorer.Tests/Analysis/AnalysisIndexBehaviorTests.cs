/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [Collection(SmokeSampleCollection.Name)]
    public sealed class AnalysisIndexBehaviorTests(SmokeSampleAnalysisFixture fixture)
    {
        [Fact]
        public void Helper_invoke_sink_emits_constructor_call_and_direct_call_edges()
        {
            var method = GetMethod("Helper::InvokeSink()");
            var outgoingEdges = fixture.Index.GetOutgoingEdges(method.Id)
                .Select(fixture.Index.GetEdge)
                .ToArray();

            Assert.Contains(outgoingEdges, edge =>
                edge.Kind == EdgeKind.ConstructorCall &&
                fixture.Index.GetMethod(edge.TargetId).DisplayName.EndsWith("MySpecialObject::.ctor()", StringComparison.Ordinal));
            Assert.Contains(outgoingEdges, edge =>
                edge.Kind == EdgeKind.DirectCall &&
                fixture.Index.GetMethod(edge.TargetId).DisplayName.EndsWith("MySpecialObject::SayHello()", StringComparison.Ordinal));
        }

        [Fact]
        public void Direct_property_access_emits_property_accessor_edges()
        {
            var method = GetMethod("PropertyAccessorBridge::ReadAfterWrite()");
            var outgoingEdges = fixture.Index.GetOutgoingEdges(method.Id)
                .Select(fixture.Index.GetEdge)
                .ToArray();

            Assert.Contains(outgoingEdges, edge =>
                edge.Kind == EdgeKind.PropertyAccessor &&
                fixture.Index.GetMethod(edge.TargetId).DisplayName.EndsWith("PropertyAccessorTarget::set_Value(System.Int32)", StringComparison.Ordinal));
            Assert.Contains(outgoingEdges, edge =>
                edge.Kind == EdgeKind.PropertyAccessor &&
                fixture.Index.GetMethod(edge.TargetId).DisplayName.EndsWith("PropertyAccessorTarget::get_Value()", StringComparison.Ordinal));
        }

        [Fact]
        public void Activator_create_instance_constant_type_call_is_summarized_as_a_type_constant()
        {
            var method = GetMethod("ActivatorCreateInstanceConstantNegative::.ctor()");
            var edge = fixture.Index.GetOutgoingEdges(method.Id)
                .Select(fixture.Index.GetEdge)
                .Single(candidate => fixture.Index.GetMethod(candidate.TargetId).DisplayName.EndsWith("System.Activator::CreateInstance(System.Type)", StringComparison.Ordinal));

            var summary = Assert.Single(edge.ArgumentSummaries);
            Assert.True(summary.IsProvablyConstant);
            Assert.Equal(ConstantValueKind.Type, summary.ConstantKind);
            Assert.Equal("MySpecialObject", summary.DisplayValue);
        }

        [Fact]
        public void Uri_constructor_result_is_summarized_as_a_uri_constant()
        {
            var method = GetMethod("UriConstantSource::.ctor()");
            var edge = fixture.Index.GetOutgoingEdges(method.Id)
                .Select(fixture.Index.GetEdge)
                .Single(candidate => fixture.Index.GetMethod(candidate.TargetId).DisplayName.EndsWith("UriConsumer::Accept(System.Uri)", StringComparison.Ordinal));

            var summary = Assert.Single(edge.ArgumentSummaries);
            Assert.True(summary.IsProvablyConstant);
            Assert.Equal(ConstantValueKind.Uri, summary.ConstantKind);
            Assert.Equal("https://example.invalid/demo", summary.DisplayValue);
        }

        [Fact]
        public void Method_display_names_preserve_closed_generic_parameter_types()
        {
            MethodRecord[] overloads = [.. fixture.Index.Methods
                .Where(method => method.DisplayName.StartsWith("ClosedGenericSinkTarget::Invoke(", StringComparison.Ordinal))
                .OrderBy(method => method.DisplayName, StringComparer.Ordinal)];

            Assert.Collection(
                overloads,
                method => Assert.Equal("ClosedGenericSinkTarget::Invoke(System.Collections.Generic.List`1<System.Int32>)", method.DisplayName),
                method => Assert.Equal("ClosedGenericSinkTarget::Invoke(System.Collections.Generic.List`1<System.String>)", method.DisplayName));
        }

        [Fact]
        public void Exact_generic_get_enumerator_dispatch_uses_the_concrete_receiver_in_strict_mode()
        {
            AnalysisIndex strictIndex = fixture.GetIndex();
            MethodRecord method = GetMethod(strictIndex, "ExactEnumerableParameterPositive::.ctor()");
            EdgeRecord[] outgoingEdges = GetOutgoingEdges(strictIndex, method);
            string[] targets = [.. outgoingEdges.Select(edge => $"{edge.Kind}:{strictIndex.GetMethod(edge.TargetId).DisplayName}")];

            Assert.True(
                outgoingEdges.Any(edge =>
                    edge.Kind == EdgeKind.InterfaceDispatch &&
                    strictIndex.GetMethod(edge.TargetId).DisplayName.EndsWith("SinkingEnumerableParameterSource::GetEnumerator()", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, targets));
        }

        [Fact]
        public void Open_ended_generic_get_enumerator_dispatch_is_dropped_in_strict_mode()
        {
            AnalysisIndex strictIndex = fixture.GetIndex();
            MethodRecord method = GetMethod(strictIndex, "EnumerableParameterRelay::Capture(System.Collections.Generic.IEnumerable`1<System.Int32>)");
            EdgeRecord[] outgoingEdges = GetOutgoingEdges(strictIndex, method);

            Assert.DoesNotContain(outgoingEdges, edge => edge.Kind == EdgeKind.InterfaceDispatch);
        }

        [Fact]
        public void Open_ended_dictionary_enumerator_current_dispatch_is_dropped_in_strict_mode()
        {
            AnalysisIndex strictIndex = fixture.GetIndex();
            MethodRecord method = GetMethod(strictIndex, "DictionaryEnumeratorRelay::ReadCurrent(System.Collections.IDictionaryEnumerator)");
            EdgeRecord[] outgoingEdges = GetOutgoingEdges(strictIndex, method);

            Assert.DoesNotContain(outgoingEdges, edge => edge.Kind == EdgeKind.InterfaceDispatch);
        }

        [Fact]
        public void Smoke_sample_index_metrics_match_the_baseline_contract()
        {
            string sampleAssemblyPath = Path.GetFullPath(typeof(MySpecialObject).Assembly.Location);
            var assemblies = AssemblyInputLoader.LoadModules(
                [sampleAssemblyPath],
                assemblyResolutionMode: AssemblyResolutionMode.Restricted);
            AnalysisIndex strictIndex = AnalysisIndex.Build(assemblies);

            Assert.Equal(1298, strictIndex.Types.Count);
            Assert.Equal(9987, strictIndex.Methods.Count);
            Assert.Equal(978, strictIndex.PropertyCount);
            Assert.Equal(406, strictIndex.PublicInstancePropertySetterCount);
            Assert.Equal(10, strictIndex.Events.Count);
            Assert.Equal(28278, strictIndex.Edges.Count);
            Assert.Equal(2275, strictIndex.OverrideRelationshipCount);
            Assert.Equal(211, strictIndex.InterfaceImplementationRelationshipCount);
            Assert.Equal(588, strictIndex.InstantiatedTypeCount);
        }

        private static EdgeRecord[] GetOutgoingEdges(AnalysisIndex index, MethodRecord method)
            => [.. index.GetOutgoingEdges(method.Id).Select(index.GetEdge)];

        private MethodRecord GetMethod(string displayName)
            => GetMethod(fixture.Index, displayName);

        private static MethodRecord GetMethod(AnalysisIndex index, string displayName)
            => index.Methods.Single(method => string.Equals(method.DisplayName, displayName, StringComparison.Ordinal));
    }
}
