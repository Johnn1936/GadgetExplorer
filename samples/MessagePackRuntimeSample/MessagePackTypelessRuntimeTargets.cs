/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using MessagePack;

public static class MessagePackTypelessRuntimeTargets
{
    public static void ResetLog() => MessagePackTypelessRuntimeRecorder.Reset();

    public static IReadOnlyList<string> GetLog() => MessagePackTypelessRuntimeRecorder.GetEntries();

    public static object CreatePublicTopLevelRoot() => new MessagePackTypelessRuntimePublicTopLevelRoot { Value = 101 };

    public static object CreateInternalTopLevelRoot() => new MessagePackTypelessRuntimeInternalTopLevelRoot { Value = 202 };

    public static object CreatePublicNestedRoot() => new MessagePackTypelessRuntimePublicVisibilityContainer.PublicNestedRoot { Value = 303 };

    public static object CreatePublicNestedRootInsideInternalContainer() => MessagePackTypelessRuntimeInternalVisibilityContainer.CreatePublicNestedRoot();

    public static object CreateProtectedNestedRoot() => MessagePackTypelessRuntimeVisibilityContainer.CreateProtectedNestedRoot();

    public static object CreatePrivateNestedRoot() => MessagePackTypelessRuntimeVisibilityContainer.CreatePrivateNestedRoot();

    public static object CreatePublicParameterlessRoot() => new MessagePackTypelessRuntimePublicParameterlessRoot { Value = 11 };

    public static object CreatePublicParameterizedRoot() => MessagePackTypelessRuntimePublicParameterizedRoot.Create("alpha", 12);

    public static object CreatePrivateParameterlessRoot() => MessagePackTypelessRuntimePrivateParameterlessRoot.Create(13);

    public static object CreatePrivateParameterizedRoot() => MessagePackTypelessRuntimePrivateParameterizedRoot.Create("beta", 14);

    public static object CreateParameterlessVsParameterizedRoot() => MessagePackTypelessRuntimeParameterlessVsParameterizedRoot.Create("gamma", 15);

    public static object CreatePublicSerializationConstructorRoot() => MessagePackTypelessRuntimePublicSerializationConstructorRoot.Create("delta", 16);

    public static object CreatePrivateSerializationConstructorRoot() => MessagePackTypelessRuntimePrivateSerializationConstructorRoot.Create("epsilon", 17);

    public static object CreateNoMatchRoot() => MessagePackTypelessRuntimeNoMatchRoot.Create("zeta", 18);

    public static object CreateAnnotatedIndexSelectionRoot() => MessagePackTypelessRuntimeAnnotatedIndexSelectionRoot.Create("eta", 19);

    public static object CreateAnnotatedPrivateSerializationConstructorRoot() => MessagePackTypelessRuntimeAnnotatedPrivateSerializationConstructorRoot.Create("theta");

    public static object CreatePrivateSetterRoot() => MessagePackTypelessRuntimePrivateSetterRoot.Create(21);

    public static object CreateProtectedSetterRoot() => MessagePackTypelessRuntimeProtectedSetterRoot.Create(22);

    public static object CreateInternalSetterRoot() => MessagePackTypelessRuntimeInternalSetterRoot.Create(23);

    public static object CreateProtectedInternalSetterRoot() => MessagePackTypelessRuntimeProtectedInternalSetterRoot.Create(24);

    public static object CreatePrivateProtectedSetterRoot() => MessagePackTypelessRuntimePrivateProtectedSetterRoot.Create(25);

    public static object CreateCallbackReceiverRoot() => MessagePackTypelessRuntimeCallbackReceiverRoot.Create(26);
}

public static class MessagePackTypelessRuntimeRecorder
{
    private static readonly List<string> s_entries = [];

    public static void Add(string value) => s_entries.Add(value);

    public static IReadOnlyList<string> GetEntries() => [.. s_entries];

    public static void Reset() => s_entries.Clear();
}

public sealed class MessagePackTypelessRuntimePublicTopLevelRoot
{
    public int Value { get; set; }
}

internal sealed class MessagePackTypelessRuntimeInternalTopLevelRoot
{
    public int Value { get; set; }
}

public static class MessagePackTypelessRuntimePublicVisibilityContainer
{
    public sealed class PublicNestedRoot
    {
        public int Value { get; set; }
    }
}

internal static class MessagePackTypelessRuntimeInternalVisibilityContainer
{
    public static object CreatePublicNestedRoot() => new PublicNestedRoot { Value = 404 };

