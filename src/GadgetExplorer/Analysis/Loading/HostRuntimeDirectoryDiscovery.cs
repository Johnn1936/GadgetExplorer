/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Loading
{
    internal static class HostRuntimeDirectoryDiscovery
    {
        public static IReadOnlyList<string> GetSearchDirectories()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var directories = new List<string>();

            string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
            {
                foreach (string? directory in trustedPlatformAssemblies
                             .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                             .Select(Path.GetDirectoryName)
                             .Where(path => !string.IsNullOrWhiteSpace(path)))
                {
                    if (seen.Add(directory!))
                    {
                        directories.Add(directory!);
                    }
                }
            }

            string? objectAssemblyDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(objectAssemblyDirectory) && seen.Add(objectAssemblyDirectory))
            {
                directories.Add(objectAssemblyDirectory);
            }

            return directories;
        }
    }
}
