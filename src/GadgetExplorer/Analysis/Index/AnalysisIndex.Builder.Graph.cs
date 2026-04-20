/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Index
{
    /// <summary>
    /// Contains graph-building helpers for <see cref="AnalysisIndex"/>.
    /// </summary>
    public sealed partial class AnalysisIndex
    {
        private sealed partial class Builder
        {
            /// <summary>
            /// Builds the forward and reverse adjacency lists for the emitted edge set.
            /// </summary>
            /// <param name="methodCount">The total method count.</param>
            private (IReadOnlyList<EdgeId>[] CallsFrom, IReadOnlyList<EdgeId>[] CalledBy) BuildAdjacency(int methodCount)
            {
                List<EdgeId>[] callsFrom = Enumerable.Range(0, methodCount).Select(_ => new List<EdgeId>()).ToArray();
                List<EdgeId>[] calledBy = Enumerable.Range(0, methodCount).Select(_ => new List<EdgeId>()).ToArray();

                foreach (EdgeRecord edge in _edges)
                {
                    if (edge.SourceId.Value < 0 || edge.TargetId.Value < 0 || edge.SourceId.Value >= methodCount || edge.TargetId.Value >= methodCount)
                    {
                        continue;
                    }

                    callsFrom[edge.SourceId.Value].Add(edge.Id);
                    calledBy[edge.TargetId.Value].Add(edge.Id);
                }

                return (
                    callsFrom.Select(list => (IReadOnlyList<EdgeId>)[.. list.OrderBy(id => id.Value)]).ToArray(),
                    calledBy.Select(list => (IReadOnlyList<EdgeId>)[.. list.OrderBy(id => id.Value)]).ToArray());
            }

            /// <summary>
            /// Adds state-machine entry edges for async and iterator methods.
            /// </summary>
            private void AddAsyncAndIteratorEdges()
            {
                foreach (MethodRecord method in _methods.Where(method => method.MethodDefinition is not null))
                {
                    TypeDef? stateMachineType = GetStateMachineType(method.MethodDefinition!);
                    if (stateMachineType is null || !TryResolveTypeId(stateMachineType, out TypeId stateMachineTypeId))
                    {
                        continue;
                    }

                    MethodDef? moveNext = _types[stateMachineTypeId.Value].TypeDef.FindMethod("MoveNext");
                    if (moveNext is null || !TryGetLoadedMethodId(moveNext, out MethodId moveNextMethodId))
                    {
                        continue;
                    }

                    AddEdges(method.Id, [moveNextMethodId], EdgeKind.AsyncIterator);
                }
            }

            /// <summary>
            /// Materializes event raise edges from observed subscriptions.
            /// </summary>
            private void MaterializeEventRaiseEdges()
            {
                var groupedSubscriptions = _eventSubscriptions
                    .GroupBy(candidate => (candidate.OwnerTypeId, candidate.HandlerTypeName))
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<MethodId>)[.. group.Select(candidate => candidate.HandlerMethodId)
                            .Distinct()
                            .OrderBy(methodId => methodId.Value)]);

                foreach (EventRaiseSite raiseSite in _eventRaiseSites)
                {
                    if (!groupedSubscriptions.TryGetValue((raiseSite.OwnerTypeId, raiseSite.HandlerTypeName), out IReadOnlyList<MethodId>? handlerTargets))
                    {
                        continue;
                    }

                    AddEdges(raiseSite.SourceMethodId, handlerTargets, EdgeKind.EventRaise);
                }
            }

            /// <summary>
            /// Records deferred delegate-relay and event-subscription inputs for later materialization passes.
            /// </summary>
            private void RecordDeferredCallEffects(MethodId sourceMethodId, ResolvedMethodReference targetMethod, IReadOnlyList<ValueState> arguments)
            {
                RecordEventSubscription(targetMethod.MethodId, targetMethod.MethodReference, arguments);
                RecordPendingDelegateRelayCallSite(sourceMethodId, targetMethod.MethodId, arguments);
            }

            /// <summary>
            /// Records delegate invocation against a delegate parameter origin.
            /// </summary>
            private void RecordDelegateParameterInvocation(MethodRecord caller, ValueState instance)
            {
                if (instance.OriginKind != ValueOriginKind.Argument || instance.OriginIndex is not { } argumentIndex)
                {
                    return;
                }

                if (!_delegateParameterInvocationsByMethod.TryGetValue(caller.Id, out HashSet<int>? invokedArguments))
                {
                    invokedArguments = [];
                    _delegateParameterInvocationsByMethod[caller.Id] = invokedArguments;
                }

                invokedArguments.Add(argumentIndex);
            }

            /// <summary>
            /// Records a pending delegate relay call site for later materialization.
            /// </summary>
            private void RecordPendingDelegateRelayCallSite(MethodId sourceMethodId, MethodId targetMethodId, IReadOnlyList<ValueState> arguments)
            {
                if (arguments.All(argument => argument.DelegateTargets.Count == 0))
                {
                    return;
                }

                _pendingDelegateRelayCallSites.Add(new PendingDelegateRelayCallSite(sourceMethodId, targetMethodId, [.. arguments]));
            }

            /// <summary>
            /// Materializes delegate relay edges from recorded relay call sites.
            /// </summary>
            private void MaterializeDelegateRelayEdges()
            {
                foreach (PendingDelegateRelayCallSite callSite in _pendingDelegateRelayCallSites)
                {
                    if (!_delegateParameterInvocationsByMethod.TryGetValue(callSite.TargetMethodId, out HashSet<int>? invokedArguments))
                    {
                        continue;
                    }

                    MethodRecord targetMethod = _methods[callSite.TargetMethodId.Value];
                    int explicitArgumentOffset = targetMethod.IsStatic ? 0 : 1;

                    foreach (int invokedArgumentIndex in invokedArguments.OrderBy(index => index))
                    {
                        int explicitArgumentIndex = invokedArgumentIndex - explicitArgumentOffset;
                        if (explicitArgumentIndex < 0 || explicitArgumentIndex >= callSite.Arguments.Count)
                        {
                            continue;
                        }

                        AddEdges(
                            callSite.SourceMethodId,
                            callSite.Arguments[explicitArgumentIndex].DelegateTargets,
                            EdgeKind.DelegateInvoke);
                    }
                }
            }

            /// <summary>
            /// Records an event subscription observed via an add accessor call.
            /// </summary>
            private void RecordEventSubscription(MethodId calledMethodId, IMethod calledMethod, IReadOnlyList<ValueState> arguments)
            {
                if (!_eventsByAddAccessor.TryGetValue(calledMethodId, out EventRecord? eventRecord))
                {
                    return;
                }

                (ValueState Value, TypeSig Parameter) delegateArgument = arguments
                    .Zip(calledMethod.MethodSig?.Params ?? [], (value, parameter) => (Value: value, Parameter: parameter))
                    .FirstOrDefault(pair => IsDelegateType(pair.Parameter));
                if (delegateArgument.Value.DelegateTargets is null || delegateArgument.Value.DelegateTargets.Count == 0)
                {
                    return;
                }

                foreach (MethodId handlerMethodId in delegateArgument.Value.DelegateTargets.Distinct().OrderBy(methodId => methodId.Value))
                {
                    _eventSubscriptions.Add(new EventSubscriptionCandidate(eventRecord.DeclaringTypeId, eventRecord.HandlerTypeName, handlerMethodId));
                }
            }

            /// <summary>
            /// Attempts to record an event raise site from a delegate invocation.
            /// </summary>
            private void RecordEventRaiseSite(MethodRecord caller, IMethod calledMethod, ValueState receiver)
            {
                if (!IsDelegateInvoke(calledMethod) || receiver.OriginFieldKey is null)
                {
                    return;
                }

                if (!_fieldDefsByKey.TryGetValue(receiver.OriginFieldKey, out FieldDef? field) ||
                    field.DeclaringType is null ||
                    GetTypeKey(field.DeclaringType) != GetTypeKey(_types[caller.DeclaringTypeId.Value].TypeDef))
                {
                    return;
                }

                _eventRaiseSites.Add(new EventRaiseSite(
                    caller.Id,
                    caller.DeclaringTypeId,
                    GetTypeDisplayName(field.FieldSig?.Type)));
            }

        }
    }
}

