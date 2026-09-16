using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._WF.Audio.InternetSound;
using NUnit.Framework;

namespace Content.Tests._WF.Audio;

[TestFixture]
public sealed class InternetSoundDownloadProxyTest
{
    [TestCase("127.0.0.1")]
    [TestCase("10.1.2.3")]
    [TestCase("172.16.1.1")]
    [TestCase("192.168.1.1")]
    [TestCase("169.254.169.254")]
    [TestCase("168.63.129.16")]
    [TestCase("100.100.100.200")]
    [TestCase("0.0.0.0")]
    [TestCase("224.0.0.1")]
    [TestCase("198.18.0.1")]
    [TestCase("::1")]
    [TestCase("::ffff:127.0.0.1")]
    [TestCase("fc00::1")]
    [TestCase("fe80::1")]
    [TestCase("64:ff9b::a00:1")]
    [TestCase("2002:7f00:1::")]
    [TestCase("2001:db8::1")]
    public void PrivateAndSpecialAddressesAreBlocked(string address) =>
        Assert.That(InternetSoundDownloadProxy.IsNonPublic(IPAddress.Parse(address)), Is.True);

    [TestCase("8.8.8.8")]
    [TestCase("1.1.1.1")]
    [TestCase("2606:4700:4700::1111")]
    public void PublicAddressesAreAllowed(string address) =>
        Assert.That(InternetSoundDownloadProxy.IsNonPublic(IPAddress.Parse(address)), Is.False);

    [Test]
    public async Task RebindingIsRecheckedAndConnectionsUseTheValidatedIp()
    {
        var resolutions = 0;
        var connections = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var proxy = new InternetSoundDownloadProxy(timeout.Token,
            (_, _) => Task.FromResult(new[] { IPAddress.Parse(++resolutions == 1 ? "8.8.8.8" : "127.0.0.1") }),
            (address, port, _) =>
            {
                Assert.That(address, Is.EqualTo(IPAddress.Parse("8.8.8.8")));
                Assert.That(port, Is.EqualTo(443));
                connections++;
                return Task.FromResult<Stream>(new MemoryStream());
            });
        Assert.That(await Request(proxy.Url, "rebind.test", 443, timeout.Token), Is.Zero);
        Assert.That(await Request(proxy.Url, "rebind.test", 443, timeout.Token), Is.EqualTo(2));
        Assert.That(connections, Is.EqualTo(1));
    }

    [Test]
    public async Task MixedDnsAnswersAndNonWebPortsAreRejectedBeforeConnecting()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connections = 0;
        await using var proxy = new InternetSoundDownloadProxy(timeout.Token,
            (_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Loopback }),
            (_, _, _) => { connections++; return Task.FromResult<Stream>(new MemoryStream()); });
        Assert.That(await Request(proxy.Url, "mixed.test", 443, timeout.Token), Is.EqualTo(2));
        Assert.That(await Request(proxy.Url, "8.8.8.8", 22, timeout.Token), Is.EqualTo(2));
        Assert.That(connections, Is.Zero);
    }

    [Test]
    public async Task HttpRedirectToPrivateHostCannotOpenASecondConnection()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var origin = new TcpListener(IPAddress.Loopback, 0);
        origin.Start();
        var originPort = ((IPEndPoint) origin.LocalEndpoint).Port;
        var connections = 0;
        await using var proxy = new InternetSoundDownloadProxy(timeout.Token,
            (host, _) => Task.FromResult(new[] { host == "public.test" ? IPAddress.Parse("8.8.8.8") : IPAddress.Loopback }),
            async (_, _, token) =>
            {
                connections++;
                // Only the test connector maps the approved public address to a local fixture.
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(IPAddress.Loopback, originPort, token);
                return new NetworkStream(socket, ownsSocket: true);
            });
        var serve = Task.Run(async () =>
        {
            using var peer = await origin.AcceptTcpClientAsync(timeout.Token);
            var stream = peer.GetStream();
            var buffer = new byte[4096];
            await stream.ReadAsync(buffer, timeout.Token);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 302 Found\r\nLocation: http://private.test/audio\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), timeout.Token);
        });
        using var handler = new HttpClientHandler { Proxy = new WebProxy(proxy.Url), UseProxy = true };
        using var http = new HttpClient(handler);
        Assert.ThrowsAsync<HttpRequestException>(async () => await http.GetAsync("http://public.test/audio", timeout.Token));
        await serve;
        Assert.That(proxy.DeniedHost, Is.EqualTo("private.test"));
        Assert.That(connections, Is.EqualTo(1));
    }

    [Test]
    public async Task DisposingClosesAnIncompleteHandshake()
    {
        var proxy = new InternetSoundDownloadProxy(CancellationToken.None);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, new Uri(proxy.Url).Port);
        await proxy.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static async Task<byte> Request(string proxyUrl, string host, int port, CancellationToken token)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, new Uri(proxyUrl).Port, token);
        var stream = client.GetStream();
        await stream.WriteAsync(new byte[] { 5, 1, 0 }, token);
        var greeting = new byte[2];
        await stream.ReadExactlyAsync(greeting, token);
        var name = Encoding.ASCII.GetBytes(host);
        using var request = new MemoryStream();
        request.Write(new byte[] { 5, 1, 0, 3, (byte) name.Length });
        request.Write(name);
        request.Write(new[] { (byte) (port >> 8), (byte) port });
        await stream.WriteAsync(request.ToArray(), token);
        var reply = new byte[10];
        await stream.ReadExactlyAsync(reply, token);
        return reply[1];
    }
}
