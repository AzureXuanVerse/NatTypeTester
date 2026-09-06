using TUnit.Core.Interfaces;

namespace UnitTest;

public sealed class StunServerList : IAsyncInitializer, IAsyncDisposable
{
	private readonly HttpClient _httpClient = new();

	public IReadOnlyList<string> Hosts { get; private set; } = [];

	public async Task InitializeAsync()
	{
		const string url = "https://raw.githubusercontent.com/pradt2/always-online-stun/master/valid_hosts_tcp.txt";
		CancellationToken cancellationToken = TestContext.Current?.ClassContext.AssemblyContext.TestSessionContext.SessionCancellationToken ?? CancellationToken.None;
		string listRaw = await _httpClient.GetStringAsync(url, cancellationToken);
		Hosts = Array.AsReadOnly(listRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
	}

	public ValueTask DisposeAsync()
	{
		_httpClient.Dispose();
		return ValueTask.CompletedTask;
	}
}
