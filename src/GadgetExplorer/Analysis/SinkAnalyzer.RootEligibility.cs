/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis
{
    public static partial class SinkAnalyzer
    {
        /// <summary>
        /// Computes the set of eligible serializer root classes.
        /// </summary>
        private static HashSet<TypeId> ComputeEligibleRootClassIds(AnalysisIndex index, SerializerProfile profile)
        {
            var eligibilityCache = new Dictionary<TypeId, bool>();
            var activeStack = new HashSet<TypeId>();
            var eligibleRootClassIds = new HashSet<TypeId>();

            foreach (TypeRecord type in index.Types.Where(type => type is { IsClass: true, IsAbstract: false }))
            {
                if (IsEligibleRootClass(index, type.Id, profile, eligibilityCache, activeStack))
                {
                    eligibleRootClassIds.Add(type.Id);
                }
            }

            return eligibleRootClassIds;
        }

        /// <summary>
        /// Determines whether a type is eligible as a serializer root class.
        /// </summary>
        private static bool IsEligibleRootClass(
            AnalysisIndex index,
            TypeId typeId,
            SerializerProfile profile,
            IDictionary<TypeId, bool> eligibilityCache,
            ISet<TypeId> activeStack)
        {
            if (eligibilityCache.TryGetValue(typeId, out bool cached))
            {
                return cached;
            }

            TypeRecord type = index.GetType(typeId);
            if (!type.IsClass ||
                type.IsAbstract ||
                (profile.AllowedRootTypeVisibilities.Count > 0 &&
                 !profile.AllowedRootTypeVisibilities.Any(allowedVisibility => MatchesRootTypeVisibility(type, allowedVisibility))))
            {
                eligibilityCache[typeId] = false;
                return false;
            }

            if (!SatisfiesRootTypeRequirements(type.TypeDef, profile.RootTypeEligibility.Requirements))
            {
                eligibilityCache[typeId] = false;
                return false;
            }

            if (!activeStack.Add(typeId))
            {
                eligibilityCache[typeId] = false;
                return false;
            }

            try
            {
                bool isEligible = profile.ActivationPolicies.Any(policy =>
                    IsEligibleForActivationPolicy(index, type.TypeDef, profile, policy, eligibilityCache, activeStack));
                eligibilityCache[typeId] = isEligible;
                return isEligible;
            }
            finally
            {
                activeStack.Remove(typeId);
            }
        }

        /// <summary>
        /// Determines whether a type is eligible under a specific activation policy.
        /// </summary>
        private static bool IsEligibleForActivationPolicy(
            AnalysisIndex index,
            TypeDef type,
            SerializerProfile profile,
            ActivationPolicy policy,
            IDictionary<TypeId, bool> eligibilityCache,
            ISet<TypeId> activeStack)
        {
            if (!MatchesActivationPolicyTypeRequirements(type, policy))
            {
                return false;
            }

            if (policy.Mode == ActivationMode.UninitializedObject)
            {
                return SatisfiesDirectGenericTypeRequirements(index, type, profile, activeStack);
            }

            MethodDef[] instanceConstructors = GetInstanceConstructors(type);
            MethodDef? selectedConstructor = TrySelectConstructorForActivationPolicy(type, instanceConstructors, policy);
            if (selectedConstructor is null)
            {
                return false;
            }

            if (policy.Mode == ActivationMode.SerializationConstructor)
            {
                return true;
            }

            if (!SatisfiesOrdinaryObjectMappingRequirements(type, policy.OrdinaryObjectMapping))
            {
                return false;
            }

            return (selectedConstructor.MethodSig?.Params.Count ?? 0) == 0 ||
                   selectedConstructor.MethodSig!.Params.All(parameterType =>
                       IsEligibleConstructorParameter(index, parameterType, profile, eligibilityCache, activeStack));
        }

        /// <summary>
        /// Determines whether a constructor parameter is serializer-compatible.
        /// </summary>
        private static bool IsEligibleConstructorParameter(
            AnalysisIndex index,
            TypeSig parameterType,
            SerializerProfile profile,
            IDictionary<TypeId, bool> eligibilityCache,
            ISet<TypeId> activeStack)
        {
            if (!SatisfiesTypeShapeRequirements(index, parameterType, profile, activeStack, requireCurrentTypeEligibility: true))
            {
                return false;
            }

            TypeSig? normalizedParameterType = AnalysisIndex.GetTypeDefinitionSignature(parameterType);
            if (normalizedParameterType is null || IsLeafConstructorParameterType(normalizedParameterType))
            {
                return true;
            }

            TypeDef? resolvedType = normalizedParameterType.ToTypeDefOrRef()?.ResolveTypeDef();
            if (resolvedType is null)
            {
                return false;
            }

            return index.TryGetTypeId(resolvedType, out TypeId matchingTypeId) &&
                   IsEligibleRootClass(index, matchingTypeId, profile, eligibilityCache, activeStack);
        }

        /// <summary>
        /// Determines whether a root class's direct generic shape is compatible with the serializer profile.
        /// </summary>
        private static bool SatisfiesDirectGenericTypeRequirements(
            AnalysisIndex index,
            TypeDef type,
            SerializerProfile profile,
            ISet<TypeId> activeStack)
        {
            if (type.HasGenericParameters)
            {
                return false;
            }

            return EnumerateDirectTypeSignatures(type)
                .All(directTypeSignature => SatisfiesTypeShapeRequirements(
                    index,
                    directTypeSignature,
                    profile,
                    activeStack,
                    requireCurrentTypeEligibility: false));
        }

        /// <summary>
        /// Enumerates the direct type relationships declared on a type definition.
        /// </summary>
        private static IEnumerable<TypeSig> EnumerateDirectTypeSignatures(TypeDef type)
        {
            var baseTypeSig = AnalysisIndex.ToTypeSig(type.BaseType);
            if (baseTypeSig is not null)
            {
                yield return baseTypeSig;
            }

            foreach (InterfaceImpl? implementedInterface in type.Interfaces)
            {
                var interfaceTypeSig = AnalysisIndex.ToTypeSig(implementedInterface.Interface);
                if (interfaceTypeSig is not null)
                {
                    yield return interfaceTypeSig;
                }
            }
        }

        /// <summary>
        /// Determines whether a type signature's contained generic arguments satisfy root requirements.
        /// </summary>
        private static bool SatisfiesTypeShapeRequirements(
            AnalysisIndex index,
            TypeSig? typeSignature,
            SerializerProfile profile,
            ISet<TypeId> activeStack,
            bool requireCurrentTypeEligibility)
        {
            switch (typeSignature)
            {
                case null:
                    return false;
                case ByRefSig byRefSig:
                    return SatisfiesTypeShapeRequirements(index, byRefSig.Next, profile, activeStack, requireCurrentTypeEligibility);
                case PtrSig ptrSig:
                    return SatisfiesTypeShapeRequirements(index, ptrSig.Next, profile, activeStack, requireCurrentTypeEligibility);
                case SZArraySig szArraySig:
                    return SatisfiesTypeShapeRequirements(index, szArraySig.Next, profile, activeStack, requireCurrentTypeEligibility);
                case ArraySig arraySig:
                    return SatisfiesTypeShapeRequirements(index, arraySig.Next, profile, activeStack, requireCurrentTypeEligibility);
                case CModOptSig cModOptSig:
                    return SatisfiesTypeShapeRequirements(index, cModOptSig.Next, profile, activeStack, requireCurrentTypeEligibility);
                case CModReqdSig cModReqdSig:
                    return SatisfiesTypeShapeRequirements(index, cModReqdSig.Next, profile, activeStack, requireCurrentTypeEligibility);
                case PinnedSig pinnedSig:
                    return SatisfiesTypeShapeRequirements(index, pinnedSig.Next, profile, activeStack, requireCurrentTypeEligibility);
                case GenericVar:
                case GenericMVar:
                    return false;
                case GenericInstSig genericInstSig:
                    {
                        if (requireCurrentTypeEligibility)
                        {
                            TypeDef? genericType = genericInstSig.GenericType.ToTypeDefOrRef()?.ResolveTypeDef();
                            if (genericType is null ||
                                !SatisfiesRootTypeRequirements(genericType, profile.RootTypeEligibility.Requirements))
                            {
                                return false;
                            }
                        }

                        return genericInstSig.GenericArguments.All(argument =>
                            SatisfiesTypeShapeRequirements(index, argument, profile, activeStack, requireCurrentTypeEligibility: true));
                    }
            }

            if (!requireCurrentTypeEligibility)
            {
                return true;
            }

            TypeSig? normalizedType = AnalysisIndex.GetTypeDefinitionSignature(typeSignature);
            if (normalizedType is null || IsLeafConstructorParameterType(normalizedType))
            {
                return true;
            }

            TypeDef? resolvedType = normalizedType.ToTypeDefOrRef()?.ResolveTypeDef();
            if (resolvedType is null || !SatisfiesRootTypeRequirements(resolvedType, profile.RootTypeEligibility.Requirements))
            {
                return false;
            }

            if (!index.TryGetTypeId(resolvedType, out TypeId resolvedTypeId))
            {
                return true;
            }

            return !activeStack.Contains(resolvedTypeId);
        }

        /// <summary>
        /// Determines whether a constructor parameter type should be treated as a leaf during recursive eligibility checks.
        /// Primitive scalars, strings, and value types all stop recursion because the analyzer does not model internal state population for them.
        /// </summary>
        private static bool IsLeafConstructorParameterType(TypeSig type)
        {
            TypeSig? normalizedType = AnalysisIndex.GetTypeDefinitionSignature(type);
            if (normalizedType is null)
            {
                return true;
            }

            return normalizedType.ElementType is ElementType.Boolean or ElementType.Char or ElementType.I1 or ElementType.U1
                or ElementType.I2 or ElementType.U2 or ElementType.I4 or ElementType.U4 or ElementType.I8 or ElementType.U8
                or ElementType.R4 or ElementType.R8 or ElementType.I or ElementType.U or ElementType.String
                or ElementType.ValueType;
        }

        /// <summary>
        /// Determines whether a type definition satisfies the configured root-type requirements.
        /// </summary>
        private static bool SatisfiesRootTypeRequirements(TypeDef type, IReadOnlyList<RootTypeRequirement> requirements)
        {
            return requirements
                .All(requirement => requirement.Kind switch
                {
                    RootTypeRequirementKind.HasAttribute => HasRequiredAttribute(type, requirement.TypeName),
                    RootTypeRequirementKind.LacksAttribute => !HasRequiredAttribute(type, requirement.TypeName),
                    _ => false
                });
        }

        /// <summary>
        /// Determines whether a type satisfies a policy's activation-specific requirements.
        /// </summary>
        private static bool MatchesActivationPolicyTypeRequirements(TypeDef type, ActivationPolicy policy)
            => policy.RequiredDeclaringTypeInterfaceNames.All(interfaceTypeName => ImplementsInterface(type, interfaceTypeName)) &&
               SatisfiesRootTypeRequirements(type, policy.Requirements);

        /// <summary>
        /// Determines whether a type's ordinary public member surface is compatible with an activation policy.
        /// </summary>
        private static bool SatisfiesOrdinaryObjectMappingRequirements(TypeDef type, OrdinaryObjectMappingPolicy policy)
        {
            if (policy.IgnoredDeclaringTypeInterfaceNames.Any(interfaceTypeName => ImplementsInterface(type, interfaceTypeName)))
            {
                return true;
            }

            if (!policy.RejectPublicFieldsOrSettablePropertiesOfInterfaceTypes &&
                policy.RejectedPublicFieldOrSettablePropertyTypeNames.Count == 0)
            {
                return true;
            }

            return EnumerateOrdinaryMappedMemberTypes(type).All(memberType =>
                !IsRejectedInterfaceTypedMember(memberType, policy) &&
                !IsRejectedConfiguredMemberType(memberType, policy));
        }

        private static IEnumerable<TypeSig> EnumerateOrdinaryMappedMemberTypes(TypeDef type)
        {
            foreach (FieldDef field in type.Fields)
            {
                if (field is { IsStatic: false, IsPublic: true } && field.FieldSig?.Type is { } fieldType)
                {
                    yield return fieldType;
                }
            }

            foreach (PropertyDef property in type.Properties)
            {
                if (property.SetMethod is { IsStatic: false, IsPublic: true } && property.PropertySig?.RetType is { } propertyType)
                {
                    yield return propertyType;
                }
            }
        }

        private static bool IsRejectedInterfaceTypedMember(TypeSig memberType, OrdinaryObjectMappingPolicy policy)
        {
            if (!policy.RejectPublicFieldsOrSettablePropertiesOfInterfaceTypes)
            {
                return false;
            }

            TypeDef? topLevelType = ResolveTopLevelTypeDef(memberType);
            return topLevelType?.IsInterface == true;
        }

        private static bool IsRejectedConfiguredMemberType(TypeSig memberType, OrdinaryObjectMappingPolicy policy)
            => policy.RejectedPublicFieldOrSettablePropertyTypeNames.Any(typeName =>
                AnalysisIndex.MatchesConfiguredTypeName(memberType, typeName));

        private static TypeDef? ResolveTopLevelTypeDef(TypeSig? typeSignature)
        {
            while (typeSignature is not null)
            {
                switch (typeSignature)
                {
                    case GenericInstSig genericInstSig:
                        return genericInstSig.GenericType.ToTypeDefOrRef()?.ResolveTypeDef();
                    case CModOptSig cModOptSig:
                        typeSignature = cModOptSig.Next;
                        continue;
                    case CModReqdSig cModReqdSig:
                        typeSignature = cModReqdSig.Next;
                        continue;
                    case PinnedSig pinnedSig:
                        typeSignature = pinnedSig.Next;
                        continue;
                    default:
                        return typeSignature.ToTypeDefOrRef()?.ResolveTypeDef();
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a type satisfies an attribute-based root-type requirement.
        /// </summary>
        private static bool HasRequiredAttribute(TypeDef type, string attributeTypeName)
        {
            if (string.Equals(attributeTypeName, "System.SerializableAttribute", StringComparison.Ordinal))
            {
                return type.IsSerializable;
            }

            return type.CustomAttributes.Any(attribute =>
                string.Equals(attribute.AttributeType.FullName, attributeTypeName, StringComparison.Ordinal));
        }

        private static bool MatchesRootTypeVisibility(TypeRecord type, RootTypeVisibility allowedVisibility)
            => allowedVisibility switch
            {
                RootTypeVisibility.PubliclyVisible => IsPubliclyVisible(type.TypeDef),
                _ => type.RootVisibility == allowedVisibility
            };

        private static bool IsPubliclyVisible(TypeDef type)
        {
            if (type.DeclaringType is { } declaringType)
            {
                return type.IsNestedPublic && IsPubliclyVisible(declaringType);
            }

            return type.IsPublic;
        }
    }
}

