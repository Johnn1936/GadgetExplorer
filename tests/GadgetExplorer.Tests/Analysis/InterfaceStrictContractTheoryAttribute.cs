/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class InterfaceStrictContractFactAttribute : FactAttribute
    {
        public InterfaceStrictContractFactAttribute()
        {
            if (!InterfaceStrictContractAttributeGate.IsEnabled())
            {
                Skip = $"Interface strict contract harness is opt-in. Set {InterfaceStrictContractTheoryAttribute.EnableEnvironmentVariableName}=1 to run it.";
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class InterfaceStrictContractTheoryAttribute : TheoryAttribute
    {
        public const string EnableEnvironmentVariableName = "GadgetExplorer_RUN_INTERFACE_STRICT_CONTRACTS";

        public InterfaceStrictContractTheoryAttribute()
        {
            if (!InterfaceStrictContractAttributeGate.IsEnabled())
            {
                Skip = $"Interface strict contract harness is opt-in. Set {EnableEnvironmentVariableName}=1 to run it.";
            }
        }
    }

    internal static class InterfaceStrictContractAttributeGate
    {
        public static bool IsEnabled()
        {
            string? value = Environment.GetEnvironmentVariable(InterfaceStrictContractTheoryAttribute.EnableEnvironmentVariableName);
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
