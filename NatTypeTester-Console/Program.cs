using Dns.Net.Clients;
using STUN.Client;
using System;
using System.Net;
using System.Threading.Tasks;

namespace NatTypeTester
{
	internal static class Program
	{
		/// <summary>
		/// stun.qq.com 3478 0.0.0.0:0
		/// </summary>
		private static async Task Main(string[] args)
		{
			var server = @"stun.syncthing.net";
			ushort port = 3478;
			IPEndPoint? local = null;
			if (args.Length > 0 && (Uri.CheckHostName(args[0]) == UriHostNameType.Dns || IPAddress.TryParse(args[0], out _)))
			{
				server = args[0];
			}
			if (args.Length > 1)
			{
				ushort.TryParse(args[1], out port);
			}
			if (args.Length > 2)
			{
				local = IPEndPoint.Parse(args[2]);
			}

			using var client = new StunClient5389UDP(new DefaultDnsClient(), server, port, local);
			await client.QueryAsync();
			var res = client.Status;

			Console.WriteLine($@"Other address is {res.OtherEndPoint}");
			Console.WriteLine($@"Binding test: {res.BindingTestResult}");
			Console.WriteLine($@"Local address: {res.LocalEndPoint}");
			Console.WriteLine($@"Mapped address: {res.PublicEndPoint}");
			Console.WriteLine($@"Nat mapping behavior: {res.MappingBehavior}");
			Console.WriteLine($@"Nat filtering behavior: {res.FilteringBehavior}");
		}
	}
}
