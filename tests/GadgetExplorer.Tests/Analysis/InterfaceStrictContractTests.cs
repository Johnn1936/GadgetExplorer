/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    [Collection(InterfaceStrictContractCollection.Name)]
    [Trait("Suite", "InterfaceStrictContract")]
    public sealed class InterfaceStrictContractTests(InterfaceStrictContractFixture fixture) : InterfaceStrictContractTestBase
    {
        [InterfaceStrictContractFact]
        public void Strict_mode_keeps_finite_candidate_interface_sets()
        {
            TriggerResult trigger = fixture.GetSetterTrigger("Case03FiniteCandidateSetRoot", "PositiveA", JsonDotNet, InterfaceExpansionMode.Strict);

            AssertHasInterfaceDispatchTarget(
                fixture,
                trigger,
                InterfaceExpansionMode.Strict,
                "JsonWorker::Execute()",
                "XmlWorker::Execute()");
        }

        [InterfaceStrictContractFact]
        public void Strict_mode_rejects_unmodeled_opaque_interface_factories()
        {
            Assert.False(fixture.HasSetterTrigger("Case19OpaqueFactoryRoot", "NegativeA", JsonDotNet, InterfaceExpansionMode.Strict));
        }

        [InterfaceStrictContractFact]
        public void Broad_mode_surfaces_opaque_interface_factory_noise_only_when_opted_in()
        {
            TriggerResult trigger = fixture.GetSetterTrigger("Case19OpaqueFactoryRoot", "NegativeA", JsonDotNet, InterfaceExpansionMode.Broad);
            AssertHasInterfaceDispatchTarget(
                fixture,
                trigger,
                InterfaceExpansionMode.Broad,
                "JsonWorker::Execute()",
                "XmlWorker::Execute()",
                "FlatWorker::Execute()");
        }

        [InterfaceStrictContractTheory]
        [InlineData("Case05OneHopParameterRelayRoot", "NegativeA")]
        [InlineData("Case06ReturnRelayRoot", "NegativeC")]
        [InlineData("Case08AliasPreservingRelayRoot", "NegativeB")]
        [InlineData("Case08AliasPreservingRelayRoot", "NegativeC")]
        [InlineData("Case09InstanceFieldRelayRoot", "NegativeB")]
        [InlineData("Case10StaticFieldRelayRoot", "NegativeB")]
        [InlineData("Case11TransparentWrapperRoot", "NegativeA")]
        [InlineData("Case16FiniteFactoryRoot", "NegativeB")]
        [InlineData("Case19OpaqueFactoryRoot", "NegativeB")]
        [InlineData("Case19OpaqueFactoryRoot", "NegativeC")]
        public void Strict_mode_rejects_curated_impossible_or_silent_transport_cases(string rootClassFullName, string propertyName)
        {
            Assert.False(fixture.HasSetterTrigger(rootClassFullName, propertyName, JsonDotNet, InterfaceExpansionMode.Strict));
        }
    }
}
