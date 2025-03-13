using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using ZeroGdk.Core.Network;
using ZeroGdk.Server.Grpc;

namespace ZeroGdk.Server.Network
{
	internal sealed class NetworkKeyStore(IOptions<NetworkOptions> networkOptions) : IExpire
	{
		private readonly struct KeyEntry(string key, OpenConnectionRequest request, DateTime timeUtc)
		{
			public readonly string Key = key;
			public readonly OpenConnectionRequest Request = request;
			public readonly DateTime TtlUtc = timeUtc;
		}

		private readonly NetworkOptions _networkOptions = networkOptions.Value;
		private readonly ConcurrentDictionary<string, OpenConnectionRequest> _requests = [];
		private readonly ConcurrentQueue<KeyEntry> _entries = [];

		public string Add(OpenConnectionRequest request)
		{
			var keyBytes = new byte[NetworkKey.Length];
			RandomNumberGenerator.Fill(keyBytes);
			var key = Convert.ToBase64String(keyBytes);
			var entry = new KeyEntry(key, request, DateTime.UtcNow.Add(_networkOptions.NetworkKeyTimeout));
			_requests[key] = request;
			_entries.Enqueue(entry);
			return key;
		}

		public void Expire()
		{
			var utcNow = DateTime.UtcNow;
			while (_entries.TryPeek(out var entry))
			{
				if (utcNow <= entry.TtlUtc)
				{
					return;
				}

				_requests.TryRemove(entry.Key, out _);
				_entries.TryDequeue(out _);
			}
		}

		public bool TryValidate(string key, [MaybeNullWhen(false)] out OpenConnectionRequest request)
		{
			return _requests.TryRemove(key, out request);
		}
	}
}
