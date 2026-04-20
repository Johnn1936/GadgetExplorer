/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis
{
    public static partial class SinkAnalyzer
    {
        /// <summary>
        /// Builds trigger findings for each compatible root class.
        /// </summary>
        private static TriggerFindingCandidate[] BuildTriggerFindings(
            AnalysisIndex index,
            IReadOnlySet<MethodId> sinkMethodIds,
            TriggerStateCandidate triggerState,
            IReadOnlyDictionary<SliceState, SliceStep> nextStepByState,
            IReadOnlySet<TypeId> eligibleRootClassIds,
            SerializerProfile profile,
            IDictionary<SliceState, IReadOnlyList<TypeRecord>> rootClassesByTriggerState)
        {
            MethodRecord triggerMethod = triggerState.Method;
            TypeRecord declaringType = index.GetType(triggerMethod.DeclaringTypeId);
            TypeRecord[] roots = [.. GetOrAddRootClasses(index, triggerMethod, triggerState.State, eligibleRootClassIds, rootClassesByTriggerState)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)];

            return roots.Select(rootType =>
            {
                string? annotation = GetTriggerAnnotation(triggerMethod, declaringType, rootType, profile);
                TriggerResult trigger = BuildTriggerResult(
                    index,
                    sinkMethodIds,
                    triggerMethod,
                    triggerState.State,
                    nextStepByState,
                    rootType,
                    profile,
                    annotation);

                return new TriggerFindingCandidate(
                    BuildFindingIdentity(rootType.Id, trigger),
                    rootType.Id,
                    rootType.FullName,
                    rootType.AssemblyQualifiedName,
                    trigger);
            }).ToArray();
        }

        /// <summary>
        /// Builds the stable identity for a reportable trigger finding.
        /// </summary>
        private static FindingIdentity BuildFindingIdentity(TypeId rootClassId, TriggerResult trigger)
            => new(rootClassId, trigger.TriggerMethodId, [.. trigger.ReachabilityPath.Select(edge => edge.Id)]);

        /// <summary>
        /// Resolves any inherited-trigger annotation that should be carried into the final finding.
        /// </summary>
        private static string? GetTriggerAnnotation(
            MethodRecord triggerMethod,
            TypeRecord declaringType,
            TypeRecord rootType,
            SerializerProfile profile)
        {
            if (rootType.FullName == declaringType.FullName)
            {
                return null;
            }

            if (triggerMethod.IsPropertySetter)
            {
                return $"Inherited setter declared on {declaringType.FullName}.";
            }

            if (triggerMethod.IsPropertyGetter)
            {
                return $"Inherited getter declared on {declaringType.FullName}.";
            }

            if (IsDeserializationCallbackTrigger(triggerMethod, profile))
            {
                return $"Inherited deserialization callback declared on {declaringType.FullName}.";
            }

            if (IsCustomDeserializationMethodTrigger(triggerMethod, profile))
            {
                return $"Inherited custom deserialization method declared on {declaringType.FullName}.";
            }

            if (IsFinalizerTrigger(triggerMethod, profile))
            {
                return $"Inherited finalizer declared on {declaringType.FullName}.";
            }

            return null;
        }

        /// <summary>
        /// Gets cached root classes for a trigger or computes them on demand.
        /// </summary>
        private static IReadOnlyList<TypeRecord> GetOrAddRootClasses(
            AnalysisIndex index,
            MethodRecord triggerMethod,
            SliceState triggerState,
            IReadOnlySet<TypeId> eligibleRootClassIds,
            IDictionary<SliceState, IReadOnlyList<TypeRecord>> rootClassesByTriggerState)
        {
            if (rootClassesByTriggerState.TryGetValue(triggerState, out IReadOnlyList<TypeRecord>? roots))
            {
                return roots;
            }

            roots = GetRootClasses(index, triggerMethod, triggerState, eligibleRootClassIds);
            rootClassesByTriggerState[triggerState] = roots;
            return roots;
        }

        /// <summary>
        /// Resolves the candidate root classes for a trigger.
        /// </summary>
        private static IReadOnlyList<TypeRecord> GetRootClasses(
            AnalysisIndex index,
            MethodRecord triggerMethod,
            SliceState triggerState,
            IReadOnlySet<TypeId> eligibleRootClassIds)
        {
            TypeRecord declaringType = index.GetType(triggerMethod.DeclaringTypeId);
            if (triggerMethod.IsConstructor)
            {
                return eligibleRootClassIds.Contains(declaringType.Id) &&
                       IsCompatibleWithReceiverConstraint(index, declaringType.Id, triggerState.RequiredReceiverTypeId)
                    ? [declaringType]
                    : [];
            }

            IEnumerable<TypeId> candidateTypeIds = declaringType.IsClass
                ? index.GetConcreteDescendantTypeIds(declaringType.Id)
                : index.Types
                    .Where(type =>
                        type is { IsClass: true, IsAbstract: false } &&
                        index.IsAssignableTo(declaringType.Id, type.Id))
                    .Select(type => type.Id);

            return [.. candidateTypeIds
                .Where(typeId =>
                    eligibleRootClassIds.Contains(typeId) &&
                    IsCompatibleWithReceiverConstraint(index, typeId, triggerState.RequiredReceiverTypeId))
                .Select(index.GetType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)];
        }

        /// <summary>
        /// Determines whether a candidate root class satisfies a receiver-sensitive slice constraint.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="candidateRootClassId">The candidate root class identifier.</param>
        /// <param name="requiredReceiverTypeId">The required receiver type constraint, if any.</param>
        private static bool IsCompatibleWithReceiverConstraint(AnalysisIndex index, TypeId candidateRootClassId, TypeId? requiredReceiverTypeId)
            => requiredReceiverTypeId is null ||
               requiredReceiverTypeId == candidateRootClassId ||
               index.IsAssignableTo(requiredReceiverTypeId.Value, candidateRootClassId);

        /// <summary>
        /// Stores a flattened trigger finding before it is grouped by root class.
        /// </summary>
        private sealed record TriggerFindingCandidate(
            FindingIdentity Identity,
            TypeId RootClassId,
            string RootClassFullName,
            string RootClassAssemblyQualifiedName,
            TriggerResult Trigger);

        /// <summary>
        /// Provides structural equality for a finding path without collapsing it to a joined string.
        /// </summary>
        private readonly record struct FindingIdentity(
            TypeId RootClassId,
            MethodId TriggerMethodId,
            EdgeId[] PathEdgeIds)
        {
            public bool Equals(FindingIdentity other)
            {
                if (RootClassId != other.RootClassId ||
                    TriggerMethodId != other.TriggerMethodId ||
                    PathEdgeIds.Length != other.PathEdgeIds.Length)
                {
                    return false;
                }

                for (int i = 0; i < PathEdgeIds.Length; i++)
                {
                    if (PathEdgeIds[i] != other.PathEdgeIds[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(RootClassId);
                hash.Add(TriggerMethodId);
                foreach (EdgeId pathEdgeId in PathEdgeIds)
                {
                    hash.Add(pathEdgeId);
                }

                return hash.ToHashCode();
            }
        }
    }
}

