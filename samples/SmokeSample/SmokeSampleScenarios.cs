/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

public sealed class MySpecialObject
{
    public void SayHello()
    {
    }
}

public static class Helper
{
    public static void InvokeSink()
    {
        new MySpecialObject().SayHello();
    }
}

public sealed class ConstructorPositive
{
    public ConstructorPositive()
    {
        Helper.InvokeSink();
    }
}

public sealed class ParameterlessPreferredCtorPositive
{
    public ParameterlessPreferredCtorPositive()
    {
        Helper.InvokeSink();
    }

    public ParameterlessPreferredCtorPositive(string value)
    {
        Helper.InvokeSink();
    }
}

public sealed class SetterPositive
{
    public int Value
    {
        set
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class GetterPositive
{
    public int Value
    {
        get
        {
            Helper.InvokeSink();
            return 42;
        }
    }
}

public sealed class Negative
{
    public Negative()
    {
    }

    public int Value
    {
        set
        {
        }
    }
}

public class Person
{
    public virtual string Name
    {
        set
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class Employee : Person
{
}

public class GetterPerson
{
    public virtual string Name
    {
        get
        {
            Helper.InvokeSink();
            return string.Empty;
        }
    }
}

public sealed class GetterEmployee : GetterPerson
{
}

public sealed class WorkforceManager
{
    private readonly List<Employee> _employees = [];

    public WorkforceManager()
    {
        _employees.Add(new Employee());
        SeedDefaults();
    }

    private void SeedDefaults()
    {
        foreach (var employee in _employees)
        {
            employee.Name = "Default";
        }
    }
}

public interface IHelloStep
{
    void Execute();
}

public sealed class InterfaceHelloStep : IHelloStep
{
    public void Execute()
    {
        Helper.InvokeSink();
    }
}

public static class InterfaceBridge
{
    public static void Dispatch(IHelloStep step)
    {
        step.Execute();
    }
}

public sealed class InterfaceCtorPositive
{
    public InterfaceCtorPositive()
    {
        InterfaceBridge.Dispatch(new InterfaceHelloStep());
    }
}

public class VirtualWorker
{
    public virtual void Run()
    {
    }
}

public sealed class SpecialVirtualWorker : VirtualWorker
{
    public override void Run()
    {
        Helper.InvokeSink();
    }
}

public sealed class VirtualCtorPositive
{
    public VirtualCtorPositive()
    {
        VirtualWorker worker = new SpecialVirtualWorker();
        worker.Run();
    }
}

public class BroadVirtualWorker
{
    public virtual void Run()
    {
    }
}

public sealed class EarlyVirtualWorker : BroadVirtualWorker
{
    public override void Run()
    {
        Helper.InvokeSink();
    }
}

public sealed class LateVirtualWorker : BroadVirtualWorker
{
    public override void Run()
    {
        Helper.InvokeSink();
    }
}

public sealed class ReceiverAwareVirtualPositive
{
    public ReceiverAwareVirtualPositive()
    {
        _ = new EarlyVirtualWorker();
        BroadVirtualWorker worker = new LateVirtualWorker();
        worker.Run();
    }
}

public abstract class OpaqueVirtualWorker
{
    public virtual void Run()
    {
    }
}

public sealed class SinkingOpaqueVirtualWorker : OpaqueVirtualWorker
{
    public override void Run()
    {
        Helper.InvokeSink();
    }
}

public sealed class SilentOpaqueVirtualWorker : OpaqueVirtualWorker
{
    public override void Run()
    {
    }
}

public static class OpaqueVirtualFactory
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
    public static extern OpaqueVirtualWorker Create();
}

public sealed class BroadVirtualOpaqueNegative
{
    public BroadVirtualOpaqueNegative()
    {
        OpaqueVirtualFactory.Create().Run();
    }
}

public abstract class CastTrackedVirtualBase
{
    public virtual void Run()
    {
    }
}

public sealed class SinkingCastTrackedVirtualWorker : CastTrackedVirtualBase
{
    public override void Run()
    {
        Helper.InvokeSink();
    }
}

public sealed class SilentCastTrackedVirtualWorker : CastTrackedVirtualBase
{
    public override void Run()
    {
    }
}

public static class OpaqueCastTrackedVirtualFactory
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
    public static extern object Create();
}

public sealed class AbstractCastVirtualPositive
{
    public AbstractCastVirtualPositive()
    {
        _ = new SinkingCastTrackedVirtualWorker();
        _ = new SilentCastTrackedVirtualWorker();
        CastTrackedVirtualBase? worker = OpaqueCastTrackedVirtualFactory.Create() as CastTrackedVirtualBase;
        if (worker is not null)
        {
            worker.Run();
        }
    }
}

public abstract class SetterRefreshBase
{
    protected void Refresh()
    {
        BeginQuery();
    }

    protected virtual void BeginQuery()
    {
    }
}

public sealed class SetterRefreshDerivedPositive : SetterRefreshBase
{
    public string? MethodName
    {
        set
        {
            _ = value;
            Refresh();
        }
    }

    protected override void BeginQuery()
    {
        Helper.InvokeSink();
    }
}

public sealed class SetterRefreshDerivedNegative : SetterRefreshBase
{
    public string? XPath
    {
        set
        {
            _ = value;
            Refresh();
        }
    }

    protected override void BeginQuery()
    {
    }
}

public static class DelegateBridge
{
    public static void Invoke(Action callback)
    {
        callback();
    }
}

public sealed class DelegateSetterPositive
{
    public int Value
    {
        set
        {
            Action callback = Helper.InvokeSink;
            DelegateBridge.Invoke(callback);
        }
    }
}

public sealed class LocalDelegateSetterPositive
{
    public int Value
    {
        set
        {
            Action callback = Helper.InvokeSink;
            callback();
        }
    }
}

public interface IGenericHelloWorker
{
    void DoWork();
}

public sealed class GenericHelloWorker : IGenericHelloWorker
{
    public void DoWork()
    {
        Helper.InvokeSink();
    }
}

public sealed class GenericRelay<TWorker>
    where TWorker : IGenericHelloWorker
{
    public void Relay(TWorker worker)
    {
        worker.DoWork();
    }
}

public sealed class GenericSetterPositive
{
    public int Value
    {
        set
        {
            var relay = new GenericRelay<GenericHelloWorker>();
            relay.Relay(new GenericHelloWorker());
        }
    }
}

public sealed class EarlyDisposable : IDisposable
{
    public void Dispose()
    {
        Helper.InvokeSink();
    }
}

public sealed class LateDisposable : IDisposable
{
    public void Dispose()
    {
        Helper.InvokeSink();
    }
}

public sealed class ReceiverAwareInterfacePositive
{
    public ReceiverAwareInterfacePositive()
    {
        _ = new EarlyDisposable();
        IDisposable disposable = new LateDisposable();
        disposable.Dispose();
    }
}

public sealed class SilentDisposable : IDisposable
{
    public void Dispose()
    {
    }
}

public sealed class UsingCastedDisposablePositive
{
    public UsingCastedDisposablePositive()
    {
        object disposable = new LateDisposable();
        using var scoped = (IDisposable)disposable;
    }
}

public sealed class UsingCastedDisposableNegative
{
    public UsingCastedDisposableNegative()
    {
        object disposable = new SilentDisposable();
        using var scoped = (IDisposable)disposable;
    }
}

public abstract class DisposalChainBase : IDisposable
{
    public void Dispose()
    {
        Close();
    }

    protected virtual void Close()
    {
        DisposeCore();
    }

    protected virtual void DisposeCore()
    {
    }
}

public sealed class DangerousDisposalChain : DisposalChainBase
{
    protected override void DisposeCore()
    {
        Helper.InvokeSink();
    }
}

public sealed class SafeDisposalChain : DisposalChainBase
{
}

public sealed class UsingBaseDisposeChainPositive
{
    public UsingBaseDisposeChainPositive()
    {
        object disposable = new DangerousDisposalChain();
        using var scoped = (IDisposable)disposable;
    }
}

public sealed class UsingBaseDisposeChainNegative
{
    public UsingBaseDisposeChainNegative()
    {
        object disposable = new SafeDisposalChain();
        using var scoped = (IDisposable)disposable;
    }
}

public sealed class SinkingNumberEnumerator : IEnumerator<int>
{
    public int Current => 0;

    object System.Collections.IEnumerator.Current => Current;

    public bool MoveNext() => false;

    public void Reset()
    {
    }

    public void Dispose()
    {
        Helper.InvokeSink();
    }
}

public sealed class SilentNumberEnumerator : IEnumerator<int>
{
    public int Current => 0;

    object System.Collections.IEnumerator.Current => Current;

    public bool MoveNext() => false;

    public void Reset()
    {
    }

    public void Dispose()
    {
    }
}

public static class NumberEnumeratorFactory
{
    public static IEnumerator<int> CreateSinking() => new SinkingNumberEnumerator();

    public static IEnumerator<int> CreateSilent() => new SilentNumberEnumerator();
}

public sealed class InterfaceConstrainedEnumeratorDisposePositive
{
    public InterfaceConstrainedEnumeratorDisposePositive()
    {
        var enumerator = NumberEnumeratorFactory.CreateSinking();
        enumerator.Dispose();
    }
}

public sealed class InterfaceConstrainedEnumeratorDisposeNegative
{
    public InterfaceConstrainedEnumeratorDisposeNegative()
    {
        var enumerator = NumberEnumeratorFactory.CreateSilent();
        enumerator.Dispose();
    }
}

public interface ISinkingDisposableHandle : IDisposable
{
}

public interface ISilentDisposableHandle : IDisposable
{
}

public sealed class SinkingDisposableHandle : ISinkingDisposableHandle
{
    public void Dispose()
    {
        Helper.InvokeSink();
    }
}

public sealed class SilentDisposableHandle : ISilentDisposableHandle
{
    public void Dispose()
    {
    }
}

public static class ConstrainedDisposableFactory
{
    public static ISinkingDisposableHandle CreateSinkingHandle() => new SinkingDisposableHandle();

    public static ISilentDisposableHandle CreateSilentHandle() => new SilentDisposableHandle();
}

public sealed class StaticTypeConstrainedInterfaceDisposePositive
{
    public StaticTypeConstrainedInterfaceDisposePositive()
    {
        var disposable = ConstrainedDisposableFactory.CreateSinkingHandle();
        disposable.Dispose();
    }
}

public sealed class StaticTypeConstrainedInterfaceDisposeNegative
{
    public StaticTypeConstrainedInterfaceDisposeNegative()
    {
        var disposable = ConstrainedDisposableFactory.CreateSilentHandle();
        disposable.Dispose();
    }
}

public struct SinkingStructEnumerator : IEnumerator<int>
{
    public int Current => 0;

    object System.Collections.IEnumerator.Current => Current;

    public bool MoveNext() => false;

    public void Reset()
    {
    }

    public void Dispose()
    {
        Helper.InvokeSink();
    }
}

public struct SilentStructEnumerator : IEnumerator<int>
{
    public int Current => 0;

    object System.Collections.IEnumerator.Current => Current;

    public bool MoveNext() => false;

    public void Reset()
    {
    }

    public void Dispose()
    {
    }
}

public sealed class SinkingStructEnumerable : IEnumerable<int>
{
    public SinkingStructEnumerator GetEnumerator() => new();

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class SilentStructEnumerable : IEnumerable<int>
{
    public SilentStructEnumerator GetEnumerator() => new();

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class StructEnumeratorForeachPositive
{
    public StructEnumeratorForeachPositive()
    {
        foreach (var _ in new SinkingStructEnumerable())
        {
        }
    }
}

public sealed class StructEnumeratorForeachNegative
{
    public StructEnumeratorForeachNegative()
    {
        foreach (var _ in new SilentStructEnumerable())
        {
        }
    }
}

public interface ICompatibleEnumeratorProbe<T>
{
    bool MoveNext();
}

public sealed class SinkingCompatibleEnumeratorProbe : ICompatibleEnumeratorProbe<int>
{
    public bool MoveNext()
    {
        Helper.InvokeSink();
        return false;
    }
}

public sealed class SilentMismatchedCompatibleEnumeratorProbe : ICompatibleEnumeratorProbe<object>
{
    public bool MoveNext() => false;
}

public static class CompatibleEnumeratorProbeFactory
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
    public static extern ICompatibleEnumeratorProbe<int> Create();
}

public sealed class ClosedGenericEnumeratorConstraintPositive
{
    public ClosedGenericEnumeratorConstraintPositive()
    {
        _ = CompatibleEnumeratorProbeFactory.Create().MoveNext();
    }
}

public interface IMismatchedEnumeratorProbe<T>
{
    bool MoveNext();
}

public sealed class SilentCompatibleMismatchedEnumeratorProbe : IMismatchedEnumeratorProbe<int>
{
    public bool MoveNext() => false;
}

public sealed class SinkingIncompatibleMismatchedEnumeratorProbe : IMismatchedEnumeratorProbe<object>
{
    public bool MoveNext()
    {
        Helper.InvokeSink();
        return false;
    }
}

public static class MismatchedEnumeratorProbeFactory
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
    public static extern IMismatchedEnumeratorProbe<int> Create();
}

public sealed class ClosedGenericEnumeratorConstraintNegative
{
    public ClosedGenericEnumeratorConstraintNegative()
    {
        _ = MismatchedEnumeratorProbeFactory.Create().MoveNext();
    }
}

public interface IObjectOnlyCastProbe
{
    void Execute();
}

public sealed class SinkingObjectOnlyCastProbe : IObjectOnlyCastProbe
{
    public void Execute()
    {
        Helper.InvokeSink();
    }
}

public static class OpaqueObjectOnlyCastProbeFactory
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
    public static extern object Create();
}

public sealed class ObjectOnlyCastConstraintNegative
{
    public ObjectOnlyCastConstraintNegative()
    {
        _ = new SinkingObjectOnlyCastProbe();
        IObjectOnlyCastProbe probe = (IObjectOnlyCastProbe)OpaqueObjectOnlyCastProbeFactory.Create();
        probe.Execute();
    }
}

public sealed class SinkingEnumerableParameterSource : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        Helper.InvokeSink();
        return new SinkingNumberEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class SilentEnumerableParameterSource : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator() => new SilentNumberEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class EnumerableParameterRelay
{
    public static IEnumerator<int> Capture(IEnumerable<int> source) => source.GetEnumerator();
}

public sealed class ExactEnumerableParameterPositive
{
    public ExactEnumerableParameterPositive()
    {
        IEnumerable<int> source = new SinkingEnumerableParameterSource();
        _ = source.GetEnumerator();
    }
}

public static class OpaqueEnumerableParameterSourceFactory
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
    public static extern IEnumerable<int> Create();
}

