/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Index
{
    public sealed partial class AnalysisIndex
    {
        private static MethodRecord CreateLoadedMethodRecord(MethodId methodId, TypeId declaringTypeId, MethodDef method)
            => new(
                methodId,
                declaringTypeId,
                FormatMethodDisplayName(method),
                method.Name,
                method.IsStatic,
                method.IsAbstract,
                method.IsConstructor ? MethodSpecialKind.Constructor : MethodSpecialKind.None,
                method.IsPublic,
                method.HasBody,
                method,
                method,
                method is { IsPinvokeImpl: true, ImplMap: not null },
                NormalizeNativeModuleName(method.ImplMap?.Module?.Name),
                method is { IsPinvokeImpl: true, ImplMap: not null }
                    ? method.ImplMap.Name ?? method.Name
                    : null);

        private static MethodRecord CreateExternalMethodRecord(MethodId methodId, IMethod method)
        {
            MethodDef? resolvedMethod = method.ResolveMethodDef();
            bool isStatic = resolvedMethod?.IsStatic ?? method.MethodSig?.HasThis == false;

            return new MethodRecord(
                methodId,
                new TypeId(-1),
                FormatMethodDisplayName(method),
                method.Name,
                isStatic,
                resolvedMethod?.IsAbstract == true,
                resolvedMethod?.IsConstructor ?? (method.Name == ".ctor" || method.Name == ".cctor")
                    ? MethodSpecialKind.Constructor
                    : MethodSpecialKind.None,
                resolvedMethod?.IsPublic == true,
                false,
                null,
                method,
                false,
                null,
                null);
        }

        internal static string? NormalizeNativeModuleName(string? moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                return null;
            }

            string trimmedModuleName = Path.GetFileName(moduleName.Trim());
            string extension = Path.GetExtension(trimmedModuleName);
            return string.IsNullOrEmpty(extension)
                ? trimmedModuleName
                : trimmedModuleName[..^extension.Length];
        }
    }
}
