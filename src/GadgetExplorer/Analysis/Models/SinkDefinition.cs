/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    public sealed record SinkDefinition
    {
        public SinkDefinition(
            string declaringType,
            string methodName,
            IReadOnlyList<SinkParameterDefinition>? parameters = null,
            string nativeModule = "",
            string nativeEntryPoint = "")
        {
            DeclaringType = declaringType;
            MethodName = methodName;
            HasExplicitParameterSignature = parameters is not null;
            Parameters = parameters ?? [];
            NativeModule = nativeModule;
            NativeEntryPoint = nativeEntryPoint;
        }

        public SinkDefinition(
            string declaringType,
            string methodName,
            IReadOnlyList<string>? parameterTypeNames,
            string nativeModule = "",
            string nativeEntryPoint = "")
            : this(
                declaringType,
                methodName,
                parameterTypeNames?
                    .Select(parameterTypeName => new SinkParameterDefinition(parameterTypeName))
                    .ToArray(),
                nativeModule,
                nativeEntryPoint)
        {
        }

        public string DeclaringType { get; }

        public string MethodName { get; }

        public string NativeModule { get; }

        public string NativeEntryPoint { get; }

        public bool HasExplicitParameterSignature { get; }

        public IReadOnlyList<SinkParameterDefinition> Parameters { get; }

        public IReadOnlyList<string> ParameterTypeNames => Parameters.Select(parameter => parameter.TypeName).ToArray();
    }
}
