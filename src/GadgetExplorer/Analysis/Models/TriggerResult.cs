/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    public sealed record TriggerResult(
        MethodId TriggerMethodId,
        string TriggerMethodDisplay,
        TriggerKind TriggerKind,
        IReadOnlyList<EdgeRecord> ReachabilityPath,
        string? TriggerDeclaredOnTypeName,
        string? TriggerAnnotation);
}

