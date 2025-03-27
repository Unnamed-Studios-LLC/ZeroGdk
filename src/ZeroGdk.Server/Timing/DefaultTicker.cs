using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ZeroGdk.Server.Timing
{
	internal sealed class DefaultTicker(IOptions<TimingOptions> timingOptions) : ITicker
	{
		private const int PrecisionDelay = 2;

		private readonly TimingOptions _timingOptions = timingOptions.Value;
		private readonly ManualResetEvent _waitEvent = new(false);
		private long _startTime;

		public int Delta { get; private set; }
		public long Total { get; private set; }

		public void Start()
		{
			_startTime = Stopwatch.GetTimestamp();
		}

		public void Stop()
		{
			_waitEvent.Set();
		}

		public void WaitNext()
		{
			var interval = _timingOptions.UpdateIntervalMs;
			var elapsed = GetTimestampMs() - Total;

			switch (_timingOptions.Strategy)
			{
				case TimingStrategy.Realtime:
					{
						var delay = interval - elapsed;
						int batches = 1;

						// wait if ahead of schedule
						if (delay > 0)
						{
							if (delay > PrecisionDelay)
								_waitEvent.WaitOne((int)delay - PrecisionDelay);

							while ((GetTimestampMs() - Total) < interval) { /* spin wait */ }
						}
						else
						{
							// behind schedule — catch up with batched updates
							batches = Math.Max(1, Math.Min(_timingOptions.MaxDeltaBatches, (int)(-delay / interval)));
						}

						Delta = interval * batches;
						Total += interval * batches;
						break;
					}

				case TimingStrategy.FixedTick:
				default:
					{
						// delay until interval has passed, even if we’re behind
						var remaining = interval - elapsed;
						if (remaining > 0)
						{
							if (remaining > PrecisionDelay)
								_waitEvent.WaitOne((int)remaining - PrecisionDelay);

							while ((GetTimestampMs() - Total) < interval) { /* spin wait */ }
						}

						Delta = interval;
						Total = GetTimestampMs();
						break;
					}
			}
		}

		private long GetTimestampMs()
		{
			return (Stopwatch.GetTimestamp() - _startTime) / TimeSpan.TicksPerMillisecond;
		}
	}
}