public sealed class OpenEndedEnumerableParameterNegative
{
    public OpenEndedEnumerableParameterNegative()
    {
        _ = new SinkingEnumerableParameterSource();
        _ = new SilentEnumerableParameterSource();
        _ = EnumerableParameterRelay.Capture(OpaqueEnumerableParameterSourceFactory.Create());
    }
}

public sealed class SinkingDictionaryEnumerator : System.Collections.IDictionaryEnumerator
{
    public object Current
    {
        get
        {
            Helper.InvokeSink();
            return Entry;
        }
    }

    public System.Collections.DictionaryEntry Entry => new("sink", 1);

    public object Key => "sink";

    public object Value => 1;

    public bool MoveNext() => false;

    public void Reset()
    {
    }
}

public sealed class SilentDictionaryEnumerator : System.Collections.IDictionaryEnumerator
{
    public object Current => Entry;

    public System.Collections.DictionaryEntry Entry => new("silent", 1);

    public object Key => "silent";

    public object Value => 1;

    public bool MoveNext() => false;

    public void Reset()
    {
    }
}

public static class DictionaryEnumeratorRelay
{
    public static object ReadCurrent(System.Collections.IDictionaryEnumerator enumerator) => enumerator.Current;
}

public sealed class ExactDictionaryEnumeratorCurrentPositive
{
    public ExactDictionaryEnumeratorCurrentPositive()
    {
        System.Collections.IDictionaryEnumerator enumerator = new SinkingDictionaryEnumerator();
        _ = enumerator.Current;
    }
}

