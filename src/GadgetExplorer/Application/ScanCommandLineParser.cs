/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.CommandLine;
using System.CommandLine.Parsing;

namespace GadgetExplorer.Application
{
    /// <summary>
    /// Parses command-line arguments for the scan command.
    /// </summary>
    public static class ScanCommandLineParser
    {
        private const string MaxPathLengthValidationMessage = "Unsupported value for --max-path-length. Use a non-negative integer.";
        private const string LegacyIncludeSinksAlias = "--include-sinks";

        private static readonly string[] s_profileAliases = ["-p", "--profile"];
        private static readonly string[] s_profileFileAliases = ["-pf", "--profile-file"];
        private static readonly string[] s_includeSinksAliases = ["-is", "--sinks"];
        private static readonly string[] s_ignoreSinksAliases = ["-ig", "--ignore-sinks"];
        private static readonly string[] s_interfaceExpansionAliases = ["-ie", "--interface-expansion"];
        private static readonly string[] s_sortAliases = ["-s", "--sort"];
        private static readonly string[] s_maxPathLengthAliases = ["-mpl", "--max-path-length"];
        private static readonly string[] s_assemblyResolutionModeAliases = ["-arm", "--assembly-resolution-mode"];
        private static readonly string[] s_outputAliases = ["-o", "--output"];
        private static readonly string[] s_outputFormatAliases = ["-of", "--output-format"];
        private static readonly string s_usageText =
"""
                                                                                        
 ▄████   ▄▄▄  ▄▄▄▄   ▄▄▄▄ ▄▄▄▄▄ ▄▄▄▄▄▄ ██████ ▄▄ ▄▄ ▄▄▄▄  ▄▄     ▄▄▄  ▄▄▄▄  ▄▄▄▄▄ ▄▄▄▄  
██  ▄▄▄ ██▀██ ██▀██ ██ ▄▄ ██▄▄    ██   ██▄▄   ▀█▄█▀ ██▄█▀ ██    ██▀██ ██▄█▄ ██▄▄  ██▄█▄ 
 ▀███▀  ██▀██ ████▀ ▀███▀ ██▄▄▄   ██   ██▄▄▄▄ ██ ██ ██    ██▄▄▄ ▀███▀ ██ ██ ██▄▄▄ ██ ██ 
                                                                                        
                                             .NET Deserialization Gadget Discovery Tool

Usage:

    GadgetExplorer <assembly-or-directory> [options]

Input:

    <assembly-or-directory>
        Assembly file or directory tree to scan.
        Directories are searched recursively for managed assemblies and runtimeconfig files.
        Example: GadgetExplorer "C:\Target\App" -p JsonDotNet
        Note: At least one assembly or directory is required.

Profile:

    -p, --profile
        Values: [BinaryFormatter | JsonDotNet | JsonDotNetGetters | MessagePackTypeless |
            PublicTwoStringConstructor | XmlSerializer]
        Use a shipped serializer profile.
        This controls which deserialization trigger policy and activation policies are modeled.
        Required unless --profile-file is used.
        Example: -p JsonDotNet

    -pf, --profile-file <path>
        Use a custom serializer profile JSON file.
        This replaces built-in profile selection for the scan.
        Required unless --profile is used.
        Example: -pf .\Profiles\Custom.profile.json

Sink Configuration:

    -is, --sinks <path>
        Load a custom sink JSON file or a directory of *.sinks.json files.
        This changes which methods count as reportable sinks.
        Default: use the shipped `sinks` directory beside the executable.
        Example: -is .\CustomSinks.json

    -ig, --ignore-sinks <path>
        Load a custom ignore-sink JSON file or a directory of *.ignore-sinks.json files.
        This suppresses configured sink patterns and can reduce noise.
        Default: use the shipped `ignore-sinks` directory beside the executable.
        Example: -ig .\CustomIgnoreSinks.json

Scan Behavior:

    -ie, --interface-expansion
        Values: [off | strict | broad]
        Control dynamic dispatch handling during graph construction.
        off: only follow interface calls when concrete receiver identity is already known.
        strict: allow strong receiver evidence, but stop when evidence runs out.
        broad: opt into heuristic fallback across compatible implementations.
        Default: strict.
        Example: -ie broad

    -s, --sort
        Values: [shortest-path | per-sink-shortest-path | type-name]
        Control finding order in the final report.
        shortest-path: shortest paths first globally.
        per-sink-shortest-path: group by sink, then shortest paths first within each sink.
        type-name: stable type-centric ordering by root class identity.
        Default: shortest-path.
        Example: -s per-sink-shortest-path

    -mpl, --max-path-length <n>
        Limit the maximum graph path length from trigger to sink.
        Lower values reduce noise and runtime but hide longer gadget chains.
        Default: unbounded.
        Example: -mpl 8

    -arm, --assembly-resolution-mode
        Values: [restricted | inference-no-fallback | inference-with-fallback]
        Control how assembly resolution expands beyond the supplied input roots.
        restricted: only resolve assemblies inside the supplied directory tree or beside supplied
            assembly files.
        inference-no-fallback: infer the target runtime from runtimeconfig files, but stay inside
            the inputs if inference fails.
        inference-with-fallback: infer the target runtime first, then fall back to the host
            runtime if inference fails.
        Default: inference-no-fallback.

Output:

    -o, --output <path>
        Write the final report to a file. Progress still goes to the console; only the report is 
        redirected. The output path does not choose the format; use --output-format for that.
        Default: write the report to stdout.
        Example: -o .\Scan.txt

    -of, --output-format
        Values: [text | json]
        Control the final report serialization format.
        text: the existing human-readable report.
        json: structured JSON output is intended for downstream tooling. It keeps the same rendered
            finding order as the text report, but emits the scan heading and each finding as 
            typed fields that are easier to filter, sort, and group.
        Default: text.
        Example: -of json

Examples:

[*] Scan a single assembly with the built-in Json.NET profile:

    .\GadgetExplorer.exe .\App.dll --profile JsonDotNet --output .\Scan-JsonDotNet.txt

[*] Scan a directory tree with the built-in BinaryFormatter profile usinga max path length 
    of 12:

    .\GadgetExplorer.exe "C:\Target\App" -p BinaryFormatter -mpl 12 -o .\Scan-BinaryFormatter.txt

[*] Scan a directory tree with the built-in XmlSerializer profile:

    .\GadgetExplorer.exe "C:\Target\App" -p XmlSerializer -o .\Scan-XmlSerializer.txt

[*] Scan a directory tree with the built-in MessagePack Typeless profile and a max path length 
    of 8:

    .\GadgetExplorer.exe "C:\Target\App" --profile MessagePackTypeless --max-path-length 8
        --output .\Scan-MessagePackTypeless.txt

[*] Use the built-in Json.NET profile with a custom sink file, a custom ignore-sink file, and
    `type-name` sorting:

    .\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -is .\CustomIncludeSinks.json
        -ig .\CustomIgnoreSinks.json -s type-name -o .\Scan-CustomSinks-TypeName.txt

[*] Use the built-in Json.NET profile with a custom sink directory:

    .\GadgetExplorer.exe "C:\Target\App" --profile JsonDotNet --sinks .\CustomSinks
        --output .\Scan-CustomSinkDirectory.txt

[*] Write the structured JSON report to disk for downstream processing:

    .\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -of json -o .\Scan-JsonDotNet.json

[*] Infer the target runtime from runtimeconfig files, but stay inside the supplied inputs if
    inference fails:

    .\GadgetExplorer.exe "C:\Target\App" --profile JsonDotNet
        --assembly-resolution-mode inference-no-fallback --output .\Scan-InferenceNoFallback.txt

[*] Infer the target runtime first, then allow host-runtime fallback if inference fails:

    .\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -arm inference-with-fallback -o
        .\Scan-InferenceWithFallback.txt

[*] Use a built-in profile with explicit interface handling, sorting, and max path length:

    .\GadgetExplorer.exe .\App.dll -p JsonDotNet -arm restricted -ie broad -s shortest-path
        -mpl 8 -o .\Scan-Restricted-Broad.txt

[*] Use a custom serializer profile file, a custom sink file, custom ignore sinks, sort by
    per-sink shortest path, and cap paths at 12:
  
    .\GadgetExplorer.exe "C:\Target\App" -pf .\Profiles\Custom.profile.json
        -is .\CustomIncludeSinks.json -ig .\CustomIgnoreSinks.json
        -s per-sink-shortest-path -mpl 12 -o .\Scan-Custom.txt
""";

