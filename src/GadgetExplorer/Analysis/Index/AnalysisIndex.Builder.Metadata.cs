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
        private sealed partial class Builder
        {
            /// <summary>
            /// Indexes all types, methods, and fields from the loaded assemblies.
            /// </summary>
            private void IndexTypesAndMethods()
            {
                foreach (ModuleDefMD module in _assemblies)
                {
                    foreach (TypeDef? type in module.GetTypes().OrderBy(GetTypeDefinitionDisplayName, StringComparer.Ordinal))
                    {
                        var typeId = new TypeId(_types.Count);
                        _typeIdsByTypeDef[type] = typeId;
                        _typeIdsByKey[GetTypeKey(type)] = typeId;

                        _types.Add(new TypeRecord(
                            typeId,
                            module,
                            type,
                            GetTypeDefinitionDisplayName(type),
                            GetRootTypeVisibility(type),
                            type is { IsClass: true, IsValueType: false },
                            type.IsInterface,
                            type.IsValueType,
                            type.IsAbstract,
                            $"{GetTypeDefinitionDisplayName(type)}, {type.Module.Assembly?.FullName ?? type.Module.Name}"));

                        foreach (MethodDef? method in type.Methods
                                     .OrderBy(method => method.MDToken.Raw)
                                     .ThenBy(method => method.Name.String, StringComparer.Ordinal))
                        {
                            var methodId = new MethodId(_methods.Count);
                            _methodIdsByMethodDef[method] = methodId;
                            _methods.Add(CreateLoadedMethodRecord(methodId, typeId, method));
                            AddLoadedMethodFallbackBindingKey(method, methodId);
                        }

                        foreach (FieldDef? field in type.Fields)
                        {
                            _fieldDefsByKey[GetFieldKey(field)] = field;
                        }
                    }
                }
            }

            /// <summary>
            /// Indexes properties and events from the already indexed types.
            /// </summary>
            private void IndexPropertiesAndEvents()
            {
                foreach (TypeRecord typeRecord in _types)
                {
                    foreach (PropertyDef? property in typeRecord.TypeDef.Properties.OrderBy(property => property.Name.String, StringComparer.Ordinal))
                    {
                        if (property.GetMethod is not null && TryGetLoadedMethodId(property.GetMethod, out MethodId resolvedGetterId))
                        {
                            SetMethodSpecialKind(resolvedGetterId, MethodSpecialKind.PropertyGetter);
                        }

                        if (property.SetMethod is not null && TryGetLoadedMethodId(property.SetMethod, out MethodId resolvedSetterId))
                        {
                            SetMethodSpecialKind(resolvedSetterId, MethodSpecialKind.PropertySetter);
                        }

                        _propertyCount++;
                        if (property.SetMethod is { IsPublic: true, IsStatic: false })
                        {
                            _publicInstancePropertySetterCount++;
                        }
                    }

                    foreach (EventDef? @event in typeRecord.TypeDef.Events.OrderBy(@event => @event.Name.String, StringComparer.Ordinal))
                    {
                        MethodId? addId = null;

                        if (@event.AddMethod is not null && TryGetLoadedMethodId(@event.AddMethod, out MethodId resolvedAddId))
                        {
                            addId = resolvedAddId;
                            SetMethodSpecialKind(resolvedAddId, MethodSpecialKind.EventAdd);
                        }

                        var eventRecord = new EventRecord(
                            typeRecord.Id,
                            GetTypeDisplayName(@event.EventType));

                        _events.Add(eventRecord);
                        if (addId is { } addAccessorId)
                        {
                            _eventsByAddAccessor[addAccessorId] = eventRecord;
                        }
                    }
                }
            }

            private void SetMethodSpecialKind(MethodId methodId, MethodSpecialKind specialKind)
            {
                MethodRecord method = _methods[methodId.Value];
                if (specialKind == MethodSpecialKind.None || method.SpecialKind == specialKind)
                {
                    return;
                }

                if (method.SpecialKind != MethodSpecialKind.None)
                {
                    throw new InvalidOperationException(
                        $"Method '{method.DisplayName}' cannot be classified as both '{method.SpecialKind}' and '{specialKind}'.");
                }

                _methods[methodId.Value] = method with { SpecialKind = specialKind };
            }
        }
    }
}
