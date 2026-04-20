/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Runtime.InteropServices;
using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    public sealed class NativeSinkResolutionBehaviorTests
    {
        [Fact]
        public void Native_module_and_entry_point_filters_resolve_direct_dll_imports()
        {
            var index = BuildCurrentTestAssemblyIndex();

            var report = SinkAnalyzer.Analyze(
                index,
                [new SinkDefinition(string.Empty, string.Empty, nativeModule: "kernel32.dll", nativeEntryPoint: "LoadLibraryEx*")],
                [],
                SerializerProfiles.ResolveShipped("JsonDotNet"));

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.True(sinkReport.IsResolved);
            Assert.Contains(
                sinkReport.SinkMethodIds.Select(index.GetMethod),
                method => string.Equals(method.Name, nameof(NativeImportTestTargets.BringInLibrary), StringComparison.Ordinal) &&
                          method.IsPInvoke &&
                          string.Equals(method.ImportedModuleName, "kernel32", StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(method.ImportedEntryPointName, "LoadLibraryExW", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Native_entry_point_matching_does_not_depend_on_the_managed_wrapper_name()
        {
            var index = BuildCurrentTestAssemblyIndex();

            var report = SinkAnalyzer.Analyze(
                index,
                [new SinkDefinition("NativeImportTestTargets", "DefinitelyNotTheImportName", nativeModule: "kernel32", nativeEntryPoint: "GetProcAddress")],
                [],
                SerializerProfiles.ResolveShipped("JsonDotNet"));

            var sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.False(sinkReport.IsResolved);

            report = SinkAnalyzer.Analyze(
                index,
                [new SinkDefinition(string.Empty, string.Empty, nativeModule: "kernel32", nativeEntryPoint: "GetProcAddress")],
                [],
                SerializerProfiles.ResolveShipped("JsonDotNet"));

            sinkReport = Assert.Single(report.SinkEvaluationResults);
            Assert.True(sinkReport.IsResolved);
            Assert.Contains(
                sinkReport.SinkMethodIds.Select(index.GetMethod),
                method => string.Equals(method.Name, nameof(NativeImportTestTargets.LookupNativeExport), StringComparison.Ordinal));
        }

        private static AnalysisIndex BuildCurrentTestAssemblyIndex()
        {
            var assemblyPath = Path.GetFullPath(typeof(NativeImportTestTargets).Assembly.Location);
            var loadResult = AssemblyInputLoader.LoadAssemblySet([assemblyPath], assemblyResolutionMode: AssemblyResolutionMode.Restricted);
            return AnalysisIndex.Build(loadResult.Modules);
        }
    }

    internal static class NativeImportTestTargets
    {
        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal static extern nint BringInLibrary(string libraryFileName, nint fileHandle, uint flags);

        [DllImport("kernel32", EntryPoint = "GetProcAddress", ExactSpelling = true, SetLastError = true)]
        internal static extern nint LookupNativeExport(nint moduleHandle, string exportName);
    }

}
