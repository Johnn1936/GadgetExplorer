/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Text.Json;
using Xunit;

namespace GadgetExplorer.Tests.Application.Reporting
{
    public sealed class ScanJsonReportWriterBehaviorTests
    {
        [Fact]
        public void Write_emits_valid_json_with_structured_recon_and_diagnostics()
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
                AssemblyResolutionMode.Restricted,
                ScanOutputFormat.Json);
            var execution = new ScanExecutionResult(
                options,
                SerializerProfiles.Resolve("JsonDotNet"),
                [],
                [],
                loadResult,
                index,
                new ScanAnalysisReport([]));
            using var writer = new StringWriter();

            ScanJsonReportWriter.Write(writer, execution);

            using JsonDocument json = JsonDocument.Parse(writer.ToString());
            JsonElement root = json.RootElement;
            JsonElement recon = root.GetProperty("recon");
            JsonElement configuration = recon.GetProperty("configuration");
            JsonElement loadCounts = recon.GetProperty("loadCounts");
            JsonElement diagnostics = recon.GetProperty("diagnostics");

            Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("json", configuration.GetProperty("outputFormat").GetString());
            Assert.Equal("restricted", configuration.GetProperty("assemblyResolutionMode").GetString());
            Assert.False(configuration.TryGetProperty("restrictResolutionToSpecifiedAssemblies", out _));
            Assert.Equal(JsonValueKind.Null, configuration.GetProperty("outputPath").ValueKind);
            Assert.Equal("shipped", configuration.GetProperty("serializerProfile").GetProperty("source").GetString());
            Assert.Equal("JsonDotNet", configuration.GetProperty("serializerProfile").GetProperty("name").GetString());
            Assert.Equal(JsonValueKind.Null, configuration.GetProperty("serializerProfile").GetProperty("filePath").ValueKind);
            Assert.Equal(1, loadCounts.GetProperty("skippedCandidateAssemblies").GetInt32());
            Assert.Equal(1, loadCounts.GetProperty("invalidRuntimeConfigFiles").GetInt32());
            Assert.Equal(0, loadCounts.GetProperty("runtimeConfigFilesWithoutUsableFrameworkRequests").GetInt32());
            Assert.Equal(1, diagnostics.GetProperty("candidateAssemblyLoadFailures").GetArrayLength());
            Assert.Equal(loadResult.Diagnostics.UnresolvedReferences.Count, diagnostics.GetProperty("unresolvedAssemblyReferences").GetArrayLength());
            Assert.Equal(1, diagnostics.GetProperty("invalidRuntimeConfigFiles").GetArrayLength());
            Assert.Equal(0, diagnostics.GetProperty("runtimeConfigFilesWithoutUsableFrameworkRequests").GetArrayLength());
            Assert.Equal(0, root.GetProperty("findings").GetArrayLength());
        }

        [Fact]
        public void Write_emits_findings_in_rendered_order_and_serializes_null_optionals()
        {
            string sampleAssemblyPath = Path.GetFullPath(typeof(MySpecialObject).Assembly.Location);
            AssemblyLoadResult loadResult = AssemblyInputLoader.LoadAssemblySet([sampleAssemblyPath], assemblyResolutionMode: AssemblyResolutionMode.Restricted);
            AnalysisIndex index = AnalysisIndex.Build(loadResult.Modules);
            SerializerProfile profile = SerializerProfiles.Resolve("JsonDotNet");
            ScanAnalysisReport report = SinkAnalyzer.Analyze(index, [new SinkDefinition("MySpecialObject", "SayHello")], [], profile);
            IReadOnlyList<RenderedFindingProjector.RenderedFinding> expectedFindings = RenderedFindingProjector.Project(report, FindingSortMode.ShortestPath);
            var options = new ScanCommandOptions(
                [sampleAssemblyPath],
                null,
                null,
                FindingSortMode.ShortestPath,
                InterfaceExpansionMode.Strict,
                null,
                "scan.json",
                "JsonDotNet",
                null,
                AssemblyResolutionMode.Restricted,
                ScanOutputFormat.Json);
            var execution = new ScanExecutionResult(
                options,
                profile,
                [new SinkDefinition("MySpecialObject", "SayHello")],
                [],
                loadResult,
                index,
                report);
            using var writer = new StringWriter();

            ScanJsonReportWriter.Write(writer, execution);

            using JsonDocument json = JsonDocument.Parse(writer.ToString());
            JsonElement root = json.RootElement;
            JsonElement configuration = root.GetProperty("recon").GetProperty("configuration");
            JsonElement findings = root.GetProperty("findings");

            Assert.NotEmpty(expectedFindings);
            Assert.Equal(expectedFindings.Count, findings.GetArrayLength());
            Assert.Equal(Path.GetFullPath("scan.json"), configuration.GetProperty("outputPath").GetString());

            for (int indexValue = 0; indexValue < expectedFindings.Count; indexValue++)
            {
                RenderedFindingProjector.RenderedFinding expected = expectedFindings[indexValue];
                JsonElement finding = findings[indexValue];
                JsonElement path = finding.GetProperty("path");

                Assert.Equal(indexValue, finding.GetProperty("sortIndex").GetInt32());
                Assert.Equal(expected.SinkDisplayName, finding.GetProperty("sinkDisplayName").GetString());
                Assert.Equal(expected.RootClassFullName, finding.GetProperty("rootType").GetProperty("fullName").GetString());
                Assert.Equal(expected.RootClassAssemblyQualifiedName, finding.GetProperty("rootType").GetProperty("assemblyQualifiedName").GetString());
                Assert.Equal(expected.Trigger.TriggerMethodDisplay, finding.GetProperty("trigger").GetProperty("methodDisplay").GetString());
                Assert.Equal(expected.Trigger.ReachabilityPath.Count, finding.GetProperty("pathLength").GetInt32());
                Assert.Equal(expected.Trigger.ReachabilityPath.Count, path.GetArrayLength());
            }

            int nullDeclaredOnIndex = expectedFindings
                .Select((finding, indexValue) => new { finding, indexValue })
                .First(entry => entry.finding.Trigger.TriggerDeclaredOnTypeName is null)
                .indexValue;
            int nullAnnotationIndex = expectedFindings
                .Select((finding, indexValue) => new { finding, indexValue })
                .First(entry => entry.finding.Trigger.TriggerAnnotation is null)
                .indexValue;

            JsonElement nullDeclaredOnFinding = findings.EnumerateArray().ElementAt(nullDeclaredOnIndex);
            JsonElement nullAnnotationFinding = findings.EnumerateArray().ElementAt(nullAnnotationIndex);

            Assert.Equal(JsonValueKind.Null, nullDeclaredOnFinding.GetProperty("trigger").GetProperty("declaredOnTypeName").ValueKind);
            Assert.Equal(JsonValueKind.Null, nullAnnotationFinding.GetProperty("trigger").GetProperty("annotation").ValueKind);
        }
    }
}
