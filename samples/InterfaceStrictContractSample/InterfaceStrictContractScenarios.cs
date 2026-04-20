/*
 * Copyright (C) 2026 Dane Evans
 * This file is part of GadgetExplorer
 * SPDX-License-Identifier: GPL-3.0-or-later
 */

using System.Collections.Generic;

public static class Case01Scenarios
{
    public static void PositiveA() { IStrictWorker worker = new JsonWorker(); worker.Execute(); }
    public static void PositiveB() { IStrictWorker worker = new XmlWorker(); worker.Execute(); }
    public static void PositiveC() { IStrictWorker worker = new FlatWorker(); worker.Execute(); }
    public static void NegativeA() { IStrictWorker worker = new SilentJsonWorker(); worker.Execute(); }
    public static void NegativeB() { IStrictWorker worker = new SilentXmlWorker(); worker.Execute(); }
    public static void NegativeC() { IStrictWorker worker = new SilentFlatWorker(); worker.Execute(); }
}

public sealed class Case01SameMethodLocalRoot
{
    public int PositiveA { set => Case01Scenarios.PositiveA(); }
    public int PositiveB { set => Case01Scenarios.PositiveB(); }
    public int PositiveC { set => Case01Scenarios.PositiveC(); }
    public int NegativeA { set => Case01Scenarios.NegativeA(); }
    public int NegativeB { set => Case01Scenarios.NegativeB(); }
    public int NegativeC { set => Case01Scenarios.NegativeC(); }
}

public static class Case02Scenarios
{
    public static void PositiveA() { object worker = new JsonWorker(); ((IStrictWorker)worker).Execute(); }
    public static void PositiveB() { ICarrierMarker marker = new MultiJsonWorker(); ((IStrictWorker)marker).Execute(); }
    public static void PositiveC() { IAlternateWorkerView view = new MultiJsonWorker(); view.Execute(); }
    public static void NegativeA() { object worker = new SilentJsonWorker(); ((IStrictWorker)worker).Execute(); }
    public static void NegativeB() { ICarrierMarker marker = new MultiSilentWorker(); ((IStrictWorker)marker).Execute(); }
    public static void NegativeC() { IAlternateWorkerView view = new MultiSilentWorker(); view.Execute(); }
}

public sealed class Case02CastAdaptationRoot
{
    public int PositiveA { set => Case02Scenarios.PositiveA(); }
    public int PositiveB { set => Case02Scenarios.PositiveB(); }
    public int PositiveC { set => Case02Scenarios.PositiveC(); }
    public int NegativeA { set => Case02Scenarios.NegativeA(); }
    public int NegativeB { set => Case02Scenarios.NegativeB(); }
    public int NegativeC { set => Case02Scenarios.NegativeC(); }
}

public static class Case03Scenarios
{
    public static void PositiveA()
    {
        IStrictWorker worker = BranchChooser.Choose() ? new JsonWorker() : new XmlWorker();
        worker.Execute();
    }

    public static void PositiveB()
    {
        IStrictWorker worker = BranchChooser.Choose() ? new JsonWorker() : new FlatWorker();
        worker.Execute();
    }

    public static void PositiveC()
    {
        IStrictWorker worker = BranchChooser.Choose() ? new XmlWorker() : new FlatWorker();
        worker.Execute();
    }

    public static void NegativeA()
    {
        IStrictWorker worker = BranchChooser.Choose() ? new SilentJsonWorker() : new SilentXmlWorker();
        worker.Execute();
    }

    public static void NegativeB()
    {
        IStrictWorker worker = BranchChooser.Choose() ? new SilentJsonWorker() : new SilentFlatWorker();
        worker.Execute();
    }

    public static void NegativeC()
    {
        IStrictWorker worker = BranchChooser.Choose() ? new SilentXmlWorker() : new SilentFlatWorker();
        worker.Execute();
    }
}

public sealed class Case03FiniteCandidateSetRoot
{
    public int PositiveA { set => Case03Scenarios.PositiveA(); }
    public int PositiveB { set => Case03Scenarios.PositiveB(); }
    public int PositiveC { set => Case03Scenarios.PositiveC(); }
    public int NegativeA { set => Case03Scenarios.NegativeA(); }
    public int NegativeB { set => Case03Scenarios.NegativeB(); }
    public int NegativeC { set => Case03Scenarios.NegativeC(); }
}

