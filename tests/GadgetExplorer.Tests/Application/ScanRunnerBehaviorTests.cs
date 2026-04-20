/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Application
{
    public sealed class ScanRunnerBehaviorTests
    {
        [Fact]
        public void Execute_propagates_sink_configuration_failures()
        {
            using var tempDirectory = new TemporaryDirectoryScope();
            using var currentDirectory = new CurrentDirectoryScope(tempDirectory.Path);
            var missingSinkConfigPath = tempDirectory.GetPath("missing-sinks.json");
            Assert.False(File.Exists(missingSinkConfigPath));
            var options = new ScanCommandOptions(
                [System.IO.Path.GetFullPath(typeof(MySpecialObject).Assembly.Location)],
                missingSinkConfigPath,
                null,
                FindingSortMode.ShortestPath,
                InterfaceExpansionMode.Strict,
                null,
                null,
                "JsonDotNet",
                null,
                AssemblyResolutionMode.Restricted);
            var progressMessages = new List<string>();

            var ex = Assert.Throws<InvalidOperationException>(() => ScanRunner.Execute(options, progressMessages.Add));

            Assert.Contains("Sink config file or directory was not found", ex.Message, StringComparison.Ordinal);
            Assert.Single(progressMessages);
            Assert.StartsWith("Loading sink configuration from ", progressMessages[0], StringComparison.Ordinal);
        }
    }

}