    public sealed class PublicNestedRoot
    {
        public int Value { get; set; }
    }
}

public class MessagePackTypelessRuntimeVisibilityContainer
{
    public static object CreateProtectedNestedRoot() => new ProtectedNestedRoot { Value = 505 };

    public static object CreatePrivateNestedRoot() => new PrivateNestedRoot { Value = 606 };

    protected sealed class ProtectedNestedRoot
    {
        public int Value { get; set; }
    }

    private sealed class PrivateNestedRoot
    {
        public int Value { get; set; }
    }
}

public sealed class MessagePackTypelessRuntimePublicParameterlessRoot
{
    public MessagePackTypelessRuntimePublicParameterlessRoot()
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePublicParameterlessRoot::.ctor()");
    }

    public int Value { get; set; }
}

public sealed class MessagePackTypelessRuntimePublicParameterizedRoot
{
    public static MessagePackTypelessRuntimePublicParameterizedRoot Create(string name, int value) => new(name, value);

    public MessagePackTypelessRuntimePublicParameterizedRoot(string name, int value)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePublicParameterizedRoot::.ctor(string,int)");
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public int Value { get; }
}

public sealed class MessagePackTypelessRuntimePrivateParameterlessRoot
{
    public static MessagePackTypelessRuntimePrivateParameterlessRoot Create(int value)
    {
        var instance = new MessagePackTypelessRuntimePrivateParameterlessRoot();
        instance.Value = value;
        return instance;
    }

    private MessagePackTypelessRuntimePrivateParameterlessRoot()
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePrivateParameterlessRoot::.ctor()");
    }

    public int Value { get; set; }
}

public sealed class MessagePackTypelessRuntimePrivateParameterizedRoot
{
    public static MessagePackTypelessRuntimePrivateParameterizedRoot Create(string name, int value) => new(name, value);

    private MessagePackTypelessRuntimePrivateParameterizedRoot(string name, int value)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePrivateParameterizedRoot::.ctor(string,int)");
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public int Value { get; }
}

public sealed class MessagePackTypelessRuntimeParameterlessVsParameterizedRoot
{
    public static MessagePackTypelessRuntimeParameterlessVsParameterizedRoot Create(string name, int value) => new(name, value);

    public MessagePackTypelessRuntimeParameterlessVsParameterizedRoot()
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeParameterlessVsParameterizedRoot::.ctor()");
        Name = "default";
    }

    public MessagePackTypelessRuntimeParameterlessVsParameterizedRoot(string name, int value)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeParameterlessVsParameterizedRoot::.ctor(string,int)");
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public int Value { get; }
}

public sealed class MessagePackTypelessRuntimePublicSerializationConstructorRoot
{
    public static MessagePackTypelessRuntimePublicSerializationConstructorRoot Create(string name, int value) => new(name, value);

    public MessagePackTypelessRuntimePublicSerializationConstructorRoot()
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePublicSerializationConstructorRoot::.ctor()");
        Name = "default";
    }

    [SerializationConstructor]
    public MessagePackTypelessRuntimePublicSerializationConstructorRoot(string name, int value)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePublicSerializationConstructorRoot::.ctor(string,int)");
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public int Value { get; }
}

public sealed class MessagePackTypelessRuntimePrivateSerializationConstructorRoot
{
    public static MessagePackTypelessRuntimePrivateSerializationConstructorRoot Create(string name, int value) => new(name, value);

    public MessagePackTypelessRuntimePrivateSerializationConstructorRoot()
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePrivateSerializationConstructorRoot::.ctor()");
        Name = "default";
    }

    [SerializationConstructor]
    private MessagePackTypelessRuntimePrivateSerializationConstructorRoot(string name, int value)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePrivateSerializationConstructorRoot::.ctor(string,int)");
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public int Value { get; }
}

public sealed class MessagePackTypelessRuntimeNoMatchRoot
{
    public static MessagePackTypelessRuntimeNoMatchRoot Create(string name, int value) => new(name, value);

    public MessagePackTypelessRuntimeNoMatchRoot(string first, int second)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeNoMatchRoot::.ctor(string,int)");
        Name = first;
        Value = second;
    }

    public string Name { get; }

    public int Value { get; }
}

[MessagePackObject]
public sealed partial class MessagePackTypelessRuntimeAnnotatedIndexSelectionRoot
{
    public static MessagePackTypelessRuntimeAnnotatedIndexSelectionRoot Create(string name, int value) => new(name, value);