public static class Case04Scenarios
{
    private static IJsonFamilyWorker GetJsonFamily() => new JsonFamilyWorker();
    private static IJsonFamilyWorker GetSilentJsonFamily() => new SilentJsonFamilyWorker();

    public static void PositiveA() { IJsonFamilyWorker worker = new JsonFamilyWorker(); worker.Execute(); }
    public static void PositiveB() { IXmlFamilyWorker worker = new XmlFamilyWorker(); worker.Execute(); }
    public static void PositiveC() { IJsonFamilyWorker worker = GetJsonFamily(); worker.Execute(); }
    public static void NegativeA() { IJsonFamilyWorker worker = new SilentJsonFamilyWorker(); worker.Execute(); }
    public static void NegativeB() { IXmlFamilyWorker worker = new SilentXmlFamilyWorker(); worker.Execute(); }
    public static void NegativeC() { IJsonFamilyWorker worker = GetSilentJsonFamily(); worker.Execute(); }
}

public sealed class Case04StrongStaticNarrowingRoot
{
    public int PositiveA { set => Case04Scenarios.PositiveA(); }
    public int PositiveB { set => Case04Scenarios.PositiveB(); }
    public int PositiveC { set => Case04Scenarios.PositiveC(); }
    public int NegativeA { set => Case04Scenarios.NegativeA(); }
    public int NegativeB { set => Case04Scenarios.NegativeB(); }
    public int NegativeC { set => Case04Scenarios.NegativeC(); }
}

public static class Case05Scenarios
{
    public static void PositiveA() => OneHopRelay.Dispatch(new JsonWorker());
    public static void PositiveB() => OneHopRelay.Dispatch(new XmlWorker());
    public static void PositiveC() => OneHopRelay.Dispatch(new FlatWorker());
    public static void NegativeA() => OneHopRelay.Dispatch(new SilentJsonWorker());
    public static void NegativeB() => OneHopRelay.Dispatch(new SilentXmlWorker());
    public static void NegativeC() => OneHopRelay.Dispatch(new SilentFlatWorker());
}

public sealed class Case05OneHopParameterRelayRoot
{
    public int PositiveA { set => Case05Scenarios.PositiveA(); }
    public int PositiveB { set => Case05Scenarios.PositiveB(); }
    public int PositiveC { set => Case05Scenarios.PositiveC(); }
    public int NegativeA { set => Case05Scenarios.NegativeA(); }
    public int NegativeB { set => Case05Scenarios.NegativeB(); }
    public int NegativeC { set => Case05Scenarios.NegativeC(); }
}

public static class Case06Scenarios
{
    public static void PositiveA() => ReturnRelay.MakeJson().Execute();
    public static void PositiveB() => ReturnRelay.MakeXml().Execute();
    public static void PositiveC() => ReturnRelay.MakeFlat().Execute();
    public static void NegativeA() => ReturnRelay.MakeSilentJson().Execute();
    public static void NegativeB() => ReturnRelay.MakeSilentXml().Execute();
    public static void NegativeC() => ReturnRelay.MakeSilentFlat().Execute();
}

public sealed class Case06ReturnRelayRoot
{
    public int PositiveA { set => Case06Scenarios.PositiveA(); }
    public int PositiveB { set => Case06Scenarios.PositiveB(); }
    public int PositiveC { set => Case06Scenarios.PositiveC(); }
    public int NegativeA { set => Case06Scenarios.NegativeA(); }
    public int NegativeB { set => Case06Scenarios.NegativeB(); }
    public int NegativeC { set => Case06Scenarios.NegativeC(); }
}

public static class Case07Scenarios
{
    public static void PositiveA() => MultiHopRelay.Start(new JsonWorker());
    public static void PositiveB() => MultiHopRelay.Start(new XmlWorker());
    public static void PositiveC() => MultiHopRelay.Start(new FlatWorker());
    public static void NegativeA() => MultiHopRelay.Start(new SilentJsonWorker());
    public static void NegativeB() => MultiHopRelay.Start(new SilentXmlWorker());
    public static void NegativeC() => MultiHopRelay.Start(new SilentFlatWorker());
}

public sealed class Case07MultiHopRelayRoot
{
    public int PositiveA { set => Case07Scenarios.PositiveA(); }
    public int PositiveB { set => Case07Scenarios.PositiveB(); }
    public int PositiveC { set => Case07Scenarios.PositiveC(); }
    public int NegativeA { set => Case07Scenarios.NegativeA(); }
    public int NegativeB { set => Case07Scenarios.NegativeB(); }
    public int NegativeC { set => Case07Scenarios.NegativeC(); }
}

