/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using dnlib.DotNet;

namespace GadgetExplorer.Analysis.Index
{
    /// <summary>
    /// Contains dispatch-map building helpers for <see cref="AnalysisIndex"/>.
    /// </summary>
    public sealed partial class AnalysisIndex
    {
        private sealed partial class Builder
        {
            /// <summary>
            /// Builds the base-type and interface relationship maps used during dispatch resolution.
            /// </summary>
            private void BuildTypeRelationships()
            {
                var directInterfaceIdsByType = new Dictionary<TypeId, IReadOnlyList<TypeId>>();

                foreach (TypeRecord type in _types)
                {
                    _baseTypeIdsByType[type.Id] = TryResolveTypeId(type.TypeDef.BaseType, out TypeId baseTypeId) ? baseTypeId : null;
                    directInterfaceIdsByType[type.Id] = [.. GetDirectInterfaces(type.TypeDef)
                        .Select(@interface => TryResolveTypeId(@interface, out TypeId interfaceId) ? interfaceId : new TypeId(-1))
                        .Where(interfaceId => interfaceId.Value >= 0)
                        .Distinct()
                        .OrderBy(interfaceId => interfaceId.Value)];
                }

                foreach (TypeRecord type in _types)
                {
                    var interfaces = new HashSet<TypeId>();
                    CollectAllInterfaces(type.Id, interfaces, directInterfaceIdsByType);
                    _allInterfaceIdsByType[type.Id] = [.. interfaces.OrderBy(id => id.Value)];
                }

                BuildConcreteDescendantLookup();
            }

            /// <summary>
            /// Collects all interfaces implemented by a type, including inherited interfaces.
            /// </summary>
            /// <param name="typeId">The type identifier to expand.</param>
            /// <param name="interfaces">The interface accumulator.</param>
            /// <param name="directInterfaceIdsByType">The direct interface lookup keyed by type.</param>
            private void CollectAllInterfaces(
                TypeId typeId,
                HashSet<TypeId> interfaces,
                IReadOnlyDictionary<TypeId, IReadOnlyList<TypeId>> directInterfaceIdsByType)
            {
                if (directInterfaceIdsByType.TryGetValue(typeId, out IReadOnlyList<TypeId>? directInterfaces))
                {
                    foreach (TypeId interfaceId in directInterfaces)
                    {
                        if (interfaces.Add(interfaceId))
                        {
                            CollectAllInterfaces(interfaceId, interfaces, directInterfaceIdsByType);
                        }
                    }
                }

                if (_baseTypeIdsByType.TryGetValue(typeId, out TypeId? baseTypeId) && baseTypeId is { } resolvedBaseTypeId)
                {
                    CollectAllInterfaces(resolvedBaseTypeId, interfaces, directInterfaceIdsByType);
                }
            }

            /// <summary>
            /// Builds the concrete-descendant lookup used during trigger root expansion.
            /// </summary>
            private void BuildConcreteDescendantLookup()
            {
                var concreteDescendantsByType = new List<TypeId>[_types.Count];

                foreach (TypeRecord type in _types.Where(type => type is { IsClass: true, IsAbstract: false }))
                {
                    TypeId? current = type.Id;
                    while (current is { } currentTypeId)
                    {
                        List<TypeId> descendants = concreteDescendantsByType[currentTypeId.Value] ??= [];
                        descendants.Add(type.Id);
                        current = _baseTypeIdsByType[currentTypeId];
                    }
                }

                _concreteDescendantTypeIdsByType = concreteDescendantsByType
                    .Select(descendants => descendants is null
                        ? []
                        : (IReadOnlyList<TypeId>)[.. descendants
                            .Distinct()
                            .OrderBy(typeId => typeId.Value)])
                    .ToArray();
            }

            /// <summary>
            /// Builds virtual and interface dispatch maps for all concrete types.
            /// </summary>
            private void BuildDispatchMaps()
            {
                var overrideTargetsByBaseMethod = new Dictionary<MethodId, HashSet<MethodId>>();
                var interfaceTargetsByMethod = new Dictionary<MethodId, HashSet<MethodId>>();

                foreach (MethodRecord method in _methods.Where(method => !method.IsStatic && method.DeclaringTypeId.Value >= 0))
                {
                    _declaredMethodByTypeAndSlot[(method.DeclaringTypeId, BuildDispatchSlotKey(method.MethodReference))] = method.Id;
                }

                foreach (TypeRecord type in _types.Where(type => type is { IsClass: true, IsAbstract: false }))
                {
                    List<TypeId> hierarchy = GetHierarchy(type.Id);

                    foreach (TypeId ancestorId in hierarchy)
                    {
                        foreach (MethodDef baseMethod in _types[ancestorId.Value].TypeDef.Methods.Where(IsVirtualInstanceMethod))
                        {
                            if (!TryGetLoadedMethodId(baseMethod, out MethodId baseMethodId))
                            {
                                continue;
                            }

                            MethodId? targetMethodId = FindVirtualDispatchTarget(hierarchy, baseMethod);
                            if (targetMethodId is { } resolvedTargetMethodId)
                            {
                                AddMapping(overrideTargetsByBaseMethod, baseMethodId, resolvedTargetMethodId);
                            }
                        }
                    }

                    if (_interfaceExpansionMode == InterfaceExpansionMode.Off)
                    {
                        continue;
                    }

                    foreach (TypeId interfaceId in _allInterfaceIdsByType.GetValueOrDefault(type.Id, []))
                    {
                        foreach (MethodDef interfaceMethod in _types[interfaceId.Value].TypeDef.Methods.Where(method => !method.IsStatic))
                        {
                            if (!TryGetLoadedMethodId(interfaceMethod, out MethodId interfaceMethodId))
                            {
                                continue;
                            }

                            MethodId? targetMethodId = FindInterfaceDispatchTarget(hierarchy, interfaceMethod);
                            if (targetMethodId is { } resolvedTargetMethodId)
                            {
                                AddMapping(interfaceTargetsByMethod, interfaceMethodId, resolvedTargetMethodId);
                            }
                        }
                    }
                }

                _overrideTargetsByBaseMethod = FreezeMappings(overrideTargetsByBaseMethod);
                _interfaceTargetsByMethod = FreezeMappings(interfaceTargetsByMethod);
            }

