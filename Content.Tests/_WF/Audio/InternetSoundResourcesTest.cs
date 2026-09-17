using System;
using System.Runtime.CompilerServices;
using Content.Shared._WF.Audio.InternetSound;
using Moq;
using NUnit.Framework;
using Robust.Shared.ContentPack;

namespace Content.Tests._WF.Audio;

[TestFixture]
public sealed class InternetSoundResourcesTest
{
    [Test]
    public void RegistryDoesNotKeepDiscardedResourceManagersAlive()
    {
        var manager = new Mock<IResourceManager>().Object;
        Assert.That(InternetSoundResources.For(manager), Is.SameAs(InternetSoundResources.For(manager)),
            "A live manager must reuse its mounted root across reconnects.");
        var discarded = CreateDiscardedManager();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.That(discarded.IsAlive, Is.False, "The static registry must not retain discarded test instances.");
        GC.KeepAlive(manager);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDiscardedManager()
    {
        var manager = new Mock<IResourceManager>().Object;
        InternetSoundResources.For(manager);
        return new WeakReference(manager);
    }
}