public static class OpaqueDictionaryEnumeratorFactory
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
    public static extern System.Collections.IDictionaryEnumerator Create();
}

public sealed class OpenEndedDictionaryEnumeratorCurrentNegative
{
    public OpenEndedDictionaryEnumeratorCurrentNegative()
    {
        _ = new SinkingDictionaryEnumerator();
        _ = new SilentDictionaryEnumerator();
        _ = DictionaryEnumeratorRelay.ReadCurrent(OpaqueDictionaryEnumeratorFactory.Create());
    }
}

public static class LayerOne
{
    public static void Start()
    {
        LayerTwo.Continue(new InterfaceHelloStep());
    }
}

public static class LayerTwo
{
    public static void Continue(IHelloStep step)
    {
        LayerThree.Finish(step);
    }
}

public static class LayerThree
{
    public static void Finish(IHelloStep step)
    {
        InterfaceBridge.Dispatch(step);
    }
}

public sealed class NestedConstructorPositive
{
    public NestedConstructorPositive()
    {
        LayerOne.Start();
    }
}

public sealed class EventPublisher
{
    public event Action? Raised;

    public void Raise()
    {
        Raised?.Invoke();
    }
}

public sealed class EventCtorPositive
{
    public EventCtorPositive()
    {
        var publisher = new EventPublisher();
        publisher.Raised += Helper.InvokeSink;
        publisher.Raise();
    }
}

