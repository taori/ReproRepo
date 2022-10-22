using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using NotImplementedException = System.NotImplementedException;

namespace Amusoft.Toolkit.System.CommandLine.Generator.Codefixes;

#pragma warning disable CS1591

[ExportCodeFixProvider(LanguageNames.CSharp, Name = "BindHandlerCodeFixProvider"), Shared]
public class BindHandlerCodeFixProvider : CodeFixProvider
{
	public override FixAllProvider GetFixAllProvider()
	{
		return WellKnownFixAllProviders.BatchFixer;
	}

	public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("ATSCG001");

	public override Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		foreach (var diagnostic in context.Diagnostics)
		{
			context.RegisterCodeFix(CodeAction.Create("Inject BindHandler call", token => FixedATSCG001(context, diagnostic, token), diagnostic.Id), diagnostic);
		}

		return Task.CompletedTask;
	}

	private async Task<Document> FixedATSCG001(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
	{
		if (!context.Document.TryGetSyntaxRoot(out var rootNode))
			return context.Document;

		if (!TryGetConstructor(rootNode, diagnostic, token, out var constructor))
			return context.Document;
		
		var editor = await DocumentEditor.CreateAsync(context.Document, token);

		ApplyCodeFix001(editor, constructor, token);

		return editor.GetChangedDocument();
	}

	private static void ApplyCodeFix001(DocumentEditor editor, ConstructorDeclarationSyntax constructor, CancellationToken token)
	{
		if (constructor.Body is not { } block)
			return;

		var statement = SyntaxFactory.ParseStatement("BindHandler();").WithAdditionalAnnotations(SyntaxAnnotation.ElasticAnnotation, Formatter.Annotation);

		if (block.Statements.FirstOrDefault() is { } firstStatement)
		{
			var newStatement = statement
				.WithTrailingTrivia(firstStatement.GetTrailingTrivia())
				.WithLeadingTrivia(firstStatement.GetLeadingTrivia());

			var replaced = block.WithStatements(block.Statements.Add(newStatement));
			var replacedConstructor = constructor.WithBody(replaced).WithAdditionalAnnotations(SyntaxAnnotation.ElasticAnnotation);
			editor.ReplaceNode(constructor, replacedConstructor);
		}
		else
		{
			var replaced = block.WithStatements(new SyntaxList<StatementSyntax>(statement));
			var replacedConstructor = constructor.WithBody(replaced);
			editor.ReplaceNode(constructor, replacedConstructor);
		}
	}

	private bool TryGetConstructor(SyntaxNode? syntaxTree, Diagnostic diagnostic, CancellationToken token, out ConstructorDeclarationSyntax? constructor)
	{
		constructor = default;

		if (syntaxTree == null)
			return false;

		var node = syntaxTree.FindNode(diagnostic.Location.SourceSpan);
		var classSyntax = FindParentOfType<ClassDeclarationSyntax>(node);
		if (classSyntax is null)
			return false;

		constructor = classSyntax.DescendantNodes().OfType<ConstructorDeclarationSyntax>().First();
		return true;
	}

	private T? FindParentOfType<T>(SyntaxNode node)
	{
		if (node is T { } casted)
			return casted;

		if (node.Parent is { } parent)
		{
			return FindParentOfType<T>(parent);
		}
		else
		{
			return default;
		}
	}
}