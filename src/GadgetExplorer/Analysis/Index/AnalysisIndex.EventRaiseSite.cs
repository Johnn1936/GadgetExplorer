/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Index
{
    public sealed partial class AnalysisIndex
    {
        private readonly record struct EventRaiseSite(MethodId SourceMethodId, TypeId OwnerTypeId, string HandlerTypeName);

    }
}
