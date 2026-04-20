/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    public abstract class InterfaceStrictContractTestBase
    {
        protected static readonly SerializerProfile JsonDotNet = SerializerProfiles.Resolve("JsonDotNet");

        protected static void AssertHasInterfaceDispatchTarget(
            InterfaceStrictContractFixture fixture,
            TriggerResult trigger,
            params string[] expectedTargetSuffixes)
            => AssertHasInterfaceDispatchTarget(fixture, trigger, InterfaceExpansionMode.Strict, expectedTargetSuffixes);

        protected static void AssertHasInterfaceDispatchTarget(
            InterfaceStrictContractFixture fixture,
            TriggerResult trigger,
            InterfaceExpansionMode mode,
            params string[] expectedTargetSuffixes)
        {
            AnalysisIndex index = fixture.GetIndex(mode);
            Assert.Contains(
                trigger.ReachabilityPath,
                edge => edge.Kind == EdgeKind.InterfaceDispatch &&
                        expectedTargetSuffixes.Any(expectedSuffix =>
                            index.GetMethod(edge.TargetId).DisplayName.EndsWith(expectedSuffix, StringComparison.Ordinal)));
        }
    }
}
