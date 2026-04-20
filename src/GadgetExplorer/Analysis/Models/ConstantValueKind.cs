/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    /// <summary>
    /// Classifies the kinds of compile-time constants that can be recognized at a call site.
    /// </summary>
    public enum ConstantValueKind
    {
        Null,
        StringLiteral,
        Primitive,
        Type,
        Uri,
        RuntimeTypeHandle
    }
}