public sealed class EventWithoutSubscriptionNegative
{
    public event Action? Raised;

    public int Value
    {
        set
        {
            Raised?.Invoke();
        }
    }
}

public sealed class GotoSkippedSinkNegative
{
    public GotoSkippedSinkNegative()
    {
        goto AfterSink;
#pragma warning disable CS0162
        Helper.InvokeSink();
#pragma warning restore CS0162
    AfterSink:
        return;
    }
}

public sealed class LockFinallyContinuationPositive
{
    private static readonly object SyncRoot = new();

    public LockFinallyContinuationPositive()
    {
        lock (SyncRoot)
        {
        }

        Helper.InvokeSink();
    }
}

public static class OutArrayPopulationHelper
{
    public static void Populate(out string[] tabTypes, out int[] tabScopes)
    {
        tabTypes = ["sink"];
        tabScopes = [1];
    }
}

public sealed class OutArrayBranchPositive
{
    public OutArrayBranchPositive()
    {
        string[]? tabTypes = null;
        int[]? tabScopes = null;
        OutArrayPopulationHelper.Populate(out tabTypes, out tabScopes);
        if (tabTypes is null || tabScopes is null || tabTypes.Length == 0)
        {
            return;
        }

        Helper.InvokeSink();
    }
}

public sealed class OnDeserializingPositive
{
    [System.Runtime.Serialization.OnDeserializing]
    private void BeforePopulate(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }
}