        private static readonly HashSet<string> s_helpRequestAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "-h",
            "--help",
            "/?",
            "/h",
            "-?"
        };

        private static readonly HashSet<string> s_trailingValueOptions = new(
            s_profileAliases
                .Concat(s_profileFileAliases)
                .Concat(s_includeSinksAliases)
                .Concat(s_ignoreSinksAliases)
                .Concat(s_interfaceExpansionAliases)
                .Concat(s_sortAliases)
                .Concat(s_maxPathLengthAliases)
                .Concat(s_assemblyResolutionModeAliases)
                .Concat(s_outputAliases)
                .Concat(s_outputFormatAliases),
            StringComparer.OrdinalIgnoreCase);

        private static readonly Lazy<ScanCommandDefinition> s_scanCommand = new(CreateCommandDefinition);

        /// <summary>
        /// Attempts to parse the supplied command-line arguments.
        /// </summary>
        /// <param name="args">The raw command-line arguments.</param>
        /// <param name="options">The parsed command options.</param>
        /// <param name="validationError">The validation error, when parsing fails.</param>
        public static bool TryParse(string[] args, out ScanCommandOptions? options, out string? validationError)
        {
            options = null;
            validationError = null;

            string[] normalizedArgs = NormalizeLegacyAliases(args);

            if (normalizedArgs.Length > 0 && s_trailingValueOptions.Contains(normalizedArgs[^1]))
            {
                validationError = $"Missing value for {normalizedArgs[^1]}.";
                return false;
            }

            ScanCommandDefinition command = s_scanCommand.Value;
            ParseResult parseResult = command.Command.Parse(normalizedArgs);
            if (parseResult.Errors.Count > 0)
            {
                validationError = FormatParseError(normalizedArgs, parseResult.Errors);
                return false;
            }

            string[] assemblyInputs = (parseResult.GetValue(command.AssemblyInputsArgument) ?? [])
                .Where(input => !string.IsNullOrWhiteSpace(input))
                .ToArray();

            if (assemblyInputs.Length == 0)
            {
                validationError = "At least one assembly or directory input is required.";
                return false;
            }

            string? profileName = parseResult.GetValue(command.ProfileOption);
            string? profileFilePath = parseResult.GetValue(command.ProfileFileOption);

            if (string.IsNullOrWhiteSpace(profileName) == string.IsNullOrWhiteSpace(profileFilePath))
            {
                validationError = string.IsNullOrWhiteSpace(profileName)
                    ? $"A serializer profile is required. Use {FormatAliasUsage(s_profileAliases)} <built-in-name> or {FormatAliasUsage(s_profileFileAliases)} <path>. Available shipped profiles: {GetAvailableProfileNamesDisplay()}."
                    : $"Specify either {FormatAliasUsage(s_profileAliases)} <built-in-name> or {FormatAliasUsage(s_profileFileAliases)} <path>, but not both.";
                return false;
            }

            string? maxPathLengthValue = parseResult.GetValue(command.MaxPathLengthOption);
            int? maxPathLength = null;
            if (!string.IsNullOrWhiteSpace(maxPathLengthValue))
            {
                if (!int.TryParse(maxPathLengthValue, out int parsedMaxPathLength) || parsedMaxPathLength < 0)
                {
                    validationError = MaxPathLengthValidationMessage;
                    return false;
                }

                maxPathLength = parsedMaxPathLength;
            }

            string sortValue = parseResult.GetValue(command.SortOption) ?? ScanOptionValues.Format(FindingSortMode.ShortestPath);
            if (!ScanOptionValues.TryParseSortMode(sortValue, out FindingSortMode sortMode))
            {
                validationError = $"Unsupported sort mode '{sortValue}'. Use {ScanOptionValues.GetSortModeChoiceList()}.";
                return false;
            }

            string interfaceExpansionValue = parseResult.GetValue(command.InterfaceExpansionOption) ?? ScanOptionValues.Format(InterfaceExpansionMode.Strict);
            if (!ScanOptionValues.TryParseInterfaceExpansionMode(interfaceExpansionValue, out InterfaceExpansionMode interfaceExpansionMode))
            {
                validationError = $"Unsupported interface expansion mode '{interfaceExpansionValue}'. Use {ScanOptionValues.GetInterfaceExpansionChoiceList()}.";
                return false;
            }

            string outputFormatValue = parseResult.GetValue(command.OutputFormatOption) ?? ScanOptionValues.Format(ScanOutputFormat.Text);
            if (!ScanOptionValues.TryParseOutputFormat(outputFormatValue, out ScanOutputFormat outputFormat))
            {
                validationError = $"Unsupported output format '{outputFormatValue}'. Use {ScanOptionValues.GetOutputFormatChoiceList()}.";
                return false;
            }

            string assemblyResolutionModeValue = parseResult.GetValue(command.AssemblyResolutionModeOption) ?? ScanOptionValues.Format(AssemblyResolutionMode.InferenceNoFallback);
            if (!ScanOptionValues.TryParseAssemblyResolutionMode(assemblyResolutionModeValue, out AssemblyResolutionMode assemblyResolutionMode))
            {
                validationError = $"Unsupported assembly resolution mode '{assemblyResolutionModeValue}'. Use {ScanOptionValues.GetAssemblyResolutionModeChoiceList()}.";
                return false;
            }

            options = new ScanCommandOptions(
                assemblyInputs,
                parseResult.GetValue(command.IncludeSinksOption),
                parseResult.GetValue(command.IgnoreSinksOption),
                sortMode,
                interfaceExpansionMode,
                maxPathLength,
                parseResult.GetValue(command.OutputOption),
                profileName,
                profileFilePath,
                assemblyResolutionMode,
                outputFormat);
            return true;
        }

