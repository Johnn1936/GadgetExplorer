/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Text.Json;

namespace GadgetExplorer.Application.Reporting
{
    /// <summary>
    /// Renders scan execution results into the structured JSON report format.
    /// </summary>
    public static class ScanJsonReportWriter
    {
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>
        /// Writes the final structured JSON report to a supplied writer.
        /// </summary>
        /// <param name="writer">The destination writer.</param>
        /// <param name="result">The scan execution result.</param>
        public static void Write(TextWriter writer, ScanExecutionResult result)
        {
            ArgumentNullException.ThrowIfNull(writer);

            ScanReportDocument document = ScanReportDocumentProjector.Project(result);
            writer.Write(JsonSerializer.Serialize(document, s_jsonSerializerOptions));
        }
    }
}
