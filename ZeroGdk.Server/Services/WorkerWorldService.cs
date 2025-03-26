using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using ZeroGdk.Server.Grpc;
using ZeroGdk.Server.Queues;

namespace ZeroGdk.Server.Services
{
	internal sealed class WorkerWorldService(WorldQueue worldQueue) : WorkerWorld.WorkerWorldBase
	{
		private readonly WorldQueue _worldQueue = worldQueue;

		public override async Task<CreateWorldResponse> Create(CreateWorldRequest request, ServerCallContext context)
		{
			return await _worldQueue.CreateAsync(request);
		}

		public override async Task<DestroyWorldResponse> Destroy(DestroyWorldRequest request, ServerCallContext context)
		{
			return await _worldQueue.DestroyAsync(request);
		}
	}
}
