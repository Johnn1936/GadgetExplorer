/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace GadgetExplorer.Analysis.Index
{
    public sealed partial class AnalysisIndex
    {
        private sealed partial class Builder
        {
            private static readonly SigComparer s_typeSigComparer = new();

            private readonly record struct ControlFlowGraph(
                ControlFlowBlock[] Blocks,
                int[] BlockIdByInstructionIndex,
                IReadOnlyDictionary<Instruction, int> InstructionIndices);

            private readonly record struct ControlFlowBlock(
                int Id,
                int StartInstructionIndex,
                int EndInstructionIndexExclusive,
                IReadOnlyList<int> SuccessorBlockIds);

            private readonly record struct ControlFlowSuccessor(int BlockId, MethodFlowState State);

            private sealed class MethodFlowState(
                ValueState[] locals,
                ValueState[] arguments,
                Dictionary<string, ValueState> fields,
                List<ValueState>? stack = null)
            {
                public List<ValueState> Stack { get; } = stack ?? [];

                public ValueState[] Locals { get; } = locals;

                public ValueState[] Arguments { get; } = arguments;

                public Dictionary<string, ValueState> Fields { get; } = fields;

                public MethodFlowState Clone()
                {
                    var stackClone = new List<ValueState>(Stack.Count);
                    stackClone.AddRange(Stack);
                    return new MethodFlowState(
                        (ValueState[])Locals.Clone(),
                        (ValueState[])Arguments.Clone(),
                        Fields.Count == 0
                            ? new Dictionary<string, ValueState>(StringComparer.Ordinal)
                            : new Dictionary<string, ValueState>(Fields, StringComparer.Ordinal),
                        stackClone);
                }
            }

            private static ControlFlowGraph BuildControlFlowGraph(CilBody body)
            {
                int instructionCount = body.Instructions.Count;
                var instructionIndices = new Dictionary<Instruction, int>(instructionCount);
                for (int instructionIndex = 0; instructionIndex < instructionCount; instructionIndex++)
                {
                    instructionIndices[body.Instructions[instructionIndex]] = instructionIndex;
                }

                IReadOnlyList<int>[] successorInstructionIndicesByInstructionIndex = BuildSuccessorInstructionIndexMap(body, instructionIndices);

                var leaders = new bool[instructionCount];
                leaders[0] = true;

                foreach (ExceptionHandler handler in body.ExceptionHandlers)
                {
                    AddLeader(handler.TryStart, leaders, instructionIndices);
                    AddLeader(handler.TryEnd, leaders, instructionIndices);
                    AddLeader(handler.HandlerStart, leaders, instructionIndices);
                    AddLeader(handler.HandlerEnd, leaders, instructionIndices);
                    AddLeader(handler.FilterStart, leaders, instructionIndices);
                }

                for (int instructionIndex = 0; instructionIndex < instructionCount; instructionIndex++)
                {
                    int naturalFallthroughInstructionIndex = instructionIndex + 1;
                    foreach (int successorInstructionIndex in successorInstructionIndicesByInstructionIndex[instructionIndex])
                    {
                        if (successorInstructionIndex == naturalFallthroughInstructionIndex)
                        {
                            continue;
                        }

                        leaders[successorInstructionIndex] = true;
                    }

                    if (instructionIndex + 1 < instructionCount && StartsNewBlockAfterInstruction(body.Instructions[instructionIndex]))
                    {
                        leaders[instructionIndex + 1] = true;
                    }
                }

                int leaderCount = 0;
                for (int instructionIndex = 0; instructionIndex < leaders.Length; instructionIndex++)
                {
                    if (leaders[instructionIndex])
                    {
                        leaderCount++;
                    }
                }

                var orderedLeaders = new int[leaderCount];
                int orderedLeaderIndex = 0;
                for (int instructionIndex = 0; instructionIndex < leaders.Length; instructionIndex++)
                {
                    if (leaders[instructionIndex])
                    {
                        orderedLeaders[orderedLeaderIndex++] = instructionIndex;
                    }
                }

                var blockIdByInstructionIndex = new int[instructionCount];
                var blocks = new ControlFlowBlock[orderedLeaders.Length];

                for (int blockId = 0; blockId < orderedLeaders.Length; blockId++)
                {
                    int startInstructionIndex = orderedLeaders[blockId];
                    int endInstructionIndexExclusive = blockId + 1 < orderedLeaders.Length
                        ? orderedLeaders[blockId + 1]
                        : instructionCount;

                    for (int instructionIndex = startInstructionIndex; instructionIndex < endInstructionIndexExclusive; instructionIndex++)
                    {
                        blockIdByInstructionIndex[instructionIndex] = blockId;
                    }
                }

                for (int blockId = 0; blockId < orderedLeaders.Length; blockId++)
                {
                    int startInstructionIndex = orderedLeaders[blockId];
                    int endInstructionIndexExclusive = blockId + 1 < orderedLeaders.Length
                        ? orderedLeaders[blockId + 1]
                        : instructionCount;
                    IReadOnlyList<int> successorBlockIds = ResolveSuccessorBlockIds(
                        successorInstructionIndicesByInstructionIndex,
                        endInstructionIndexExclusive - 1,
                        blockIdByInstructionIndex);
                    blocks[blockId] = new ControlFlowBlock(blockId, startInstructionIndex, endInstructionIndexExclusive, successorBlockIds);
                }

                return new ControlFlowGraph(blocks, blockIdByInstructionIndex, instructionIndices);
            }

            private static void AddLeader(
                Instruction? instruction,
                bool[] leaders,
                Dictionary<Instruction, int> instructionIndices)
            {
                if (instruction is not null && instructionIndices.TryGetValue(instruction, out int instructionIndex))
                {
                    leaders[instructionIndex] = true;
                }
            }

            private static IReadOnlyList<int> ResolveSuccessorBlockIds(
                IReadOnlyList<int>[] successorInstructionIndicesByInstructionIndex,
                int instructionIndex,
                int[] blockIdByInstructionIndex)
            {
                IReadOnlyList<int> successorInstructionIndices = successorInstructionIndicesByInstructionIndex[instructionIndex];
                return successorInstructionIndices.Count switch
                {
                    0 => [],
                    1 => [blockIdByInstructionIndex[successorInstructionIndices[0]]],
                    _ => DeduplicateSuccessorBlockIds(successorInstructionIndices, blockIdByInstructionIndex)
                };
            }

            private static int[] DeduplicateSuccessorBlockIds(
                IReadOnlyList<int> successorInstructionIndices,
                int[] blockIdByInstructionIndex)
            {
                var successorBlockIds = new int[successorInstructionIndices.Count];
                int successorCount = 0;
                int lastBlockId = -1;
                for (int successorIndex = 0; successorIndex < successorInstructionIndices.Count; successorIndex++)
                {
                    int successorBlockId = blockIdByInstructionIndex[successorInstructionIndices[successorIndex]];
                    if (successorCount > 0 && successorBlockId == lastBlockId)
                    {
                        continue;
                    }

                    successorBlockIds[successorCount++] = successorBlockId;
                    lastBlockId = successorBlockId;
                }

                return successorCount == successorBlockIds.Length
                    ? successorBlockIds
                    : successorBlockIds[..successorCount];
            }

            private static bool StartsNewBlockAfterInstruction(Instruction instruction)
                => instruction.OpCode.FlowControl is FlowControl.Branch or FlowControl.Break or FlowControl.Cond_Branch or FlowControl.Return or FlowControl.Throw;

            private static IReadOnlyList<int>[] BuildSuccessorInstructionIndexMap(
                CilBody body,
                Dictionary<Instruction, int> instructionIndices)
            {
                Dictionary<int, IReadOnlyList<int>> endfinallySuccessorInstructionIndicesByInstructionIndex =
                    BuildEndfinallySuccessorInstructionIndexMap(body, instructionIndices);
                var successorInstructionIndicesByInstructionIndex = new IReadOnlyList<int>[body.Instructions.Count];
                for (int instructionIndex = 0; instructionIndex < body.Instructions.Count; instructionIndex++)
                {
                    successorInstructionIndicesByInstructionIndex[instructionIndex] = GetSuccessorInstructionIndices(
                        body,
                        instructionIndex,
                        instructionIndices,
                        endfinallySuccessorInstructionIndicesByInstructionIndex);
                }

                return successorInstructionIndicesByInstructionIndex;
            }

            private static Dictionary<int, IReadOnlyList<int>> BuildEndfinallySuccessorInstructionIndexMap(
                CilBody body,
                Dictionary<Instruction, int> instructionIndices)
            {
                ExceptionHandler[] finallyHandlers = [.. body.ExceptionHandlers.Where(handler => handler.HandlerType == ExceptionHandlerType.Finally)];
                if (finallyHandlers.Length == 0)
                {
                    return new Dictionary<int, IReadOnlyList<int>>();
                }

                Dictionary<ExceptionHandler, IReadOnlyList<int>> endfinallyInstructionIndicesByHandler = finallyHandlers
                    .ToDictionary(handler => handler, handler => GetEndfinallyInstructionIndices(body, handler, instructionIndices));
                var successorSetsByEndfinallyInstructionIndex = new Dictionary<int, SortedSet<int>>();

                for (int instructionIndex = 0; instructionIndex < body.Instructions.Count; instructionIndex++)
                {
                    Instruction instruction = body.Instructions[instructionIndex];
                    if ((instruction.OpCode.Code != Code.Leave && instruction.OpCode.Code != Code.Leave_S) ||
                        instruction.Operand is not Instruction leaveTarget ||
                        !instructionIndices.TryGetValue(leaveTarget, out int leaveTargetInstructionIndex))
                    {
                        continue;
                    }

                    ExceptionHandler[] exitedFinallyHandlers = GetExitedFinallyHandlers(body, instructionIndex, leaveTargetInstructionIndex, instructionIndices);
                    for (int handlerIndex = 0; handlerIndex < exitedFinallyHandlers.Length; handlerIndex++)
                    {
                        int continuationInstructionIndex = handlerIndex + 1 < exitedFinallyHandlers.Length && exitedFinallyHandlers[handlerIndex + 1].HandlerStart is not null
                            ? instructionIndices[exitedFinallyHandlers[handlerIndex + 1].HandlerStart!]
                            : leaveTargetInstructionIndex;

                        foreach (int endfinallyInstructionIndex in endfinallyInstructionIndicesByHandler[exitedFinallyHandlers[handlerIndex]])
                        {
                            if (!successorSetsByEndfinallyInstructionIndex.TryGetValue(endfinallyInstructionIndex, out SortedSet<int>? successorSet))
                            {
                                successorSet = [];
                                successorSetsByEndfinallyInstructionIndex[endfinallyInstructionIndex] = successorSet;
                            }

                            successorSet.Add(continuationInstructionIndex);
                        }
                    }
                }

                return successorSetsByEndfinallyInstructionIndex.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<int>)[.. pair.Value]);
            }

            private static IReadOnlyList<int> GetSuccessorInstructionIndices(
                CilBody body,
                int instructionIndex,
                Dictionary<Instruction, int> instructionIndices,
                Dictionary<int, IReadOnlyList<int>> endfinallySuccessorInstructionIndicesByInstructionIndex)
            {
                Instruction instruction = body.Instructions[instructionIndex];
                int? fallthroughInstructionIndex = instructionIndex + 1 < body.Instructions.Count ? instructionIndex + 1 : null;

                if ((instruction.OpCode.Code == Code.Leave || instruction.OpCode.Code == Code.Leave_S) &&
                    instruction.Operand is Instruction leaveTarget &&
                    instructionIndices.TryGetValue(leaveTarget, out int leaveTargetInstructionIndex))
                {
                    return GetLeaveSuccessorInstructionIndices(body, instructionIndex, leaveTargetInstructionIndex, instructionIndices);
                }

                if (instruction.OpCode == OpCodes.Endfinally &&
                    endfinallySuccessorInstructionIndicesByInstructionIndex.TryGetValue(instructionIndex, out IReadOnlyList<int>? endfinallySuccessorInstructionIndices))
                {
                    return endfinallySuccessorInstructionIndices;
                }

                if (instruction.OpCode.Code == Code.Switch && instruction.Operand is IList<Instruction> switchTargets)
                {
                    var successorInstructionIndices = new int[switchTargets.Count + (fallthroughInstructionIndex is null ? 0 : 1)];
                    int successorCount = 0;
                    for (int targetIndex = 0; targetIndex < switchTargets.Count; targetIndex++)
                    {
                        successorInstructionIndices[successorCount++] = instructionIndices[switchTargets[targetIndex]];
                    }

                    if (fallthroughInstructionIndex is { } fallthroughIndex)
                    {
                        successorInstructionIndices[successorCount++] = fallthroughIndex;
                    }

                    Array.Sort(successorInstructionIndices, 0, successorCount);
                    return DeduplicateInstructionIndices(successorInstructionIndices, successorCount);
                }

                if (instruction.Operand is Instruction branchTarget)
                {
                    int targetInstructionIndex = instructionIndices[branchTarget];
                    return instruction.OpCode.FlowControl == FlowControl.Cond_Branch && fallthroughInstructionIndex is { } fallthroughIndex
                        ? [targetInstructionIndex, fallthroughIndex]
                        : [targetInstructionIndex];
                }

                if (instruction.OpCode.FlowControl is FlowControl.Return or FlowControl.Throw)
                {
                    return [];
                }

                if (instruction.OpCode.Code is Code.Endfilter or Code.Rethrow)
                {
                    return [];
                }

                return fallthroughInstructionIndex is { } nextInstruction
                    ? [nextInstruction]
                    : [];
            }

            private static IReadOnlyList<int> GetLeaveSuccessorInstructionIndices(
                CilBody body,
                int leaveInstructionIndex,
                int leaveTargetInstructionIndex,
                Dictionary<Instruction, int> instructionIndices)
            {
                ExceptionHandler[] exitedFinallyHandlers = GetExitedFinallyHandlers(body, leaveInstructionIndex, leaveTargetInstructionIndex, instructionIndices);
                if (exitedFinallyHandlers.Length == 0 || exitedFinallyHandlers[0].HandlerStart is null)
                {
                    return [leaveTargetInstructionIndex];
                }

                return [instructionIndices[exitedFinallyHandlers[0].HandlerStart!]];
            }

            private static ExceptionHandler[] GetExitedFinallyHandlers(
                CilBody body,
                int originInstructionIndex,
                int targetInstructionIndex,
                Dictionary<Instruction, int> instructionIndices)
                => [.. body.ExceptionHandlers
                    .Where(handler =>
                        handler.HandlerType == ExceptionHandlerType.Finally &&
                        IsInstructionIndexWithinRegion(originInstructionIndex, handler.TryStart, handler.TryEnd, instructionIndices) &&
                        !IsInstructionIndexWithinRegion(targetInstructionIndex, handler.TryStart, handler.TryEnd, instructionIndices))
                    .OrderByDescending(handler => instructionIndices.TryGetValue(handler.TryStart!, out int startInstructionIndex) ? startInstructionIndex : -1)
                    .ThenBy(handler => instructionIndices.TryGetValue(handler.TryEnd!, out int endInstructionIndex) ? endInstructionIndex : int.MaxValue)];

            private static IReadOnlyList<int> GetEndfinallyInstructionIndices(
                CilBody body,
                ExceptionHandler handler,
                Dictionary<Instruction, int> instructionIndices)
            {
                if (handler.HandlerStart is null || !instructionIndices.TryGetValue(handler.HandlerStart, out int handlerStartInstructionIndex))
                {
                    return [];
                }

                int handlerEndInstructionIndex = handler.HandlerEnd is not null && instructionIndices.TryGetValue(handler.HandlerEnd, out int resolvedHandlerEndInstructionIndex)
                    ? resolvedHandlerEndInstructionIndex
                    : body.Instructions.Count;
                return [.. Enumerable.Range(handlerStartInstructionIndex, handlerEndInstructionIndex - handlerStartInstructionIndex)
                    .Where(index => body.Instructions[index].OpCode == OpCodes.Endfinally)];
            }

            private static int[] DeduplicateInstructionIndices(int[] instructionIndices, int count)
            {
                if (count == 0)
                {
                    return [];
                }

                int distinctCount = 1;
                for (int index = 1; index < count; index++)
                {
                    if (instructionIndices[index] != instructionIndices[distinctCount - 1])
                    {
                        instructionIndices[distinctCount++] = instructionIndices[index];
                    }
                }

                return distinctCount == instructionIndices.Length
                    ? instructionIndices
                    : instructionIndices[..distinctCount];
            }

            private static bool IsInstructionIndexWithinRegion(
                int instructionIndex,
                Instruction? start,
                Instruction? endExclusive,
                IReadOnlyDictionary<Instruction, int> instructionIndices)
            {
                if (start is null || !instructionIndices.TryGetValue(start, out int startInstructionIndex))
                {
                    return false;
                }

                int endInstructionIndex = endExclusive is not null && instructionIndices.TryGetValue(endExclusive, out int resolvedEndInstructionIndex)
                    ? resolvedEndInstructionIndex
                    : int.MaxValue;
                return instructionIndex >= startInstructionIndex && instructionIndex < endInstructionIndex;
            }

            private static MethodFlowState CreateInitialMethodFlowState(CilBody body, MethodDef method)
            {
                var locals = new ValueState[body.Variables.Count];
                for (int localIndex = 0; localIndex < body.Variables.Count; localIndex++)
                {
                    locals[localIndex] = ValueState.CreateUnknown(body.Variables[localIndex].Type, mayOriginateExternally: false)
                        .WithOrigin(ValueOriginKind.Local, localIndex, null);
                }

                return new MethodFlowState(
                    locals,
                    CreateInitialArgumentValues(method),
                    new Dictionary<string, ValueState>(StringComparer.Ordinal));
            }

            private MethodFlowState?[] ComputeBlockEntryStates(MethodRecord method, CilBody body, ControlFlowGraph graph)
            {
                var entryStates = new MethodFlowState?[graph.Blocks.Length];
                entryStates[0] = CreateInitialMethodFlowState(body, method.MethodDefinition!);

                var pendingBlocks = new Queue<int>();
                var queued = new bool[graph.Blocks.Length];
                EnqueueBlock(0, pendingBlocks, queued);

                while (pendingBlocks.Count > 0)
                {
                    int blockId = pendingBlocks.Dequeue();
                    queued[blockId] = false;

                    if (entryStates[blockId] is not { } entryState)
                    {
                        continue;
                    }

                    IReadOnlyList<ControlFlowSuccessor> successors = ExecuteBlock(method, body, graph, graph.Blocks[blockId], entryState, emitEffects: false);
                    foreach (ControlFlowSuccessor successor in successors)
                    {
                        if (MergeIntoEntryState(entryStates, successor, body, method.MethodDefinition!))
                        {
                            EnqueueBlock(successor.BlockId, pendingBlocks, queued);
                        }
                    }
                }

                return entryStates;
            }

            private static void EnqueueBlock(int blockId, Queue<int> pendingBlocks, bool[] queued)
            {
                if (queued[blockId])
                {
                    return;
                }

                pendingBlocks.Enqueue(blockId);
                queued[blockId] = true;
            }

            private void EmitControlFlowGraph(MethodRecord method, CilBody body, ControlFlowGraph graph, MethodFlowState?[] entryStates)
            {
                foreach (ControlFlowBlock block in graph.Blocks)
                {
                    if (entryStates[block.Id] is not { } entryState)
                    {
                        continue;
                    }

                    _ = ExecuteBlock(method, body, graph, block, entryState, emitEffects: true);
                }
            }

            private IReadOnlyList<ControlFlowSuccessor> ExecuteBlock(
                MethodRecord method,
                CilBody body,
                ControlFlowGraph graph,
                ControlFlowBlock block,
                MethodFlowState entryState,
                bool emitEffects,
                bool cloneEntryState = true)
            {
                MethodFlowState state = cloneEntryState ? entryState.Clone() : entryState;

                for (int instructionIndex = block.StartInstructionIndex; instructionIndex < block.EndInstructionIndexExclusive; instructionIndex++)
                {
                    Instruction instruction = body.Instructions[instructionIndex];
                    bool isTerminalInstruction = instructionIndex == block.EndInstructionIndexExclusive - 1;
                    if (!isTerminalInstruction)
                    {
                        ExecuteInstruction(method, body, instruction, state, emitEffects);
                        continue;
                    }

                    return TransferTerminalInstruction(method, body, graph, block, instructionIndex, instruction, state, emitEffects);
                }

                return [];
            }

            private IReadOnlyList<ControlFlowSuccessor> TransferTerminalInstruction(
                MethodRecord method,
                CilBody body,
                ControlFlowGraph graph,
                ControlFlowBlock block,
                int instructionIndex,
                Instruction instruction,
                MethodFlowState state,
                bool emitEffects)
            {
                switch (instruction.OpCode.Code)
                {
                    case Code.Brtrue:
                    case Code.Brtrue_S:
                    case Code.Brfalse:
                    case Code.Brfalse_S:
                        {
                            ValueState condition = PopOrUnknown(state.Stack);
                            if (!TryEvaluateUnaryBranch(instruction.OpCode.Code, condition, out bool takeBranch))
                            {
                                return CreateSuccessors(block.SuccessorBlockIds, state);
                            }

                            return CreateSuccessor(
                                ResolveConditionalSuccessorBlockId(graph, instructionIndex, instruction, takeBranch),
                                state);
                        }

                    case Code.Beq:
                    case Code.Beq_S:
                    case Code.Bne_Un:
                    case Code.Bne_Un_S:
                    case Code.Bge:
                    case Code.Bge_S:
                    case Code.Bge_Un:
                    case Code.Bge_Un_S:
                    case Code.Bgt:
                    case Code.Bgt_S:
                    case Code.Bgt_Un:
                    case Code.Bgt_Un_S:
                    case Code.Ble:
                    case Code.Ble_S:
                    case Code.Ble_Un:
                    case Code.Ble_Un_S:
                    case Code.Blt:
                    case Code.Blt_S:
                    case Code.Blt_Un:
                    case Code.Blt_Un_S:
                        {
                            ValueState right = PopOrUnknown(state.Stack);
                            ValueState left = PopOrUnknown(state.Stack);
                            if (!TryEvaluateBinaryBranch(instruction.OpCode.Code, left, right, out bool takeBranch))
                            {
                                return CreateSuccessors(block.SuccessorBlockIds, state);
                            }

                            return CreateSuccessor(
                                ResolveConditionalSuccessorBlockId(graph, instructionIndex, instruction, takeBranch),
                                state);
                        }

                    case Code.Switch:
                        {
                            ValueState switchValue = PopOrUnknown(state.Stack);
                            if (instruction.Operand is not IList<Instruction> switchTargets ||
                                !TryEvaluateSwitchBranch(graph, instructionIndex, switchValue, switchTargets, out int switchTargetBlockId))
                            {
                                return CreateSuccessors(block.SuccessorBlockIds, state);
                            }

                            return CreateSuccessor(switchTargetBlockId, state);
                        }

                    case Code.Br:
                    case Code.Br_S:
                    case Code.Leave:
                    case Code.Leave_S:
                        return CreateSuccessors(block.SuccessorBlockIds, state);

                    case Code.Ret:
                    case Code.Throw:
                    case Code.Rethrow:
                    case Code.Endfilter:
                        ApplyDefaultStackTransition(state.Stack, instruction.OpCode);
                        return [];
                }

                ExecuteInstruction(method, body, instruction, state, emitEffects);
                return CreateSuccessors(block.SuccessorBlockIds, state);
            }

            private void ExecuteInstruction(
                MethodRecord method,
                CilBody body,
                Instruction instruction,
                MethodFlowState state,
                bool emitEffects)
            {
                if (TryPushRecognizedConstantValue(method.MethodDefinition!, instruction, state.Stack))
                {
                    return;
                }

                if (instruction.OpCode == OpCodes.Ldftn || instruction.OpCode == OpCodes.Ldvirtftn)
                {
                    state.Stack.Add(TryResolveMethodReference(instruction.Operand as IMethod, out ResolvedMethodReference functionTarget)
                        ? ValueState.CreateMethodPointer(functionTarget.MethodId)
                        : ValueState.Unknown);
                    return;
                }

                if (TryGetLocalAddressIndex(instruction.OpCode.Code, instruction.Operand, out int localAddressIndex))
                {
                    state.Stack.Add(ValueState.CreateAddress(
                        body.Variables[localAddressIndex].Type,
                        AddressTargetKind.Local,
                        localAddressIndex,
                        null));
                    return;
                }

                if (TryGetLocalLoadIndex(instruction.OpCode.Code, instruction.Operand, out int localLoadIndex))
                {
                    state.Stack.Add(LoadLocalValue(localLoadIndex, state.Locals, body));
                    return;
                }

                if (TryGetArgumentAddressIndex(method.MethodDefinition!, instruction.OpCode.Code, instruction.Operand, out int argumentAddressIndex))
                {
                    state.Stack.Add(ValueState.CreateAddress(
                        GetArgumentType(method.MethodDefinition!, argumentAddressIndex),
                        AddressTargetKind.Argument,
                        argumentAddressIndex,
                        null,
                        mayOriginateExternally: true));
                    return;
                }

                if (TryGetArgumentLoadIndex(method.MethodDefinition!, instruction.OpCode.Code, instruction.Operand, out int argumentLoadIndex))
                {
                    state.Stack.Add(LoadArgumentValue(method.MethodDefinition!, argumentLoadIndex, state.Arguments));
                    return;
                }

                if (TryGetLocalStoreIndex(instruction.OpCode.Code, instruction.Operand, out int localStoreIndex))
                {
                    ValueState storedValue = PopOrUnknown(state.Stack);
                    state.Locals[localStoreIndex] = MergeTrackedSlotValue(
                            state.Locals[localStoreIndex],
                            storedValue,
                            body.Variables[localStoreIndex].Type,
                            storedValue.MayOriginateExternally)
                        .WithOrigin(ValueOriginKind.Local, localStoreIndex, storedValue.OriginFieldKey);
                    return;
                }

                if (TryGetArgumentStoreIndex(method.MethodDefinition!, instruction.OpCode.Code, instruction.Operand, out int argumentStoreIndex))
                {
                    ValueState storedValue = PopOrUnknown(state.Stack);
                    state.Arguments[argumentStoreIndex] = MergeTrackedSlotValue(
                            state.Arguments[argumentStoreIndex],
                            storedValue,
                            GetArgumentType(method.MethodDefinition!, argumentStoreIndex),
                            storedValue.MayOriginateExternally)
                        .WithOrigin(ValueOriginKind.Argument, argumentStoreIndex, storedValue.OriginFieldKey);
                    return;
                }

                if (instruction.OpCode == OpCodes.Dup)
                {
                    state.Stack.Add(state.Stack.Count == 0 ? ValueState.Unknown : state.Stack[^1]);
                    return;
                }

                if (instruction.OpCode == OpCodes.Pop)
                {
                    _ = PopOrUnknown(state.Stack);
                    return;
                }

                if (instruction.OpCode == OpCodes.Ldfld || instruction.OpCode == OpCodes.Ldsfld || instruction.OpCode == OpCodes.Ldflda || instruction.OpCode == OpCodes.Ldsflda)
                {
                    if (instruction.OpCode == OpCodes.Ldfld || instruction.OpCode == OpCodes.Ldflda)
                    {
                        _ = PopOrUnknown(state.Stack);
                    }

                    if (!TryResolveFieldReference(instruction.Operand as IField, out FieldDef field))
                    {
                        state.Stack.Add(ValueState.Unknown);
                        return;
                    }

                    state.Stack.Add(instruction.OpCode == OpCodes.Ldflda || instruction.OpCode == OpCodes.Ldsflda
                        ? ValueState.CreateAddress(field.FieldSig?.Type, AddressTargetKind.Field, null, GetFieldKey(field), mayOriginateExternally: true)
                        : LoadFieldValue(field, state.Fields));
                    return;
                }

                if (instruction.OpCode == OpCodes.Stfld || instruction.OpCode == OpCodes.Stsfld)
                {
                    ValueState value = PopOrUnknown(state.Stack);
                    if (instruction.OpCode == OpCodes.Stfld)
                    {
                        _ = PopOrUnknown(state.Stack);
                    }

                    if (TryResolveFieldReference(instruction.Operand as IField, out FieldDef field))
                    {
                        string fieldKey = GetFieldKey(field);
                        ValueState existingFieldValue = state.Fields.TryGetValue(fieldKey, out ValueState currentFieldValue)
                            ? currentFieldValue
                            : ValueState.CreateUnknown(field.FieldSig?.Type);
                        state.Fields[fieldKey] = MergeTrackedSlotValue(existingFieldValue, value, field.FieldSig?.Type, mayOriginateExternally: true)
                            .WithOrigin(ValueOriginKind.Field, null, fieldKey);
                    }

                    return;
                }

                if (IsLoadIndirectInstruction(instruction.OpCode.Code))
                {
                    ValueState addressValue = PopOrUnknown(state.Stack);
                    state.Stack.Add(ReadThroughTrackedAddress(
                        addressValue,
                        method.MethodDefinition!,
                        body,
                        state.Locals,
                        state.Arguments,
                        state.Fields,
                        ToTypeSig(instruction.Operand as IType) ?? addressValue.TypeSig));
                    return;
                }

                if (IsStoreIndirectInstruction(instruction.OpCode.Code))
                {
                    ValueState incomingValue = PopOrUnknown(state.Stack);
                    ValueState addressValue = PopOrUnknown(state.Stack);
                    WriteThroughTrackedAddress(
                        addressValue,
                        incomingValue,
                        ToTypeSig(instruction.Operand as IType) ?? addressValue.TypeSig,
                        method.MethodDefinition!,
                        body,
                        state.Locals,
                        state.Arguments,
                        state.Fields,
                        overwriteExisting: false);
                    return;
                }

                if (instruction.OpCode == OpCodes.Castclass || instruction.OpCode == OpCodes.Isinst || instruction.OpCode == OpCodes.Box || instruction.OpCode == OpCodes.Unbox_Any)
                {
                    PreserveTypeAdaptedValue(instruction, state.Stack);
                    return;
                }

                if (instruction.OpCode == OpCodes.Newobj)
                {
                    HandleNewObjectCall(method, body, instruction, state, state.Stack, emitEffects);
                    return;
                }

                if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                {
                    HandleMethodCall(method, body, instruction, state, state.Stack, emitEffects);
                    return;
                }

                ApplyDefaultStackTransition(state.Stack, instruction.OpCode);
            }

            private bool MergeIntoEntryState(
                MethodFlowState?[] entryStates,
                ControlFlowSuccessor successor,
                CilBody body,
                MethodDef method)
            {
                if (entryStates[successor.BlockId] is not { } existingState)
                {
                    entryStates[successor.BlockId] = successor.State;
                    return true;
                }

                if (AreEquivalent(existingState, successor.State))
                {
                    return false;
                }

                MethodFlowState mergedState = MergeMethodFlowStates(existingState, successor.State, body, method);
                if (AreEquivalent(existingState, mergedState))
                {
                    return false;
                }

                entryStates[successor.BlockId] = mergedState;
                return true;
            }

            private MethodFlowState MergeMethodFlowStates(
                MethodFlowState current,
                MethodFlowState incoming,
                CilBody body,
                MethodDef method)
            {
                var mergedLocals = new ValueState[current.Locals.Length];
                for (int localIndex = 0; localIndex < mergedLocals.Length; localIndex++)
                {
                    mergedLocals[localIndex] = JoinValueStates(current.Locals[localIndex], incoming.Locals[localIndex], body.Variables[localIndex].Type);
                }

                var mergedArguments = new ValueState[current.Arguments.Length];
                for (int argumentIndex = 0; argumentIndex < mergedArguments.Length; argumentIndex++)
                {
                    mergedArguments[argumentIndex] = JoinValueStates(current.Arguments[argumentIndex], incoming.Arguments[argumentIndex], GetArgumentType(method, argumentIndex));
                }

                Dictionary<string, ValueState> mergedFields = MergeFieldStates(current.Fields, incoming.Fields);

                int mergedStackCount = current.Stack.Count == incoming.Stack.Count
                    ? current.Stack.Count
                    : 0;
                var mergedStack = new List<ValueState>(mergedStackCount);
                for (int stackIndex = 0; stackIndex < mergedStackCount; stackIndex++)
                {
                    mergedStack.Add(JoinValueStates(current.Stack[stackIndex], incoming.Stack[stackIndex], slotType: null));
                }

                return new MethodFlowState(mergedLocals, mergedArguments, mergedFields, mergedStack);
            }

            private Dictionary<string, ValueState> MergeFieldStates(
                Dictionary<string, ValueState> currentFields,
                Dictionary<string, ValueState> incomingFields)
            {
                if (currentFields.Count == 0 && incomingFields.Count == 0)
                {
                    return new Dictionary<string, ValueState>(StringComparer.Ordinal);
                }

                var mergedFields = new Dictionary<string, ValueState>(currentFields.Count + incomingFields.Count, StringComparer.Ordinal);
                foreach ((string fieldKey, ValueState currentValue) in currentFields)
                {
                    ValueState incomingValue = incomingFields.TryGetValue(fieldKey, out ValueState resolvedIncomingValue)
                        ? resolvedIncomingValue
                        : ValueState.Unknown;
                    mergedFields[fieldKey] = JoinValueStates(currentValue, incomingValue, ResolveFieldType(fieldKey));
                }

                foreach ((string fieldKey, ValueState incomingValue) in incomingFields)
                {
                    if (mergedFields.ContainsKey(fieldKey))
                    {
                        continue;
                    }

                    mergedFields[fieldKey] = JoinValueStates(ValueState.Unknown, incomingValue, ResolveFieldType(fieldKey));
                }

                return mergedFields;
            }

            private TypeSig? ResolveFieldType(string fieldKey)
                => _fieldDefsByKey.TryGetValue(fieldKey, out FieldDef? field)
                    ? field.FieldSig?.Type
                    : null;

            private static IReadOnlyList<ControlFlowSuccessor> CreateSuccessor(int successorBlockId, MethodFlowState state)
                => [new ControlFlowSuccessor(successorBlockId, state)];

            private static IReadOnlyList<ControlFlowSuccessor> CreateSuccessors(IReadOnlyList<int> successorBlockIds, MethodFlowState state)
            {
                return successorBlockIds.Count switch
                {
                    0 => [],
                    1 => CreateSuccessor(successorBlockIds[0], state),
                    _ => CreateSplitSuccessors(successorBlockIds, state)
                };
            }

            private static ControlFlowSuccessor[] CreateSplitSuccessors(IReadOnlyList<int> successorBlockIds, MethodFlowState state)
            {
                var successors = new ControlFlowSuccessor[successorBlockIds.Count];
                successors[0] = new ControlFlowSuccessor(successorBlockIds[0], state);
                for (int successorIndex = 1; successorIndex < successorBlockIds.Count; successorIndex++)
                {
                    successors[successorIndex] = new ControlFlowSuccessor(successorBlockIds[successorIndex], state.Clone());
                }

                return successors;
            }

            private static int ResolveConditionalSuccessorBlockId(
                ControlFlowGraph graph,
                int instructionIndex,
                Instruction instruction,
                bool takeBranch)
            {
                int successorInstructionIndex = takeBranch
                    ? graph.InstructionIndices[(Instruction)instruction.Operand!]
                    : instructionIndex + 1;
                return graph.BlockIdByInstructionIndex[successorInstructionIndex];
            }

            private static bool TryEvaluateUnaryBranch(Code code, ValueState condition, out bool takeBranch)
            {
                takeBranch = false;
                if (!TryGetTruthiness(condition, out bool conditionIsTrue))
                {
                    return false;
                }

                takeBranch = code is Code.Brtrue or Code.Brtrue_S
                    ? conditionIsTrue
                    : !conditionIsTrue;
                return true;
            }

            private static bool TryEvaluateBinaryBranch(Code code, ValueState left, ValueState right, out bool takeBranch)
            {
                takeBranch = false;
                if (TryGetConstantEquality(left, right, out bool areEqual))
                {
                    takeBranch = code switch
                    {
                        Code.Beq or Code.Beq_S => areEqual,
                        Code.Bne_Un or Code.Bne_Un_S => !areEqual,
                        _ => takeBranch
                    };

                    if (code is Code.Beq or Code.Beq_S or Code.Bne_Un or Code.Bne_Un_S)
                    {
                        return true;
                    }
                }

                if (!TryGetNumericComparison(left, right, out int comparison))
                {
                    return false;
                }

                takeBranch = code switch
                {
                    Code.Bge or Code.Bge_S or Code.Bge_Un or Code.Bge_Un_S => comparison >= 0,
                    Code.Bgt or Code.Bgt_S or Code.Bgt_Un or Code.Bgt_Un_S => comparison > 0,
                    Code.Ble or Code.Ble_S or Code.Ble_Un or Code.Ble_Un_S => comparison <= 0,
                    Code.Blt or Code.Blt_S or Code.Blt_Un or Code.Blt_Un_S => comparison < 0,
                    _ => false
                };
                return true;
            }

            private static bool TryEvaluateSwitchBranch(
                ControlFlowGraph graph,
                int instructionIndex,
                ValueState switchValue,
                IList<Instruction> switchTargets,
                out int targetBlockId)
            {
                targetBlockId = default;
                if (switchValue.ConstantValue is not { Kind: ConstantValueKind.Primitive } constantValue ||
                    !int.TryParse(constantValue.DisplayValue, out int caseIndex))
                {
                    return false;
                }

                int targetInstructionIndex = caseIndex >= 0 && caseIndex < switchTargets.Count
                    ? graph.InstructionIndices[switchTargets[caseIndex]]
                    : instructionIndex + 1;
                targetBlockId = graph.BlockIdByInstructionIndex[targetInstructionIndex];
                return true;
            }

            private static bool TryGetTruthiness(ValueState value, out bool isTrue)
            {
                isTrue = false;
                if (value.ConstantValue is not { } constantValue)
                {
                    return false;
                }

                switch (constantValue.Kind)
                {
                    case ConstantValueKind.Null:
                        isTrue = false;
                        return true;
                    case ConstantValueKind.Primitive:
                        if (double.TryParse(constantValue.DisplayValue, out double numericValue))
                        {
                            isTrue = numericValue != 0;
                            return true;
                        }

                        return false;
                    default:
                        isTrue = true;
                        return true;
                }
            }

            private static bool TryGetConstantEquality(ValueState left, ValueState right, out bool areEqual)
            {
                areEqual = false;
                if (left.ConstantValue is not { } leftConstant || right.ConstantValue is not { } rightConstant)
                {
                    return false;
                }

                if (leftConstant.Kind == ConstantValueKind.Null || rightConstant.Kind == ConstantValueKind.Null)
                {
                    areEqual = leftConstant.Kind == rightConstant.Kind;
                    return true;
                }

                if (leftConstant.Kind == ConstantValueKind.Primitive &&
                    rightConstant.Kind == ConstantValueKind.Primitive &&
                    double.TryParse(leftConstant.DisplayValue, out double leftNumeric) &&
                    double.TryParse(rightConstant.DisplayValue, out double rightNumeric))
                {
                    areEqual = leftNumeric.Equals(rightNumeric);
                    return true;
                }

                areEqual = leftConstant.Kind == rightConstant.Kind &&
                           string.Equals(leftConstant.DisplayValue, rightConstant.DisplayValue, StringComparison.Ordinal);
                return true;
            }

            private static bool TryGetNumericComparison(ValueState left, ValueState right, out int comparison)
            {
                comparison = default;
                if (left.ConstantValue is not { Kind: ConstantValueKind.Primitive } leftConstant ||
                    right.ConstantValue is not { Kind: ConstantValueKind.Primitive } rightConstant ||
                    !double.TryParse(leftConstant.DisplayValue, out double leftNumeric) ||
                    !double.TryParse(rightConstant.DisplayValue, out double rightNumeric))
                {
                    return false;
                }

                comparison = leftNumeric.CompareTo(rightNumeric);
                return true;
            }

            private static ValueState JoinValueStates(ValueState current, ValueState incoming, TypeSig? slotType)
            {
                TypeSig? mergedTypeSig = ResolveMergedTypeSig(current.TypeSig, incoming.TypeSig, slotType);
                ValueOriginKind originKind = current.OriginKind == incoming.OriginKind ? current.OriginKind : ValueOriginKind.None;
                int? originIndex = current.OriginIndex == incoming.OriginIndex ? current.OriginIndex : null;
                string? originFieldKey = string.Equals(current.OriginFieldKey, incoming.OriginFieldKey, StringComparison.Ordinal)
                    ? current.OriginFieldKey
                    : null;
                AddressTargetKind addressTargetKind = current.AddressTargetKind == incoming.AddressTargetKind &&
                                                      current.AddressTargetIndex == incoming.AddressTargetIndex &&
                                                      string.Equals(current.AddressTargetFieldKey, incoming.AddressTargetFieldKey, StringComparison.Ordinal)
                    ? current.AddressTargetKind
                    : AddressTargetKind.None;
                int? addressTargetIndex = addressTargetKind == AddressTargetKind.None ? null : current.AddressTargetIndex;
                string? addressTargetFieldKey = addressTargetKind == AddressTargetKind.None ? null : current.AddressTargetFieldKey;

                return new ValueState(
                    mergedTypeSig,
                    current.MethodPointerTarget == incoming.MethodPointerTarget ? current.MethodPointerTarget : null,
                    MergeMethodIds(current.DelegateTargets, incoming.DelegateTargets),
                    MergeTypeIds(current.PossibleConcreteTypeIds, incoming.PossibleConcreteTypeIds),
                    IntersectTypeIds(current.ReceiverTypeConstraintIds, incoming.ReceiverTypeConstraintIds),
                    Equals(current.ConstantValue, incoming.ConstantValue) ? current.ConstantValue : null,
                    current.MayOriginateExternally || incoming.MayOriginateExternally,
                    originKind,
                    originIndex,
                    originFieldKey,
                    addressTargetKind,
                    addressTargetIndex,
                    addressTargetFieldKey);
            }

            private static TypeSig? ResolveMergedTypeSig(TypeSig? currentTypeSig, TypeSig? incomingTypeSig, TypeSig? slotType)
            {
                if (AreEquivalent(currentTypeSig, incomingTypeSig))
                {
                    return currentTypeSig ?? incomingTypeSig ?? slotType;
                }

                if (slotType is not null)
                {
                    return slotType;
                }

                return currentTypeSig ?? incomingTypeSig;
            }

            private static IReadOnlyList<MethodId> MergeMethodIds(IReadOnlyList<MethodId> current, IReadOnlyList<MethodId> incoming)
            {
                if (current.Count == 0)
                {
                    return incoming;
                }

                if (incoming.Count == 0 || AreEquivalent(current, incoming))
                {
                    return current;
                }

                var merged = new MethodId[current.Count + incoming.Count];
                int currentIndex = 0;
                int incomingIndex = 0;
                int mergedCount = 0;
                while (currentIndex < current.Count && incomingIndex < incoming.Count)
                {
                    MethodId currentValue = current[currentIndex];
                    MethodId incomingValue = incoming[incomingIndex];
                    if (currentValue.Value == incomingValue.Value)
                    {
                        merged[mergedCount++] = currentValue;
                        currentIndex++;
                        incomingIndex++;
                    }
                    else if (currentValue.Value < incomingValue.Value)
                    {
                        merged[mergedCount++] = currentValue;
                        currentIndex++;
                    }
                    else
                    {
                        merged[mergedCount++] = incomingValue;
                        incomingIndex++;
                    }
                }

                while (currentIndex < current.Count)
                {
                    merged[mergedCount++] = current[currentIndex++];
                }

                while (incomingIndex < incoming.Count)
                {
                    merged[mergedCount++] = incoming[incomingIndex++];
                }

                return TrimArray(merged, mergedCount);
            }

            private static IReadOnlyList<TypeId> MergeTypeIds(IReadOnlyList<TypeId> current, IReadOnlyList<TypeId> incoming)
            {
                if (current.Count == 0)
                {
                    return incoming;
                }

                if (incoming.Count == 0 || AreEquivalent(current, incoming))
                {
                    return current;
                }

                var merged = new TypeId[current.Count + incoming.Count];
                int currentIndex = 0;
                int incomingIndex = 0;
                int mergedCount = 0;
                while (currentIndex < current.Count && incomingIndex < incoming.Count)
                {
                    TypeId currentValue = current[currentIndex];
                    TypeId incomingValue = incoming[incomingIndex];
                    if (currentValue.Value == incomingValue.Value)
                    {
                        merged[mergedCount++] = currentValue;
                        currentIndex++;
                        incomingIndex++;
                    }
                    else if (currentValue.Value < incomingValue.Value)
                    {
                        merged[mergedCount++] = currentValue;
                        currentIndex++;
                    }
                    else
                    {
                        merged[mergedCount++] = incomingValue;
                        incomingIndex++;
                    }
                }

                while (currentIndex < current.Count)
                {
                    merged[mergedCount++] = current[currentIndex++];
                }

                while (incomingIndex < incoming.Count)
                {
                    merged[mergedCount++] = incoming[incomingIndex++];
                }

                return TrimArray(merged, mergedCount);
            }

            private static IReadOnlyList<TypeId> IntersectTypeIds(IReadOnlyList<TypeId> current, IReadOnlyList<TypeId> incoming)
            {
                if (current.Count == 0 || incoming.Count == 0)
                {
                    return [];
                }

                if (AreEquivalent(current, incoming))
                {
                    return current;
                }

                var intersection = new TypeId[Math.Min(current.Count, incoming.Count)];
                int currentIndex = 0;
                int incomingIndex = 0;
                int intersectionCount = 0;
                while (currentIndex < current.Count && incomingIndex < incoming.Count)
                {
                    TypeId currentValue = current[currentIndex];
                    TypeId incomingValue = incoming[incomingIndex];
                    if (currentValue.Value == incomingValue.Value)
                    {
                        intersection[intersectionCount++] = currentValue;
                        currentIndex++;
                        incomingIndex++;
                    }
                    else if (currentValue.Value < incomingValue.Value)
                    {
                        currentIndex++;
                    }
                    else
                    {
                        incomingIndex++;
                    }
                }

                return TrimArray(intersection, intersectionCount);
            }

            private static MethodId[] TrimArray(MethodId[] values, int count)
                => count switch
                {
                    0 => [],
                    _ when count == values.Length => values,
                    _ => values[..count]
                };

            private static TypeId[] TrimArray(TypeId[] values, int count)
                => count switch
                {
                    0 => [],
                    _ when count == values.Length => values,
                    _ => values[..count]
                };

            private static bool AreEquivalent(MethodFlowState current, MethodFlowState incoming)
            {
                if (current.Stack.Count != incoming.Stack.Count ||
                    current.Locals.Length != incoming.Locals.Length ||
                    current.Arguments.Length != incoming.Arguments.Length ||
                    current.Fields.Count != incoming.Fields.Count)
                {
                    return false;
                }

                for (int index = 0; index < current.Stack.Count; index++)
                {
                    if (!AreEquivalent(current.Stack[index], incoming.Stack[index]))
                    {
                        return false;
                    }
                }

                for (int index = 0; index < current.Locals.Length; index++)
                {
                    if (!AreEquivalent(current.Locals[index], incoming.Locals[index]))
                    {
                        return false;
                    }
                }

                for (int index = 0; index < current.Arguments.Length; index++)
                {
                    if (!AreEquivalent(current.Arguments[index], incoming.Arguments[index]))
                    {
                        return false;
                    }
                }

                foreach ((string fieldKey, ValueState currentValue) in current.Fields)
                {
                    if (!incoming.Fields.TryGetValue(fieldKey, out ValueState incomingValue) ||
                        !AreEquivalent(currentValue, incomingValue))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool AreEquivalent(ValueState current, ValueState incoming)
                => AreEquivalent(current.TypeSig, incoming.TypeSig) &&
                   current.MethodPointerTarget == incoming.MethodPointerTarget &&
                   current.MayOriginateExternally == incoming.MayOriginateExternally &&
                   current.OriginKind == incoming.OriginKind &&
                   current.OriginIndex == incoming.OriginIndex &&
                   string.Equals(current.OriginFieldKey, incoming.OriginFieldKey, StringComparison.Ordinal) &&
                   Equals(current.ConstantValue, incoming.ConstantValue) &&
                   AreEquivalent(current.DelegateTargets, incoming.DelegateTargets) &&
                   AreEquivalent(current.PossibleConcreteTypeIds, incoming.PossibleConcreteTypeIds) &&
                   AreEquivalent(current.ReceiverTypeConstraintIds, incoming.ReceiverTypeConstraintIds);

            private static bool AreEquivalent(TypeSig? current, TypeSig? incoming)
                => ReferenceEquals(current, incoming) ||
                   (current is null
                       ? incoming is null
                       : incoming is not null && s_typeSigComparer.Equals(current, incoming));

            private static bool AreEquivalent(IReadOnlyList<MethodId> current, IReadOnlyList<MethodId> incoming)
            {
                if (ReferenceEquals(current, incoming))
                {
                    return true;
                }

                if (current.Count != incoming.Count)
                {
                    return false;
                }

                for (int index = 0; index < current.Count; index++)
                {
                    if (current[index] != incoming[index])
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool AreEquivalent(IReadOnlyList<TypeId> current, IReadOnlyList<TypeId> incoming)
            {
                if (ReferenceEquals(current, incoming))
                {
                    return true;
                }

                if (current.Count != incoming.Count)
                {
                    return false;
                }

                for (int index = 0; index < current.Count; index++)
                {
                    if (current[index] != incoming[index])
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
