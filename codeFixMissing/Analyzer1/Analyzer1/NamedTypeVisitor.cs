using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Amusoft.Toolkit.System.CommandLine.Generator.Utility;

internal class NamedTypeVisitor : SymbolVisitor
{
	private readonly CancellationToken _cancellationToken;
	public readonly HashSet<INamedTypeSymbol> ExportedTypes;

	public NamedTypeVisitor(CancellationToken cancellation)
	{
		_cancellationToken = cancellation;
		ExportedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
	}

	public override void VisitAssembly(IAssemblySymbol symbol)
	{
		_cancellationToken.ThrowIfCancellationRequested();
		symbol.GlobalNamespace.Accept(this);
	}

	public override void VisitNamespace(INamespaceSymbol symbol)
	{
		foreach (var namespaceOrType in symbol.GetMembers())
		{
			_cancellationToken.ThrowIfCancellationRequested();
			namespaceOrType.Accept(this);
		}
	}
	public override void VisitNamedType(INamedTypeSymbol type)
	{
		_cancellationToken.ThrowIfCancellationRequested();

		if (!ExportedTypes.Add(type))
			return;

		var nestedTypes = type.GetTypeMembers();
		if (nestedTypes.IsDefaultOrEmpty)
			return;

		foreach (var nestedType in nestedTypes)
		{
			_cancellationToken.ThrowIfCancellationRequested();
			nestedType.Accept(this);
		}
	}
}