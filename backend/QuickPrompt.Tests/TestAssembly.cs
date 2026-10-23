using Xunit;

// Transport fixtures reserve and then register ports in the process-wide Windows
// HTTP namespace. Isolate test cases; concurrency exercised inside a test remains.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
