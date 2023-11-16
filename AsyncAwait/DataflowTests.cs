using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Xunit.Abstractions;

namespace AsyncAwait;

public class DataflowTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Stopwatch watch;
    
    public DataflowTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        watch = new Stopwatch();
    }
    
    [Fact]
    public async Task Dataflow_Init()
    {
        var multiplyBlock = new TransformBlock<int, int>(item => item * 2);
        var subtractBlock = new TransformBlock<int, int>(item => item - 2);
        var outputtingBlock = new ActionBlock<int>(item => _testOutputHelper.WriteLine(item.ToString()));

        var options = new DataflowLinkOptions
        {
            PropagateCompletion = true
        };

        multiplyBlock.LinkTo(subtractBlock, options);
        subtractBlock.LinkTo(outputtingBlock, options);

        multiplyBlock.Post(9);

        multiplyBlock.Complete();
        await outputtingBlock.Completion;
    }
}