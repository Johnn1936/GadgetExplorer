/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Globalization;

namespace GadgetExplorer.Application.Reporting
{
    /// <summary>
    /// Renders scan execution results into the plain-text report format.
    /// </summary>
    public static class ScanReportWriter
    {
        /// <summary>
        /// Writes the final report text to a supplied writer.
        /// </summary>
        /// <param name="writer">The destination writer.</param>
        /// <param name="result">The scan execution result.</param>
        public static void Write(TextWriter writer, ScanExecutionResult result)
        {
            ArgumentNullException.ThrowIfNull(writer);

            ScanReportDocument document = ScanReportDocumentProjector.Project(result);
            Write(writer, document);
        }

        private static void Write(TextWriter writer, ScanReportDocument document)
        {
            WriteReconSummary(writer, document);
            WritePotentialSinkSummary(writer, document);
            WriteResolvedSinkEvaluationResults(writer, document);
        }

        private static void WriteReconSummary(TextWriter writer, ScanReportDocument document)
        {
            writer.WriteLine("Recon");
            writer.WriteLine($"  Command line args: {ScanReportValueFormatter.FormatCommandLineArgumentsLabel(document.Recon.CommandLineArguments)}");
            writer.WriteLine($"  Inputs: {string.Join(", ", document.Recon.Inputs)}");
            writer.WriteLine($"  Sink config: {document.Recon.Configuration.IncludeSinkConfigPath}");
            writer.WriteLine($"  Ignore sink config: {document.Recon.Configuration.IgnoreSinkConfigPath}");
            writer.WriteLine(document.Recon.Configuration.SerializerProfile.Source == "file"
                ? $"  Serializer profile file: {document.Recon.Configuration.SerializerProfile.FilePath}"
                : $"  Serializer profile: {document.Recon.Configuration.SerializerProfile.Name}");

            writer.WriteLine($"  Finding sort: {document.Recon.Configuration.FindingSort}");
            writer.WriteLine($"  Dispatch mode: {document.Recon.Configuration.DispatchMode}");
            writer.WriteLine($"  Max path length: {document.Recon.Configuration.MaxPathLength?.ToString(CultureInfo.InvariantCulture) ?? "<unbounded>"}");
            writer.WriteLine($"  Output file: {document.Recon.Configuration.OutputPath ?? "<stdout>"}");
            writer.WriteLine($"  Configured sinks: {document.Recon.SinkCounts.Configured}");
            writer.WriteLine($"  Ignore sink patterns: {document.Recon.SinkCounts.IgnoredPatterns}");
            writer.WriteLine($"  Resolved sinks: {document.Recon.SinkCounts.Resolved}");
            writer.WriteLine($"  Ignored sinks: {document.Recon.SinkCounts.Ignored}");
            writer.WriteLine($"  Unresolved sinks: {document.Recon.SinkCounts.Unresolved}");
            writer.WriteLine($"  Input roots: {document.Recon.LoadCounts.InputRoots}");
            writer.WriteLine($"  Candidate assembly files discovered: {document.Recon.LoadCounts.CandidateAssemblyFiles}");
            writer.WriteLine($"  Loaded assemblies: {document.Recon.LoadCounts.LoadedAssemblies}");
            writer.WriteLine($"  Loaded candidate assemblies: {document.Recon.LoadCounts.LoadedCandidateAssemblies}");
            writer.WriteLine($"  Loaded input-root dependencies: {document.Recon.LoadCounts.LoadedInputRootDependencies}");
            writer.WriteLine($"  Loaded assemblies from input roots: {document.Recon.LoadCounts.LoadedAssembliesFromInputRoots}");
            writer.WriteLine($"  Skipped candidate assemblies: {document.Recon.LoadCounts.SkippedCandidateAssemblies}");
            writer.WriteLine($"  Unresolved references: {document.Recon.LoadCounts.UnresolvedReferences}");
            writer.WriteLine($"  Inferred installed runtime assemblies: {document.Recon.LoadCounts.InferredInstalledRuntimeAssemblies}");
            writer.WriteLine($"  Host-runtime fallback assemblies: {document.Recon.LoadCounts.HostRuntimeFallbackAssemblies}");
            writer.WriteLine($"  Global assembly cache assemblies: {document.Recon.LoadCounts.GlobalAssemblyCacheAssemblies}");
            writer.WriteLine($"  Runtimeconfig files: {document.Recon.LoadCounts.RuntimeConfigFiles}");
            writer.WriteLine($"  Invalid runtimeconfig files: {document.Recon.LoadCounts.InvalidRuntimeConfigFiles}");
            writer.WriteLine($"  Runtimeconfig files without usable frameworks: {document.Recon.LoadCounts.RuntimeConfigFilesWithoutUsableFrameworkRequests}");
            writer.WriteLine($"  Runtimeconfig status: {FormatRuntimeConfigStatus(document)}");
            writer.WriteLine($"  Requested frameworks: {FormatRequestedFrameworks(document.Recon.Runtime.RequestedFrameworks)}");
            writer.WriteLine($"  Installed runtime inference: {FormatInstalledRuntimeInference(document)}");
            writer.WriteLine($"  Assembly resolution mode: {document.Recon.Configuration.AssemblyResolutionMode}");
            writer.WriteLine($"  Types processed: {document.Recon.IndexCounts.TypesProcessed}");
            writer.WriteLine($"  Classes: {document.Recon.IndexCounts.Classes.Total} ({document.Recon.IndexCounts.Classes.Concrete} concrete, {document.Recon.IndexCounts.Classes.Abstract} abstract)");
            writer.WriteLine($"  Interfaces: {document.Recon.IndexCounts.Interfaces}");
            writer.WriteLine($"  Value types: {document.Recon.IndexCounts.ValueTypes}");
            writer.WriteLine($"  Methods indexed: {document.Recon.IndexCounts.MethodsIndexed}");
            writer.WriteLine($"  Properties indexed: {document.Recon.IndexCounts.PropertiesIndexed}");
            writer.WriteLine($"  Public instance property setters: {document.Recon.IndexCounts.PublicInstancePropertySetters}");
            writer.WriteLine($"  Events indexed: {document.Recon.IndexCounts.EventsIndexed}");
            writer.WriteLine($"  Graph edges: {document.Recon.IndexCounts.GraphEdges}");
            writer.WriteLine($"  Override relationships: {document.Recon.IndexCounts.OverrideRelationships}");
            writer.WriteLine($"  Interface implementations: {document.Recon.IndexCounts.InterfaceImplementations}");
            writer.WriteLine($"  Instantiated concrete types: {document.Recon.IndexCounts.InstantiatedConcreteTypes}");

            foreach (string warning in EnumerateLoadWarnings(document))
            {
                writer.WriteLine($"  Warning: {warning}");
            }

            writer.WriteLine();
        }

