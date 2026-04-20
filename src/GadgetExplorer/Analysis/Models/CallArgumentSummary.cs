/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    /// <summary>
    /// Summarizes whether a specific call argument was recognized as a compile-time constant.
    /// </summary>
    /// <param name="ArgumentIndex">The zero-based explicit parameter index.</param>
    /// <param name="IsProvablyConstant">Whether the argument is provably constant.</param>
    /// <param name="ConstantKind">The recognized constant kind, if any.</param>
    /// <param name="DisplayValue">The display form of the recognized constant, if any.</param>
    public sealed record CallArgumentSummary(
        int ArgumentIndex,
        bool IsProvablyConstant,
        ConstantValueKind? ConstantKind,
        string? DisplayValue)
    {
        /// <summary>
        /// Creates a constant argument summary.
        /// </summary>
        /// <param name="argumentIndex">The argument index.</param>
        /// <param name="constantValue">The recognized constant value.</param>
        public static CallArgumentSummary CreateConstant(int argumentIndex, ConstantValue constantValue)
            => new(argumentIndex, true, constantValue.Kind, constantValue.DisplayValue);

        /// <summary>
        /// Creates a non-constant argument summary.
        /// </summary>
        /// <param name="argumentIndex">The argument index.</param>
        public static CallArgumentSummary CreateNonConstant(int argumentIndex)
            => new(argumentIndex, false, null, null);
    }
}
