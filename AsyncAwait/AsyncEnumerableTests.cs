using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace AsyncAwait;

public class AsyncEnumerableTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Stopwatch watch;
    
    public AsyncEnumerableTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        watch = new Stopwatch();
    }

    [Fact]
    public async Task AsyncStream_Check()
    {
        watch.Start();
        await foreach (var str in GetStringsAsync()) //Asynchronous GetNextEnumerator -> however after yield the thread is released!
        {
            _testOutputHelper.WriteLine(str);   
            await Task.Delay(3000);
        }
        _testOutputHelper.WriteLine("The end");
        watch.Stop();
    }

    [Fact]
    public async Task AsyncStream_Linq()
    {
        watch.Start();
        await foreach (var integer in GetIntsAsync().Where(
                           x => x % 2 == 0)) //Huge note! Even though the results are filtered with Linq expression - all side effects are still taking a place! Ex. _testOutputHelper logs
        {
            _testOutputHelper.WriteLine($"Return a value number {integer}");   
            await Task.Delay(3000);
        }
        _testOutputHelper.WriteLine("The end");
        watch.Stop();
    }

    [Fact]
    public async Task AsyncStream_LinqAwait()
    {
        watch.Start();
        await foreach (var integer in GetIntsAsync().WhereAwait( //Await here means that the delegate WILL be awaited 
                           async x=>
                           {
                               await Task.Delay(10); 
                               return x % 2 == 0;
                           })) //Huge note! Even though the results are filtered with Linq expression - all side effects are still taking a place! Ex. _testOutputHelper logs
        {
            _testOutputHelper.WriteLine($"Return a value number {integer}");   
            await Task.Delay(3000);
        }
        _testOutputHelper.WriteLine("The end");
        watch.Stop();
    }

    [Fact]
    public async Task AsyncStream_Cancellations()
    {
        using var cts = new CancellationTokenSource(3500);
        var ct = cts.Token;
        watch.Start();
        await foreach (var str in GetStringsAsync().WithCancellation(ct))
        {
            _testOutputHelper.WriteLine(str);   
            await Task.Delay(3000, ct).ContinueWith((t) => {});
        }
        _testOutputHelper.WriteLine("The end");
        watch.Stop();
    }
    
    private async IAsyncEnumerable<int> GetIntsAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(i * 1000);
            yield return i; //After return yield no further work is done...
            _testOutputHelper.WriteLine($"Now we resume. {watch.ElapsedMilliseconds/1000}..."); //The task resumes here
        }
    }
    
    private async IAsyncEnumerable<int> GetIntsAsync([EnumeratorCancellation] CancellationToken token)
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(i * 1000, token);
            yield return i; //After return yield no further work is done...
            _testOutputHelper.WriteLine($"Now we resume. {watch.ElapsedMilliseconds/1000}..."); //The task resumes here
        }
    }
    
    private async IAsyncEnumerable<string> GetStringsAsync([EnumeratorCancellation] CancellationToken token = default)
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(i * 1000, token).ContinueWith(t => {});
            yield return $"Return a value number: {i.ToString()} - {watch.ElapsedMilliseconds/1000}..."; //After return yield no further work is done...
            _testOutputHelper.WriteLine($"Now we resume. {watch.ElapsedMilliseconds/1000}..."); //The task resumes here
        }
    }
}