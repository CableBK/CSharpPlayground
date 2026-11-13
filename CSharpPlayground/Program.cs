using CSharpPlayground;
using CSharpPlayground.Advanced.Thread;

namespace CSharpPlayground
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var thread = new Advanced.Thread.Thread();
            thread.RunParallelCounter();
        }
    }
}
