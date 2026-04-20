/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    public sealed record ClassFinding(
        TypeId RootClassId,
        string RootClassFullName,
        string RootClassAssemblyQualifiedName,
        IReadOnlyList<TriggerResult> TriggerResults);
}

