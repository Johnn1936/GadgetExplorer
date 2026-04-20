# Serializer Behavior Matrix

Date: 2026-04-16

## Purpose

This document answers three narrow questions for each serializer:

- what are the restrictions on the root type,
- what are the restrictions on construction of the root type,
- and what root triggers can kick off execution of a code graph.

Those are the serializer behaviors that matter most to GadgetExplorer's current root/trigger-centric architecture. This is not a general gap map for serializer state modeling.

## Scope

- `JsonDotNet` here means the shipped Json.NET profile for `TypeNameHandling != None`.
- `BinaryFormatter` here means classic runtime BinaryFormatter behavior over BinaryFormatter/NRBF payloads, not passive offline NRBF parsing.
- `MessagePackTypeless` here means unsafe MessagePack-CSharp typeless deserialization through `MessagePackSerializer.Typeless` / `TypelessContractlessStandardResolver`.
- `XmlSerializer` here means base `System.Xml.Serialization.XmlSerializer` behavior, not custom XML wrappers that derive an expected type from attacker-controlled data.
- This document is about first-party serializer behavior that GadgetExplorer can plausibly model with its current root/trigger-centric architecture.
- This document is not about general state propagation or object-state correctness after deserialization.

## Out Of Scope For This Document

These may matter for full serializer truth, but they are not the question this document is trying to answer:

- field population details,
- `[NonSerialized]` filtering,
- collection merge/replace behavior,
- fixup ordering that only matters if we model state,
- binder and surrogate infrastructure,
- application-registered converter/event wiring,
- general stateful gadget construction or bridge logic.

If any of those become immediate graph-walking requirements later, they should be documented separately.

## Evidence Used

Official docs and API references:

- Json.NET serialization guide: <https://www.newtonsoft.com/json/help/html/SerializationGuide.htm>
- Json.NET `JsonConstructorAttribute`: <https://www.newtonsoft.com/json/help/html/JsonConstructorAttribute.htm>
- Json.NET constructor handling: <https://www.newtonsoft.com/json/help/html/DeserializeConstructorHandling.htm>
- Json.NET serialization callbacks: <https://www.newtonsoft.com/json/help/html/SerializationCallbackAttributes.htm>
- Json.NET serialization error handling: <https://www.newtonsoft.com/json/help/html/serializationerrorhandling.htm>
- Json.NET `DataContract` / `DataMember` support: <https://www.newtonsoft.com/json/help/html/DataContractAndDataMember.htm>
- BinaryFormatter functionality reference: <https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-migration-guide/functionality-reference>
- `FormatterServices.GetUninitializedObject`: <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatterservices.getuninitializedobject?view=net-9.0>
- `OnDeserializedAttribute`: <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.ondeserializedattribute?view=net-9.0>
- `IDeserializationCallback`: <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.ideserializationcallback?view=net-9.0>
- `IObjectReference`: <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.iobjectreference?view=net-9.0>
- `ISerializable`: <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.iserializable?view=net-9.0>
- CA2229 "Implement serialization constructors": <https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2229>
- XML serialization overview: <https://learn.microsoft.com/en-us/dotnet/standard/serialization/introducing-xml-serialization>
- `XmlSerializer` constructors: <https://learn.microsoft.com/en-us/dotnet/api/system.xml.serialization.xmlserializer.-ctor?view=net-10.0>
- `XmlSerializer` runtime-library notes: <https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-xml-serialization-xmlserializer>
- `IXmlSerializable`: <https://learn.microsoft.com/en-us/dotnet/api/system.xml.serialization.ixmlserializable?view=net-9.0>
- `XmlIncludeAttribute`: <https://learn.microsoft.com/en-us/dotnet/api/system.xml.serialization.xmlincludeattribute?view=net-9.0>
- `XmlSerializationReader.CreateInaccessibleConstructorException`: <https://learn.microsoft.com/en-us/dotnet/api/system.xml.serialization.xmlserializationreader.createinaccessibleconstructorexception?view=net-10.0>
- Controlling XML serialization using attributes: <https://learn.microsoft.com/en-us/dotnet/standard/serialization/controlling-xml-serialization-using-attributes>
- Attributes that control XML serialization: <https://learn.microsoft.com/en-us/dotnet/standard/serialization/attributes-that-control-xml-serialization>
- How to control serialization of derived classes: <https://learn.microsoft.com/en-us/dotnet/standard/serialization/how-to-control-serialization-of-derived-classes>

