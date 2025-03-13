using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark
{
	[MemoryDiagnoser]
	public class SortTest
	{
		[Params(10, 100, 1000, 10000)]
		public int Count;

		private List<Entity> _entityIds = new();
		private HashSet<Entity> _entityIdsHash = new();
		private List<Entity> _lastEntities = new();
		private List<Entity> _lastEntities2 = new();
		private List<Entity> _lastEntitiesSorted = new();
		private HashSet<Entity> _lastEntitiesHash = new();
		private HashSet<Entity> _lastEntitiesHash2 = new();
		private List<Entity> _newEntities = new(1000);
		private List<Entity> _removedEntities = new(1000);

		[IterationSetup]
		public void Setup()
		{
			var random = new Random(1234567);
			_entityIds.Clear();
			_entityIdsHash.Clear();
			_lastEntities.Clear();
			_lastEntities2.Clear();
			_lastEntitiesSorted.Clear();
			_lastEntitiesHash.Clear();
			_lastEntitiesHash2.Clear();
			_newEntities.Clear();
			_removedEntities.Clear();

			for (int i = 0; i < Count; i++)
			{
				_entityIds.Add(new Entity(random.Next(0, 1000), -1, 1));

				var last = new Entity(random.Next(0, 1000), -1, 1);
				_lastEntities.Add(last);
				_lastEntities2.Add(last);
				_lastEntitiesSorted.Add(last);
				_lastEntitiesHash.Add(last);
				_lastEntitiesHash2.Add(last);
			}
		}

		/*
		[Benchmark]
		public void Radix()
		{
			_lastEntities2.Clear();
			RadixSort(CollectionsMarshal.AsSpan(_entityIds), (uint)_entityIds.Count);

			int i = 0, j = 0;
			while (i < _lastEntitiesSorted.Count && j < _entityIds.Count)
			{
				uint a = _lastEntitiesSorted[i];
				uint b = _entityIds[j];
				if (a < b)
				{
					// previous[i] is missing in the current list (removed)
					_removedEntities.Add(a);
					while (i < _lastEntitiesSorted.Count && _lastEntitiesSorted[i] == a) i++;
				}
				else if (a > b)
				{
					// current[j] is not in the previous list (new)
					_newEntities.Add(b);
					while (j < _entityIds.Count && _entityIds[j] == b) j++;
				}
				else
				{
					// Both lists have this element; move on.
					while (i < _lastEntitiesSorted.Count && _lastEntitiesSorted[i] == a) i++;
					while (j < _entityIds.Count && _entityIds[j] == b) j++;
				}
			}

			// Any remaining elements in current are new.
			while (j < _entityIds.Count)
			{
				_newEntities.Add(_entityIds[j]);
				j++;
			}

			// Any remaining elements in previous are removed.
			while (i < _lastEntitiesSorted.Count)
			{
				_removedEntities.Add(_lastEntitiesSorted[i]);
				i++;
			}
		}
		*/

		[Benchmark]
		public void Dictionary()
		{
			_lastEntitiesHash2.Clear();

			foreach (var entityId in _entityIds)
			{
				if (_entityIdsHash.Add(entityId) &&
					!_lastEntitiesHash.Contains(entityId))
				{
					_newEntities.Add(entityId);
				}
			}

			foreach (var entityId in _lastEntitiesHash)
			{
				if (!_entityIdsHash.Contains(entityId))
				{
					_removedEntities.Add(entityId);
				}
			}
		}

		static unsafe void RadixSort(Span<uint> aSpan, uint count)
		{
			uint[] mIndex = ArrayPool<uint>.Shared.Rent(4 * 256);    // count / index matrix
			Array.Clear(mIndex);
			uint[] bArray = ArrayPool<uint>.Shared.Rent((int)count);           // allocate temp array

			fixed (uint* aPtr = aSpan)
			fixed (uint* bPtr = bArray)
			{
				var a = aPtr;
				var b = bPtr;
				uint i, j, m, n;
				uint u;
				for (i = 0; i < count; i++)
				{         // generate histograms
					u = a[i];
					for (j = 0; j < 4; j++)
					{
						mIndex[j * 256 + (u & 0xff)]++;
						u >>= 8;
					}
				}
				for (j = 0; j < 4; j++)
				{             // convert to indices
					m = 0;
					for (i = 0; i < 256; i++)
					{
						n = mIndex[j * 256 + i];
						mIndex[j * 256 + i] = m;
						m += n;
					}
				}
				for (j = 0; j < 4; j++)
				{             // radix sort
					for (i = 0; i < count; i++)
					{     //  sort by current lsb
						u = a[i];
						m = (u >> ((int)(j << 3))) & 0xff;
						b[mIndex[j * 256 + m]++] = u;
					}
					uint* t = a;                  //  swap references
					a = b;
					b = t;
				}
			}
			
			ArrayPool<uint>.Shared.Return(mIndex);
			ArrayPool<uint>.Shared.Return(bArray);
		}
	}
}
