/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis
{
    public static partial class SinkAnalyzer
    {
        private readonly record struct SliceState(MethodId MethodId, TypeId? RequiredReceiverTypeId);

        private readonly record struct SliceStep(EdgeId EdgeId, SliceState NextState);

        private sealed record ReverseSinkSliceResult(
            IReadOnlySet<SliceState> SliceStates,
            IReadOnlyDictionary<SliceState, SliceStep> NextStepByState);

        /// <summary>
        /// Computes the reverse sink slice and explanation map.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="sinkDefinition">The sink definition being analyzed.</param>
        /// <param name="sinkMethodIds">The resolved sink method identifiers.</param>
        /// <param name="allResolvedSinkMethodIds">The union of all resolved sink method identifiers.</param>
        /// <param name="ignoreSinkDefinitions">The ignore-sink definitions.</param>
        /// <param name="maxPathLength">The optional maximum slice path length.</param>
        private static ReverseSinkSliceResult ComputeReverseSinkSlice(
            AnalysisIndex index,
            SinkDefinition sinkDefinition,
            IReadOnlyList<MethodId> sinkMethodIds,
            IReadOnlySet<MethodId> allResolvedSinkMethodIds,
            IReadOnlyList<SinkDefinition> ignoreSinkDefinitions,
            int? maxPathLength)
        {
            SliceState[] initialStates = sinkMethodIds
                .Select(methodId => new SliceState(methodId, GetMethodReceiverConstraint(index, methodId)))
                .Distinct()
                .ToArray();
            var sinkSlice = new HashSet<SliceState>(initialStates);
            var currentSinkMethodIds = sinkMethodIds.ToHashSet();
            var nextStepByState = new Dictionary<SliceState, SliceStep>();
            var queue = new Queue<(SliceState State, int PathLength)>(initialStates.Select(state => (state, 0)));

            while (queue.Count > 0)
            {
                (SliceState currentState, int pathLength) = queue.Dequeue();
                if (maxPathLength is not null && pathLength >= maxPathLength.Value)
                {
                    continue;
                }

                foreach (EdgeId incomingEdgeId in index.GetIncomingEdges(currentState.MethodId))
                {
                    EdgeRecord edge = index.GetEdge(incomingEdgeId);
                    if (currentSinkMethodIds.Contains(currentState.MethodId) && ShouldIgnoreConstantArgumentSinkCall(edge, sinkDefinition))
                    {
                        continue;
                    }

                    MethodRecord sourceMethod = index.GetMethod(edge.SourceId);
                    if (IsIgnored(sourceMethod, ignoreSinkDefinitions) ||
                        !TryCreatePreviousSliceState(index, sourceMethod, edge, currentState, out SliceState sourceState) ||
                        !sinkSlice.Add(sourceState))
                    {
                        continue;
                    }

                    nextStepByState[sourceState] = new SliceStep(edge.Id, currentState);
                    if (!IsDifferentSinkBoundary(edge.SourceId, currentSinkMethodIds, allResolvedSinkMethodIds))
                    {
                        queue.Enqueue((sourceState, pathLength + 1));
                    }
                }
            }

            return new ReverseSinkSliceResult(sinkSlice, nextStepByState);
        }

        /// <summary>
        /// Resolves the receiver-type constraint that must hold for a method itself to execute on an instance.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="methodId">The method identifier.</param>
        private static TypeId? GetMethodReceiverConstraint(AnalysisIndex index, MethodId methodId)
            => GetMethodReceiverConstraint(index, index.GetMethod(methodId));

        /// <summary>
        /// Resolves the receiver-type constraint that must hold for a method itself to execute on an instance.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="method">The method record to inspect.</param>
        private static TypeId? GetMethodReceiverConstraint(AnalysisIndex index, MethodRecord method)
        {
            if (!method.IsInstance || method.DeclaringTypeId.Value < 0)
            {
                return null;
            }

            TypeRecord declaringType = index.GetType(method.DeclaringTypeId);
            return declaringType is { IsClass: true, IsAbstract: false }
                ? method.DeclaringTypeId
                : null;
        }

        /// <summary>
        /// Creates the previous reverse-slice state for an incoming edge when receiver constraints remain compatible.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="sourceMethod">The source method record.</param>
        /// <param name="incomingEdge">The incoming edge being traversed.</param>
        /// <param name="currentState">The current reverse-slice state.</param>
        /// <param name="previousState">The computed previous state.</param>
        private static bool TryCreatePreviousSliceState(
            AnalysisIndex index,
            MethodRecord sourceMethod,
            EdgeRecord incomingEdge,
            SliceState currentState,
            out SliceState previousState)
        {
            previousState = default;
            if (!IsCompatibleReceiverFlow(index, incomingEdge, currentState))
            {
                return false;
            }

            TypeId? sourceMethodReceiverConstraint = GetMethodReceiverConstraint(index, sourceMethod);
            TypeId? requiredReceiverTypeId;
            if (incomingEdge.PreservesCallerInstanceReceiver)
            {
                if (!TryMergeReceiverConstraints(index, sourceMethodReceiverConstraint, currentState.RequiredReceiverTypeId, out requiredReceiverTypeId))
                {
                    return false;
                }
            }
            else
            {
                requiredReceiverTypeId = sourceMethodReceiverConstraint;
            }

            previousState = new SliceState(sourceMethod.Id, requiredReceiverTypeId);
            return true;
        }

        /// <summary>
        /// Determines whether an incoming call edge remains compatible with the current receiver-sensitive slice state.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="incomingEdge">The incoming edge being traversed.</param>
        /// <param name="currentState">The current reverse-slice state.</param>
        private static bool IsCompatibleReceiverFlow(AnalysisIndex index, EdgeRecord incomingEdge, SliceState currentState)
        {
            if (currentState.RequiredReceiverTypeId is not { } requiredReceiverTypeId ||
                incomingEdge.ReceiverTypeConstraintId is not { } receiverTypeConstraintId)
            {
                return true;
            }

            return requiredReceiverTypeId == receiverTypeConstraintId ||
                   index.IsAssignableTo(requiredReceiverTypeId, receiverTypeConstraintId);
        }

        /// <summary>
        /// Merges receiver-type constraints by keeping the narrower compatible type when possible.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="left">The first receiver constraint.</param>
        /// <param name="right">The second receiver constraint.</param>
        /// <param name="merged">The merged receiver constraint.</param>
        private static bool TryMergeReceiverConstraints(AnalysisIndex index, TypeId? left, TypeId? right, out TypeId? merged)
        {
            if (left is null)
            {
                merged = right;
                return true;
            }

            if (right is null)
            {
                merged = left;
                return true;
            }

            if (left == right)
            {
                merged = left;
                return true;
            }

            if (index.IsAssignableTo(left.Value, right.Value))
            {
                merged = right;
                return true;
            }

            if (index.IsAssignableTo(right.Value, left.Value))
            {
                merged = left;
                return true;
            }

            merged = null;
            return false;
        }

        /// <summary>
        /// Determines whether an incoming call edge into the current sink should be ignored because configured arguments are provably constant.
        /// </summary>
        /// <param name="edge">The incoming sink call edge.</param>
        /// <param name="sinkDefinition">The sink definition being evaluated.</param>
        private static bool ShouldIgnoreConstantArgumentSinkCall(EdgeRecord edge, SinkDefinition sinkDefinition)
        {
            int[] ignoredArgumentIndexes = sinkDefinition.Parameters
                .Select((parameter, parameterIndex) => (parameter, parameterIndex))
                .Where(entry => entry.parameter.IgnoreSinkIfConstant)
                .Select(entry => entry.parameterIndex)
                .ToArray();

            if (ignoredArgumentIndexes.Length == 0 || edge.ArgumentSummaries.Count == 0)
            {
                return false;
            }

            return ignoredArgumentIndexes.All(argumentIndex =>
                edge.ArgumentSummaries.Any(summary =>
                    summary.ArgumentIndex == argumentIndex &&
                    summary.IsProvablyConstant));
        }

        /// <summary>
        /// Determines whether reverse traversal should stop at a method because it belongs to a different configured sink.
        /// </summary>
        /// <param name="methodId">The method to evaluate.</param>
        /// <param name="currentSinkMethodIds">The current sink family's resolved method identifiers.</param>
        /// <param name="allResolvedSinkMethodIds">The union of all resolved sink method identifiers.</param>
        private static bool IsDifferentSinkBoundary(
            MethodId methodId,
            HashSet<MethodId> currentSinkMethodIds,
            IReadOnlySet<MethodId> allResolvedSinkMethodIds)
            => allResolvedSinkMethodIds.Contains(methodId) && !currentSinkMethodIds.Contains(methodId);
    }
}