public sealed class OnDeserializedPositive
{
    [System.Runtime.Serialization.OnDeserialized]
    private void AfterPopulate(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }
}

public abstract class CallbackHookBase
{
    [System.Runtime.Serialization.OnDeserialized]
    protected void AfterPopulateBase(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }
}

public sealed class InheritedOnDeserializedPositive : CallbackHookBase
{
}

public sealed class FinalizerPositive
{
    ~FinalizerPositive()
    {
        Helper.InvokeSink();
    }
}

public abstract class FinalizerHookBase
{
    ~FinalizerHookBase()
    {
        Helper.InvokeSink();
    }
}

public sealed class InheritedFinalizerPositive : FinalizerHookBase
{
}

public sealed class FinalizerFileDeletePositive
{
    ~FinalizerFileDeletePositive()
    {
        System.IO.File.Delete("sample.tmp");
    }
}

public sealed class IgnoredStackTraceNoisePositive
{
    public IgnoredStackTraceNoisePositive()
    {
        _ = new System.Diagnostics.StackTrace();
    }
}

public sealed class JsonConstructorPositive
{
    public JsonConstructorPositive()
    {
    }

    [Newtonsoft.Json.JsonConstructor]
    public JsonConstructorPositive(string value)
    {
        Helper.InvokeSink();
    }
}

public sealed class JsonNonPublicParameterlessPositive
{
    private JsonNonPublicParameterlessPositive()
    {
    }

    public int Value
    {
        set => Helper.InvokeSink();
    }
}

public sealed class JsonSinglePublicParameterizedPreferredOverNonPublicParameterlessPositive
{
    private JsonSinglePublicParameterizedPreferredOverNonPublicParameterlessPositive()
    {
    }

    public JsonSinglePublicParameterizedPreferredOverNonPublicParameterlessPositive(string value)
    {
        Helper.InvokeSink();
    }
}

public sealed class JsonMultiplePublicParameterizedNegative
{
    public JsonMultiplePublicParameterizedNegative(string value)
    {
        Helper.InvokeSink();
    }

    public JsonMultiplePublicParameterizedNegative(int value)
    {
        Helper.InvokeSink();
    }
}

[Serializable]
public sealed class JsonNetSerializableISerializablePrefersSerializationConstructorPositive : System.Runtime.Serialization.ISerializable
{
    public JsonNetSerializableISerializablePrefersSerializationConstructorPositive()
    {
        Helper.InvokeSink();
    }

    private JsonNetSerializableISerializablePrefersSerializationConstructorPositive(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }

    public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
    }
}

public sealed class JsonNetNonPublicSetterWithoutOptInNegative
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

public sealed class JsonNetNonPublicSetterJsonPropertyPositive
{
    private int _value;

    [Newtonsoft.Json.JsonProperty]
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

[System.Runtime.Serialization.DataContract]
public sealed class JsonNetNonPublicSetterDataMemberPositive
{
    private int _value;

    [System.Runtime.Serialization.DataMember]
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

public sealed class JsonNetOnErrorPositive
{
    [Newtonsoft.Json.Serialization.OnError]
    private void OnError(System.Runtime.Serialization.StreamingContext context, Newtonsoft.Json.Serialization.ErrorContext errorContext)
    {
        Helper.InvokeSink();
    }
}

internal sealed class JsonNetInternalRootPositive
{
    public JsonNetInternalRootPositive()
    {
        Helper.InvokeSink();
    }
}

public class JsonNetNonPublicRootContainer
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

public static class JsonNetVisibilityTargets
{
    public static object CreateInternalTopLevelRoot() => new InternalTopLevelRoot { Value = 101 };

    public static object CreateProtectedNestedRoot() => VisibilityRootContainer.CreateProtectedNestedRoot();

    public static object CreatePrivateNestedRoot() => VisibilityRootContainer.CreatePrivateNestedRoot();

