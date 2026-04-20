/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Models
{
    public sealed record MethodRecord(
        MethodId Id,
        TypeId DeclaringTypeId,
        string DisplayName,
        string Name,
        bool IsStatic,
        bool IsAbstract,
        MethodSpecialKind SpecialKind,
        bool IsPublic,
        bool HasBody,
        MethodDef? MethodDefinition,
        IMethod MethodReference,
        bool IsPInvoke,
        string? ImportedModuleName,
        string? ImportedEntryPointName)
    {
        public bool IsInstance => !IsStatic;

        public bool IsConstructor => SpecialKind == MethodSpecialKind.Constructor;

        public bool IsPropertyGetter => SpecialKind == MethodSpecialKind.PropertyGetter;

        public bool IsPropertySetter => SpecialKind == MethodSpecialKind.PropertySetter;

        public bool IsEventAdd => SpecialKind == MethodSpecialKind.EventAdd;

        public bool IsFinalizer
            => IsInstance &&
               Name == "Finalize" &&
               (MethodReference.MethodSig?.Params.Count ?? -1) == 0;
    }
}