Established research reviewed for this revalidation:

- Alvaro Munoz and Oleksandr Mirosh, "Friday the 13th: JSON Attacks", Black Hat USA 2017
- Piotr Bazydlo, "Exploiting Hardened .NET Deserialization: New Exploitation Ideas and Abuse of Insecure Serialization", Hexacon 2023

Repo verification used for this revalidation:

- `src/GadgetExplorer/Configuration/SerializerProfile.cs`
- `src/GadgetExplorer/Analysis/SinkAnalyzer.RootEligibility.cs`
- `src/GadgetExplorer/Analysis/SinkAnalyzer.Triggers.cs`
- `src/GadgetExplorer/resources/serializer-profiles/JsonDotNet.profile.json`
- `src/GadgetExplorer/resources/serializer-profiles/BinaryFormatter.profile.json`
- `src/GadgetExplorer/resources/serializer-profiles/MessagePackTypeless.profile.json`
- `src/GadgetExplorer/resources/serializer-profiles/XmlSerializer.profile.json`
- `tests/GadgetExplorer.Tests/Analysis/ProfileBehaviorTests.cs`
- `tests/GadgetExplorer.Tests/Analysis/RootEligibilityBehaviorTests.cs`
- `tests/GadgetExplorer.Tests/Analysis/TriggerBehaviorTests.cs`
- `tests/GadgetExplorer.Tests/Analysis/JsonDotNetRuntimeBehaviorTests.cs`
- `tests/GadgetExplorer.Tests/Analysis/MessagePackTypelessRuntimeBehaviorTests.cs`
- `tests/GadgetExplorer.Tests/Analysis/XmlSerializerRuntimeBehaviorTests.cs`

Local runtime sanity checks used for the XmlSerializer addition:

- separate-assembly XmlSerializer repros for root visibility, parameterless-constructor behavior, non-public setter rejection, `IXmlSerializable.ReadXml(XmlReader)`, and callback absence

## Focused Matrix

### 1. Root Type Restrictions

| Axis | Json.NET behavior | BinaryFormatter behavior | XmlSerializer behavior |
| --- | --- | --- | --- |
| Public root visibility | No inherent public-visibility requirement when type resolution already reaches the target type. Runtime tests confirmed cross-assembly `internal` top-level roots plus `protected` and `private` nested roots under `TypeNameHandling.All` and `TypeNameHandling.Auto`. | Runtime does not inherently require public visibility. | Official docs consistently describe public classes/public class declarations, and a local cross-assembly repro accepted public roots and public nested roots but rejected `internal`, `protected`, and `private` roots as inaccessible. |
| Root attributes | Ordinary object mode does not require `[Serializable]`. Json.NET's `ISerializable` path does. | Root type normally requires `[Serializable]`. | No `[Serializable]` requirement. XML attributes such as `XmlRootAttribute`, `XmlTypeAttribute`, `XmlIncludeAttribute`, `XmlElementAttribute`, and `XmlArrayItemAttribute` shape the recognized XML/type surface but do not act like BinaryFormatter-style eligibility markers. |
| Root interfaces | `ISerializable` matters only for the serialization-constructor path. | `ISerializable` matters for the serialization-constructor path. | No interface is required for ordinary object mode. `IXmlSerializable` is an opt-in custom serialization/deserialization path. Interface-typed members are not supported by the ordinary auto-mapped path. |
| Learned-type / polymorphism gates | Payload-controlled type information is optional and profile/policy dependent. | Type identity is carried by the formatter itself. | The serializer learns allowed types from the expected root graph plus explicit mechanisms such as `XmlIncludeAttribute`, `XmlElement(Type)`, `XmlArrayItem(Type)`, `extraTypes`, and `XmlAttributeOverrides`. Friday the 13th's `ExpandedWrapper` trick works by reshaping that learned graph when expected type is attacker-controlled; it is not a separate serializer mode. |
| Unsupported member shapes | Json.NET is comparatively permissive here. | BinaryFormatter does not depend on public member mapping in the same way. | Friday the 13th calls out interface and `System.Type` members as problematic for XmlSerializer graphs. Local repro rejected an interface-typed member root during serializer construction and failed to deserialize a `System.Type` member. |

