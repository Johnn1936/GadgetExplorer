/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Application
{
    /// <summary>
    /// Executes the end-to-end scan pipeline.
    /// </summary>
    public static class ScanRunner
    {
        /// <summary>
        /// Runs the full analysis pipeline for the supplied options.
        /// </summary>
        /// <param name="options">The scan options.</param>
        /// <param name="progress">The progress callback.</param>
        public static ScanExecutionResult Execute(ScanCommandOptions options, Action<string> progress)
        {
            SerializerProfile serializerProfile = options.ProfileFilePath is not null
                ? SerializerProfiles.LoadFromPath(options.ProfileFilePath)
                : SerializerProfiles.ResolveShipped(options.ProfileName!);
            progress($"Loading sink configuration from {options.ResolvedIncludeSinkConfigPath}.");
            IReadOnlyList<SinkDefinition> sinkDefinitions = SinkConfigurations.Load(SinkConfigurationKind.Include, options.IncludeSinkConfigPath);
            progress($"Loading ignore-sink configuration from {options.ResolvedIgnoreSinkConfigPath}.");
            IReadOnlyList<SinkDefinition> ignoreSinkDefinitions = SinkConfigurations.Load(SinkConfigurationKind.Ignore, options.IgnoreSinkConfigPath);
            progress($"Loading assemblies from {options.AssemblyInputs.Count} input(s) using assembly resolution mode '{options.AssemblyResolutionModeDisplayText}'.");
            AssemblyLoadResult loadResult = AssemblyInputLoader.LoadAssemblySet(options.AssemblyInputs, progress, options.AssemblyResolutionMode);
            progress($"Building analysis index over {loadResult.Modules.Count} loaded assemblies.");
            var index = AnalysisIndex.Build(loadResult.Modules, options.InterfaceExpansionMode, progress);
            progress($"Running sink analysis across {sinkDefinitions.Count} configured sink pattern(s) using serializer profile '{serializerProfile.Name}'.");
            ScanAnalysisReport report = SinkAnalyzer.Analyze(index, sinkDefinitions, ignoreSinkDefinitions, serializerProfile, options.MaxPathLength, progress);
            progress("Scan completed successfully.");

            return new ScanExecutionResult(
                options,
                serializerProfile,
                sinkDefinitions,
                ignoreSinkDefinitions,
                loadResult,
                index,
                report);
        }
    }
}

