using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroGdk.Server.Utils
{
	internal struct TimedBucket
	{
		private readonly int _maxCount;
		private readonly long _tickFrameSize;
		private long _bucket;
		private int _count;

		public TimedBucket(int maxCount, long tickFrameSize)
		{
			_maxCount = maxCount;
			_tickFrameSize = tickFrameSize;
			_bucket = Stopwatch.GetTimestamp() / _tickFrameSize;
			_count = 0;
		}

		public async ValueTask WaitAsync(CancellationToken cancellationToken)
		{
			long time;
			while ((time = Stopwatch.GetTimestamp()) / _tickFrameSize == _bucket &&
				_count >= _maxCount &&
				!cancellationToken.IsCancellationRequested)
			{
				var nextBucketTime = (_bucket + 1) * _tickFrameSize;
				var timeRemaining = nextBucketTime - time;
				var toWait = (int)(timeRemaining / _tickFrameSize);
				await Task.Delay(toWait, cancellationToken).ConfigureAwait(false);
			}

			var currentBucket = Stopwatch.GetTimestamp() / _tickFrameSize;
			if (currentBucket != _bucket)
			{
				_count = 1;
				_bucket = currentBucket;
			}
			else
			{
				_count++;
			}
		}
	}
}
