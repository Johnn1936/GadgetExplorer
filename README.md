```text
        ██████╗  █████╗ ██████╗  ██████╗ ███████╗████████╗              
       ██╔════╝ ██╔══██╗██╔══██╗██╔════╝ ██╔════╝╚══██╔══╝              
       ▓▓║  ▓▓▓╗▓▓▓▓▓▓▓║▓▓║  ▓▓║▓▓║  ▓▓▓╗▓▓▓▓▓╗     ▓▓║                 
       ▒▒║   ▒▒║▒▒╔══▒▒║▒▒║  ▒▒║▒▒║   ▒▒║▒▒╔══╝     ▒▒║                 
       ╚░░░░░░╔╝░░║  ░░║░░░░░░╔╝╚░░░░░░╔╝░░░░░░░╗   ░░║                 
        ╚═════╝ ╚═╝  ╚═╝╚═════╝  ╚═════╝ ╚══════╝   ╚═╝

███████╗██╗  ██╗██████╗ ██╗      ██████╗ ██████╗ ███████╗██████╗ 
██╔════╝╚██╗██╔╝██╔══██╗██║     ██╔═══██╗██╔══██╗██╔════╝██╔══██╗
▓▓▓▓▓╗   ╚▓▓▓╔╝ ▓▓▓▓▓▓╔╝▓▓║     ▓▓║   ▓▓║▓▓▓▓▓▓╔╝▓▓▓▓▓╗  ▓▓▓▓▓▓╔╝
▒▒╔══╝   ▒▒╔▒▒╗ ▒▒╔═══╝ ▒▒║     ▒▒║   ▒▒║▒▒╔══▒▒╗▒▒╔══╝  ▒▒╔══▒▒╗
░░░░░░░╗░░╔╝ ░░╗░░║     ░░░░░░░╗╚░░░░░░╔╝░░║  ░░║░░░░░░░╗░░║  ░░║
╚══════╝╚═╝  ╚═╝╚═╝     ╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝
```
# GadgetExplorer - .NET Deserialization Gadget Discovery Tool

## Overview

GadgetExplorer is a .NET command-line tool for finding potential deserialization gadget chains in managed applications. It scans one or more assemblies, builds a reachability graph with dispatch and callback heuristics, and reports when a deserialization entrypoint can reach a sink you care about.

The tool is aimed at people doing .NET security review, deserialization research, gadget hunting, secure code review, and targeted triage of suspicious managed applications. It is especially useful when you already know which serializer behavior and sink families you care about and want a faster way to answer: "Could this deserialization entrypoint lead to a usable gadget chain into this sink?"

Out of the box, the shipped sink set contains hundreds of sinks, covering common categories such as:

- File writes and filesystem impact: `System.IO.File.WriteAllText`, `System.IO.File.WriteAllBytes`, `System.IO.FileStream.Write`, `System.IO.Compression.ZipFile.ExtractToDirectory`
- Command or script execution: `System.Diagnostics.Process.Start`, `System.Management.Automation.PowerShell.Invoke`
- SSRF and outbound network access: `System.Net.WebRequest.Create`, `System.Net.WebRequest.GetResponse`, `System.Net.Http.HttpClient.GetAsync`, `System.Net.Http.HttpClient.SendAsync`, `System.Net.WebClient.DownloadString`
- XXE-adjacent XML loading and transform paths: `System.Xml.XmlDocument.LoadXml`
- Reflection and dynamic invocation: `System.Reflection.MethodBase.Invoke`, `System.Reflection.MethodInfo.Invoke`, `System.Type.InvokeMember`, `System.Activator.CreateInstance`
- Chained deserialization: `System.Runtime.Serialization.Formatters.Binary.BinaryFormatter.Deserialize`, `System.Runtime.Serialization.Formatters.Soap.SoapFormatter.Deserialize`, `System.Runtime.Serialization.NetDataContractSerializer.Deserialize`
- Assembly loading and dynamic code loading: `System.Reflection.Assembly.Load`, `System.Reflection.Assembly.LoadFrom`, `System.Reflection.Assembly.UnsafeLoadFrom`