        /// <summary>
        /// Determines whether the supplied arguments represent an explicit help request.
        /// </summary>
        public static bool IsHelpRequest(IReadOnlyList<string> args)
            => args.Any(argument => s_helpRequestAliases.Contains(argument));

        /// <summary>
        /// Gets the usage text for the command.
        /// </summary>
        public static string GetUsage() => s_usageText;

        private static ScanCommandDefinition CreateCommandDefinition()
        {
            var assemblyInputsArgument = new Argument<string[]>("assembly-or-directory")
            {
                Arity = ArgumentArity.ZeroOrMore,
                Description = "Assembly file or directory tree to scan. Directories are searched recursively for managed assemblies and runtimeconfig files."
            };

            Option<string?> profileOption = CreateStringOption(
                s_profileAliases,
                $"Use a shipped serializer profile. Available shipped profiles: {GetAvailableProfileNamesDisplay()}.");
            profileOption.HelpName = "name";

            Option<string?> profileFileOption = CreateStringOption(
                s_profileFileAliases,
                "Use a custom serializer profile JSON file instead of a shipped profile.");
            profileFileOption.HelpName = "path";

            Option<string?> includeSinksOption = CreateStringOption(
                s_includeSinksAliases,
                "Load a custom sink JSON file or a directory of *.sinks.json files. Default: the shipped sinks directory beside the executable.");
            includeSinksOption.HelpName = "path";

            Option<string?> ignoreSinksOption = CreateStringOption(
                s_ignoreSinksAliases,
                "Load a custom ignore-sink JSON file or a directory of *.ignore-sinks.json files. Default: the shipped ignore-sinks directory beside the executable.");
            ignoreSinksOption.HelpName = "path";

            Option<string?> interfaceExpansionOption = CreateStringOption(
                s_interfaceExpansionAliases,
                $"Control dynamic dispatch handling during graph construction. Values: {ScanOptionValues.GetInterfaceExpansionPipeList()}. Default: {ScanOptionValues.Format(InterfaceExpansionMode.Strict)}.");
            interfaceExpansionOption.HelpName = "mode";
            interfaceExpansionOption.DefaultValueFactory = _ => ScanOptionValues.Format(InterfaceExpansionMode.Strict);

            Option<string?> sortOption = CreateStringOption(
                s_sortAliases,
                $"Control finding order in the final report. Values: {ScanOptionValues.GetSortModePipeList()}. Default: {ScanOptionValues.Format(FindingSortMode.ShortestPath)}.");
            sortOption.HelpName = "mode";
            sortOption.DefaultValueFactory = _ => ScanOptionValues.Format(FindingSortMode.ShortestPath);

            Option<string?> maxPathLengthOption = CreateStringOption(
                s_maxPathLengthAliases,
                "Limit the maximum graph path length from trigger to sink. Default: unbounded.");
            maxPathLengthOption.HelpName = "n";

            Option<string?> assemblyResolutionModeOption = CreateStringOption(
                s_assemblyResolutionModeAliases,
                $"Control how assembly resolution expands beyond the supplied inputs. Values: {ScanOptionValues.GetAssemblyResolutionModePipeList()}. Default: {ScanOptionValues.Format(AssemblyResolutionMode.InferenceNoFallback)}.");
            assemblyResolutionModeOption.HelpName = "mode";
            assemblyResolutionModeOption.DefaultValueFactory = _ => ScanOptionValues.Format(AssemblyResolutionMode.InferenceNoFallback);

            Option<string?> outputOption = CreateStringOption(
                s_outputAliases,
                "Write the final report to a file. Default: write the report to stdout.");
            outputOption.HelpName = "path";

            Option<string?> outputFormatOption = CreateStringOption(
                s_outputFormatAliases,
                $"Control the final report serialization format. Values: {ScanOptionValues.GetOutputFormatPipeList()}. Default: {ScanOptionValues.Format(ScanOutputFormat.Text)}.");
            outputFormatOption.HelpName = "format";
            outputFormatOption.DefaultValueFactory = _ => ScanOptionValues.Format(ScanOutputFormat.Text);

            var command = new RootCommand("GadgetExplorer - .NET Deserialization Gadget Discovery Tool")
            {
                assemblyInputsArgument,
                profileOption,
                profileFileOption,
                includeSinksOption,
                ignoreSinksOption,
                interfaceExpansionOption,
                sortOption,
                maxPathLengthOption,
                assemblyResolutionModeOption,
                outputOption,
                outputFormatOption
            };

            return new ScanCommandDefinition(
                command,
                assemblyInputsArgument,
                profileOption,
                profileFileOption,
                includeSinksOption,
                ignoreSinksOption,
                interfaceExpansionOption,
                sortOption,
                maxPathLengthOption,
                assemblyResolutionModeOption,
                outputOption,
                outputFormatOption);
        }

