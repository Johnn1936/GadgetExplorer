/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    public sealed class DispatchSlotKey(
        string methodName,
        int genericArity,
        string returnTypeName,
        IReadOnlyList<string> parameterTypeNames) : IEquatable<DispatchSlotKey>
    {
        private string MethodName { get; } = methodName;

        private int GenericArity { get; } = genericArity;

        private string ReturnTypeName { get; } = returnTypeName;

        private IReadOnlyList<string> ParameterTypeNames { get; } = parameterTypeNames;

        public bool Equals(DispatchSlotKey? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other is null)
            {
                return false;
            }

            return string.Equals(MethodName, other.MethodName, StringComparison.Ordinal) &&
                   GenericArity == other.GenericArity &&
                   string.Equals(ReturnTypeName, other.ReturnTypeName, StringComparison.Ordinal) &&
                   ParameterTypeNames.SequenceEqual(other.ParameterTypeNames, StringComparer.Ordinal);
        }

        public override bool Equals(object? obj) => Equals(obj as DispatchSlotKey);

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(MethodName, StringComparer.Ordinal);
            hashCode.Add(GenericArity);
            hashCode.Add(ReturnTypeName, StringComparer.Ordinal);
            foreach (string parameterTypeName in ParameterTypeNames)
            {
                hashCode.Add(parameterTypeName, StringComparer.Ordinal);
            }

            return hashCode.ToHashCode();
        }
    }
}
