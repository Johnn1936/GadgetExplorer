/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Tests.Fixtures
{
    public sealed class InterfaceStrictContractFixture
    {
        private static readonly SerializerProfile DefaultProfile = SerializerProfiles.Resolve("JsonDotNet");
        private static readonly IReadOnlyDictionary<InterfaceExpansionMode, Lazy<InterfaceStrictContractFixtureData>> SharedByMode =
            Enum.GetValues<InterfaceExpansionMode>()
                .ToDictionary(
                    mode => mode,
                    mode => new Lazy<InterfaceStrictContractFixtureData>(() => CreateData(mode), LazyThreadSafetyMode.ExecutionAndPublication));

        public AnalysisIndex Index => GetIndex();

        public AnalysisIndex GetIndex(InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => GetData(mode).Index;

        public SinkEvaluationResult GetSinkEvaluationResult(SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => GetData(mode).GetSinkEvaluationResult(profile ?? DefaultProfile);

        public ClassFinding? TryGetFinding(string rootClassFullName, SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => GetSinkEvaluationResult(profile, mode).Findings.SingleOrDefault(finding => finding.RootClassFullName == rootClassFullName);

        public TriggerResult? TryGetSetterTrigger(string rootClassFullName, string propertyName, SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => TryGetFinding(rootClassFullName, profile, mode)?
                .TriggerResults
                .SingleOrDefault(trigger => trigger.TriggerMethodDisplay.EndsWith($"::set_{propertyName}(System.Int32)", StringComparison.Ordinal));

        public TriggerResult GetSetterTrigger(string rootClassFullName, string propertyName, SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => TryGetSetterTrigger(rootClassFullName, propertyName, profile, mode)
               ?? throw new InvalidOperationException($"Expected trigger '{rootClassFullName}::{propertyName}' was not present.");

        public bool HasSetterTrigger(string rootClassFullName, string propertyName, SerializerProfile? profile = null, InterfaceExpansionMode mode = InterfaceExpansionMode.Strict)
            => TryGetSetterTrigger(rootClassFullName, propertyName, profile, mode) is not null;

        private static InterfaceStrictContractFixtureData GetData(InterfaceExpansionMode mode)
            => SharedByMode[mode].Value;

        private static InterfaceStrictContractFixtureData CreateData(InterfaceExpansionMode mode)
        {
            var sampleAssemblyPath = Path.GetFullPath(typeof(InterfaceStrictSink).Assembly.Location);
            var assemblies = AssemblyInputLoader.LoadModules(
                [sampleAssemblyPath],
                assemblyResolutionMode: AssemblyResolutionMode.Restricted);
            var index = AnalysisIndex.Build(assemblies, mode);
            return new InterfaceStrictContractFixtureData(index, [new SinkDefinition("InterfaceStrictSink", "Hit")]);
        }

        private sealed class InterfaceStrictContractFixtureData(AnalysisIndex index, IReadOnlyList<SinkDefinition> sinkDefinitions)
        {
            private readonly Dictionary<string, SinkEvaluationResult> _reportsByProfileName = new(StringComparer.Ordinal);

            public AnalysisIndex Index { get; } = index;

            public SinkEvaluationResult GetSinkEvaluationResult(SerializerProfile profile)
            {
                if (_reportsByProfileName.TryGetValue(profile.Name, out SinkEvaluationResult? sinkReport))
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
