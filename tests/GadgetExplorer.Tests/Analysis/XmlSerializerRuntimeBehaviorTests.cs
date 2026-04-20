/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Xml.Serialization;
using Xunit;

namespace GadgetExplorer.Tests.Analysis
{
    public sealed class XmlSerializerRuntimeBehaviorTests
    {
        [Fact]
        public void Public_top_level_root_can_round_trip_across_assemblies()
        {
            var result = Assert.IsType<XmlSerializerRuntimePublicTopLevelRoot>(
                Deserialize(
                    typeof(XmlSerializerRuntimePublicTopLevelRoot),
                    "<XmlSerializerRuntimePublicTopLevelRoot><Value>123</Value></XmlSerializerRuntimePublicTopLevelRoot>"));

            Assert.Equal(123, result.Value);
        }

        [Fact]
        public void Public_nested_root_can_round_trip_across_assemblies()
        {
            var result = Assert.IsType<XmlSerializerRuntimePublicRootContainer.PublicNestedRoot>(
                Deserialize(
                    typeof(XmlSerializerRuntimePublicRootContainer.PublicNestedRoot),
                    "<PublicNestedRoot><Value>456</Value></PublicNestedRoot>"));

            Assert.Equal(456, result.Value);
        }

        [Fact]
        public void Public_nested_root_inside_internal_container_is_inaccessible_across_assemblies()
            => AssertInaccessibleRootFailure(XmlSerializerRuntimeTargets.GetPublicNestedRootInsideInternalContainerType());

        [Fact]
        public void Internal_root_is_inaccessible_across_assemblies()
            => AssertInaccessibleRootFailure(XmlSerializerRuntimeTargets.GetInternalRootType());

        [Fact]
        public void Protected_nested_root_is_inaccessible_across_assemblies()
            => AssertInaccessibleRootFailure(XmlSerializerRuntimeTargets.GetProtectedNestedRootType());

        [Fact]
        public void Private_nested_root_is_inaccessible_across_assemblies()
            => AssertInaccessibleRootFailure(XmlSerializerRuntimeTargets.GetPrivateNestedRootType());

        [Fact]
        public void Public_root_with_private_parameterless_constructor_succeeds()
        {
            var result = Assert.IsType<XmlSerializerRuntimePrivateConstructorRoot>(
                Deserialize(
                    typeof(XmlSerializerRuntimePrivateConstructorRoot),
                    "<XmlSerializerRuntimePrivateConstructorRoot><Value>789</Value></XmlSerializerRuntimePrivateConstructorRoot>"));

            Assert.Equal(789, result.Value);
        }

        [Fact]
        public void Root_with_property_lacking_a_public_setter_fails()
        {
            Assert.Throws<InvalidOperationException>(() => new XmlSerializer(typeof(XmlSerializerRuntimePrivateSetterRoot)));
        }

        [Fact]
        public void Ixmlserializable_runs_constructor_then_readxml()
        {
            XmlSerializerRuntimeTargets.ResetCustomReadXmlSteps();

            _ = Deserialize(
                typeof(XmlSerializerRuntimeCustomReadXmlRoot),
                "<XmlSerializerRuntimeCustomReadXmlRoot />");

            Assert.Equal(["constructor", "ReadXml"], XmlSerializerRuntimeTargets.GetCustomReadXmlSteps());
        }

        [Fact]
        public void On_deserialized_attribute_is_not_invoked()
        {
            XmlSerializerRuntimeTargets.ResetOnDeserializedState();

            var result = Assert.IsType<XmlSerializerRuntimeOnDeserializedRoot>(
                Deserialize(
                    typeof(XmlSerializerRuntimeOnDeserializedRoot),
                    "<XmlSerializerRuntimeOnDeserializedRoot><Value>11</Value></XmlSerializerRuntimeOnDeserializedRoot>"));

            Assert.Equal(11, result.Value);
            Assert.False(XmlSerializerRuntimeTargets.GetOnDeserializedState());
        }

        [Fact]
        public void Ideserializationcallback_is_not_invoked()
        {
            XmlSerializerRuntimeTargets.ResetInterfaceCallbackState();

            var result = Assert.IsType<XmlSerializerRuntimeInterfaceCallbackRoot>(
                Deserialize(
                    typeof(XmlSerializerRuntimeInterfaceCallbackRoot),
                    "<XmlSerializerRuntimeInterfaceCallbackRoot><Value>22</Value></XmlSerializerRuntimeInterfaceCallbackRoot>"));

            Assert.Equal(22, result.Value);
            Assert.False(XmlSerializerRuntimeTargets.GetInterfaceCallbackState());
        }

        [Fact]
        public void Interface_typed_public_member_root_is_rejected_during_serializer_construction()
        {
            Assert.Throws<InvalidOperationException>(() => new XmlSerializer(typeof(XmlSerializerRuntimeInterfaceMemberRoot)));
        }

        [Fact]
        public void System_type_public_member_root_is_not_treated_as_a_supported_ordinary_deserialization_path()
        {
            XmlSerializer? serializer = null;
            Exception? constructionException = Record.Exception(() => serializer = new XmlSerializer(typeof(XmlSerializerRuntimeTypeMemberRoot)));
            if (constructionException is not null)
            {
                Assert.IsAssignableFrom<InvalidOperationException>(constructionException);
                return;
            }

            string xml = "<XmlSerializerRuntimeTypeMemberRoot><Value>System.String</Value></XmlSerializerRuntimeTypeMemberRoot>";

            object? result = null;
            Exception? exception = Record.Exception(() => result = Deserialize(serializer!, xml));
            if (exception is not null)
            {
                Assert.IsAssignableFrom<InvalidOperationException>(exception);
                return;
            }

            Assert.NotNull(result);
            Assert.Null(Assert.IsType<XmlSerializerRuntimeTypeMemberRoot>(result).Value);
        }

        private static void AssertInaccessibleRootFailure(Type rootType)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new XmlSerializer(rootType));

            Assert.Contains(rootType.Name, exception.ToString(), StringComparison.Ordinal);
            Assert.True(
                exception.ToString().Contains("inaccessible", StringComparison.OrdinalIgnoreCase) ||
                exception.ToString().Contains("not accessible", StringComparison.OrdinalIgnoreCase),
                exception.ToString());
        }

        private static object Deserialize(Type rootType, string xml)
            => Deserialize(new XmlSerializer(rootType), xml);

        private static object Deserialize(XmlSerializer serializer, string xml)
        {
            using var reader = new StringReader(xml);
            return serializer.Deserialize(reader) ?? throw new InvalidOperationException("XmlSerializer returned null.");
        }
    }
}
