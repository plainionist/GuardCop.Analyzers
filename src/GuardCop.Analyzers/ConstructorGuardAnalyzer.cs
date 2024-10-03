using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GuardCop.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ConstructorGuardAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "CC0001";
    internal const string MessageFormat = "Parameter '{0}' is not guarded using the Contract API";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Constructor parameters must have guards",
        MessageFormat,
        "CleanCode",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        "Ensure that all constructor parameters are guarded using the Contract API");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;

        var notGuardedParameters = constructor.ParameterList.Parameters
            .Where(x => !GuardSyntax.IsParameterGuarded(constructor, x));

        foreach (var parameter in notGuardedParameters)
        {
            var diagnostic = Diagnostic.Create(Rule, parameter.GetLocation(), parameter.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