    public static string GetInternalTopLevelRootAssemblyQualifiedName() => typeof(InternalTopLevelRoot).AssemblyQualifiedName
        ?? throw new InvalidOperationException("Assembly-qualified name was null.");

    public static string GetProtectedNestedRootAssemblyQualifiedName() => VisibilityRootContainer.GetProtectedNestedRootType().AssemblyQualifiedName
        ?? throw new InvalidOperationException("Assembly-qualified name was null.");

    public static string GetPrivateNestedRootAssemblyQualifiedName() => VisibilityRootContainer.GetPrivateNestedRootType().AssemblyQualifiedName
        ?? throw new InvalidOperationException("Assembly-qualified name was null.");
}

internal sealed class InternalTopLevelRoot
{
    public int Value { get; set; }
}

public class VisibilityRootContainer
{
    public static object CreateProtectedNestedRoot() => new ProtectedNestedRoot { Value = 202 };

    public static object CreatePrivateNestedRoot() => new PrivateNestedRoot { Value = 303 };

    public static Type GetProtectedNestedRootType() => typeof(ProtectedNestedRoot);

    public static Type GetPrivateNestedRootType() => typeof(PrivateNestedRoot);

    protected sealed class ProtectedNestedRoot
    {
        public int Value { get; set; }
    }

    private sealed class PrivateNestedRoot
    {
        public int Value { get; set; }
    }
}

public sealed class ExactSignatureConstructorPositive
{
    public ExactSignatureConstructorPositive()
    {
    }

    public ExactSignatureConstructorPositive(string first, string second)
    {
        Helper.InvokeSink();
    }
}

public sealed class ExactSignatureConstructorNegative
{
    public ExactSignatureConstructorNegative()
    {
        Helper.InvokeSink();
    }

    public ExactSignatureConstructorNegative(string value)
    {
    }
}

[Serializable]
public sealed class BinaryFormatterSerializableCallbackPositive
{
    [System.Runtime.Serialization.OnDeserialized]
    private void AfterDeserialize(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }
}

[Serializable]
public sealed class BinaryFormatterSerializableLeaf
{
}

public sealed class BinaryFormatterNonSerializableLeaf
{
}

[Serializable]
public class BinaryFormatterGenericCallbackBase<T>
{
    [System.Runtime.Serialization.OnDeserialized]
    protected void AfterDeserialize(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }
}

[Serializable]
public sealed class BinaryFormatterClosedGenericRootPositive : BinaryFormatterGenericCallbackBase<BinaryFormatterSerializableLeaf>
{
}

[Serializable]
public sealed class BinaryFormatterClosedGenericRootNegative : BinaryFormatterGenericCallbackBase<BinaryFormatterNonSerializableLeaf>
{
}

[Serializable]
public sealed class BinaryFormatterNestedGenericRootPositive : BinaryFormatterGenericCallbackBase<List<BinaryFormatterSerializableLeaf>>
{
}

[Serializable]
public sealed class BinaryFormatterNestedGenericRootNegative : BinaryFormatterGenericCallbackBase<List<BinaryFormatterNonSerializableLeaf>>
{
}

[Serializable]
public sealed class BinaryFormatterOpenGenericCallbackRoot<T>
{
    [System.Runtime.Serialization.OnDeserialized]
    private void AfterDeserialize(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }
}

[Serializable]
public sealed class BinaryFormatterSerializationConstructorPositive : System.Runtime.Serialization.ISerializable
{
    private BinaryFormatterSerializationConstructorPositive(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }

    public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
    }
}

[Serializable]
public sealed class BinaryFormatterSealedPrivateSerializationConstructorPositive : System.Runtime.Serialization.ISerializable
{
    private BinaryFormatterSealedPrivateSerializationConstructorPositive(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }

    public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
    }
}

[Serializable]
public class BinaryFormatterUnsealedProtectedSerializationConstructorPositive : System.Runtime.Serialization.ISerializable
{
    protected BinaryFormatterUnsealedProtectedSerializationConstructorPositive(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }

    public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
    }
}

[Serializable]
public sealed class BinaryFormatterSealedPublicSerializationConstructorPositive : System.Runtime.Serialization.ISerializable
{
    public BinaryFormatterSealedPublicSerializationConstructorPositive(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }

    public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
    }
}

[Serializable]
public class BinaryFormatterUnsealedPrivateSerializationConstructorPositive : System.Runtime.Serialization.ISerializable
{
    private BinaryFormatterUnsealedPrivateSerializationConstructorPositive(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }

    public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
    }
}

[Serializable]
public class BinaryFormatterUnsealedInternalSerializationConstructorPositive : System.Runtime.Serialization.ISerializable
{
    internal BinaryFormatterUnsealedInternalSerializationConstructorPositive(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }

