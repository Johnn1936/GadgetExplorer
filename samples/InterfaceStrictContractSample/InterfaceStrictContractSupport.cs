/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks;

public static class InterfaceStrictSink
{
    public static void Hit()
    {
    }
}

public interface IStrictWorker
{
    void Execute();
}

public interface IJsonFamilyWorker : IStrictWorker
{
}

public interface IXmlFamilyWorker : IStrictWorker
{
}

public interface ICarrierMarker
{
}

public interface IAlternateWorkerView
{
    void Execute();
}

public sealed class JsonWorker : IStrictWorker
{
    public void Execute() => InterfaceStrictSink.Hit();
}

public sealed class XmlWorker : IStrictWorker
{
    public void Execute() => InterfaceStrictSink.Hit();
}

public sealed class FlatWorker : IStrictWorker
{
    public void Execute() => InterfaceStrictSink.Hit();
}

public sealed class SilentJsonWorker : IStrictWorker
{
    public void Execute()
    {
    }
}

public sealed class SilentXmlWorker : IStrictWorker
{
    public void Execute()
    {
    }
}

public sealed class SilentFlatWorker : IStrictWorker
{
    public void Execute()
    {
    }
}

public sealed class MultiJsonWorker : IStrictWorker, ICarrierMarker, IAlternateWorkerView
{
    public void Execute() => InterfaceStrictSink.Hit();
}

public sealed class MultiSilentWorker : IStrictWorker, ICarrierMarker, IAlternateWorkerView
{
    public void Execute()
    {
    }
}

public sealed class JsonFamilyWorker : IJsonFamilyWorker
{
    public void Execute() => InterfaceStrictSink.Hit();
}

public sealed class XmlFamilyWorker : IXmlFamilyWorker
{
    public void Execute() => InterfaceStrictSink.Hit();
}

public sealed class SilentJsonFamilyWorker : IJsonFamilyWorker
{
    public void Execute()
    {
    }
}

public sealed class SilentXmlFamilyWorker : IXmlFamilyWorker
{
    public void Execute()
    {
    }
}

public static class BranchChooser
{
    public static bool Choose() => DateTime.UtcNow.Ticks >= 0 && Environment.TickCount != int.MinValue;
}

public static class OneHopRelay
{
    public static void Dispatch(IStrictWorker worker) => worker.Execute();
}

public static class ReturnRelay
{
    public static IStrictWorker MakeJson() => new JsonWorker();

    public static IStrictWorker MakeXml() => new XmlWorker();

    public static IStrictWorker MakeFlat() => new FlatWorker();

    public static IStrictWorker MakeSilentJson() => new SilentJsonWorker();

    public static IStrictWorker MakeSilentXml() => new SilentXmlWorker();

    public static IStrictWorker MakeSilentFlat() => new SilentFlatWorker();
}

public static class MultiHopRelay
{
    public static void Start(IStrictWorker worker) => Continue(worker);

    private static void Continue(IStrictWorker worker) => Finish(worker);

    private static void Finish(IStrictWorker worker) => worker.Execute();
}

public static class AliasRelay
{
    public static IStrictWorker Identity(IStrictWorker worker) => worker;

    public static IStrictWorker First(IStrictWorker first, IStrictWorker second)
    {
        _ = second;
        return first;
    }

    public static IStrictWorker Second(IStrictWorker first, IStrictWorker second)
    {
        _ = first;
        return second;
    }
}

public sealed class InstanceFieldHost
{
    private IStrictWorker? _worker;

    public void Store(IStrictWorker worker) => _worker = worker;

    public void ExecuteStored() => _worker?.Execute();
}

public static class StaticFieldHost
{
    private static IStrictWorker? _worker;

    public static void Store(IStrictWorker worker) => _worker = worker;

    public static void ExecuteStored() => _worker?.Execute();
}

public sealed record WorkerBox(IStrictWorker Worker);

public static class WorkerBoxRelay
{
    public static void Run(WorkerBox box) => box.Worker.Execute();
}

public sealed class GenericRelay<TWorker>
    where TWorker : IStrictWorker
{
    public void Run(TWorker worker) => worker.Execute();
}

public sealed class DelegatePublisher
{
    public event Action? Raised;

    public void Raise() => Raised?.Invoke();
}

public static class DelegateRelay
{
    public static void Invoke(Action action) => action();
}

public static class AsyncRelay
{
    public static async Task RunAfterAwait(IStrictWorker worker)
    {
        await Task.Yield();
        worker.Execute();
    }

    public static async Task RunAfterResult(IStrictWorker worker)
    {
        await Task.FromResult(42);
        worker.Execute();
    }
}

public static class IteratorRelay
{
    public static IEnumerable<int> RunAfterYield(IStrictWorker worker)
    {
        yield return 1;
        worker.Execute();
    }
}

public static class CollectionRelay
{
    public static void RunFromArray(IStrictWorker worker)
    {
        IStrictWorker[] workers = new IStrictWorker[1];
        workers[0] = worker;
        workers[0].Execute();
    }

