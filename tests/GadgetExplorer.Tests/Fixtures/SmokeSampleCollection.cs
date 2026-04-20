/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit;

namespace GadgetExplorer.Tests.Fixtures
{
    [CollectionDefinition(Name)]
    public sealed class SmokeSampleCollection : ICollectionFixture<SmokeSampleAnalysisFixture>
    {
        public const string Name = "Smoke sample analysis";
    }
}
