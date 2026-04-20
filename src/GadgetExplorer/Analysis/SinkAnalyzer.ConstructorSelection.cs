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
        /// Selects the constructors used by the serializer profile for a type.
        /// </summary>
        private static IReadOnlyList<MethodDef> GetSelectedConstructors(TypeDef type, SerializerProfile profile)
        {
            MethodDef[] instanceConstructors = GetInstanceConstructors(type);
            foreach (ActivationPolicy policy in profile.ActivationPolicies)
            {
                if (!MatchesActivationPolicyTypeRequirements(type, policy))
                {
                    continue;
                }

                MethodDef? selectedConstructor = TrySelectConstructorForActivationPolicy(type, instanceConstructors, policy);
                if (selectedConstructor is not null)
                {
                    return [selectedConstructor];
                }
            }

            return [];
        }

        /// <summary>
        /// Selects the constructor, if any, for a single activation policy.
        /// </summary>
        private static MethodDef? TrySelectConstructorForActivationPolicy(TypeDef type, MethodDef[] instanceConstructors, ActivationPolicy policy)
        {
            if (instanceConstructors.Length == 0)
            {
                return null;
            }

            return policy.Mode switch
            {
                ActivationMode.ConstructorSelection => TrySelectConstructorFromRules(type, instanceConstructors, policy),
                ActivationMode.ExactSignatureConstructor => TryGetExactSignatureConstructor(instanceConstructors, policy.ExactConstructorSignature),
                ActivationMode.SerializationConstructor => TryGetSerializationConstructor(type, instanceConstructors, policy.SerializationConstructorSignature),
                _ => null
            };
        }

        /// <summary>
        /// Selects a constructor from ordered constructor-selection rules.
        /// </summary>
        private static MethodDef? TrySelectConstructorFromRules(TypeDef type, MethodDef[] instanceConstructors, ActivationPolicy policy)
        {
            foreach (ConstructorSelectionRule rule in policy.ConstructorSelectionRules)
            {
                if (!MatchesConstructorSelectionConstraint(instanceConstructors, policy, rule.When))
                {
                    continue;
                }

                ConstructorSelectionResult selection = rule.Target switch
                {
                    ConstructorSelectionTarget.Attributed => SelectAttributedConstructor(instanceConstructors, policy),
                    ConstructorSelectionTarget.BestMatch => SelectBestMatchConstructor(type, instanceConstructors, policy),
                    ConstructorSelectionTarget.PublicParameterless => SelectConstructor(GetPublicParameterlessConstructor(instanceConstructors)),
                    ConstructorSelectionTarget.NonPublicParameterless => SelectConstructor(GetNonPublicParameterlessConstructor(instanceConstructors)),
                    ConstructorSelectionTarget.SinglePublicParameterized => SelectConstructor(GetSinglePublicParameterizedConstructor(instanceConstructors)),
                    _ => NoConstructorMatch
                };

                if (selection.SelectedConstructor is not null)
                {
                    return selection.SelectedConstructor;
                }

                if (selection.StopProcessing)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether constructor counts satisfy a rule condition.
        /// </summary>
        private static bool MatchesConstructorSelectionConstraint(
            MethodDef[] instanceConstructors,
            ActivationPolicy policy,
            ConstructorSelectionConstraint constraint)
        {
            if (constraint.AttributedConstructorCount is { } attributedConstructorCount &&
                CountAttributedConstructors(instanceConstructors, policy.PreferredConstructorAttributeTypeNames) != attributedConstructorCount)
            {
                return false;
            }

            if (constraint.PublicParameterlessCount is { } publicParameterlessCount &&
                CountConstructors(instanceConstructors, method => method.IsPublic && IsParameterlessConstructor(method)) != publicParameterlessCount)
            {
                return false;
            }

            if (constraint.NonPublicParameterlessCount is { } nonPublicParameterlessCount &&
                CountConstructors(instanceConstructors, method => !method.IsPublic && IsParameterlessConstructor(method)) != nonPublicParameterlessCount)
            {
                return false;
            }

            if (constraint.PublicParameterizedCount is { } publicParameterizedCount &&
                CountConstructors(instanceConstructors, method => method.IsPublic && !IsParameterlessConstructor(method)) != publicParameterizedCount)
            {
                return false;
            }

            return true;
        }

        private static ConstructorSelectionResult SelectAttributedConstructor(
            MethodDef[] instanceConstructors,
            ActivationPolicy policy)
        {
            if (policy.PreferredConstructorAttributeTypeNames.Count == 0)
            {
                return NoConstructorMatch;
            }

            MethodDef[] attributedConstructors = instanceConstructors
                .Where(method => method.CustomAttributes.Any(attribute =>
                    policy.PreferredConstructorAttributeTypeNames.Contains(attribute.AttributeType.FullName, StringComparer.Ordinal)))
                .ToArray();

            if (attributedConstructors.Length == 0)
            {
                return NoConstructorMatch;
            }

            if (attributedConstructors.Length != 1)
            {
                return StopConstructorSelection;
            }

            MethodDef attributedConstructor = attributedConstructors[0];
            if (policy.AllowedConstructorVisibilities.Count > 0 &&
                !MatchesConfiguredMethodVisibility(attributedConstructor, policy.AllowedConstructorVisibilities))
            {
                return StopConstructorSelection;
            }

            return SelectConstructor(attributedConstructor);
        }

        private static ConstructorSelectionResult SelectBestMatchConstructor(
            TypeDef type,
            MethodDef[] instanceConstructors,
            ActivationPolicy policy)
        {
            IReadOnlyList<ConstructorBindingMode> bindingModes = GetBestMatchBindingModes(type, policy);
            if (bindingModes.Count == 0)
            {
                return NoConstructorMatch;
            }

            MethodDef[] candidateConstructors = [.. instanceConstructors
                .Where(method =>
                    policy.AllowedConstructorVisibilities.Count == 0 ||
                    MatchesConfiguredMethodVisibility(method, policy.AllowedConstructorVisibilities))
                .OrderBy(method => method.MDToken.Raw)];

            MethodDef? bestConstructor = null;
            int bestScore = int.MinValue;

            foreach (MethodDef constructor in candidateConstructors)
            {
                foreach (ConstructorBindingMode bindingMode in bindingModes)
                {
                    if (!TryGetBestMatchConstructorScore(type, constructor, policy, bindingMode, out int score))
                    {
                        continue;
                    }

                    if (score > bestScore)
                    {
                        bestConstructor = constructor;
                        bestScore = score;
                    }

                    break;
                }
            }

            return SelectConstructor(bestConstructor);
        }

        private static IReadOnlyList<ConstructorBindingMode> GetBestMatchBindingModes(TypeDef type, ActivationPolicy policy)
        {
            if (policy.ConstructorBindingModes.Count == 0)
            {
                return [];
            }

            bool hasIndexedMember = HasAnyIndexedBindableMember(type, policy);
            var orderedModes = new List<ConstructorBindingMode>(policy.ConstructorBindingModes.Count);

            if (hasIndexedMember && policy.ConstructorBindingModes.Contains(ConstructorBindingMode.Index))
            {
                orderedModes.Add(ConstructorBindingMode.Index);
            }

            foreach (ConstructorBindingMode mode in policy.ConstructorBindingModes)
            {
                if (mode == ConstructorBindingMode.Index && !hasIndexedMember)
                {
                    continue;
                }

                if (!orderedModes.Contains(mode))
                {
                    orderedModes.Add(mode);
                }
            }

            return orderedModes;
        }

        private static bool TryGetBestMatchConstructorScore(
            TypeDef type,
            MethodDef constructor,
            ActivationPolicy policy,
            ConstructorBindingMode bindingMode,
            out int score)
        {
            score = 0;
            if (constructor.MethodSig is null)
            {
                return false;
            }

            if (constructor.MethodSig.Params.Count == 0)
            {
                return true;
            }

            IReadOnlyList<ConstructorBindableMember> bindableMembers = GetBindableMembers(type, policy);
            if (bindableMembers.Count == 0)
            {
                return false;
            }

            for (int parameterIndex = 0; parameterIndex < constructor.MethodSig.Params.Count; parameterIndex++)
            {
                TypeSig parameterType = constructor.MethodSig.Params[parameterIndex];
                ConstructorBindableMember? matchedMember = bindingMode switch
                {
                    ConstructorBindingMode.Name => FindNameBoundMember(bindableMembers, constructor, parameterIndex),
                    ConstructorBindingMode.Index => bindableMembers.FirstOrDefault(member => member.Index == parameterIndex),
                    _ => null
                };

                if (matchedMember is null || !AreEquivalentTypeSignatures(parameterType, matchedMember.Type))
                {
                    return false;
                }

                score++;
            }

            return true;
        }

        private static IReadOnlyList<ConstructorBindableMember> GetBindableMembers(TypeDef type, ActivationPolicy policy)
        {
            var members = new List<ConstructorBindableMember>();

            foreach (FieldDef field in type.Fields)
            {
                if (field.IsStatic || field.FieldSig?.Type is not { } fieldType || LooksLikeCompilerGeneratedBackingField(field.Name))
                {
                    continue;
                }

                members.Add(new ConstructorBindableMember(
                    field.Name,
                    fieldType,
                    GetBindableMemberNames(field, field.Name),
                    GetBindableMemberIndex(field, policy.IndexedMemberAttributeTypeNames)));
            }

            foreach (PropertyDef property in type.Properties)
            {
                MethodDef? accessor = property.GetMethod ?? property.SetMethod;
                if (accessor is null || accessor.IsStatic || property.PropertySig?.RetType is not { } propertyType)
                {
                    continue;
                }

                members.Add(new ConstructorBindableMember(
                    property.Name,
                    propertyType,
                    GetBindableMemberNames(property, property.Name),
                    GetBindableMemberIndex(property, policy.IndexedMemberAttributeTypeNames)));
            }

            return members;
        }

        private static ConstructorBindableMember? FindNameBoundMember(
            IReadOnlyList<ConstructorBindableMember> bindableMembers,
            MethodDef constructor,
            int parameterIndex)
        {
            if (constructor.ParamDefs.Count <= parameterIndex)
            {
                return null;
            }

            string? parameterName = constructor.ParamDefs[parameterIndex].Name;
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return null;
            }

            return bindableMembers.FirstOrDefault(member =>
                member.Names.Contains(parameterName, StringComparer.OrdinalIgnoreCase));
        }

        private static bool HasAnyIndexedBindableMember(TypeDef type, ActivationPolicy policy)
            => policy.IndexedMemberAttributeTypeNames.Count > 0 &&
               (type.Fields.Any(field => GetBindableMemberIndex(field, policy.IndexedMemberAttributeTypeNames) is not null) ||
                type.Properties.Any(property => GetBindableMemberIndex(property, policy.IndexedMemberAttributeTypeNames) is not null));

        private static IReadOnlyList<string> GetBindableMemberNames(IHasCustomAttribute provider, string declaredName)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                declaredName
            };

            foreach (CustomAttribute attribute in provider.CustomAttributes)
            {
                if (string.Equals(attribute.AttributeType.FullName, "MessagePack.KeyAttribute", StringComparison.Ordinal) &&
                    TryGetCustomAttributeConstructorArgument<string>(attribute, 0, out string? keyName) &&
                    !string.IsNullOrWhiteSpace(keyName))
                {
                    names.Add(keyName);
                }

                if (string.Equals(attribute.AttributeType.FullName, "System.Runtime.Serialization.DataMemberAttribute", StringComparison.Ordinal) &&
                    TryGetCustomAttributeNamedArgument(attribute, "Name", out string? dataMemberName) &&
                    !string.IsNullOrWhiteSpace(dataMemberName))
                {
                    names.Add(dataMemberName);
                }
            }

            return [.. names];
        }

        private static int? GetBindableMemberIndex(IHasCustomAttribute provider, IReadOnlyList<string> indexedMemberAttributeTypeNames)
        {
            foreach (CustomAttribute attribute in provider.CustomAttributes)
            {
                if (!indexedMemberAttributeTypeNames.Contains(attribute.AttributeType.FullName, StringComparer.Ordinal))
                {
                    continue;
                }

                if (TryGetCustomAttributeConstructorArgument<int>(attribute, 0, out int index))
                {
                    return index;
                }
            }

            return null;
        }

        private static bool TryGetCustomAttributeConstructorArgument<T>(CustomAttribute attribute, int index, out T value)
        {
            value = default!;
            if (attribute.ConstructorArguments.Count <= index)
            {
                return false;
            }

            object? argumentValue = attribute.ConstructorArguments[index].Value;
            if (argumentValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            return false;
        }

        private static bool TryGetCustomAttributeNamedArgument<T>(CustomAttribute attribute, string name, out T value)
        {
            value = default!;
            foreach (CANamedArgument namedArgument in attribute.NamedArguments)
            {
                if (!string.Equals(namedArgument.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (namedArgument.Argument.Value is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeCompilerGeneratedBackingField(UTF8String fieldName)
            => fieldName.String.Contains('<', StringComparison.Ordinal) &&
               fieldName.String.Contains('>', StringComparison.Ordinal);

        private static bool AreEquivalentTypeSignatures(TypeSig left, TypeSig right)
        {
            string leftName = GetComparableTypeName(left);
            string rightName = GetComparableTypeName(right);
            return string.Equals(leftName, rightName, StringComparison.Ordinal);
        }

        private static string GetComparableTypeName(TypeSig type)
            => AnalysisIndex.GetTypeDefinitionSignature(type)?.FullName ?? type.FullName;

        private static ConstructorSelectionResult SelectConstructor(MethodDef? constructor)
            => constructor is null ? NoConstructorMatch : new ConstructorSelectionResult(constructor, false);

        private static MethodDef? GetSinglePublicParameterizedConstructor(MethodDef[] instanceConstructors)
        {
            MethodDef[] publicParameterizedConstructors = [.. instanceConstructors
                .Where(method => method.IsPublic && !IsParameterlessConstructor(method))
                .OrderBy(method => method.MDToken.Raw)];

            return publicParameterizedConstructors.Length == 1 ? publicParameterizedConstructors[0] : null;
        }

        private static MethodDef? GetPublicParameterlessConstructor(MethodDef[] instanceConstructors)
            => instanceConstructors.SingleOrDefault(method => method.IsPublic && IsParameterlessConstructor(method));

        private static MethodDef? GetNonPublicParameterlessConstructor(MethodDef[] instanceConstructors)
            => instanceConstructors.SingleOrDefault(method => !method.IsPublic && IsParameterlessConstructor(method));

        private static MethodDef? TryGetExactSignatureConstructor(MethodDef[] instanceConstructors, ExactConstructorSignature? signature)
        {
            if (signature is null)
            {
                return null;
            }

            return instanceConstructors.SingleOrDefault(method =>
                (!signature.RequirePublic || method.IsPublic) &&
                AnalysisIndex.HasExactParameterSignature(method.MethodSig, signature.ParameterTypeNames));
        }

        private static MethodDef? TryGetSerializationConstructor(
            TypeDef declaringType,
            MethodDef[] instanceConstructors,
            SerializationConstructorSignature? signature)
        {
            if (signature is null)
            {
                return null;
            }

            IReadOnlyList<MethodVisibility> allowedVisibilities = declaringType.IsSealed
                ? signature.VisibilityPolicy.SealedTypeAllowedVisibilities
                : signature.VisibilityPolicy.UnsealedTypeAllowedVisibilities;

            if (allowedVisibilities.Count == 0)
            {
                return null;
            }

            return instanceConstructors.SingleOrDefault(method =>
                MatchesConfiguredMethodVisibility(method, allowedVisibilities) &&
                AnalysisIndex.HasExactParameterSignature(method.MethodSig, signature.ParameterTypeNames));
        }

        private static bool MatchesConfiguredMethodVisibility(MethodDef method, IReadOnlyList<MethodVisibility> allowedVisibilities)
            => allowedVisibilities.Contains(GetMethodVisibility(method));

        private static MethodVisibility GetMethodVisibility(MethodDef method)
        {
            if (method.IsPublic)
            {
                return MethodVisibility.Public;
            }

            if (method.IsPrivate)
            {
                return MethodVisibility.Private;
            }

            if (method.IsFamily)
            {
                return MethodVisibility.Family;
            }

            if (method.IsAssembly)
            {
                return MethodVisibility.Assembly;
            }

            if (method.IsFamilyOrAssembly)
            {
                return MethodVisibility.FamilyOrAssembly;
            }

            if (method.IsFamilyAndAssembly)
            {
                return MethodVisibility.FamilyAndAssembly;
            }

            throw new InvalidOperationException($"Unsupported method visibility on '{method.FullName}'.");
        }

        private static int CountConstructors(MethodDef[] instanceConstructors, Func<MethodDef, bool> predicate)
            => instanceConstructors.Count(predicate);

        private static int CountAttributedConstructors(
            MethodDef[] instanceConstructors,
            IReadOnlyList<string> preferredConstructorAttributeTypeNames)
            => preferredConstructorAttributeTypeNames.Count == 0
                ? 0
                : instanceConstructors.Count(method => method.CustomAttributes.Any(attribute =>
                    preferredConstructorAttributeTypeNames.Contains(attribute.AttributeType.FullName, StringComparer.Ordinal)));

        private static MethodDef[] GetInstanceConstructors(TypeDef type)
            => [.. type.Methods
                .Where(method => method.IsConstructor && !method.IsStatic)
                .OrderBy(method => method.MDToken.Raw)];

        private static bool IsParameterlessConstructor(MethodDef method)
            => method.MethodSig?.Params.Count == 0;

        private static readonly ConstructorSelectionResult NoConstructorMatch = new(null, false);
        private static readonly ConstructorSelectionResult StopConstructorSelection = new(null, true);

        /// <summary>
        /// Stores one constructor-selection result together with whether later rules should be skipped.
        /// </summary>
        private readonly record struct ConstructorSelectionResult(
            MethodDef? SelectedConstructor,
            bool StopProcessing);

        /// <summary>
        /// Stores one bindable serialized member for best-match constructor selection.
        /// </summary>
        private sealed record ConstructorBindableMember(
            string DeclaredName,
            TypeSig Type,
            IReadOnlyList<string> Names,
            int? Index);
    }
}