GadgetExplorer mapping:
`JsonDotNet` admits public and non-public roots across the declared visibility buckets that Json.NET runtime tests already cover when type resolution reaches the target type.
`BinaryFormatter` does not require public roots.
The shipped `XmlSerializer` profile requires publicly visible roots, does not require `[Serializable]`, treats `IXmlSerializable` as opt-in serializer-specific behavior, stays conservative on learned-type admission, and avoids over-claiming support for roots whose reachable public members rely on interface- or `System.Type`-based ordinary XmlSerializer mapping.

### 2. Root Construction Restrictions

| Axis | Json.NET behavior | BinaryFormatter behavior | XmlSerializer behavior |
| --- | --- | --- | --- |
| Ordinary activation mode | Constructor-based. | Uninitialized-object-based for ordinary formatter restoration. | Constructor-based for ordinary object mode; `IXmlSerializable` still requires object construction before `ReadXml`. |
| Constructor selection | `[JsonConstructor]`, then public parameterless, then a single public parameterized constructor, then non-public parameterless by default. | Ordinary constructors are skipped; `ISerializable` uses the serialization constructor. | Parameterized constructors are not used. Official docs require a parameterless constructor. A local .NET Framework repro confirmed that a private parameterless constructor can still be used, so visibility should remain serializer-specific and configurable rather than assumed globally. |
| Exact constructor requirements | No single exact signature in ordinary object mode; selection follows the ordered rules above. `ISerializable` uses `(SerializationInfo, StreamingContext)`. | `ISerializable` uses `(SerializationInfo, StreamingContext)`. | Exact parameterless constructor. `IXmlSerializable` types also require a parameterless constructor. |
| Constructor parameter recursion | Json.NET can recurse through constructor-selected roots and constructor parameters that satisfy current eligibility rules. | BinaryFormatter ordinary restoration does not depend on ordinary constructor parameter eligibility. | No constructor-parameter recursion because parameterized constructors are not part of the ordinary deserialization path. |

GadgetExplorer mapping:
The shipped Json.NET and BinaryFormatter mappings already match these behaviors.
The shipped `XmlSerializer` profile models constructor-based activation, parameterless-constructor selection through explicit ordered constructor-selection rules, and no constructor-parameter recursion.
The parameterless-constructor visibility policy should stay serializer-specific and configurable rather than being turned into a global default.

### 3. Root Trigger Surfaces

| Axis | Json.NET behavior | BinaryFormatter behavior | XmlSerializer behavior |
| --- | --- | --- | --- |
| Constructors | Real trigger surface. | Serialization constructor is a real trigger surface; ordinary constructors are not. | Parameterless constructor is a real trigger surface. Local repro showed constructor execution on successful deserialization. |
| Public property setters | Real trigger surface. | Not a normal trigger surface. | Real trigger surface for ordinary object mode. |
| Non-public property setters | Can be opt-in writable. Official Json.NET docs explicitly call out `JsonPropertyAttribute` or `DataMemberAttribute` for deserialization of non-public-setter properties. | Not a normal trigger surface. | Not part of the ordinary model. Official docs require public get/set accessors, and a local repro failed serializer creation for a property without a public setter. |
| Attribute callbacks | `OnDeserializing` and `OnDeserialized` are real deserialization callbacks. | `OnDeserializing` and `OnDeserialized` are real deserialization callbacks. | No evidence that XmlSerializer uses formatter-style callback attributes. A local repro did not invoke `[OnDeserialized]`. |
| Error-path callbacks | `OnErrorAttribute` is a real Json.NET callback surface during deserialization error handling. | No directly analogous per-type callback surface of the same kind is in current scope. | No directly analogous built-in per-type error callback surface is in current scope. |
| Interface callbacks | Default object-mode Json.NET does not use `IDeserializationCallback`. | `IDeserializationCallback` is a real formatter callback. | `IDeserializationCallback` is not part of ordinary XmlSerializer behavior. A local repro did not invoke it. |
| Custom XML interface | Default object-mode Json.NET does not use `IXmlSerializable`. | Not applicable. | `IXmlSerializable.ReadXml(XmlReader)` is a real deserialization method. Official docs say it provides custom formatting for serialization/deserialization, and a local repro confirmed constructor followed by `ReadXml`. |
| Object replacement callback | Default object-mode Json.NET does not use `IObjectReference`. | `IObjectReference` is a real formatter callback. | Not part of ordinary XmlSerializer behavior. |
| Finalizers | Not called by Json.NET directly, but GadgetExplorer treats them as post-deserialization trigger surfaces. | Same. | Same. |

