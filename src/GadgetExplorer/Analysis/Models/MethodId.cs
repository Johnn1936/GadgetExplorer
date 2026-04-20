/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Globalization;

namespace GadgetExplorer.Analysis.Models
{
    public readonly record struct MethodId(int Value)
    {
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    }
}

