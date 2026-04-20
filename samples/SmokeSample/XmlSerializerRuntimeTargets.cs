/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

public interface IXmlSerializerRuntimeUnsupportedMemberContract
{
}

public static class XmlSerializerRuntimeTargets
{
    public static System.Type GetInternalRootType() => typeof(XmlSerializerRuntimeInternalRoot);

    public static System.Type GetProtectedNestedRootType() => XmlSerializerRuntimeVisibilityContainer.GetProtectedNestedRootType();

    public static System.Type GetPrivateNestedRootType() => XmlSerializerRuntimeVisibilityContainer.GetPrivateNestedRootType();

    public static System.Type GetPublicNestedRootInsideInternalContainerType() => XmlSerializerRuntimeInternalVisibilityContainer.GetPublicNestedRootType();

    public static void ResetCustomReadXmlSteps() => XmlSerializerRuntimeCustomReadXmlRoot.Reset();

    public static IReadOnlyList<string> GetCustomReadXmlSteps() => XmlSerializerRuntimeCustomReadXmlRoot.GetSteps();

    public static void ResetOnDeserializedState() => XmlSerializerRuntimeOnDeserializedRoot.CallbackInvoked = false;

    public static bool GetOnDeserializedState() => XmlSerializerRuntimeOnDeserializedRoot.CallbackInvoked;

    public static void ResetInterfaceCallbackState() => XmlSerializerRuntimeInterfaceCallbackRoot.CallbackInvoked = false;

    public static bool GetInterfaceCallbackState() => XmlSerializerRuntimeInterfaceCallbackRoot.CallbackInvoked;
}

public sealed class XmlSerializerRuntimePublicTopLevelRoot
{
    public int Value { get; set; }
}

public sealed class XmlSerializerRuntimePublicRootContainer
{
    public sealed class PublicNestedRoot
    {
        public int Value { get; set; }
    }
}

internal sealed class XmlSerializerRuntimeInternalRoot
{
    public int Value { get; set; }
}

public class XmlSerializerRuntimeVisibilityContainer
{
    public static System.Type GetProtectedNestedRootType() => typeof(ProtectedNestedRoot);

    public static System.Type GetPrivateNestedRootType() => typeof(PrivateNestedRoot);

    protected sealed class ProtectedNestedRoot
    {
        public int Value { get; set; }
    }

    private sealed class PrivateNestedRoot
    {
        public int Value { get; set; }
    }
}

internal static class XmlSerializerRuntimeInternalVisibilityContainer
{
    public static System.Type GetPublicNestedRootType() => typeof(PublicNestedRoot);

    public sealed class PublicNestedRoot
    {
        public int Value { get; set; }
    }
}

public sealed class XmlSerializerRuntimePrivateConstructorRoot
{
    private XmlSerializerRuntimePrivateConstructorRoot()
    {
    }

    public int Value { get; set; }
}

public sealed class XmlSerializerRuntimePrivateSetterRoot
{
    public int Value { get; private set; }
}

public sealed class XmlSerializerRuntimeCustomReadXmlRoot : System.Xml.Serialization.IXmlSerializable
{
    private static readonly List<string> s_steps = [];

    public XmlSerializerRuntimeCustomReadXmlRoot()
    {
        s_steps.Add("constructor");
    }

    public static IReadOnlyList<string> GetSteps() => [.. s_steps];

    public static void Reset() => s_steps.Clear();

    public System.Xml.Schema.XmlSchema? GetSchema() => null;

    public void ReadXml(System.Xml.XmlReader reader)
    {
        s_steps.Add("ReadXml");
        reader.MoveToContent();
        reader.ReadStartElement();
    }

    public void WriteXml(System.Xml.XmlWriter writer)
    {
    }
}

public sealed class XmlSerializerRuntimeOnDeserializedRoot
{
    public static bool CallbackInvoked { get; set; }

    public int Value { get; set; }

    [System.Runtime.Serialization.OnDeserialized]
    private void AfterDeserialize(System.Runtime.Serialization.StreamingContext context)
    {
        CallbackInvoked = true;
    }
}

public sealed class XmlSerializerRuntimeInterfaceCallbackRoot : System.Runtime.Serialization.IDeserializationCallback
{
    public static bool CallbackInvoked { get; set; }

    public int Value { get; set; }

    public void OnDeserialization(object? sender)
    {
        CallbackInvoked = true;
    }
}

public sealed class XmlSerializerRuntimeInterfaceMemberRoot
{
    public IXmlSerializerRuntimeUnsupportedMemberContract? Adapter { get; set; }
}

public sealed class XmlSerializerRuntimeTypeMemberRoot
{
    public System.Type? Value { get; set; }
}
