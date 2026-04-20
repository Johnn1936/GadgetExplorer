/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GadgetExplorer.Configuration
{
    /// <summary>
    /// Shared loader for sink-definition based configuration files.
    /// </summary>
    internal static class SinkDefinitionFileLoader
    {
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        public static IReadOnlyList<SinkDefinition> LoadRequired(string resolvedPath, string configurationLabel, string searchPattern)
        {
            if (File.Exists(resolvedPath))
            {
                return LoadFromFile(resolvedPath, configurationLabel);
            }

            if (Directory.Exists(resolvedPath))
            {
                return LoadFromDirectory(resolvedPath, configurationLabel, searchPattern, requireAtLeastOneFile: true);
            }

            throw new InvalidOperationException($"{configurationLabel} file or directory was not found: {resolvedPath}");
        }

        public static IReadOnlyList<SinkDefinition> LoadOptional(string resolvedPath, string configurationLabel, string searchPattern)
        {
            if (File.Exists(resolvedPath))
            {
                return LoadFromFile(resolvedPath, configurationLabel);
            }

            if (Directory.Exists(resolvedPath))
            {
                return LoadFromDirectory(resolvedPath, configurationLabel, searchPattern, requireAtLeastOneFile: false);
            }

            return [];
        }

        private static SinkDefinition[] LoadFromDirectory(string resolvedPath, string configurationLabel, string searchPattern, bool requireAtLeastOneFile)
        {
            string[] matchingFiles = Directory.EnumerateFiles(resolvedPath, searchPattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();

            if (matchingFiles.Length == 0)
            {
                if (requireAtLeastOneFile)
                {
                    throw new InvalidOperationException($"{configurationLabel} directory '{resolvedPath}' does not contain any files matching '{searchPattern}'.");
                }

                return [];
            }

            return matchingFiles
                .SelectMany(path => LoadFromFile(path, configurationLabel))
                .ToArray();
        }

        private static SinkDefinition[] LoadFromFile(string resolvedPath, string configurationLabel)
        {
            string json = File.ReadAllText(resolvedPath);
            SinkConfigFile? model = JsonSerializer.Deserialize<SinkConfigFile>(json, s_jsonSerializerOptions);
            if (model?.Sinks?.Count is not > 0)
            {
                throw new InvalidOperationException($"{configurationLabel} file '{resolvedPath}' does not contain any sinks.");
            }

            SinkDefinition[] sinks = model.Sinks
                .Select(sink => new SinkDefinition(
                    sink.DeclaringType?.Trim() ?? string.Empty,
                    sink.MethodName?.Trim() ?? string.Empty,
                    ResolveParameters(sink, resolvedPath, configurationLabel),
                    sink.NativeModule?.Trim() ?? string.Empty,
                    sink.NativeEntryPoint?.Trim() ?? string.Empty))
                .ToArray();

            SinkDefinition? invalidSink = sinks.FirstOrDefault(sink =>
                string.IsNullOrWhiteSpace(sink.DeclaringType) &&
                string.IsNullOrWhiteSpace(sink.MethodName) &&
                string.IsNullOrWhiteSpace(sink.NativeModule) &&
                string.IsNullOrWhiteSpace(sink.NativeEntryPoint));

            if (invalidSink is not null)
            {
                throw new InvalidOperationException($"{configurationLabel} file '{resolvedPath}' contains a sink with no declaringType, methodName, nativeModule, or nativeEntryPoint.");
            }

            SinkDefinition? invalidSignatureSink = sinks.FirstOrDefault(sink =>
                sink.HasExplicitParameterSignature &&
                string.IsNullOrWhiteSpace(sink.MethodName) &&
                string.IsNullOrWhiteSpace(sink.NativeEntryPoint));

            if (invalidSignatureSink is not null)
            {
                throw new InvalidOperationException($"{configurationLabel} file '{resolvedPath}' contains a sink with parameterTypeNames but no methodName or nativeEntryPoint.");
            }

            return sinks;
        }

        private static SinkParameterDefinition[]? ResolveParameters(SinkConfigEntry sink, string resolvedPath, string configurationLabel)
        {
            if (sink.Parameters is not null)
            {
                if (sink.ParameterTypeNames is { Count: > 0 })
                {
                    throw new InvalidOperationException($"{configurationLabel} file '{resolvedPath}' contains a sink that mixes the parameters format with parameterTypeNames.");
                }

                SinkParameterDefinition[] resolvedParameters = sink.Parameters
                    .Select(parameter => new SinkParameterDefinition(
                        parameter.TypeName?.Trim() ?? string.Empty,
                        parameter.IgnoreSinkIfConstant is true))
                    .ToArray();

                if (resolvedParameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.TypeName)))
                {
                    throw new InvalidOperationException($"{configurationLabel} file '{resolvedPath}' contains a sink parameter with no typeName.");
                }

                return resolvedParameters;
            }

            if (sink.ParameterTypeNames is not { Count: > 0 })
            {
                return null;
            }

            return sink.ParameterTypeNames
                .Select(parameterTypeName => new SinkParameterDefinition(
                    parameterTypeName?.Trim() ?? string.Empty,
                    false))
                .ToArray();
        }
    }
}
