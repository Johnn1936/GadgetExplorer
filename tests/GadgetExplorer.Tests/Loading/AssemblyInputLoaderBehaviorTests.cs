/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;
using Xunit;

namespace GadgetExplorer.Tests.Loading
{
    public sealed class AssemblyInputLoaderBehaviorTests : IDisposable
    {
        private const string TestFrameworkName = "Microsoft.NETCore.App";
        private const string TestFrameworkVersion = "9.1.2";
        private readonly List<string> _temporaryDirectories = [];

        [Fact]
        public void File_input_discovers_sibling_runtimeconfig()
        {
            var scanDirectory = CreateTempDirectory();
            var sampleAssemblyPath = typeof(MySpecialObject).Assembly.Location;
            var copiedAssemblyPath = Path.Combine(scanDirectory, Path.GetFileName(sampleAssemblyPath));
            File.Copy(sampleAssemblyPath, copiedAssemblyPath, overwrite: true);
            var runtimeConfigPath = Path.ChangeExtension(copiedAssemblyPath, ".runtimeconfig.json");
            File.WriteAllText(
                runtimeConfigPath,
                """
              {
                "runtimeOptions": {
                  "tfm": "net9.0",
                  "framework": {
                    "name": "Microsoft.NETCore.App",
                    "version": "9.1.2"
                  }
                }
              }
              """);

            var loadResult = AssemblyInputLoader.LoadAssemblySet([copiedAssemblyPath]);

            Assert.Contains(Path.GetFullPath(runtimeConfigPath), loadResult.LoadPlan.RuntimeConfigPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(Path.GetDirectoryName(copiedAssemblyPath)!, loadResult.LoadPlan.InputRoots, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void Directory_without_candidate_assemblies_throws()
        {
            var scanDirectory = CreateTempDirectory();
            File.WriteAllText(Path.Combine(scanDirectory, "readme.txt"), "nothing to see here");

            var ex = Assert.Throws<InvalidOperationException>(() => AssemblyInputLoader.LoadAssemblySet([scanDirectory]));

            Assert.Equal("No candidate assembly files were found in the provided inputs.", ex.Message);
        }

        [Fact]
        public void Invalid_candidate_assembly_is_reported_without_failing_the_load()
        {
            var scanDirectory = CreateTempDirectory();
            var sampleAssemblyPath = typeof(MySpecialObject).Assembly.Location;
            File.Copy(sampleAssemblyPath, Path.Combine(scanDirectory, Path.GetFileName(sampleAssemblyPath)), overwrite: true);
            var invalidAssemblyPath = Path.Combine(scanDirectory, "not-really-an-assembly.dll");
            File.WriteAllText(invalidAssemblyPath, "hello");
            var progressMessages = new List<string>();

            var loadResult = AssemblyInputLoader.LoadAssemblySet([scanDirectory], progressMessages.Add, AssemblyResolutionMode.Restricted);

            Assert.Contains(loadResult.Modules, module =>
                string.Equals(
                    Path.GetFileName(module.Location),
                    Path.GetFileName(sampleAssemblyPath),
                    StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(loadResult.AssemblyOriginsByPath.Keys, path =>
                string.Equals(Path.GetFileName(path), "not-really-an-assembly.dll", StringComparison.OrdinalIgnoreCase));
            CandidateAssemblyLoadFailure failure = Assert.Single(loadResult.Diagnostics.CandidateAssemblyLoadFailures);
            Assert.Equal(Path.GetFullPath(invalidAssemblyPath), failure.Path);
            Assert.NotEmpty(failure.Message);
            Assert.Contains(progressMessages, message =>
                message.Contains("Skipped candidate assembly", StringComparison.Ordinal) &&
                message.Contains("not-really-an-assembly.dll", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(progressMessages, message =>
                message.Contains("Skipped 1 unreadable candidate assembly file(s)", StringComparison.Ordinal));
        }

        [Fact]
        public void Load_result_returns_external_origin_for_unknown_or_null_paths()
        {
            var loadResult = new AssemblyLoadResult(
                [],
                new AssemblyLoadPlan([], [], [], [], [], [], [], AssemblyResolutionMode.Restricted, false, []),
                new Dictionary<string, LoadedAssemblyOrigin>(),
                new AssemblyLoadDiagnostics([], [], [], []));
            string missingAssemblyPath = Path.Combine(
                CreateTempDirectory(),
                $"missing-assembly-origin-{Guid.NewGuid():N}-{Guid.NewGuid():N}-{Guid.NewGuid():N}.dll");

            Assert.False(File.Exists(missingAssemblyPath));

            Assert.Equal(LoadedAssemblyOrigin.External, loadResult.GetAssemblyOrigin(null));
            Assert.Equal(LoadedAssemblyOrigin.External, loadResult.GetAssemblyOrigin(missingAssemblyPath));
        }

        [Fact]
        public void Malformed_runtimeconfig_is_reported()
        {
            var scanDirectory = CreateTempDirectory();
            var sampleAssemblyPath = typeof(MySpecialObject).Assembly.Location;
            var copiedAssemblyPath = Path.Combine(scanDirectory, Path.GetFileName(sampleAssemblyPath));
            File.Copy(sampleAssemblyPath, copiedAssemblyPath, overwrite: true);
            var runtimeConfigPath = Path.ChangeExtension(copiedAssemblyPath, ".runtimeconfig.json");
            File.WriteAllText(runtimeConfigPath, "{ this is not valid json");
            var progressMessages = new List<string>();

            var loadResult = AssemblyInputLoader.LoadAssemblySet([scanDirectory], progressMessages.Add, AssemblyResolutionMode.InferenceWithFallback);

            RuntimeConfigDiagnostic diagnostic = Assert.Single(loadResult.Diagnostics.InvalidRuntimeConfigFiles);
            Assert.Equal(Path.GetFullPath(runtimeConfigPath), diagnostic.Path);
            Assert.NotEmpty(diagnostic.Message);
            Assert.Empty(loadResult.LoadPlan.RequestedFrameworks);
            Assert.Contains("1 runtimeconfig file(s) failed to parse.", loadResult.LoadPlan.Warnings);
            Assert.Contains(progressMessages, message =>
                message.Contains("Invalid runtimeconfig", StringComparison.Ordinal) &&
                message.Contains(runtimeConfigPath, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Runtimeconfig_without_usable_framework_requests_is_counted_and_reported()
        {
            var scanDirectory = CreateTempDirectory();
            var sampleAssemblyPath = typeof(MySpecialObject).Assembly.Location;
            var copiedAssemblyPath = Path.Combine(scanDirectory, Path.GetFileName(sampleAssemblyPath));
            File.Copy(sampleAssemblyPath, copiedAssemblyPath, overwrite: true);
            var runtimeConfigPath = Path.ChangeExtension(copiedAssemblyPath, ".runtimeconfig.json");
            File.WriteAllText(
                runtimeConfigPath,
                """
              {
                "runtimeOptions": {
                  "tfm": "net9.0"
                }
              }
              """);
            var progressMessages = new List<string>();

            var loadResult = AssemblyInputLoader.LoadAssemblySet([scanDirectory], progressMessages.Add, AssemblyResolutionMode.InferenceWithFallback);

            RuntimeConfigDiagnostic diagnostic = Assert.Single(loadResult.Diagnostics.RuntimeConfigFilesWithoutUsableFrameworkRequests);
            Assert.Equal(Path.GetFullPath(runtimeConfigPath), diagnostic.Path);
            Assert.Equal("No usable framework requests were found.", diagnostic.Message);
            Assert.Empty(loadResult.LoadPlan.RequestedFrameworks);
            Assert.Contains("1 runtimeconfig file(s) produced no usable framework requests.", loadResult.LoadPlan.Warnings);
            Assert.Contains(progressMessages, message =>
                message.Contains("Runtimeconfig", StringComparison.Ordinal) &&
                message.Contains("No usable framework requests were found.", StringComparison.Ordinal));
        }

        [Fact]
        public void Runtimeconfig_included_frameworks_are_parsed()
        {
            var scanDirectory = CreateTempDirectory();
            var sampleAssemblyPath = typeof(MySpecialObject).Assembly.Location;
            var copiedAssemblyPath = Path.Combine(scanDirectory, Path.GetFileName(sampleAssemblyPath));
            File.Copy(sampleAssemblyPath, copiedAssemblyPath, overwrite: true);
            File.WriteAllText(
                Path.ChangeExtension(copiedAssemblyPath, ".runtimeconfig.json"),
                """
              {
                "runtimeOptions": {
                  "tfm": "net9.0",
                  "includedFrameworks": [
                    {
                      "name": "Microsoft.NETCore.App",
                      "version": "9.1.2"
                    }
                  ]
                }
              }
              """);

            var loadResult = AssemblyInputLoader.LoadAssemblySet([scanDirectory]);

            Assert.Contains(loadResult.LoadPlan.RequestedFrameworks, framework =>
                framework.Name == TestFrameworkName &&
                framework.Version == Version.Parse(TestFrameworkVersion));
        }

        [Fact]
        public void Runtimeconfig_frameworks_array_is_parsed()
        {
            var scanDirectory = CreateTempDirectory();
            var sampleAssemblyPath = typeof(MySpecialObject).Assembly.Location;
            var copiedAssemblyPath = Path.Combine(scanDirectory, Path.GetFileName(sampleAssemblyPath));
            File.Copy(sampleAssemblyPath, copiedAssemblyPath, overwrite: true);
            File.WriteAllText(
                Path.ChangeExtension(copiedAssemblyPath, ".runtimeconfig.json"),
                """
              {
                "runtimeOptions": {
                  "tfm": "net9.0",
                  "frameworks": [
                    {
                      "name": "Microsoft.NETCore.App",
                      "version": "9.1.2"
                    }
                  ]
                }
              }
              """);

            var loadResult = AssemblyInputLoader.LoadAssemblySet([scanDirectory]);

            Assert.Contains(loadResult.LoadPlan.RequestedFrameworks, framework =>
                framework.Name == TestFrameworkName &&
                framework.Version == Version.Parse(TestFrameworkVersion));
        }

        [Fact]
        public void Restricted_mode_does_not_infer_installed_runtime_when_runtimeconfig_requests_a_framework()
        {
            var scanDirectory = CreateTempDirectory();
            var sampleAssemblyPath = typeof(MySpecialObject).Assembly.Location;
            var copiedAssemblyPath = Path.Combine(scanDirectory, Path.GetFileName(sampleAssemblyPath));
            File.Copy(sampleAssemblyPath, copiedAssemblyPath, overwrite: true);
            File.WriteAllText(
                Path.ChangeExtension(copiedAssemblyPath, ".runtimeconfig.json"),
                """
              {
                "runtimeOptions": {
                  "tfm": "net9.0",
                  "framework": {
                    "name": "Microsoft.NETCore.App",
                    "version": "9.1.2"
                  }
                }
              }
              """);

            var loadResult = AssemblyInputLoader.LoadAssemblySet([scanDirectory], assemblyResolutionMode: AssemblyResolutionMode.Restricted);

            Assert.Equal(AssemblyResolutionMode.Restricted, loadResult.LoadPlan.AssemblyResolutionMode);
            Assert.NotEmpty(loadResult.LoadPlan.RuntimeConfigPaths);
            Assert.Contains(loadResult.LoadPlan.RequestedFrameworks, framework =>
                framework.Name == TestFrameworkName &&
                framework.Version == Version.Parse(TestFrameworkVersion));
            Assert.Empty(loadResult.LoadPlan.InferredInstalledRuntimeDirectories);
            Assert.False(loadResult.LoadPlan.UsedHostRuntimeFallback);
            Assert.All(loadResult.LoadPlan.ResolverSearchDirectories, searchDirectory => Assert.StartsWith(scanDirectory, searchDirectory, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Restricted_mode_reports_unresolved_references_for_partially_loadable_inputs()
        {
            var scanDirectory = CreateTempDirectory();
            var copiedAssemblyPath = Path.Combine(scanDirectory, "MissingDependencySample.dll");
            WriteAssemblyWithMissingReference(copiedAssemblyPath);
            var progressMessages = new List<string>();

            var loadResult = AssemblyInputLoader.LoadAssemblySet([copiedAssemblyPath], progressMessages.Add, AssemblyResolutionMode.Restricted);

            Assert.NotEmpty(loadResult.Modules);
            Assert.NotEmpty(loadResult.Diagnostics.UnresolvedReferences);
            Assert.Contains(progressMessages, message =>
                message.Contains("unresolved assembly reference(s)", StringComparison.Ordinal));

            UnresolvedAssemblyReference unresolvedReference = loadResult.Diagnostics.UnresolvedReferences[0];
            Assert.Contains(Path.GetFileName(copiedAssemblyPath), unresolvedReference.RequestingModulePath, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(unresolvedReference.RequestingModuleDisplayName);
            Assert.NotEmpty(unresolvedReference.ReferenceDisplayName);
            Assert.NotEmpty(unresolvedReference.Message);
        }

        public void Dispose()
        {
            foreach (var directory in _temporaryDirectories)
            {
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                }
                catch
                {
                    // Best-effort cleanup for test temp directories.
                }
            }
        }

        private string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "GadgetExplorer.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _temporaryDirectories.Add(directory);
            return directory;
        }

        private static void WriteAssemblyWithMissingReference(string assemblyPath)
        {
            var corLibAssemblyRef = new AssemblyRefUser(new AssemblyNameInfo(typeof(object).Assembly.GetName().FullName));
            var module = new ModuleDefUser("MissingDependencySample", null, corLibAssemblyRef)
            {
                Kind = ModuleKind.Dll,
                RuntimeVersion = "v4.0.30319"
            };
            var assembly = new AssemblyDefUser("MissingDependencySample", new Version(1, 0, 0, 0));
            assembly.Modules.Add(module);

            var fakeDependency = new AssemblyRefUser(new AssemblyNameInfo("DefinitelyMissingDependency, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));
            var missingBaseType = new TypeRefUser(module, "Synthetic.Missing", "MissingDependencyBaseType", fakeDependency);
            var sampleType = new TypeDefUser("Synthetic", "MissingDependencyType", missingBaseType)
            {
                Attributes = TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit
            };
            module.Types.Add(sampleType);
            module.Write(assemblyPath);
        }
    }
}
