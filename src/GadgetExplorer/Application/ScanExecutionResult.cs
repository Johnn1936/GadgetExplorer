/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Application
{
    /// <summary>
    /// Captures the full result of one scan execution.
    /// </summary>
    public sealed record ScanExecutionResult(
        ScanCommandOptions Options,
        SerializerProfile SerializerProfile,
        IReadOnlyList<SinkDefinition> SinkDefinitions,
        IReadOnlyList<SinkDefinition> IgnoreSinkDefinitions,
        AssemblyLoadResult LoadResult,
        AnalysisIndex Index,
        ScanAnalysisReport Report);
}

