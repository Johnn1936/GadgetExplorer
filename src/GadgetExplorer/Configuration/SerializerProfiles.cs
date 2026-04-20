/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace GadgetExplorer.Configuration
{
    /// <summary>
    /// Loads serializer profiles from shipped or user-specified profile files.
    /// </summary>
    public static class SerializerProfiles
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        private static readonly Lazy<ReadOnlyDictionary<string, SerializerProfileEntry>> s_shippedProfiles = new(LoadShippedProfiles, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Resolves a serializer profile by shipped name or explicit profile-file path.
        /// </summary>
        /// <param name="nameOrPath">The required shipped profile name or explicit path.</param>
        public static SerializerProfile Resolve(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                throw new InvalidOperationException(
                    $"A serializer profile is required. Use --profile <name> or --profile-file <path>. Available shipped profiles: {string.Join(", ", GetAvailableProfileNames())}.");
            }

            if (LooksLikePath(nameOrPath))
            {
                return LoadFromPath(nameOrPath);
            }

            return ResolveShipped(nameOrPath);
        }

        /// <summary>
        /// Resolves a shipped serializer profile by name.
        /// </summary>
        /// <param name="name">The shipped profile name.</param>
        public static SerializerProfile ResolveShipped(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException(
                    $"A serializer profile is required. Use --profile <name> or --profile-file <path>. Available shipped profiles: {string.Join(", ", GetAvailableProfileNames())}.");
            }

            string normalizedName = NormalizeProfileName(name);
            if (s_shippedProfiles.Value.TryGetValue(normalizedName, out SerializerProfileEntry? entry))
            {
                return entry.Profile;
            }

            throw new InvalidOperationException(
                $"Unknown serializer profile '{name}'. Available shipped profiles: {string.Join(", ", GetAvailableProfileNames())}.");
        }

        /// <summary>
        /// Loads a serializer profile from an explicit file path.
        /// </summary>
        /// <param name="path">The explicit profile file path.</param>
        public static SerializerProfile LoadFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("A serializer profile file path is required.");
            }

            return LoadProfileFromPath(Path.GetFullPath(path));
        }

        /// <summary>
        /// Gets the primary names of the shipped serializer profiles.
        /// </summary>
        public static IReadOnlyList<string> GetAvailableProfileNames()
            => [.. s_shippedProfiles.Value.Values
                .Select(entry => entry.Profile.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)];

        /// <summary>
        /// Loads all shipped serializer profiles from the tool's profile directory.
        /// </summary>
        private static ReadOnlyDictionary<string, SerializerProfileEntry> LoadShippedProfiles()
        {
            string profileDirectory = GetShippedProfileDirectory();
            if (!Directory.Exists(profileDirectory))
            {
                throw new InvalidOperationException($"The shipped serializer profile directory was not found: {profileDirectory}");
            }

            var entries = new Dictionary<string, SerializerProfileEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (string filePath in Directory.EnumerateFiles(profileDirectory, "*.profile.json", SearchOption.TopDirectoryOnly)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                SerializerProfile profile = LoadProfileFromPath(filePath);
                var entry = new SerializerProfileEntry(filePath, profile);
                RegisterShippedProfile(entries, NormalizeProfileName(profile.Name), entry);
                RegisterShippedProfile(entries, NormalizeProfileName(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath))), entry);
            }

            if (entries.Count == 0)
            {
                throw new InvalidOperationException($"No shipped serializer profiles were found in '{profileDirectory}'.");
            }

            return new ReadOnlyDictionary<string, SerializerProfileEntry>(entries);
        }

        /// <summary>
        /// Resolves the shipped serializer-profile directory.
        /// </summary>
        private static string GetShippedProfileDirectory()
            => ShippedResourcePaths.GetDeployedResourceDirectory(
                AppContext.BaseDirectory,
                ShippedResourcePaths.SerializerProfilesDirectoryName);

        /// <summary>
        /// Loads and validates a serializer profile from disk.
        /// </summary>
        /// <param name="filePath">The profile-file path.</param>
        private static SerializerProfile LoadProfileFromPath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"Serializer profile file was not found: {filePath}");
            }

            string json = File.ReadAllText(filePath);
            SerializerProfile profile = JsonSerializer.Deserialize<SerializerProfile>(json, s_jsonOptions) ?? throw new InvalidOperationException($"Serializer profile file '{filePath}' could not be parsed.");
            ValidateProfile(profile, filePath);
            return profile;
        }

        /// <summary>
        /// Validates the shape of a loaded serializer profile.
        /// </summary>
        /// <param name="profile">The profile to validate.</param>
        /// <param name="filePath">The source file path.</param>
        private static void ValidateProfile(SerializerProfile profile, string filePath)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                throw new InvalidOperationException($"Serializer profile file '{filePath}' does not define a profile name.");
            }

            if (profile.ActivationPolicies.Count == 0)
            {
                throw new InvalidOperationException($"Serializer profile '{profile.Name}' does not define any activation policies.");
            }

            foreach (ActivationPolicy activationPolicy in profile.ActivationPolicies)
            {
                if (activationPolicy.ConstructorSelectionRules.Any(rule => rule.Target == ConstructorSelectionTarget.BestMatch) &&
                    activationPolicy.ConstructorBindingModes.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Serializer profile '{profile.Name}' must define activationPolicies[].constructorBindingModes when using best-match constructor selection.");
                }

                if (activationPolicy.Mode == ActivationMode.SerializationConstructor &&
                    activationPolicy.SerializationConstructorSignature is null)
                {
                    throw new InvalidOperationException(
                        $"Serializer profile '{profile.Name}' must define activationPolicies[].serializationConstructorSignature for serialization-constructor activation.");
                }

                if (activationPolicy.Mode == ActivationMode.SerializationConstructor &&
                    activationPolicy.SerializationConstructorSignature?.VisibilityPolicy is null)
                {
                    throw new InvalidOperationException(
                        $"Serializer profile '{profile.Name}' must define activationPolicies[].serializationConstructorSignature.visibilityPolicy for serialization-constructor activation.");
                }
            }
        }

        /// <summary>
        /// Registers a shipped serializer profile under a normalized lookup key.
        /// </summary>
        /// <param name="entries">The mutable registry.</param>
        /// <param name="normalizedName">The normalized lookup key.</param>
        /// <param name="entry">The entry to register.</param>
        private static void RegisterShippedProfile(
            IDictionary<string, SerializerProfileEntry> entries,
            string normalizedName,
            SerializerProfileEntry entry)
        {
            if (entries.TryGetValue(normalizedName, out SerializerProfileEntry? existing) &&
                !string.Equals(existing.FilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Duplicate serializer profile name '{entry.Profile.Name}' between '{existing.FilePath}' and '{entry.FilePath}'.");
            }

            entries[normalizedName] = entry;
        }

        /// <summary>
        /// Determines whether a profile reference should be treated as a file path.
        /// </summary>
        /// <param name="nameOrPath">The raw profile reference.</param>
        private static bool LooksLikePath(string nameOrPath)
            => nameOrPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
               nameOrPath.Contains(Path.DirectorySeparatorChar) ||
               nameOrPath.Contains(Path.AltDirectorySeparatorChar);

        /// <summary>
        /// Normalizes profile names for lookup.
        /// </summary>
        /// <param name="name">The raw profile name.</param>
        private static string NormalizeProfileName(string name)
            => new string(name
                    .Where(ch => ch is not '-' and not '_' and not '.' and not ' ')
                    .ToArray())
                .ToLowerInvariant();

        /// <summary>
        /// Stores one shipped profile entry.
        /// </summary>
        /// <param name="FilePath">The source file path.</param>
        /// <param name="Profile">The loaded serializer profile.</param>
        private sealed record SerializerProfileEntry(
            string FilePath,
            SerializerProfile Profile);
    }
}
