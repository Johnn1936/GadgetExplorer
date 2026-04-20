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
        /// <summary>
        /// Represents a stable lookup key for a loaded type definition.
        /// </summary>
        /// <param name="Module">The declaring module.</param>
        /// <param name="MetadataToken">The metadata token.</param>
        internal readonly record struct TypeLookupKey(ModuleDef Module, int MetadataToken);
    }
}
