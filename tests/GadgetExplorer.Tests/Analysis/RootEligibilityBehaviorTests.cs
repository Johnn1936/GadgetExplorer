/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [Collection(SmokeSampleCollection.Name)]
    public sealed class RootEligibilityBehaviorTests(SmokeSampleAnalysisFixture fixture) : SmokeSampleAnalysisTestBase
    {
        [Fact]
        public void Json_dot_net_profile_accepts_internal_constructor_roots()
        {
            var trigger = fixture.GetSingleTrigger("JsonNetInternalRootPositive", JsonDotNet);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("JsonNetInternalRootPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Json_dot_net_profile_accepts_protected_nested_constructor_roots()
        {
            var trigger = fixture.GetSingleTrigger("JsonNetNonPublicRootContainer+ProtectedNestedRootPositive", JsonDotNet);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("JsonNetNonPublicRootContainer+ProtectedNestedRootPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Json_dot_net_profile_accepts_private_nested_constructor_roots()
        {
            var trigger = fixture.GetSingleTrigger("JsonNetNonPublicRootContainer+PrivateNestedRootPositive", JsonDotNet);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("JsonNetNonPublicRootContainer+PrivateNestedRootPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Message_pack_typeless_profile_accepts_internal_constructor_roots()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessInternalRootPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("MessagePackTypelessInternalRootPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Message_pack_typeless_profile_accepts_public_nested_roots_inside_internal_containers()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessInternalRootContainer+PublicNestedRootPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("MessagePackTypelessInternalRootContainer+PublicNestedRootPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Message_pack_typeless_profile_accepts_protected_nested_constructor_roots()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessNonPublicRootContainer+ProtectedNestedRootPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("MessagePackTypelessNonPublicRootContainer+ProtectedNestedRootPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Message_pack_typeless_profile_accepts_private_nested_constructor_roots()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessNonPublicRootContainer+PrivateNestedRootPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("MessagePackTypelessNonPublicRootContainer+PrivateNestedRootPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Custom_root_visibility_profiles_can_exclude_private_roots_without_excluding_internal_or_protected_roots()
        {
            var profile = new SerializerProfile(
                "JsonDotNetWithoutPrivateRoots",
                new RootTypeEligibilityPolicy(
                    [
                        RootTypeVisibility.Public,
                        RootTypeVisibility.Internal,
                        RootTypeVisibility.Protected,
                        RootTypeVisibility.ProtectedInternal,
                        RootTypeVisibility.PrivateProtected
                    ]),
                JsonDotNet.TriggerPolicy,
                JsonDotNet.ActivationPolicies,
                JsonDotNet.Callbacks,
                JsonDotNet.CustomDeserializationMethods);

            var internalTrigger = fixture.GetSingleTrigger("JsonNetInternalRootPositive", profile);
            var protectedTrigger = fixture.GetSingleTrigger("JsonNetNonPublicRootContainer+ProtectedNestedRootPositive", profile);

            Assert.Equal("JsonNetInternalRootPositive::.ctor()", internalTrigger.TriggerMethodDisplay);
            Assert.Equal("JsonNetNonPublicRootContainer+ProtectedNestedRootPositive::.ctor()", protectedTrigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("JsonNetNonPublicRootContainer+PrivateNestedRootPositive", profile));
        }

        [Fact]
        public void Accepts_resolvable_constructor_dependency_chain_for_property_root()
        {
            var trigger = fixture.GetSingleTrigger("RecursiveDependencyManager");

            Assert.Equal("RecursiveDependencyManager::set_IsActive(System.Boolean)", trigger.TriggerMethodDisplay);
            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
        }

        [Fact]
        public void Rejects_unresolvable_or_ambiguous_constructor_dependency_roots()
        {
            Assert.False(fixture.HasFinding("InvalidDependencyManager"));
            Assert.False(fixture.HasFinding("MultiCtorManager"));
        }

        [Fact]
        public void Supports_parameterized_constructor_root_for_property_trigger()
        {
            var trigger = fixture.GetSingleTrigger("WorkerManager");

            Assert.Equal("WorkerManager::set_IsActive(System.Boolean)", trigger.TriggerMethodDisplay);
            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
        }

        [Fact]
        public void Json_dot_net_defaults_honor_json_constructor_attribute()
        {
            var trigger = fixture.GetSingleTrigger("JsonConstructorPositive", JsonDotNet);

            Assert.Equal("JsonConstructorPositive::.ctor(System.String)", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));
        }

        [Fact]
        public void Json_dot_net_defaults_allow_non_public_parameterless_constructors_when_no_public_constructor_is_usable()
        {
            var trigger = fixture.GetSingleTrigger("JsonNonPublicParameterlessPositive", JsonDotNet);

            Assert.Equal("JsonNonPublicParameterlessPositive::set_Value(System.Int32)", trigger.TriggerMethodDisplay);
            Assert.Equal(TriggerKind.PublicPropertySetter, trigger.TriggerKind);
        }

        [Fact]
        public void Json_dot_net_defaults_prefer_a_single_public_parameterized_constructor_over_a_non_public_parameterless_constructor()
        {
            var trigger = fixture.GetSingleTrigger("JsonSinglePublicParameterizedPreferredOverNonPublicParameterlessPositive", JsonDotNet);

            Assert.Equal("JsonSinglePublicParameterizedPreferredOverNonPublicParameterlessPositive::.ctor(System.String)", trigger.TriggerMethodDisplay);
            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
        }

        [Fact]
        public void Json_dot_net_defaults_reject_multiple_public_parameterized_constructors_without_a_selector()
        {
            Assert.False(fixture.HasFinding("JsonMultiplePublicParameterizedNegative", JsonDotNet));
        }

        [Fact]
        public void Message_pack_typeless_profile_prefers_best_match_parameterized_constructors_over_parameterless_fallbacks()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessPrivateParameterizedConstructorPositive", MessagePackTypeless);

            Assert.Equal("MessagePackTypelessPrivateParameterizedConstructorPositive::.ctor(System.String)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Xml_serializer_profile_uses_public_parameterless_constructor_activation()
        {
            var trigger = fixture.GetSingleTrigger("XmlSerializerPublicParameterlessPositive", XmlSerializer);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("XmlSerializerPublicParameterlessPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Xml_serializer_profile_uses_private_parameterless_constructor_activation_when_no_public_parameterless_constructor_exists()
        {
            var trigger = fixture.GetSingleTrigger("XmlSerializerPrivateParameterlessPositive", XmlSerializer);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("XmlSerializerPrivateParameterlessPositive::.ctor()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Exact_signature_constructor_profile_honors_the_requested_signature()
        {
            var trigger = fixture.GetSingleTrigger("ExactSignatureConstructorPositive", PublicTwoStringConstructor);

            Assert.Equal("ExactSignatureConstructorPositive::.ctor(System.String, System.String)", trigger.TriggerMethodDisplay);
            Assert.Collection(
                trigger.ReachabilityPath,
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "Helper::InvokeSink()"),
                edge => AssertEdge(fixture, edge, EdgeKind.DirectCall, "MySpecialObject::SayHello()"));

            Assert.False(fixture.HasFinding("ExactSignatureConstructorNegative", PublicTwoStringConstructor));
        }

        [Fact]
        public void Exact_signature_constructor_profile_distinguishes_closed_generic_parameter_types()
        {
            var profile = new SerializerProfile(
                "ExactClosedGenericListActivator",
                new RootTypeEligibilityPolicy([RootTypeVisibility.PubliclyVisible]),
                new SerializerTriggerPolicy(false),
                [
                    new ActivationPolicy(
                        ActivationMode.ExactSignatureConstructor,
                        ExactConstructorSignature: new ExactConstructorSignature(
                            true,
                            ["System.Nullable`1<System.Int32>"]))
                ],
                new CallbackPolicy([], []));

            var trigger = fixture.GetSingleTrigger("ExactClosedGenericConstructorPositive", profile);

            Assert.Equal("ExactClosedGenericConstructorPositive::.ctor(System.Nullable`1<System.Int32>)", trigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("ExactClosedGenericConstructorNegative", profile));
        }
    }
}
