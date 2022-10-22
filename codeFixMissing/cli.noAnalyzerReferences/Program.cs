using System.CommandLine;

namespace cli.noAnalyzerReferences;
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}

public class GenerateExecuteHandlerAttribute : Attribute{ }

[GenerateExecuteHandler]
public class TestCommand : Command
{
    public TestCommand() : base("")
    {
    }

    public void BindHandler() { }
}
