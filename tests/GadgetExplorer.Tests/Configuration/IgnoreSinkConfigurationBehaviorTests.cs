/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Configuration
{
    public sealed class IgnoreSinkConfigurationBehaviorTests
    {
        [Fact]
        public void Missing_ignore_sink_file_returns_an_empty_list()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            string missingPath = tempDirectory.GetPath("missing-ignore-sinks.json");
            Assert.False(File.Exists(missingPath));

            IReadOnlyList<SinkDefinition> ignoreSinks = SinkConfigurations.Load(SinkConfigurationKind.Ignore, missingPath);

            Assert.Empty(ignoreSinks);
        }

        [Fact]
        public void Missing_ignore_sink_directory_returns_an_empty_list()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            string missingDirectoryPath = tempDirectory.GetPath("missing-ignore-sinks");
            Assert.False(Directory.Exists(missingDirectoryPath));

            IReadOnlyList<SinkDefinition> ignoreSinks = SinkConfigurations.Load(SinkConfigurationKind.Ignore, missingDirectoryPath);

            Assert.Empty(ignoreSinks);
        }

        [Fact]
        public void Explicit_ignore_sink_file_path_loads_sink_definitions()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            string configPath = tempDirectory.WriteFile(
                "ignore-sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "Helper",
                  "methodName": "InvokeSink"
                }
              ]
            }
            """);

            IReadOnlyList<SinkDefinition> ignoreSinks = SinkConfigurations.Load(SinkConfigurationKind.Ignore, configPath);

            SinkDefinition ignoreSink = Assert.Single(ignoreSinks);
            Assert.Equal("Helper", ignoreSink.DeclaringType);
            Assert.Equal("InvokeSink", ignoreSink.MethodName);
        }

        [Fact]
        public void Explicit_ignore_sink_directory_path_loads_matching_files_in_deterministic_order()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            tempDirectory.WriteFile(
                "b.ignore-sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "Helper",
                  "methodName": "Second"
                }
              ]
            }
            """);
            tempDirectory.WriteFile(
                "a.ignore-sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "Helper",
                  "methodName": "First"
                }
              ]
            }
            """);
            tempDirectory.WriteFile(
                Path.Combine("nested", "c.ignore-sinks.json"),
                """
            {
              "sinks": [
                {
                  "declaringType": "Helper",
                  "methodName": "Nested"
                }
              ]
            }
            """);

            IReadOnlyList<SinkDefinition> ignoreSinks = SinkConfigurations.Load(SinkConfigurationKind.Ignore, tempDirectory.Path);

            Assert.Collection(
                ignoreSinks,
                sink => Assert.Equal("First", sink.MethodName),
                sink => Assert.Equal("Second", sink.MethodName));
        }

        [Fact]
        public void Explicit_ignore_sink_path_preserves_broad_matching_when_parameters_are_omitted()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            string configPath = tempDirectory.WriteFile(
                "ignore-sinks.json",
                """
            {
              "sinks": [
                {
                  "declaringType": "System.IO.File",
                  "methodName": "Delete"
                }
              ]
            }
            """);

            SinkDefinition ignoreSink = Assert.Single(SinkConfigurations.Load(SinkConfigurationKind.Ignore, configPath));

            Assert.Equal("System.IO.File", ignoreSink.DeclaringType);
            Assert.Equal("Delete", ignoreSink.MethodName);
            Assert.False(ignoreSink.HasExplicitParameterSignature);
            Assert.Empty(ignoreSink.Parameters);
            Assert.Empty(ignoreSink.ParameterTypeNames);
        }
    }
}
