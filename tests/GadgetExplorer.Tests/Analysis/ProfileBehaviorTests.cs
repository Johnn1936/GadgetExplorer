/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [Collection(SmokeSampleCollection.Name)]
    public sealed class ProfileBehaviorTests(SmokeSampleAnalysisFixture fixture) : SmokeSampleAnalysisTestBase
    {
        [Fact]
        public void Shipped_serializer_profiles_match_current_behavior()
        {
            Assert.Equal("JsonDotNet", JsonDotNet.Name);
            Assert.True(JsonDotNet.SupportsConstructorTriggers);
            Assert.False(JsonDotNet.SupportsPublicPropertyGetterTriggers);
            Assert.True(JsonDotNet.SupportsPublicPropertySetterTriggers);
            Assert.True(JsonDotNet.SupportsDeserializationCallbackTriggers);
            Assert.True(JsonDotNet.SupportsFinalizerTriggers);
            AssertAllRootTypeVisibilities(JsonDotNet.AllowedRootTypeVisibilities);
            Assert.Empty(JsonDotNet.AllowedPropertyGetterVisibilities);
            Assert.Equal([MethodVisibility.Public], JsonDotNet.AllowedPropertySetterVisibilities);
            Assert.Contains("Newtonsoft.Json.JsonPropertyAttribute", JsonDotNet.NonPublicPropertySetterOptInAttributeTypeNames);
            Assert.Contains("System.Runtime.Serialization.DataMemberAttribute", JsonDotNet.NonPublicPropertySetterOptInAttributeTypeNames);
            Assert.Contains(
                JsonDotNet.Callbacks.AttributeCallbacks,
                callback => callback is
                {
                    AttributeTypeName: "System.Runtime.Serialization.OnDeserializingAttribute",
                    ReturnTypeName: "System.Void"
                } &&
                callback.ParameterTypeNames.SequenceEqual(["System.Runtime.Serialization.StreamingContext"]));
            Assert.Contains(
                JsonDotNet.Callbacks.AttributeCallbacks,
                callback => callback is
                {
                    AttributeTypeName: "System.Runtime.Serialization.OnDeserializedAttribute",
                    ReturnTypeName: "System.Void"
                } &&
                callback.ParameterTypeNames.SequenceEqual(["System.Runtime.Serialization.StreamingContext"]));
            Assert.Contains(
                JsonDotNet.Callbacks.AttributeCallbacks,
                callback => callback is
                {
                    AttributeTypeName: "Newtonsoft.Json.Serialization.OnErrorAttribute",
                    ReturnTypeName: "System.Void"
                } &&
                callback.ParameterTypeNames.SequenceEqual(
                    ["System.Runtime.Serialization.StreamingContext", "Newtonsoft.Json.Serialization.ErrorContext"]));
            Assert.Empty(JsonDotNet.Callbacks.InterfaceCallbacks);
            Assert.Collection(
                JsonDotNet.ActivationPolicies,
                serializationPolicy =>
                {
                    Assert.Equal(ActivationMode.SerializationConstructor, serializationPolicy.Mode);
                    Assert.Contains("System.Runtime.Serialization.ISerializable", serializationPolicy.RequiredDeclaringTypeInterfaceNames);
                    Assert.NotNull(serializationPolicy.SerializationConstructorSignature);
                    Assert.Contains(MethodVisibility.Public, serializationPolicy.SerializationConstructorSignature!.VisibilityPolicy.SealedTypeAllowedVisibilities);
                    Assert.Contains(MethodVisibility.Private, serializationPolicy.SerializationConstructorSignature.VisibilityPolicy.SealedTypeAllowedVisibilities);
                    Assert.Contains(MethodVisibility.Public, serializationPolicy.SerializationConstructorSignature.VisibilityPolicy.UnsealedTypeAllowedVisibilities);
                    Assert.Contains(MethodVisibility.Family, serializationPolicy.SerializationConstructorSignature.VisibilityPolicy.UnsealedTypeAllowedVisibilities);
                    Assert.Contains(serializationPolicy.Requirements, requirement =>
                        requirement is { Kind: RootTypeRequirementKind.HasAttribute, TypeName: "System.SerializableAttribute" });
                },
                constructorSelectionPolicy =>
                {
                    Assert.Equal(ActivationMode.ConstructorSelection, constructorSelectionPolicy.Mode);
                    Assert.Contains("Newtonsoft.Json.JsonConstructorAttribute", constructorSelectionPolicy.PreferredConstructorAttributeTypeNames);
                    Assert.Collection(
                        constructorSelectionPolicy.ConstructorSelectionRules,
                        rule => Assert.Equal(ConstructorSelectionTarget.Attributed, rule.Target),
                        rule => Assert.Equal(ConstructorSelectionTarget.PublicParameterless, rule.Target),
                        rule => Assert.Equal(ConstructorSelectionTarget.SinglePublicParameterized, rule.Target),
                        rule => Assert.Equal(ConstructorSelectionTarget.NonPublicParameterless, rule.Target));
                });

            Assert.Equal("JsonDotNetGetters", JsonDotNetGetters.Name);
            Assert.False(JsonDotNetGetters.SupportsConstructorTriggers);
            Assert.True(JsonDotNetGetters.SupportsPublicPropertyGetterTriggers);
            Assert.False(JsonDotNetGetters.SupportsPublicPropertySetterTriggers);
            Assert.False(JsonDotNetGetters.SupportsDeserializationCallbackTriggers);
            Assert.False(JsonDotNetGetters.SupportsFinalizerTriggers);
            AssertAllRootTypeVisibilities(JsonDotNetGetters.AllowedRootTypeVisibilities);
            Assert.Equal([MethodVisibility.Public], JsonDotNetGetters.AllowedPropertyGetterVisibilities);
            Assert.Empty(JsonDotNetGetters.AllowedPropertySetterVisibilities);
            Assert.Empty(JsonDotNetGetters.Callbacks.AttributeCallbacks);
            Assert.Empty(JsonDotNetGetters.Callbacks.InterfaceCallbacks);
            var getterActivationPolicy = Assert.Single(JsonDotNetGetters.ActivationPolicies);
            Assert.Equal(ActivationMode.ConstructorSelection, getterActivationPolicy.Mode);
            Assert.Contains("Newtonsoft.Json.JsonConstructorAttribute", getterActivationPolicy.PreferredConstructorAttributeTypeNames);
            Assert.Collection(
                getterActivationPolicy.ConstructorSelectionRules,
                rule => Assert.Equal(ConstructorSelectionTarget.Attributed, rule.Target),
                rule => Assert.Equal(ConstructorSelectionTarget.PublicParameterless, rule.Target),
                rule => Assert.Equal(ConstructorSelectionTarget.SinglePublicParameterized, rule.Target),
                rule => Assert.Equal(ConstructorSelectionTarget.NonPublicParameterless, rule.Target));

            Assert.Equal("PublicTwoStringConstructor", PublicTwoStringConstructor.Name);
            Assert.True(PublicTwoStringConstructor.SupportsConstructorTriggers);
            Assert.False(PublicTwoStringConstructor.SupportsPublicPropertyGetterTriggers);
            Assert.False(PublicTwoStringConstructor.SupportsPublicPropertySetterTriggers);
            Assert.False(PublicTwoStringConstructor.SupportsDeserializationCallbackTriggers);
            Assert.True(PublicTwoStringConstructor.SupportsFinalizerTriggers);
            Assert.Equal([RootTypeVisibility.PubliclyVisible], PublicTwoStringConstructor.AllowedRootTypeVisibilities);
            Assert.Empty(PublicTwoStringConstructor.AllowedPropertyGetterVisibilities);
            Assert.Empty(PublicTwoStringConstructor.AllowedPropertySetterVisibilities);
            Assert.Empty(PublicTwoStringConstructor.Callbacks.AttributeCallbacks);
            Assert.Empty(PublicTwoStringConstructor.Callbacks.InterfaceCallbacks);
            var activatorActivationPolicy = Assert.Single(PublicTwoStringConstructor.ActivationPolicies);
            Assert.Equal(ActivationMode.ExactSignatureConstructor, activatorActivationPolicy.Mode);
            Assert.Equal(["System.String", "System.String"], activatorActivationPolicy.ExactConstructorSignature!.ParameterTypeNames);

            Assert.Equal("BinaryFormatter", BinaryFormatter.Name);
            Assert.True(BinaryFormatter.SupportsConstructorTriggers);
            Assert.False(BinaryFormatter.SupportsPublicPropertyGetterTriggers);
            Assert.False(BinaryFormatter.SupportsPublicPropertySetterTriggers);
            Assert.True(BinaryFormatter.SupportsDeserializationCallbackTriggers);
            Assert.True(BinaryFormatter.SupportsFinalizerTriggers);
            AssertAllRootTypeVisibilities(BinaryFormatter.AllowedRootTypeVisibilities);
            Assert.Empty(BinaryFormatter.AllowedPropertyGetterVisibilities);
            Assert.Empty(BinaryFormatter.AllowedPropertySetterVisibilities);
            Assert.Contains(BinaryFormatter.RootTypeEligibility.Requirements, requirement =>
                requirement is { Kind: RootTypeRequirementKind.HasAttribute, TypeName: "System.SerializableAttribute" });
            Assert.Contains(BinaryFormatter.ActivationPolicies, policy => policy.Mode == ActivationMode.UninitializedObject);
            var serializationConstructorPolicy = Assert.Single(BinaryFormatter.ActivationPolicies, policy => policy.Mode == ActivationMode.SerializationConstructor);
            Assert.Contains("System.Runtime.Serialization.ISerializable", serializationConstructorPolicy.RequiredDeclaringTypeInterfaceNames);
            Assert.Equal(
                [
                    MethodVisibility.Public,
                    MethodVisibility.Private,
                    MethodVisibility.Family,
                    MethodVisibility.Assembly,
                    MethodVisibility.FamilyOrAssembly,
                    MethodVisibility.FamilyAndAssembly
                ],
                serializationConstructorPolicy.SerializationConstructorSignature!.VisibilityPolicy.SealedTypeAllowedVisibilities);
            Assert.Equal(
                [
                    MethodVisibility.Public,
                    MethodVisibility.Private,
                    MethodVisibility.Family,
                    MethodVisibility.Assembly,
                    MethodVisibility.FamilyOrAssembly,
                    MethodVisibility.FamilyAndAssembly
                ],
                serializationConstructorPolicy.SerializationConstructorSignature.VisibilityPolicy.UnsealedTypeAllowedVisibilities);
            Assert.Contains(BinaryFormatter.Callbacks.AttributeCallbacks, callback =>
                callback is
                {
                    AttributeTypeName: "System.Runtime.Serialization.OnDeserializingAttribute",
                    ReturnTypeName: "System.Void"
                } &&
                callback.ParameterTypeNames.SequenceEqual(["System.Runtime.Serialization.StreamingContext"]));
            Assert.Contains(BinaryFormatter.Callbacks.AttributeCallbacks, callback =>
                callback is
                {
                    AttributeTypeName: "System.Runtime.Serialization.OnDeserializedAttribute",
                    ReturnTypeName: "System.Void"
                } &&
                callback.ParameterTypeNames.SequenceEqual(["System.Runtime.Serialization.StreamingContext"]));
            Assert.Contains(BinaryFormatter.Callbacks.InterfaceCallbacks, callback =>
                callback is { InterfaceTypeName: "System.Runtime.Serialization.IDeserializationCallback", MethodName: "OnDeserialization" });
            Assert.Contains(BinaryFormatter.Callbacks.InterfaceCallbacks, callback =>
                callback is { InterfaceTypeName: "System.Runtime.Serialization.IObjectReference", MethodName: "GetRealObject" });

            Assert.Equal("MessagePackTypeless", MessagePackTypeless.Name);
            Assert.True(MessagePackTypeless.SupportsConstructorTriggers);
            Assert.False(MessagePackTypeless.SupportsPublicPropertyGetterTriggers);
            Assert.True(MessagePackTypeless.SupportsPublicPropertySetterTriggers);
            Assert.True(MessagePackTypeless.SupportsDeserializationCallbackTriggers);
            Assert.True(MessagePackTypeless.SupportsFinalizerTriggers);
            AssertAllRootTypeVisibilities(MessagePackTypeless.AllowedRootTypeVisibilities);
            Assert.Empty(MessagePackTypeless.AllowedPropertyGetterVisibilities);
            Assert.Equal(
                [
                    MethodVisibility.Public,
                    MethodVisibility.Private,
                    MethodVisibility.Family,
                    MethodVisibility.Assembly,
                    MethodVisibility.FamilyOrAssembly,
                    MethodVisibility.FamilyAndAssembly
                ],
                MessagePackTypeless.AllowedPropertySetterVisibilities);
            Assert.Empty(MessagePackTypeless.Callbacks.AttributeCallbacks);
            Assert.Contains(
                MessagePackTypeless.Callbacks.InterfaceCallbacks,
                callback => callback is
                {
                    InterfaceTypeName: "MessagePack.IMessagePackSerializationCallbackReceiver",
                    MethodName: "OnAfterDeserialize",
                    ReturnTypeName: "System.Void"
                } &&
                callback.ParameterTypeNames.SequenceEqual([]));
            Assert.Collection(
                MessagePackTypeless.ActivationPolicies,
                messagePackObjectPolicy =>
                {
                    Assert.Equal(ActivationMode.ConstructorSelection, messagePackObjectPolicy.Mode);
                    Assert.Contains("MessagePack.SerializationConstructorAttribute", messagePackObjectPolicy.PreferredConstructorAttributeTypeNames);
                    Assert.Equal([MethodVisibility.Public], messagePackObjectPolicy.AllowedConstructorVisibilities);
                    Assert.Equal(
                        [ConstructorBindingMode.Name, ConstructorBindingMode.Index],
                        messagePackObjectPolicy.ConstructorBindingModes);
                    Assert.Equal(["MessagePack.KeyAttribute"], messagePackObjectPolicy.IndexedMemberAttributeTypeNames);
                    Assert.Contains(messagePackObjectPolicy.Requirements, requirement =>
                        requirement is { Kind: RootTypeRequirementKind.HasAttribute, TypeName: "MessagePack.MessagePackObjectAttribute" });
                    Assert.Collection(
                        messagePackObjectPolicy.ConstructorSelectionRules,
                        rule => Assert.Equal(ConstructorSelectionTarget.Attributed, rule.Target),
                        rule => Assert.Equal(ConstructorSelectionTarget.BestMatch, rule.Target));
                },
                dataContractPolicy =>
                {
                    Assert.Equal(ActivationMode.ConstructorSelection, dataContractPolicy.Mode);
                    Assert.Equal([MethodVisibility.Public], dataContractPolicy.AllowedConstructorVisibilities);
                    Assert.Equal([ConstructorBindingMode.Name], dataContractPolicy.ConstructorBindingModes);
                    Assert.Contains(dataContractPolicy.Requirements, requirement =>
                        requirement is { Kind: RootTypeRequirementKind.HasAttribute, TypeName: "System.Runtime.Serialization.DataContractAttribute" });
                },
                contractlessPolicy =>
                {
                    Assert.Equal(ActivationMode.ConstructorSelection, contractlessPolicy.Mode);
                    Assert.Equal(
                        [
                            MethodVisibility.Public,
                            MethodVisibility.Private,
                            MethodVisibility.Family,
                            MethodVisibility.Assembly,
                            MethodVisibility.FamilyOrAssembly,
                            MethodVisibility.FamilyAndAssembly
                        ],
                        contractlessPolicy.AllowedConstructorVisibilities);
                    Assert.Equal([ConstructorBindingMode.Name], contractlessPolicy.ConstructorBindingModes);
                    Assert.Contains(contractlessPolicy.Requirements, requirement =>
                        requirement is { Kind: RootTypeRequirementKind.LacksAttribute, TypeName: "MessagePack.MessagePackObjectAttribute" });
                    Assert.Contains(contractlessPolicy.Requirements, requirement =>
                        requirement is { Kind: RootTypeRequirementKind.LacksAttribute, TypeName: "System.Runtime.Serialization.DataContractAttribute" });
                });

            Assert.Equal("XmlSerializer", XmlSerializer.Name);
            Assert.True(XmlSerializer.SupportsConstructorTriggers);
            Assert.False(XmlSerializer.SupportsPublicPropertyGetterTriggers);
            Assert.True(XmlSerializer.SupportsPublicPropertySetterTriggers);
            Assert.False(XmlSerializer.SupportsDeserializationCallbackTriggers);
            Assert.True(XmlSerializer.SupportsCustomDeserializationMethodTriggers);
            Assert.True(XmlSerializer.SupportsFinalizerTriggers);
            Assert.Equal([RootTypeVisibility.PubliclyVisible], XmlSerializer.AllowedRootTypeVisibilities);
            Assert.Empty(XmlSerializer.AllowedPropertyGetterVisibilities);
            Assert.Equal([MethodVisibility.Public], XmlSerializer.AllowedPropertySetterVisibilities);
            Assert.Empty(XmlSerializer.NonPublicPropertySetterOptInAttributeTypeNames);
            Assert.Empty(XmlSerializer.Callbacks.AttributeCallbacks);
            Assert.Empty(XmlSerializer.Callbacks.InterfaceCallbacks);
            Assert.Collection(
                XmlSerializer.CustomDeserializationMethods.InterfaceMethods,
                method =>
                {
                    Assert.Equal("System.Xml.Serialization.IXmlSerializable", method.InterfaceTypeName);
                    Assert.Equal("ReadXml", method.MethodName);
                    Assert.Equal(["System.Xml.XmlReader"], method.ParameterTypeNames);
                    Assert.Equal("System.Void", method.ReturnTypeName);
                });
            var xmlActivationPolicy = Assert.Single(XmlSerializer.ActivationPolicies);
            Assert.Equal(ActivationMode.ConstructorSelection, xmlActivationPolicy.Mode);
            Assert.Empty(xmlActivationPolicy.PreferredConstructorAttributeTypeNames);
            Assert.Empty(xmlActivationPolicy.RequiredDeclaringTypeInterfaceNames);
            Assert.Collection(
                xmlActivationPolicy.ConstructorSelectionRules,
                rule =>
                {
                    Assert.Equal(ConstructorSelectionTarget.PublicParameterless, rule.Target);
                    Assert.Equal(1, rule.When.PublicParameterlessCount);
                    Assert.Null(rule.When.NonPublicParameterlessCount);
                    Assert.Null(rule.When.PublicParameterizedCount);
                },
                rule =>
                {
                    Assert.Equal(ConstructorSelectionTarget.NonPublicParameterless, rule.Target);
                    Assert.Equal(0, rule.When.PublicParameterlessCount);
                    Assert.Equal(1, rule.When.NonPublicParameterlessCount);
                    Assert.Null(rule.When.PublicParameterizedCount);
                });
            Assert.Contains("System.Xml.Serialization.IXmlSerializable", xmlActivationPolicy.OrdinaryObjectMapping.IgnoredDeclaringTypeInterfaceNames);
            Assert.True(xmlActivationPolicy.OrdinaryObjectMapping.RejectPublicFieldsOrSettablePropertiesOfInterfaceTypes);
            Assert.Equal(["System.Type"], xmlActivationPolicy.OrdinaryObjectMapping.RejectedPublicFieldOrSettablePropertyTypeNames);
        }

        [Fact]
        public void Shipped_serializer_profiles_can_be_resolved_by_name()
        {
            Assert.Same(JsonDotNet, SerializerProfiles.Resolve("JsonDotNet"));
            Assert.Same(JsonDotNetGetters, SerializerProfiles.Resolve("JsonDotNetGetters"));
            Assert.Same(PublicTwoStringConstructor, SerializerProfiles.Resolve("PublicTwoStringConstructor"));
            Assert.Same(BinaryFormatter, SerializerProfiles.Resolve("BinaryFormatter"));
            Assert.Same(MessagePackTypeless, SerializerProfiles.Resolve("MessagePackTypeless"));
            Assert.Same(XmlSerializer, SerializerProfiles.Resolve("XmlSerializer"));
        }

        [Fact]
        public void Binary_formatter_profile_requires_serializable_attribute_for_callback_roots()
        {
            var trigger = fixture.GetSingleTrigger("BinaryFormatterSerializableCallbackPositive", BinaryFormatter);

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("BinaryFormatterSerializableCallbackPositive::AfterDeserialize(System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("BinaryFormatterNonSerializableCallbackNegative", BinaryFormatter));
        }

        [Fact]
        public void Binary_formatter_profile_requires_serializable_concrete_generic_root_arguments()
        {
            var trigger = fixture.GetSingleTrigger("BinaryFormatterClosedGenericRootPositive", BinaryFormatter);

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("BinaryFormatterGenericCallbackBase`1::AfterDeserialize(System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("BinaryFormatterClosedGenericRootNegative", BinaryFormatter));
        }

        [Fact]
        public void Binary_formatter_profile_rejects_open_generic_root_definitions()
        {
            Assert.False(fixture.HasFinding("BinaryFormatterOpenGenericCallbackRoot`1", BinaryFormatter));
        }

        [Fact]
        public void Binary_formatter_serialization_constructor_requires_iserializable()
        {
            var trigger = fixture.GetSingleTrigger("BinaryFormatterSerializationConstructorPositive", BinaryFormatter);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("BinaryFormatterSerializationConstructorPositive::.ctor(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("BinaryFormatterSerializationConstructorNegative", BinaryFormatter));
            Assert.False(fixture.HasFinding("BinaryFormatterNonSerializableISerializableNegative", BinaryFormatter));
        }

        [Fact]
        public void Binary_formatter_profile_accepts_any_exact_signature_serialization_constructor_visibility()
        {
            var sealedTrigger = fixture.GetSingleTrigger("BinaryFormatterSealedPrivateSerializationConstructorPositive", BinaryFormatter);
            Assert.Equal(
                "BinaryFormatterSealedPrivateSerializationConstructorPositive::.ctor(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)",
                sealedTrigger.TriggerMethodDisplay);

            var unsealedTrigger = fixture.GetSingleTrigger("BinaryFormatterUnsealedProtectedSerializationConstructorPositive", BinaryFormatter);
            Assert.Equal(
                "BinaryFormatterUnsealedProtectedSerializationConstructorPositive::.ctor(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)",
                unsealedTrigger.TriggerMethodDisplay);

            var sealedPublicTrigger = fixture.GetSingleTrigger("BinaryFormatterSealedPublicSerializationConstructorPositive", BinaryFormatter);
            Assert.Equal(
                "BinaryFormatterSealedPublicSerializationConstructorPositive::.ctor(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)",
                sealedPublicTrigger.TriggerMethodDisplay);

            var unsealedPrivateTrigger = fixture.GetSingleTrigger("BinaryFormatterUnsealedPrivateSerializationConstructorPositive", BinaryFormatter);
            Assert.Equal(
                "BinaryFormatterUnsealedPrivateSerializationConstructorPositive::.ctor(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)",
                unsealedPrivateTrigger.TriggerMethodDisplay);

            var unsealedInternalTrigger = fixture.GetSingleTrigger("BinaryFormatterUnsealedInternalSerializationConstructorPositive", BinaryFormatter);
            Assert.Equal(
                "BinaryFormatterUnsealedInternalSerializationConstructorPositive::.ctor(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)",
                unsealedInternalTrigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Binary_formatter_profile_recognizes_ideserializationcallback()
        {
            var trigger = fixture.GetSingleTrigger("BinaryFormatterInterfaceCallbackPositive", BinaryFormatter);

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("BinaryFormatterInterfaceCallbackPositive::OnDeserialization(System.Object)", trigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("BinaryFormatterInterfaceCallbackNegative", BinaryFormatter));
        }

        [Fact]
        public void Binary_formatter_profile_recognizes_iobjectreference()
        {
            var trigger = fixture.GetSingleTrigger("BinaryFormatterObjectReferencePositive", BinaryFormatter);

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("BinaryFormatterObjectReferencePositive::GetRealObject(System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("BinaryFormatterObjectReferenceNegative", BinaryFormatter));
        }

        [Fact]
        public void Binary_formatter_profile_expands_inherited_ideserializationcallback_to_derived_root()
        {
            var trigger = fixture.GetSingleTrigger("BinaryFormatterInheritedInterfaceCallbackPositive", BinaryFormatter);

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("BinaryFormatterInterfaceCallbackBase", trigger.TriggerDeclaredOnTypeName);
            Assert.Equal("BinaryFormatterInterfaceCallbackBase::OnDeserialization(System.Object)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Binary_formatter_profile_expands_inherited_iobjectreference_to_derived_root()
        {
            var trigger = fixture.GetSingleTrigger("BinaryFormatterInheritedObjectReferencePositive", BinaryFormatter);

            Assert.Equal(TriggerKind.DeserializationCallback, trigger.TriggerKind);
            Assert.Equal("BinaryFormatterObjectReferenceBase", trigger.TriggerDeclaredOnTypeName);
            Assert.Equal("BinaryFormatterObjectReferenceBase::GetRealObject(System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Json_dot_net_profile_rejects_binary_formatter_only_interface_callbacks()
        {
            Assert.False(fixture.HasFinding("BinaryFormatterInterfaceCallbackPositive", JsonDotNet));
            Assert.False(fixture.HasFinding("BinaryFormatterObjectReferencePositive", JsonDotNet));
        }

        [Fact]
        public void Binary_formatter_profile_rejects_json_dot_net_public_property_setter_triggers()
        {
            Assert.False(fixture.HasFinding("SetterPositive", BinaryFormatter));
            Assert.False(fixture.HasFinding("Employee", BinaryFormatter));
        }

        [Fact]
        public void Json_dot_net_profile_recognizes_serialization_constructor_when_type_is_serializable_and_iserializable()
        {
            var trigger = fixture.GetSingleTrigger("BinaryFormatterSerializationConstructorPositive", JsonDotNet);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("BinaryFormatterSerializationConstructorPositive::.ctor(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("BinaryFormatterSerializationConstructorNegative", JsonDotNet));
            Assert.False(fixture.HasFinding("BinaryFormatterNonSerializableISerializableNegative", JsonDotNet));
        }

        [Fact]
        public void Json_dot_net_profile_prefers_serialization_constructor_over_ordinary_constructor_for_iserializable_types()
        {
            var trigger = fixture.GetSingleTrigger("JsonNetSerializableISerializablePrefersSerializationConstructorPositive", JsonDotNet);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("JsonNetSerializableISerializablePrefersSerializationConstructorPositive::.ctor(System.Runtime.Serialization.SerializationInfo, System.Runtime.Serialization.StreamingContext)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Json_dot_net_profile_recognizes_opted_in_non_public_property_setters()
        {
            var trigger = fixture.GetSingleTrigger("JsonNetNonPublicSetterJsonPropertyPositive", JsonDotNet);

            Assert.Equal(TriggerKind.NonPublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("JsonNetNonPublicSetterJsonPropertyPositive::set_Value(System.Int32)", trigger.TriggerMethodDisplay);
            Assert.False(fixture.HasFinding("JsonNetNonPublicSetterWithoutOptInNegative", JsonDotNet));
        }

        [Fact]
        public void Json_dot_net_profile_recognizes_datamember_opted_in_non_public_property_setters()
        {
            var trigger = fixture.GetSingleTrigger("JsonNetNonPublicSetterDataMemberPositive", JsonDotNet);

            Assert.Equal(TriggerKind.NonPublicPropertySetter, trigger.TriggerKind);
            Assert.Equal("JsonNetNonPublicSetterDataMemberPositive::set_Value(System.Int32)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Message_pack_typeless_profile_recognizes_private_parameterized_constructor_roots()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessPrivateParameterizedConstructorPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("MessagePackTypelessPrivateParameterizedConstructorPositive::.ctor(System.String)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Message_pack_typeless_profile_uses_index_based_constructor_binding_for_annotated_int_key_models()
        {
            var trigger = fixture.GetSingleTrigger("MessagePackTypelessIndexConstructorPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.Constructor, trigger.TriggerKind);
            Assert.Equal("MessagePackTypelessIndexConstructorPositive::.ctor(System.String, System.Int32)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Message_pack_typeless_profile_rejects_roots_without_a_matching_constructor()
        {
            Assert.False(fixture.HasFinding("MessagePackTypelessNoMatchNegative", MessagePackTypeless));
        }

        [Fact]
        public void Message_pack_typeless_profile_rejects_annotated_private_serialization_constructor_paths_that_fail_at_runtime()
        {
            Assert.False(fixture.HasFinding("MessagePackTypelessAnnotatedPrivateSerializationConstructorNegative", MessagePackTypeless));
        }

        [Fact]
        public void Xml_serializer_profile_recognizes_ixmlserializable_readxml_as_a_custom_deserialization_method()
        {
            var trigger = fixture.GetSingleTrigger("XmlSerializerCustomReadXmlPositive", XmlSerializer);

            Assert.Equal(TriggerKind.CustomDeserializationMethod, trigger.TriggerKind);
            Assert.Equal("XmlSerializerCustomReadXmlPositive::ReadXml(System.Xml.XmlReader)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Xml_serializer_profile_does_not_report_formatter_style_callbacks()
        {
            Assert.False(fixture.HasFinding("OnDeserializedPositive", XmlSerializer));
            Assert.False(fixture.HasFinding("BinaryFormatterInterfaceCallbackPositive", XmlSerializer));
            Assert.False(fixture.HasFinding("BinaryFormatterObjectReferencePositive", XmlSerializer));
        }

        [Fact]
        public void Message_pack_typeless_profile_keeps_finalizers_enabled()
        {
            var trigger = fixture.GetSingleTrigger("FinalizerPositive", MessagePackTypeless);

            Assert.Equal(TriggerKind.Finalizer, trigger.TriggerKind);
            Assert.Equal("FinalizerPositive::Finalize()", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Xml_serializer_profile_rejects_ordinary_roots_with_interface_typed_members()
        {
            Assert.False(fixture.HasFinding("XmlSerializerInterfaceMemberNegative", XmlSerializer));
        }

        [Fact]
        public void Xml_serializer_profile_rejects_ordinary_roots_with_system_type_members()
        {
            Assert.False(fixture.HasFinding("XmlSerializerTypeMemberNegative", XmlSerializer));
        }

        [Fact]
        public void Xml_serializer_profile_keeps_ixmlserializable_roots_eligible_when_ordinary_member_filter_would_fail()
        {
            var trigger = fixture.GetSingleTrigger("XmlSerializerCustomReadXmlInterfaceMemberPositive", XmlSerializer);

            Assert.Equal(TriggerKind.CustomDeserializationMethod, trigger.TriggerKind);
            Assert.Equal("XmlSerializerCustomReadXmlInterfaceMemberPositive::ReadXml(System.Xml.XmlReader)", trigger.TriggerMethodDisplay);
        }

        [Fact]
        public void Serializer_profiles_can_be_loaded_from_an_explicit_file_path()
        {
            var profilePath = Path.Combine(AppContext.BaseDirectory, "serializer-profiles", "PublicTwoStringConstructor.profile.json");
            var profile = SerializerProfiles.Resolve(profilePath);

            Assert.Equal("PublicTwoStringConstructor", profile.Name);
            var activationPolicy = Assert.Single(profile.ActivationPolicies);
            Assert.Equal(ActivationMode.ExactSignatureConstructor, activationPolicy.Mode);
        }

        private static void AssertAllRootTypeVisibilities(IReadOnlyList<RootTypeVisibility> visibilities)
        {
            Assert.Equal(
                [
                    RootTypeVisibility.Public,
                    RootTypeVisibility.Internal,
                    RootTypeVisibility.Protected,
                    RootTypeVisibility.ProtectedInternal,
                    RootTypeVisibility.PrivateProtected,
                    RootTypeVisibility.Private
                ],
                visibilities);
        }
    }
}
