/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Loading
{
    /// <summary>
    /// Controls how assembly resolution expands beyond the supplied input roots.
    /// </summary>
    public enum AssemblyResolutionMode
    {
        Restricted,
        InferenceNoFallback,
        InferenceWithFallback
    }
}
