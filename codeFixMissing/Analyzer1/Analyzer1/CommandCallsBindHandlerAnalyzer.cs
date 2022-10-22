using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Amusoft.Toolkit.System.CommandLine.Generator.Utility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Amusoft.Toolkit.System.CommandLine.Generator.Analyzers;

#pragma warning disable CS1591

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CommandCallsBindHandlerAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor ConstructorWithBindHandlerRule = new DiagnosticDescriptor(
		"ATSCG001",
		"BindHandler call missing", 
		"BindHandler must be called in constructor of {0}",
		"Amusoft.Toolkit.System.CommandLine.Generator Usage", DiagnosticSeverity.Error, isEnabledByDefault: true,
		description: "BindHandler sets up the handler of a command with its arguments. Failing to do so would create a command with a handler that will not be executed.",
		WellKnownDiagnosticTags.NotConfigurable);

	internal static readonly DiagnosticDescriptor GeneratorAttributeMissingRule = new DiagnosticDescriptor(
		"ATSCG002",
		"Generator attribute missing", 
		"No generator attribute is specified for {0}",
		"Amusoft.Toolkit.System.CommandLine.Generator Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true,
		description: $"Neither GenerateExecuteHandlerAttribute nor GenerateCommandHandlerAttribute is specified.");

	internal static readonly DiagnosticDescriptor RootCommandMustBeInheritedRule = new DiagnosticDescriptor(
		"ATSCG003",
		"There is no command that inherits RootCommand",
		"There is no command that inherits RootCommand",
		"Amusoft.Toolkit.System.CommandLine.Generator Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true,
		description: $"In order for the IRootCommandProvider to work there must be a command that implements RootCommand.");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
	{
		get {
			return	ImmutableArray.Create(
				RootCommandMustBeInheritedRule, 
				ConstructorWithBindHandlerRule, 
				GeneratorAttributeMissingRule
			);
		}
	}

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeClassNode, SyntaxKind.ClassDeclaration);
		context.RegisterCompilationAction(AnalyzeCompilation);
		context.EnableConcurrentExecution();
	}

	private void AnalyzeCompilation(CompilationAnalysisContext context)
	{
		var commandVisitor = new NamedTypeVisitor(context.CancellationToken);
		commandVisitor.Visit(context.Compilation.Assembly);

		var allNamedTypes = commandVisitor.ExportedTypes.ToImmutableArray();
		var commandSymbols = allNamedTypes
			.Select(d => (
				command: d, 
				baseMetaName : d.BaseType?.MetadataName, 
				isBaseTypeCommand: d.BaseType is { MetadataName: "Command" or "RootCommand" })
			)
			.Where(d => d.isBaseTypeCommand)
			.ToImmutableArray();

		if (commandSymbols.Length > 0)
		{
			if (!commandSymbols.Any(d => d.isBaseTypeCommand && d.baseMetaName == "RootCommand"))
			{
				context.ReportDiagnostic(Diagnostic.Create(RootCommandMustBeInheritedRule, null));
			}
		}
	}

	private void AnalyzeClassNode(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is ClassDeclarationSyntax classDeclaration)
		{
			ApplyDiagnostics(classDeclaration, context);
		}
	}

	private static void RaiseBindHandlerMissing(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
	{
		context.ReportDiagnostic(Diagnostic.Create(ConstructorWithBindHandlerRule, classDeclaration.Identifier.GetLocation(), classDeclaration.Identifier.Text));
	}

	private static void RaiseAttributeMissing(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
	{
		context.ReportDiagnostic(Diagnostic.Create(GeneratorAttributeMissingRule, classDeclaration.Identifier.GetLocation(), classDeclaration.Identifier.Text));
	}

	private static void ApplyDiagnostics(ClassDeclarationSyntax classDeclaration, SyntaxNodeAnalysisContext context)
	{
		if (!TryGetCommandType(classDeclaration, out var commandTypeSyntax))
			return;

		if (!TryGeneratorAttribute(classDeclaration, out var attributeSyntax))
		{
			if (!IsCandidateForAttributeDiagnostic(context, classDeclaration))
				return;

			RaiseAttributeMissing(context, classDeclaration);
			return;
		}

		if (!TryGetConstructors(classDeclaration, out var constructors))
		{
			RaiseBindHandlerMissing(context, classDeclaration);
			return;
		}
	}

	private static bool IsCandidateForAttributeDiagnostic(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration)
	{
		if (SyntaxIsRootCommand(classDeclaration))
			return false;

		TryGetClassAttributeIdentifiers(classDeclaration, out var classAttributes);
		if (classAttributes.HasValue && classAttributes.Value.Any(identifier => identifier is
		    {
			    Identifier.Text:
			    "HasParentCommand"
			    or "HasParentCommandAttribute"
			    or "HasChildCommand"
			    or "HasChildCommandAttribute"
		    }))
			return false;

		return true;
	}

	private static bool SyntaxIsRootCommand(ClassDeclarationSyntax classDeclaration)
	{
		return classDeclaration.BaseList is {Types.Count: > 0} && classDeclaration.BaseList.Types[0].Type is IdentifierNameSyntax {Identifier.Text: "RootCommand"};
	}

	private static bool TryGetConstructors(ClassDeclarationSyntax classDeclaration, out ImmutableArray<(ConstructorDeclarationSyntax constructor, InvocationExpressionSyntax methodCall)>? constructorsSyntax)
	{
		var bindCalls = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>()
			.SelectMany(constructor => 
				constructor
					.DescendantNodes()
					.OfType<InvocationExpressionSyntax>()
					.Where(IsBindHandlerInvocation)
					.Select(methodCall => (constructor, methodCall)))
			.ToImmutableArray();

		if (bindCalls.Length > 0)
		{
			constructorsSyntax = bindCalls;
			return true;
		}

		static bool IsBindHandlerInvocation(InvocationExpressionSyntax invocationExpressionSyntax)
		{
			return invocationExpressionSyntax is {ArgumentList.Arguments.Count: 0} && invocationExpressionSyntax.Expression is SimpleNameSyntax {Identifier.Text: "BindHandler"};
		}

		constructorsSyntax = default;
		return false;
	}

	private static bool TryGetClassAttributeIdentifiers(ClassDeclarationSyntax classDeclaration, out ImmutableArray<IdentifierNameSyntax>? attributeIdentifiers)
	{
		if (classDeclaration is {AttributeLists: { } attributes} && attributes is {Count: > 0})
		{
			attributeIdentifiers = attributes.SelectMany(attributeList => attributeList.Attributes.Select(attribute => attribute.Name))
				.OfType<IdentifierNameSyntax>()
				.ToImmutableArray();

			return attributeIdentifiers.Value.Length > 0;
		}

		attributeIdentifiers = default;
		return false;
	}

	private static bool TryGeneratorAttribute(ClassDeclarationSyntax classDeclaration, out IdentifierNameSyntax? identifierNameSyntax)
	{
		if (classDeclaration is {AttributeLists: { } attributes} && attributes is {Count: > 0})
		{
			foreach (var attributeSyntax in attributes.SelectMany(d => d.Attributes))
			{
				if (attributeSyntax.Name is IdentifierNameSyntax {Identifier.Text: 
					    "GenerateExecuteHandlerAttribute" 
					    or "GenerateExecuteHandler" 
					    or "GenerateCommandHandlerAttribute" 
					    or "GenerateCommandHandler"} attr)
				{
					identifierNameSyntax = attr;
					return true;
				}
			}
		}

		identifierNameSyntax = default;
		return false;
	}

	private static bool TryGetCommandType(ClassDeclarationSyntax classDeclaration, out IdentifierNameSyntax? identifierNameSyntax)
	{
		if (classDeclaration is {BaseList.Types: { } types} && types is {Count: > 0})
		{
			if (types[0].Type is IdentifierNameSyntax {Identifier.Text: "Command" or "RootCommand"} identifier)
			{
				identifierNameSyntax = identifier;
				return true;
			}
		}

		identifierNameSyntax = default;
		return false;
	}
}