public static class Case08Scenarios
{
    public static void PositiveA() => AliasRelay.Identity(new JsonWorker()).Execute();
    public static void PositiveB() => AliasRelay.First(new XmlWorker(), new SilentFlatWorker()).Execute();
    public static void PositiveC() => AliasRelay.Second(new SilentJsonWorker(), new FlatWorker()).Execute();
    public static void NegativeA() => AliasRelay.Identity(new SilentJsonWorker()).Execute();
    public static void NegativeB() => AliasRelay.First(new SilentXmlWorker(), new JsonWorker()).Execute();
    public static void NegativeC() => AliasRelay.Second(new FlatWorker(), new SilentFlatWorker()).Execute();
}

public sealed class Case08AliasPreservingRelayRoot
{
    public int PositiveA { set => Case08Scenarios.PositiveA(); }
    public int PositiveB { set => Case08Scenarios.PositiveB(); }
    public int PositiveC { set => Case08Scenarios.PositiveC(); }
    public int NegativeA { set => Case08Scenarios.NegativeA(); }
    public int NegativeB { set => Case08Scenarios.NegativeB(); }
    public int NegativeC { set => Case08Scenarios.NegativeC(); }
}

public static class Case09Scenarios
{
    public static void PositiveA()
    {
        var host = new InstanceFieldHost();
        host.Store(new JsonWorker());
        host.ExecuteStored();
    }

    public static void PositiveB()
    {
        var host = new InstanceFieldHost();
        host.Store(new XmlWorker());
        host.ExecuteStored();
    }

    public static void PositiveC()
    {
        var host = new InstanceFieldHost();
        host.Store(new FlatWorker());
        host.ExecuteStored();
    }

    public static void NegativeA()
    {
        var host = new InstanceFieldHost();
        host.Store(new SilentJsonWorker());
        host.ExecuteStored();
    }

    public static void NegativeB()
    {
        var host = new InstanceFieldHost();
        host.Store(new JsonWorker());
        host.Store(new SilentXmlWorker());
        host.ExecuteStored();
    }

    public static void NegativeC()
    {
        var host = new InstanceFieldHost();
        host.Store(new SilentFlatWorker());
        host.ExecuteStored();
    }
}

public sealed class Case09InstanceFieldRelayRoot
{
    public int PositiveA { set => Case09Scenarios.PositiveA(); }
    public int PositiveB { set => Case09Scenarios.PositiveB(); }
    public int PositiveC { set => Case09Scenarios.PositiveC(); }
    public int NegativeA { set => Case09Scenarios.NegativeA(); }
    public int NegativeB { set => Case09Scenarios.NegativeB(); }
    public int NegativeC { set => Case09Scenarios.NegativeC(); }
}

public static class Case10Scenarios
{
    public static void PositiveA() { StaticFieldHost.Store(new JsonWorker()); StaticFieldHost.ExecuteStored(); }
    public static void PositiveB() { StaticFieldHost.Store(new XmlWorker()); StaticFieldHost.ExecuteStored(); }
    public static void PositiveC() { StaticFieldHost.Store(new FlatWorker()); StaticFieldHost.ExecuteStored(); }
    public static void NegativeA() { StaticFieldHost.Store(new SilentJsonWorker()); StaticFieldHost.ExecuteStored(); }
    public static void NegativeB() { StaticFieldHost.Store(new JsonWorker()); StaticFieldHost.Store(new SilentXmlWorker()); StaticFieldHost.ExecuteStored(); }
    public static void NegativeC() { StaticFieldHost.Store(new SilentFlatWorker()); StaticFieldHost.ExecuteStored(); }
}

public sealed class Case10StaticFieldRelayRoot
{
    public int PositiveA { set => Case10Scenarios.PositiveA(); }
    public int PositiveB { set => Case10Scenarios.PositiveB(); }
    public int PositiveC { set => Case10Scenarios.PositiveC(); }
    public int NegativeA { set => Case10Scenarios.NegativeA(); }
    public int NegativeB { set => Case10Scenarios.NegativeB(); }
    public int NegativeC { set => Case10Scenarios.NegativeC(); }
}

