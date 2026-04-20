/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Progress
{
    /// <summary>
    /// Provides shared progress-reporting rules for long-running scan steps.
    /// </summary>
    internal static class ScanProgress
    {
        private const int MaxProgressSteps = 10;

        /// <summary>
        /// Determines whether the current position has crossed a new progress step and returns the
        /// highest completed percentage step when it does.
        /// </summary>
        /// <param name="currentCount">The current completed work item count.</param>
        /// <param name="totalCount">The total work item count.</param>
        /// <param name="lastReportedStep">The last reported progress step.</param>
        /// <param name="percentage">The completed percentage when a new step is crossed.</param>
        public static bool TryGetStepPercentage(int currentCount, int totalCount, ref int lastReportedStep, out int percentage)
        {
            percentage = 0;
            if (currentCount <= 0 || totalCount <= 0)
            {
                return false;
            }

            int completedSteps = currentCount >= totalCount
                ? MaxProgressSteps
                : Math.Min(MaxProgressSteps, (int)((long)currentCount * MaxProgressSteps / totalCount));

            if (completedSteps <= lastReportedStep)
            {
                return false;
            }

            lastReportedStep = completedSteps;
            percentage = completedSteps * 10;
            return true;
        }
    }
}

