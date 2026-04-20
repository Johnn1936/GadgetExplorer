/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Configuration
{
    internal static class ShippedResourcePaths
    {
        internal const string SinksDirectoryName = "sinks";
        internal const string IgnoreSinksDirectoryName = "ignore-sinks";
        internal const string SerializerProfilesDirectoryName = "serializer-profiles";

        internal static string GetDeployedResourceDirectory(string baseDirectory, string resourceDirectoryName)
            => Path.GetFullPath(Path.Combine(baseDirectory, resourceDirectoryName));
    }
}
