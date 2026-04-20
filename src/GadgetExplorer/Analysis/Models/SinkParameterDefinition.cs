/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    /// <summary>
    /// Describes a single sink parameter, including whether the sink should be ignored when that argument is provably constant.
    /// </summary>
    public sealed record SinkParameterDefinition(
        string TypeName,
        bool IgnoreSinkIfConstant = false);
}
