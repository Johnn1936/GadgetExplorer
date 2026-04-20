# Contributing

Thanks for your interest in improving `GadgetExplorer`.

## Workflow

1. Fork the repository.
2. Create a branch for your change.
3. Make your changes.
4. Build and run the tests:
   - `dotnet build .\GadgetExplorer.sln`
   - `dotnet test .\tests\GadgetExplorer.Tests\GadgetExplorer.Tests.csproj`
5. If you change the scanning engine, sink matching, profiles, or reporting behavior, run a before/after comparison on a larger real-world codebase and make sure expected findings are not unexpectedly lost.
6. Push your branch to your fork.
7. Open a pull request with a short summary of the change and any relevant test or scan notes.

## Guidelines

- Keep changes small and focused.
- Follow the existing code style and project structure.
- Prefer the narrowest layer that owns the behavior you are changing.
- Add or update tests when behavior changes.
- If you change loading, indexing, dispatch, sink matching, profiles, or reporting, include targeted tests and then run the broader relevant test command before finishing.
