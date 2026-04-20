/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

extern alias newtonsoftjson;
using System.Runtime.Serialization;
using Xunit;
using ErrorContext = newtonsoftjson::Newtonsoft.Json.Serialization.ErrorContext;
using JsonConvert = newtonsoftjson::Newtonsoft.Json.JsonConvert;
using JsonSerializerSettings = newtonsoftjson::Newtonsoft.Json.JsonSerializerSettings;
using OnErrorAttribute = newtonsoftjson::Newtonsoft.Json.Serialization.OnErrorAttribute;
using TypeNameHandling = newtonsoftjson::Newtonsoft.Json.TypeNameHandling;

namespace GadgetExplorer.Tests.Analysis
{
    public sealed class JsonDotNetRuntimeBehaviorTests
    {
        [Fact]
        public void Type_name_handling_can_round_trip_internal_top_level_root_across_assemblies()
        {
            AssertTypeNameHandlingRoundTrip(
                JsonNetVisibilityTargets.CreateInternalTopLevelRoot,
                JsonNetVisibilityTargets.GetInternalTopLevelRootAssemblyQualifiedName,
                "InternalTopLevelRoot");
        }

        [Fact]
        public void Type_name_handling_can_round_trip_protected_nested_root_across_assemblies()
        {
            AssertTypeNameHandlingRoundTrip(
                JsonNetVisibilityTargets.CreateProtectedNestedRoot,
                JsonNetVisibilityTargets.GetProtectedNestedRootAssemblyQualifiedName,
                "VisibilityRootContainer+ProtectedNestedRoot");
        }

        [Fact]
        public void Type_name_handling_can_round_trip_private_nested_root_across_assemblies()
        {
            AssertTypeNameHandlingRoundTrip(
                JsonNetVisibilityTargets.CreatePrivateNestedRoot,
                JsonNetVisibilityTargets.GetPrivateNestedRootAssemblyQualifiedName,
                "VisibilityRootContainer+PrivateNestedRoot");
        }

        [Fact]
        public void Default_json_net_supports_datamember_opted_in_non_public_setters()
        {
            var result = JsonConvert.DeserializeObject<DataMemberPrivateSetterRoot>("{\"Value\":456}");

            Assert.NotNull(result);
            Assert.Equal(456, result!.Value);
        }

        [Fact]
        public void Default_json_net_invokes_on_error_attribute_callbacks()
        {
            OnErrorRoot.CallbackInvoked = false;

            var result = JsonConvert.DeserializeObject<OnErrorRoot>("{\"Value\":\"not-an-int\"}");

            Assert.NotNull(result);
            Assert.True(OnErrorRoot.CallbackInvoked);
        }

        private static void AssertTypeNameHandlingRoundTrip(
            Func<object> factory,
            Func<string> assemblyQualifiedNameFactory,
            string expectedTypeNameSuffix)
        {
            object original = factory();

            AssertRoundTrip(
                original,
                expectedTypeNameSuffix,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

            AssertRoundTrip(
                original,
                expectedTypeNameSuffix,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

            AssertManualPayload(
                assemblyQualifiedNameFactory(),
                expectedTypeNameSuffix,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

            AssertManualPayload(
                assemblyQualifiedNameFactory(),
                expectedTypeNameSuffix,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
        }

        private static void AssertRoundTrip(object original, string expectedTypeNameSuffix, JsonSerializerSettings settings)
        {
            string json = JsonConvert.SerializeObject(original, typeof(object), settings);
            object? roundTrip = JsonConvert.DeserializeObject<object>(json, settings);

            Assert.NotNull(roundTrip);
            Assert.EndsWith(expectedTypeNameSuffix, roundTrip!.GetType().FullName, StringComparison.Ordinal);
        }

        private static void AssertManualPayload(string assemblyQualifiedName, string expectedTypeNameSuffix, JsonSerializerSettings settings)
        {
            string json = "{\"$type\":\"" + EscapeForJson(assemblyQualifiedName) + "\",\"Value\":404}";
            object? result = JsonConvert.DeserializeObject<object>(json, settings);

            Assert.NotNull(result);
            Assert.EndsWith(expectedTypeNameSuffix, result!.GetType().FullName, StringComparison.Ordinal);
        }

        private static string EscapeForJson(string value)
            => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        [DataContract]
        private sealed class DataMemberPrivateSetterRoot
        {
            [DataMember]
            public int Value { get; private set; }
        }

        private sealed class OnErrorRoot
        {
            public static bool CallbackInvoked { get; set; }

            public int Value { get; set; }

            [OnError]
            private void HandleError(StreamingContext context, ErrorContext errorContext)
            {
                CallbackInvoked = true;
                errorContext.Handled = true;
            }
        }
    }
}
