/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Index
{
    public sealed partial class AnalysisIndex
    {
        private readonly record struct ValueState(
            TypeSig? TypeSig,
            MethodId? MethodPointerTarget,
            IReadOnlyList<MethodId> DelegateTargets,
            IReadOnlyList<TypeId> PossibleConcreteTypeIds,
            IReadOnlyList<TypeId> ReceiverTypeConstraintIds,
            ConstantValue? ConstantValue,
            bool MayOriginateExternally,
            ValueOriginKind OriginKind,
            int? OriginIndex,
            string? OriginFieldKey,
            AddressTargetKind AddressTargetKind,
            int? AddressTargetIndex,
            string? AddressTargetFieldKey)
        {
            /// <summary>
            /// Gets an unknown value placeholder.
            /// </summary>
            public static ValueState Unknown => new(null, null, [], [], [], null, true, ValueOriginKind.None, null, null, AddressTargetKind.None, null, null);
            /// <summary>
            /// Creates an unknown value with a known type signature.
            /// </summary>
            /// <param name="typeSig">The associated type signature.</param>
            /// <param name="possibleConcreteTypeIds">The tracked concrete runtime type candidates.</param>
            /// <param name="mayOriginateExternally">Whether the value may originate from outside the current method.</param>
            public static ValueState CreateUnknown(TypeSig? typeSig, IEnumerable<TypeId>? possibleConcreteTypeIds = null, bool mayOriginateExternally = true)
                => new(typeSig, null, [], NormalizeTypeIds(possibleConcreteTypeIds), [], null, mayOriginateExternally, ValueOriginKind.None, null, null, AddressTargetKind.None, null, null);
            /// <summary>
            /// Creates a value that represents a method pointer.
            /// </summary>
            /// <param name="methodId">The method identifier.</param>
            public static ValueState CreateMethodPointer(MethodId methodId) => new(null, methodId, [], [], [], null, false, ValueOriginKind.None, null, null, AddressTargetKind.None, null, null);
            /// <summary>
            /// Creates a delegate value with tracked targets and origin.
            /// </summary>
            /// <param name="delegateType">The delegate type.</param>
            /// <param name="targets">The delegate targets.</param>
            /// <param name="originKind">The origin kind.</param>
            /// <param name="originIndex">The origin index.</param>
            /// <param name="originFieldKey">The origin field key.</param>
            /// <param name="mayOriginateExternally">Whether the value may originate from outside the current method.</param>
            public static ValueState CreateDelegate(TypeSig delegateType, IEnumerable<MethodId> targets, ValueOriginKind originKind = ValueOriginKind.None, int? originIndex = null, string? originFieldKey = null, bool mayOriginateExternally = true)
                => new(delegateType, null, [.. targets.Distinct().OrderBy(methodId => methodId.Value)], [], [], null, mayOriginateExternally, originKind, originIndex, originFieldKey, AddressTargetKind.None, null, null);
            /// <summary>
            /// Creates a value that represents a newly constructed object instance.
            /// </summary>
            /// <param name="typeSig">The constructed type signature.</param>
            /// <param name="typeId">The constructed type identifier.</param>
            public static ValueState CreateConstructedObject(TypeSig typeSig, TypeId typeId)
                => new(typeSig, null, [], [typeId], [typeId], null, false, ValueOriginKind.None, null, null, AddressTargetKind.None, null, null);
            /// <summary>
            /// Creates a value that represents a recognized compile-time constant.
            /// </summary>
            /// <param name="typeSig">The value type signature.</param>
            /// <param name="constantValue">The recognized constant value.</param>
            public static ValueState CreateConstant(TypeSig? typeSig, ConstantValue constantValue)
                => new(typeSig, null, [], [], [], constantValue, false, ValueOriginKind.None, null, null, AddressTargetKind.None, null, null);
            /// <summary>
            /// Creates a value that represents the managed address of a tracked slot.
            /// </summary>
            /// <param name="slotType">The addressed slot type.</param>
            /// <param name="targetKind">The addressed slot kind.</param>
            /// <param name="targetIndex">The addressed slot index.</param>
            /// <param name="targetFieldKey">The addressed field key.</param>
            /// <param name="mayOriginateExternally">Whether values read through the address may originate externally.</param>
            public static ValueState CreateAddress(TypeSig? slotType, AddressTargetKind targetKind, int? targetIndex, string? targetFieldKey, bool mayOriginateExternally = false)
                => new(slotType, null, [], [], [], null, mayOriginateExternally, ValueOriginKind.None, null, null, targetKind, targetIndex, targetFieldKey);

            /// <summary>
            /// Creates a copy of the value with updated origin metadata.
            /// </summary>
            /// <param name="originKind">The origin kind.</param>
            /// <param name="originIndex">The origin index.</param>
            /// <param name="originFieldKey">The origin field key.</param>
            public ValueState WithOrigin(ValueOriginKind originKind, int? originIndex, string? originFieldKey)
                => this with { OriginKind = originKind, OriginIndex = originIndex, OriginFieldKey = originFieldKey };

            /// <summary>
            /// Creates a copy of the value with updated type information.
            /// </summary>
            /// <param name="typeSig">The updated type signature.</param>
            /// <param name="possibleConcreteTypeIds">The updated concrete runtime type candidates.</param>
            /// <param name="mayOriginateExternally">Whether the value may originate from outside the current method.</param>
            public ValueState WithTypeInfo(TypeSig? typeSig, IEnumerable<TypeId>? possibleConcreteTypeIds, bool mayOriginateExternally)
                => this with { TypeSig = typeSig, PossibleConcreteTypeIds = NormalizeTypeIds(possibleConcreteTypeIds), MayOriginateExternally = mayOriginateExternally };

            /// <summary>
            /// Creates a copy of the value with updated type constraints.
            /// </summary>
            /// <param name="receiverTypeConstraintIds">The type constraints to preserve.</param>
            public ValueState WithTypeConstraints(IEnumerable<TypeId>? receiverTypeConstraintIds)
                => this with { ReceiverTypeConstraintIds = NormalizeTypeIds(receiverTypeConstraintIds) };

            /// <summary>
            /// Normalizes concrete runtime type candidates into a distinct, ordered list.
            /// </summary>
            /// <param name="typeIds">The type candidates to normalize.</param>
            private static TypeId[] NormalizeTypeIds(IEnumerable<TypeId>? typeIds)
                => typeIds is null
                    ? []
                    : typeIds
                        .Distinct()
                        .OrderBy(typeId => typeId.Value)
                        .ToArray();
        }

    }
}
