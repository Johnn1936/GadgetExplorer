/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    public sealed record SinkEvaluationResult(
        SinkDefinition SinkDefinition,
        IReadOnlyList<MethodId> SinkMethodIds,
        string SinkDisplayName,
        bool IsResolved,
        bool IsIgnored,
        string? ResolutionNote,
        IReadOnlyList<ClassFinding> Findings);
}

