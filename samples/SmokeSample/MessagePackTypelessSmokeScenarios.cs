/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using MessagePack;

internal sealed class MessagePackTypelessInternalRootPositive
{
    public MessagePackTypelessInternalRootPositive()
    {
        Helper.InvokeSink();
    }
}

internal static class MessagePackTypelessInternalRootContainer
{
    public sealed class PublicNestedRootPositive
    {
        public PublicNestedRootPositive()
        {
            Helper.InvokeSink();
        }
    }
}

public class MessagePackTypelessNonPublicRootContainer
{
    protected sealed class ProtectedNestedRootPositive
    {
        public ProtectedNestedRootPositive()
        {
            Helper.InvokeSink();
        }
    }

    private sealed class PrivateNestedRootPositive
    {
        public PrivateNestedRootPositive()
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class MessagePackTypelessPrivateParameterizedConstructorPositive
{
    private MessagePackTypelessPrivateParameterizedConstructorPositive(string command)
    {
        Command = command;
        Helper.InvokeSink();
    }

    public string Command { get; }
}

public sealed class MessagePackTypelessPrivateSetterPositive
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

public sealed class MessagePackTypelessCallbackReceiverPositive : IMessagePackSerializationCallbackReceiver
{
    public void OnAfterDeserialize()
    {
        Helper.InvokeSink();
    }

    public void OnBeforeSerialize()
    {
    }
}

public sealed class MessagePackTypelessNoMatchNegative
{
    public MessagePackTypelessNoMatchNegative(string first, int second)
    {
        Name = first;
        Value = second;
        Helper.InvokeSink();
    }

    public string Name { get; }

    public int Value { get; }
}

[MessagePackObject]
public sealed partial class MessagePackTypelessIndexConstructorPositive
{
    public MessagePackTypelessIndexConstructorPositive(string arbitrary, int random)
    {
        Name = arbitrary;
        Value = random;
        Helper.InvokeSink();
    }

    public MessagePackTypelessIndexConstructorPositive(int value, string name)
    {
        Name = name;
        Value = value;
    }

    [Key(0)]
    public string Name { get; }

    [Key(1)]
    public int Value { get; }
}

[MessagePackObject(true)]
public sealed partial class MessagePackTypelessAnnotatedPrivateSerializationConstructorNegative
{
    public MessagePackTypelessAnnotatedPrivateSerializationConstructorNegative()
    {
        Name = "default";
        Helper.InvokeSink();
    }

    [SerializationConstructor]
    private MessagePackTypelessAnnotatedPrivateSerializationConstructorNegative(string name)
    {
        Name = name;
        Helper.InvokeSink();
    }

    public string Name { get; }
}