GadgetExplorer mapping:
The shipped Json.NET and BinaryFormatter mappings already match these behaviors.
The shipped `XmlSerializer` profile surfaces parameterless constructors, public property setters, and `IXmlSerializable.ReadXml(XmlReader)` with a dedicated `custom-deserialization-method` trigger label.
It does not claim non-public setter support, formatter-style callback attributes, `IDeserializationCallback`, `IObjectReference`, or an XmlSerializer-specific error callback surface.
Finalizers follow the same cross-profile post-deserialization policy as the other shipped profiles.

## What The Shipped Profiles Already Capture Correctly

### JsonDotNet

- public and non-public root admission when type resolution reaches the target type,
- constructor selection via `[JsonConstructor]`, public parameterless, single public parameterized, then non-public parameterless,
- conditional serialization constructors for types that are both `[Serializable]` and `ISerializable`,
- public property setters,
- `[JsonProperty]`- and `[DataMember]`-opted non-public property setters,
- `OnDeserializing` / `OnDeserialized`,
- `OnErrorAttribute`,
- absence of `IDeserializationCallback` / `IObjectReference`,
- finalizer follow-on surfaces.


### BinaryFormatter

- no public-visibility requirement for roots,
- `[Serializable]` root requirement,
- uninitialized-object activation,
- exact-signature `ISerializable` serialization-constructor admission across constructor visibilities through explicit serializer-profile metadata,
- no public property setter trigger channel,
- `OnDeserializing` / `OnDeserialized`,
- `IDeserializationCallback`,
- `IObjectReference`,
- finalizer follow-on surfaces.

### MessagePackTypeless

- public and non-public root admission across the supported declared visibility buckets, including internal and nested non-public contractless roots,
- separate public-only annotated/object-model handling versus contractless allow-private fallback for otherwise unannotated roots,
- constructor-based activation with `SerializationConstructor` precedence,
- best-match constructor selection with name-based contractless binding and index-based support for annotated int-key object models,
- public and non-public property setter triggers for the reachable setter visibilities in the allow-private path,
- `IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()` as a deserialization callback trigger,
- no `OnBeforeSerialize()` trigger surface,
- no field-only trigger modeling,
- and finalizer follow-on surfaces.

### XmlSerializer

- public/publicly visible roots only,
- no `[Serializable]` requirement,
- parameterless-constructor activation via ordered constructor selection,
- public property setters as ordinary method triggers,
- `IXmlSerializable.ReadXml(XmlReader)` as a custom deserialization trigger,
- no formatter-style callback attributes,
- no `IDeserializationCallback`,
- no `IObjectReference`,
- finalizer follow-on surfaces,
- and conservative ordinary-member compatibility filtering for interface-typed and `System.Type` public instance fields or public settable instance properties.

One important GadgetExplorer-facing caveat is worth calling out explicitly: XmlSerializer also populates public fields. That is not a method trigger surface, so it does not change the root/trigger matrix directly, but it means the shipped base `XmlSerializer` profile is still conservative for field-only gadget shapes unless GadgetExplorer grows field modeling later.

