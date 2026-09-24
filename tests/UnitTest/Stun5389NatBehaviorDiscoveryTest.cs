using STUN.Client;
using STUN.Enums;
using STUN.Messages;
using System.Net;
using static STUN.Utils.AttributeExtensions;

namespace UnitTest;

public class Stun5389NatBehaviorDiscoveryTest
{
	private static readonly IPEndPoint LocalAddress1 = IPEndPoint.Parse(@"127.0.0.1:114");
	private static readonly IPEndPoint MappedAddress1 = IPEndPoint.Parse(@"1.1.1.1:114");
	private static readonly IPEndPoint MappedAddress2 = IPEndPoint.Parse(@"1.1.1.1:514");
	private static readonly IPEndPoint MappedAddress3 = IPEndPoint.Parse(@"1.1.1.1:810");
	private static readonly IPEndPoint ServerAddress = IPEndPoint.Parse(@"2.2.2.2:1919");
	private static readonly IPEndPoint ChangedAddress1 = IPEndPoint.Parse(@"3.3.3.3:23333");
	private static readonly IPEndPoint ChangedAddress2 = IPEndPoint.Parse(@"2.2.2.2:810");
	private static readonly IPEndPoint ChangedAddress3 = IPEndPoint.Parse(@"3.3.3.3:1919");

	public static IEnumerable<Func<IPEndPoint?>> UnsupportedOtherAddresses()
	{
		yield return () => null;
		yield return () => new IPEndPoint(ChangedAddress2.Address, ChangedAddress2.Port);
		yield return () => new IPEndPoint(ChangedAddress3.Address, ChangedAddress3.Port);
	}

	public static IEnumerable<Func<(IPEndPoint?, FilteringBehavior)>> FilteringTest3Responses()
	{
		yield return () => (null, FilteringBehavior.AddressAndPortDependent);
		yield return () => (new IPEndPoint(ChangedAddress2.Address, ChangedAddress2.Port), FilteringBehavior.AddressDependent);
		yield return () => (new IPEndPoint(ServerAddress.Address, ServerAddress.Port), FilteringBehavior.UnsupportedServer);
	}

	private static StunResponse CreateBindingResponse(IPEndPoint mapped, IPEndPoint? other, IPEndPoint remote, IPEndPoint local)
	{
		List<StunAttribute> attrs =
		[
			BuildMapping(IpFamily.IPv4, mapped.Address, (ushort)mapped.Port)
		];

		if (other is not null)
		{
			attrs.Add(BuildChangeAddress(IpFamily.IPv4, other.Address, (ushort)other.Port));
		}

		return new StunResponse(new StunMessage5389 { Attributes = attrs }, remote, local);
	}

	#region BindingTest

	[Test]
	public async Task BindingTest_Success()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateBindingTest();

		StunResponse response = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		_ = session.GotResponse(response);

		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	public async Task BindingTest_Fail()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateBindingTest();

