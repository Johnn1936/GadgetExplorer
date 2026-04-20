/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis
{
    /// <summary>
    /// Controls how dynamic dispatch is modeled during graph construction.
    /// </summary>
    public enum InterfaceExpansionMode
    {
        Off,
        Strict,
        Broad
    }
}