## Installation

Download the latest release archive from this repository's GitHub Releases page, extract it, and keep the contents together.

The release folder is intentionally file-backed:

- `GadgetExplorer.exe`: the scanner executable
- `sinks\*.sinks.json`: the default shipped include-sink pack, grouped by broad vulnerability class
- `ignore-sinks\*.ignore-sinks.json`: the default shipped ignore-sink pack
- `serializer-profiles\*.profile.json`: the shipped built-in serializer profiles

Run the tool from that directory, or invoke `GadgetExplorer.exe` directly while keeping those sidecar folders beside it.

## Build From Source

Restore dependencies:

```text
dotnet restore
```

Build a release binary:

```text
dotnet build .\GadgetExplorer.sln -c Release
```

The build output lands under `artifacts\bin\...`.

## Usage
```text
Usage:

  GadgetExplorer <assembly-or-directory> [options]

Input:

  <assembly-or-directory>
      Assembly file or directory tree to scan.
      Directories are searched recursively for managed assemblies and runtimeconfig files.
      Example: GadgetExplorer "C:\Target\App" -p JsonDotNet
      Note: At least one assembly or directory is required.

Profile:

  -p, --profile [BinaryFormatter | JsonDotNet | JsonDotNetGetters | MessagePackTypeless | PublicTwoStringConstructor | XmlSerializer]
      Use a shipped serializer profile.
      This controls which deserialization trigger policy and activation policies are modeled.
      Required unless --profile-file is used.
      Example: -p JsonDotNet

  -pf, --profile-file <path>
      Use a custom serializer profile JSON file.
      This replaces built-in profile selection for the scan.
      Required unless --profile is used.
      Example: -pf .\Profiles\Custom.profile.json

Sink Configuration:

  -is, --sinks <path>
      Load a custom sink JSON file or a directory of *.sinks.json files.
      This changes which methods count as reportable sinks.
      Default: use the shipped `sinks` directory beside the executable.
      Example: -is .\CustomSinks.json

  -ig, --ignore-sinks <path>
      Load a custom ignore-sink JSON file or a directory of *.ignore-sinks.json files.
      This suppresses configured sink patterns and can reduce noise.
      Default: use the shipped `ignore-sinks` directory beside the executable.
      Example: -ig .\CustomIgnoreSinks.json

Scan Behavior:

  -ie, --interface-expansion [off | strict | broad]
      Control dynamic dispatch handling during graph construction.
      off: only follow interface calls when concrete receiver identity is already known.
      strict: allow strong receiver evidence, but stop when evidence runs out.
      broad: opt into heuristic fallback across compatible implementations.
      Default: strict.
      Example: -ie broad

  -s, --sort [shortest-path | per-sink-shortest-path | type-name]
      Control finding order in the final report.
      shortest-path: shortest paths first globally.
      per-sink-shortest-path: group by sink, then shortest paths first within each sink.
      type-name: stable type-centric ordering by root class identity.
      Default: shortest-path.
      Example: -s per-sink-shortest-path

  -mpl, --max-path-length <n>
      Limit the maximum graph path length from trigger to sink.
      Lower values reduce noise and runtime but hide longer gadget chains.
      Default: unbounded.
      Example: -mpl 8

  -arm, --assembly-resolution-mode [restricted | inference-no-fallback | inference-with-fallback]
      Control how assembly resolution expands beyond the supplied input roots.
      restricted: only resolve assemblies inside the supplied directory tree or beside supplied assembly files.
      inference-no-fallback: infer the target runtime from runtimeconfig files, but stay inside the inputs if inference fails.
      inference-with-fallback: infer the target runtime first, then fall back to the host runtime if inference fails.
      Default: inference-no-fallback.

Output:

  -o, --output <path>
      Write the final report to a file.
      Progress still goes to the console; only the report is redirected.
      The output path does not choose the format; use --output-format for that.
      Default: write the report to stdout.
      Example: -o .\Scan.txt

  -of, --output-format [text | json]
      Control the final report serialization format.
      text: the existing human-readable report.
      json: a structured machine-friendly document with recon metadata and flat ordered findings.
      Default: text.
      Example: -of json
```