    public MessagePackTypelessRuntimeAnnotatedIndexSelectionRoot(string arbitrary, int random)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeAnnotatedIndexSelectionRoot::.ctor(string,int)");
        Name = arbitrary;
        Value = random;
    }

    public MessagePackTypelessRuntimeAnnotatedIndexSelectionRoot(int value, string name)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeAnnotatedIndexSelectionRoot::.ctor(int,string)");
        Name = name;
        Value = value;
    }

    [Key(0)]
    public string Name { get; }

    [Key(1)]
    public int Value { get; }
}

[MessagePackObject(true)]
public sealed partial class MessagePackTypelessRuntimeAnnotatedPrivateSerializationConstructorRoot
{
    public static MessagePackTypelessRuntimeAnnotatedPrivateSerializationConstructorRoot Create(string name) => new(name);

    public MessagePackTypelessRuntimeAnnotatedPrivateSerializationConstructorRoot()
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeAnnotatedPrivateSerializationConstructorRoot::.ctor()");
        Name = "default";
    }

    [SerializationConstructor]
    private MessagePackTypelessRuntimeAnnotatedPrivateSerializationConstructorRoot(string name)
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeAnnotatedPrivateSerializationConstructorRoot::.ctor(string)");
        Name = name;
    }

    public string Name { get; }
}

public sealed class MessagePackTypelessRuntimePrivateSetterRoot
{
    private int _value;

    public static MessagePackTypelessRuntimePrivateSetterRoot Create(int value)
    {
        var instance = new MessagePackTypelessRuntimePrivateSetterRoot();
        instance.Value = value;
        return instance;
    }

    public int Value
    {
        get => _value;
        private set
        {
            MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePrivateSetterRoot::set_Value(System.Int32)");
            _value = value;
        }
    }
}

public class MessagePackTypelessRuntimeProtectedSetterRoot
{
    private int _value;

    public static MessagePackTypelessRuntimeProtectedSetterRoot Create(int value)
    {
        var instance = new MessagePackTypelessRuntimeProtectedSetterRoot();
        instance.Value = value;
        return instance;
    }

    public int Value
    {
        get => _value;
        protected set
        {
            MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeProtectedSetterRoot::set_Value(System.Int32)");
            _value = value;
        }
    }
}

public sealed class MessagePackTypelessRuntimeInternalSetterRoot
{
    private int _value;

    public static MessagePackTypelessRuntimeInternalSetterRoot Create(int value)
    {
        var instance = new MessagePackTypelessRuntimeInternalSetterRoot();
        instance.Value = value;
        return instance;
    }

    public int Value
    {
        get => _value;
        internal set
        {
            MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeInternalSetterRoot::set_Value(System.Int32)");
            _value = value;
        }
    }
}

public class MessagePackTypelessRuntimeProtectedInternalSetterRoot
{
    private int _value;

    public static MessagePackTypelessRuntimeProtectedInternalSetterRoot Create(int value)
    {
        var instance = new MessagePackTypelessRuntimeProtectedInternalSetterRoot();
        instance.Value = value;
        return instance;
    }

    public int Value
    {
        get => _value;
        protected internal set
        {
            MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeProtectedInternalSetterRoot::set_Value(System.Int32)");
            _value = value;
        }
    }
}

public class MessagePackTypelessRuntimePrivateProtectedSetterRoot
{
    private int _value;

    public static MessagePackTypelessRuntimePrivateProtectedSetterRoot Create(int value)
    {
        var instance = new MessagePackTypelessRuntimePrivateProtectedSetterRoot();
        instance.Value = value;
        return instance;
    }

    public int Value
    {
        get => _value;
        private protected set
        {
            MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimePrivateProtectedSetterRoot::set_Value(System.Int32)");
            _value = value;
        }
    }
}

public sealed class MessagePackTypelessRuntimeCallbackReceiverRoot : IMessagePackSerializationCallbackReceiver
{
    public static MessagePackTypelessRuntimeCallbackReceiverRoot Create(int value) => new() { Value = value };

    public int Value { get; set; }

    public void OnAfterDeserialize()
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeCallbackReceiverRoot::OnAfterDeserialize()");
    }

    public void OnBeforeSerialize()
    {
        MessagePackTypelessRuntimeRecorder.Add("MessagePackTypelessRuntimeCallbackReceiverRoot::OnBeforeSerialize()");
    }
}