public static class Case11Scenarios
{
    public static void PositiveA() => WorkerBoxRelay.Run(new WorkerBox(new JsonWorker()));
    public static void PositiveB() => WorkerBoxRelay.Run(new WorkerBox(new XmlWorker()));
    public static void PositiveC() => WorkerBoxRelay.Run(new WorkerBox(AliasRelay.Identity(new FlatWorker())));
    public static void NegativeA() => WorkerBoxRelay.Run(new WorkerBox(new SilentJsonWorker()));
    public static void NegativeB() => WorkerBoxRelay.Run(new WorkerBox(new SilentXmlWorker()));
    public static void NegativeC() => WorkerBoxRelay.Run(new WorkerBox(new SilentFlatWorker()));
}

public sealed class Case11TransparentWrapperRoot
{
    public int PositiveA { set => Case11Scenarios.PositiveA(); }
    public int PositiveB { set => Case11Scenarios.PositiveB(); }
    public int PositiveC { set => Case11Scenarios.PositiveC(); }
    public int NegativeA { set => Case11Scenarios.NegativeA(); }
    public int NegativeB { set => Case11Scenarios.NegativeB(); }
    public int NegativeC { set => Case11Scenarios.NegativeC(); }
}

public static class Case12Scenarios
{
    public static void PositiveA() => new GenericRelay<JsonWorker>().Run(new JsonWorker());
    public static void PositiveB() => new GenericRelay<XmlWorker>().Run(new XmlWorker());
    public static void PositiveC() => new GenericRelay<FlatWorker>().Run(new FlatWorker());
    public static void NegativeA() => new GenericRelay<SilentJsonWorker>().Run(new SilentJsonWorker());
    public static void NegativeB() => new GenericRelay<SilentXmlWorker>().Run(new SilentXmlWorker());
    public static void NegativeC() => new GenericRelay<SilentFlatWorker>().Run(new SilentFlatWorker());
}

public sealed class Case12GenericTransportRoot
{
    public int PositiveA { set => Case12Scenarios.PositiveA(); }
    public int PositiveB { set => Case12Scenarios.PositiveB(); }
    public int PositiveC { set => Case12Scenarios.PositiveC(); }
    public int NegativeA { set => Case12Scenarios.NegativeA(); }
    public int NegativeB { set => Case12Scenarios.NegativeB(); }
    public int NegativeC { set => Case12Scenarios.NegativeC(); }
}

public static class Case13Scenarios
{
    public static void PositiveA()
    {
        IStrictWorker worker = new JsonWorker();
        Action action = () => worker.Execute();
        action();
    }

    public static void PositiveB()
    {
        IStrictWorker worker = new XmlWorker();
        DelegateRelay.Invoke(() => worker.Execute());
    }

    public static void PositiveC()
    {
        IStrictWorker worker = new FlatWorker();
        var publisher = new DelegatePublisher();
        publisher.Raised += () => worker.Execute();
        publisher.Raise();
    }

    public static void NegativeA()
    {
        IStrictWorker worker = new SilentJsonWorker();
        Action action = () => worker.Execute();
        action();
    }

    public static void NegativeB()
    {
        IStrictWorker worker = new SilentXmlWorker();
        DelegateRelay.Invoke(() => worker.Execute());
    }

    public static void NegativeC()
    {
        IStrictWorker worker = new SilentFlatWorker();
        var publisher = new DelegatePublisher();
        publisher.Raised += () => worker.Execute();
        publisher.Raise();
    }
}

public sealed class Case13DelegateCaptureRoot
{
    public int PositiveA { set => Case13Scenarios.PositiveA(); }
    public int PositiveB { set => Case13Scenarios.PositiveB(); }
    public int PositiveC { set => Case13Scenarios.PositiveC(); }
    public int NegativeA { set => Case13Scenarios.NegativeA(); }
    public int NegativeB { set => Case13Scenarios.NegativeB(); }
    public int NegativeC { set => Case13Scenarios.NegativeC(); }
}

public static class Case14Scenarios
{
    public static void PositiveA() => AsyncRelay.RunAfterAwait(new JsonWorker()).GetAwaiter().GetResult();
    public static void PositiveB() => AsyncRelay.RunAfterResult(new XmlWorker()).GetAwaiter().GetResult();
    public static void PositiveC()
    {
        foreach (int _ in IteratorRelay.RunAfterYield(new FlatWorker()))
        {
        }
    }