Structured JSON output is intended for downstream tooling. It keeps the same rendered finding order as the text report, but emits the scan heading and each finding as typed fields that are easier to filter, sort, and group.

### Built-In Profiles

The shipped built-in profiles are:

- `JsonDotNet`: models the Json.NET `TypeNameHandling != None` scenario with constructor selection, public property setters, `JsonProperty`/`DataMember`-opted non-public setters, deserialization callback attributes including `OnError`, finalizers enabled, and public plus non-public root types when type resolution reaches them.
- `JsonDotNetGetters`: a narrower Json.NET-oriented profile for the same `TypeNameHandling != None` scenario. It disables constructor, setter, callback, and finalizer trigger surfaces and focuses on public property getters while keeping the same root-type visibility coverage as `JsonDotNet`.
- `BinaryFormatter`: models `BinaryFormatter`-style behavior with `[Serializable]` roots, uninitialized object creation, exact-signature `ISerializable` serialization constructors across constructor visibilities, deserialization callback attributes, `IDeserializationCallback`, `IObjectReference`, and finalizers.
- `MessagePackTypeless`: models unsafe MessagePack-CSharp Typeless deserialization through `MessagePackSerializer.Typeless` / `TypelessContractlessStandardResolver`, including annotated and unannotated contractless roots, constructor triggers with `SerializationConstructor` precedence and best-match selection, public plus non-public property setter triggers reachable through the allow-private path, `IMessagePackSerializationCallbackReceiver.OnAfterDeserialize()`, and finalizers. It intentionally does not add field-only trigger modeling or broader custom formatter execution.
- `XmlSerializer`: models publicly visible roots, parameterless-constructor activation, public property setters, `IXmlSerializable.ReadXml(XmlReader)`, finalizers enabled, conservative ordinary-member compatibility filtering for interface and `System.Type` member shapes, and no formatter-style callbacks.
- `PublicTwoStringConstructor`: a narrow research profile for objects reached through a publicly visible type with a public constructor taking exactly two `System.String` parameters, with finalizers enabled and setter/callback behavior disabled.

### Sink Files

- `sinks\*.sinks.json`: define which methods count as sinks worth reporting.
- `ignore-sinks\*.ignore-sinks.json`: define sink patterns that should be ignored or treated as slice boundaries.

Both file types use a JSON document with a top-level `sinks` array. The `--sinks` and `--ignore-sinks` options accept either one JSON file or one directory. When a directory is supplied, GadgetExplorer reads only the top directory, loads matching files, sorts them by file name using ordinal comparison, and concatenates their `sinks` arrays deterministically.

Include-sink files can be broad or signature-specific. The richer `parameters` form lets each parameter independently opt into constant-argument suppression:

```json
{
  "sinks": [
    {
      "declaringType": "System.Xml.XmlDocument",
      "methodName": "Load",
      "parameters": [
        {
          "typeName": "System.String",
          "ignoreSinkIfConstant": true
        }
      ]
    }
  ]
}
```

`ignoreSinkIfConstant` applies to include-sink definitions only. It means "do not report this sink if GadgetExplorer concludes that the value passed to this parameter is constant at the call site." This is useful for reducing noise from APIs such as `XmlDocument.Load(string)` when the XML path is a hard-coded local configuration file. That determination is heuristic rather than perfect, so constant detection should be treated as a noise-reduction aid, not as proof that a path is safe.

Ignore-sink files do not use `ignoreSinkIfConstant`. They simply describe sink patterns that should be ignored or used as slice boundaries.

## Examples

Scan a single assembly with the built-in Json.NET profile:

```text
.\GadgetExplorer.exe .\App.dll -p JsonDotNet -o .\Scan-JsonDotNet.txt
```

Use the shipped sink directories by default:

```text
.\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -o .\Scan-DefaultSinks.txt
```

Scan a directory tree with the built-in BinaryFormatter profile using  a max path length of 12:

```text
.\GadgetExplorer.exe "C:\Target\App" -p BinaryFormatter -mpl 12 -o .\Scan-BinaryFormatter.txt
```

Scan a directory tree with the built-in XmlSerializer profile:

```text
.\GadgetExplorer.exe "C:\Target\App" -p XmlSerializer -o .\Scan-XmlSerializer.txt
```

Scan a directory tree with the built-in MessagePack Typeless profile and a max path length of 8:

```text
.\GadgetExplorer.exe "C:\Target\App" -p MessagePackTypeless -mpl 8 -o .\Scan-MessagePackTypeless.txt
```

Use the built-in Json.NET profile with a custom sink file, a custom ignore-sink file, and `type-name` sorting:

```text
.\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -is .\CustomIncludeSinks.json -ig .\CustomIgnoreSinks.json -s type-name -o .\Scan-CustomSinks-TypeName.txt
```

Use the built-in Json.NET profile with a custom sink directory:

```text
.\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -is .\CustomSinks -o .\Scan-CustomSinkDirectory.txt
```

Write the structured JSON report to disk for downstream processing:

```text
.\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -of json -o .\Scan-JsonDotNet.json
```

Infer the target runtime from runtimeconfig files, but stay inside the supplied inputs if inference fails:

```text
.\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -arm inference-no-fallback -o .\Scan-InferenceNoFallback.txt
```

Infer the target runtime first, then allow host-runtime fallback if inference fails:

```text
.\GadgetExplorer.exe "C:\Target\App" -p JsonDotNet -arm inference-with-fallback -o .\Scan-InferenceWithFallback.txt
```

Use a built-in profile with explicit interface handling, sorting, and max path length:

```text
.\GadgetExplorer.exe .\App.dll -p JsonDotNet -arm restricted -ie broad -s shortest-path -mpl 8 -o .\Scan-Restricted-Broad.txt
```

Use a custom serializer profile file, a custom sink file, custom ignore sinks, sort by per-sink shortest path, and cap paths at 12:

```text
.\GadgetExplorer.exe "C:\Target\App" -pf .\Profiles\Custom.profile.json -is .\CustomIncludeSinks.json -ig .\CustomIgnoreSinks.json -s per-sink-shortest-path -mpl 12 -o .\Scan-Custom.txt
```

## Sample Findings

The examples below are representative real chains. Their formatting has been reviewed against the current text report layout so labels such as `Assembly`, `Declared On`, `Note`, edge kinds, and `Origin=input-root` still match the current renderer.

### Arbitrary Method Invocations

```text
System.Windows.Data.ObjectDataProvider, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35
  Assembly: C:\Example\AppA\PresentationFramework.dll (AssemblyVersion=4.0.0.0, FileVersion=4.800.122.15205, Origin=input-root)
    System.Windows.Data.ObjectDataProvider::set_MethodName(System.String)
      -> [DirectCall] System.Windows.Data.DataSourceProvider::Refresh()
      -> [VirtualDispatch] System.Windows.Data.ObjectDataProvider::BeginQuery()
      -> [DirectCall] System.Windows.Data.ObjectDataProvider::QueryWorker(System.Object)
      -> [DirectCall] System.Windows.Data.ObjectDataProvider::InvokeMethodOnInstance(System.Exception&)
      -> [DirectCall] System.Type::InvokeMember(System.String, System.Reflection.BindingFlags, System.Reflection.Binder, System.Object, System.Object[], System.Globalization.CultureInfo)
```

### Arbitrary Getters

```text
System.Windows.Forms.BindingSource, System.Windows.Forms, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
  Assembly: C:\Example\AppB\System.Windows.Forms.dll (AssemblyVersion=9.0.0.0, FileVersion=9.0.1326.6403, Origin=input-root)
    System.Windows.Forms.BindingSource::set_DataMember(System.String)
      -> [DirectCall] System.Windows.Forms.BindingSource::ResetList()
      -> [DirectCall] System.Windows.Forms.ListBindingHelper::GetList(System.Object, System.String)
      -> [VirtualDispatch] System.ComponentModel.ReflectPropertyDescriptor::GetValue(System.Object)
      -> [DirectCall] System.Reflection.MethodBase::Invoke(System.Object, System.Object[])
```

