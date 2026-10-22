using Xunit;

// Each HTTP-backed test stands up its own WebApplicationFactory, and the machinery that resolves
// the app's entry point and captures the built IHost keeps its state in process-wide statics. Two
// factories starting at the same moment clobber each other, and one of them fails with
// "The entry point exited without ever building an IHost".
//
// xUnit runs different test classes in parallel by default, so that race is reachable as soon as
// more than one class talks to the API. Serializing the assembly removes it; the integration suite
// is a few dozen tests, so the wall-clock cost is small next to having a suite that fails at random.
// It also keeps the LocalDB-backed tests from competing for the same LocalDB instance.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
