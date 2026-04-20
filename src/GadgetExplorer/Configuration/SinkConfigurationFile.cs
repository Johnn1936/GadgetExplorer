/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Configuration
{
    /// <summary>
    /// Represents the sink configuration file payload.
    /// </summary>
    internal sealed class SinkConfigFile
    {
        /// <summary>
        /// Gets the configured sink entries.
        /// </summary>
        public List<SinkConfigEntry>? Sinks { get; init; }
    }

    /// <summary>
    /// Represents a single sink entry in the configuration file.
    /// </summary>
    internal sealed class SinkConfigEntry
    {
        /// <summary>
        /// Gets the optional declaring type filter.
        /// </summary>
        public string? DeclaringType { get; init; }

        /// <summary>
        /// Gets the optional method name filter.
        /// </summary>
        public string? MethodName { get; init; }

        /// <summary>
        /// Gets the optional imported native module filter for P/Invoke methods.
        /// </summary>
        public string? NativeModule { get; init; }

        /// <summary>
        /// Gets the optional imported native entry-point filter for P/Invoke methods.
        /// </summary>
        public string? NativeEntryPoint { get; init; }

        /// <summary>
        /// Gets the optional exact parameter type list for overload matching.
        /// </summary>
        public List<string>? ParameterTypeNames { get; init; }

        /// <summary>
        /// Gets the optional parameter definitions in the preferred configuration format.
        /// </summary>
        public List<SinkParameterEntry>? Parameters { get; init; }

    }

    /// <summary>
    /// Represents a single parameter entry in the preferred sink configuration format.
    /// </summary>
    internal sealed class SinkParameterEntry
    {
        /// <summary>
        /// Gets the exact parameter type name.
        /// </summary>
        public string? TypeName { get; init; }

        /// <summary>
        /// Gets whether the sink should be ignored when this argument is provably constant.
        /// </summary>
        public bool? IgnoreSinkIfConstant { get; init; }
    }
}
