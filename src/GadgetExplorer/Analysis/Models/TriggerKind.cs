/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    public enum TriggerKind
    {
        Constructor,
        PublicPropertyGetter,
        PublicPropertySetter,
        NonPublicPropertySetter,
        DeserializationCallback,
        CustomDeserializationMethod,
        Finalizer
    }
}
