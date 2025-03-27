using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using ZeroGdk.Server.Network;

namespace ZeroGdk.Server.HostedServices
{
	internal sealed class TcpNetworkListener : IHostedService
	{
		private readonly TcpNetworkValidator _validator;
		private readonly Socket _listenSocket;
		private readonly SocketAsyncEventArgs _acceptArgs;
		private readonly IPEndPoint _localEndPoint;
		private readonly NetworkOptions _networkOptions;
		private readonly ILogger<TcpNetworkListener> _logger;

		public TcpNetworkListener(TcpNetworkValidator validator,
			IOptions<NetworkOptions> networkOptions,
			ILogger<TcpNetworkListener> logger)
		{
			_validator = validator;
			_networkOptions = networkOptions.Value;

			var addressFamily = (_networkOptions.IpModes & IpModes.Ipv6) == IpModes.Ipv6 ?
				AddressFamily.InterNetworkV6 :
				AddressFamily.InterNetwork;

			_listenSocket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp)
			{
				DualMode = (_networkOptions.IpModes & IpModes.Both) == IpModes.Both
			};

			_acceptArgs = new SocketAsyncEventArgs();
			_acceptArgs.Completed += OnAcceptCompleted;

			var listenAddress = (_networkOptions.IpModes & IpModes.Ipv6) == IpModes.Ipv6 ?
				IPAddress.IPv6Any :
				IPAddress.Any;
			_localEndPoint = new IPEndPoint(listenAddress, _networkOptions.GamePort);
			_logger = logger;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_listenSocket.Bind(_localEndPoint);
			_listenSocket.Listen(_networkOptions.ListenBacklog);

			DoAccept();

			_logger.LogInformation("Listening for tcp on port: {port}", _localEndPoint.Port);
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_logger.LogInformation("Stopping tcp listener..");

			try
			{
				_listenSocket.Close();
				_listenSocket.Dispose();
			}
			catch (Exception)
			{

			}

			_validator.Stop();
			return Task.CompletedTask;
		}

		public void Update()
		{
			_validator.Expire();
		}

		private void DoAccept()
		{
			_acceptArgs.AcceptSocket = null;

			try
			{
				var pending = _listenSocket.AcceptAsync(_acceptArgs);
				if (!pending)
				{
					OnAcceptCompleted(this, _acceptArgs);
				}
			}
			catch (Exception)
			{

			}
		}

		private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs args)
		{
			if (args.SocketError != SocketError.Success)
			{
				DoAccept();
				return;
			}

			if (args.AcceptSocket != null)
			{
				_validator.Add(args.AcceptSocket);
			}

			DoAccept();
		}
	}
}
