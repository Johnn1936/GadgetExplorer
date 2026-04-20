/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Application.Reporting
{
    internal static class ScanReportValueFormatter
    {
        public static string FormatLoadedAssemblyOrigin(LoadedAssemblyOrigin origin)
            => origin switch
            {
                LoadedAssemblyOrigin.InputRoot => "input-root",
                LoadedAssemblyOrigin.InferredInstalledRuntime => "installed-runtime",
                LoadedAssemblyOrigin.HostRuntimeFallback => "host-runtime-fallback",
                LoadedAssemblyOrigin.GlobalAssemblyCache => "global-assembly-cache",
                _ => "external"
            };

        public static string FormatAssemblyResolutionMode(AssemblyResolutionMode mode)
            => ScanOptionValues.Format(mode);

        public static string FormatSerializerProfileSource(bool usesProfileFile)
            => usesProfileFile ? "file" : "shipped";

        public static string FormatTriggerKind(TriggerKind kind)
            => kind switch
            {
                TriggerKind.Constructor => "constructor",
                TriggerKind.PublicPropertyGetter => "public-property-getter",
                TriggerKind.PublicPropertySetter => "public-property-setter",
                TriggerKind.NonPublicPropertySetter => "non-public-property-setter",
                TriggerKind.DeserializationCallback => "deserialization-callback",
                TriggerKind.CustomDeserializationMethod => "custom-deserialization-method",
                TriggerKind.Finalizer => "finalizer",
                _ => kind.ToString()
            };

        public static string FormatEdgeKind(EdgeKind kind)
            => kind switch
            {
                EdgeKind.DirectCall => "direct-call",
                EdgeKind.ConstructorCall => "constructor-call",
                EdgeKind.PropertyAccessor => "property-accessor",
                EdgeKind.VirtualDispatch => "virtual-dispatch",
                EdgeKind.InterfaceDispatch => "interface-dispatch",
                EdgeKind.DelegateInvoke => "delegate-invoke",
                EdgeKind.EventAccessor => "event-accessor",
                EdgeKind.EventRaise => "event-raise",
                EdgeKind.AsyncIterator => "async-iterator",
                _ => kind.ToString()
            };

        public static string FormatTextEdgeKindLabel(string kind)
            => kind switch
            {
                "direct-call" => nameof(EdgeKind.DirectCall),
                "constructor-call" => nameof(EdgeKind.ConstructorCall),
                "property-accessor" => nameof(EdgeKind.PropertyAccessor),
                "virtual-dispatch" => nameof(EdgeKind.VirtualDispatch),
                "interface-dispatch" => nameof(EdgeKind.InterfaceDispatch),
                "delegate-invoke" => nameof(EdgeKind.DelegateInvoke),
                "event-accessor" => nameof(EdgeKind.EventAccessor),
                "event-raise" => nameof(EdgeKind.EventRaise),
                "async-iterator" => nameof(EdgeKind.AsyncIterator),
                _ => kind
            };

        public static string FormatConstantValueKind(ConstantValueKind kind)
            => kind switch
            {
                ConstantValueKind.Null => "null",
                ConstantValueKind.StringLiteral => "string-literal",
                ConstantValueKind.Primitive => "primitive",
                ConstantValueKind.Type => "type",
                ConstantValueKind.Uri => "uri",
                ConstantValueKind.RuntimeTypeHandle => "runtime-type-handle",
                _ => kind.ToString()
            };

        public static string FormatCommandLineArgumentsLabel(IReadOnlyList<string> commandLineArguments)
            => commandLineArguments.Count == 0
                ? "<none>"
                : string.Join(" ", commandLineArguments.Select(QuoteArgument));

        private static string QuoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            return argument.Any(char.IsWhiteSpace) || argument.Contains('"')
                ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
                : argument;
        }
    }
}
