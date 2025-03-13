using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Net;
using ZeroGdk.Server.Firewall;
using ZeroGdk.Server.Grpc;
using ZeroGdk.Server.Network;
using ZeroGdk.Server.Queues;
using ZeroGdk.Server.Routing;

namespace ZeroGdk.Server.Services
{
	[Authorize]
	internal class WorkerConnectionService(NetworkKeyStore networkKeyStore,
		FirewallQueue firewallQueue,
		IOptions<NetworkOptions> networkOptions,
		RouteResolver<Connection> routeResolver) : WorkerConnection.WorkerConnectionBase
	{
		private readonly NetworkKeyStore _networkKeyStore = networkKeyStore;
		private readonly FirewallQueue _firewallQueue = firewallQueue;
		private readonly NetworkOptions _networkOptions = networkOptions.Value;
		private readonly RouteResolver<Connection> _routeResolver = routeResolver;

		public override async Task<OpenConnectionResponse> Open(OpenConnectionRequest request, ServerCallContext context)
		{
			// validate client IP
			if (!IPAddress.TryParse(request.ClientIp, out var clientIpAddress))
			{
				return new OpenConnectionResponse
				{
					Result = ConnectionResult.InvalidClientIpAddress
				};
			}

			// validate factory route
			if (!_routeResolver.TryResolve(request.Route, out _))
			{
				return new OpenConnectionResponse
				{
					Result = ConnectionResult.RouteNotFound
				};
			}

			// create network key, open a temporary firewall hole for the client
			var key = _networkKeyStore.Add(request);
			var timeoutSeconds = (int)Math.Ceiling(_networkOptions.NetworkKeyTimeout.TotalSeconds);
			await _firewallQueue.EnqueueAsync(FirewallRequest.AddTemporary(clientIpAddress, timeoutSeconds)).ConfigureAwait(false);

			return new OpenConnectionResponse
			{
				Result = ConnectionResult.Success,
				Key = key
			};
		}
	}
}
