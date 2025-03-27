using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ZeroGdk.Server.Firewall;
using ZeroGdk.Server.Queues;

namespace ZeroGdk.Server.HostedServices
{
	internal sealed class LinuxFirewallProcessor(FirewallQueue queue,
		ILogger<LinuxFirewallProcessor> logger,
		IOptions<NetworkOptions> networkOptions) : BackgroundService
	{
		private readonly FirewallQueue _queue = queue;
		private readonly ILogger<LinuxFirewallProcessor> _logger = logger;
		private readonly NetworkOptions _networkOptions = networkOptions.Value;

		private readonly HashSet<IPAddress> _ipAddressCache = [];

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await InitializeFirewallRulesAsync(stoppingToken);
			await InitializeFirewallSetsAsync(stoppingToken);

			await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
			{
				try
				{
					await ProcessRequestAsync(request, stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to process firewall request: {type}", request.GetType());
				}
			}
		}

		private async Task InitializeFirewallRulesAsync(CancellationToken ct)
		{
			var port = _networkOptions.GamePort;

			// Allow all traffic on loopback interface (localhost communications)
			await RunCmdAsync("sudo", "iptables -A INPUT -i lo -j ACCEPT", ct);
			await RunCmdAsync("sudo", "ip6tables -A INPUT -i lo -j ACCEPT", ct);

			// Allow established and related incoming traffic (responses to outbound requests)
			await RunCmdAsync("sudo", "iptables -A INPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT", ct);
			await RunCmdAsync("sudo", "ip6tables -A INPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT", ct);

			// Your application-specific rules for IPv4
			await RunCmdAsync("sudo", $"iptables -A INPUT -p tcp --dport {port} -m set --match-set allowed_ips_v4 src -j ACCEPT", ct);
			await RunCmdAsync("sudo", $"iptables -A INPUT -p tcp --dport {port} -m set --match-set temp_ips_v4 src -j ACCEPT", ct);
			await RunCmdAsync("sudo", $"iptables -A INPUT -p tcp --dport {port} -j DROP", ct);

			// Your application-specific rules for IPv6
			await RunCmdAsync("sudo", $"ip6tables -A INPUT -p tcp --dport {port} -m set --match-set allowed_ips_v6 src -j ACCEPT", ct);
			await RunCmdAsync("sudo", $"ip6tables -A INPUT -p tcp --dport {port} -m set --match-set temp_ips_v6 src -j ACCEPT", ct);
			await RunCmdAsync("sudo", $"ip6tables -A INPUT -p tcp --dport {port} -j DROP", ct);

			_logger.LogInformation("Firewall iptables rules initialized successfully.");
		}

		private async Task InitializeFirewallSetsAsync(CancellationToken ct)
		{
			// IPv4 Persistent Set
			await RunCmdAsync("sudo", "ipset create allowed_ips_v4 hash:ip -exist", ct);

			// IPv4 Temporary Set (5-minute default timeout)
			await RunCmdAsync("sudo", "ipset create temp_ips_v4 hash:ip timeout 300 -exist", ct);

			// IPv6 Persistent Set
			await RunCmdAsync("sudo", "ipset create allowed_ips_v6 hash:ip family inet6 -exist", ct);

			// IPv6 Temporary Set (5-minute default timeout)
			await RunCmdAsync("sudo", "ipset create temp_ips_v6 hash:ip family inet6 timeout 300 -exist", ct);

			_logger.LogDebug("Firewall ipsets initialized successfully.");
		}

		private static IPAddress Normalize(IPAddress address)
		{
			if (address.IsIPv4MappedToIPv6)
			{
				return address.MapToIPv4();
			}
			return address;
		}

		private static async Task RunCmdAsync(string cmd, string args, CancellationToken cancellationToken)
		{
			var psi = new ProcessStartInfo(cmd, args)
			{
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using var process = Process.Start(psi);
			if (process == null)
			{
				throw new InvalidOperationException($"Unable to start process: {cmd}");
			}

			await process.WaitForExitAsync(cancellationToken);

			if (process.ExitCode != 0)
			{
				string error = await process.StandardError.ReadToEndAsync(cancellationToken);
				throw new InvalidOperationException($"Command '{cmd} {args}' failed: {error}");
			}
		}

		private async Task ProcessRequestAsync(FirewallRequest req, CancellationToken cancellationToken)
		{
			switch (req.Action)
			{
				case FirewallAction.SyncPersistent:
					if (req.IpAddresses != null)
					{
						await SyncPersistentIpsAsync(req.IpAddresses.Select(Normalize), cancellationToken);
					}
					break;

				case FirewallAction.AddTemporary:
					if (req.IpAddress != null)
					{
						await AddTemporaryIpAsync(Normalize(req.IpAddress), req.TimeoutSeconds, cancellationToken);
					}
					break;
			}
		}

		private async Task SyncPersistentIpsAsync(IEnumerable<IPAddress> ipAddresses, CancellationToken ct)
		{
			var toAdd = ipAddresses.Except(_ipAddressCache);
			var toRemove = _ipAddressCache.Except(ipAddresses);

			foreach (var ip in toAdd)
			{
				if (_ipAddressCache.Contains(ip))
				{
					continue;
				}

				string setName = ip.AddressFamily == AddressFamily.InterNetwork ? "allowed_ips_v4" : "allowed_ips_v6";
				await RunCmdAsync("sudo", $"ipset add {setName} {ip}", ct);
				_ipAddressCache.Add(ip);
			}

			foreach (var ip in toRemove)
			{
				if (!_ipAddressCache.Contains(ip))
				{
					continue;
				}

				string setName = ip.AddressFamily == AddressFamily.InterNetwork ? "allowed_ips_v4" : "allowed_ips_v6";
				await RunCmdAsync("sudo", $"ipset del {setName} {ip}", ct);
				_ipAddressCache.Remove(ip);
			}
		}

		private Task AddTemporaryIpAsync(IPAddress ip, int timeout, CancellationToken ct)
		{
			string setName = ip.AddressFamily == AddressFamily.InterNetwork ? "temp_ips_v4" : "temp_ips_v6";
			return RunCmdAsync("sudo", $"ipset add {setName} {ip} timeout {timeout}", ct);
		}
	}
}