        private static void WritePotentialSinkSummary(TextWriter writer, ScanReportDocument document)
        {
            var sinkSummaries = document.Findings
                .GroupBy(finding => finding.SinkDisplayName)
                .Select(group => new
                {
                    SinkDisplayName = group.Key,
                    FindingCount = group.Count()
                })
                .OrderBy(summary => summary.SinkDisplayName, StringComparer.Ordinal)
                .ToArray();

            writer.WriteLine("Potential Sinks");

            if (sinkSummaries.Length == 0)
            {
                writer.WriteLine("  <none>");
                writer.WriteLine();
                return;
            }

            foreach (var sinkSummary in sinkSummaries)
            {
                writer.WriteLine($"  {sinkSummary.SinkDisplayName}: {sinkSummary.FindingCount}");
            }

            writer.WriteLine();
        }

        private static void WriteResolvedSinkEvaluationResults(TextWriter writer, ScanReportDocument document)
        {
            foreach (ScanFindingDocument finding in document.Findings)
            {
                WriteRenderedFinding(writer, finding);
            }
        }

        private static void WriteRenderedFinding(TextWriter writer, ScanFindingDocument finding)
        {
            writer.WriteLine(finding.RootType.AssemblyQualifiedName);
            writer.WriteLine($"  Assembly: {finding.RootAssembly.Path ?? "<unknown>"} (AssemblyVersion={finding.RootAssembly.AssemblyVersion ?? "<unknown>"}, FileVersion={finding.RootAssembly.FileVersion ?? "<unknown>"}, Origin={finding.RootAssembly.Origin})");
            if (finding.Trigger.DeclaredOnTypeName is not null)
            {
                writer.WriteLine($"  Declared On: {finding.Trigger.DeclaredOnTypeName}");
            }

            if (finding.Trigger.Annotation is not null)
            {
                writer.WriteLine($"  Note: {finding.Trigger.Annotation}");
            }

            writer.WriteLine($"    {finding.Trigger.MethodDisplay}");

            foreach (ScanPathStepDocument step in finding.Path)
            {
                writer.WriteLine($"      -> [{ScanReportValueFormatter.FormatTextEdgeKindLabel(step.Kind)}] {step.TargetMethodDisplay}");
            }

            writer.WriteLine();
        }

        private static string FormatRequestedFrameworks(IReadOnlyList<ScanRuntimeFrameworkDocument> frameworks)
            => frameworks.Count == 0
                ? "<none>"
                : string.Join(", ", frameworks.Select(framework => $"{framework.Name} {framework.Version}"));

        private static string FormatInstalledRuntimeInference(ScanReportDocument document)
            => GetAssemblyResolutionMode(document) == AssemblyResolutionMode.Restricted
                ? "<disabled by assembly resolution mode 'restricted'>"
                : document.Recon.Runtime.InferredInstalledRuntimeDirectories.Count == 0
                    ? "<none>"
                    : string.Join(", ", document.Recon.Runtime.InferredInstalledRuntimeDirectories);