The Friday the 13th `ExpandedWrapper` trick does not change the above profile shape. It is a graph-shaping technique that abuses attacker-controlled expected type and XmlSerializer's learned-type rules to widen the set of assignable types. A base `XmlSerializer` profile should therefore model the underlying learned-type mechanisms, not special-case `ExpandedWrapper` itself.

## Current Json.NET Status

No high-confidence missing root restriction, construction rule, or trigger surface has been identified for the current shipped Json.NET profile surface.

There is no remaining shipped Json.NET root-visibility split:

- `JsonDotNet` already admits the non-public roots supported by the runtime evidence.

## Current BinaryFormatter Status

No high-confidence missing root restriction, construction rule, or trigger surface has been identified for the current shipped BinaryFormatter profile surface.

The main BinaryFormatter-specific construction nuance is now explicit in serializer-profile data rather than hidden behind a coarse global rule:

- the shipped `BinaryFormatter` profile models exact-signature serialization-constructor admission across constructor visibilities,
- the constructor-visibility policy remains serializer-specific and configurable,
- and inherited `IDeserializationCallback` / `IObjectReference` expansion is now covered by focused analyzer tests.

## Current XmlSerializer Status

The shipped base `XmlSerializer` profile now models:

- public/publicly visible roots,
- parameterless-constructor activation,
- public property setters,
- `IXmlSerializable.ReadXml(XmlReader)`,
- finalizers,
- conservative ordinary-member compatibility filtering for interface-typed and `System.Type` public member shapes,
- no formatter-style callbacks,
- no `IDeserializationCallback`,
- and no `IObjectReference`.

The shipped base `XmlSerializer` profile does not currently implement:

- field-only trigger surfaces,
- `ExpandedWrapper`-specific logic,
- caller-supplied `extraTypes`,
- caller-supplied `XmlAttributeOverrides`,
- or broader bridge/application wiring behavior.

## Current MessagePackTypeless Status

The shipped `MessagePackTypeless` profile models:

- unsafe typeless MessagePack-CSharp deserialization through `MessagePackSerializer.Typeless`,
- public plus non-public root visibility buckets validated with separate-assembly runtime checks,
- public-only annotated/object-model constructor selection versus contractless allow-private constructor selection,
- `SerializationConstructor` precedence,
- best-match constructor selection,
- public and non-public property setter triggers across the reachable setter visibilities,
- `IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()`,
- finalizers,
- and no `OnBeforeSerialize()` or field-only trigger surface claims.

The shipped `MessagePackTypeless` profile does not currently implement:

- field-only gadget modeling,
- broader custom formatter graph execution,
- resolver-registration inference from application code,
- or hardened-policy variants that intentionally restrict typeless type resolution.

## Things That Are Not Current Gaps

These are intentionally not treated as actionable gaps for this document:

- field restoration details,
- `[NonSerialized]`,
- fixup/callback ordering that only matters if we model state,
- binder and surrogate behavior,
- application-registered `JsonConverter` instances,
- serializer `Error` event wiring,
- and general stateful gadget construction.

## Conclusion

- The useful framing is:
  - root type restrictions,
  - root construction restrictions,
  - and root trigger surfaces.
- For current GadgetExplorer usage, the Json.NET behavior questions raised here are now explicitly covered by shipped profile behavior plus targeted tests.
- `JsonDotNet` now directly reflects the non-public root visibilities supported in the `TypeNameHandling != None` scenario.
- For current GadgetExplorer usage, both Json.NET and BinaryFormatter now have their current root/constructor/trigger behavior expressed through serializer-specific profile data rather than cross-serializer defaults.
- XmlSerializer fits the same matrix framing cleanly:
  - public/publicly visible roots,
  - parameterless-constructor activation,
  - public setters,
  - `IXmlSerializable.ReadXml(XmlReader)` as the main serializer-specific custom method surface,
  - and conservative ordinary-member filtering for interface and `System.Type` member shapes on the ordinary object-mapping path.
- The ExpandedWrapper research is useful because it clarifies XmlSerializer's learned-type boundaries, but it does not imply that a GadgetExplorer XmlSerializer profile should embed wrapper-specific behavior.
