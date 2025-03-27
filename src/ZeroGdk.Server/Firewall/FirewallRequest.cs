using System.Net;

namespace ZeroGdk.Server.Firewall
{
	internal sealed class FirewallRequest
	{
		private FirewallRequest(FirewallAction action, IEnumerable<IPAddress>? ipAddresses, IPAddress? ipAddress, int timeoutSeconds)
		{
			Action = action;
			IpAddresses = ipAddresses;
			IpAddress = ipAddress;
			TimeoutSeconds = timeoutSeconds;
		}

		public FirewallAction Action { get; }
		public IEnumerable<IPAddress>? IpAddresses { get; }   // for Sync
		public IPAddress? IpAddress { get; }                  // for Temporary
		public int TimeoutSeconds { get; }

		public static FirewallRequest AddTemporary(IPAddress ipAddress, int timeoutSeconds)
		{
			return new FirewallRequest(FirewallAction.AddTemporary, null, ipAddress, timeoutSeconds);
		}

		public static FirewallRequest SyncPersistent(IEnumerable<IPAddress> ipAddresses)
		{
			return new FirewallRequest(FirewallAction.SyncPersistent, ipAddresses, null, 0);
		}
	}
}
