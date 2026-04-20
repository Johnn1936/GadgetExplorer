/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Tests.Fixtures
{
    public sealed class SmokeSampleAnalysisFixture
    {
        private static readonly SerializerProfile DefaultProfile = SerializerProfiles.Resolve("JsonDotNet");
        private static readonly IReadOnlyDictionary<InterfaceExpansionMode, Lazy<SmokeSampleAnalysisFixtureData>> SharedByMode =
            Enum.GetValues<InterfaceExpansionMode>()
                .ToDictionary(
                    mode => mode,
                    mode => new Lazy<SmokeSampleAnalysisFixtureData>(() => CreateData(mode), LazyThreadSafetyMode.ExecutionAndPublication));

        public AnalysisIndex Index => GetIndex();
        public SinkEvaluationResult SinkEvaluationResult => GetSinkEvaluationResult();

        public AnalysisIndex GetIndex(InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => GetData(mode).Index;

        public SinkEvaluationResult GetSinkEvaluationResult(SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => GetData(mode).GetSinkEvaluationResult(profile ?? DefaultProfile);

        public ClassFinding? TryGetFinding(string rootClassFullName, SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => GetSinkEvaluationResult(profile, mode).Findings.SingleOrDefault(finding => finding.RootClassFullName == rootClassFullName);

        public ClassFinding GetFinding(string rootClassFullName, SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => TryGetFinding(rootClassFullName, profile, mode)
               ?? throw new InvalidOperationException($"Expected finding for root '{rootClassFullName}' was not present.");

        public TriggerResult GetSingleTrigger(string rootClassFullName, SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
        {
            var finding = GetFinding(rootClassFullName, profile, mode);
            return finding.TriggerResults.Count switch
            {
                1 => finding.TriggerResults[0],
                _ => throw new InvalidOperationException($"Expected exactly one trigger for '{rootClassFullName}', but found {finding.TriggerResults.Count}.")
            };
        }

        public bool HasFinding(string rootClassFullName, SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => TryGetFinding(rootClassFullName, profile, mode) is not null;

        private static SmokeSampleAnalysisFixtureData GetData(InterfaceExpansionMode mode)
            => SharedByMode[mode].Value;

        private static SmokeSampleAnalysisFixtureData CreateData(InterfaceExpansionMode mode)
        {
            var sampleAssemblyPath = Path.GetFullPath(typeof(MySpecialObject).Assembly.Location);
            var assemblies = AssemblyInputLoader.LoadModules(
                [sampleAssemblyPath],
                assemblyResolutionMode: AssemblyResolutionMode.Restricted);
            var index = AnalysisIndex.Build(assemblies, mode);
            return new SmokeSampleAnalysisFixtureData(index, [new SinkDefinition("MySpecialObject", "SayHello")]);
        }

        private sealed class SmokeSampleAnalysisFixtureData(AnalysisIndex index, IReadOnlyList<SinkDefinition> sinkDefinitions)
        {
            private readonly Dictionary<string, SinkEvaluationResult> _reportsByProfileName = new(StringComparer.Ordinal);

            public AnalysisIndex Index { get; } = index;

            public SinkEvaluationResult GetSinkEvaluationResult(SerializerProfile profile)
            {
                if (_reportsByProfileName.TryGetValue(profile.Name, out var sinkReport))
                {
                    return sinkReport;
                }

                sinkReport = SinkAnalyzer.Analyze(Index, sinkDefinitions, [], profile).SinkEvaluationResults.Single();
                _reportsByProfileName[profile.Name] = sinkReport;
                return sinkReport;
            }
        }
    }

}