### BinaryFormatter Bridges

```text
System.Security.Principal.GenericIdentity, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
  Assembly: C:\Example\AppC\Managed\mscorlib.dll (AssemblyVersion=4.0.0.0, FileVersion=4.6.57.0, Origin=input-root)
  Declared On: System.Security.Claims.ClaimsIdentity
  Note: Inherited deserialization callback declared on System.Security.Claims.ClaimsIdentity.
    System.Security.Claims.ClaimsIdentity::OnDeserializedMethod(System.Runtime.Serialization.StreamingContext)
      -> [DirectCall] System.Security.Claims.ClaimsIdentity::DeserializeClaims(System.String)
      -> [DirectCall] System.Runtime.Serialization.Formatters.Binary.BinaryFormatter::Deserialize(System.IO.Stream, System.Runtime.Remoting.Messaging.HeaderHandler, System.Boolean)
```

### SSRF

```text
System.Drawing.Bitmap, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
  Assembly: C:\Example\AppC\Managed\System.Drawing.dll (AssemblyVersion=4.0.0.0, FileVersion=4.6.57.0, Origin=input-root)
  Declared On: System.Drawing.Image
  Note: Inherited finalizer declared on System.Drawing.Image.
    System.Drawing.Image::Finalize()
      -> [VirtualDispatch] System.Drawing.Image::Dispose(System.Boolean)
      -> [DirectCall] System.IO.Stream::Dispose()
      -> [VirtualDispatch] System.IO.Stream::Close()
      -> [VirtualDispatch] System.Net.WebClient+WebClientWriteStream::Dispose(System.Boolean)
      -> [VirtualDispatch] System.Net.WebClient::GetWebResponse(System.Net.WebRequest)
      -> [VirtualDispatch] System.Net.HttpWebRequest::GetResponse()
```

### Arbitrary Assembly Load

```text
System.CodeDom.Compiler.CompilerResults, System.CodeDom, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
  Assembly: C:\Example\AppD\System.CodeDom.dll (AssemblyVersion=9.0.0.0, FileVersion=9.0.325.11113, Origin=input-root)
    System.CodeDom.Compiler.CompilerResults::get_CompiledAssembly()
      -> [DirectCall] System.Reflection.Assembly::LoadFile(System.String)
```

## Limitations

GadgetExplorer is not a general-purpose taint engine and it is not a push-button exploitability oracle. It is a researcher-assisted tool that surfaces candidate deserialization gadget chains for further manual analysis.

Current implementation limits include:

- It does not track object state through gadget chains. In practice, that means no general field/property taint propagation across object population steps.
- It does not perform full symbolic execution or path-sensitive branching analysis.
- It does not model arbitrary reflection semantics beyond the graph edges present in the loaded code.
- It does not model arbitrary event semantics. It handles the common observed subscription-and-raise pattern, not every possible event usage.
- Interface dispatch is modeled carefully, but still heuristically. Missing metadata, facade/reference mismatches, or incomplete receiver information can still produce under- or over-approximation.
- Restricted-mode results depend on what gets loaded. If you omit assemblies, the graph can miss paths. If you allow runtime inference and fallback, the graph can grow noisier.
- `--assembly-resolution-mode restricted` improves containment and repeatability, but it can also hide real paths that rely on runtime/framework assemblies outside the supplied tree.
- The default `inference-no-fallback` mode usually gives broader framework-aware coverage while still avoiding host-runtime fallback when inference fails.
- `--interface-expansion off` keeps only interface calls backed by concrete receiver identity, so subtype-constraint and exploratory fallback paths disappear.
- `--interface-expansion broad` enables exploratory fallback heuristics, which can surface additional paths but is intentionally noisier than `strict`.

## License
```
GPLv3
```
## Copyright
```
Copyright (C) 2026 Dane Evans
```
