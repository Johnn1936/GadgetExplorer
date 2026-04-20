/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Loading
{
    internal static class AssemblyInputExpander
    {
        public static AssemblyInputExpansion Expand(IEnumerable<string> inputs)
        {
            var candidateAssemblyFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var runtimeConfigPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inputRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawInput in inputs)
            {
                if (string.IsNullOrWhiteSpace(rawInput))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(rawInput);
                if (Directory.Exists(fullPath))
                {
                    inputRoots.Add(fullPath);

                    foreach (string file in Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories)
                                 .Concat(Directory.EnumerateFiles(fullPath, "*.exe", SearchOption.AllDirectories))
                                 .Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        candidateAssemblyFiles.Add(file);
                    }

                    foreach (string runtimeConfigPath in Directory.EnumerateFiles(fullPath, "*.runtimeconfig.json", SearchOption.AllDirectories))
                    {
                        runtimeConfigPaths.Add(runtimeConfigPath);
                    }

                    continue;
                }

                if (!File.Exists(fullPath) ||
                    (!fullPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                     !fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                candidateAssemblyFiles.Add(fullPath);
                string? containingDirectory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(containingDirectory))
                {
                    inputRoots.Add(containingDirectory);
                }

                string siblingRuntimeConfigPath = Path.ChangeExtension(fullPath, ".runtimeconfig.json");
                if (File.Exists(siblingRuntimeConfigPath))
                {
                    runtimeConfigPaths.Add(Path.GetFullPath(siblingRuntimeConfigPath));
                }
            }

            string[] orderedCandidateAssemblyFiles = [.. candidateAssemblyFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];
            string[] candidateDirectories = [.. orderedCandidateAssemblyFiles
                .Select(Path.GetDirectoryName)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];

            return new AssemblyInputExpansion(
                orderedCandidateAssemblyFiles,
                candidateDirectories,
                [.. inputRoots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)],
                [.. runtimeConfigPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)]);
        }
    }

    internal sealed record AssemblyInputExpansion(
        IReadOnlyList<string> CandidateAssemblyFiles,
        IReadOnlyList<string> CandidateDirectories,
        IReadOnlyList<string> InputRoots,
        IReadOnlyList<string> RuntimeConfigPaths);
}
