/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using Xunit.Abstractions;
using Xunit.Sdk;

namespace GadgetExplorer.Tests.TestSupport
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestPriorityAttribute(int priority) : Attribute
    {
        public int Priority { get; } = priority;
    }

    public sealed class PriorityOrderer : ITestCaseOrderer
    {
        public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
            where TTestCase : ITestCase
            => testCases
                .OrderBy(testCase => GetPriority(testCase))
                .ThenBy(testCase => testCase.TestMethod.Method.Name, StringComparer.Ordinal);

        private static int GetPriority(ITestCase testCase)
        {
            IAttributeInfo? priorityAttribute = testCase.TestMethod.Method
                .GetCustomAttributes(typeof(TestPriorityAttribute).AssemblyQualifiedName!)
                .FirstOrDefault();

            return priorityAttribute?.GetNamedArgument<int>(nameof(TestPriorityAttribute.Priority)) ?? 0;
        }
    }
}
