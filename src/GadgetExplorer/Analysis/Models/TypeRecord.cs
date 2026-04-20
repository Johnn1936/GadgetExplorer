/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Models
{
    public sealed record TypeRecord(
        TypeId Id,
        ModuleDefMD Module,
        TypeDef TypeDef,
        string FullName,
        RootTypeVisibility RootVisibility,
        bool IsClass,
        bool IsInterface,
        bool IsValueType,
        bool IsAbstract,
        string AssemblyQualifiedName);
}

