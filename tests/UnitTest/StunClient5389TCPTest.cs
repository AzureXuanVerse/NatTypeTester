using Dns.Net.Clients;
using STUN;
using STUN.Client;
using STUN.Enums;
using STUN.Proxy;
using STUN.StunResult;
using System.Net;

namespace UnitTest;

public class StunClient5389TCPTest
{
	private readonly DefaultAClient _dnsClient = new();

	private static readonly IPEndPoint Any = new(IPAddress.Any, 0);

	[Test]
	public async Task BindingTestSuccessAsync(CancellationToken cancellationToken)
	{
		Skip.When(TestEnvironment.IsCI, "Skipped on CI");

		IPAddress ip = await _dnsClient.QueryAsync(TestEnvironment.StunServerHost, cancellationToken);
		using IStunClient5389 client = new StunClient5389TCP(new IPEndPoint(ip, StunServer.DefaultPort), Any);

		StunResult5389 response = await client.BindingTestAsync(cancellationToken);

		await Assert.That(response.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(response.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(response.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(response.PublicEndPoint).IsNotNull();
		await Assert.That(response.LocalEndPoint).IsNotNull();
		await Assert.That(response.OtherEndPoint).IsNotNull();
	}

	[Test]
	public async Task BindingTestFailAsync(CancellationToken cancellationToken)
	{
		IPAddress ip = IPAddress.Parse(@"1.1.1.1");
		using IStunClient5389 client = new StunClient5389TCP(new IPEndPoint(ip, StunServer.DefaultPort), Any);

		StunResult5389 response = await client.BindingTestAsync(cancellationToken);

		await Assert.That(response.BindingTestResult).IsEqualTo(BindingTestResult.Fail);
		await Assert.That(response.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(response.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(response.PublicEndPoint).IsNull();
		await Assert.That(response.LocalEndPoint).IsNull();
		await Assert.That(response.OtherEndPoint).IsNull();
	}

	[Test]
	public async Task TlsBindingTestSuccessAsync(CancellationToken cancellationToken)
	{
		Skip.When(TestEnvironment.IsCI, "Skipped on CI");

		await Assert.That(StunServer.TryParse(TestEnvironment.StunServerHost, out StunServer? stunServer, StunServer.DefaultTlsPort)).IsTrue();
		await Assert.That(stunServer).IsNotNull();
		IPAddress ip = await _dnsClient.QueryAsync(stunServer.Hostname, cancellationToken);
		ITcpProxy tls = new TlsProxy(stunServer.Hostname);
		using IStunClient5389 client = new StunClient5389TCP(new IPEndPoint(ip, stunServer.Port), Any, tls);

		StunResult5389 response = await client.BindingTestAsync(cancellationToken);

		await Assert.That(response.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(response.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(response.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(response.PublicEndPoint).IsNotNull();
		await Assert.That(response.LocalEndPoint).IsNotNull();
		await Assert.That(response.OtherEndPoint).IsNotNull();
	}

	[Test]
	[Explicit]
	[ClassDataSource<StunServerList>(Shared = SharedType.PerClass)]
	public async Task TestServerAsync(StunServerList serverList, CancellationToken cancellationToken)
	{
		foreach (string host in serverList.Hosts)
		{
			try
			{
				if (!HostnameEndpoint.TryParse(host, out HostnameEndpoint? hostEndpoint, StunServer.DefaultPort))
				{
					continue;
				}

				IPAddress ip = await _dnsClient.QueryAsync(hostEndpoint.Hostname, cancellationToken);
				using IStunClient5389 client = new StunClient5389TCP(new IPEndPoint(ip, hostEndpoint.Port), Any);

				await client.QueryAsync(cancellationToken);

				if (client.State.MappingBehavior is MappingBehavior.AddressAndPortDependent or MappingBehavior.AddressDependent or MappingBehavior.EndpointIndependent or MappingBehavior.Direct)
				{
					Console.WriteLine(host);
				}
			}
			catch
			{
				// ignored
			}
		}
	}

	[Test]
	[Explicit]
	[ClassDataSource<StunServerList>(Shared = SharedType.PerClass)]
	public async Task TestTlsServerAsync(StunServerList serverList, CancellationToken cancellationToken)
	{
		foreach (string host in serverList.Hosts)
		{
			try
			{
				if (!HostnameEndpoint.TryParse(host, out HostnameEndpoint? hostEndpoint, StunServer.DefaultTlsPort))
				{
					continue;
				}

				IPAddress ip = await _dnsClient.QueryAsync(hostEndpoint.Hostname, cancellationToken);
				ITcpProxy proxy = new TlsProxy(hostEndpoint.Hostname);
				using IStunClient5389 client = new StunClient5389TCP(new IPEndPoint(ip, StunServer.DefaultTlsPort), Any, proxy);

				await client.QueryAsync(cancellationToken);

				if (client.State.MappingBehavior is MappingBehavior.AddressAndPortDependent or MappingBehavior.AddressDependent or MappingBehavior.EndpointIndependent or MappingBehavior.Direct)
				{
					Console.WriteLine(host);
				}
			}
			catch
			{
				// ignored
			}
		}
	}

	[Test]
	public async Task FilteringBehaviorTestAsync(CancellationToken cancellationToken)
	{
		await Assert.That(async () =>
		{
			using IStunClient5389 client = new StunClient5389TCP(new IPEndPoint(IPAddress.Loopback, 3478), Any);
			await client.FilteringBehaviorTestAsync(cancellationToken);
		}).Throws<NotSupportedException>();
	}
}
