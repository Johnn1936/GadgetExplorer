/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Loading
{
    internal sealed class AssemblyLoadPolicy(AssemblyLoadPlan loadPlan)
    {
        public bool IsAllowedResolvedAssemblyLocation(string assemblyPath)
        {
            if (IsUnderAnyDirectory(assemblyPath, loadPlan.InputRoots))
            {
                return true;
            }

            if (loadPlan.AssemblyResolutionMode == AssemblyResolutionMode.Restricted)
            {
                return false;
            }

            if (IsUnderAnyDirectory(assemblyPath, loadPlan.InferredInstalledRuntimeDirectories))
            {
                return true;
            }

            return loadPlan.UsedHostRuntimeFallback &&
                   (IsUnderAnyDirectory(assemblyPath, loadPlan.HostRuntimeSearchDirectories) || IsGlobalAssemblyCachePath(assemblyPath));
        }

        public LoadedAssemblyOrigin ClassifyLoadedAssemblyOrigin(string assemblyPath)
        {
            if (IsUnderAnyDirectory(assemblyPath, loadPlan.InputRoots))
            {
                return LoadedAssemblyOrigin.InputRoot;
            }

            if (IsUnderAnyDirectory(assemblyPath, loadPlan.InferredInstalledRuntimeDirectories))
            {
                return LoadedAssemblyOrigin.InferredInstalledRuntime;
            }

            if (loadPlan.UsedHostRuntimeFallback && IsUnderAnyDirectory(assemblyPath, loadPlan.HostRuntimeSearchDirectories))
            {
                return LoadedAssemblyOrigin.HostRuntimeFallback;
            }

            return IsGlobalAssemblyCachePath(assemblyPath)
                ? LoadedAssemblyOrigin.GlobalAssemblyCache
                : LoadedAssemblyOrigin.External;
        }

        private static bool IsUnderAnyDirectory(string filePath, IEnumerable<string> directories)
            => directories.Any(directory => IsUnderDirectory(filePath, directory));

        private static bool IsUnderDirectory(string filePath, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            string normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath)) + Path.DirectorySeparatorChar;
            string normalizedFile = Path.GetFullPath(filePath);
            return normalizedFile.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGlobalAssemblyCachePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalizedPath = Path.GetFullPath(path);
            return normalizedPath.Contains($"{Path.DirectorySeparatorChar}Microsoft.NET{Path.DirectorySeparatorChar}assembly{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
        }
    }
}
