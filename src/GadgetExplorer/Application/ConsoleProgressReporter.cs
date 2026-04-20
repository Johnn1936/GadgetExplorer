/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Application
{
    /// <summary>
    /// Creates simple console progress reporters.
    /// </summary>
    public static class ConsoleProgressReporter
    {
        /// <summary>
        /// Creates a timestamped progress callback that writes to stderr.
        /// </summary>
        public static Action<string> Create()
            => message => Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