            /// <summary>
            /// Gets the inheritance hierarchy for a type, starting with the type itself.
            /// </summary>
            /// <param name="typeId">The type identifier to expand.</param>
            private List<TypeId> GetHierarchy(TypeId typeId)
            {
                var hierarchy = new List<TypeId>();
                TypeId current = typeId;
                hierarchy.Add(current);

                while (_baseTypeIdsByType.TryGetValue(current, out TypeId? baseTypeId) && baseTypeId is { } resolvedBaseTypeId)
                {
                    hierarchy.Add(resolvedBaseTypeId);
                    current = resolvedBaseTypeId;
                }

                return hierarchy;
            }

            /// <summary>
            /// Finds the concrete virtual dispatch target for a base method on a hierarchy.
            /// </summary>
            /// <param name="hierarchy">The hierarchy to search.</param>
            /// <param name="baseMethod">The base virtual method.</param>
            private MethodId? FindVirtualDispatchTarget(IReadOnlyList<TypeId> hierarchy, MethodDef baseMethod)
            {
                if (!TryResolveTypeId(baseMethod.DeclaringType, out TypeId baseTypeId))
                {
                    return TryGetLoadedMethodId(baseMethod, out MethodId unresolvedBaseMethodId) && !_methods[unresolvedBaseMethodId.Value].IsAbstract
                        ? unresolvedBaseMethodId
                        : null;
                }

                DispatchSlotKey slotKey = BuildDispatchSlotKey(baseMethod);

                foreach (TypeId hierarchyTypeId in hierarchy)
                {
                    if (_declaredMethodByTypeAndSlot.TryGetValue((hierarchyTypeId, slotKey), out MethodId candidateMethodId) &&
                        !_methods[candidateMethodId.Value].IsAbstract)
                    {
                        return candidateMethodId;
                    }

                    if (hierarchyTypeId == baseTypeId)
                    {
                        break;
                    }
                }

                return TryGetLoadedMethodId(baseMethod, out MethodId baseMethodId) && !_methods[baseMethodId.Value].IsAbstract
                    ? baseMethodId
                    : null;
            }

            /// <summary>
            /// Finds the concrete interface dispatch target for an interface method on a hierarchy.
            /// </summary>
            /// <param name="hierarchy">The hierarchy to search.</param>
            /// <param name="interfaceMethod">The interface method to resolve.</param>
            private MethodId? FindInterfaceDispatchTarget(IReadOnlyList<TypeId> hierarchy, MethodDef interfaceMethod)
            {
                foreach (TypeId hierarchyTypeId in hierarchy)
                {
                    foreach (MethodDef declaredMethod in _types[hierarchyTypeId.Value].TypeDef.Methods)
                    {
                        if (declaredMethod.Overrides.Any(methodOverride =>
                                methodOverride.MethodDeclaration.ResolveMethodDef() is MethodDef overriddenMethod &&
                                overriddenMethod.Module == interfaceMethod.Module &&
                                overriddenMethod.MDToken.Raw == interfaceMethod.MDToken.Raw) &&
                            TryGetLoadedMethodId(declaredMethod, out MethodId explicitMethodId) &&
                            !_methods[explicitMethodId.Value].IsAbstract)
                        {
                            return explicitMethodId;
                        }
                    }
                }

                DispatchSlotKey slotKey = BuildDispatchSlotKey(interfaceMethod);
                return FindInterfaceDispatchTarget(hierarchy, slotKey);
            }

            /// <summary>
            /// Finds the concrete interface dispatch target for a dispatch slot on a hierarchy.
            /// </summary>
            /// <param name="hierarchy">The hierarchy to search.</param>
            /// <param name="slotKey">The dispatch slot to resolve.</param>
            private MethodId? FindInterfaceDispatchTarget(IReadOnlyList<TypeId> hierarchy, DispatchSlotKey slotKey)
            {
                foreach (TypeId hierarchyTypeId in hierarchy)
                {
                    if (_declaredMethodByTypeAndSlot.TryGetValue((hierarchyTypeId, slotKey), out MethodId candidateMethodId) &&
                        !_methods[candidateMethodId.Value].IsAbstract)
                    {
                        return candidateMethodId;
                    }
                }

                return null;
            }
        }
    }
}