        private static string FormatRuntimeConfigStatus(ScanReportDocument document)
        {
            AssemblyResolutionMode assemblyResolutionMode = GetAssemblyResolutionMode(document);

            if (document.Recon.LoadCounts.RuntimeConfigFiles == 0)
            {
                return assemblyResolutionMode switch
                {
                    AssemblyResolutionMode.Restricted => "no runtimeconfig files found; installed runtime inference disabled by restricted mode",
                    AssemblyResolutionMode.InferenceWithFallback => "no runtimeconfig files found; host-runtime fallback enabled",
                    _ => "no runtimeconfig files found; analysis stays within input roots"
                };
            }

            if (document.Recon.Runtime.RequestedFrameworks.Count > 0)
            {
                if (assemblyResolutionMode == AssemblyResolutionMode.Restricted)
                {
                    return $"parsed {document.Recon.Runtime.RequestedFrameworks.Count} usable framework request(s); installed runtime inference disabled by restricted mode";
                }

                return document.Recon.Runtime.InferredInstalledRuntimeDirectories.Count > 0
                    ? $"parsed {document.Recon.Runtime.RequestedFrameworks.Count} usable framework request(s) and inferred {document.Recon.Runtime.InferredInstalledRuntimeDirectories.Count} matching installed runtime director{(document.Recon.Runtime.InferredInstalledRuntimeDirectories.Count == 1 ? "y" : "ies")}"
                    : assemblyResolutionMode == AssemblyResolutionMode.InferenceWithFallback
                        ? $"parsed {document.Recon.Runtime.RequestedFrameworks.Count} usable framework request(s), but no matching installed runtime was inferred; host-runtime fallback enabled"
                        : $"parsed {document.Recon.Runtime.RequestedFrameworks.Count} usable framework request(s), but no matching installed runtime was inferred";
            }

            string status;
            if (document.Recon.LoadCounts.InvalidRuntimeConfigFiles > 0 &&
                document.Recon.LoadCounts.RuntimeConfigFilesWithoutUsableFrameworkRequests > 0)
            {
                status = $"{document.Recon.LoadCounts.InvalidRuntimeConfigFiles} failed to parse and {document.Recon.LoadCounts.RuntimeConfigFilesWithoutUsableFrameworkRequests} parsed without usable framework requests";
            }
            else if (document.Recon.LoadCounts.InvalidRuntimeConfigFiles > 0)
            {
                status = $"{document.Recon.LoadCounts.InvalidRuntimeConfigFiles} runtimeconfig file(s) failed to parse";
            }
            else
            {
                status = $"{document.Recon.LoadCounts.RuntimeConfigFilesWithoutUsableFrameworkRequests} runtimeconfig file(s) parsed without usable framework requests";
            }

            return assemblyResolutionMode switch
            {
                AssemblyResolutionMode.Restricted => $"{status}; installed runtime inference disabled by restricted mode",
                AssemblyResolutionMode.InferenceWithFallback => $"{status}; host-runtime fallback enabled",
                _ => $"{status}; analysis stays within input roots"
            };
        }

        private static AssemblyResolutionMode GetAssemblyResolutionMode(ScanReportDocument document)
            => ScanOptionValues.TryParseAssemblyResolutionMode(document.Recon.Configuration.AssemblyResolutionMode, out AssemblyResolutionMode assemblyResolutionMode)
                ? assemblyResolutionMode
                : AssemblyResolutionMode.Restricted;

        private static IEnumerable<string> EnumerateLoadWarnings(ScanReportDocument document)
        {
            foreach (string warning in document.Recon.Runtime.Warnings)
            {
                yield return warning;
            }

            if (document.Recon.Diagnostics.CandidateAssemblyLoadFailures.Count > 0)
            {
                yield return $"{document.Recon.Diagnostics.CandidateAssemblyLoadFailures.Count} candidate assembly file(s) could not be loaded.";
            }

            if (document.Recon.Diagnostics.UnresolvedAssemblyReferences.Count > 0)
            {
                yield return $"{document.Recon.Diagnostics.UnresolvedAssemblyReferences.Count} assembly reference(s) could not be resolved while loading reachable modules.";
            }

            foreach (ScanCandidateAssemblyLoadFailureDocument failure in document.Recon.Diagnostics.CandidateAssemblyLoadFailures)
            {
                yield return $"Skipped candidate assembly '{failure.Path}': {failure.Message}";
            }

            foreach (ScanRuntimeConfigDiagnosticDocument diagnostic in document.Recon.Diagnostics.InvalidRuntimeConfigFiles)
            {
                yield return $"Invalid runtimeconfig '{diagnostic.Path}': {diagnostic.Message}";
            }

            foreach (ScanRuntimeConfigDiagnosticDocument diagnostic in document.Recon.Diagnostics.RuntimeConfigFilesWithoutUsableFrameworkRequests)
            {
                yield return $"Runtimeconfig '{diagnostic.Path}' produced no usable framework requests.";
            }
        }
    }
}
