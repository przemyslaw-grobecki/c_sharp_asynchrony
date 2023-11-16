using System.Diagnostics;
using FluentAssertions;
using Xunit.Abstractions;

namespace AsyncAwait;

public class ParallelTests
{
    private readonly ITestOutputHelper testOutputHelper;
    private readonly Stopwatch watch;
    
    public ParallelTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
        watch = new Stopwatch();
    }

    private class Matrix
    {
        public int Id { get; private set; }

        public Matrix(int id)
        {
            Id = id;
        }
        public void Invert(Action<int> action)
        {
            action.Invoke(Id);
        }

        public bool IsInvertible()
        {
            return Id % 2 != 0;
        }
    }

    [Fact]
    public void InvertMatricesInParallel()
    {
        var matrices = new List<Matrix>
        {
            new(1),
            new(2),
            new(3)
        };

        Parallel.ForEach(matrices, (matrix, state) =>
        {
            if (!matrix.IsInvertible())
            {
                state.Stop(); //Note that after stopping, no further "iterations" will be made
            }
            else
            {
                matrix.Invert(id => testOutputHelper.WriteLine($"Hey its {id}"));   
            }
        });
    }
    
    [Fact]
    public async Task InvertMatricesInParallel_WithCancellation()
    {
        var cts = new CancellationTokenSource(2000);
        var ct = cts.Token;
        var matrices = new List<Matrix>
        {
            new(1),
            new(5),
            new(7)
        };

        var act = async () => await Parallel.ForEachAsync(matrices,
            new ParallelOptions
            {
                CancellationToken = ct
            },
            async (matrix, token) =>
            {
                await Task.Delay(matrix.Id * 1000, token);
                matrix.Invert(id => testOutputHelper.WriteLine($"Hey its {id}"));
            }
        );

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ParallelAggregation()
    {
        var integers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var semaphore = new SemaphoreSlim(1, 1);
        var sum = 0;
        Parallel.ForEach(source: integers,
            localInit: () => 0,
            body: (i, state, localValue) => localValue + i,
            localFinally: localValue =>
            {
                semaphore.Wait();
                try
                {
                    sum += localValue;
                }
                finally
                {
                    semaphore.Release();
                }
            }
        );
        sum.Should().Be(integers.Aggregate(0, (total, next) => total + next));
    }

    [Fact]
    public void DynamicParallelism()
    {
         var root = new Node(testOutputHelper)
         {
             Message = "root"
         };
         var left = new Node(testOutputHelper)
         {
             Message = "firstLeft"
         };
         var right = new Node(testOutputHelper)
         {
             Message = "firstRight"
         };
         var leftsLeft = new Node(testOutputHelper)
         {
             Message = "secondLeft"
         };
         root.Left = left;
         root.Right = right;
         left.Left = leftsLeft;
         var task = Task.Factory.StartNew(() => ParallelTraverse(root), CancellationToken.None,
             TaskCreationOptions.None, TaskScheduler.Default
         );
         
         task.Wait();
         return;

         void ParallelTraverse(Node node)
         {
             Task[] tasks = { 
                 Task.Factory.StartNew(
                    node.DoExpensiveOperation,
                    CancellationToken.None,
                    TaskCreationOptions.AttachedToParent,
                    TaskScheduler.Default) 
             };
             if (node.Left != null)
             {
                 tasks.Append(Task.Factory.StartNew(
                     () => ParallelTraverse(node.Left), CancellationToken.None, TaskCreationOptions.AttachedToParent,
                     TaskScheduler.Default));
             }
             if (node.Right != null)
             {
                 tasks.Append(Task.Factory.StartNew(
                     () => ParallelTraverse(node.Right), CancellationToken.None, TaskCreationOptions.AttachedToParent,
                     TaskScheduler.Default));
             }

             Task.WaitAll(tasks);
         }
    }

    private class Node
    {
        private readonly Random random = new(Guid.NewGuid().GetHashCode());
        private readonly ITestOutputHelper output;
        public Node(ITestOutputHelper output)
        {
            this.output = output;
        }
        public required string Message { get; init; }
        public Node? Left { get; set; } = null;
        public Node? Right { get; set; } = null;

        public void DoExpensiveOperation()
        {
            Thread.Sleep(1000 * random.Next(1,3));
            output.WriteLine(Message);
        }
    }
}