    public static void NegativeA() => AsyncRelay.RunAfterAwait(new SilentJsonWorker()).GetAwaiter().GetResult();
    public static void NegativeB() => AsyncRelay.RunAfterResult(new SilentXmlWorker()).GetAwaiter().GetResult();
    public static void NegativeC()
    {
        foreach (int _ in IteratorRelay.RunAfterYield(new SilentFlatWorker()))
        {
        }
    }
}

public sealed class Case14AsyncIteratorRoot
{
    public int PositiveA { set => Case14Scenarios.PositiveA(); }
    public int PositiveB { set => Case14Scenarios.PositiveB(); }
    public int PositiveC { set => Case14Scenarios.PositiveC(); }
    public int NegativeA { set => Case14Scenarios.NegativeA(); }
    public int NegativeB { set => Case14Scenarios.NegativeB(); }
    public int NegativeC { set => Case14Scenarios.NegativeC(); }
}

public static class Case15Scenarios
{
    public static void PositiveA() => CollectionRelay.RunFromArray(new JsonWorker());
    public static void PositiveB() => CollectionRelay.RunFromList(new XmlWorker());
    public static void PositiveC() => CollectionRelay.RunFromMap(new FlatWorker());
    public static void NegativeA() => CollectionRelay.RunFromArray(new SilentJsonWorker());
    public static void NegativeB() => CollectionRelay.RunFromList(new SilentXmlWorker());
    public static void NegativeC() => CollectionRelay.RunFromMap(new SilentFlatWorker());
}

public sealed class Case15CollectionTransportRoot
{
    public int PositiveA { set => Case15Scenarios.PositiveA(); }
    public int PositiveB { set => Case15Scenarios.PositiveB(); }
    public int PositiveC { set => Case15Scenarios.PositiveC(); }
    public int NegativeA { set => Case15Scenarios.NegativeA(); }
    public int NegativeB { set => Case15Scenarios.NegativeB(); }
    public int NegativeC { set => Case15Scenarios.NegativeC(); }
}

public static class Case16Scenarios
{
    public static void PositiveA() => ModeFactory.Create(WorkerMode.Json).Execute();
    public static void PositiveB() => ModeFactory.Create(WorkerMode.Xml).Execute();
    public static void PositiveC() => ModeFactory.Create(WorkerMode.Flat).Execute();
    public static void NegativeA() => ModeFactory.Create(WorkerMode.SilentJson).Execute();
    public static void NegativeB() => ModeFactory.Create(WorkerMode.SilentXml).Execute();
    public static void NegativeC() => ModeFactory.Create(WorkerMode.SilentFlat).Execute();
}

public sealed class Case16FiniteFactoryRoot
{
    public int PositiveA { set => Case16Scenarios.PositiveA(); }
    public int PositiveB { set => Case16Scenarios.PositiveB(); }
    public int PositiveC { set => Case16Scenarios.PositiveC(); }
    public int NegativeA { set => Case16Scenarios.NegativeA(); }
    public int NegativeB { set => Case16Scenarios.NegativeB(); }
    public int NegativeC { set => Case16Scenarios.NegativeC(); }
}

public static class Case17Scenarios
{
    public static void PositiveA() => IntrinsicLikeRelay.RunFromTask(new JsonWorker());
    public static void PositiveB() => IntrinsicLikeRelay.RunFromLazy(new XmlWorker());
    public static void PositiveC() => IntrinsicLikeRelay.RunFromConcurrentDictionary(new FlatWorker());
    public static void NegativeA() => IntrinsicLikeRelay.RunFromTask(new SilentJsonWorker());
    public static void NegativeB() => IntrinsicLikeRelay.RunFromLazy(new SilentXmlWorker());
    public static void NegativeC() => IntrinsicLikeRelay.RunFromConcurrentDictionary(new SilentFlatWorker());
}

public sealed class Case17IntrinsicLikeLibraryRoot
{
    public int PositiveA { set => Case17Scenarios.PositiveA(); }
    public int PositiveB { set => Case17Scenarios.PositiveB(); }
    public int PositiveC { set => Case17Scenarios.PositiveC(); }
    public int NegativeA { set => Case17Scenarios.NegativeA(); }
    public int NegativeB { set => Case17Scenarios.NegativeB(); }
    public int NegativeC { set => Case17Scenarios.NegativeC(); }
}

