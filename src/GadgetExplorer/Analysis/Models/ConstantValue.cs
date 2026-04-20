/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    /// <summary>
    /// Represents a recognized compile-time constant value.
    /// </summary>
    /// <param name="Kind">The constant value kind.</param>
    /// <param name="DisplayValue">The display form of the constant.</param>
    public sealed record ConstantValue(
        ConstantValueKind Kind,
        string DisplayValue);
}
