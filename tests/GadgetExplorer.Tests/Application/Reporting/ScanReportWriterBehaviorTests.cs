/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Application.Reporting
{
    public sealed class ScanReportWriterBehaviorTests
    {
        [Fact]
        public void Write_includes_loader_diagnostics_in_recon()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            string sampleAssemblyPath = Path.GetFullPath(typeof(MySpecialObject).Assembly.Location);
            string copiedAssemblyPath = tempDirectory.GetPath(Path.GetFileName(sampleAssemblyPath));
            File.Copy(sampleAssemblyPath, copiedAssemblyPath, overwrite: true);
            tempDirectory.WriteFile("not-really-an-assembly.dll", "hello");
            tempDirectory.WriteFile(
                $"{Path.GetFileNameWithoutExtension(copiedAssemblyPath)}.runtimeconfig.json",
                "{ this is not valid json");

            AssemblyLoadResult loadResult = AssemblyInputLoader.LoadAssemblySet([tempDirectory.Path], assemblyResolutionMode: AssemblyResolutionMode.Restricted);
            AnalysisIndex index = AnalysisIndex.Build(loadResult.Modules);
            var options = new ScanCommandOptions(
                [tempDirectory.Path],
                null,
                null,
                FindingSortMode.ShortestPath,
                InterfaceExpansionMode.Strict,
                null,
                null,
                "JsonDotNet",
                null,
                AssemblyResolutionMode.Restricted);
            var execution = new ScanExecutionResult(
                options,
                SerializerProfiles.Resolve("JsonDotNet"),
                [],
                [],
                loadResult,
                index,
                new ScanAnalysisReport([]));
            using var writer = new StringWriter();

            ScanReportWriter.Write(writer, execution);

            string report = writer.ToString();
            Assert.Contains("Skipped candidate assemblies: 1", report, StringComparison.Ordinal);
            Assert.Contains("Invalid runtimeconfig files: 1", report, StringComparison.Ordinal);
            Assert.Contains("Runtimeconfig files without usable frameworks: 0", report, StringComparison.Ordinal);
            Assert.Contains("Runtimeconfig status: 1 runtimeconfig file(s) failed to parse", report, StringComparison.Ordinal);
            Assert.Contains("Warning: 1 candidate assembly file(s) could not be loaded.", report, StringComparison.Ordinal);
            Assert.Contains("Warning: Skipped candidate assembly '", report, StringComparison.Ordinal);
            Assert.Contains("Warning: Invalid runtimeconfig '", report, StringComparison.Ordinal);
        }

        [Fact]
        public void Write_uses_rendered_findings_to_count_potential_sink_entries()
        {
            string sampleAssemblyPath = Path.GetFullPath(typeof(MySpecialObject).Assembly.Location);
            AssemblyLoadResult loadResult = AssemblyInputLoader.LoadAssemblySet([sampleAssemblyPath], assemblyResolutionMode: AssemblyResolutionMode.Restricted);
            AnalysisIndex index = AnalysisIndex.Build(loadResult.Modules);
            SerializerProfile profile = SerializerProfiles.Resolve("JsonDotNet");
            ScanAnalysisReport report = SinkAnalyzer.Analyze(index, [new SinkDefinition("MySpecialObject", "SayHello")], [], profile);
            SinkEvaluationResult sinkReport = Assert.Single(report.SinkEvaluationResults);
            int expectedFindingCount = RenderedFindingProjector.Project(report, FindingSortMode.ShortestPath)
                .Count(finding => string.Equals(finding.SinkDisplayName, sinkReport.SinkDisplayName, StringComparison.Ordinal));
            var options = new ScanCommandOptions(
                [sampleAssemblyPath],
                null,
                null,
                FindingSortMode.ShortestPath,
                InterfaceExpansionMode.Strict,
                null,
                null,
                "JsonDotNet",
                null,
                AssemblyResolutionMode.Restricted);
            var execution = new ScanExecutionResult(
                options,
                profile,
                [new SinkDefinition("MySpecialObject", "SayHello")],
                [],
                loadResult,
                index,
                report);
            using var writer = new StringWriter();

            ScanReportWriter.Write(writer, execution);

            Assert.Contains($"  {sinkReport.SinkDisplayName}: {expectedFindingCount}", writer.ToString(), StringComparison.Ordinal);
        }
    }
}