    public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
    }
}

[Serializable]
public sealed class BinaryFormatterSerializationConstructorNegative
{
    private BinaryFormatterSerializationConstructorNegative(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }
}

public sealed class BinaryFormatterNonSerializableISerializableNegative : System.Runtime.Serialization.ISerializable
{
    private BinaryFormatterNonSerializableISerializableNegative(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }

    public void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
    }
}

[Serializable]
public sealed class BinaryFormatterInterfaceCallbackPositive : System.Runtime.Serialization.IDeserializationCallback
{
    public void OnDeserialization(object? sender)
    {
        Helper.InvokeSink();
    }
}

[Serializable]
public sealed class BinaryFormatterInterfaceCallbackNegative
{
    public void OnDeserialization(object? sender)
    {
        Helper.InvokeSink();
    }
}

[Serializable]
public abstract class BinaryFormatterInterfaceCallbackBase : System.Runtime.Serialization.IDeserializationCallback
{
    public void OnDeserialization(object? sender)
    {
        Helper.InvokeSink();
    }
}

[Serializable]
public sealed class BinaryFormatterInheritedInterfaceCallbackPositive : BinaryFormatterInterfaceCallbackBase
{
}

#pragma warning disable SYSLIB0050 // Intentional BinaryFormatter coverage in the smoke sample.
[Serializable]
public sealed class BinaryFormatterObjectReferencePositive : System.Runtime.Serialization.IObjectReference
{
    public object GetRealObject(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
        return this;
    }
}

[Serializable]
public sealed class BinaryFormatterObjectReferenceNegative
{
    public object GetRealObject(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
        return this;
    }
}

[Serializable]
public abstract class BinaryFormatterObjectReferenceBase : System.Runtime.Serialization.IObjectReference
{
    public object GetRealObject(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
        return this;
    }
}

[Serializable]
public sealed class BinaryFormatterInheritedObjectReferencePositive : BinaryFormatterObjectReferenceBase
{
}
#pragma warning restore SYSLIB0050

public sealed class BinaryFormatterNonSerializableCallbackNegative
{
    [System.Runtime.Serialization.OnDeserialized]
    private void AfterDeserialize(System.Runtime.Serialization.StreamingContext context)
    {
        Helper.InvokeSink();
    }
}

public static class OverloadedSinkTarget
{
    public static void Invoke()
    {
    }

    public static void Invoke(string value)
    {
        Helper.InvokeSink();
    }
}

public static class ClosedGenericSinkTarget
{
    public static void Invoke(List<string> values)
    {
    }

    public static void Invoke(List<int> values)
    {
    }
}

public sealed class ClosedGenericSinkStringPositive
{
    public ClosedGenericSinkStringPositive()
    {
        ClosedGenericSinkTarget.Invoke(["hello"]);
    }
}

public sealed class ClosedGenericSinkIntNegative
{
    public ClosedGenericSinkIntNegative()
    {
        ClosedGenericSinkTarget.Invoke([1]);
    }
}

public sealed class ExactClosedGenericConstructorPositive
{
    public ExactClosedGenericConstructorPositive()
    {
    }

    public ExactClosedGenericConstructorPositive(int? value)
    {
        Helper.InvokeSink();
    }
}

public sealed class ExactClosedGenericConstructorNegative
{
    public ExactClosedGenericConstructorNegative()
    {
    }

    public ExactClosedGenericConstructorNegative(long? value)
    {
        Helper.InvokeSink();
    }
}

public sealed class OverloadedSinkStringPositive
{
    public OverloadedSinkStringPositive()
    {
        OverloadedSinkTarget.Invoke("hello");
    }
}

public sealed class OverloadedSinkParameterlessNegative
{
    public OverloadedSinkParameterlessNegative()
    {
        OverloadedSinkTarget.Invoke();
    }
}

public sealed class AssemblyLoadFromPositive
{
    public AssemblyLoadFromPositive()
    {
        System.Reflection.Assembly.LoadFrom("plugin.dll");
    }
}

public sealed class AssemblyLoadFromVariablePositive
{
    public string PluginPath
    {
        set
        {
            var path = value;
            System.Reflection.Assembly.LoadFrom(path);
        }
    }
}

public sealed class AssemblyLoadNameConstantNegative
{
    public AssemblyLoadNameConstantNegative()
    {
        System.Reflection.Assembly.Load("Plugin.Assembly");
    }
}

public sealed class AssemblyLoadNameVariablePositive
{
    public string AssemblyName
    {
        set
        {
            System.Reflection.Assembly.Load(value);
        }
    }
}

