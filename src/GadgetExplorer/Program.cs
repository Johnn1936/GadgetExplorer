/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

if (args.Length == 0 || ScanCommandLineParser.IsHelpRequest(args))
{
    Console.WriteLine(ScanCommandLineParser.GetUsage());
    return 0;
}

if (!ScanCommandLineParser.TryParse(args, out ScanCommandOptions? options, out string? validationError))
{
    if (!string.IsNullOrWhiteSpace(validationError))
    {
        Console.Error.WriteLine(validationError);
    }

    return 1;
}

options = options! with
{
    CommandLineArguments = [.. args]
};

try
{
    Action<string> progress = ConsoleProgressReporter.Create();
    progress($"Command line args: {options.CommandLineArgumentsLabel}");
    progress($"Inputs: {string.Join(", ", options.AssemblyInputs.Select(Path.GetFullPath))}");
    progress(options.ProfileFilePath is not null
        ? $"Serializer profile file: {options.ResolvedProfileFilePath}"
        : $"Serializer profile: {options.ProfileName}");

    progress($"Finding sort: {options.SortModeDisplayText}");
    progress($"Dispatch mode: {options.InterfaceExpansionModeDisplayText}");
    progress($"Max path length: {options.MaxPathLengthLabel}");
    progress($"Assembly resolution mode: {options.AssemblyResolutionModeDisplayText}");
    progress($"Output format: {options.OutputFormatDisplayText}");
    progress($"Output file: {options.ResolvedOutputPath ?? "<stdout>"}");

    ScanExecutionResult execution = ScanRunner.Execute(options, progress);
    if (execution.Options.ResolvedOutputPath is not null)
    {
        using var fileWriter = new StreamWriter(execution.Options.ResolvedOutputPath);
        if (execution.Options.OutputFormat == ScanOutputFormat.Json)
        {
            ScanJsonReportWriter.Write(fileWriter, execution);
        }
        else
        {
            ScanReportWriter.Write(fileWriter, execution);
        }

        fileWriter.Flush();
        Console.WriteLine($"Scan written to: {execution.Options.ResolvedOutputPath}");
    }
    else
    {
        if (execution.Options.OutputFormat == ScanOutputFormat.Json)
        {
            ScanJsonReportWriter.Write(Console.Out, execution);
        }
        else
        {
            ScanReportWriter.Write(Console.Out, execution);
        }
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
