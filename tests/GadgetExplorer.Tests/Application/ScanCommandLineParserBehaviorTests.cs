/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Application
{
    public sealed class ScanCommandLineParserBehaviorTests
    {
        private const string ExampleInputDirectory = "example-managed";
        private const string ExampleInputAssembly = "example-app.dll";
        private const string ExampleProfilePath = "./profiles/custom.profile.json";
        private const string ExampleSinksFilePath = "./custom-include.sinks.json";
        private const string ExampleSinksDirectoryPath = "./custom-sinks";
        private const string ExampleIgnoreSinksFilePath = "./custom-ignore.ignore-sinks.json";
        private const string ExampleIgnoreSinksDirectoryPath = "./custom-ignore-sinks";

        [Fact]
        public void Parses_assembly_resolution_mode_without_treating_it_as_an_input_path()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputDirectory,
                    "--profile",
                    "JsonDotNet",
                    "--assembly-resolution-mode",
                    "restricted",
                    "--interface-expansion",
                    "off",
                    "--sort",
                    "type-name",
                    "--output",
                    "scan.txt"
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal([ExampleInputDirectory], options.AssemblyInputs);
            Assert.Equal("JsonDotNet", options.ProfileName);
            Assert.Null(options.ProfileFilePath);
            Assert.Equal(AssemblyResolutionMode.Restricted, options.AssemblyResolutionMode);
            Assert.Equal(InterfaceExpansionMode.Off, options.InterfaceExpansionMode);
            Assert.Equal(FindingSortMode.TypeName, options.SortMode);
            Assert.Null(options.MaxPathLength);
            Assert.Equal("scan.txt", options.OutputPath);
        }

        [Fact]
        public void Parses_inference_no_fallback_assembly_resolution_mode()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputAssembly,
                    "--profile",
                    "JsonDotNet",
                    "--assembly-resolution-mode",
                    "inference-no-fallback"
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(AssemblyResolutionMode.InferenceNoFallback, options.AssemblyResolutionMode);
        }

        [Fact]
        public void Parses_interface_expansion_mode()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputAssembly,
                    "--profile",
                    "JsonDotNet",
                    "--interface-expansion",
                    "broad"
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(InterfaceExpansionMode.Broad, options.InterfaceExpansionMode);
        }

        [Fact]
        public void Parses_output_format_mode()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputAssembly,
                    "--profile",
                    "JsonDotNet",
                    "--output-format",
                    "json"
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(ScanOutputFormat.Json, options.OutputFormat);
        }

        [Fact]
        public void Parses_independent_sink_configuration_flags()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputDirectory,
                    "--profile",
                    "JsonDotNet",
                    "--sinks",
                    ExampleSinksFilePath,
                    "--ignore-sinks",
                    ExampleIgnoreSinksFilePath
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(ExampleSinksFilePath, options.IncludeSinkConfigPath);
            Assert.Equal(ExampleIgnoreSinksFilePath, options.IgnoreSinkConfigPath);
        }

        [Fact]
        public void Parses_sink_directory_path()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputDirectory,
                    "--profile",
                    "JsonDotNet",
                    "--sinks",
                    ExampleSinksDirectoryPath
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(ExampleSinksDirectoryPath, options.IncludeSinkConfigPath);
        }

        [Fact]
        public void Parses_ignore_sink_directory_path()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputDirectory,
                    "--profile",
                    "JsonDotNet",
                    "--ignore-sinks",
                    ExampleIgnoreSinksDirectoryPath
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(ExampleIgnoreSinksDirectoryPath, options.IgnoreSinkConfigPath);
        }

        [Fact]
        public void Parses_max_path_length_option()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputAssembly,
                    "--profile",
                    "JsonDotNet",
                    "--max-path-length",
                    "12"
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(12, options.MaxPathLength);
        }

        [Fact]
        public void Parses_native_aliases_for_supported_options()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputDirectory,
                    "-p",
                    "JsonDotNet",
                    "-arm",
                    "inference-with-fallback",
                    "-ie",
                    "off",
                    "-s",
                    "type-name",
                    "-o",
                    "scan.txt",
                    "-of",
                    "json",
                    "-mpl",
                    "9",
                    "-is",
                    ExampleSinksFilePath,
                    "-ig",
                    ExampleIgnoreSinksFilePath
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal([ExampleInputDirectory], options.AssemblyInputs);
            Assert.Equal("JsonDotNet", options.ProfileName);
            Assert.Null(options.ProfileFilePath);
            Assert.Equal(AssemblyResolutionMode.InferenceWithFallback, options.AssemblyResolutionMode);
            Assert.Equal(InterfaceExpansionMode.Off, options.InterfaceExpansionMode);
            Assert.Equal(FindingSortMode.TypeName, options.SortMode);
            Assert.Equal(9, options.MaxPathLength);
            Assert.Equal("scan.txt", options.OutputPath);
            Assert.Equal(ScanOutputFormat.Json, options.OutputFormat);
            Assert.Equal(ExampleSinksFilePath, options.IncludeSinkConfigPath);
            Assert.Equal(ExampleIgnoreSinksFilePath, options.IgnoreSinkConfigPath);
        }

        [Fact]
        public void Accepts_hidden_include_sinks_alias_for_backwards_compatibility()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputAssembly,
                    "--profile",
                    "JsonDotNet",
                    "--include-sinks",
                    ExampleSinksFilePath
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(ExampleSinksFilePath, options.IncludeSinkConfigPath);
        }

        [Fact]
        public void Accepts_profile_file_alias()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "-pf", ExampleProfilePath],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Null(options.ProfileName);
            Assert.Equal(ExampleProfilePath, options.ProfileFilePath);
        }
    }
}