		StunDiscoveryAction? action = session.GotResponse(null);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Fail);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsNull();
		await Assert.That(session.Result.OtherEndPoint).IsNull();
		await Assert.That(session.Result.LocalEndPoint).IsNull();
	}

	[Test]
	public async Task BindingTest_UnsupportedServer()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateBindingTest();

		StunResponse response = new(new StunMessage5389(), ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(response);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.UnsupportedServer);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsNull();
		await Assert.That(session.Result.OtherEndPoint).IsNull();
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	#endregion

	#region MappingBehaviorTest

	[Test]
	public async Task MappingBehaviorTest_BindingFail()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		StunDiscoveryAction? action = session.CreateMappingBehaviorTest();
		await Assert.That(action).IsNotNull();

		action = session.GotResponse(null);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Fail);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsNull();
		await Assert.That(session.Result.OtherEndPoint).IsNull();
		await Assert.That(session.Result.LocalEndPoint).IsNull();
	}

	[Test]
	[MethodDataSource(nameof(UnsupportedOtherAddresses))]
	public async Task MappingBehaviorTest_UnsupportedServer(IPEndPoint? otherAddress)
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateMappingBehaviorTest();

		StunResponse response = CreateBindingResponse(MappedAddress1, otherAddress, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(response);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.UnsupportedServer);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(otherAddress);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	public async Task MappingBehaviorTest_Direct()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateMappingBehaviorTest();

		StunResponse response = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, MappedAddress1);
		StunDiscoveryAction? action = session.GotResponse(response);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Direct);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(MappedAddress1);
	}

	[Test]
	public async Task MappingBehaviorTest_EndpointIndependent()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateMappingBehaviorTest();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test II - same mapped address
		StunResponse r2 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ChangedAddress3, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.EndpointIndependent);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	[Arguments(true, MappingBehavior.AddressDependent)]
	[Arguments(false, MappingBehavior.AddressAndPortDependent)]
	public async Task MappingBehaviorTest_Test3(bool sameMapping, MappingBehavior expected)
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateMappingBehaviorTest();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test II - different mapped address
		StunResponse r2 = CreateBindingResponse(MappedAddress2, ChangedAddress1, ChangedAddress3, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test III compares its mapping with test II
		StunResponse r3 = CreateBindingResponse(sameMapping ? MappedAddress2 : MappedAddress3, ChangedAddress1, ChangedAddress1, LocalAddress1);
		action = session.GotResponse(r3);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(expected);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task MappingBehaviorTest_Test2Fail(bool hasResponse)
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateMappingBehaviorTest();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test II receives no response or a response without a mapping attribute
		StunResponse? r2 = hasResponse ? new(new StunMessage5389(), ChangedAddress3, LocalAddress1) : null;
		action = session.GotResponse(r2);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Fail);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task MappingBehaviorTest_Test3Fail(bool hasResponse)
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateMappingBehaviorTest();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test II - different mapped
		StunResponse r2 = CreateBindingResponse(MappedAddress2, ChangedAddress1, ChangedAddress3, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test III receives no response or a response without a mapping attribute
		StunResponse? r3 = hasResponse ? new(new StunMessage5389(), ChangedAddress1, LocalAddress1) : null;
		action = session.GotResponse(r3);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Fail);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	#endregion

	#region FilteringBehaviorTest

	[Test]
	public async Task FilteringBehaviorTest_BindingFail()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		StunDiscoveryAction? action = session.CreateFilteringBehaviorTest();
		await Assert.That(action).IsNotNull();

		action = session.GotResponse(null);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Fail);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsNull();
		await Assert.That(session.Result.OtherEndPoint).IsNull();
		await Assert.That(session.Result.LocalEndPoint).IsNull();
	}

	[Test]
	[MethodDataSource(nameof(UnsupportedOtherAddresses))]
	public async Task FilteringBehaviorTest_UnsupportedServer(IPEndPoint? otherAddress)
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateFilteringBehaviorTest();

		StunResponse response = CreateBindingResponse(MappedAddress1, otherAddress, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(response);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.UnsupportedServer);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(otherAddress);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	[Arguments(true, FilteringBehavior.EndpointIndependent)]
	[Arguments(false, FilteringBehavior.UnsupportedServer)]
	public async Task FilteringBehaviorTest_Test2(bool responseFromOtherAddress, FilteringBehavior expected)
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateFilteringBehaviorTest();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test II requires a response from OtherAddress
		StunResponse r2 = new(new StunMessage5389(), responseFromOtherAddress ? ChangedAddress1 : ServerAddress, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(expected);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	[MethodDataSource(nameof(FilteringTest3Responses))]
	public async Task FilteringBehaviorTest_Test3(IPEndPoint? respondingServer, FilteringBehavior expected)
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateFilteringBehaviorTest();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test II - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Test III requires a response from the same IP and a different port
		StunResponse? r3 = respondingServer is null ? null : new(new StunMessage5389(), respondingServer, LocalAddress1);
		action = session.GotResponse(r3);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(expected);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	#endregion

	#region Query (Filtering + Mapping combined)

	[Test]
	public async Task Query_BindingFail()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		StunDiscoveryAction? action = session.GotResponse(null);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Fail);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsNull();
		await Assert.That(session.Result.OtherEndPoint).IsNull();
		await Assert.That(session.Result.LocalEndPoint).IsNull();
	}

	[Test]
	public async Task Query_BindingUnsupportedServer()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		// Binding response without XOR-MAPPED-ADDRESS
		StunResponse response = new(new StunMessage5389(), ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(response);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.UnsupportedServer);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsNull();
		await Assert.That(session.Result.OtherEndPoint).IsNull();
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	public async Task Query_UnsupportedServer()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		StunResponse response = CreateBindingResponse(MappedAddress1, null, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(response);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.UnsupportedServer);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsNull();
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	public async Task Query_Direct_FilteringAddressAndPortDependent()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		// Binding: public == local
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, MappedAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(MappedAddress1);

		// Filtering test II - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(MappedAddress1);

		// Filtering test III - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Direct);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.AddressAndPortDependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(MappedAddress1);
	}

	[Test]
	public async Task Query_EndpointIndependent_FilteringAddressAndPortDependent()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test II - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test III - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.AddressAndPortDependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Mapping test II - same mapped
		StunResponse r2 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ChangedAddress3, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.EndpointIndependent);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.AddressAndPortDependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	[Arguments(true, MappingBehavior.AddressAndPortDependent)]
	[Arguments(false, MappingBehavior.Fail)]
	public async Task Query_MappingTest3_FilteringAddressAndPortDependent(bool hasResponse, MappingBehavior expected)
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test II - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test III - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.AddressAndPortDependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Mapping test II - different mapped
		StunResponse r2 = CreateBindingResponse(MappedAddress2, ChangedAddress1, ChangedAddress3, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.AddressAndPortDependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Mapping test III receives a new mapping or no response
		StunResponse? r3 = hasResponse ? CreateBindingResponse(MappedAddress3, ChangedAddress1, ChangedAddress1, LocalAddress1) : null;
		action = session.GotResponse(r3);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(expected);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.AddressAndPortDependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	public async Task Query_AddressDependent_FilteringEndpointIndependent()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test II - response from OtherEndPoint
		StunResponse r2 = new(new StunMessage5389(), ChangedAddress1, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.EndpointIndependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Mapping test II - different mapped
		StunResponse r3 = CreateBindingResponse(MappedAddress2, ChangedAddress1, ChangedAddress3, LocalAddress1);
		action = session.GotResponse(r3);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.EndpointIndependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Mapping test III - same as test II
		StunResponse r4 = CreateBindingResponse(MappedAddress2, ChangedAddress1, ChangedAddress1, LocalAddress1);
		action = session.GotResponse(r4);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.AddressDependent);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.EndpointIndependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	public async Task Query_MappingTest2Fail_FilteringAddressDependent()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test II - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test III - response from same IP different port
		StunResponse r2 = new(new StunMessage5389(), ChangedAddress2, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.AddressDependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Mapping test II - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Fail);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.AddressDependent);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	public async Task Query_FilteringTest2UnsupportedServer_SkipMapping()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test II - response from wrong address
		StunResponse r2 = new(new StunMessage5389(), ServerAddress, LocalAddress1);
		action = session.GotResponse(r2);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.UnsupportedServer);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	[Test]
	public async Task Query_FilteringTest3UnsupportedServer_SkipMapping()
	{
		Stun5389NatBehaviorDiscovery session = new(ServerAddress);
		_ = session.CreateQuery();

		// Binding test
		StunResponse r1 = CreateBindingResponse(MappedAddress1, ChangedAddress1, ServerAddress, LocalAddress1);
		StunDiscoveryAction? action = session.GotResponse(r1);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test II - no response
		action = session.GotResponse(null);
		await Assert.That(action).IsNotNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.Unknown);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);

		// Filtering test III - response from same address (unsupported)
		StunResponse r3 = new(new StunMessage5389(), ServerAddress, LocalAddress1);
		action = session.GotResponse(r3);
		await Assert.That(action).IsNull();
		await Assert.That(session.Result.BindingTestResult).IsEqualTo(BindingTestResult.Success);
		await Assert.That(session.Result.MappingBehavior).IsEqualTo(MappingBehavior.Unknown);
		await Assert.That(session.Result.FilteringBehavior).IsEqualTo(FilteringBehavior.UnsupportedServer);
		await Assert.That(session.Result.PublicEndPoint).IsEqualTo(MappedAddress1);
		await Assert.That(session.Result.OtherEndPoint).IsEqualTo(ChangedAddress1);
		await Assert.That(session.Result.LocalEndPoint).IsEqualTo(LocalAddress1);
	}

	#endregion
}
