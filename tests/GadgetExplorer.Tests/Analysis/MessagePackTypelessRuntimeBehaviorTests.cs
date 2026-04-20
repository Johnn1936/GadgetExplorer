/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using MessagePack;
using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [TestCaseOrderer(
        "GadgetExplorer.Tests.TestSupport.PriorityOrderer",
        "GadgetExplorer.Tests")]
    public sealed class MessagePackTypelessRuntimeBehaviorTests
    {
        [Fact]
        public void Public_top_level_root_can_round_trip_across_assemblies()
        {
            object roundTrip = RoundTrip(MessagePackTypelessRuntimeTargets.CreatePublicTopLevelRoot());

            Assert.EndsWith("MessagePackTypelessRuntimePublicTopLevelRoot", roundTrip.GetType().FullName, StringComparison.Ordinal);
            Assert.Equal(101, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Internal_top_level_root_can_round_trip_across_assemblies()
        {
            object roundTrip = RoundTrip(MessagePackTypelessRuntimeTargets.CreateInternalTopLevelRoot());

            Assert.EndsWith("MessagePackTypelessRuntimeInternalTopLevelRoot", roundTrip.GetType().FullName, StringComparison.Ordinal);
            Assert.Equal(202, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Public_nested_root_can_round_trip_across_assemblies()
        {
            object roundTrip = RoundTrip(MessagePackTypelessRuntimeTargets.CreatePublicNestedRoot());

            Assert.EndsWith("MessagePackTypelessRuntimePublicVisibilityContainer+PublicNestedRoot", roundTrip.GetType().FullName, StringComparison.Ordinal);
            Assert.Equal(303, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Public_nested_root_inside_internal_container_can_round_trip_across_assemblies()
        {
            object roundTrip = RoundTrip(MessagePackTypelessRuntimeTargets.CreatePublicNestedRootInsideInternalContainer());

            Assert.EndsWith("MessagePackTypelessRuntimeInternalVisibilityContainer+PublicNestedRoot", roundTrip.GetType().FullName, StringComparison.Ordinal);
            Assert.Equal(404, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Protected_nested_root_can_round_trip_across_assemblies()
        {
            object roundTrip = RoundTrip(MessagePackTypelessRuntimeTargets.CreateProtectedNestedRoot());

            Assert.EndsWith("MessagePackTypelessRuntimeVisibilityContainer+ProtectedNestedRoot", roundTrip.GetType().FullName, StringComparison.Ordinal);
            Assert.Equal(505, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Private_nested_root_can_round_trip_across_assemblies()
        {
            object roundTrip = RoundTrip(MessagePackTypelessRuntimeTargets.CreatePrivateNestedRoot());

            Assert.EndsWith("MessagePackTypelessRuntimeVisibilityContainer+PrivateNestedRoot", roundTrip.GetType().FullName, StringComparison.Ordinal);
            Assert.Equal(606, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Public_parameterless_constructor_runs_on_deserialization()
        {
            RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreatePublicParameterlessRoot(),
                out _,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimePublicParameterlessRoot::.ctor()"],
                deserializeLog);
        }

        [Fact]
        public void Public_parameterized_constructor_is_selected_by_best_match()
        {
            object roundTrip = RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreatePublicParameterizedRoot(),
                out _,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimePublicParameterizedRoot::.ctor(string,int)"],
                deserializeLog);
            Assert.Equal("alpha", GetStringProperty(roundTrip, "Name"));
            Assert.Equal(12, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Private_parameterless_constructor_runs_for_contractless_allow_private_types()
        {
            RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreatePrivateParameterlessRoot(),
                out _,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimePrivateParameterlessRoot::.ctor()"],
                deserializeLog);
        }

        [Fact]
        public void Private_parameterized_constructor_runs_for_contractless_allow_private_types()
        {
            object roundTrip = RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreatePrivateParameterizedRoot(),
                out _,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimePrivateParameterizedRoot::.ctor(string,int)"],
                deserializeLog);
            Assert.Equal("beta", GetStringProperty(roundTrip, "Name"));
            Assert.Equal(14, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Parameterized_constructor_is_preferred_over_parameterless_when_it_matches_more_members()
        {
            RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreateParameterlessVsParameterizedRoot(),
                out _,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimeParameterlessVsParameterizedRoot::.ctor(string,int)"],
                deserializeLog);
        }

        [Fact]
        public void Public_serialization_constructor_takes_precedence_over_other_matching_constructors()
        {
            object roundTrip = RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreatePublicSerializationConstructorRoot(),
                out _,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimePublicSerializationConstructorRoot::.ctor(string,int)"],
                deserializeLog);
            Assert.Equal("delta", GetStringProperty(roundTrip, "Name"));
            Assert.Equal(16, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Private_serialization_constructor_takes_precedence_for_contractless_allow_private_types()
        {
            object roundTrip = RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreatePrivateSerializationConstructorRoot(),
                out _,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimePrivateSerializationConstructorRoot::.ctor(string,int)"],
                deserializeLog);
            Assert.Equal("epsilon", GetStringProperty(roundTrip, "Name"));
            Assert.Equal(17, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        public void Contractless_types_with_no_matching_constructor_fail()
        {
            var ex = Assert.Throws<MessagePackSerializationException>(() =>
                RoundTrip(MessagePackTypelessRuntimeTargets.CreateNoMatchRoot()));

            Assert.Contains("constructor", ex.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Annotated_int_key_models_use_index_based_constructor_binding()
        {
            object roundTrip = RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreateAnnotatedIndexSelectionRoot(),
                out _,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimeAnnotatedIndexSelectionRoot::.ctor(string,int)"],
                deserializeLog);
            Assert.Equal("eta", GetStringProperty(roundTrip, "Name"));
            Assert.Equal(19, GetInt32Property(roundTrip, "Value"));
        }

        [Fact]
        [TestPriority(-1)]
        public void Annotated_private_serialization_constructor_fails_in_the_public_object_model_path()
        {
            Assert.Throws<MessagePackSerializationException>(() =>
                RoundTrip(MessagePackTypelessRuntimeTargets.CreateAnnotatedPrivateSerializationConstructorRoot()));
        }

        [Fact]
        public void Private_setter_runs_during_deserialization()
            => AssertSetterLog(
                MessagePackTypelessRuntimeTargets.CreatePrivateSetterRoot(),
                "MessagePackTypelessRuntimePrivateSetterRoot::set_Value(System.Int32)");

        [Fact]
        public void Protected_setter_runs_during_deserialization()
            => AssertSetterLog(
                MessagePackTypelessRuntimeTargets.CreateProtectedSetterRoot(),
                "MessagePackTypelessRuntimeProtectedSetterRoot::set_Value(System.Int32)");

        [Fact]
        public void Internal_setter_runs_during_deserialization()
            => AssertSetterLog(
                MessagePackTypelessRuntimeTargets.CreateInternalSetterRoot(),
                "MessagePackTypelessRuntimeInternalSetterRoot::set_Value(System.Int32)");

        [Fact]
        public void Protected_internal_setter_runs_during_deserialization()
            => AssertSetterLog(
                MessagePackTypelessRuntimeTargets.CreateProtectedInternalSetterRoot(),
                "MessagePackTypelessRuntimeProtectedInternalSetterRoot::set_Value(System.Int32)");

        [Fact]
        public void Private_protected_setter_runs_during_deserialization()
            => AssertSetterLog(
                MessagePackTypelessRuntimeTargets.CreatePrivateProtectedSetterRoot(),
                "MessagePackTypelessRuntimePrivateProtectedSetterRoot::set_Value(System.Int32)");

        [Fact]
        public void Callback_receiver_invokes_on_after_deserialize_but_not_on_before_serialize_during_deserialization()
        {
            RoundTripWithCapturedLogs(
                MessagePackTypelessRuntimeTargets.CreateCallbackReceiverRoot(),
                out IReadOnlyList<string> serializeLog,
                out IReadOnlyList<string> deserializeLog);

            Assert.Equal(
                ["MessagePackTypelessRuntimeCallbackReceiverRoot::OnBeforeSerialize()"],
                serializeLog);
            Assert.Equal(
                ["MessagePackTypelessRuntimeCallbackReceiverRoot::OnAfterDeserialize()"],
                deserializeLog);
        }

        private static void AssertSetterLog(object value, string expectedLogEntry)
        {
            RoundTripWithCapturedLogs(value, out _, out IReadOnlyList<string> deserializeLog);
            Assert.Equal([expectedLogEntry], deserializeLog);
        }

        private static object RoundTripWithCapturedLogs(
            object value,
            out IReadOnlyList<string> serializeLog,
            out IReadOnlyList<string> deserializeLog)
        {
            MessagePackTypelessRuntimeTargets.ResetLog();
            byte[] bytes = MessagePackSerializer.Typeless.Serialize(value);
            serializeLog = MessagePackTypelessRuntimeTargets.GetLog();

            MessagePackTypelessRuntimeTargets.ResetLog();
            object roundTrip = MessagePackSerializer.Typeless.Deserialize(bytes)
                ?? throw new InvalidOperationException("Typeless MessagePack deserialization returned null.");
            deserializeLog = MessagePackTypelessRuntimeTargets.GetLog();
            return roundTrip;
        }

        private static object RoundTrip(object value)
        {
            byte[] bytes = MessagePackSerializer.Typeless.Serialize(value);
            return MessagePackSerializer.Typeless.Deserialize(bytes)
                ?? throw new InvalidOperationException("Typeless MessagePack deserialization returned null.");
        }

        private static int GetInt32Property(object instance, string propertyName)
            => Assert.IsType<int>(instance.GetType().GetProperty(propertyName)!.GetValue(instance));

        private static string GetStringProperty(object instance, string propertyName)
            => Assert.IsType<string>(instance.GetType().GetProperty(propertyName)!.GetValue(instance));
    }
}
