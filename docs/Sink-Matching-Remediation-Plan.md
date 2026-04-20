# Sink Matching Remediation Plan

Date: 2026-04-20

## Purpose

Resolve the shipped sink-matching bug without changing core matching behavior.

Chosen approach:

- keep `AnalysisIndex` exact and simple,
- fix the shipped sink definitions so they already use dnlib-compatible type names,
- remove escaped Unicode marker spellings from the shipped JSON,
- add tests that keep the shipped packs canonical.

This stays intentionally narrow. It does not redesign sink resolution, add loader-side parsing, or broaden matching semantics.

## Confirmed Diagnosis

The mismatch came from shipped sink data, not from dnlib or JSON loading:

- `SinkDefinitionFileLoader` already loads the JSON correctly.
- `AnalysisIndex.MatchesConfiguredTypeName(...)` expects exact dnlib-style type names for exact signature matching.
- Some shipped sink files used friendly generic spellings like `System.ArraySegment<System.Byte>` instead of dnlib-style spellings like ``System.ArraySegment`1<System.Byte>``.
- The same files also contained escaped Unicode spellings like `\u003c`, `\u003e`, and `\u0026`, which are unnecessary and make the packs harder to review.

## Affected Shipped Sink Files

The affected shipped include packs were:

- `src/GadgetExplorer/resources/sinks/filesystem.sinks.json`
- `src/GadgetExplorer/resources/sinks/ssrf.sinks.json`
- `src/GadgetExplorer/resources/sinks/command-execution.sinks.json`

No broader architecture change is required because the bug is fully explained by those shipped definitions being non-canonical.

## Real Missed Sinks

These are concrete shipped examples that can be missed when exact signature matching compares the configured type names against dnlib metadata names.

### Filesystem

1. `src/GadgetExplorer/resources/sinks/filesystem.sinks.json:664`
   Before:
   `System.IO.File::AppendAllBytes(System.String, System.ReadOnlySpan<System.Byte>)`
   Canonical fix:
   ``System.IO.File::AppendAllBytes(System.String, System.ReadOnlySpan`1<System.Byte>)``

2. `src/GadgetExplorer/resources/sinks/filesystem.sinks.json:714`
   Before:
   `System.IO.File::AppendAllLines(System.String, System.Collections.Generic.IEnumerable<System.String>)`
   Canonical fix:
   ``System.IO.File::AppendAllLines(System.String, System.Collections.Generic.IEnumerable`1<System.String>)``

3. `src/GadgetExplorer/resources/sinks/filesystem.sinks.json:818`
   Before:
   `System.IO.File::AppendAllTextAsync(System.String, System.ReadOnlyMemory<System.Char>, System.Text.Encoding, System.Threading.CancellationToken)`
   Canonical fix:
   ``System.IO.File::AppendAllTextAsync(System.String, System.ReadOnlyMemory`1<System.Char>, System.Text.Encoding, System.Threading.CancellationToken)``

### SSRF

4. `src/GadgetExplorer/resources/sinks/ssrf.sinks.json:1035`
   Before:
   `System.Net.Sockets.Socket::Send(System.Collections.Generic.IList<System.ArraySegment<System.Byte>>)`
   Canonical fix:
   ``System.Net.Sockets.Socket::Send(System.Collections.Generic.IList`1<System.ArraySegment`1<System.Byte>>)``

5. `src/GadgetExplorer/resources/sinks/ssrf.sinks.json:1077`
   Before:
   `System.Net.Sockets.Socket::SendAsync(System.ArraySegment<System.Byte>, System.Net.Sockets.SocketFlags, System.Boolean)`
   Canonical fix:
   ``System.Net.Sockets.Socket::SendAsync(System.ArraySegment`1<System.Byte>, System.Net.Sockets.SocketFlags, System.Boolean)``

6. `src/GadgetExplorer/resources/sinks/ssrf.sinks.json:1241`
   Before:
   `System.Net.Sockets.Socket::SendToAsync(System.ArraySegment<System.Byte>, System.Net.Sockets.SocketFlags, System.Net.EndPoint)`
   Canonical fix:
   ``System.Net.Sockets.Socket::SendToAsync(System.ArraySegment`1<System.Byte>, System.Net.Sockets.SocketFlags, System.Net.EndPoint)``

### Command Execution

7. `src/GadgetExplorer/resources/sinks/command-execution.sinks.json:196`
   Before:
   `System.Management.Automation.PowerShell::Invoke(System.Collections.IEnumerable, System.Collections.Generic.IList<T>)`
   Canonical fix:
   ``System.Management.Automation.PowerShell::Invoke(System.Collections.IEnumerable, System.Collections.Generic.IList`1<T>)``

8. `src/GadgetExplorer/resources/sinks/command-execution.sinks.json:238`
   Before:
   `System.Management.Automation.PowerShell::Invoke(System.Management.Automation.PSDataCollection<TInput>, System.Management.Automation.PSDataCollection<TOutput>, System.Management.Automation.PSInvocationSettings)`
   Canonical fix:
   ``System.Management.Automation.PowerShell::Invoke(System.Management.Automation.PSDataCollection`1<TInput>, System.Management.Automation.PSDataCollection`1<TOutput>, System.Management.Automation.PSInvocationSettings)``

## Remediation

### 1. Revert matcher-side normalization

Do not keep generic-string parsing in `AnalysisIndex`.

Reason:

- it pushes config cleanup into the hot path,
- it makes exact matching harder to reason about,
- the shipped packs can be corrected directly.

### 2. Canonicalize the shipped sink definitions

Update the affected JSON entries so that parameter type names use the same shape dnlib reports:

- add generic arity markers such as `` `1 `` where needed,
- preserve by-ref markers like `&`,
- keep arrays and other existing exact names unchanged,
- remove all `\u003c`, `\u003e`, and `\u0026` spellings from the shipped sink JSON.

### 3. Add shipped-pack guardrails

Add tests that:

- prove real canonical filesystem and SSRF sink signatures resolve,
- scan the shipped include sink files for leftover Unicode escape sequences,
- scan the shipped include sink files for leftover friendly generic type spellings without arity markers,
- assert that known corrected examples are present in loaded shipped sink definitions.

## Out Of Scope

The following are intentionally out of scope:

- changing how exact matching works,
- adding loader-side generic normalization,
- redesigning sink configuration formats,
- changing report rendering,
- broad config/schema work beyond these shipped sink packs.

## Expected Outcome

After this change:

- the shipped filesystem generic sinks above resolve with no matcher changes,
- the shipped SSRF generic sinks above resolve with no matcher changes,
- the shipped command-execution generic entries are in canonical form when PowerShell assemblies are present,
- the shipped JSON is readable and free of escaped Unicode type markers,
- future regressions in shipped sink files are caught by tests,
- exact signature matching remains strict and cheap.