        private static Option<string?> CreateStringOption(string[] aliases, string description)
            => new(aliases[1], aliases[0])
            {
                Description = description
            };

        private static string[] NormalizeLegacyAliases(IReadOnlyList<string> args)
            => [.. args.Select(argument =>
                string.Equals(argument, LegacyIncludeSinksAlias, StringComparison.OrdinalIgnoreCase)
                    ? s_includeSinksAliases[1]
                    : argument)];

        private static string FormatAliasUsage(string[] aliases)
            => $"{aliases[1]} ({aliases[0]})";

        private static string FormatParseError(IReadOnlyList<string> args, IReadOnlyList<ParseError> parseErrors)
        {
            if (parseErrors.Count == 0)
            {
                return "Unable to parse command-line arguments.";
            }

            if (ContainsAnyAlias(args, s_maxPathLengthAliases))
            {
                return MaxPathLengthValidationMessage;
            }

            return parseErrors[0].Message;
        }

        private static bool ContainsAnyAlias(IEnumerable<string> args, IReadOnlyCollection<string> aliases)
            => args.Any(aliases.Contains);

        private static string GetAvailableProfileNamesDisplay()
        {
            try
            {
                IReadOnlyList<string> availableProfiles = SerializerProfiles.GetAvailableProfileNames();
                return availableProfiles.Count == 0
                    ? "<none>"
                    : string.Join(", ", availableProfiles);
            }
            catch
            {
                return "<unavailable>";
            }
        }

        private sealed record ScanCommandDefinition(
            RootCommand Command,
            Argument<string[]> AssemblyInputsArgument,
            Option<string?> ProfileOption,
            Option<string?> ProfileFileOption,
            Option<string?> IncludeSinksOption,
            Option<string?> IgnoreSinksOption,
            Option<string?> InterfaceExpansionOption,
            Option<string?> SortOption,
            Option<string?> MaxPathLengthOption,
            Option<string?> AssemblyResolutionModeOption,
            Option<string?> OutputOption,
            Option<string?> OutputFormatOption);
    }
}
