using Dns.Net.Clients;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using STUN.Client;
using STUN.Enums;
using STUN.Messages.StunAttributeValues;
using STUN.Proxy;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace UnitTest
{
	[TestClass]
	public class UnitTest
	{
		private static ReadOnlySpan<byte> MagicCookieAndTransactionId => new byte[]
		{
			0x21, 0x12, 0xa4, 0x42,
			0xb7, 0xe7, 0xa7, 0x01,
			0xbc, 0x34, 0xd6, 0x86,
			0xfa, 0x87, 0xdf, 0xae
		};

		private static readonly byte[] XorPort = { 0xa1, 0x47 };
		private static readonly byte[] XorIPv4 = { 0xe1, 0x12, 0xa6, 0x43 };
		private static readonly byte[] XorIPv6 =
		{
				0x01, 0x13, 0xa9, 0xfa,
				0xa5, 0xd3, 0xf1, 0x79,
				0xbc, 0x25, 0xf4, 0xb5,
				0xbe, 0xd2, 0xb9, 0xd9
		};

		private const ushort Port = 32853;
		private readonly IPAddress IPv4 = IPAddress.Parse(@"192.0.2.1");
		private readonly IPAddress IPv6 = IPAddress.Parse(@"2001:db8:1234:5678:11:2233:4455:6677");

		private readonly byte[] _ipv4Response = new byte[] { 0x00, (byte)IpFamily.IPv4 }.Concat(XorPort).Concat(XorIPv4).ToArray();
		private readonly byte[] _ipv6Response = new byte[] { 0x00, (byte)IpFamily.IPv6 }.Concat(XorPort).Concat(XorIPv6).ToArray();

		/// <summary>
		/// https://tools.ietf.org/html/rfc5769.html
		/// </summary>
		[TestMethod]
		public void TestXorMapped()
		{
			var t = new XorMappedAddressStunAttributeValue(MagicCookieAndTransactionId)
			{
				Port = Port,
				Family = IpFamily.IPv4,
				Address = IPv4
			};
			Span<byte> temp = stackalloc byte[ushort.MaxValue];

			var length4 = t.WriteTo(temp);
			Assert.AreNotEqual(0, length4);
			Assert.IsTrue(temp[..length4].SequenceEqual(_ipv4Response));

			t = new XorMappedAddressStunAttributeValue(MagicCookieAndTransactionId);
			Assert.IsTrue(t.TryParse(_ipv4Response));
			Assert.AreEqual(t.Port, Port);
			Assert.AreEqual(t.Family, IpFamily.IPv4);
			Assert.AreEqual(t.Address, IPv4);

			t = new XorMappedAddressStunAttributeValue(MagicCookieAndTransactionId);
			Assert.IsTrue(t.TryParse(_ipv6Response));
			Assert.AreEqual(t.Port, Port);
			Assert.AreEqual(t.Family, IpFamily.IPv6);
			Assert.AreEqual(t.Address, IPv6);

			var length6 = t.WriteTo(temp);
			Assert.AreNotEqual(0, length6);
			Assert.IsTrue(temp[..length6].SequenceEqual(_ipv6Response));
		}

		[TestMethod]
		public async Task BindingTest()
		{
			using var client = new StunClient5389UDP(new DefaultDnsClient(), @"stun.syncthing.net", 3478, new IPEndPoint(IPAddress.Any, 0));
			await client.BindingTestAsync();
			var result = client.Status;

			Assert.AreEqual(result.BindingTestResult, BindingTestResult.Success);
			Assert.IsNotNull(result.LocalEndPoint);
			Assert.IsNotNull(result.PublicEndPoint);
			Assert.IsNotNull(result.OtherEndPoint);
			Assert.AreNotEqual(result.LocalEndPoint!.Address, IPAddress.Any);
			Assert.AreEqual(result.MappingBehavior, MappingBehavior.Unknown);
			Assert.AreEqual(result.FilteringBehavior, FilteringBehavior.Unknown);
		}

		[TestMethod]
		public async Task MappingBehaviorTest()
		{
			using var client = new StunClient5389UDP(new DefaultDnsClient(), @"stun.syncthing.net", 3478, new IPEndPoint(IPAddress.Any, 0));
			await client.MappingBehaviorTestAsync();
			var result = client.Status;

			Assert.AreEqual(result.BindingTestResult, BindingTestResult.Success);
			Assert.IsNotNull(result.LocalEndPoint);
			Assert.IsNotNull(result.PublicEndPoint);
			Assert.IsNotNull(result.OtherEndPoint);
			Assert.AreNotEqual(result.LocalEndPoint!.Address, IPAddress.Any);
			Assert.IsTrue(result.MappingBehavior is
				MappingBehavior.Direct or
				MappingBehavior.EndpointIndependent or
				MappingBehavior.AddressDependent or
				MappingBehavior.AddressAndPortDependent
			);
			Assert.AreEqual(result.FilteringBehavior, FilteringBehavior.Unknown);
		}

		[TestMethod]
		public async Task FilteringBehaviorTest()
		{
			using var client = new StunClient5389UDP(new DefaultDnsClient(), @"stun.syncthing.net", 3478, new IPEndPoint(IPAddress.Any, 0));
			await client.FilteringBehaviorTestAsync();
			var result = client.Status;

			Assert.AreEqual(result.BindingTestResult, BindingTestResult.Success);
			Assert.IsNotNull(result.LocalEndPoint);
			Assert.IsNotNull(result.PublicEndPoint);
			Assert.IsNotNull(result.OtherEndPoint);
			Assert.AreNotEqual(result.LocalEndPoint!.Address, IPAddress.Any);
			Assert.AreEqual(result.MappingBehavior, MappingBehavior.Unknown);
			Assert.IsTrue(result.FilteringBehavior is
				FilteringBehavior.EndpointIndependent or
				FilteringBehavior.AddressDependent or
				FilteringBehavior.AddressAndPortDependent
			);
		}

		[TestMethod]
		public async Task CombiningTest()
		{
			using var client = new StunClient5389UDP(new DefaultDnsClient(), @"stun.syncthing.net", 3478, new IPEndPoint(IPAddress.Any, 0));
			await client.QueryAsync();
			var result = client.Status;

			Assert.AreEqual(result.BindingTestResult, BindingTestResult.Success);
			Assert.IsNotNull(result.LocalEndPoint);
			Assert.IsNotNull(result.PublicEndPoint);
			Assert.IsNotNull(result.OtherEndPoint);
			Assert.AreNotEqual(result.LocalEndPoint!.Address, IPAddress.Any);
			Assert.IsTrue(result.MappingBehavior is
				MappingBehavior.Direct or
				MappingBehavior.EndpointIndependent or
				MappingBehavior.AddressDependent or
				MappingBehavior.AddressAndPortDependent
			);
			Assert.IsTrue(result.FilteringBehavior is
				FilteringBehavior.EndpointIndependent or
				FilteringBehavior.AddressDependent or
				FilteringBehavior.AddressAndPortDependent
			);
		}

		[TestMethod]
		public async Task ProxyTest()
		{
			using var proxy = ProxyFactory.CreateProxy(ProxyType.Socks5, IPEndPoint.Parse(@"0.0.0.0:0"), IPEndPoint.Parse(@"127.0.0.1:10000"));
			using var client = new StunClient5389UDP(new DefaultDnsClient(), @"stun.syncthing.net", 3478, new IPEndPoint(IPAddress.Any, 0), proxy);
			await client.QueryAsync();
			var result = client.Status;

			Assert.AreEqual(result.BindingTestResult, BindingTestResult.Success);
			Assert.IsNotNull(result.LocalEndPoint);
			Assert.IsNotNull(result.PublicEndPoint);
			Assert.IsNotNull(result.OtherEndPoint);
			Assert.AreNotEqual(result.LocalEndPoint!.Address, IPAddress.Any);
			Assert.IsTrue(
				result.MappingBehavior is MappingBehavior.Direct
				or MappingBehavior.EndpointIndependent
				or MappingBehavior.AddressDependent
				or MappingBehavior.AddressAndPortDependent);
			Assert.IsTrue(
				result.FilteringBehavior is FilteringBehavior.EndpointIndependent
				or FilteringBehavior.AddressDependent
				or FilteringBehavior.AddressAndPortDependent);

			Console.WriteLine(result.BindingTestResult);
			Console.WriteLine(result.MappingBehavior);
			Console.WriteLine(result.FilteringBehavior);
			Console.WriteLine(result.OtherEndPoint);
			Console.WriteLine(result.LocalEndPoint);
			Console.WriteLine(result.PublicEndPoint);
		}
	}
}
