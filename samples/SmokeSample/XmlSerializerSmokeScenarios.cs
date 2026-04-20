/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

public interface IXmlSerializerUnsupportedMemberContract
{
    void Execute();
}

public sealed class XmlSerializerPublicParameterlessPositive
{
    public XmlSerializerPublicParameterlessPositive()
    {
        Helper.InvokeSink();
    }
}

public sealed class XmlSerializerPrivateParameterlessPositive
{
    private XmlSerializerPrivateParameterlessPositive()
    {
        Helper.InvokeSink();
    }
}

public sealed class XmlSerializerPublicSetterPositive
{
    public int Value
    {
        set
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class XmlSerializerNonPublicSetterNegative
{
    private int _value;

    public int Value
    {
        get => _value;
        private set
        {
            _value = value;
            Helper.InvokeSink();
        }
    }
}

public sealed class XmlSerializerCustomReadXmlPositive : System.Xml.Serialization.IXmlSerializable
{
    public System.Xml.Schema.XmlSchema? GetSchema() => null;

    public void ReadXml(System.Xml.XmlReader reader)
    {
        Helper.InvokeSink();
    }

    public void WriteXml(System.Xml.XmlWriter writer)
    {
    }
}

public sealed class XmlSerializerInterfaceMemberNegative
{
    public IXmlSerializerUnsupportedMemberContract? Adapter { get; set; }

    public int Value
    {
        set
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class XmlSerializerTypeMemberNegative
{
    public System.Type? ExposedType { get; set; }

    public int Value
    {
        set
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class XmlSerializerCustomReadXmlInterfaceMemberPositive : System.Xml.Serialization.IXmlSerializable
{
    public IXmlSerializerUnsupportedMemberContract? Adapter { get; set; }

    public System.Xml.Schema.XmlSchema? GetSchema() => null;

    public void ReadXml(System.Xml.XmlReader reader)
    {
        Helper.InvokeSink();
    }

    public void WriteXml(System.Xml.XmlWriter writer)
    {
    }
}
