/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis
{
    public static partial class SinkAnalyzer
    {
        /// <summary>
        /// Finds methods that match a sink definition.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="sinkDefinition">The sink definition to resolve.</param>
        /// <param name="exactManagedPairLookup">The exact managed sink-pair lookup.</param>
        private static MethodId[] FindMatchingSinkMethods(
            AnalysisIndex index,
            SinkDefinition sinkDefinition,
            IReadOnlyDictionary<SinkMethodLookupKey, MethodId[]> exactManagedPairLookup)
            => GetSinkMethodCandidates(index, sinkDefinition, exactManagedPairLookup)
                .Where(method => MatchesMethod(method, sinkDefinition))
                .OrderBy(method => method.DisplayName, StringComparer.Ordinal)
                .Select(method => method.Id)
                .ToArray();

        /// <summary>
        /// Builds the exact managed declaring-type/method-name lookup used for narrow sink resolution.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        private static IReadOnlyDictionary<SinkMethodLookupKey, MethodId[]> BuildExactManagedSinkPairLookup(AnalysisIndex index)
        {
            var methodIdsByLookupKey = new Dictionary<SinkMethodLookupKey, List<MethodId>>();

            foreach (MethodRecord method in index.Methods)
            {
                IType? declaringType = method.MethodReference.DeclaringType;
                if (declaringType is null)
                {
                    continue;
                }

                string? exactDeclaringTypeName = AnalysisIndex.GetTypeDisplayName(AnalysisIndex.ToTypeSig(declaringType));
                AddExactManagedSinkPair(methodIdsByLookupKey, exactDeclaringTypeName, method);

                if (!string.Equals(declaringType.Name, exactDeclaringTypeName, StringComparison.Ordinal))
                {
                    AddExactManagedSinkPair(methodIdsByLookupKey, declaringType.Name, method);
                }
            }

            return methodIdsByLookupKey.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray());
        }

        /// <summary>
        /// Gets the candidate methods for a sink definition.
        /// </summary>
        /// <param name="index">The analysis index to query.</param>
        /// <param name="sinkDefinition">The sink definition to resolve.</param>
        /// <param name="exactManagedPairLookup">The exact managed sink-pair lookup.</param>
        private static IEnumerable<MethodRecord> GetSinkMethodCandidates(
            AnalysisIndex index,
            SinkDefinition sinkDefinition,
            IReadOnlyDictionary<SinkMethodLookupKey, MethodId[]> exactManagedPairLookup)
        {
            if (!CanUseExactManagedPairLookup(sinkDefinition) ||
                !exactManagedPairLookup.TryGetValue(new SinkMethodLookupKey(sinkDefinition.DeclaringType, sinkDefinition.MethodName), out MethodId[]? candidateMethodIds))
            {
                return index.Methods;
            }

            return candidateMethodIds.Select(index.GetMethod);
        }

        /// <summary>
        /// Determines whether a sink definition can use the exact managed lookup.
        /// </summary>
        /// <param name="sinkDefinition">The sink definition to evaluate.</param>
        private static bool CanUseExactManagedPairLookup(SinkDefinition sinkDefinition)
            => !string.IsNullOrWhiteSpace(sinkDefinition.DeclaringType) &&
               !string.IsNullOrWhiteSpace(sinkDefinition.MethodName) &&
               !IsWildcardMethodName(sinkDefinition.MethodName) &&
               string.IsNullOrWhiteSpace(sinkDefinition.NativeModule) &&
               string.IsNullOrWhiteSpace(sinkDefinition.NativeEntryPoint);

        /// <summary>
        /// Adds a method to the exact managed sink-pair lookup.
        /// </summary>
        /// <param name="methodIdsByLookupKey">The lookup under construction.</param>
        /// <param name="declaringTypeName">The declaring type name to index.</param>
        /// <param name="method">The method to add.</param>
        private static void AddExactManagedSinkPair(
            IDictionary<SinkMethodLookupKey, List<MethodId>> methodIdsByLookupKey,
            string? declaringTypeName,
            MethodRecord method)
        {
            if (string.IsNullOrWhiteSpace(declaringTypeName))
            {
                return;
            }

            var lookupKey = new SinkMethodLookupKey(declaringTypeName, method.Name);
            if (!methodIdsByLookupKey.TryGetValue(lookupKey, out List<MethodId>? methodIds))
            {
                methodIds = [];
                methodIdsByLookupKey.Add(lookupKey, methodIds);
            }

            methodIds.Add(method.Id);
        }

        /// <summary>
        /// Determines whether a method is ignored by configuration.
        /// </summary>
        /// <param name="method">The method to evaluate.</param>
        /// <param name="ignoreSinkDefinitions">The ignore-sink definitions.</param>
        private static bool IsIgnored(MethodRecord method, IReadOnlyList<SinkDefinition> ignoreSinkDefinitions)
            => ignoreSinkDefinitions.Any(ignore => MatchesMethod(method, ignore));

        /// <summary>
        /// Determines whether a method matches a sink definition.
        /// </summary>
        /// <param name="method">The method to evaluate.</param>
        /// <param name="definition">The sink definition to compare against.</param>
        private static bool MatchesMethod(MethodRecord method, SinkDefinition definition)
            => MatchesDeclaringType(method.MethodReference, definition.DeclaringType) &&
               MatchesMethodName(method.Name, definition.MethodName) &&
               MatchesMethodSignature(method, definition) &&
               MatchesNativeImport(method, definition);

        /// <summary>
        /// Determines whether a method's declaring type matches the configured type filter.
        /// </summary>
        /// <param name="method">The method to evaluate.</param>
        /// <param name="configuredDeclaringType">The configured declaring type filter.</param>
        private static bool MatchesDeclaringType(IMethod method, string configuredDeclaringType)
        {
            if (string.IsNullOrWhiteSpace(configuredDeclaringType))
            {
                return true;
            }

            IType? declaringType = method.DeclaringType;
            if (declaringType is null)
            {
                return false;
            }

            return AnalysisIndex.MatchesConfiguredTypeName(AnalysisIndex.ToTypeSig(declaringType), configuredDeclaringType) ||
                   string.Equals(declaringType.Name, configuredDeclaringType, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether a method name matches the configured method-name filter.
        /// </summary>
        /// <param name="actualMethodName">The actual method name.</param>
        /// <param name="configuredMethodName">The configured method-name filter.</param>
        private static bool MatchesMethodName(string actualMethodName, string configuredMethodName)
        {
            if (string.IsNullOrWhiteSpace(configuredMethodName))
            {
                return true;
            }

            if (IsWildcardMethodName(configuredMethodName))
            {
                string methodNamePrefix = GetConfiguredMethodNamePrefix(configuredMethodName);
                return actualMethodName.StartsWith(methodNamePrefix, StringComparison.Ordinal);
            }

            return string.Equals(actualMethodName, configuredMethodName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether a method matches the configured overload signature.
        /// </summary>
        /// <param name="method">The method to evaluate.</param>
        /// <param name="definition">The sink definition to compare against.</param>
        private static bool MatchesMethodSignature(MethodRecord method, SinkDefinition definition)
        {
            if (!definition.HasExplicitParameterSignature)
            {
                return true;
            }

            return AnalysisIndex.HasExactParameterSignature(method.MethodReference.MethodSig, definition.ParameterTypeNames);
        }

        /// <summary>
        /// Determines whether a method's imported native module and entry point match the configured filters.
        /// </summary>
        /// <param name="method">The method to evaluate.</param>
        /// <param name="definition">The sink definition to compare against.</param>
        private static bool MatchesNativeImport(MethodRecord method, SinkDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(definition.NativeModule) &&
                string.IsNullOrWhiteSpace(definition.NativeEntryPoint))
            {
                return true;
            }

            if (!method.IsPInvoke)
            {
                return false;
            }

            return MatchesNativeModuleName(method.ImportedModuleName, definition.NativeModule) &&
                   MatchesNativeEntryPointName(method.ImportedEntryPointName, definition.NativeEntryPoint);
        }

        /// <summary>
        /// Formats a sink definition for display.
        /// </summary>
        /// <param name="sinkDefinition">The sink definition to format.</param>
        private static string FormatSink(SinkDefinition sinkDefinition)
        {
            string methodName = GetConfiguredMethodNamePrefix(sinkDefinition.MethodName);
            string managedPart = FormatManagedSink(sinkDefinition, methodName);
            string nativePart = FormatNativeSink(sinkDefinition);

            if (string.IsNullOrWhiteSpace(nativePart))
            {
                return managedPart;
            }

            return string.IsNullOrWhiteSpace(managedPart)
                ? nativePart
                : $"{managedPart} [native:{nativePart}]";
        }

        /// <summary>
        /// Determines whether the configured method name uses prefix matching.
        /// </summary>
        /// <param name="configuredMethodName">The configured method name.</param>
        private static bool IsWildcardMethodName(string configuredMethodName)
            => !string.IsNullOrWhiteSpace(configuredMethodName) &&
               configuredMethodName.EndsWith('*');

        /// <summary>
        /// Gets the configured method-name prefix without wildcard syntax.
        /// </summary>
        /// <param name="configuredMethodName">The configured method name.</param>
        private static string GetConfiguredMethodNamePrefix(string configuredMethodName)
            => IsWildcardMethodName(configuredMethodName)
                ? configuredMethodName[..^1]
                : configuredMethodName;

        /// <summary>
        /// Formats the managed portion of a sink definition for display.
        /// </summary>
        /// <param name="sinkDefinition">The sink definition to format.</param>
        /// <param name="methodName">The normalized configured method name.</param>
        private static string FormatManagedSink(SinkDefinition sinkDefinition, string methodName)
        {
            if (string.IsNullOrWhiteSpace(sinkDefinition.DeclaringType) &&
                string.IsNullOrWhiteSpace(sinkDefinition.MethodName))
            {
                return string.Empty;
            }

            if (sinkDefinition.HasExplicitParameterSignature)
            {
                string formattedParameters = string.Join(", ", sinkDefinition.ParameterTypeNames);
                string declaringType = string.IsNullOrWhiteSpace(sinkDefinition.DeclaringType) ? "*" : sinkDefinition.DeclaringType;
                string formattedMethodName = string.IsNullOrWhiteSpace(methodName) ? "*" : methodName;
                return $"{declaringType}::{formattedMethodName}({formattedParameters})";
            }

            return string.IsNullOrWhiteSpace(sinkDefinition.DeclaringType)
                ? $"*::{sinkDefinition.MethodName}"
                : string.IsNullOrWhiteSpace(methodName)
                    ? sinkDefinition.DeclaringType
                    : IsWildcardMethodName(sinkDefinition.MethodName)
                        ? $"{sinkDefinition.DeclaringType}::{methodName}*"
                        : $"{sinkDefinition.DeclaringType}::{methodName}";
        }

        /// <summary>
        /// Formats the native-import portion of a sink definition for display.
        /// </summary>
        /// <param name="sinkDefinition">The sink definition to format.</param>
        private static string FormatNativeSink(SinkDefinition sinkDefinition)
        {
            if (string.IsNullOrWhiteSpace(sinkDefinition.NativeModule) &&
                string.IsNullOrWhiteSpace(sinkDefinition.NativeEntryPoint))
            {
                return string.Empty;
            }

            string nativeModule = string.IsNullOrWhiteSpace(sinkDefinition.NativeModule) ? "*" : sinkDefinition.NativeModule;
            string nativeEntryPoint = string.IsNullOrWhiteSpace(sinkDefinition.NativeEntryPoint) ? "*" : sinkDefinition.NativeEntryPoint;
            return $"{nativeModule}!{nativeEntryPoint}";
        }

        /// <summary>
        /// Determines whether an imported native module name matches the configured filter.
        /// </summary>
        /// <param name="actualModuleName">The normalized imported module name.</param>
        /// <param name="configuredModuleName">The configured native module filter.</param>
        private static bool MatchesNativeModuleName(string? actualModuleName, string configuredModuleName)
        {
            if (string.IsNullOrWhiteSpace(configuredModuleName))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(actualModuleName) &&
                   string.Equals(actualModuleName, AnalysisIndex.NormalizeNativeModuleName(configuredModuleName), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether an imported native entry point matches the configured filter.
        /// </summary>
        /// <param name="actualEntryPointName">The imported native entry point.</param>
        /// <param name="configuredEntryPointName">The configured native entry-point filter.</param>
        private static bool MatchesNativeEntryPointName(string? actualEntryPointName, string configuredEntryPointName)
        {
            if (string.IsNullOrWhiteSpace(configuredEntryPointName))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(actualEntryPointName) &&
                   MatchesConfiguredName(actualEntryPointName, configuredEntryPointName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Matches a configured exact or wildcard-prefixed name against an actual value.
        /// </summary>
        /// <param name="actualName">The actual name.</param>
        /// <param name="configuredName">The configured name filter.</param>
        /// <param name="comparison">The comparison mode.</param>
        private static bool MatchesConfiguredName(string actualName, string configuredName, StringComparison comparison)
        {
            if (IsWildcardMethodName(configuredName))
            {
                string configuredNamePrefix = GetConfiguredMethodNamePrefix(configuredName);
                return actualName.StartsWith(configuredNamePrefix, comparison);
            }

            return string.Equals(actualName, configuredName, comparison);
        }

        /// <summary>
        /// Represents an exact managed sink lookup key.
        /// </summary>
        /// <param name="DeclaringType">The configured declaring type.</param>
        /// <param name="MethodName">The configured method name.</param>
        private readonly record struct SinkMethodLookupKey(string DeclaringType, string MethodName);

    }
}