    public static void RunFromList(IStrictWorker worker)
    {
        var workers = new List<IStrictWorker> { worker };
        workers[0].Execute();
    }

    public static void RunFromMap(IStrictWorker worker)
    {
        var workers = new Dictionary<string, IStrictWorker>
        {
            ["worker"] = worker
        };

        workers["worker"].Execute();
    }
}

public enum WorkerMode
{
    Json,
    Xml,
    Flat,
    SilentJson,
    SilentXml,
    SilentFlat
}

public static class ModeFactory
{
    public static IStrictWorker Create(WorkerMode mode)
        => mode switch
        {
            WorkerMode.Json => new JsonWorker(),
            WorkerMode.Xml => new XmlWorker(),
            WorkerMode.Flat => new FlatWorker(),
            WorkerMode.SilentJson => new SilentJsonWorker(),
            WorkerMode.SilentXml => new SilentXmlWorker(),
            _ => new SilentFlatWorker()
        };
}

public static class IntrinsicLikeRelay
{
    public static void RunFromTask(IStrictWorker worker)
        => Task.FromResult(worker).Result.Execute();

    public static void RunFromLazy(IStrictWorker worker)
        => new Lazy<IStrictWorker>(() => worker).Value.Execute();

    public static void RunFromConcurrentDictionary(IStrictWorker worker)
    {
        var map = new ConcurrentDictionary<string, IStrictWorker>();
        map["worker"] = worker;
        map["worker"].Execute();
    }
}

public static class ReflectionRelay
{
    public static void RunFromType(Type workerType)
        => ((IStrictWorker)Activator.CreateInstance(workerType)!).Execute();

    public static void RunFromServiceLocator(Type serviceType)
        => ((IStrictWorker)SimpleServiceLocator.Resolve(serviceType)).Execute();

    public static void RunFromPluginName(string pluginName)
        => PluginCatalog.Resolve(pluginName).Execute();
}

public static class SimpleServiceLocator
{
    public static object Resolve(Type serviceType)
        => serviceType == typeof(JsonWorker)
            ? new JsonWorker()
            : serviceType == typeof(XmlWorker)
                ? new XmlWorker()
                : serviceType == typeof(FlatWorker)
                    ? new FlatWorker()
                    : serviceType == typeof(SilentJsonWorker)
                        ? new SilentJsonWorker()
                        : serviceType == typeof(SilentXmlWorker)
                            ? new SilentXmlWorker()
                            : new SilentFlatWorker();
}

public static class PluginCatalog
{
    public static IStrictWorker Resolve(string name)
        => name switch
        {
            "json" => new JsonWorker(),
            "xml" => new XmlWorker(),
            "flat" => new FlatWorker(),
            "silent-json" => new SilentJsonWorker(),
            "silent-xml" => new SilentXmlWorker(),
            _ => new SilentFlatWorker()
        };
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ModeledReturnTypeAttribute(Type returnType) : Attribute
{
    public Type ReturnType { get; } = returnType;
}

public static class AnnotatedOpaqueFactories
{
    [ModeledReturnType(typeof(JsonWorker))]
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetJson();

    [ModeledReturnType(typeof(XmlWorker))]
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetXml();

    [ModeledReturnType(typeof(FlatWorker))]
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetFlat();

    [ModeledReturnType(typeof(SilentJsonWorker))]
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetSilentJson();

    [ModeledReturnType(typeof(SilentXmlWorker))]
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetSilentXml();

    [ModeledReturnType(typeof(SilentFlatWorker))]
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetSilentFlat();
}

public static class UnmodeledOpaqueFactories
{
    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetUnknown();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetOtherUnknown();

    [MethodImpl(MethodImplOptions.InternalCall)]
    public static extern IStrictWorker GetThirdUnknown();
}

public sealed class SingleItemQueue
{
    private readonly Queue<IStrictWorker> _items = new();

    public void Enqueue(IStrictWorker worker) => _items.Enqueue(worker);

    public IStrictWorker Dequeue() => _items.Dequeue();
}

public static class QueueRelay
{
    public static void RunThroughQueue(IStrictWorker worker)
    {
        var queue = new SingleItemQueue();
        queue.Enqueue(worker);
        queue.Dequeue().Execute();
    }

    public static void RunThroughChannel(IStrictWorker worker)
    {
        Channel<IStrictWorker> channel = Channel.CreateUnbounded<IStrictWorker>();
        channel.Writer.TryWrite(worker);
        channel.Reader.ReadAsync().AsTask().Result.Execute();
    }
}

public sealed class MutableTransport
{
    public IStrictWorker? Worker { get; set; }

    public void ExecuteCurrent() => Worker?.Execute();
}

public sealed class WorkerDispatchProxy : DispatchProxy
{
    public IStrictWorker? Target { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        => targetMethod?.Invoke(Target, args);
}

public static class ProxyRelay
{
    public static void Run(IStrictWorker worker)
    {
        IStrictWorker proxy = DispatchProxy.Create<IStrictWorker, WorkerDispatchProxy>();
        ((WorkerDispatchProxy)(object)proxy).Target = worker;
        proxy.Execute();
    }
}
