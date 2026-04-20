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
        /// Finds reverse-slice states that are valid serializer triggers.
        /// </summary>
        private static IReadOnlyList<TriggerStateCandidate> FindPositiveTriggerStates(
            AnalysisIndex index,
            IReadOnlySet<SliceState> sinkSliceStates,
            IReadOnlySet<TypeId> eligibleRootClassIds,
            SerializerProfile profile,
            IDictionary<SliceState, IReadOnlyList<TypeRecord>> rootClassesByTriggerState)
            => [.. sinkSliceStates
                .Select(state => new TriggerStateCandidate(state, index.GetMethod(state.MethodId)))
                .Where(candidate => IsTrigger(index, candidate.Method, eligibleRootClassIds, profile, rootClassesByTriggerState, candidate.State))
                .OrderBy(candidate => index.GetType(candidate.Method.DeclaringTypeId).FullName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Method.DisplayName, StringComparer.Ordinal)];

        /// <summary>
        /// Determines whether a method is a valid serializer trigger.
        /// </summary>
        private static bool IsTrigger(
            AnalysisIndex index,
            MethodRecord method,
            IReadOnlySet<TypeId> eligibleRootClassIds,
            SerializerProfile profile,
            IDictionary<SliceState, IReadOnlyList<TypeRecord>> rootClassesByTriggerState,
            SliceState triggerState)
        {
            if (method.MethodDefinition is null || method.DeclaringTypeId.Value < 0)
            {
                return false;
            }

            if (profile.SupportsConstructorTriggers && method is { IsConstructor: true, IsInstance: true })
            {
                return eligibleRootClassIds.Contains(method.DeclaringTypeId) &&
                       IsSelectedConstructorTrigger(index.GetType(method.DeclaringTypeId), method, profile);
            }

            if (IsPropertyGetterTrigger(method, profile))
            {
                return GetOrAddRootClasses(index, method, triggerState, eligibleRootClassIds, rootClassesByTriggerState).Count > 0;
            }

            if (IsPropertySetterTrigger(method, profile))
            {
                return GetOrAddRootClasses(index, method, triggerState, eligibleRootClassIds, rootClassesByTriggerState).Count > 0;
            }

            if (IsDeserializationCallbackTrigger(method, profile))
            {
                return GetOrAddRootClasses(index, method, triggerState, eligibleRootClassIds, rootClassesByTriggerState).Count > 0;
            }

            if (IsCustomDeserializationMethodTrigger(method, profile))
            {
                return GetOrAddRootClasses(index, method, triggerState, eligibleRootClassIds, rootClassesByTriggerState).Count > 0;
            }

            if (IsFinalizerTrigger(method, profile))
            {
                return GetOrAddRootClasses(index, method, triggerState, eligibleRootClassIds, rootClassesByTriggerState).Count > 0;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a constructor method is selected by the serializer profile.
        /// </summary>
        private static bool IsSelectedConstructorTrigger(TypeRecord declaringType, MethodRecord method, SerializerProfile profile)
        {
            if (method.MethodDefinition is null)
            {
                return false;
            }

            return GetSelectedConstructors(declaringType.TypeDef, profile)
                .Any(selectedConstructor => IsSameMethod(method.MethodDefinition, selectedConstructor));
        }

        /// <summary>
        /// Determines whether a method is a supported deserialization callback trigger.
        /// </summary>
        private static bool IsDeserializationCallbackTrigger(MethodRecord method, SerializerProfile profile)
            => profile.SupportsDeserializationCallbackTriggers &&
               method is { IsInstance: true, HasBody: true, MethodDefinition: not null } &&
               (MatchesAttributeCallback(method.MethodDefinition, profile.Callbacks.AttributeCallbacks) ||
                MatchesInterfaceCallback(method.MethodDefinition, profile.Callbacks.InterfaceCallbacks));

        /// <summary>
        /// Determines whether a method is a serializer-specific custom deserialization method trigger.
        /// </summary>
        private static bool IsCustomDeserializationMethodTrigger(MethodRecord method, SerializerProfile profile)
            => profile.SupportsCustomDeserializationMethodTriggers &&
               method is { IsInstance: true, HasBody: true, MethodDefinition: not null } &&
               MatchesInterfaceMethod(method.MethodDefinition, profile.CustomDeserializationMethods.InterfaceMethods);

        /// <summary>
        /// Determines whether a method is a supported finalizer trigger.
        /// </summary>
        private static bool IsFinalizerTrigger(MethodRecord method, SerializerProfile profile)
            => profile.SupportsFinalizerTriggers &&
               method is { HasBody: true, IsFinalizer: true, MethodDefinition: not null } &&
               method.MethodDefinition.MethodSig?.RetType.ElementType == ElementType.Void;

        /// <summary>
        /// Determines the trigger kind to report for a serializer trigger method.
        /// </summary>
        private static TriggerKind GetTriggerKind(MethodRecord method, SerializerProfile profile)
        {
            if (method.IsConstructor)
            {
                return TriggerKind.Constructor;
            }

            if (method.IsPropertyGetter)
            {
                return TriggerKind.PublicPropertyGetter;
            }

            if (method.IsPropertySetter)
            {
                return method.IsPublic
                    ? TriggerKind.PublicPropertySetter
                    : TriggerKind.NonPublicPropertySetter;
            }

            if (IsDeserializationCallbackTrigger(method, profile))
            {
                return TriggerKind.DeserializationCallback;
            }

            if (IsCustomDeserializationMethodTrigger(method, profile))
            {
                return TriggerKind.CustomDeserializationMethod;
            }

            if (IsFinalizerTrigger(method, profile))
            {
                return TriggerKind.Finalizer;
            }

            throw new InvalidOperationException($"Method '{method.DisplayName}' is not a recognized trigger kind.");
        }

        /// <summary>
        /// Builds the trigger result path for a root class.
        /// </summary>
        private static TriggerResult BuildTriggerResult(
            AnalysisIndex index,
            IReadOnlySet<MethodId> sinkMethodIds,
            MethodRecord triggerMethod,
            SliceState triggerState,
            IReadOnlyDictionary<SliceState, SliceStep> nextStepByState,
            TypeRecord rootClass,
            SerializerProfile profile,
            string? triggerAnnotation)
        {
            var reachabilityPath = new List<EdgeRecord>();
            SliceState currentState = triggerState;
            while (!sinkMethodIds.Contains(currentState.MethodId))
            {
                if (!nextStepByState.TryGetValue(currentState, out SliceStep nextStep))
                {
                    throw new InvalidOperationException($"No explanation path was recorded from '{triggerMethod.DisplayName}' to sink.");
                }

                EdgeRecord edge = index.GetEdge(nextStep.EdgeId);
                reachabilityPath.Add(edge);
                currentState = nextStep.NextState;
            }

            string? declaredOnTypeName = index.GetType(triggerMethod.DeclaringTypeId).FullName != rootClass.FullName
                ? index.GetType(triggerMethod.DeclaringTypeId).FullName
                : null;

            return new TriggerResult(
                triggerMethod.Id,
                triggerMethod.DisplayName,
                GetTriggerKind(triggerMethod, profile),
                reachabilityPath,
                declaredOnTypeName,
                triggerAnnotation);
        }

        private static bool IsPropertySetterTrigger(MethodRecord method, SerializerProfile profile)
        {
            if (method is not { IsPropertySetter: true, IsInstance: true, MethodDefinition: not null })
            {
                return false;
            }

            if (profile.AllowedPropertySetterVisibilities.Count > 0 &&
                MatchesConfiguredMethodVisibility(method.MethodDefinition, profile.AllowedPropertySetterVisibilities))
            {
                return true;
            }

            if (method.IsPublic)
            {
                return false;
            }

            IReadOnlyList<string> optInAttributeTypeNames = profile.NonPublicPropertySetterOptInAttributeTypeNames;
            if (optInAttributeTypeNames.Count == 0)
            {
                return false;
            }

            if (HasAnyOptInAttribute(method.MethodDefinition, optInAttributeTypeNames))
            {
                return true;
            }

            PropertyDef? property = FindDeclaringPropertyForSetter(method.MethodDefinition);
            return property is not null && HasAnyOptInAttribute(property, optInAttributeTypeNames);
        }

        private static PropertyDef? FindDeclaringPropertyForSetter(MethodDef setter)
            => setter.DeclaringType?.Properties.FirstOrDefault(property =>
                property.SetMethod is not null &&
                IsSameMethod(property.SetMethod, setter));

        private static bool IsPropertyGetterTrigger(MethodRecord method, SerializerProfile profile)
            => method is { IsPropertyGetter: true, IsInstance: true, MethodDefinition: not null } &&
               MatchesConfiguredMethodVisibility(method.MethodDefinition, profile.AllowedPropertyGetterVisibilities);

        private static bool HasAnyOptInAttribute(IHasCustomAttribute provider, IReadOnlyList<string> attributeTypeNames)
            => provider.CustomAttributes.Any(attribute =>
                attributeTypeNames.Contains(attribute.AttributeType.FullName, StringComparer.Ordinal));
        private static bool MatchesAttributeCallback(MethodDef method, IReadOnlyList<AttributeCallbackSignature> callbacks)
            => callbacks.Any(callback => MatchesAttributeCallback(method, callback));

        private static bool MatchesAttributeCallback(MethodDef method, AttributeCallbackSignature callback)
        {
            if (!method.CustomAttributes.Any(attribute =>
                    string.Equals(attribute.AttributeType.FullName, callback.AttributeTypeName, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!AnalysisIndex.MatchesConfiguredTypeName(method.MethodSig?.RetType, callback.ReturnTypeName))
            {
                return false;
            }

            return AnalysisIndex.HasExactParameterSignature(method.MethodSig, callback.ParameterTypeNames);
        }

        private static bool MatchesInterfaceCallback(MethodDef method, IReadOnlyList<InterfaceCallbackSignature> callbacks)
            => callbacks.Any(callback => MatchesInterfaceCallback(method, callback));

        private static bool MatchesInterfaceCallback(MethodDef method, InterfaceCallbackSignature callback)
        {
            if (!ImplementsInterface(method.DeclaringType, callback.InterfaceTypeName))
            {
                return false;
            }

            if (!HasCallbackMethodName(method, callback.MethodName))
            {
                return false;
            }

            if (!AnalysisIndex.MatchesConfiguredTypeName(method.MethodSig?.RetType, callback.ReturnTypeName))
            {
                return false;
            }

            return AnalysisIndex.HasExactParameterSignature(method.MethodSig, callback.ParameterTypeNames);
        }

        private static bool MatchesInterfaceMethod(MethodDef method, IReadOnlyList<InterfaceMethodSignature> methods)
            => methods.Any(candidate => MatchesInterfaceMethod(method, candidate));

        private static bool MatchesInterfaceMethod(MethodDef method, InterfaceMethodSignature candidate)
        {
            if (!ImplementsInterface(method.DeclaringType, candidate.InterfaceTypeName))
            {
                return false;
            }

            if (!HasCallbackMethodName(method, candidate.MethodName))
            {
                return false;
            }

            if (!AnalysisIndex.MatchesConfiguredTypeName(method.MethodSig?.RetType, candidate.ReturnTypeName))
            {
                return false;
            }

            return AnalysisIndex.HasExactParameterSignature(method.MethodSig, candidate.ParameterTypeNames);
        }

        private static bool HasCallbackMethodName(MethodDef method, string expectedMethodName)
            => string.Equals(method.Name, expectedMethodName, StringComparison.Ordinal) ||
               method.Name.String.EndsWith($".{expectedMethodName}", StringComparison.Ordinal);

        private static bool ImplementsInterface(TypeDef type, string interfaceTypeName)
        {
            for (TypeDef? current = type; current is not null; current = current.BaseType?.ResolveTypeDef())
            {
                if (current.Interfaces.Any(implementation =>
                        string.Equals(implementation.Interface.FullName, interfaceTypeName, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameMethod(MethodDef left, MethodDef right)
            => left.Module == right.Module && left.MDToken.Raw == right.MDToken.Raw;

        /// <summary>
        /// Couples a reverse-slice state with the trigger method resolved from that state.
        /// </summary>
        private sealed record TriggerStateCandidate(
            SliceState State,
            MethodRecord Method);
    }
}

