using System.CommandLine;
using System.CommandLine.Invocation;
using Amusoft.Toolkit.System.CommandLine.Attributes;

namespace codeFixMissing;

class Program
{
	static void Main(string[] args)
	{

		// See https://aka.ms/new-console-template for more information
		Console.WriteLine("Hello, World!");
	}
}

[GenerateExecuteHandler]
public partial class TestCommand : Command
{
	public TestCommand() : base("test")
	{
	}

	public Task ExecuteAsync(InvocationContext context)
	{
		throw new NotImplementedException();
	}
}
