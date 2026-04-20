/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    public sealed record EdgeRecord(
        EdgeId Id,
        MethodId SourceId,
        MethodId TargetId,
        EdgeKind Kind,
        IReadOnlyList<CallArgumentSummary>? ArgumentSummaries = null,
        TypeId? ReceiverTypeConstraintId = null,
        bool PreservesCallerInstanceReceiver = false)
    {
        public IReadOnlyList<CallArgumentSummary> ArgumentSummaries { get; } = ArgumentSummaries ?? [];
    }
}

