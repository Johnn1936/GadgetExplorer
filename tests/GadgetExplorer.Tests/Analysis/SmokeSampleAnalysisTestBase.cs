/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    public abstract class SmokeSampleAnalysisTestBase
    {
        protected static readonly SerializerProfile JsonDotNet = SerializerProfiles.Resolve("JsonDotNet");
        protected static readonly SerializerProfile JsonDotNetGetters = SerializerProfiles.Resolve("JsonDotNetGetters");
        protected static readonly SerializerProfile PublicTwoStringConstructor = SerializerProfiles.Resolve("PublicTwoStringConstructor");
        protected static readonly SerializerProfile BinaryFormatter = SerializerProfiles.Resolve("BinaryFormatter");
        protected static readonly SerializerProfile MessagePackTypeless = SerializerProfiles.Resolve("MessagePackTypeless");
        protected static readonly SerializerProfile XmlSerializer = SerializerProfiles.Resolve("XmlSerializer");

        protected static void AssertEdge(SmokeSampleAnalysisFixture fixture, EdgeRecord edge, EdgeKind expectedKind, string expectedDisplaySuffix)
            => AssertEdge(fixture.Index, edge, expectedKind, expectedDisplaySuffix);

        protected static void AssertEdge(AnalysisIndex index, EdgeRecord edge, EdgeKind expectedKind, string expectedDisplaySuffix)
        {
            Assert.Equal(expectedKind, edge.Kind);
            Assert.EndsWith(expectedDisplaySuffix, index.GetMethod(edge.TargetId).DisplayName, StringComparison.Ordinal);
        }
    }
}
