/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Configuration
{
    /// <summary>
    /// Describes the serializer-specific root, activation, trigger, and callback rules used by the analyzer.
    /// </summary>
    /// <param name="Name">The profile name.</param>
    /// <param name="RootTypeEligibility">The root-type eligibility policy.</param>
    /// <param name="TriggerPolicy">The serializer trigger policy.</param>
    /// <param name="ActivationPolicies">The supported object-activation policies.</param>
    /// <param name="Callbacks">The callback behavior.</param>
    /// <param name="CustomDeserializationMethods">The serializer-specific custom deserialization method behavior.</param>
    public sealed record SerializerProfile(
        string Name,
        RootTypeEligibilityPolicy RootTypeEligibility,
        SerializerTriggerPolicy TriggerPolicy,
        IReadOnlyList<ActivationPolicy> ActivationPolicies,
        CallbackPolicy Callbacks,
        CustomDeserializationMethodPolicy? CustomDeserializationMethods = null)
    {
        /// <summary>
        /// Gets the serializer-specific custom deserialization method behavior.
        /// </summary>
        public CustomDeserializationMethodPolicy CustomDeserializationMethods { get; } = CustomDeserializationMethods ?? new();

        /// <summary>
        /// Gets whether the profile exposes any constructor-based trigger path.
        /// </summary>
        public bool SupportsConstructorTriggers => TriggerPolicy.SupportsConstructors ?? ActivationPolicies.Any(policy => policy.ExposesConstructorTrigger);

        /// <summary>
        /// Gets whether public instance property getters are exposed as serializer triggers.
        /// </summary>
        public bool SupportsPublicPropertyGetterTriggers => AllowedPropertyGetterVisibilities.Contains(MethodVisibility.Public);

        /// <summary>
        /// Gets the directly allowed property getter visibilities.
        /// </summary>
        public IReadOnlyList<MethodVisibility> AllowedPropertyGetterVisibilities => TriggerPolicy.AllowedPropertyGetterVisibilities;

        /// <summary>
        /// Gets whether public instance property setters are treated as serializer triggers.
        /// </summary>
        public bool SupportsPublicPropertySetterTriggers => AllowedPropertySetterVisibilities.Contains(MethodVisibility.Public);

        /// <summary>
        /// Gets the attribute-based opt-in rules for non-public property setter triggers.
        /// </summary>
        public IReadOnlyList<string> NonPublicPropertySetterOptInAttributeTypeNames => TriggerPolicy.NonPublicPropertySetterOptInAttributeTypeNames;

        /// <summary>
        /// Gets the directly allowed property setter visibilities.
        /// </summary>
        public IReadOnlyList<MethodVisibility> AllowedPropertySetterVisibilities => TriggerPolicy.AllowedPropertySetterVisibilities;

        /// <summary>
        /// Gets whether deserialization callbacks can be triggered by the serializer.
        /// </summary>
        public bool SupportsDeserializationCallbackTriggers
            => Callbacks.AttributeCallbacks.Count > 0 ||
               Callbacks.InterfaceCallbacks.Count > 0;

        /// <summary>
        /// Gets whether serializer-specific custom deserialization methods can be triggered.
        /// </summary>
        public bool SupportsCustomDeserializationMethodTriggers => CustomDeserializationMethods.InterfaceMethods.Count > 0;

        /// <summary>
        /// Gets whether finalizers should be treated as serializer triggers.
        /// </summary>
        public bool SupportsFinalizerTriggers => TriggerPolicy.SupportsFinalizers;

        /// <summary>
        /// Gets the explicitly allowed root-type visibility matchers.
        /// </summary>
        public IReadOnlyList<RootTypeVisibility> AllowedRootTypeVisibilities => RootTypeEligibility.AllowedTypeVisibilities;
    }

    /// <summary>
    /// Describes serializer root-type eligibility rules.
    /// </summary>
    /// <param name="AllowedTypeVisibilities">The explicitly allowed root-type visibility matchers.</param>
    /// <param name="Requirements">Additional root-type requirements that must be satisfied.</param>
    public sealed record RootTypeEligibilityPolicy(
        IReadOnlyList<RootTypeVisibility>? AllowedTypeVisibilities = null,
        IReadOnlyList<RootTypeRequirement>? Requirements = null)
    {
        /// <summary>
        /// Gets the explicitly allowed root-type visibility matchers.
        /// </summary>
        public IReadOnlyList<RootTypeVisibility> AllowedTypeVisibilities { get; } = AllowedTypeVisibilities ?? [];

        /// <summary>
        /// Gets the additional root-type requirements.
        /// </summary>
        public IReadOnlyList<RootTypeRequirement> Requirements { get; } = Requirements ?? [];
    }

    /// <summary>
    /// Describes one supported root-type visibility matcher.
    /// </summary>
    public enum RootTypeVisibility
    {
        PubliclyVisible,
        Public,
        Internal,
        Protected,
        ProtectedInternal,
        PrivateProtected,
        Private
    }

    /// <summary>
    /// Describes one additional root-type requirement enforced by the analyzer.
    /// </summary>
    /// <param name="Kind">The requirement kind.</param>
    /// <param name="TypeName">The referenced type name for the requirement.</param>
    public sealed record RootTypeRequirement(
        RootTypeRequirementKind Kind,
        string TypeName);

    /// <summary>
    /// Describes the supported root-type requirement kinds.
    /// </summary>
    public enum RootTypeRequirementKind
    {
        HasAttribute,
        LacksAttribute
    }

    /// <summary>
    /// Describes auxiliary trigger rules that are not implied by activation or callbacks.
    /// </summary>
    /// <param name="SupportsFinalizers">Whether finalizers should be treated as triggers for the serializer.</param>
    /// <param name="SupportsConstructors">Whether constructors should be treated as root triggers when the serializer can activate objects through constructors.</param>
    /// <param name="AllowedPropertyGetterVisibilities">The instance property getter visibilities that should be treated as root triggers.</param>
    /// <param name="AllowedPropertySetterVisibilities">The instance property setter visibilities that should be treated as root triggers.</param>
    /// <param name="NonPublicPropertySetterOptInAttributeTypeNames">The property or accessor attributes that opt non-public setters into trigger discovery.</param>
    public sealed record SerializerTriggerPolicy(
        bool SupportsFinalizers,
        bool? SupportsConstructors = null,
        IReadOnlyList<MethodVisibility>? AllowedPropertyGetterVisibilities = null,
        IReadOnlyList<MethodVisibility>? AllowedPropertySetterVisibilities = null,
        IReadOnlyList<string>? NonPublicPropertySetterOptInAttributeTypeNames = null)
    {
        /// <summary>
        /// Gets the allowed property getter visibilities.
        /// </summary>
        public IReadOnlyList<MethodVisibility> AllowedPropertyGetterVisibilities { get; } = AllowedPropertyGetterVisibilities ?? [];

        /// <summary>
        /// Gets the allowed property setter visibilities.
        /// </summary>
        public IReadOnlyList<MethodVisibility> AllowedPropertySetterVisibilities { get; } = AllowedPropertySetterVisibilities ?? [];

        /// <summary>
        /// Gets the property or accessor attributes that opt non-public setters into trigger discovery.
        /// </summary>
        public IReadOnlyList<string> NonPublicPropertySetterOptInAttributeTypeNames { get; } = NonPublicPropertySetterOptInAttributeTypeNames ?? [];
    }

    /// <summary>
    /// Describes one supported object-activation policy for a serializer.
    /// </summary>
    /// <param name="Mode">The activation mode.</param>
    /// <param name="ConstructorSelectionRules">The ordered constructor-selection rules for constructor-selection activation.</param>
    /// <param name="PreferredConstructorAttributeTypeNames">The constructor attributes that explicitly select a constructor.</param>
    /// <param name="AllowedConstructorVisibilities">The constructor visibilities allowed for constructor-selection activation.</param>
    /// <param name="ConstructorBindingModes">The constructor-parameter binding modes supported by best-match constructor selection.</param>
    /// <param name="IndexedMemberAttributeTypeNames">The member attributes that indicate index-based binding should be preferred when present.</param>
    /// <param name="ExactConstructorSignature">The required exact constructor signature, when applicable.</param>
    /// <param name="Requirements">Additional root-type requirements enforced only for this activation policy.</param>
    /// <param name="SerializationConstructorSignature">The special serialization-constructor signature, when applicable.</param>
    /// <param name="OrdinaryObjectMapping">The ordinary object-mapping compatibility rules enforced only for this activation policy.</param>
    public sealed record ActivationPolicy(
        ActivationMode Mode,
        IReadOnlyList<ConstructorSelectionRule>? ConstructorSelectionRules = null,
        IReadOnlyList<string>? PreferredConstructorAttributeTypeNames = null,
        IReadOnlyList<MethodVisibility>? AllowedConstructorVisibilities = null,
        IReadOnlyList<ConstructorBindingMode>? ConstructorBindingModes = null,
        IReadOnlyList<string>? IndexedMemberAttributeTypeNames = null,
        IReadOnlyList<string>? RequiredDeclaringTypeInterfaceNames = null,
        ExactConstructorSignature? ExactConstructorSignature = null,
        SerializationConstructorSignature? SerializationConstructorSignature = null,
        IReadOnlyList<RootTypeRequirement>? Requirements = null,
        OrdinaryObjectMappingPolicy? OrdinaryObjectMapping = null)
    {
        /// <summary>
        /// Gets the ordered constructor-selection rules.
        /// </summary>
        public IReadOnlyList<ConstructorSelectionRule> ConstructorSelectionRules { get; } = ConstructorSelectionRules ?? [];

        /// <summary>
        /// Gets the preferred constructor-selection attributes.
        /// </summary>
        public IReadOnlyList<string> PreferredConstructorAttributeTypeNames { get; } = PreferredConstructorAttributeTypeNames ?? [];

        /// <summary>
        /// Gets the constructor visibilities allowed for constructor-selection activation.
        /// </summary>
        public IReadOnlyList<MethodVisibility> AllowedConstructorVisibilities { get; } = AllowedConstructorVisibilities ?? [];

        /// <summary>
        /// Gets the constructor-parameter binding modes supported by best-match constructor selection.
        /// </summary>
        public IReadOnlyList<ConstructorBindingMode> ConstructorBindingModes { get; } = ConstructorBindingModes ?? [];

        /// <summary>
        /// Gets the member attributes that indicate index-based binding should be preferred.
        /// </summary>
        public IReadOnlyList<string> IndexedMemberAttributeTypeNames { get; } = IndexedMemberAttributeTypeNames ?? [];

        /// <summary>
        /// Gets the interface requirements that the declaring type must satisfy for this activation policy.
        /// </summary>
        public IReadOnlyList<string> RequiredDeclaringTypeInterfaceNames { get; } = RequiredDeclaringTypeInterfaceNames ?? [];

        /// <summary>
        /// Gets the additional root-type requirements that only apply to this activation policy.
        /// </summary>
        public IReadOnlyList<RootTypeRequirement> Requirements { get; } = Requirements ?? [];

        /// <summary>
        /// Gets the ordinary object-mapping compatibility rules for this activation policy.
        /// </summary>
        public OrdinaryObjectMappingPolicy OrdinaryObjectMapping { get; } = OrdinaryObjectMapping ?? new();

        /// <summary>
        /// Gets whether this activation policy surfaces a constructor trigger.
        /// </summary>
        public bool ExposesConstructorTrigger => Mode is ActivationMode.ConstructorSelection or ActivationMode.ExactSignatureConstructor or ActivationMode.SerializationConstructor;
    }

    /// <summary>
    /// Describes the supported activation modes.
    /// </summary>
    public enum ActivationMode
    {
        ConstructorSelection,
        ExactSignatureConstructor,
        UninitializedObject,
        SerializationConstructor
    }

    /// <summary>
    /// Describes one ordered constructor-selection rule.
    /// </summary>
    /// <param name="When">The constructor-shape condition that must match.</param>
    /// <param name="Target">The constructor target to select.</param>
    public sealed record ConstructorSelectionRule(
        ConstructorSelectionConstraint When,
        ConstructorSelectionTarget Target);

    /// <summary>
    /// Describes the constructor-shape condition for a rule.
    /// </summary>
    /// <param name="AttributedConstructorCount">The exact count of attributed constructors required, when constrained.</param>
    /// <param name="PublicParameterlessCount">The exact count of public parameterless constructors required, when constrained.</param>
    /// <param name="NonPublicParameterlessCount">The exact count of non-public parameterless constructors required, when constrained.</param>
    /// <param name="PublicParameterizedCount">The exact count of public parameterized constructors required, when constrained.</param>
    public sealed record ConstructorSelectionConstraint(
        int? AttributedConstructorCount = null,
        int? PublicParameterlessCount = null,
        int? NonPublicParameterlessCount = null,
        int? PublicParameterizedCount = null);

    /// <summary>
    /// Describes the constructor target selected by a rule.
    /// </summary>
    public enum ConstructorSelectionTarget
    {
        Attributed,
        BestMatch,
        PublicParameterless,
        NonPublicParameterless,
        SinglePublicParameterized
    }

    /// <summary>
    /// Describes how best-match constructor parameters bind to serialized members.
    /// </summary>
    public enum ConstructorBindingMode
    {
        Name,
        Index
    }

    /// <summary>
    /// Describes an exact constructor signature requirement.
    /// </summary>
    /// <param name="RequirePublic">Whether the constructor must be public.</param>
    /// <param name="ParameterTypeNames">The exact parameter type names.</param>
    public sealed record ExactConstructorSignature(
        bool RequirePublic,
        IReadOnlyList<string> ParameterTypeNames);

    /// <summary>
    /// Describes a special serialization-constructor signature.
    /// </summary>
    /// <param name="VisibilityPolicy">The allowed constructor visibilities for sealed and unsealed declaring types.</param>
    /// <param name="ParameterTypeNames">The exact parameter type names.</param>
    public sealed record SerializationConstructorSignature(
        SerializationConstructorVisibilityPolicy VisibilityPolicy,
        IReadOnlyList<string> ParameterTypeNames);

    /// <summary>
    /// Describes the allowed serialization-constructor visibilities for sealed and unsealed declaring types.
    /// </summary>
    /// <param name="SealedTypeAllowedVisibilities">The allowed constructor visibilities when the declaring type is sealed.</param>
    /// <param name="UnsealedTypeAllowedVisibilities">The allowed constructor visibilities when the declaring type is not sealed.</param>
    public sealed record SerializationConstructorVisibilityPolicy(
        IReadOnlyList<MethodVisibility> SealedTypeAllowedVisibilities,
        IReadOnlyList<MethodVisibility> UnsealedTypeAllowedVisibilities);

    /// <summary>
    /// Describes a constructor or method visibility that can be matched from metadata.
    /// </summary>
    public enum MethodVisibility
    {
        Public,
        Private,
        Family,
        Assembly,
        FamilyOrAssembly,
        FamilyAndAssembly
    }

    /// <summary>
    /// Describes callback behavior supported by a serializer.
    /// </summary>
    /// <param name="AttributeCallbacks">The supported attribute-based callback method shapes.</param>
    /// <param name="InterfaceCallbacks">The supported interface callback method shapes.</param>
    public sealed record CallbackPolicy(
        IReadOnlyList<AttributeCallbackSignature>? AttributeCallbacks = null,
        IReadOnlyList<InterfaceCallbackSignature>? InterfaceCallbacks = null)
    {
        /// <summary>
        /// Gets the supported attribute callback method shapes.
        /// </summary>
        public IReadOnlyList<AttributeCallbackSignature> AttributeCallbacks { get; } = AttributeCallbacks ?? [];

        /// <summary>
        /// Gets the supported interface callback method shapes.
        /// </summary>
        public IReadOnlyList<InterfaceCallbackSignature> InterfaceCallbacks { get; } = InterfaceCallbacks ?? [];
    }

    /// <summary>
    /// Describes one supported attribute callback method shape.
    /// </summary>
    /// <param name="AttributeTypeName">The required callback attribute type name.</param>
    /// <param name="ParameterTypeNames">The exact callback parameter type names.</param>
    /// <param name="ReturnTypeName">The exact callback return type name.</param>
    public sealed record AttributeCallbackSignature(
        string AttributeTypeName,
        IReadOnlyList<string> ParameterTypeNames,
        string ReturnTypeName);

    /// <summary>
    /// Describes one supported interface callback method shape.
    /// </summary>
    /// <param name="InterfaceTypeName">The required implemented interface type name.</param>
    /// <param name="MethodName">The callback method name.</param>
    /// <param name="ParameterTypeNames">The exact callback parameter type names.</param>
    /// <param name="ReturnTypeName">The exact callback return type name.</param>
    public sealed record InterfaceCallbackSignature(
        string InterfaceTypeName,
        string MethodName,
        IReadOnlyList<string> ParameterTypeNames,
        string ReturnTypeName);

    /// <summary>
    /// Describes serializer-specific custom deserialization methods that are not formatter-style callbacks.
    /// </summary>
    /// <param name="InterfaceMethods">The supported interface-bound custom deserialization methods.</param>
    public sealed record CustomDeserializationMethodPolicy(
        IReadOnlyList<InterfaceMethodSignature>? InterfaceMethods = null)
    {
        /// <summary>
        /// Gets the supported interface-bound custom deserialization methods.
        /// </summary>
        public IReadOnlyList<InterfaceMethodSignature> InterfaceMethods { get; } = InterfaceMethods ?? [];
    }

    /// <summary>
    /// Describes one supported interface-bound custom deserialization method shape.
    /// </summary>
    /// <param name="InterfaceTypeName">The required implemented interface type name.</param>
    /// <param name="MethodName">The custom deserialization method name.</param>
    /// <param name="ParameterTypeNames">The exact method parameter type names.</param>
    /// <param name="ReturnTypeName">The exact method return type name.</param>
    public sealed record InterfaceMethodSignature(
        string InterfaceTypeName,
        string MethodName,
        IReadOnlyList<string> ParameterTypeNames,
        string ReturnTypeName);

    /// <summary>
    /// Describes ordinary object-mapping compatibility rules enforced by a serializer activation policy.
    /// </summary>
    /// <param name="IgnoredDeclaringTypeInterfaceNames">The declaring-type interfaces that bypass the ordinary object-mapping compatibility checks for this activation policy.</param>
    /// <param name="RejectPublicFieldsOrSettablePropertiesOfInterfaceTypes">Whether public instance fields or public settable instance properties whose declared top-level type is an interface should be rejected.</param>
    /// <param name="RejectedPublicFieldOrSettablePropertyTypeNames">The exact public instance field or public settable instance property type names that should be rejected.</param>
    public sealed record OrdinaryObjectMappingPolicy(
        IReadOnlyList<string>? IgnoredDeclaringTypeInterfaceNames = null,
        bool RejectPublicFieldsOrSettablePropertiesOfInterfaceTypes = false,
        IReadOnlyList<string>? RejectedPublicFieldOrSettablePropertyTypeNames = null)
    {
        /// <summary>
        /// Gets the declaring-type interfaces that bypass the ordinary mapping compatibility rules.
        /// </summary>
        public IReadOnlyList<string> IgnoredDeclaringTypeInterfaceNames { get; } = IgnoredDeclaringTypeInterfaceNames ?? [];

        /// <summary>
        /// Gets the exact public member type names rejected by ordinary object mapping.
        /// </summary>
        public IReadOnlyList<string> RejectedPublicFieldOrSettablePropertyTypeNames { get; } = RejectedPublicFieldOrSettablePropertyTypeNames ?? [];
    }
}