public sealed class AssemblyLoadFileNegative
{
    public AssemblyLoadFileNegative()
    {
        System.Reflection.Assembly.LoadFile("plugin.dll");
    }
}

public sealed class ActivatorCreateInstanceConstantNegative
{
    public ActivatorCreateInstanceConstantNegative()
    {
        var type = typeof(MySpecialObject);
        System.Activator.CreateInstance(type);
    }
}

public sealed class ActivatorCreateInstanceVariablePositive
{
    public Type TargetType
    {
        set
        {
            System.Activator.CreateInstance(value);
        }
    }
}

public sealed class WebRequestCreateConstantNegative
{
    public WebRequestCreateConstantNegative()
    {
        _ = System.Net.WebRequest.Create("https://example.invalid/fixed");
    }
}

public sealed class WebRequestCreateVariablePositive
{
    public string Url
    {
        set
        {
            _ = System.Net.WebRequest.Create(value);
        }
    }
}

public sealed class AppDomainExecuteAssemblyConstantPositive
{
    public AppDomainExecuteAssemblyConstantPositive()
    {
        _ = AppDomain.CurrentDomain.ExecuteAssembly("fixed-tool.exe");
    }
}

public sealed class MethodBaseInvokePositive
{
    public MethodBaseInvokePositive()
    {
        System.Reflection.MethodBase method = typeof(MethodBaseInvokeTarget).GetMethod(nameof(MethodBaseInvokeTarget.Run))!;
        method.Invoke(new MethodBaseInvokeTarget(), Array.Empty<object>());
    }
}

public sealed class MethodBaseInvokeTarget
{
    public void Run()
    {
        Helper.InvokeSink();
    }
}

public sealed class PropertyDescriptorGetterPositive
{
    public PropertyDescriptorGetterPositive()
    {
        var descriptor = System.ComponentModel.TypeDescriptor.GetProperties(this)[nameof(Value)];
        descriptor!.GetValue(this);
    }

    public string Value
    {
        get
        {
            Helper.InvokeSink();
            return string.Empty;
        }
    }
}

internal sealed class InternalConstructorPositive
{
    public InternalConstructorPositive()
    {
        Helper.InvokeSink();
    }
}

public sealed class WorkerTask
{
    private readonly string _workerName;

    public WorkerTask(string workerName)
    {
        _workerName = workerName;
    }

    public void Work()
    {
        Helper.InvokeSink();
    }
}

public sealed class WorkerSeed
{
    public WorkerSeed(string workerName)
    {
    }
}

public sealed class WorkerConfig
{
    public WorkerConfig(WorkerSeed seed)
    {
    }
}

public sealed class WorkerManager
{
    private bool _isActive;

    public List<WorkerTask> Workers { get; set; } = [];

    public WorkerManager(string workerName, int count)
    {
        for (var index = 0; index < count; index++)
        {
            Workers.Add(new WorkerTask(workerName));
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (value)
            {
                foreach (var worker in Workers)
                {
                    worker.Work();
                }
            }

            _isActive = value;
        }
    }
}

public sealed class InvalidWorkerConfig
{
    public InvalidWorkerConfig(string workerName)
    {
    }

    public InvalidWorkerConfig(string workerName, int count)
    {
    }
}

public sealed class RecursiveDependencyManager
{
    public RecursiveDependencyManager(WorkerConfig config)
    {
    }

    public bool IsActive
    {
        set
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class InvalidDependencyManager
{
    public InvalidDependencyManager(InvalidWorkerConfig config)
    {
    }

    public bool IsActive
    {
        set
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class MultiCtorManager
{
    public MultiCtorManager(string workerName)
    {
    }

    public MultiCtorManager(string workerName, int count)
    {
    }

    public bool IsActive
    {
        set
        {
            Helper.InvokeSink();
        }
    }
}

public sealed class PropertyAccessorTarget
{
    public int Value { get; set; }
}

public static class PropertyAccessorBridge
{
    public static int ReadAfterWrite()
    {
        var target = new PropertyAccessorTarget();
        target.Value = 7;
        return target.Value;
    }
}

public static class UriConsumer
{
    public static void Accept(Uri uri)
    {
    }
}

public sealed class UriConstantSource
{
    public UriConstantSource()
    {
        UriConsumer.Accept(new Uri("https://example.invalid/demo"));
    }
}

public sealed class ProcessStartParameterlessPositive
{
    public ProcessStartParameterlessPositive()
    {
        var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe");
        process.Start();
    }
}

public sealed class ProcessStartStringPositive
{
    public string FileName
    {
        set
        {
            System.Diagnostics.Process.Start(value);
        }
    }
}
