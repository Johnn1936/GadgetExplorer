/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Collections.ObjectModel;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace GadgetExplorer.Analysis.Index
{
    public sealed partial class AnalysisIndex
    {
        /// <summary>
        /// Initializes a new index builder.
        /// </summary>
        /// <param name="assemblies">The assemblies to index.</param>
        /// <param name="interfaceExpansionMode">The interface expansion mode.</param>
        /// <param name="progress">The optional progress callback.</param>
        private sealed partial class Builder(IEnumerable<ModuleDefMD> assemblies, InterfaceExpansionMode interfaceExpansionMode, Action<string>? progress)
        {
            private readonly IReadOnlyList<ModuleDefMD> _assemblies = [.. assemblies.OrderBy(module => module.Assembly?.FullName ?? module.Name, StringComparer.Ordinal)];
            private readonly List<TypeRecord> _types = [];
            private readonly List<MethodRecord> _methods = [];
            private readonly List<EventRecord> _events = [];
            private readonly List<EdgeRecord> _edges = [];
            private readonly Dictionary<TypeDef, TypeId> _typeIdsByTypeDef = [];
            private readonly Dictionary<string, TypeId> _typeIdsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<MethodDef, MethodId> _methodIdsByMethodDef = [];
            private readonly Dictionary<string, MethodId> _externalMethodIdsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<IMethod, ResolvedMethodReference> _resolvedMethodReferencesByMethod = new(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<string, FieldDef> _fieldDefsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<MethodId, EventRecord> _eventsByAddAccessor = [];
            private Dictionary<MethodId, IReadOnlyList<MethodId>> _overrideTargetsByBaseMethod = [];
            private Dictionary<MethodId, IReadOnlyList<MethodId>> _interfaceTargetsByMethod = [];
            private readonly Dictionary<MethodId, TypeId?> _dispatchDeclaringTypeIdsByMethod = [];
            private readonly Dictionary<TypeId, TypeId?> _baseTypeIdsByType = [];
            private readonly Dictionary<TypeId, IReadOnlyList<TypeId>> _allInterfaceIdsByType = [];
            private readonly Dictionary<(TypeId TypeId, DispatchSlotKey SlotKey), MethodId> _declaredMethodByTypeAndSlot = [];
            private readonly HashSet<TypeId> _everInstantiatedTypes = [];
            private IReadOnlyList<TypeId>[] _concreteDescendantTypeIdsByType = [];
            private int _propertyCount;
            private int _publicInstancePropertySetterCount;
            private readonly List<EventSubscriptionCandidate> _eventSubscriptions = [];
            private readonly List<EventRaiseSite> _eventRaiseSites = [];
            private readonly Dictionary<MethodId, HashSet<int>> _delegateParameterInvocationsByMethod = [];
            private readonly List<PendingDelegateRelayCallSite> _pendingDelegateRelayCallSites = [];
            private readonly Dictionary<(string DeclaringTypeName, DispatchSlotKey Signature), List<MethodId>> _loadedMethodIdsByFallbackBindingKey = [];
            private readonly InterfaceExpansionMode _interfaceExpansionMode = interfaceExpansionMode;
            private readonly Action<string>? _progress = progress;

            /// <summary>
            /// Builds the final <see cref="AnalysisIndex"/>.
            /// </summary>
            public AnalysisIndex Build()
            {
                _progress?.Invoke("Indexing types and methods.");
                IndexTypesAndMethods();
                _progress?.Invoke($"Indexed {_types.Count} type(s) and {_methods.Count} method(s).");
                _progress?.Invoke("Indexing properties and events.");
                IndexPropertiesAndEvents();
                _progress?.Invoke($"Indexed {_propertyCount} propert(ies) and {_events.Count} event(s).");
                _progress?.Invoke("Building type relationship maps.");
                BuildTypeRelationships();
                _progress?.Invoke(_interfaceExpansionMode switch
                {
                    InterfaceExpansionMode.Off => "Building dispatch maps (interface broadening disabled).",
                    InterfaceExpansionMode.Strict => "Building dispatch maps (strict mode).",
                    _ => "Building dispatch maps (broad mode)."
                });
                BuildDispatchMaps();
                _progress?.Invoke("Scanning method bodies for graph edges.");
                ScanMethodBodies();
                _progress?.Invoke("Materializing delegate relay edges.");
                MaterializeDelegateRelayEdges();
                _progress?.Invoke("Linking async and iterator state-machine entry edges.");
                AddAsyncAndIteratorEdges();
                _progress?.Invoke("Materializing event raise edges.");
                MaterializeEventRaiseEdges();

                _progress?.Invoke("Building adjacency lists.");
                (IReadOnlyList<EdgeId>[] callsFrom, IReadOnlyList<EdgeId>[] calledBy) = BuildAdjacency(_methods.Count);
                _progress?.Invoke($"Index build complete. {_edges.Count} edge(s).");

                int overrideRelationshipCount = _overrideTargetsByBaseMethod.Values.Sum(targets => targets.Count);
                int interfaceImplementationRelationshipCount = _interfaceTargetsByMethod.Values.Sum(targets => targets.Count);

                return new AnalysisIndex(
                    _assemblies,
                    [.. _types],
                    [.. _methods],
                    _propertyCount,
                    _publicInstancePropertySetterCount,
                    [.. _events],
                    [.. _edges],
                    callsFrom,
                    calledBy,
                    overrideRelationshipCount,
                    interfaceImplementationRelationshipCount,
                    _everInstantiatedTypes.Count,
                    _concreteDescendantTypeIdsByType,
                    new ReadOnlyDictionary<TypeId, TypeId?>(_baseTypeIdsByType),
                    new ReadOnlyDictionary<TypeId, IReadOnlyList<TypeId>>(_allInterfaceIdsByType),
                    new ReadOnlyDictionary<TypeLookupKey, TypeId>(
                        _types.ToDictionary(type => CreateTypeLookupKey(type.TypeDef), type => type.Id)));
            }

            /// <summary>
            /// Converts mutable method-target sets into ordered lookup lists.
            /// </summary>
            /// <param name="map">The mutable map to convert.</param>
            private static Dictionary<MethodId, IReadOnlyList<MethodId>> FreezeMappings(Dictionary<MethodId, HashSet<MethodId>> map)
                => map.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<MethodId>)[.. pair.Value.OrderBy(methodId => methodId.Value)]);

            /// <summary>
            /// Scans all method bodies and emits graph edges.
            /// </summary>
            private void ScanMethodBodies()
            {
                MethodRecord[] scannableMethods = [.. _methods
                    .Where(method => method is { HasBody: true, MethodDefinition: not null })
                    .OrderBy(method => method.Id.Value)];

                int lastReportedProgressStep = 0;
                int scannedCount = 0;
                foreach (MethodRecord method in scannableMethods)
                {
                    ScanMethodBody(method);
                    scannedCount++;
                    if (ScanProgress.TryGetStepPercentage(scannedCount, scannableMethods.Length, ref lastReportedProgressStep, out int percentage))
                    {
                        _progress?.Invoke($"Scanned {scannedCount}/{scannableMethods.Length} method bod{(scannableMethods.Length == 1 ? "y" : "ies")} ({percentage}%).");
                    }
                }
            }

            /// <summary>
            /// Scans a single method body and emits graph edges.
            /// </summary>
            /// <param name="method">The method to scan.</param>
            private void ScanMethodBody(MethodRecord method)
            {
                if (method.MethodDefinition?.Body is not CilBody body || body.Instructions.Count == 0)
                {
                    return;
                }

                ControlFlowGraph graph = BuildControlFlowGraph(body);
                if (graph.Blocks.Length == 1)
                {
                    _ = ExecuteBlock(
                        method,
                        body,
                        graph,
                        graph.Blocks[0],
                        CreateInitialMethodFlowState(body, method.MethodDefinition!),
                        emitEffects: true,
                        cloneEntryState: false);
                    return;
                }

                MethodFlowState?[] entryStates = ComputeBlockEntryStates(method, body, graph);
                EmitControlFlowGraph(method, body, graph, entryStates);
            }

            /// <summary>
            /// Handles object construction calls and records constructor edges.
            /// </summary>
            /// <param name="caller">The calling method.</param>
            /// <param name="body">The current method body.</param>
            /// <param name="instruction">The current instruction.</param>
            /// <param name="state">The current flow state, when available.</param>
            /// <param name="stack">The simulated evaluation stack.</param>
            /// <param name="emitEffects">Whether graph edges and side effects should be emitted.</param>
            private void HandleNewObjectCall(MethodRecord caller, CilBody body, Instruction instruction, MethodFlowState? state, List<ValueState> stack, bool emitEffects)
            {
                if (!TryResolveMethodReference(instruction.Operand as IMethod, out ResolvedMethodReference targetMethod))
                {
                    return;
                }

                int parameterCount = targetMethod.MethodReference.MethodSig?.Params.Count ?? 0;
                IReadOnlyList<ValueState> arguments = PopMany(stack, parameterCount);
                ApplyByRefWriteBacks(caller.MethodDefinition!, body, targetMethod.MethodReference.MethodSig?.Params, arguments, state);
                if (emitEffects)
                {
                    IReadOnlyList<CallArgumentSummary> argumentSummaries = SummarizeCallArguments(arguments);
                    AddEdges(caller.Id, [targetMethod.MethodId], EdgeKind.ConstructorCall, argumentSummaries);
                }

                if (TryCreateRecognizedConstructedValue(targetMethod, arguments, out ValueState recognizedConstructedValue))
                {
                    stack.Add(recognizedConstructedValue);
                    return;
                }

                TypeDef? constructedType = targetMethod.MethodDefinition?.DeclaringType ?? targetMethod.MethodReference.DeclaringType?.ResolveTypeDef();
                if (emitEffects &&
                    constructedType is not null &&
                    TryResolveTypeId(constructedType, out TypeId constructedTypeId))
                {
                    _everInstantiatedTypes.Add(constructedTypeId);
                }

                var constructedTypeSig = constructedType?.ToTypeSig();
                if (constructedTypeSig is not null && IsDelegateType(constructedTypeSig))
                {
                    MethodId? methodPointer = arguments.FirstOrDefault(argument => argument.MethodPointerTarget is not null).MethodPointerTarget;
                    if (methodPointer is { } methodId)
                    {
                        stack.Add(ValueState.CreateDelegate(constructedTypeSig, [methodId]));
                        return;
                    }
                }

                if (constructedTypeSig is not null && constructedType is not null && TryResolveTypeId(constructedType, out TypeId concreteTypeId))
                {
                    stack.Add(ValueState.CreateConstructedObject(constructedTypeSig, concreteTypeId));
                    return;
                }

                stack.Add(constructedTypeSig is null ? ValueState.Unknown : ValueState.CreateUnknown(constructedTypeSig, mayOriginateExternally: false));
            }

            /// <summary>
            /// Handles a method call instruction and emits the corresponding edges.
            /// </summary>
            /// <param name="caller">The calling method.</param>
            /// <param name="body">The current method body.</param>
            /// <param name="instruction">The current instruction.</param>
            /// <param name="state">The current flow state, when available.</param>
            /// <param name="stack">The simulated evaluation stack.</param>
            /// <param name="emitEffects">Whether graph edges and side effects should be emitted.</param>
            private void HandleMethodCall(
                MethodRecord caller,
                CilBody body,
                Instruction instruction,
                MethodFlowState? state,
                List<ValueState> stack,
                bool emitEffects)
            {
                if (!TryResolveMethodReference(instruction.Operand as IMethod, out ResolvedMethodReference targetMethod))
                {
                    ApplyCallStackTransition(null, stack);
                    return;
                }

                int parameterCount = targetMethod.MethodReference.MethodSig?.Params.Count ?? 0;
                IReadOnlyList<ValueState> arguments = PopMany(stack, parameterCount);
                ValueState instance = !IsStaticMethod(targetMethod.MethodReference)
                    ? PopOrUnknown(stack)
                    : ValueState.Unknown;
                IReadOnlyList<CallArgumentSummary> argumentSummaries = SummarizeCallArguments(arguments);
                bool preservesCallerInstanceReceiver = PreservesCallerInstanceReceiver(caller, instance);

                if (emitEffects && TryHandleDelegateInvokeCall(caller, targetMethod.MethodReference, instance, argumentSummaries))
                {
                    PushReturnValue(targetMethod.MethodReference, stack, instance);
                    return;
                }

                if (emitEffects)
                {
                    EmitMethodCallEdges(caller, instruction, targetMethod, instance, argumentSummaries, preservesCallerInstanceReceiver);
                    RecordDeferredCallEffects(caller.Id, targetMethod, arguments);
                }

                ApplyByRefWriteBacks(caller.MethodDefinition!, body, targetMethod.MethodReference.MethodSig?.Params, arguments, state);

                if (TryCreateRecognizedCallReturnValue(targetMethod, arguments, out ValueState recognizedReturnValue))
                {
                    stack.Add(recognizedReturnValue);
                    return;
                }

                PushReturnValue(targetMethod.MethodReference, stack, instance);
            }

            /// <summary>
            /// Emits delegate-invocation edges and deferred event-raise evidence for a delegate call.
            /// </summary>
            /// <param name="caller">The calling method.</param>
            /// <param name="calledMethod">The called method reference.</param>
            /// <param name="instance">The delegate receiver value.</param>
            /// <param name="argumentSummaries">The summarized explicit call arguments.</param>
            private bool TryHandleDelegateInvokeCall(
                MethodRecord caller,
                IMethod calledMethod,
                ValueState instance,
                IReadOnlyList<CallArgumentSummary> argumentSummaries)
            {
                if (!IsDelegateInvoke(calledMethod))
                {
                    return false;
                }

                RecordDelegateParameterInvocation(caller, instance);
                AddEdges(caller.Id, instance.DelegateTargets, EdgeKind.DelegateInvoke, argumentSummaries);
                RecordEventRaiseSite(caller, calledMethod, instance);
                return true;
            }

            /// <summary>
            /// Emits the direct, virtual, or interface call edges for a non-delegate call.
            /// </summary>
            /// <param name="caller">The calling method.</param>
            /// <param name="instruction">The current call instruction.</param>
            /// <param name="targetMethod">The resolved callee.</param>
            /// <param name="instance">The receiver value.</param>
            /// <param name="argumentSummaries">The summarized explicit call arguments.</param>
            /// <param name="preservesCallerInstanceReceiver">Whether the receiver aliases the caller's <c>this</c>.</param>
            private void EmitMethodCallEdges(
                MethodRecord caller,
                Instruction instruction,
                ResolvedMethodReference targetMethod,
                ValueState instance,
                IReadOnlyList<CallArgumentSummary> argumentSummaries,
                bool preservesCallerInstanceReceiver)
            {
                if (instruction.OpCode == OpCodes.Call && !IsStaticMethod(targetMethod.MethodReference))
                {
                    EdgeKind edgeKind = ClassifyCallKind(targetMethod.MethodId);
                    AddEdges(
                        caller.Id,
                        [targetMethod.MethodId],
                        edgeKind,
                        argumentSummaries,
                        ResolveReceiverTypeConstraintForCallEdge(targetMethod.MethodId, edgeKind, instance),
                        preservesCallerInstanceReceiver);
                    return;
                }

                if (IsInterfaceDispatch(targetMethod.MethodReference))
                {
                    IReadOnlyList<MethodId> targets = ExpandInterfaceTargets(targetMethod.MethodId, instance);
                    AddEdges(
                        caller.Id,
                        targets,
                        EdgeKind.InterfaceDispatch,
                        argumentSummaries,
                        ResolveReceiverTypeConstraintForCallEdge(targetMethod.MethodId, EdgeKind.InterfaceDispatch, instance),
                        preservesCallerInstanceReceiver);
                    return;
                }

                if (RequiresVirtualExpansion(targetMethod.MethodReference))
                {
                    IReadOnlyList<MethodId> targets = ExpandVirtualTargets(targetMethod.MethodId, instance, preservesCallerInstanceReceiver);
                    AddEdges(
                        caller.Id,
                        targets,
                        EdgeKind.VirtualDispatch,
                        argumentSummaries,
                        ResolveReceiverTypeConstraintForCallEdge(targetMethod.MethodId, EdgeKind.VirtualDispatch, instance),
                        preservesCallerInstanceReceiver);
                    return;
                }

                EdgeKind directEdgeKind = ClassifyCallKind(targetMethod.MethodId);
                AddEdges(
                    caller.Id,
                    [targetMethod.MethodId],
                    directEdgeKind,
                    argumentSummaries,
                    ResolveReceiverTypeConstraintForCallEdge(targetMethod.MethodId, directEdgeKind, instance),
                    preservesCallerInstanceReceiver);
            }

            /// <summary>
            /// Pushes the return value for a call back onto the simulated stack.
            /// </summary>
            /// <param name="calledMethod">The called method.</param>
            /// <param name="stack">The simulated evaluation stack.</param>
            /// <param name="instance">The receiver value to reuse when appropriate.</param>
            private static void PushReturnValue(IMethod? calledMethod, List<ValueState> stack, ValueState instance)
            {
                TypeSig? returnType = calledMethod?.MethodSig?.RetType;
                if (returnType is null || returnType.ElementType == ElementType.Void)
                {
                    return;
                }

                if (IsDelegateType(returnType))
                {
                    stack.Add(ValueState.CreateDelegate(returnType, instance.DelegateTargets, mayOriginateExternally: instance.MayOriginateExternally));
                    return;
                }

                stack.Add(ValueState.CreateUnknown(returnType));
            }

            /// <summary>
            /// Applies conservative write-back effects for by-ref or out call arguments that point at tracked slots.
            /// </summary>
            /// <param name="method">The current method definition.</param>
            /// <param name="body">The current method body.</param>
            /// <param name="parameterTypes">The called parameter types.</param>
            /// <param name="arguments">The explicit call arguments.</param>
            /// <param name="state">The current flow state, when available.</param>
            private void ApplyByRefWriteBacks(
                MethodDef method,
                CilBody body,
                IList<TypeSig>? parameterTypes,
                IReadOnlyList<ValueState> arguments,
                MethodFlowState? state)
            {
                if (state is null || parameterTypes is null || parameterTypes.Count == 0 || arguments.Count == 0)
                {
                    return;
                }

                int count = Math.Min(parameterTypes.Count, arguments.Count);
                for (int argumentIndex = 0; argumentIndex < count; argumentIndex++)
                {
                    if (parameterTypes[argumentIndex] is not ByRefSig byRefParameterType)
                    {
                        continue;
                    }

                    WriteThroughTrackedAddress(
                        arguments[argumentIndex],
                        ValueState.CreateUnknown(byRefParameterType.Next, mayOriginateExternally: true),
                        byRefParameterType.Next,
                        method,
                        body,
                        state.Locals,
                        state.Arguments,
                        state.Fields,
                        overwriteExisting: true);
                }
            }

            /// <summary>
            /// Preserves tracked runtime type information across type-adapting stack operations.
            /// </summary>
            /// <param name="instruction">The adapting instruction.</param>
            /// <param name="stack">The simulated evaluation stack.</param>
            private void PreserveTypeAdaptedValue(Instruction instruction, List<ValueState> stack)
            {
                ValueState value = PopOrUnknown(stack);
                TypeSig? adaptedType = ToTypeSig(instruction.Operand as IType) ?? value.TypeSig;
                stack.Add(value
                    .WithTypeInfo(adaptedType, value.PossibleConcreteTypeIds, value.MayOriginateExternally)
                    .WithTypeConstraints(GetTypeConstraintsForAdaptedValue(instruction, value)));
            }

            /// <summary>
            /// Preserves useful pre-adaptation receiver type constraints across cast-like operations.
            /// </summary>
            /// <param name="instruction">The adapting instruction.</param>
            /// <param name="value">The pre-adaptation value.</param>
            private HashSet<TypeId> GetTypeConstraintsForAdaptedValue(Instruction instruction, ValueState value)
            {
                var constraintTypeIds = new HashSet<TypeId>(value.ReceiverTypeConstraintIds);
                if (instruction.OpCode != OpCodes.Castclass && instruction.OpCode != OpCodes.Isinst)
                {
                    return constraintTypeIds;
                }

                if (TryResolveTypeId(value.TypeSig, out TypeId priorTypeId))
                {
                    constraintTypeIds.Add(priorTypeId);
                }

                if (TryResolveTypeId(ToTypeSig(instruction.Operand as IType), out TypeId adaptedTypeId))
                {
                    constraintTypeIds.Add(adaptedTypeId);
                }

                return constraintTypeIds;
            }

            /// <summary>
            /// Creates the initial argument-value table for a method body scan.
            /// </summary>
            /// <param name="method">The method being scanned.</param>
            private static ValueState[] CreateInitialArgumentValues(MethodDef method)
            {
                int argumentCount = (method.IsStatic ? 0 : 1) + (method.MethodSig?.Params.Count ?? 0);
                var argumentValues = new ValueState[argumentCount];
                for (int argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
                {
                    argumentValues[argumentIndex] = ValueState.CreateUnknown(GetArgumentType(method, argumentIndex))
                        .WithOrigin(ValueOriginKind.Argument, argumentIndex, null);
                }

                return argumentValues;
            }

            /// <summary>
            /// Loads a local value from the simulated local table.
            /// </summary>
            /// <param name="localIndex">The local index to read.</param>
            /// <param name="locals">The simulated locals.</param>
            /// <param name="body">The method body being scanned.</param>
            private static ValueState LoadLocalValue(
                int localIndex,
                ValueState[] locals,
                CilBody body)
            {
                TypeSig localType = body.Variables[localIndex].Type;
                if (localIndex >= 0 && localIndex < locals.Length)
                {
                    ValueState local = locals[localIndex];
                    return local
                        .WithTypeInfo(local.TypeSig ?? localType, local.PossibleConcreteTypeIds, local.MayOriginateExternally)
                        .WithOrigin(ValueOriginKind.Local, localIndex, local.OriginFieldKey);
                }

                return ValueState.CreateUnknown(localType, mayOriginateExternally: false)
                    .WithOrigin(ValueOriginKind.Local, localIndex, null);
            }

            /// <summary>
            /// Loads an argument value from the simulated argument state.
            /// </summary>
            /// <param name="method">The method being scanned.</param>
            /// <param name="argumentIndex">The argument index to read.</param>
            /// <param name="argumentValues">The simulated argument values.</param>
            private static ValueState LoadArgumentValue(
                MethodDef method,
                int argumentIndex,
                ValueState[] argumentValues)
            {
                TypeSig? argumentType = GetArgumentType(method, argumentIndex);
                if (argumentIndex >= 0 && argumentIndex < argumentValues.Length)
                {
                    ValueState argumentValue = argumentValues[argumentIndex];
                    return argumentValue
                        .WithTypeInfo(argumentValue.TypeSig ?? argumentType, argumentValue.PossibleConcreteTypeIds, argumentValue.MayOriginateExternally)
                        .WithOrigin(ValueOriginKind.Argument, argumentIndex, argumentValue.OriginFieldKey);
                }

                return ValueState.CreateUnknown(argumentType)
                    .WithOrigin(ValueOriginKind.Argument, argumentIndex, null);
            }

            /// <summary>
            /// Loads a tracked field value.
            /// </summary>
            /// <param name="field">The field being read.</param>
            /// <param name="fieldValues">Tracked field values.</param>
            private static ValueState LoadFieldValue(
                FieldDef field,
                Dictionary<string, ValueState> fieldValues)
            {
                string fieldKey = GetFieldKey(field);
                if (fieldValues.TryGetValue(fieldKey, out ValueState fieldValue))
                {
                    if (field.FieldSig?.Type is { } fieldType)
                    {
                        fieldValue = fieldValue.WithTypeInfo(fieldType, fieldValue.PossibleConcreteTypeIds, mayOriginateExternally: true);
                    }

                    return fieldValue.WithOrigin(ValueOriginKind.Field, null, fieldKey);
                }

                return ValueState.CreateUnknown(field.FieldSig?.Type).WithOrigin(ValueOriginKind.Field, null, fieldKey);
            }

            /// <summary>
            /// Reads a value through a tracked local, argument, or field address.
            /// </summary>
            /// <param name="addressValue">The tracked address value.</param>
            /// <param name="method">The current method.</param>
            /// <param name="body">The current body.</param>
            /// <param name="locals">The tracked locals.</param>
            /// <param name="arguments">The tracked arguments.</param>
            /// <param name="fieldValues">The tracked fields.</param>
            /// <param name="fallbackType">The fallback pointed-at type.</param>
            private static ValueState ReadThroughTrackedAddress(
                ValueState addressValue,
                MethodDef method,
                CilBody body,
                ValueState[] locals,
                ValueState[] arguments,
                Dictionary<string, ValueState> fieldValues,
                TypeSig? fallbackType)
            {
                return addressValue.AddressTargetKind switch
                {
                    AddressTargetKind.Local when addressValue.AddressTargetIndex is { } localIndex =>
                        AdaptLoadedAddressValue(LoadLocalValue(localIndex, locals, body), fallbackType),
                    AddressTargetKind.Argument when addressValue.AddressTargetIndex is { } argumentIndex =>
                        AdaptLoadedAddressValue(LoadArgumentValue(method, argumentIndex, arguments), fallbackType),
                    AddressTargetKind.Field when addressValue.AddressTargetFieldKey is { } fieldKey && TryGetTrackedField(fieldKey, fieldValues, out ValueState fieldValue) =>
                        AdaptLoadedAddressValue(fieldValue, fallbackType),
                    _ => ValueState.CreateUnknown(fallbackType ?? addressValue.TypeSig)
                };
            }

            /// <summary>
            /// Writes a value through a tracked local, argument, or field address.
            /// </summary>
            /// <param name="addressValue">The tracked address value.</param>
            /// <param name="incomingValue">The value being stored.</param>
            /// <param name="slotType">The slot type.</param>
            /// <param name="method">The current method.</param>
            /// <param name="body">The current body.</param>
            /// <param name="locals">The tracked locals.</param>
            /// <param name="arguments">The tracked arguments.</param>
            /// <param name="fieldValues">The tracked fields.</param>
            /// <param name="overwriteExisting">Whether the write should clobber the prior tracked slot value.</param>
            private void WriteThroughTrackedAddress(
                ValueState addressValue,
                ValueState incomingValue,
                TypeSig? slotType,
                MethodDef method,
                CilBody body,
                ValueState[] locals,
                ValueState[] arguments,
                Dictionary<string, ValueState> fieldValues,
                bool overwriteExisting)
            {
                switch (addressValue.AddressTargetKind)
                {
                    case AddressTargetKind.Local when addressValue.AddressTargetIndex is { } localIndex:
                        {
                            ValueState updatedValue = overwriteExisting
                                ? CreateStoredTrackedValue(incomingValue, slotType ?? body.Variables[localIndex].Type, mayOriginateExternally: incomingValue.MayOriginateExternally, ValueOriginKind.Local, localIndex, null)
                                : MergeTrackedSlotValue(locals[localIndex], incomingValue, slotType ?? body.Variables[localIndex].Type, incomingValue.MayOriginateExternally)
                                    .WithOrigin(ValueOriginKind.Local, localIndex, incomingValue.OriginFieldKey);
                            locals[localIndex] = updatedValue;
                            return;
                        }

                    case AddressTargetKind.Argument when addressValue.AddressTargetIndex is { } argumentIndex:
                        {
                            TypeSig? argumentType = slotType ?? GetArgumentType(method, argumentIndex);
                            ValueState updatedValue = overwriteExisting
                                ? CreateStoredTrackedValue(incomingValue, argumentType, mayOriginateExternally: incomingValue.MayOriginateExternally, ValueOriginKind.Argument, argumentIndex, null)
                                : MergeTrackedSlotValue(arguments[argumentIndex], incomingValue, argumentType, incomingValue.MayOriginateExternally)
                                    .WithOrigin(ValueOriginKind.Argument, argumentIndex, incomingValue.OriginFieldKey);
                            arguments[argumentIndex] = updatedValue;
                            return;
                        }

                    case AddressTargetKind.Field when addressValue.AddressTargetFieldKey is { } fieldKey:
                        {
                            TypeSig? fieldType = ResolveFieldType(fieldKey);
                            ValueState existingFieldValue = fieldValues.TryGetValue(fieldKey, out ValueState currentFieldValue)
                                ? currentFieldValue
                                : ValueState.CreateUnknown(fieldType);
                            fieldValues[fieldKey] = overwriteExisting
                                ? CreateStoredTrackedValue(incomingValue, fieldType, mayOriginateExternally: true, ValueOriginKind.Field, null, fieldKey)
                                : MergeTrackedSlotValue(existingFieldValue, incomingValue, fieldType, mayOriginateExternally: true)
                                    .WithOrigin(ValueOriginKind.Field, null, fieldKey);
                            return;
                        }
                }
            }

            /// <summary>
            /// Creates the tracked slot value produced by a direct or indirect store.
            /// </summary>
            /// <param name="incomingValue">The incoming stored value.</param>
            /// <param name="slotType">The declared slot type.</param>
            /// <param name="mayOriginateExternally">Whether the slot may receive external values.</param>
            /// <param name="originKind">The slot origin kind.</param>
            /// <param name="originIndex">The slot origin index.</param>
            /// <param name="originFieldKey">The slot origin field key.</param>
            private static ValueState CreateStoredTrackedValue(
                ValueState incomingValue,
                TypeSig? slotType,
                bool mayOriginateExternally,
                ValueOriginKind originKind,
                int? originIndex,
                string? originFieldKey)
                => incomingValue
                    .WithTypeInfo(incomingValue.TypeSig ?? slotType, incomingValue.PossibleConcreteTypeIds, mayOriginateExternally)
                    .WithOrigin(originKind, originIndex, originFieldKey);

            /// <summary>
            /// Adapts a value read through a tracked address to the requested pointed-at type.
            /// </summary>
            /// <param name="value">The loaded slot value.</param>
            /// <param name="fallbackType">The pointed-at type.</param>
            private static ValueState AdaptLoadedAddressValue(ValueState value, TypeSig? fallbackType)
                => value.WithTypeInfo(value.TypeSig ?? fallbackType, value.PossibleConcreteTypeIds, value.MayOriginateExternally);

            /// <summary>
            /// Tries to read a tracked field value by key.
            /// </summary>
            /// <param name="fieldKey">The tracked field key.</param>
            /// <param name="fieldValues">The tracked field values.</param>
            /// <param name="fieldValue">The resolved field value.</param>
            private static bool TryGetTrackedField(string fieldKey, Dictionary<string, ValueState> fieldValues, out ValueState fieldValue)
            {
                if (fieldValues.TryGetValue(fieldKey, out fieldValue))
                {
                    return true;
                }

                fieldValue = default;
                return false;
            }

            /// <summary>
            /// Merges an assigned value into a tracked slot while preserving accumulated over-approximations.
            /// </summary>
            /// <param name="existingValue">The prior tracked slot value.</param>
            /// <param name="incomingValue">The newly assigned value.</param>
            /// <param name="slotType">The declared slot type.</param>
            /// <param name="mayOriginateExternally">Whether values read from the slot may come from outside the method.</param>
            private static ValueState MergeTrackedSlotValue(
                ValueState existingValue,
                ValueState incomingValue,
                TypeSig? slotType,
                bool mayOriginateExternally)
            {
                TypeSig? mergedType = incomingValue.TypeSig ?? slotType;
                return incomingValue with
                {
                    TypeSig = mergedType,
                    DelegateTargets = MergeMethodIds(existingValue.DelegateTargets, incomingValue.DelegateTargets),
                    PossibleConcreteTypeIds = MergeTypeIds(existingValue.PossibleConcreteTypeIds, incomingValue.PossibleConcreteTypeIds),
                    MayOriginateExternally = mayOriginateExternally
                };
            }

            /// <summary>
            /// Expands virtual dispatch targets for a base method.
            /// </summary>
            /// <param name="baseMethodId">The base method identifier.</param>
            /// <param name="receiver">The receiver value at the call site.</param>
            /// <param name="preservesCallerInstanceReceiver">Whether the call preserves the caller's receiver identity.</param>
            private IReadOnlyList<MethodId> ExpandVirtualTargets(MethodId baseMethodId, ValueState receiver, bool preservesCallerInstanceReceiver)
            {
                if (TryResolveConcreteReceiverTypeId(receiver, out TypeId receiverTypeId) &&
                    TryResolveVirtualTargetForReceiver(baseMethodId, receiverTypeId, out MethodId receiverTargetId))
                {
                    return [receiverTargetId];
                }

                IReadOnlyList<MethodId> candidateTargets = ResolveVirtualTargetsForConcreteReceiverCandidates(baseMethodId, receiver);
                if (candidateTargets.Count > 0)
                {
                    return candidateTargets;
                }

                IReadOnlyList<MethodId> constrainedTargets = ResolveTargetsFromStrongReceiverConstraints(GetBroadVirtualTargets(baseMethodId), baseMethodId, receiver);
                if (constrainedTargets.Count > 0)
                {
                    return constrainedTargets;
                }

                if (preservesCallerInstanceReceiver)
                {
                    IReadOnlyList<MethodId> preservedTargets = GetBroadVirtualTargets(baseMethodId);
                    if (preservedTargets.Count > 0)
                    {
                        return preservedTargets;
                    }
                }

                return _interfaceExpansionMode == InterfaceExpansionMode.Broad
                    ? GetBroadVirtualTargets(baseMethodId)
                    : [];
            }

            /// <summary>
            /// Expands interface dispatch targets for an interface method.
            /// </summary>
            /// <param name="interfaceMethodId">The interface method identifier.</param>
            /// <param name="receiver">The receiver value at the call site.</param>
            private IReadOnlyList<MethodId> ExpandInterfaceTargets(MethodId interfaceMethodId, ValueState receiver)
            {
                if (TryResolveConcreteReceiverTypeId(receiver, out TypeId receiverTypeId) &&
                    TryResolveInterfaceTargetForReceiver(interfaceMethodId, receiverTypeId, out MethodId receiverTargetId))
                {
                    return [receiverTargetId];
                }

                IReadOnlyList<MethodId> candidateTargets = ResolveInterfaceTargetsForConcreteReceiverCandidates(interfaceMethodId, receiver);
                if (candidateTargets.Count > 0)
                {
                    return candidateTargets;
                }

                if (_interfaceExpansionMode == InterfaceExpansionMode.Off)
                {
                    return [];
                }

                IReadOnlyList<MethodId> targets = GetBroadInterfaceTargets(interfaceMethodId);
                if (targets.Count == 0)
                {
                    return [];
                }

                bool usedExactConstructedReceiverConstraint = false;
                if (_interfaceExpansionMode == InterfaceExpansionMode.Strict &&
                    TryGetExactConstructedReceiverTypeConstraint(interfaceMethodId, receiver, out TypeSig exactReceiverTypeConstraint))
                {
                    // Strict mode should only keep closed-generic interface targets that truly match the receiver shape.
                    targets = FilterTargetsByExactReceiverTypeConstraint(targets, exactReceiverTypeConstraint);
                    if (targets.Count == 0)
                    {
                        return [];
                    }

                    usedExactConstructedReceiverConstraint = true;
                }

                IReadOnlyList<MethodId> constrainedTargets = ResolveTargetsFromStrongReceiverConstraints(targets, interfaceMethodId, receiver);
                if (constrainedTargets.Count > 0)
                {
                    if (_interfaceExpansionMode == InterfaceExpansionMode.Strict &&
                        IsEnumeratorStyleInterfaceDispatch(interfaceMethodId) &&
                        constrainedTargets.Count > 1)
                    {
                        return [];
                    }

                    return constrainedTargets;
                }

                if (_interfaceExpansionMode == InterfaceExpansionMode.Strict && usedExactConstructedReceiverConstraint)
                {
                    if (IsEnumeratorStyleInterfaceDispatch(interfaceMethodId) &&
                        targets.Count > 1)
                    {
                        return [];
                    }

                    return targets;
                }

                if (_interfaceExpansionMode != InterfaceExpansionMode.Broad)
                {
                    return [];
                }

                if (HasStrongReceiverTypeConstraints(interfaceMethodId, receiver))
                {
                    return [];
                }

                IReadOnlyList<MethodId> prunedTargets = PruneInstantiatedTargets(targets);
                return prunedTargets.Count > 0 ? prunedTargets : targets;
            }

            /// <summary>
            /// Prunes dynamic targets to types observed via instantiation when possible.
            /// </summary>
            /// <param name="targets">The candidate targets.</param>
            private MethodId[] PruneInstantiatedTargets(IReadOnlyList<MethodId> targets)
            {
                MethodId[] prunedTargets = [.. targets
                    .Where(target => _everInstantiatedTypes.Contains(_methods[target.Value].DeclaringTypeId))
                    .Distinct()
                    .OrderBy(target => target.Value)];

                return prunedTargets;
            }

            /// <summary>
            /// Tries to resolve a concrete loaded receiver type from a simulated value.
            /// </summary>
            /// <param name="receiver">The receiver value to inspect.</param>
            /// <param name="receiverTypeId">The resolved receiver type identifier.</param>
            private bool TryResolveConcreteReceiverTypeId(ValueState receiver, out TypeId receiverTypeId)
            {
                if (receiver.PossibleConcreteTypeIds.Count == 1)
                {
                    receiverTypeId = receiver.PossibleConcreteTypeIds[0];
                    return true;
                }

                if (!TryResolveTypeId(receiver.TypeSig, out receiverTypeId))
                {
                    return false;
                }

                TypeRecord receiverType = _types[receiverTypeId.Value];
                return receiverType is { IsInterface: false, IsAbstract: false };
            }

            /// <summary>
            /// Tries to resolve the exact virtual dispatch target for a concrete receiver type.
            /// </summary>
            /// <param name="baseMethodId">The base virtual method identifier.</param>
            /// <param name="receiverTypeId">The concrete receiver type identifier.</param>
            /// <param name="targetMethodId">The resolved target method identifier.</param>
            private bool TryResolveVirtualTargetForReceiver(MethodId baseMethodId, TypeId receiverTypeId, out MethodId targetMethodId)
            {
                targetMethodId = default;
                MethodRecord baseMethodBuilder = _methods[baseMethodId.Value];
                if (baseMethodBuilder.MethodDefinition is not null &&
                    TryFindVirtualTargetForReceiver(baseMethodBuilder.MethodDefinition, receiverTypeId, out MethodId target))
                {
                    targetMethodId = target;
                    return true;
                }

                foreach (MethodId equivalentMethodId in GetEquivalentLoadedMethodIdsByFallbackBinding(baseMethodId))
                {
                    MethodRecord equivalentMethod = _methods[equivalentMethodId.Value];
                    if (equivalentMethod.MethodDefinition is null ||
                        !TryFindVirtualTargetForReceiver(equivalentMethod.MethodDefinition, receiverTypeId, out target))
                    {
                        continue;
                    }

                    targetMethodId = target;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Gets virtual dispatch targets from equivalent loaded base methods that share the same signature.
            /// </summary>
            /// <param name="baseMethodId">The base virtual method identifier.</param>
            private IReadOnlyList<MethodId> GetEquivalentVirtualTargets(MethodId baseMethodId)
                => [.. GetEquivalentLoadedMethodIdsByFallbackBinding(baseMethodId)
                    .SelectMany(candidateMethodId => _overrideTargetsByBaseMethod.TryGetValue(candidateMethodId, out IReadOnlyList<MethodId>? candidateTargets) ? candidateTargets : [])
                    .Distinct()
                    .OrderBy(targetMethodId => targetMethodId.Value)];

            /// <summary>
            /// Tries to resolve the exact virtual dispatch target for a concrete receiver type from a loaded base method definition.
            /// </summary>
            /// <param name="baseMethod">The loaded base method definition.</param>
            /// <param name="receiverTypeId">The concrete receiver type identifier.</param>
            /// <param name="targetMethodId">The resolved target method identifier.</param>
            private bool TryFindVirtualTargetForReceiver(MethodDef baseMethod, TypeId receiverTypeId, out MethodId targetMethodId)
            {
                targetMethodId = default;
                MethodId? resolvedTargetMethodId = FindVirtualDispatchTarget(GetHierarchy(receiverTypeId), baseMethod);
                if (resolvedTargetMethodId is not { } target)
                {
                    return false;
                }

                targetMethodId = target;
                return true;
            }

            /// <summary>
            /// Tries to resolve the exact interface dispatch target for a concrete receiver type.
            /// </summary>
            /// <param name="interfaceMethodId">The interface method identifier.</param>
            /// <param name="receiverTypeId">The concrete receiver type identifier.</param>
            /// <param name="targetMethodId">The resolved target method identifier.</param>
            private bool TryResolveInterfaceTargetForReceiver(MethodId interfaceMethodId, TypeId receiverTypeId, out MethodId targetMethodId)
            {
                targetMethodId = default;
                IReadOnlyList<TypeId> receiverHierarchy = GetHierarchy(receiverTypeId);
                foreach (MethodId loadedInterfaceMethodId in GetLoadedInterfaceDispatchMethodIds(interfaceMethodId))
                {
                    MethodRecord loadedInterfaceMethod = _methods[loadedInterfaceMethodId.Value];
                    if (loadedInterfaceMethod.MethodDefinition is null)
                    {
                        continue;
                    }

                    MethodId? resolvedTargetMethodId = FindInterfaceDispatchTarget(receiverHierarchy, loadedInterfaceMethod.MethodDefinition);
                    if (resolvedTargetMethodId is not { } target)
                    {
                        continue;
                    }

                    targetMethodId = target;
                    return true;
                }

                MethodId? slotTargetMethodId = FindInterfaceDispatchTarget(
                    receiverHierarchy,
                    BuildDispatchSlotKey(_methods[interfaceMethodId.Value].MethodReference));
                if (slotTargetMethodId is { } slotTarget)
                {
                    targetMethodId = slotTarget;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Gets interface dispatch targets from equivalent loaded interface methods that share the same signature.
            /// </summary>
            /// <param name="interfaceMethodId">The interface method identifier.</param>
            private IReadOnlyList<MethodId> GetEquivalentInterfaceTargets(MethodId interfaceMethodId)
            {
                var targets = new HashSet<MethodId>();
                foreach (MethodId candidateMethodId in GetLoadedInterfaceDispatchMethodIds(interfaceMethodId))
                {
                    if (!_interfaceTargetsByMethod.TryGetValue(candidateMethodId, out IReadOnlyList<MethodId>? candidateTargets))
                    {
                        continue;
                    }

                    foreach (MethodId targetMethodId in candidateTargets)
                    {
                        targets.Add(targetMethodId);
                    }
                }

                return [.. targets.OrderBy(targetMethodId => targetMethodId.Value)];
            }

            /// <summary>
            /// Gets the loaded interface methods that represent the same dispatch slot as a call-site method.
            /// </summary>
            /// <param name="interfaceMethodId">The interface method identifier.</param>
            private IReadOnlyList<MethodId> GetLoadedInterfaceDispatchMethodIds(MethodId interfaceMethodId)
            {
                var methodIds = new HashSet<MethodId>();
                MethodRecord interfaceMethod = _methods[interfaceMethodId.Value];
                if (interfaceMethod.MethodDefinition is not null)
                {
                    methodIds.Add(interfaceMethodId);
                }

                foreach (MethodId equivalentMethodId in GetEquivalentLoadedMethodIdsByFallbackBinding(interfaceMethodId))
                {
                    methodIds.Add(equivalentMethodId);
                }

                if (!TryResolveDispatchDeclaringTypeId(interfaceMethodId, out TypeId interfaceTypeId) ||
                    !_types[interfaceTypeId.Value].IsInterface)
                {
                    return [.. methodIds.OrderBy(methodId => methodId.Value)];
                }

                DispatchSlotKey slotKey = BuildDispatchSlotKey(interfaceMethod.MethodReference);
                foreach (MethodDef method in _types[interfaceTypeId.Value].TypeDef.Methods.Where(method => !method.IsStatic && BuildDispatchSlotKey(method) == slotKey))
                {
                    if (TryGetLoadedMethodId(method, out MethodId loadedMethodId))
                    {
                        methodIds.Add(loadedMethodId);
                    }
                }

                return [.. methodIds.OrderBy(methodId => methodId.Value)];
            }

            /// <summary>
            /// Gets other loaded methods that share the same fallback-binding identity as a method.
            /// </summary>
            /// <param name="methodId">The method identifier to expand.</param>
            private IReadOnlyList<MethodId> GetEquivalentLoadedMethodIdsByFallbackBinding(MethodId methodId)
            {
                MethodRecord method = _methods[methodId.Value];
                if (!TryGetFallbackBindingKey(method.MethodReference, out (string DeclaringTypeName, DispatchSlotKey Signature) key) ||
                    !_loadedMethodIdsByFallbackBindingKey.TryGetValue(key, out List<MethodId>? candidateMethodIds))
                {
                    return [];
                }

                return [.. candidateMethodIds
                    .Where(candidateMethodId => candidateMethodId != methodId)
                    .Distinct()
                    .OrderBy(candidateMethodId => candidateMethodId.Value)];
            }

            /// <summary>
            /// Resolves virtual dispatch targets from tracked concrete receiver candidates.
            /// </summary>
            /// <param name="baseMethodId">The base virtual method identifier.</param>
            /// <param name="receiver">The receiver value at the call site.</param>
            private IReadOnlyList<MethodId> ResolveVirtualTargetsForConcreteReceiverCandidates(MethodId baseMethodId, ValueState receiver)
            {
                var targets = new HashSet<MethodId>();
                foreach (TypeId receiverTypeId in receiver.PossibleConcreteTypeIds)
                {
                    if (TryResolveVirtualTargetForReceiver(baseMethodId, receiverTypeId, out MethodId targetMethodId))
                    {
                        targets.Add(targetMethodId);
                    }
                }

                return [.. targets.OrderBy(targetMethodId => targetMethodId.Value)];
            }

            /// <summary>
            /// Resolves interface dispatch targets from tracked concrete receiver candidates.
            /// </summary>
            /// <param name="interfaceMethodId">The interface method identifier.</param>
            /// <param name="receiver">The receiver value at the call site.</param>
            private IReadOnlyList<MethodId> ResolveInterfaceTargetsForConcreteReceiverCandidates(MethodId interfaceMethodId, ValueState receiver)
            {
                var targets = new HashSet<MethodId>();
                foreach (TypeId receiverTypeId in receiver.PossibleConcreteTypeIds)
                {
                    if (TryResolveInterfaceTargetForReceiver(interfaceMethodId, receiverTypeId, out MethodId targetMethodId))
                    {
                        targets.Add(targetMethodId);
                    }
                }

                return [.. targets.OrderBy(targetMethodId => targetMethodId.Value)];
            }

            /// <summary>
            /// Gets the broad virtual target universe for a base method.
            /// </summary>
            /// <param name="baseMethodId">The base virtual method identifier.</param>
            private IReadOnlyList<MethodId> GetBroadVirtualTargets(MethodId baseMethodId)
            {
                if (_overrideTargetsByBaseMethod.TryGetValue(baseMethodId, out IReadOnlyList<MethodId>? targets))
                {
                    return targets;
                }

                IReadOnlyList<MethodId> equivalentTargets = GetEquivalentVirtualTargets(baseMethodId);
                if (equivalentTargets.Count > 0)
                {
                    return equivalentTargets;
                }

                return !_methods[baseMethodId.Value].IsAbstract ? [baseMethodId] : [];
            }

            /// <summary>
            /// Gets the broad interface target universe for an interface method.
            /// </summary>
            /// <param name="interfaceMethodId">The interface method identifier.</param>
            private IReadOnlyList<MethodId> GetBroadInterfaceTargets(MethodId interfaceMethodId)
            {
                if (_interfaceTargetsByMethod.TryGetValue(interfaceMethodId, out IReadOnlyList<MethodId>? exactTargets))
                {
                    return exactTargets;
                }

                IReadOnlyList<MethodId> equivalentTargets = GetEquivalentInterfaceTargets(interfaceMethodId);
                if (equivalentTargets.Count > 0)
                {
                    return equivalentTargets;
                }

                return GetInheritedInterfaceTargets(interfaceMethodId);
            }

            /// <summary>
            /// Gets interface targets through inherited interface slots when the call site was bound against a sub-interface view.
            /// </summary>
            /// <param name="interfaceMethodId">The interface method identifier.</param>
            private IReadOnlyList<MethodId> GetInheritedInterfaceTargets(MethodId interfaceMethodId)
            {
                if (!TryResolveDispatchDeclaringTypeId(interfaceMethodId, out TypeId interfaceTypeId) ||
                    !_types[interfaceTypeId.Value].IsInterface)
                {
                    return [];
                }

                DispatchSlotKey slotKey = BuildDispatchSlotKey(_methods[interfaceMethodId.Value].MethodReference);
                var targets = new HashSet<MethodId>();
                foreach (TypeId inheritedInterfaceId in _allInterfaceIdsByType.GetValueOrDefault(interfaceTypeId, []))
                {
                    foreach (MethodDef method in _types[inheritedInterfaceId.Value].TypeDef.Methods.Where(method => !method.IsStatic && BuildDispatchSlotKey(method) == slotKey))
                    {
                        if (!TryGetLoadedMethodId(method, out MethodId inheritedMethodId) ||
                            !_interfaceTargetsByMethod.TryGetValue(inheritedMethodId, out IReadOnlyList<MethodId>? candidateTargets))
                        {
                            continue;
                        }

                        foreach (MethodId candidateTargetId in candidateTargets)
                        {
                            targets.Add(candidateTargetId);
                        }
                    }
                }

                return [.. targets.OrderBy(targetMethodId => targetMethodId.Value)];
            }

            /// <summary>
            /// Resolves targets when a receiver carries a strong narrowing constraint.
            /// </summary>
            /// <param name="targets">The candidate targets.</param>
            /// <param name="dispatchMethodId">The dispatch slot being resolved.</param>
            /// <param name="receiver">The receiver value at the call site.</param>
            private IReadOnlyList<MethodId> ResolveTargetsFromStrongReceiverConstraints(
                IReadOnlyList<MethodId> targets,
                MethodId dispatchMethodId,
                ValueState receiver)
            {
                TypeId[] receiverTypeConstraintIds = [.. GetStrongReceiverTypeConstraintIds(dispatchMethodId, receiver)];
                if (receiverTypeConstraintIds.Length == 0)
                {
                    return [];
                }

                return FilterTargetsByReceiverTypeConstraints(targets, receiverTypeConstraintIds);
            }

            /// <summary>
            /// Determines whether the receiver carries any strong dispatch-narrowing constraints.
            /// </summary>
            /// <param name="dispatchMethodId">The dispatch slot being resolved.</param>
            /// <param name="receiver">The receiver value at the call site.</param>
            private bool HasStrongReceiverTypeConstraints(MethodId dispatchMethodId, ValueState receiver)
                => GetStrongReceiverTypeConstraintIds(dispatchMethodId, receiver).Count > 0;

            /// <summary>
            /// Tries to get an exact constructed receiver-type constraint that should narrow strict interface dispatch.
            /// </summary>
            /// <param name="dispatchMethodId">The dispatch slot being resolved.</param>
            /// <param name="receiver">The receiver value at the call site.</param>
            /// <param name="receiverTypeConstraint">The exact constructed receiver type constraint.</param>
            private bool TryGetExactConstructedReceiverTypeConstraint(MethodId dispatchMethodId, ValueState receiver, out TypeSig receiverTypeConstraint)
            {
                receiverTypeConstraint = null!;
                TypeSig? receiverTypeSig = StripConstraintTypeModifiers(receiver.TypeSig);
                if (receiverTypeSig is null ||
                    !ContainsConstructedGenericSignature(receiverTypeSig) ||
                    !TryResolveDispatchDeclaringTypeId(dispatchMethodId, out TypeId dispatchDeclaringTypeId) ||
                    !DoesTypeSignatureSatisfyTypeConstraint(receiverTypeSig, dispatchDeclaringTypeId))
                {
                    return false;
                }

                receiverTypeConstraint = receiverTypeSig;
                return true;
            }

            /// <summary>
            /// Gets the receiver type constraints that are strong enough to justify strict dispatch.
            /// </summary>
            /// <param name="dispatchMethodId">The dispatch slot being resolved.</param>
            /// <param name="receiver">The receiver value at the call site.</param>
            private HashSet<TypeId> GetStrongReceiverTypeConstraintIds(MethodId dispatchMethodId, ValueState receiver)
            {
                var receiverTypeConstraintIds = new HashSet<TypeId>(
                    receiver.ReceiverTypeConstraintIds
                        .Where(receiverTypeConstraintId => IsStrongPreservedReceiverTypeConstraint(dispatchMethodId, receiverTypeConstraintId)));
                if (TryResolveTypeId(receiver.TypeSig, out TypeId receiverTypeId) &&
                    IsMeaningfulReceiverTypeConstraint(dispatchMethodId, receiverTypeId))
                {
                    receiverTypeConstraintIds.Add(receiverTypeId);
                }

                return receiverTypeConstraintIds;
            }

            /// <summary>
            /// Determines whether a preserved cast-time receiver constraint is strong enough to justify dispatch narrowing.
            /// </summary>
            /// <param name="dispatchMethodId">The dispatch slot being resolved.</param>
            /// <param name="receiverTypeConstraintId">The preserved receiver constraint.</param>
            private bool IsStrongPreservedReceiverTypeConstraint(MethodId dispatchMethodId, TypeId receiverTypeConstraintId)
            {
                if (IsMeaningfulReceiverTypeConstraint(dispatchMethodId, receiverTypeConstraintId))
                {
                    return true;
                }

                return IsExactAbstractVirtualDispatchConstraint(dispatchMethodId, receiverTypeConstraintId);
            }

            /// <summary>
            /// Allows successful casts to the exact abstract virtual slot type to justify later virtual dispatch.
            /// </summary>
            /// <param name="dispatchMethodId">The dispatch slot being resolved.</param>
            /// <param name="receiverTypeConstraintId">The preserved receiver constraint.</param>
            private bool IsExactAbstractVirtualDispatchConstraint(MethodId dispatchMethodId, TypeId receiverTypeConstraintId)
            {
                if (!TryResolveDispatchDeclaringTypeId(dispatchMethodId, out TypeId dispatchDeclaringTypeId) ||
                    receiverTypeConstraintId != dispatchDeclaringTypeId)
                {
                    return false;
                }

                TypeRecord dispatchDeclaringType = _types[dispatchDeclaringTypeId.Value];
                return dispatchDeclaringType is { IsClass: true, IsAbstract: true };
            }

            /// <summary>
            /// Determines whether a receiver type meaningfully narrows a dispatch slot beyond its declared target type.
            /// </summary>
            /// <param name="dispatchMethodId">The dispatch slot being resolved.</param>
            /// <param name="receiverTypeConstraintId">The candidate receiver constraint.</param>
            private bool IsMeaningfulReceiverTypeConstraint(MethodId dispatchMethodId, TypeId receiverTypeConstraintId)
            {
                MethodRecord dispatchMethod = _methods[dispatchMethodId.Value];
                string receiverConstraintTypeName = _types[receiverTypeConstraintId.Value].TypeDef.FullName;
                string dispatchDeclaringTypeName = GetTypeDefinitionDisplayName(dispatchMethod.MethodReference.DeclaringType);
                if (string.Equals(receiverConstraintTypeName, dispatchDeclaringTypeName, StringComparison.Ordinal))
                {
                    return false;
                }

                return TryResolveDispatchDeclaringTypeId(dispatchMethodId, out TypeId dispatchDeclaringTypeId)
                    ? IsAssignableTo(dispatchDeclaringTypeId, receiverTypeConstraintId, _baseTypeIdsByType, _allInterfaceIdsByType)
                    : true;
            }

            /// <summary>
            /// Resolves the declaring type of a dispatch slot, even when the slot itself was rebound only by reference.
            /// </summary>
            /// <param name="dispatchMethodId">The dispatch slot identifier.</param>
            /// <param name="dispatchDeclaringTypeId">The resolved declaring type identifier.</param>
            private bool TryResolveDispatchDeclaringTypeId(MethodId dispatchMethodId, out TypeId dispatchDeclaringTypeId)
            {
                if (_dispatchDeclaringTypeIdsByMethod.TryGetValue(dispatchMethodId, out TypeId? cachedDispatchDeclaringTypeId))
                {
                    if (cachedDispatchDeclaringTypeId is { } resolvedCachedDispatchDeclaringTypeId)
                    {
                        dispatchDeclaringTypeId = resolvedCachedDispatchDeclaringTypeId;
                        return true;
                    }

                    dispatchDeclaringTypeId = default;
                    return false;
                }

                MethodRecord dispatchMethod = _methods[dispatchMethodId.Value];
                if (dispatchMethod.DeclaringTypeId.Value >= 0)
                {
                    dispatchDeclaringTypeId = dispatchMethod.DeclaringTypeId;
                    _dispatchDeclaringTypeIdsByMethod[dispatchMethodId] = dispatchDeclaringTypeId;
                    return true;
                }

                if (TryResolveTypeId(dispatchMethod.MethodReference.DeclaringType, out dispatchDeclaringTypeId))
                {
                    _dispatchDeclaringTypeIdsByMethod[dispatchMethodId] = dispatchDeclaringTypeId;
                    return true;
                }

                string declaringTypeName = GetTypeDefinitionDisplayName(dispatchMethod.MethodReference.DeclaringType);
                IAssembly? declaringTypeAssembly = dispatchMethod.MethodReference.DeclaringType?.DefinitionAssembly;
                UTF8String? declaringAssemblySimpleName = declaringTypeAssembly?.Name;
                string? declaringAssemblyFullName = declaringTypeAssembly?.FullName;
                Version? declaringAssemblyVersion = declaringTypeAssembly?.Version;

                TypeRecord? fallbackType = _types
                    .Where(type => string.Equals(type.TypeDef.FullName, declaringTypeName, StringComparison.Ordinal))
                    .OrderByDescending(type => string.Equals(
                        type.TypeDef.Module.Assembly?.FullName,
                        declaringAssemblyFullName,
                        StringComparison.Ordinal))
                    .ThenByDescending(type => string.Equals(
                        type.TypeDef.Module.Assembly?.Name?.String,
                        declaringAssemblySimpleName,
                        StringComparison.Ordinal))
                    .ThenBy(type => GetAssemblyVersionDistance(type.TypeDef.Module.Assembly?.Version, declaringAssemblyVersion))
                    .ThenByDescending(type => IsCoreFacadeReferencePreferredTarget(declaringAssemblySimpleName, type.TypeDef.Module.Assembly?.Name?.String))
                    .ThenBy(type => type.Id.Value)
                    .FirstOrDefault();
                if (fallbackType is null)
                {
                    dispatchDeclaringTypeId = default;
                    _dispatchDeclaringTypeIdsByMethod[dispatchMethodId] = null;
                    return false;
                }

                dispatchDeclaringTypeId = fallbackType.Id;
                _dispatchDeclaringTypeIdsByMethod[dispatchMethodId] = dispatchDeclaringTypeId;
                return true;
            }

            /// <summary>
            /// Determines whether an interface dispatch slot is part of iterator or enumerator traversal.
            /// </summary>
            /// <param name="dispatchMethodId">The dispatch slot identifier.</param>
            private bool IsEnumeratorStyleInterfaceDispatch(MethodId dispatchMethodId)
            {
                MethodRecord dispatchMethod = _methods[dispatchMethodId.Value];
                string declaringTypeName = GetTypeDefinitionDisplayName(dispatchMethod.MethodReference.DeclaringType);
                string methodName = dispatchMethod.MethodReference.Name;
                return (declaringTypeName, methodName) switch
                {
                    ("System.Collections.IEnumerable", "GetEnumerator") => true,
                    ("System.Collections.Generic.IEnumerable`1", "GetEnumerator") => true,
                    ("System.Collections.IDictionary", "GetEnumerator") => true,
                    ("System.Collections.IEnumerator", "MoveNext") => true,
                    ("System.Collections.IEnumerator", "get_Current") => true,
                    ("System.Collections.Generic.IEnumerator`1", "get_Current") => true,
                    ("System.Collections.IDictionaryEnumerator", "get_Current") => true,
                    ("System.Collections.IDictionaryEnumerator", "get_Entry") => true,
                    ("System.Collections.IDictionaryEnumerator", "get_Key") => true,
                    ("System.Collections.IDictionaryEnumerator", "get_Value") => true,
                    _ => false
                };
            }

            /// <summary>
            /// Filters interface targets to those whose declaring types preserve an exact constructed receiver signature.
            /// </summary>
            /// <param name="targets">The candidate dispatch targets.</param>
            /// <param name="receiverTypeConstraint">The exact constructed receiver type constraint.</param>
            private IReadOnlyList<MethodId> FilterTargetsByExactReceiverTypeConstraint(IReadOnlyList<MethodId> targets, TypeSig receiverTypeConstraint)
            {
                return [.. targets
                    .Where(target =>
                    {
                        TypeId declaringTypeId = _methods[target.Value].DeclaringTypeId;
                        return declaringTypeId.Value >= 0 &&
                               DoesTypeSatisfyExactReceiverTypeConstraint(declaringTypeId, receiverTypeConstraint);
                    })
                    .Distinct()
                    .OrderBy(target => target.Value)];
            }

            /// <summary>
            /// Filters dispatch targets to those compatible with explicit receiver type constraints.
            /// </summary>
            /// <param name="targets">The candidate dispatch targets.</param>
            /// <param name="receiverTypeConstraintIds">The receiver constraints to apply.</param>
            private IReadOnlyList<MethodId> FilterTargetsByReceiverTypeConstraints(IReadOnlyList<MethodId> targets, TypeId[] receiverTypeConstraintIds)
            {
                if (receiverTypeConstraintIds.Length == 0)
                {
                    return [];
                }

                return [.. targets
                    .Where(target =>
                    {
                        TypeId declaringTypeId = _methods[target.Value].DeclaringTypeId;
                        return receiverTypeConstraintIds.All(receiverTypeConstraintId =>
                            IsAssignableTo(receiverTypeConstraintId, declaringTypeId, _baseTypeIdsByType, _allInterfaceIdsByType));
                    })
                    .Distinct()
                    .OrderBy(target => target.Value)];
            }

            /// <summary>
            /// Determines whether an exact type signature is relevant to a loaded type constraint.
            /// </summary>
            /// <param name="typeSignature">The exact type signature to evaluate.</param>
            /// <param name="targetTypeId">The loaded type constraint.</param>
            private bool DoesTypeSignatureSatisfyTypeConstraint(TypeSig typeSignature, TypeId targetTypeId)
                => TryResolveTypeId(typeSignature, out TypeId candidateTypeId) &&
                   IsAssignableTo(targetTypeId, candidateTypeId, _baseTypeIdsByType, _allInterfaceIdsByType);

            /// <summary>
            /// Determines whether a candidate target declaring type preserves an exact constructed receiver signature.
            /// </summary>
            /// <param name="candidateTypeId">The candidate target declaring type.</param>
            /// <param name="receiverTypeConstraint">The exact constructed receiver type constraint.</param>
            private bool DoesTypeSatisfyExactReceiverTypeConstraint(TypeId candidateTypeId, TypeSig receiverTypeConstraint)
            {
                var pending = new Stack<TypeId>();
                var visited = new HashSet<TypeId>();
                pending.Push(candidateTypeId);

                while (pending.Count > 0)
                {
                    TypeId currentTypeId = pending.Pop();
                    if (!visited.Add(currentTypeId))
                    {
                        continue;
                    }

                    TypeDef currentType = _types[currentTypeId.Value].TypeDef;
                    if (TypeSignaturesMatch(currentType.ToTypeSig(), receiverTypeConstraint))
                    {
                        return true;
                    }

                    TypeSig? baseTypeSig = StripConstraintTypeModifiers(ToTypeSig(currentType.BaseType));
                    if (TypeSignaturesMatch(baseTypeSig, receiverTypeConstraint))
                    {
                        return true;
                    }

                    if (TryResolveTypeId(baseTypeSig, out TypeId baseTypeId))
                    {
                        pending.Push(baseTypeId);
                    }

                    foreach (InterfaceImpl implementedInterface in currentType.Interfaces)
                    {
                        TypeSig? interfaceTypeSig = StripConstraintTypeModifiers(ToTypeSig(implementedInterface.Interface));
                        if (TypeSignaturesMatch(interfaceTypeSig, receiverTypeConstraint))
                        {
                            return true;
                        }

                        if (TryResolveTypeId(interfaceTypeSig, out TypeId interfaceTypeId))
                        {
                            pending.Push(interfaceTypeId);
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// Determines whether two exact type signatures match after removing non-semantic modifiers.
            /// </summary>
            /// <param name="left">The left type signature.</param>
            /// <param name="right">The right type signature.</param>
            private static bool TypeSignaturesMatch(TypeSig? left, TypeSig? right)
                => left is not null &&
                   right is not null &&
                   s_typeSigComparer.Equals(StripConstraintTypeModifiers(left), StripConstraintTypeModifiers(right));

            /// <summary>
            /// Removes non-semantic modifiers from a type signature while preserving generic instantiations.
            /// </summary>
            /// <param name="type">The type signature to normalize.</param>
            private static TypeSig? StripConstraintTypeModifiers(TypeSig? type)
            {
                while (type is not null)
                {
                    switch (type)
                    {
                        case ByRefSig byRefSig:
                            type = byRefSig.Next;
                            continue;
                        case CModOptSig cModOptSig:
                            type = cModOptSig.Next;
                            continue;
                        case CModReqdSig cModReqdSig:
                            type = cModReqdSig.Next;
                            continue;
                        case PinnedSig pinnedSig:
                            type = pinnedSig.Next;
                            continue;
                        default:
                            return type;
                    }
                }

                return null;
            }

            /// <summary>
            /// Adds edges from a source method to target methods.
            /// </summary>
            /// <param name="sourceId">The source method identifier.</param>
            /// <param name="targets">The candidate target identifiers.</param>
            /// <param name="kind">The edge kind.</param>
            /// <param name="argumentSummaries">The summarized explicit call arguments.</param>
            /// <param name="receiverTypeConstraintId">The receiver-type constraint to attach to emitted edges.</param>
            /// <param name="preservesCallerInstanceReceiver">Whether the call preserves the caller instance receiver.</param>
            private void AddEdges(
                MethodId sourceId,
                IReadOnlyList<MethodId> targets,
                EdgeKind kind,
                IReadOnlyList<CallArgumentSummary>? argumentSummaries = null,
                TypeId? receiverTypeConstraintId = null,
                bool preservesCallerInstanceReceiver = false)
            {
                if (targets.Count == 0)
                {
                    return;
                }

                MethodId[] orderedTargets = [.. targets.Distinct().OrderBy(target => target.Value)];
                foreach (MethodId target in orderedTargets)
                {
                    TypeId? effectiveReceiverTypeConstraintId = receiverTypeConstraintId ?? GetImplicitReceiverTypeConstraintForCallEdge(kind, target);
                    var edgeId = new EdgeId(_edges.Count);
                    _edges.Add(new EdgeRecord(
                        edgeId,
                        sourceId,
                        target,
                        kind,
                        argumentSummaries,
                        effectiveReceiverTypeConstraintId,
                        preservesCallerInstanceReceiver));
                }
            }

            /// <summary>
            /// Determines whether a call receiver is the caller's <c>this</c> instance.
            /// </summary>
            /// <param name="caller">The caller method.</param>
            /// <param name="instance">The receiver value at the call site.</param>
            private static bool PreservesCallerInstanceReceiver(MethodRecord caller, ValueState instance)
                => caller.IsInstance &&
                   instance is { OriginKind: ValueOriginKind.Argument, OriginIndex: 0 };

            /// <summary>
            /// Resolves a concrete receiver-type constraint for a call edge when available.
            /// </summary>
            /// <param name="targetMethodId">The target method identifier.</param>
            /// <param name="kind">The edge kind being emitted.</param>
            /// <param name="instance">The receiver value at the call site.</param>
            private TypeId? ResolveReceiverTypeConstraintForCallEdge(MethodId targetMethodId, EdgeKind kind, ValueState instance)
            {
                if (TryResolveConcreteReceiverTypeId(instance, out TypeId receiverTypeId))
                {
                    return receiverTypeId;
                }

                return GetImplicitReceiverTypeConstraintForCallEdge(kind, targetMethodId);
            }

            /// <summary>
            /// Gets an implicit receiver-type constraint for a call edge when the target itself narrows the receiver.
            /// </summary>
            /// <param name="kind">The edge kind being emitted.</param>
            /// <param name="targetMethodId">The target method identifier.</param>
            private TypeId? GetImplicitReceiverTypeConstraintForCallEdge(EdgeKind kind, MethodId targetMethodId)
            {
                if (kind != EdgeKind.VirtualDispatch)
                {
                    return null;
                }

                MethodRecord targetMethod = _methods[targetMethodId.Value];
                if (!targetMethod.IsInstance || targetMethod.DeclaringTypeId.Value < 0)
                {
                    return null;
                }

                TypeRecord targetType = _types[targetMethod.DeclaringTypeId.Value];
                return targetType is { IsClass: true, IsAbstract: false }
                    ? targetMethod.DeclaringTypeId
                    : null;
            }

            /// <summary>
            /// Classifies the edge kind for a direct method target.
            /// </summary>
            /// <param name="targetMethodId">The target method identifier.</param>
            private EdgeKind ClassifyCallKind(MethodId targetMethodId)
            {
                MethodRecord targetMethod = _methods[targetMethodId.Value];
                if (targetMethod.IsConstructor)
                {
                    return EdgeKind.ConstructorCall;
                }

                if (targetMethod.IsPropertyGetter || targetMethod.IsPropertySetter)
                {
                    return EdgeKind.PropertyAccessor;
                }

                if (targetMethod.IsEventAdd)
                {
                    return EdgeKind.EventAccessor;
                }

                return EdgeKind.DirectCall;
            }

            /// <summary>
            /// Determines whether a method participates in virtual instance dispatch.
            /// </summary>
            /// <param name="method">The method to evaluate.</param>
            private static bool IsVirtualInstanceMethod(MethodDef method)
                => method is { IsStatic: false, IsVirtual: true, IsPrivate: false };

            /// <summary>
            /// Determines whether a method reference requires virtual expansion.
            /// </summary>
            /// <param name="method">The method reference to evaluate.</param>
            private static bool RequiresVirtualExpansion(IMethod? method)
            {
                MethodDef? resolvedMethod = method?.ResolveMethodDef();
                return resolvedMethod is not null &&
                       resolvedMethod is { IsStatic: false, IsVirtual: true, IsFinal: false, DeclaringType.IsInterface: false };
            }

            /// <summary>
            /// Determines whether a method reference represents interface dispatch.
            /// </summary>
            /// <param name="method">The method reference to evaluate.</param>
            private static bool IsInterfaceDispatch(IMethod? method)
                => method?.DeclaringType?.ResolveTypeDef()?.IsInterface == true;

            /// <summary>
            /// Determines whether a method reference represents delegate invocation.
            /// </summary>
            /// <param name="method">The method reference to evaluate.</param>
            private static bool IsDelegateInvoke(IMethod? method)
                => method is not null &&
                   string.Compare(method.Name, "Invoke", StringComparison.Ordinal) == 0 &&
                   IsDelegateType(method.DeclaringType?.ToTypeSig());

            /// <summary>
            /// Determines whether a method reference is static.
            /// </summary>
            /// <param name="method">The method reference to evaluate.</param>
            private static bool IsStaticMethod(IMethod method)
                => method.ResolveMethodDef()?.IsStatic ?? method.MethodSig?.HasThis == false;

            /// <summary>
            /// Gets the generated state-machine type associated with a method.
            /// </summary>
            /// <param name="method">The method to inspect.</param>
            private static TypeDef? GetStateMachineType(MethodDef method)
            {
                foreach (CustomAttribute? attribute in method.CustomAttributes)
                {
                    string attributeName = attribute.AttributeType.FullName;
                    if (!string.Equals(attributeName, "System.Runtime.CompilerServices.AsyncStateMachineAttribute", StringComparison.Ordinal) &&
                        !string.Equals(attributeName, "System.Runtime.CompilerServices.IteratorStateMachineAttribute", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (attribute.ConstructorArguments.Count == 1 &&
                        attribute.ConstructorArguments[0].Value is ITypeDefOrRef stateMachineType)
                    {
                        return stateMachineType.ResolveTypeDef();
                    }
                }

                return null;
            }

            /// <summary>
            /// Determines whether a type is a delegate type.
            /// </summary>
            /// <param name="type">The type signature to evaluate.</param>
            private static bool IsDelegateType(TypeSig? type)
            {
                TypeDef? resolvedType = GetTypeDefinitionSignature(type)?.ToTypeDefOrRef()?.ResolveTypeDef();
                while (resolvedType is not null)
                {
                    if (string.Equals(resolvedType.FullName, "System.MulticastDelegate", StringComparison.Ordinal))
                    {
                        return true;
                    }

                    resolvedType = resolvedType.BaseType?.ResolveTypeDef();
                }

                return false;
            }

            /// <summary>
            /// Applies a default stack transition for an opcode.
            /// </summary>
            /// <param name="stack">The simulated evaluation stack.</param>
            /// <param name="opCode">The opcode to apply.</param>
            private static void ApplyDefaultStackTransition(List<ValueState> stack, OpCode opCode)
            {
                int popCount = opCode.StackBehaviourPop switch
                {
                    StackBehaviour.Pop0 => 0,
                    StackBehaviour.Pop1 => 1,
                    StackBehaviour.Pop1_pop1 => 2,
                    StackBehaviour.Popi => 1,
                    StackBehaviour.Popi_pop1 => 2,
                    StackBehaviour.Popi_popi => 2,
                    StackBehaviour.Popi_popi8 => 2,
                    StackBehaviour.Popi_popi_popi => 3,
                    StackBehaviour.Popi_popr4 => 2,
                    StackBehaviour.Popi_popr8 => 2,
                    StackBehaviour.Popref => 1,
                    StackBehaviour.Popref_pop1 => 2,
                    StackBehaviour.Popref_popi => 2,
                    StackBehaviour.Popref_popi_popi => 3,
                    StackBehaviour.Popref_popi_popi8 => 3,
                    StackBehaviour.Popref_popi_popr4 => 3,
                    StackBehaviour.Popref_popi_popr8 => 3,
                    StackBehaviour.Popref_popi_popref => 3,
                    _ => 0
                };

                for (int i = 0; i < popCount; i++)
                {
                    _ = PopOrUnknown(stack);
                }

                int pushCount = opCode.StackBehaviourPush switch
                {
                    StackBehaviour.Push0 => 0,
                    StackBehaviour.Push1 => 1,
                    StackBehaviour.Pushi => 1,
                    StackBehaviour.Pushi8 => 1,
                    StackBehaviour.Pushr4 => 1,
                    StackBehaviour.Pushr8 => 1,
                    StackBehaviour.Pushref => 1,
                    StackBehaviour.Push1_push1 => 2,
                    _ => 0
                };

                for (int i = 0; i < pushCount; i++)
                {
                    stack.Add(ValueState.Unknown);
                }
            }

            /// <summary>
            /// Applies the stack transition for a call instruction.
            /// </summary>
            /// <param name="targetMethod">The called method.</param>
            /// <param name="stack">The simulated evaluation stack.</param>
            private static void ApplyCallStackTransition(IMethod? targetMethod, List<ValueState> stack)
            {
                int parameterCount = targetMethod?.MethodSig?.Params.Count ?? 0;
                _ = PopMany(stack, parameterCount);
                if (targetMethod is not null && !IsStaticMethod(targetMethod))
                {
                    _ = PopOrUnknown(stack);
                }

                PushReturnValue(targetMethod, stack, ValueState.Unknown);
            }

            /// <summary>
            /// Pops a value from the stack or returns an unknown value when the stack is empty.
            /// </summary>
            /// <param name="stack">The simulated evaluation stack.</param>
            private static ValueState PopOrUnknown(List<ValueState> stack)
            {
                if (stack.Count == 0)
                {
                    return ValueState.Unknown;
                }

                ValueState value = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                return value;
            }

            /// <summary>
            /// Pops multiple values from the stack in call-order.
            /// </summary>
            /// <param name="stack">The simulated evaluation stack.</param>
            /// <param name="count">The number of values to pop.</param>
            private static ValueState[] PopMany(List<ValueState> stack, int count)
            {
                if (count <= 0)
                {
                    return [];
                }

                var popped = new ValueState[count];
                for (int i = count - 1; i >= 0; i--)
                {
                    popped[i] = PopOrUnknown(stack);
                }

                return popped;
            }

            /// <summary>
            /// Resolves a method reference to a loaded or external method identifier.
            /// </summary>
            /// <param name="method">The method reference to resolve.</param>
            /// <param name="methodReference">The resolved method reference data.</param>
            private bool TryResolveMethodReference(IMethod? method, out ResolvedMethodReference methodReference)
            {
                methodReference = default;
                if (method is null)
                {
                    return false;
                }

                if (_resolvedMethodReferencesByMethod.TryGetValue(method, out methodReference))
                {
                    return true;
                }

                if (TryBindExactLoadedMethod(method, out methodReference))
                {
                    _resolvedMethodReferencesByMethod[method] = methodReference;
                    return true;
                }

                if (TryRebindLoadedMethod(method, out methodReference))
                {
                    _resolvedMethodReferencesByMethod[method] = methodReference;
                    return true;
                }

                methodReference = CreateExternalMethodReference(method);
                _resolvedMethodReferencesByMethod[method] = methodReference;
                return true;
            }

            /// <summary>
            /// Tries to bind a method reference to the exact loaded method definition that it resolves to.
            /// </summary>
            /// <param name="method">The method reference to resolve.</param>
            /// <param name="methodReference">The resolved loaded method reference data.</param>
            private bool TryBindExactLoadedMethod(IMethod method, out ResolvedMethodReference methodReference)
            {
                methodReference = default;
                MethodDef? resolvedMethod = method.ResolveMethodDef();
                if (resolvedMethod is null || !TryGetLoadedMethodId(resolvedMethod, out MethodId methodId))
                {
                    return false;
                }

                methodReference = new ResolvedMethodReference(methodId, method, resolvedMethod);
                return true;
            }

            /// <summary>
            /// Adds a loaded method to the fallback-binding lookup.
            /// </summary>
            /// <param name="method">The loaded method definition.</param>
            /// <param name="methodId">The corresponding method identifier.</param>
            private void AddLoadedMethodFallbackBindingKey(MethodDef method, MethodId methodId)
            {
                if (!TryGetFallbackBindingKey(method, out (string DeclaringTypeName, DispatchSlotKey Signature) key))
                {
                    return;
                }

                if (!_loadedMethodIdsByFallbackBindingKey.TryGetValue(key, out List<MethodId>? methodIds))
                {
                    methodIds = [];
                    _loadedMethodIdsByFallbackBindingKey[key] = methodIds;
                }

                methodIds.Add(methodId);
            }

            /// <summary>
            /// Tries to rebind a method reference to a loaded method using the fallback-binding lookup.
            /// </summary>
            /// <param name="method">The method reference to resolve.</param>
            /// <param name="methodReference">The rebound loaded method reference data.</param>
            private bool TryRebindLoadedMethod(IMethod method, out ResolvedMethodReference methodReference)
            {
                methodReference = default;
                if (!TryGetFallbackBindingKey(method, out (string DeclaringTypeName, DispatchSlotKey Signature) key) ||
                    !_loadedMethodIdsByFallbackBindingKey.TryGetValue(key, out List<MethodId>? candidateMethodIds))
                {
                    return false;
                }

                MethodId methodId = SelectBestFallbackBindingCandidate(method, candidateMethodIds);
                methodReference = new ResolvedMethodReference(methodId, method, _methods[methodId.Value].MethodDefinition);
                return true;
            }

            /// <summary>
            /// Creates the fallback-binding key used to rebind equivalent loaded methods.
            /// </summary>
            /// <param name="method">The method to key.</param>
            /// <param name="key">The computed fallback-binding key.</param>
            private static bool TryGetFallbackBindingKey(IMethod method, out (string DeclaringTypeName, DispatchSlotKey Signature) key)
            {
                key = default;
                if (UsesConstructedGenericSignature(method))
                {
                    return false;
                }

                IMethod signatureSource = method.ResolveMethodDef() ?? method;
                string declaringTypeName = signatureSource.DeclaringType is null
                    ? "<global>"
                    : GetTypeDefinitionDisplayName(signatureSource.DeclaringType);

                key = (declaringTypeName, BuildMethodBindingSignatureKey(signatureSource));
                return true;
            }

            /// <summary>
            /// Selects the best loaded fallback-binding candidate for a method reference.
            /// </summary>
            /// <param name="method">The method reference being rebound.</param>
            /// <param name="candidateMethodIds">The matching loaded-method candidates.</param>
            private MethodId SelectBestFallbackBindingCandidate(IMethod method, List<MethodId> candidateMethodIds)
            {
                if (candidateMethodIds.Count == 1)
                {
                    return candidateMethodIds[0];
                }

                IAssembly? declaringTypeAssembly = method.DeclaringType?.DefinitionAssembly;
                UTF8String? declaringAssemblySimpleName = declaringTypeAssembly?.Name;
                string? declaringAssemblyFullName = declaringTypeAssembly?.FullName;
                Version? declaringAssemblyVersion = declaringTypeAssembly?.Version;

                return candidateMethodIds
                    .Select(candidateMethodId => _methods[candidateMethodId.Value])
                    .OrderByDescending(candidate => string.Equals(
                        candidate.MethodDefinition?.DeclaringType.Module.Assembly?.FullName,
                        declaringAssemblyFullName,
                        StringComparison.Ordinal))
                    .ThenByDescending(candidate => string.Equals(
                        candidate.MethodDefinition?.DeclaringType.Module.Assembly?.Name?.String,
                        declaringAssemblySimpleName,
                        StringComparison.Ordinal))
                    .ThenBy(candidate => GetAssemblyVersionDistance(candidate.MethodDefinition?.DeclaringType.Module.Assembly?.Version, declaringAssemblyVersion))
                    .ThenByDescending(candidate => IsCoreFacadeReferencePreferredTarget(declaringAssemblySimpleName, candidate.MethodDefinition?.DeclaringType.Module.Assembly?.Name?.String))
                    .ThenBy(candidate => candidate.Id.Value)
                    .Select(candidate => candidate.Id)
                    .First();
            }

            /// <summary>
            /// Computes an ordering distance between two assembly versions.
            /// </summary>
            /// <param name="candidateVersion">The candidate loaded assembly version.</param>
            /// <param name="referenceVersion">The referenced assembly version.</param>
            private static long GetAssemblyVersionDistance(Version? candidateVersion, Version? referenceVersion)
            {
                if (candidateVersion is null || referenceVersion is null)
                {
                    return long.MaxValue;
                }

                return
                    Math.Abs(candidateVersion.Major - referenceVersion.Major) * 1_000_000_000L +
                    Math.Abs(candidateVersion.Minor - referenceVersion.Minor) * 1_000_000L +
                    Math.Abs(candidateVersion.Build - referenceVersion.Build) * 1_000L +
                    Math.Abs(candidateVersion.Revision - referenceVersion.Revision);
            }

            /// <summary>
            /// Prefers the platform core library when a facade reference points into the modern BCL surface.
            /// </summary>
            /// <param name="referenceAssemblySimpleName">The simple name of the referenced declaring-type assembly.</param>
            /// <param name="candidateAssemblySimpleName">The simple name of the loaded candidate assembly.</param>
            private static bool IsCoreFacadeReferencePreferredTarget(string? referenceAssemblySimpleName, string? candidateAssemblySimpleName)
                => string.Equals(candidateAssemblySimpleName, "System.Private.CoreLib", StringComparison.Ordinal) &&
                   referenceAssemblySimpleName is not null &&
                   (referenceAssemblySimpleName.StartsWith("System.", StringComparison.Ordinal) ||
                    string.Equals(referenceAssemblySimpleName, "netstandard", StringComparison.Ordinal));

            /// <summary>
            /// Determines whether a method reference carries constructed generic instantiations that should remain outside fallback rebinding.
            /// </summary>
            /// <param name="method">The method reference to inspect.</param>
            private static bool UsesConstructedGenericSignature(IMethod method)
                => ContainsConstructedGenericSignature(ToTypeSig(method.DeclaringType)) ||
                   ContainsConstructedGenericSignature(method.MethodSig?.RetType) ||
                   method.MethodSig?.Params.Any(ContainsConstructedGenericSignature) == true;

            /// <summary>
            /// Determines whether a type signature is or wraps a constructed generic instantiation.
            /// </summary>
            /// <param name="type">The type signature to inspect.</param>
            private static bool ContainsConstructedGenericSignature(TypeSig? type)
            {
                while (type is not null)
                {
                    switch (type)
                    {
                        case GenericInstSig:
                            return true;
                        case ByRefSig byRefSig:
                            type = byRefSig.Next;
                            continue;
                        case PtrSig ptrSig:
                            type = ptrSig.Next;
                            continue;
                        case SZArraySig szArraySig:
                            type = szArraySig.Next;
                            continue;
                        case ArraySig arraySig:
                            type = arraySig.Next;
                            continue;
                        case CModOptSig cModOptSig:
                            type = cModOptSig.Next;
                            continue;
                        case CModReqdSig cModReqdSig:
                            type = cModReqdSig.Next;
                            continue;
                        case PinnedSig pinnedSig:
                            type = pinnedSig.Next;
                            continue;
                        default:
                            return false;
                    }
                }

                return false;
            }

            /// <summary>
            /// Resolves a field reference to a loaded field definition.
            /// </summary>
            /// <param name="field">The field reference to resolve.</param>
            /// <param name="resolvedField">The resolved field definition.</param>
            private static bool TryResolveFieldReference(IField? field, out FieldDef resolvedField)
            {
                resolvedField = field?.ResolveFieldDef()!;
                return resolvedField is not null;
            }

            /// <summary>
            /// Creates the synthetic external-method target used when no loaded binding succeeds.
            /// </summary>
            /// <param name="method">The external method reference.</param>
            private ResolvedMethodReference CreateExternalMethodReference(IMethod method)
                => new(GetOrCreateExternalMethodId(method), method, null);

            /// <summary>
            /// Gets or creates a synthetic method identifier for an external method reference.
            /// </summary>
            /// <param name="method">The external method reference.</param>
            private MethodId GetOrCreateExternalMethodId(IMethod method)
            {
                string key = $"external:{FormatMethodDisplayName(method)}";
                if (_externalMethodIdsByKey.TryGetValue(key, out MethodId existing))
                {
                    return existing;
                }

                var methodId = new MethodId(_methods.Count);
                _externalMethodIdsByKey[key] = methodId;
                _methods.Add(CreateExternalMethodRecord(methodId, method));
                return methodId;
            }

            /// <summary>
            /// Tries to get a loaded method identifier for a method definition.
            /// </summary>
            /// <param name="method">The method definition to resolve.</param>
            /// <param name="methodId">The resolved method identifier.</param>
            private bool TryGetLoadedMethodId(MethodDef method, out MethodId methodId)
                => _methodIdsByMethodDef.TryGetValue(method, out methodId);

            /// <summary>
            /// Tries to resolve a loaded type identifier from a dnlib type.
            /// </summary>
            /// <param name="type">The type to resolve.</param>
            /// <param name="typeId">The resolved type identifier.</param>
            private bool TryResolveTypeId(IType? type, out TypeId typeId)
            {
                typeId = default;
                if (type is null)
                {
                    return false;
                }

                TypeDef? resolvedType = GetTypeDefinitionSignature(ToTypeSig(type))?.ToTypeDefOrRef()?.ResolveTypeDef();
                return resolvedType is not null && (_typeIdsByTypeDef.TryGetValue(resolvedType, out typeId) || _typeIdsByKey.TryGetValue(GetTypeKey(resolvedType), out typeId));
            }

            /// <summary>
            /// Gets the directly declared interfaces for a type.
            /// </summary>
            /// <param name="type">The type to inspect.</param>
            private static IEnumerable<ITypeDefOrRef> GetDirectInterfaces(TypeDef type)
                => type.Interfaces.Select(@interface => @interface.Interface).Where(@interface => @interface is not null);

            /// <summary>
            /// Determines the declared visibility bucket of a type for root-eligibility checks.
            /// </summary>
            /// <param name="type">The type to evaluate.</param>
            private static RootTypeVisibility GetRootTypeVisibility(TypeDef type)
            {
                if (type.DeclaringType is null)
                {
                    return type.IsPublic ? RootTypeVisibility.Public : RootTypeVisibility.Internal;
                }

                if (type.IsNestedPublic)
                {
                    return RootTypeVisibility.Public;
                }

                if (type.IsNestedPrivate)
                {
                    return RootTypeVisibility.Private;
                }

                if (type.IsNestedFamily)
                {
                    return RootTypeVisibility.Protected;
                }

                if (type.IsNestedAssembly)
                {
                    return RootTypeVisibility.Internal;
                }

                if (type.IsNestedFamilyOrAssembly)
                {
                    return RootTypeVisibility.ProtectedInternal;
                }

                if (type.IsNestedFamilyAndAssembly)
                {
                    return RootTypeVisibility.PrivateProtected;
                }

                throw new InvalidOperationException($"Unsupported nested type visibility on '{type.FullName}'.");
            }

            /// <summary>
            /// Determines whether a type is externally visible.
            /// </summary>
            /// <param name="type">The type to evaluate.</param>
            private static bool IsPubliclyVisible(TypeDef type)
            {
                if (type.DeclaringType is { } declaringType)
                {
                    return type.IsNestedPublic && IsPubliclyVisible(declaringType);
                }

                return type.IsPublic;
            }

            /// <summary>
            /// Builds a stable lookup key for a type definition.
            /// </summary>
            /// <param name="type">The type definition to format.</param>
            private static string GetTypeKey(TypeDef type)
                => $"{type.Module.Assembly?.FullName ?? type.Module.Name}:{type.FullName}";

            /// <summary>
            /// Builds a stable lookup key for a field definition.
            /// </summary>
            /// <param name="field">The field definition to format.</param>
            private static string GetFieldKey(FieldDef field)
                => $"{GetTypeKey(field.DeclaringType)}::{field.Name}::{field.MDToken.Raw:X8}";

            /// <summary>
            /// Gets the argument type for a method parameter index.
            /// </summary>
            /// <param name="method">The method to inspect.</param>
            /// <param name="argumentIndex">The argument index.</param>
            private static TypeSig? GetArgumentType(MethodDef method, int argumentIndex)
            {
                if (!method.IsStatic)
                {
                    if (argumentIndex == 0)
                    {
                        return method.DeclaringType.ToTypeSig();
                    }

                    argumentIndex--;
                }

                return argumentIndex >= 0 && argumentIndex < (method.MethodSig?.Params.Count ?? 0)
                    ? method.MethodSig!.Params[argumentIndex]
                    : null;
            }

            /// <summary>
            /// Tries to decode a local-load instruction into a local index.
            /// </summary>
            /// <param name="code">The instruction opcode.</param>
            /// <param name="operand">The instruction operand.</param>
            /// <param name="index">The decoded local index.</param>
            private static bool TryGetLocalLoadIndex(Code code, object? operand, out int index)
            {
                index = code switch
                {
                    Code.Ldloc_0 => 0,
                    Code.Ldloc_1 => 1,
                    Code.Ldloc_2 => 2,
                    Code.Ldloc_3 => 3,
                    Code.Ldloc_S or Code.Ldloc => operand is Local local ? local.Index : -1,
                    _ => -1
                };

                return index >= 0;
            }

            /// <summary>
            /// Tries to decode a local-address instruction into a local index.
            /// </summary>
            /// <param name="code">The instruction opcode.</param>
            /// <param name="operand">The instruction operand.</param>
            /// <param name="index">The decoded local index.</param>
            private static bool TryGetLocalAddressIndex(Code code, object? operand, out int index)
            {
                index = code switch
                {
                    Code.Ldloca or Code.Ldloca_S => operand is Local local ? local.Index : -1,
                    _ => -1
                };

                return index >= 0;
            }

            /// <summary>
            /// Tries to decode a local-store instruction into a local index.
            /// </summary>
            /// <param name="code">The instruction opcode.</param>
            /// <param name="operand">The instruction operand.</param>
            /// <param name="index">The decoded local index.</param>
            private static bool TryGetLocalStoreIndex(Code code, object? operand, out int index)
            {
                index = code switch
                {
                    Code.Stloc_0 => 0,
                    Code.Stloc_1 => 1,
                    Code.Stloc_2 => 2,
                    Code.Stloc_3 => 3,
                    Code.Stloc_S or Code.Stloc => operand is Local local ? local.Index : -1,
                    _ => -1
                };

                return index >= 0;
            }

            /// <summary>
            /// Tries to decode an argument-load instruction into an argument index.
            /// </summary>
            /// <param name="method">The declaring method.</param>
            /// <param name="code">The instruction opcode.</param>
            /// <param name="operand">The instruction operand.</param>
            /// <param name="index">The decoded argument index.</param>
            private static bool TryGetArgumentLoadIndex(MethodDef method, Code code, object? operand, out int index)
            {
                index = code switch
                {
                    Code.Ldarg_0 => 0,
                    Code.Ldarg_1 => 1,
                    Code.Ldarg_2 => 2,
                    Code.Ldarg_3 => 3,
                    Code.Ldarg_S or Code.Ldarg => operand is Parameter parameter ? GetArgumentIndex(method, parameter) : -1,
                    _ => -1
                };

                return index >= 0;
            }

            /// <summary>
            /// Tries to decode an argument-address instruction into an argument index.
            /// </summary>
            /// <param name="method">The declaring method.</param>
            /// <param name="code">The instruction opcode.</param>
            /// <param name="operand">The instruction operand.</param>
            /// <param name="index">The decoded argument index.</param>
            private static bool TryGetArgumentAddressIndex(MethodDef method, Code code, object? operand, out int index)
            {
                index = code switch
                {
                    Code.Ldarga or Code.Ldarga_S => operand is Parameter parameter ? GetArgumentIndex(method, parameter) : -1,
                    _ => -1
                };

                return index >= 0;
            }

            /// <summary>
            /// Tries to decode an argument-store instruction into an argument index.
            /// </summary>
            /// <param name="method">The declaring method.</param>
            /// <param name="code">The instruction opcode.</param>
            /// <param name="operand">The instruction operand.</param>
            /// <param name="index">The decoded argument index.</param>
            private static bool TryGetArgumentStoreIndex(MethodDef method, Code code, object? operand, out int index)
            {
                index = code switch
                {
                    Code.Starg or Code.Starg_S => operand is Parameter parameter ? GetArgumentIndex(method, parameter) : -1,
                    _ => -1
                };

                return index >= 0;
            }

            /// <summary>
            /// Determines whether an opcode loads through a managed or unmanaged address.
            /// </summary>
            /// <param name="code">The opcode to inspect.</param>
            private static bool IsLoadIndirectInstruction(Code code)
                => code is Code.Ldind_I1 or
                   Code.Ldind_U1 or
                   Code.Ldind_I2 or
                   Code.Ldind_U2 or
                   Code.Ldind_I4 or
                   Code.Ldind_U4 or
                   Code.Ldind_I8 or
                   Code.Ldind_I or
                   Code.Ldind_R4 or
                   Code.Ldind_R8 or
                   Code.Ldind_Ref or
                   Code.Ldobj;

            /// <summary>
            /// Determines whether an opcode stores through a managed or unmanaged address.
            /// </summary>
            /// <param name="code">The opcode to inspect.</param>
            private static bool IsStoreIndirectInstruction(Code code)
                => code is Code.Stind_I1 or
                   Code.Stind_I2 or
                   Code.Stind_I4 or
                   Code.Stind_I8 or
                   Code.Stind_I or
                   Code.Stind_R4 or
                   Code.Stind_R8 or
                   Code.Stind_Ref or
                   Code.Stobj;

            /// <summary>
            /// Translates a dnlib parameter into the scanner's argument index space, including the implicit instance argument.
            /// </summary>
            /// <param name="method">The declaring method.</param>
            /// <param name="parameter">The parameter metadata.</param>
            private static int GetArgumentIndex(MethodDef method, Parameter parameter)
                => parameter.MethodSigIndex + (method.IsStatic ? 0 : 1);

            /// <summary>
            /// Adds a method-target mapping to a multimap.
            /// </summary>
            /// <param name="map">The map to update.</param>
            /// <param name="key">The source method identifier.</param>
            /// <param name="value">The target method identifier.</param>
            private static void AddMapping(Dictionary<MethodId, HashSet<MethodId>> map, MethodId key, MethodId value)
            {
                if (!map.TryGetValue(key, out HashSet<MethodId>? values))
                {
                    values = [];
                    map[key] = values;
                }

                _ = values.Add(value);
            }
        }

    }
}
