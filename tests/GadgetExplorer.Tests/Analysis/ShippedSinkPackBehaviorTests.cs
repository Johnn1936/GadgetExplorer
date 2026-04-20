/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Text.RegularExpressions;
using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    public sealed class ShippedSinkPackBehaviorTests
    {
        [Fact]
        public void Shipped_sink_and_ignore_packs_resolve_by_default_from_the_runtime_directory()
        {
            string includePath = SinkConfigurations.ResolvePath(SinkConfigurationKind.Include);
            string ignorePath = SinkConfigurations.ResolvePath(SinkConfigurationKind.Ignore);

            Assert.True(Directory.Exists(includePath), $"Expected shipped include sink directory at '{includePath}'.");
            Assert.True(Directory.Exists(ignorePath), $"Expected shipped ignore sink directory at '{ignorePath}'.");

            Assert.NotEmpty(SinkConfigurations.Load(SinkConfigurationKind.Include));
            Assert.NotEmpty(SinkConfigurations.Load(SinkConfigurationKind.Ignore));
        }

        [Fact]
        public void Shipped_include_sink_pack_keeps_the_expected_raw_and_collapsed_counts()
        {
            IReadOnlyList<SinkDefinition> includeSinks = SinkConfigurations.Load(SinkConfigurationKind.Include);

            Assert.Equal(538, includeSinks.Count);

            string[] collapsedFamilies = [.. includeSinks
                .Select(sink => string.IsNullOrWhiteSpace(sink.NativeEntryPoint)
                    ? $"{sink.DeclaringType}::{sink.MethodName}"
                    : $"{sink.NativeModule}!{sink.NativeEntryPoint}")
                .Distinct(StringComparer.Ordinal)];

            Assert.Equal(274, collapsedFamilies.Length);
        }

        [Fact]
        public void Shipped_include_sink_pack_uses_canonical_parameter_type_names()
        {
            string includePath = SinkConfigurations.ResolvePath(SinkConfigurationKind.Include);
            string[] includeFiles = [.. Directory.EnumerateFiles(includePath, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)];

            Assert.NotEmpty(includeFiles);

            foreach (string includeFile in includeFiles)
            {
                string json = File.ReadAllText(includeFile);
                Assert.DoesNotMatch(new Regex(@"\\u[0-9a-fA-F]{4}", RegexOptions.CultureInvariant), json);

                string[] nonCanonicalTypeNameLines = [.. File.ReadLines(includeFile)
                    .Where(line => line.Contains("\"typeName\"", StringComparison.Ordinal) &&
                                   Regex.IsMatch(line, @"[A-Za-z_][A-Za-z0-9_.+]*<", RegexOptions.CultureInvariant))];

                Assert.True(
                    nonCanonicalTypeNameLines.Length == 0,
                    $"Expected canonical parameter type names in '{Path.GetFileName(includeFile)}' but found:{Environment.NewLine}{string.Join(Environment.NewLine, nonCanonicalTypeNameLines)}");
            }

            IReadOnlyList<SinkDefinition> includeSinks = SinkConfigurations.Load(SinkConfigurationKind.Include);

            Assert.Contains(includeSinks, sink =>
                string.Equals(sink.DeclaringType, "System.IO.File", StringComparison.Ordinal) &&
                string.Equals(sink.MethodName, "AppendAllLines", StringComparison.Ordinal) &&
                sink.ParameterTypeNames.SequenceEqual(["System.String", "System.Collections.Generic.IEnumerable`1<System.String>"]));

            Assert.Contains(includeSinks, sink =>
                string.Equals(sink.DeclaringType, "System.Net.Sockets.Socket", StringComparison.Ordinal) &&
                string.Equals(sink.MethodName, "SendAsync", StringComparison.Ordinal) &&
                sink.ParameterTypeNames.SequenceEqual(["System.ArraySegment`1<System.Byte>", "System.Net.Sockets.SocketFlags", "System.Boolean"]));

            Assert.Contains(includeSinks, sink =>
                string.Equals(sink.DeclaringType, "System.Management.Automation.PowerShell", StringComparison.Ordinal) &&
                string.Equals(sink.MethodName, "Invoke", StringComparison.Ordinal) &&
                sink.ParameterTypeNames.SequenceEqual(["System.Collections.IEnumerable", "System.Collections.Generic.IList`1<T>"]));
        }
    }
}
