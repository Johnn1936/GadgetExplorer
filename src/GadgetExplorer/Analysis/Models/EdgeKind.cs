/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

namespace GadgetExplorer.Analysis.Models
{
    public enum EdgeKind
    {
        DirectCall,
        ConstructorCall,
        PropertyAccessor,
        VirtualDispatch,
        InterfaceDispatch,
        DelegateInvoke,
        EventAccessor,
        EventRaise,
        AsyncIterator
    }
}

