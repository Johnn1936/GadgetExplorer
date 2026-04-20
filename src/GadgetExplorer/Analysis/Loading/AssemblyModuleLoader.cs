/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Loading
{
    internal static class AssemblyModuleLoader
    {
        public static AssemblyModuleLoadResult Load(AssemblyLoadPlan loadPlan, Action<string>? progress = null)
        {
            var resolver = new AssemblyResolver
            {
                EnableTypeDefCache = true
            };

            foreach (string directory in loadPlan.ResolverSearchDirectories)
            {
                resolver.PostSearchPaths.Add(directory);
            }

            var moduleContext = new ModuleContext(resolver);
            resolver.DefaultModuleContext = moduleContext;

            var policy = new AssemblyLoadPolicy(loadPlan);
            var modulesByPath = new Dictionary<string, ModuleDefMD>(StringComparer.OrdinalIgnoreCase);
            var originsByPath = new Dictionary<string, LoadedAssemblyOrigin>(StringComparer.OrdinalIgnoreCase);
            var candidateAssemblyLoadFailures = new List<CandidateAssemblyLoadFailure>();
            var unresolvedReferences = new List<UnresolvedAssemblyReference>();
            var queue = new Queue<string>(loadPlan.CandidateAssemblyFiles);
            var candidateFiles = new HashSet<string>(loadPlan.CandidateAssemblyFiles, StringComparer.OrdinalIgnoreCase);
            int processedCandidateFileCount = 0;
            int lastReportedProgressStep = 0;

            while (queue.Count > 0)
            {
                string file = queue.Dequeue();
                bool isCandidateFile = candidateFiles.Remove(file);
                if (modulesByPath.ContainsKey(file))
                {
                    AssemblyLoadProgressReporter.ReportCandidateFileProgress(progress, isCandidateFile, ref processedCandidateFileCount, loadPlan.CandidateAssemblyFiles.Count, ref lastReportedProgressStep, modulesByPath.Count, null);
                    continue;
                }

                ModuleDefMD module;
                try
                {
                    module = ModuleDefMD.Load(file, moduleContext);
                }
                catch (Exception ex)
                {
                    if (isCandidateFile)
                    {
                        string message = GetExceptionSummary(ex);
                        candidateAssemblyLoadFailures.Add(new CandidateAssemblyLoadFailure(file, message));
                        progress?.Invoke($"Warning: Skipped candidate assembly '{file}': {message}");
                    }

                    AssemblyLoadProgressReporter.ReportCandidateFileProgress(progress, isCandidateFile, ref processedCandidateFileCount, loadPlan.CandidateAssemblyFiles.Count, ref lastReportedProgressStep, modulesByPath.Count, null);
                    continue;
                }

                modulesByPath[file] = module;
                originsByPath[file] = policy.ClassifyLoadedAssemblyOrigin(file);
                string? latestModuleDisplayName = module.Assembly?.FullName ?? module.Name;

                if (module.Assembly is not null)
                {
                    resolver.AddToCache(module.Assembly);
                }

                foreach (AssemblyRef? reference in module.GetAssemblyRefs())
                {
                    AssemblyDef? resolvedAssembly;
                    try
                    {
                        resolvedAssembly = resolver.Resolve(reference, module);
                    }
                    catch (Exception ex)
                    {
                        unresolvedReferences.Add(new UnresolvedAssemblyReference(
                            GetModuleLocation(module) ?? file,
                            latestModuleDisplayName,
                            reference.FullName,
                            GetExceptionSummary(ex)));
                        continue;
                    }

                    if (resolvedAssembly?.ManifestModule is not ModuleDefMD referencedModule)
                    {
                        unresolvedReferences.Add(new UnresolvedAssemblyReference(
                            GetModuleLocation(module) ?? file,
                            latestModuleDisplayName,
                            reference.FullName,
                            "Resolver returned no loadable module."));
                        continue;
                    }

                    string? referencedLocation = GetModuleLocation(referencedModule);
                    if (referencedLocation is null ||
                        modulesByPath.ContainsKey(referencedLocation) ||
                        !policy.IsAllowedResolvedAssemblyLocation(referencedLocation))
                    {
                        continue;
                    }

                    queue.Enqueue(referencedLocation);
                }

                AssemblyLoadProgressReporter.ReportCandidateFileProgress(progress, isCandidateFile, ref processedCandidateFileCount, loadPlan.CandidateAssemblyFiles.Count, ref lastReportedProgressStep, modulesByPath.Count, latestModuleDisplayName);
            }

            ModuleDefMD[] orderedModules = [.. modulesByPath.Values.OrderBy(module => module.Assembly?.FullName ?? module.Name, StringComparer.Ordinal)];

            return new AssemblyModuleLoadResult(
                orderedModules,
                new Dictionary<string, LoadedAssemblyOrigin>(originsByPath, StringComparer.OrdinalIgnoreCase),
                candidateAssemblyLoadFailures,
                unresolvedReferences);
        }

        private static string GetExceptionSummary(Exception ex)
        {
            Exception current = ex;
            while (current.InnerException is not null)
            {
                current = current.InnerException;
            }

            return string.IsNullOrWhiteSpace(current.Message)
                ? current.GetType().Name
                : $"{current.GetType().Name}: {current.Message}";
        }

        private static string? GetModuleLocation(ModuleDefMD module)
        {
            try
            {
                return module.Location;
            }
            catch
            {
                return null;
            }
        }
    }

    internal sealed record AssemblyModuleLoadResult(
        IReadOnlyList<ModuleDefMD> Modules,
        IReadOnlyDictionary<string, LoadedAssemblyOrigin> AssemblyOriginsByPath,
        IReadOnlyList<CandidateAssemblyLoadFailure> CandidateAssemblyLoadFailures,
        IReadOnlyList<UnresolvedAssemblyReference> UnresolvedReferences);
}
