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
        private readonly IReadOnlyList<EdgeId>[] _callsFrom;
        private readonly IReadOnlyList<EdgeId>[] _calledBy;
        private readonly int _overrideRelationshipCount;
        private readonly int _interfaceImplementationRelationshipCount;
        private readonly int _instantiatedTypeCount;
        private readonly IReadOnlyList<TypeId>[] _concreteDescendantTypeIdsByType;
        private readonly IReadOnlyDictionary<TypeId, TypeId?> _baseTypeIdsByType;
        private readonly IReadOnlyDictionary<TypeId, IReadOnlyList<TypeId>> _allInterfaceIdsByType;
        private readonly IReadOnlyDictionary<TypeLookupKey, TypeId> _typeIdsByLookupKey;

        /// <summary>
        /// Initializes a new analysis index from precomputed graph data.
        /// </summary>
        /// <param name="assemblies">The loaded assemblies.</param>
        /// <param name="types">The indexed types.</param>
        /// <param name="methods">The indexed methods.</param>
        /// <param name="propertyCount">The indexed property count.</param>
        /// <param name="publicInstancePropertySetterCount">The public instance property-setter count.</param>
        /// <param name="events">The indexed events.</param>
        /// <param name="edges">The emitted graph edges.</param>
        /// <param name="callsFrom">The outgoing adjacency list.</param>
        /// <param name="calledBy">The incoming adjacency list.</param>
        /// <param name="overrideRelationshipCount">The discovered override relationship count.</param>
        /// <param name="interfaceImplementationRelationshipCount">The discovered interface implementation relationship count.</param>
        /// <param name="instantiatedTypeCount">The observed instantiated type count.</param>
        /// <param name="concreteDescendantTypeIdsByType">The concrete subtype lookup keyed by base type.</param>
        /// <param name="baseTypeIdsByType">The base-type lookup.</param>
        /// <param name="allInterfaceIdsByType">The transitive interface lookup.</param>
        /// <param name="typeIdsByLookupKey">The type-definition lookup map.</param>
        private AnalysisIndex(
            IReadOnlyList<ModuleDefMD> assemblies,
            IReadOnlyList<TypeRecord> types,
            IReadOnlyList<MethodRecord> methods,
            int propertyCount,
            int publicInstancePropertySetterCount,
            IReadOnlyList<EventRecord> events,
            IReadOnlyList<EdgeRecord> edges,
            IReadOnlyList<EdgeId>[] callsFrom,
            IReadOnlyList<EdgeId>[] calledBy,
            int overrideRelationshipCount,
            int interfaceImplementationRelationshipCount,
            int instantiatedTypeCount,
            IReadOnlyList<TypeId>[] concreteDescendantTypeIdsByType,
            IReadOnlyDictionary<TypeId, TypeId?> baseTypeIdsByType,
            IReadOnlyDictionary<TypeId, IReadOnlyList<TypeId>> allInterfaceIdsByType,
            IReadOnlyDictionary<TypeLookupKey, TypeId> typeIdsByLookupKey)
        {
            Assemblies = assemblies;
            Types = types;
            Methods = methods;
            PropertyCount = propertyCount;
            PublicInstancePropertySetterCount = publicInstancePropertySetterCount;
            Events = events;
            Edges = edges;
            _callsFrom = callsFrom;
            _calledBy = calledBy;
            _overrideRelationshipCount = overrideRelationshipCount;
            _interfaceImplementationRelationshipCount = interfaceImplementationRelationshipCount;
            _instantiatedTypeCount = instantiatedTypeCount;
            _concreteDescendantTypeIdsByType = concreteDescendantTypeIdsByType;
            _baseTypeIdsByType = baseTypeIdsByType;
            _allInterfaceIdsByType = allInterfaceIdsByType;
            _typeIdsByLookupKey = typeIdsByLookupKey;
        }

        /// <summary>
        /// Gets the loaded assemblies included in the index.
        /// </summary>
        public IReadOnlyList<ModuleDefMD> Assemblies { get; }
        /// <summary>
        /// Gets the indexed types.
        /// </summary>
        public IReadOnlyList<TypeRecord> Types { get; }
        /// <summary>
        /// Gets the indexed methods.
        /// </summary>
        public IReadOnlyList<MethodRecord> Methods { get; }
        /// <summary>
        /// Gets the number of indexed properties.
        /// </summary>
        public int PropertyCount { get; }
        /// <summary>
        /// Gets the indexed events.
        /// </summary>
        public IReadOnlyList<EventRecord> Events { get; }
        /// <summary>
        /// Gets the emitted graph edges.
        /// </summary>
        public IReadOnlyList<EdgeRecord> Edges { get; }
        /// <summary>
        /// Gets the number of indexed interfaces.
        /// </summary>
        public int InterfaceTypeCount => Types.Count(type => type.IsInterface);
        /// <summary>
        /// Gets the number of indexed classes.
        /// </summary>
        public int ClassTypeCount => Types.Count(type => type.IsClass);
        /// <summary>
        /// Gets the number of indexed concrete classes.
        /// </summary>
        public int ConcreteClassTypeCount => Types.Count(type => type is { IsClass: true, IsAbstract: false });
        /// <summary>
        /// Gets the number of indexed abstract classes.
        /// </summary>
        public int AbstractClassTypeCount => Types.Count(type => type is { IsClass: true, IsAbstract: true });
        /// <summary>
        /// Gets the number of indexed value types.
        /// </summary>
        public int ValueTypeCount => Types.Count(type => type.IsValueType);
        /// <summary>
        /// Gets the number of public instance property setters.
        /// </summary>
        public int PublicInstancePropertySetterCount { get; }
        /// <summary>
        /// Gets the number of discovered override relationships.
        /// </summary>
        public int OverrideRelationshipCount => _overrideRelationshipCount;
        /// <summary>
        /// Gets the number of discovered interface implementation relationships.
        /// </summary>
        public int InterfaceImplementationRelationshipCount => _interfaceImplementationRelationshipCount;
        /// <summary>
        /// Gets the number of types observed via object instantiation.
        /// </summary>
        public int InstantiatedTypeCount => _instantiatedTypeCount;

        /// <summary>
        /// Builds an analysis index from the provided assemblies.
        /// </summary>
        /// <param name="assemblies">The assemblies to index.</param>
        /// <param name="interfaceExpansionMode">The interface expansion mode.</param>
        /// <param name="progress">The optional progress callback.</param>
        public static AnalysisIndex Build(IEnumerable<ModuleDefMD> assemblies, InterfaceExpansionMode interfaceExpansionMode = InterfaceExpansionMode.Strict, Action<string>? progress = null)
            => new Builder(assemblies, interfaceExpansionMode, progress).Build();

        /// <summary>
        /// Gets a method record by identifier.
        /// </summary>
        /// <param name="methodId">The method identifier.</param>
        public MethodRecord GetMethod(MethodId methodId) => Methods[methodId.Value];
        /// <summary>
        /// Gets a type record by identifier.
        /// </summary>
        /// <param name="typeId">The type identifier.</param>
        public TypeRecord GetType(TypeId typeId) => Types[typeId.Value];
        /// <summary>
        /// Gets an edge record by identifier.
        /// </summary>
        /// <param name="edgeId">The edge identifier.</param>
        public EdgeRecord GetEdge(EdgeId edgeId) => Edges[edgeId.Value];
        /// <summary>
        /// Gets the outgoing edges for a method.
        /// </summary>
        /// <param name="methodId">The source method identifier.</param>
        public IReadOnlyList<EdgeId> GetOutgoingEdges(MethodId methodId) => _callsFrom[methodId.Value];
        /// <summary>
        /// Gets the incoming edges for a method.
        /// </summary>
        /// <param name="methodId">The target method identifier.</param>
        public IReadOnlyList<EdgeId> GetIncomingEdges(MethodId methodId) => _calledBy[methodId.Value];
        /// <summary>
        /// Tries to resolve a loaded type identifier from a type definition.
        /// </summary>
        /// <param name="typeDef">The type definition to resolve.</param>
        /// <param name="typeId">The resolved type identifier.</param>
        public bool TryGetTypeId(TypeDef typeDef, out TypeId typeId) => _typeIdsByLookupKey.TryGetValue(CreateTypeLookupKey(typeDef), out typeId);

        /// <summary>
        /// Gets the concrete descendants of a class type, including the type itself when concrete.
        /// </summary>
        /// <param name="typeId">The base class identifier.</param>
        public IReadOnlyList<TypeId> GetConcreteDescendantTypeIds(TypeId typeId) => _concreteDescendantTypeIdsByType[typeId.Value];

        /// <summary>
        /// Determines whether a candidate type is assignable to a target type.
        /// </summary>
        /// <param name="targetTypeId">The target type identifier.</param>
        /// <param name="candidateTypeId">The candidate type identifier.</param>
        public bool IsAssignableTo(TypeId targetTypeId, TypeId candidateTypeId)
            => IsAssignableTo(targetTypeId, candidateTypeId, _baseTypeIdsByType, _allInterfaceIdsByType);

        /// <summary>
        /// Determines whether a candidate type is assignable to a target type.
        /// </summary>
        /// <param name="targetTypeId">The target type identifier.</param>
        /// <param name="candidateTypeId">The candidate type identifier.</param>
        /// <param name="baseTypeIdsByType">The base-type lookup to walk.</param>
        /// <param name="allInterfaceIdsByType">The transitive interface lookup to query.</param>
        private static bool IsAssignableTo(
            TypeId targetTypeId,
            TypeId candidateTypeId,
            IReadOnlyDictionary<TypeId, TypeId?> baseTypeIdsByType,
            IReadOnlyDictionary<TypeId, IReadOnlyList<TypeId>> allInterfaceIdsByType)
        {
            if (targetTypeId == candidateTypeId)
            {
                return true;
            }

            TypeId current = candidateTypeId;
            while (baseTypeIdsByType.TryGetValue(current, out TypeId? baseTypeId) && baseTypeId is { } resolvedBaseTypeId)
            {
                if (resolvedBaseTypeId == targetTypeId)
                {
                    return true;
                }

                current = resolvedBaseTypeId;
            }

            return allInterfaceIdsByType.TryGetValue(candidateTypeId, out IReadOnlyList<TypeId>? interfaces) && interfaces.Contains(targetTypeId);
        }

        /// <summary>
        /// Gets the underlying type-definition signature used for loaded-type lookups.
        /// </summary>
        /// <param name="type">The type signature to normalize.</param>
        internal static TypeSig? GetTypeDefinitionSignature(TypeSig? type)
        {
            while (type is not null)
            {
                switch (type)
                {
                    case ByRefSig byRefSig:
                        type = byRefSig.Next;
                        continue;
                    case PtrSig ptrSig:
                        type = ptrSig.Next;
                        continue;
                    case SZArraySig szArraySig:
                        type = szArraySig.Next;
                        continue;
                    case ArraySig arraySig:
                        type = arraySig.Next;
                        continue;
                    case GenericInstSig genericInstSig:
                        return genericInstSig.GenericType;
                    case CModOptSig cModOptSig:
                        type = cModOptSig.Next;
                        continue;
                    case CModReqdSig cModReqdSig:
                        type = cModReqdSig.Next;
                        continue;
                    case PinnedSig pinnedSig:
                        type = pinnedSig.Next;
                        continue;
                    default:
                        return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets an exact display name for an <see cref="IType"/>.
        /// </summary>
        /// <param name="type">The type to format.</param>
        internal static string GetTypeDisplayName(IType? type)
            => type switch
            {
                null => "<unknown>",
                TypeSig typeSig => GetTypeDisplayName(typeSig),
                _ => ToTypeSig(type) is { } typeSig ? GetTypeDisplayName(typeSig) : type.FullName.Replace('/', '+')
            };

        /// <summary>
        /// Gets an exact display name for a <see cref="TypeSig"/>.
        /// </summary>
        /// <param name="type">The type signature to format.</param>
        internal static string GetTypeDisplayName(TypeSig? type)
        {
            TypeSig? displayType = StripDisplayModifiers(type);
            return displayType is null ? "<unknown>" : displayType.FullName.Replace('/', '+');
        }

        /// <summary>
        /// Gets the type-definition display name used for loaded-type identity.
        /// </summary>
        /// <param name="type">The type to format.</param>
        internal static string GetTypeDefinitionDisplayName(ITypeDefOrRef? type)
            => type is null ? "<unknown>" : type.FullName.Replace('/', '+');

        /// <summary>
        /// Gets the type-definition display name used for loaded-type identity.
        /// </summary>
        /// <param name="type">The type signature to format.</param>
        internal static string GetTypeDefinitionDisplayName(TypeSig? type)
        {
            TypeSig? normalizedType = GetTypeDefinitionSignature(type);
            return normalizedType is null ? "<unknown>" : normalizedType.FullName.Replace('/', '+');
        }

        /// <summary>
        /// Determines whether an exact metadata type matches a configured type name.
        /// </summary>
        /// <param name="actualType">The actual metadata type.</param>
        /// <param name="configuredTypeName">The configured type name.</param>
        internal static bool MatchesConfiguredTypeName(TypeSig? actualType, string configuredTypeName)
            => string.Equals(GetTypeDisplayName(actualType), configuredTypeName, StringComparison.Ordinal) ||
               string.Equals(GetTopLevelGenericTypeDefinitionName(actualType), configuredTypeName, StringComparison.Ordinal);

        /// <summary>
        /// Determines whether a method signature exactly matches configured parameter types.
        /// </summary>
        /// <param name="methodSignature">The signature to evaluate.</param>
        /// <param name="parameterTypeNames">The configured parameter types.</param>
        internal static bool HasExactParameterSignature(MethodSig? methodSignature, IReadOnlyList<string> parameterTypeNames)
        {
            if (methodSignature is null || methodSignature.Params.Count != parameterTypeNames.Count)
            {
                return false;
            }

            return methodSignature.Params
                .Zip(parameterTypeNames)
                .All(pair => MatchesConfiguredTypeName(pair.First, pair.Second));
        }

        /// <summary>
        /// Converts a dnlib type representation into a <see cref="TypeSig"/>.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        internal static TypeSig? ToTypeSig(IType? type)
            => type switch
            {
                null => null,
                TypeSig typeSig => typeSig,
                TypeDef typeDef => typeDef.ToTypeSig(),
                TypeRef typeRef => typeRef.ToTypeSig(),
                TypeSpec typeSpec => typeSpec.TypeSig,
                _ => null
            };

        /// <summary>
        /// Removes display-only modifiers while preserving arrays, pointers, by-ref markers, and generic instantiations.
        /// </summary>
        /// <param name="type">The type signature to normalize.</param>
        private static TypeSig? StripDisplayModifiers(TypeSig? type)
        {
            while (type is not null)
            {
                switch (type)
                {
                    case CModOptSig cModOptSig:
                        type = cModOptSig.Next;
                        continue;
                    case CModReqdSig cModReqdSig:
                        type = cModReqdSig.Next;
                        continue;
                    case PinnedSig pinnedSig:
                        type = pinnedSig.Next;
                        continue;
                    default:
                        return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Formats a method reference for display.
        /// </summary>
        /// <param name="method">The method to format.</param>
        private static string FormatMethodDisplayName(IMethod method)
        {
            string declaringType = method.DeclaringType is null ? "<global>" : GetTypeDisplayName(method.DeclaringType);
            string[] parameters = method.MethodSig?.Params.Select(GetTypeDisplayName).ToArray() ?? [];
            return $"{declaringType}::{method.Name}({string.Join(", ", parameters)})";
        }

        /// <summary>
        /// Builds a method-signature key that preserves closed generic instantiations.
        /// </summary>
        /// <param name="method">The method to map.</param>
        private static DispatchSlotKey BuildMethodBindingSignatureKey(IMethod method)
        {
            string returnType = method.MethodSig?.RetType is null ? "System.Void" : GetTypeDisplayName(method.MethodSig.RetType);
            string[] parameterTypes = method.MethodSig?.Params.Select(GetTypeDisplayName).ToArray() ?? [];
            return new DispatchSlotKey(method.Name, method.NumberOfGenericParameters, returnType, parameterTypes);
        }

        /// <summary>
        /// Builds a dispatch slot key for a method.
        /// </summary>
        /// <param name="method">The method to map.</param>
        private static DispatchSlotKey BuildDispatchSlotKey(IMethod method)
        {
            string returnType = method.MethodSig?.RetType is null ? "System.Void" : GetDispatchSlotTypeName(method.MethodSig.RetType);
            string[] parameterTypes = method.MethodSig?.Params.Select(GetDispatchSlotTypeName).ToArray() ?? [];
            return new DispatchSlotKey(method.Name, method.NumberOfGenericParameters, returnType, parameterTypes);
        }

        /// <summary>
        /// Gets the dispatch-slot type name used for override and interface slot matching.
        /// </summary>
        /// <param name="type">The type signature to format.</param>
        private static string GetDispatchSlotTypeName(TypeSig? type)
        {
            TypeSig? slotType = StripDispatchSlotModifiers(type);
            return slotType is null ? "<unknown>" : slotType.FullName.Replace('/', '+');
        }

        /// <summary>
        /// Removes dispatch-slot modifiers while collapsing closed generic instantiations to their generic type definitions.
        /// </summary>
        /// <param name="type">The type signature to normalize.</param>
        private static TypeSig? StripDispatchSlotModifiers(TypeSig? type)
        {
            while (type is not null)
            {
                switch (type)
                {
                    case GenericInstSig genericInstSig:
                        return genericInstSig.GenericType;
                    case CModOptSig cModOptSig:
                        type = cModOptSig.Next;
                        continue;
                    case CModReqdSig cModReqdSig:
                        type = cModReqdSig.Next;
                        continue;
                    case PinnedSig pinnedSig:
                        type = pinnedSig.Next;
                        continue;
                    default:
                        return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the top-level generic type-definition name when a signature is a closed generic instantiation.
        /// </summary>
        /// <param name="type">The type signature to inspect.</param>
        private static string? GetTopLevelGenericTypeDefinitionName(TypeSig? type)
        {
            while (type is not null)
            {
                switch (type)
                {
                    case GenericInstSig genericInstSig:
                        return GetTypeDefinitionDisplayName(genericInstSig.GenericType);
                    case CModOptSig cModOptSig:
                        type = cModOptSig.Next;
                        continue;
                    case CModReqdSig cModReqdSig:
                        type = cModReqdSig.Next;
                        continue;
                    case PinnedSig pinnedSig:
                        type = pinnedSig.Next;
                        continue;
                    default:
                        return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a stable lookup key for a type definition.
        /// </summary>
        /// <param name="type">The type definition to map.</param>
        private static TypeLookupKey CreateTypeLookupKey(TypeDef type)
            => new(type.Module, unchecked((int)type.MDToken.Raw));
    }
}

