/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Loading
{
    internal static class InstalledRuntimeLocator
    {
        public static IReadOnlyList<string> InferDirectories(IReadOnlyList<RuntimeFrameworkRequest> requestedFrameworks)
        {
            var inferredDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RuntimeFrameworkRequest framework in requestedFrameworks)
            {
                string? matchingDirectory = FindBestInstalledRuntimeDirectory(framework);
                if (!string.IsNullOrWhiteSpace(matchingDirectory))
                {
                    inferredDirectories.Add(matchingDirectory);
                }
            }

            return [.. inferredDirectories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];
        }

        private static string? FindBestInstalledRuntimeDirectory(RuntimeFrameworkRequest framework)
        {
            var candidates = new List<(Version Version, string Path)>();
            foreach (string sharedFrameworkRoot in GetSharedFrameworkRoots())
            {
                string frameworkRoot = Path.Combine(sharedFrameworkRoot, framework.Name);
                if (!Directory.Exists(frameworkRoot))
                {
                    continue;
                }

                foreach (string versionDirectory in Directory.EnumerateDirectories(frameworkRoot))
                {
                    string versionName = Path.GetFileName(versionDirectory);
                    if (!Version.TryParse(versionName, out Version? installedVersion))
                    {
                        continue;
                    }

                    if (installedVersion.Major == framework.Version.Major &&
                        installedVersion.Minor == framework.Version.Minor)
                    {
                        candidates.Add((installedVersion, versionDirectory));
                    }
                }
            }

            return candidates
                .OrderBy(candidate => GetVersionDistance(candidate.Version, framework.Version))
                .ThenByDescending(candidate => candidate.Version)
                .Select(candidate => candidate.Path)
                .FirstOrDefault();
        }

        private static IEnumerable<string> GetSharedFrameworkRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? currentRuntimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(currentRuntimeDirectory))
            {
                DirectoryInfo? versionDirectory = Directory.GetParent(currentRuntimeDirectory);
                DirectoryInfo? frameworkDirectory = versionDirectory?.Parent;
                string? sharedRoot = frameworkDirectory?.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(sharedRoot) && Directory.Exists(sharedRoot) && seen.Add(sharedRoot))
                {
                    yield return sharedRoot;
                }
            }

            foreach (string? dotnetRoot in new[]
                     {
                         Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                         Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet")
                     })
            {
                if (string.IsNullOrWhiteSpace(dotnetRoot))
                {
                    continue;
                }

                string sharedRoot = Path.Combine(dotnetRoot, "shared");
                if (Directory.Exists(sharedRoot) && seen.Add(sharedRoot))
                {
                    yield return sharedRoot;
                }
            }
        }

        private static long GetVersionDistance(Version candidateVersion, Version referenceVersion)
            => Math.Abs(candidateVersion.Major - referenceVersion.Major) * 1_000_000_000L +
               Math.Abs(candidateVersion.Minor - referenceVersion.Minor) * 1_000_000L +
               Math.Abs(candidateVersion.Build - referenceVersion.Build) * 1_000L +
               Math.Abs(candidateVersion.Revision - referenceVersion.Revision);
    }
}
