/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Application
{
    public sealed class ScanCommandLineParserValidationBehaviorTests
    {
        private const string ExampleInputDirectory = "example-managed";
        private const string ExampleInputAssembly = "example-app.dll";
        private const string ExampleInputAssemblyOne = "example-one.dll";
        private const string ExampleInputAssemblyTwo = "example-two.dll";
        private const string ExampleThirdInput = "example-three";
        private const string ExampleProfilePath = "./profiles/custom.profile.json";
        private const string ExampleSinksPathOne = "./custom-sinks-one";
        private const string ExampleSinksPathTwo = "./custom-sinks-two";
        private const string ExampleIgnoreSinksPathOne = "./custom-ignore-one";
        private const string ExampleIgnoreSinksPathTwo = "./custom-ignore-two";

        [Fact]
        public void Uses_default_values_for_minimal_valid_request()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "--profile", "JsonDotNet"],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal([ExampleInputAssembly], options.AssemblyInputs);
            Assert.Equal("JsonDotNet", options.ProfileName);
            Assert.Null(options.ProfileFilePath);
            Assert.Null(options.MaxPathLength);
            Assert.Equal(InterfaceExpansionMode.Strict, options.InterfaceExpansionMode);
            Assert.Equal(AssemblyResolutionMode.InferenceNoFallback, options.AssemblyResolutionMode);
            Assert.Equal(FindingSortMode.ShortestPath, options.SortMode);
            Assert.Equal(ScanOutputFormat.Text, options.OutputFormat);
            Assert.Null(options.OutputPath);
        }

        [Fact]
        public void Preserves_multiple_assembly_inputs_in_original_order()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputAssemblyOne,
                    ExampleInputAssemblyTwo,
                    ExampleThirdInput,
                    "--profile",
                    "JsonDotNet"
                ],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Equal(
                [ExampleInputAssemblyOne, ExampleInputAssemblyTwo, ExampleThirdInput],
                options.AssemblyInputs);
        }

        [Fact]
        public void Rejects_requests_without_assembly_inputs()
        {
            var parsed = ScanCommandLineParser.TryParse(
                ["--profile", "JsonDotNet"],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Equal("At least one assembly or directory input is required.", validationError);
        }

        [Fact]
        public void Rejects_requests_without_a_profile()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Contains("A serializer profile is required.", validationError, StringComparison.Ordinal);
            Assert.Contains("--profile", validationError, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("--profile")]
        [InlineData("--profile-file")]
        [InlineData("--sinks")]
        [InlineData("--ignore-sinks")]
        [InlineData("--sort")]
        [InlineData("--interface-expansion")]
        [InlineData("--max-path-length")]
        [InlineData("--assembly-resolution-mode")]
        [InlineData("--output")]
        [InlineData("--output-format")]
        [InlineData("-p")]
        [InlineData("-pf")]
        [InlineData("-is")]
        [InlineData("-ig")]
        [InlineData("-s")]
        [InlineData("-ie")]
        [InlineData("-mpl")]
        [InlineData("-arm")]
        [InlineData("-o")]
        [InlineData("-of")]
        public void Rejects_missing_option_values(string optionName)
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, optionName],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Equal($"Missing value for {optionName}.", validationError);
        }

        [Fact]
        public void Rejects_multiple_sink_paths()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputAssembly,
                    "--profile",
                    "JsonDotNet",
                    "--sinks",
                    ExampleSinksPathOne,
                    "--sinks",
                    ExampleSinksPathTwo
                ],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Contains("--sinks", validationError, StringComparison.Ordinal);
            Assert.Contains("expects a single argument", validationError, StringComparison.Ordinal);
        }

        [Fact]
        public void Rejects_multiple_ignore_sink_paths()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [
                    ExampleInputAssembly,
                    "--profile",
                    "JsonDotNet",
                    "--ignore-sinks",
                    ExampleIgnoreSinksPathOne,
                    "--ignore-sinks",
                    ExampleIgnoreSinksPathTwo
                ],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Contains("--ignore-sinks", validationError, StringComparison.Ordinal);
            Assert.Contains("expects a single argument", validationError, StringComparison.Ordinal);
        }

        [Fact]
        public void Accepts_explicit_profile_file_values()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "--profile-file", ExampleProfilePath],
                out var options,
                out var validationError);

            Assert.True(parsed);
            Assert.Null(validationError);
            Assert.NotNull(options);
            Assert.Null(options.ProfileName);
            Assert.Equal(ExampleProfilePath, options.ProfileFilePath);
        }

        [Fact]
        public void Rejects_requests_that_specify_both_profile_modes()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "--profile", "JsonDotNet", "--profile-file", ExampleProfilePath],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Equal("Specify either --profile (-p) <built-in-name> or --profile-file (-pf) <path>, but not both.", validationError);
        }

        [Fact]
        public void Rejects_unsupported_sort_values_without_throwing()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "--profile", "JsonDotNet", "--sort", "nonsense"],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Contains("Unsupported sort mode", validationError, StringComparison.Ordinal);
        }

        [Fact]
        public void Rejects_unsupported_interface_expansion_values_without_throwing()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "--profile", "JsonDotNet", "--interface-expansion", "exact"],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Contains("Unsupported interface expansion mode", validationError, StringComparison.Ordinal);
        }

        [Fact]
        public void Rejects_unsupported_output_format_values_without_throwing()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "--profile", "JsonDotNet", "--output-format", "yaml"],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Contains("Unsupported output format", validationError, StringComparison.Ordinal);
        }

        [Fact]
        public void Rejects_unsupported_assembly_resolution_mode_values_without_throwing()
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "--profile", "JsonDotNet", "--assembly-resolution-mode", "heuristic"],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Contains("Unsupported assembly resolution mode", validationError, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("abc")]
        public void Rejects_invalid_max_path_length_values(string value)
        {
            var parsed = ScanCommandLineParser.TryParse(
                [ExampleInputAssembly, "--profile", "JsonDotNet", "--max-path-length", value],
                out var options,
                out var validationError);

            Assert.False(parsed);
            Assert.Null(options);
            Assert.Equal("Unsupported value for --max-path-length. Use a non-negative integer.", validationError);
        }
    }

}
