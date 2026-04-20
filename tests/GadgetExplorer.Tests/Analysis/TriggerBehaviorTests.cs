/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [Collection(SmokeSampleCollection.Name)]
    public sealed class TriggerBehaviorTests(SmokeSampleAnalysisFixture fixture) : SmokeSampleAnalysisTestBase
    {
        [Fact]
        public void Finds_direct_constructor_trigger()
        {
            var trigger = fixture.GetSingleTrigger("ConstructorPositive");

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("ConstructorPositive::.ctor()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Prefers_parameterless_constructor_when_multiple_public_constructors_exist()
        {
            var trigger = fixture.GetSingleTrigger("ParameterlessPreferredCtorPositive");

            Assert.Equal("ParameterlessPreferredCtorPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Finds_direct_public_property_setter_trigger()
        {
            var trigger = fixture.GetSingleTrigger("SetterPositive");

            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("SetterPositive::set_Value(System.Int32)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Json_dot_net_getter_profile_finds_public_property_getter_trigger()
        {
            var trigger = fixture.GetSingleTrigger("GetterPositive", JsonDotNetGetters);

            Assert.Equal(TriggerKind.PublicPropertyGetter, trigger.TriggerKind);
            Assert.Equal("GetterPositive::get_Value()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Xml_serializer_profile_finds_public_property_setter_trigger()
        {
            var trigger = fixture.GetSingleTrigger("XmlSerializerPublicSetterPositive", XmlSerializer);

            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("XmlSerializerPublicSetterPositive::set_Value(System.Int32)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Xml_serializer_profile_does_not_report_non_public_property_setter_triggers()
        {
            Assert.False(fixture.HasFinding("XmlSerializerNonPublicSetterNegative", XmlSerializer));
        }

        [Fact]
        public void Message_pack_typeless_profile_finds_private_property_setter_triggers()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessPrivateSetterPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.NonPublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("MessagePackTypelessPrivateSetterPositive::set_Value(System.Int32)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Does_not_report_negative_type_without_sink_path()
        {
            Assert.False(fixture.HasFinding("Negative"));
        }

        [Fact]
        public void Expands_inherited_setter_to_derived_root()
        {
            var trigger = fixture.GetSingleTrigger("Employee");

            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("Person", trigger.TriggerDeclaredOnTypeName);
            Assert.Equal("Person::set_Name(System.String)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Json_dot_net_getter_profile_expands_inherited_getter_to_derived_root()
        {
            var trigger = fixture.GetSingleTrigger("GetterEmployee", JsonDotNetGetters);

            Assert.Equal(TriggerKind.PublicPropertyGetter, trigger.TriggerKind);
            Assert.Equal("GetterPerson", trigger.TriggerDeclaredOnTypeName);
            Assert.Equal("GetterPerson::get_Name()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Finds_on_deserializing_callback_trigger()
        {
            var trigger = fixture.GetSingleTrigger("OnDeserializingPositive");

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("OnDeserializingPositive::BeforePopulate(System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Finds_on_deserialized_callback_trigger()
        {
            var trigger = fixture.GetSingleTrigger("OnDeserializedPositive");

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("OnDeserializedPositive::AfterPopulate(System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Finds_on_error_callback_trigger()
        {
            var trigger = fixture.GetSingleTrigger("JsonNetOnErrorPositive", JsonDotNet);

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal(
                "JsonNetOnErrorPositive::OnError(System.Runtime.Serialization.StreamingContext, Newtonsoft.Json.Serialization.ErrorContext)",
                trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Expands_inherited_deserialization_callback_to_derived_root()
        {
            var trigger = fixture.GetSingleTrigger("InheritedOnDeserializedPositive");

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("CallbackHookBase", trigger.TriggerDeclaredOnTypeName);
            Assert.Equal("CallbackHookBase::AfterPopulateBase(System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Message_pack_typeless_profile_finds_messagepack_callback_receiver_triggers()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessCallbackReceiverPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("MessagePackTypelessCallbackReceiverPositive::OnAfterDeserialize()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Finds_finalizer_trigger()
        {
            var trigger = fixture.GetSingleTrigger("FinalizerPositive");

            Assert.Equal(TriggerKind.Finalizer, trigger.TriggerKind);
            Assert.Equal("FinalizerPositive::Finalize()", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Json_dot_net_getter_profile_ignores_constructor_setter_and_finalizer_roots()
        {
            Assert.False(fixture.HasFinding("ConstructorPositive", JsonDotNetGetters));
            Assert.False(fixture.HasFinding("SetterPositive", JsonDotNetGetters));
            Assert.False(fixture.HasFinding("FinalizerPositive", JsonDotNetGetters));
        }

        [Fact]
        public void Expands_inherited_finalizer_to_derived_root()
        {
            var trigger = fixture.GetSingleTrigger("InheritedFinalizerPositive");

            Assert.Equal(TriggerKind.Finalizer, trigger.TriggerKind);
            Assert.Equal("FinalizerHookBase", trigger.TriggerDeclaredOnTypeName);
            Assert.Equal("FinalizerHookBase::Finalize()", trigger.TriggerMethodDisplay);
        }
    }
}
