using Benchmark;
using BenchmarkDotNet.Running;

#if RELEASE
BenchmarkRunner.Run<SortTest>();
#else
var test = new SortTest();
test.Count = 1000;
test.Setup();
test.Radix();
test.Setup();
test.Dictionary();
#endif