public static class Case18Scenarios
{
    public static void PositiveA() => ReflectionRelay.RunFromType(typeof(JsonWorker));
    public static void PositiveB() => ReflectionRelay.RunFromServiceLocator(typeof(XmlWorker));
    public static void PositiveC() => ReflectionRelay.RunFromPluginName("flat");
    public static void NegativeA() => ReflectionRelay.RunFromType(typeof(SilentJsonWorker));
    public static void NegativeB() => ReflectionRelay.RunFromServiceLocator(typeof(SilentXmlWorker));
    public static void NegativeC() => ReflectionRelay.RunFromPluginName("silent-flat");
}

public sealed class Case18DynamicResolutionRoot
{
    public int PositiveA { set => Case18Scenarios.PositiveA(); }
    public int PositiveB { set => Case18Scenarios.PositiveB(); }
    public int PositiveC { set => Case18Scenarios.PositiveC(); }
    public int NegativeA { set => Case18Scenarios.NegativeA(); }
    public int NegativeB { set => Case18Scenarios.NegativeB(); }
    public int NegativeC { set => Case18Scenarios.NegativeC(); }
}

public static class Case19Scenarios
{
    public static void PositiveA() => AnnotatedOpaqueFactories.GetJson().Execute();
    public static void PositiveB() => AnnotatedOpaqueFactories.GetXml().Execute();
    public static void PositiveC() => AnnotatedOpaqueFactories.GetFlat().Execute();
    public static void NegativeA() => UnmodeledOpaqueFactories.GetUnknown().Execute();
    public static void NegativeB() => AnnotatedOpaqueFactories.GetSilentJson().Execute();
    public static void NegativeC() => AnnotatedOpaqueFactories.GetSilentXml().Execute();
}

public sealed class Case19OpaqueFactoryRoot
{
    public int PositiveA { set => Case19Scenarios.PositiveA(); }
    public int PositiveB { set => Case19Scenarios.PositiveB(); }
    public int PositiveC { set => Case19Scenarios.PositiveC(); }
    public int NegativeA { set => Case19Scenarios.NegativeA(); }
    public int NegativeB { set => Case19Scenarios.NegativeB(); }
    public int NegativeC { set => Case19Scenarios.NegativeC(); }
}

public static class Case20Scenarios
{
    public static void PositiveA() => QueueRelay.RunThroughQueue(new JsonWorker());
    public static void PositiveB() => QueueRelay.RunThroughChannel(new XmlWorker());
    public static void PositiveC()
    {
        var transport = new MutableTransport { Worker = new FlatWorker() };
        transport.ExecuteCurrent();
    }

    public static void NegativeA() => QueueRelay.RunThroughQueue(new SilentJsonWorker());
    public static void NegativeB() => QueueRelay.RunThroughChannel(new SilentXmlWorker());
    public static void NegativeC()
    {
        var transport = new MutableTransport { Worker = new JsonWorker() };
        transport.Worker = new SilentFlatWorker();
        transport.ExecuteCurrent();
    }
}

public sealed class Case20HardTransportRoot
{
    public int PositiveA { set => Case20Scenarios.PositiveA(); }
    public int PositiveB { set => Case20Scenarios.PositiveB(); }
    public int PositiveC { set => Case20Scenarios.PositiveC(); }
    public int NegativeA { set => Case20Scenarios.NegativeA(); }
    public int NegativeB { set => Case20Scenarios.NegativeB(); }
    public int NegativeC { set => Case20Scenarios.NegativeC(); }
}

public static class Case21Scenarios
{
    public static void PositiveA() => ProxyRelay.Run(new JsonWorker());
    public static void PositiveB() => ProxyRelay.Run(new XmlWorker());
    public static void PositiveC() => ProxyRelay.Run(new FlatWorker());
    public static void NegativeA() => ProxyRelay.Run(new SilentJsonWorker());
    public static void NegativeB() => ProxyRelay.Run(new SilentXmlWorker());
    public static void NegativeC() => ProxyRelay.Run(new SilentFlatWorker());
}

public sealed class Case21DynamicProxyRoot
{
    public int PositiveA { set => Case21Scenarios.PositiveA(); }
    public int PositiveB { set => Case21Scenarios.PositiveB(); }
    public int PositiveC { set => Case21Scenarios.PositiveC(); }
    public int NegativeA { set => Case21Scenarios.NegativeA(); }
    public int NegativeB { set => Case21Scenarios.NegativeB(); }
    public int NegativeC { set => Case21Scenarios.NegativeC(); }
}
