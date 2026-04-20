/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Globalization;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace GadgetExplorer.Analysis.Index
{
    public sealed partial class AnalysisIndex
    {
        private sealed partial class Builder
        {
            /// <summary>
            /// Tries to recognize and push a compile-time constant produced directly by an instruction.
            /// </summary>
            /// <param name="method">The current method.</param>
            /// <param name="instruction">The instruction to inspect.</param>
            /// <param name="stack">The simulated evaluation stack.</param>
            /// <returns><see langword="true"/> when a constant value was pushed.</returns>
            private static bool TryPushRecognizedConstantValue(MethodDef method, Instruction instruction, List<ValueState> stack)
            {
                if (!TryCreateRecognizedConstantValue(method.Module, instruction, out ValueState constantValue))
                {
                    return false;
                }

                stack.Add(constantValue);
                return true;
            }

            /// <summary>
            /// Tries to create a compile-time constant from a single IL instruction.
            /// </summary>
            /// <param name="module">The current module.</param>
            /// <param name="instruction">The instruction to inspect.</param>
            /// <param name="constantValue">The created constant value.</param>
            /// <returns><see langword="true"/> when the instruction was recognized as a constant producer.</returns>
            private static bool TryCreateRecognizedConstantValue(ModuleDef module, Instruction instruction, out ValueState constantValue)
            {
                constantValue = default;

                if (instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string stringLiteral)
                {
                    constantValue = ValueState.CreateConstant(
                        module.CorLibTypes.String,
                        new ConstantValue(ConstantValueKind.StringLiteral, stringLiteral));
                    return true;
                }

                if (instruction.OpCode == OpCodes.Ldnull)
                {
                    constantValue = ValueState.CreateConstant(
                        null,
                        new ConstantValue(ConstantValueKind.Null, "null"));
                    return true;
                }

                if (TryGetPrimitiveConstantDisplayValue(instruction, out string primitiveDisplayValue))
                {
                    constantValue = ValueState.CreateConstant(
                        null,
                        new ConstantValue(ConstantValueKind.Primitive, primitiveDisplayValue));
                    return true;
                }

                if (instruction.OpCode == OpCodes.Ldtoken && instruction.Operand is IType type)
                {
                    constantValue = ValueState.CreateConstant(
                        null,
                        new ConstantValue(ConstantValueKind.RuntimeTypeHandle, GetTypeDisplayName(type)));
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Tries to recognize a primitive constant emitted by an IL literal instruction.
            /// </summary>
            /// <param name="instruction">The instruction to inspect.</param>
            /// <param name="displayValue">The formatted primitive display value.</param>
            /// <returns><see langword="true"/> when a primitive literal was recognized.</returns>
            private static bool TryGetPrimitiveConstantDisplayValue(Instruction instruction, out string displayValue)
            {
                displayValue = instruction.OpCode.Code switch
                {
                    Code.Ldc_I4_M1 => "-1",
                    Code.Ldc_I4_0 => "0",
                    Code.Ldc_I4_1 => "1",
                    Code.Ldc_I4_2 => "2",
                    Code.Ldc_I4_3 => "3",
                    Code.Ldc_I4_4 => "4",
                    Code.Ldc_I4_5 => "5",
                    Code.Ldc_I4_6 => "6",
                    Code.Ldc_I4_7 => "7",
                    Code.Ldc_I4_8 => "8",
                    Code.Ldc_I4 or Code.Ldc_I4_S => FormatPrimitiveLiteral(instruction.Operand),
                    Code.Ldc_I8 => FormatPrimitiveLiteral(instruction.Operand),
                    Code.Ldc_R4 => FormatPrimitiveLiteral(instruction.Operand),
                    Code.Ldc_R8 => FormatPrimitiveLiteral(instruction.Operand),
                    _ => string.Empty
                };

                return displayValue.Length > 0;
            }

            /// <summary>
            /// Formats a primitive literal operand using invariant culture.
            /// </summary>
            /// <param name="operand">The literal operand.</param>
            /// <returns>The formatted literal.</returns>
            private static string FormatPrimitiveLiteral(object? operand)
                => operand switch
                {
                    sbyte value => value.ToString(CultureInfo.InvariantCulture),
                    byte value => value.ToString(CultureInfo.InvariantCulture),
                    short value => value.ToString(CultureInfo.InvariantCulture),
                    ushort value => value.ToString(CultureInfo.InvariantCulture),
                    int value => value.ToString(CultureInfo.InvariantCulture),
                    uint value => value.ToString(CultureInfo.InvariantCulture),
                    long value => value.ToString(CultureInfo.InvariantCulture),
                    ulong value => value.ToString(CultureInfo.InvariantCulture),
                    float value => value.ToString(CultureInfo.InvariantCulture),
                    double value => value.ToString(CultureInfo.InvariantCulture),
                    _ => string.Empty
                };

            /// <summary>
            /// Tries to recognize a constant value produced by a constructor invocation.
            /// </summary>
            /// <param name="targetMethod">The constructor being invoked.</param>
            /// <param name="arguments">The constructor arguments.</param>
            /// <param name="constructedValue">The recognized constructed constant.</param>
            /// <returns><see langword="true"/> when the constructor result is recognized as constant-like.</returns>
            private static bool TryCreateRecognizedConstructedValue(
                ResolvedMethodReference targetMethod,
                IReadOnlyList<ValueState> arguments,
                out ValueState constructedValue)
            {
                constructedValue = default;

                string declaringTypeName = GetTypeDisplayName(targetMethod.MethodReference.DeclaringType);
                if (declaringTypeName == "System.Uri" &&
                    arguments is [{ ConstantValue: { Kind: ConstantValueKind.StringLiteral } uriString }])
                {
                    TypeSig? constructedTypeSig = targetMethod.MethodDefinition?.DeclaringType?.ToTypeSig()
                                                  ?? targetMethod.MethodReference.DeclaringType?.ToTypeSig();
                    constructedValue = ValueState.CreateConstant(
                        constructedTypeSig,
                        uriString with { Kind = ConstantValueKind.Uri });
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Tries to recognize a constant-like return value produced by a method call.
            /// </summary>
            /// <param name="targetMethod">The called method.</param>
            /// <param name="arguments">The explicit call arguments.</param>
            /// <param name="returnValue">The recognized return value.</param>
            /// <returns><see langword="true"/> when the return value is recognized.</returns>
            private static bool TryCreateRecognizedCallReturnValue(
                ResolvedMethodReference targetMethod,
                IReadOnlyList<ValueState> arguments,
                out ValueState returnValue)
            {
                returnValue = default;

                if (MatchesMethod(targetMethod.MethodReference, "System.Type", "GetTypeFromHandle", ["System.RuntimeTypeHandle"]) &&
                    arguments is [{ ConstantValue: { Kind: ConstantValueKind.RuntimeTypeHandle } typeHandle }])
                {
                    TypeSig? returnType = targetMethod.MethodReference.MethodSig?.RetType;
                    returnValue = ValueState.CreateConstant(
                        returnType,
                        typeHandle with { Kind = ConstantValueKind.Type });
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Summarizes the explicit arguments supplied to a method or constructor call.
            /// </summary>
            /// <param name="arguments">The explicit arguments.</param>
            /// <returns>The per-argument summaries.</returns>
            private static CallArgumentSummary[] SummarizeCallArguments(IReadOnlyList<ValueState> arguments)
                => arguments
                    .Select((argument, argumentIndex) => CreateCallArgumentSummary(argumentIndex, argument))
                    .ToArray();

            /// <summary>
            /// Creates the call-summary entry for a single explicit argument.
            /// </summary>
            /// <param name="argumentIndex">The explicit argument index.</param>
            /// <param name="argument">The tracked argument value.</param>
            /// <returns>The resulting summary.</returns>
            private static CallArgumentSummary CreateCallArgumentSummary(int argumentIndex, ValueState argument)
                => argument.ConstantValue is { } constantValue
                    ? CallArgumentSummary.CreateConstant(argumentIndex, constantValue)
                    : CallArgumentSummary.CreateNonConstant(argumentIndex);

            /// <summary>
            /// Determines whether a method reference matches the supplied declaring type, name, and exact parameter list.
            /// </summary>
            /// <param name="method">The method reference to inspect.</param>
            /// <param name="declaringTypeName">The expected declaring type full name.</param>
            /// <param name="methodName">The expected method name.</param>
            /// <param name="parameterTypeNames">The expected exact parameter type names.</param>
            /// <returns><see langword="true"/> when the reference matches the supplied signature.</returns>
            private static bool MatchesMethod(IMethod method, string declaringTypeName, string methodName, IReadOnlyList<string> parameterTypeNames)
                => string.Equals(GetTypeDisplayName(method.DeclaringType), declaringTypeName, StringComparison.Ordinal) &&
                   string.Equals(method.Name, methodName, StringComparison.Ordinal) &&
                   (method.MethodSig?.Params.Select(GetTypeDisplayName).ToArray() ?? []).SequenceEqual(parameterTypeNames, StringComparer.Ordinal);
        }
    }
}
