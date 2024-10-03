using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GuardCop.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConstructorGuardFixer)), Shared]
public class ConstructorGuardFixer : CodeFixProvider
{
    private const string Title = "Add Contract.Requires() for parameter";

    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ConstructorGuardAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => ConstructorGuardFixAllProvider.Instance;

    public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c => AddGuardForParameter(context.Document, diagnostic, c),
                    Title),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    public static async Task<Document> AddGuardForParameter(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);

        var constructor = FindConstructor(root, diagnostic);

        var guardStatements = GuardSyntax.GetGuardStatements(constructor);

        var remainingStatements = constructor.Body.Statements
            .Except(guardStatements)
            .ToList();

        AddMissingGuardStatement(constructor, guardStatements, diagnostic);

        guardStatements = SortGuardStatements(constructor, guardStatements);

        AddSingleEmptyLineAfterGuards(guardStatements, remainingStatements);

        root = RewriteConstructorBody(root, constructor, guardStatements, remainingStatements);

        return document.WithSyntaxRoot(root);
    }

    private static ConstructorDeclarationSyntax FindConstructor(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.AncestorsAndSelf()
            .OfType<ConstructorDeclarationSyntax>()
            .First();

    private static void AddMissingGuardStatement(ConstructorDeclarationSyntax constructor, List<ExpressionStatementSyntax> guardStatements, Diagnostic diagnostic)
    {
        var parameter = constructor.ParameterList.Parameters
            .Single(x => x.SpanStart == diagnostic.Location.SourceSpan.Start);

        guardStatements.Add(GuardSyntax.CreateGuardStatement(parameter));
    }

    private static List<ExpressionStatementSyntax> SortGuardStatements(ConstructorDeclarationSyntax constructor, List<ExpressionStatementSyntax> guardStatements)
    {
        var parameters = constructor.ParameterList.Parameters
            .Select(x => x.Identifier.Text)
            .ToList();

        return guardStatements
            .OrderBy(x => parameters.IndexOf(GuardSyntax.GetParameterNameFromGuard(x)))
            .ToList();
    }

    private static void AddSingleEmptyLineAfterGuards(List<ExpressionStatementSyntax> guardStatements, List<StatementSyntax> remainingStatements)
    {
        if (remainingStatements.Count > 0)
        {
            var trivia = SyntaxFactory.TriviaList(remainingStatements[0]
                .GetLeadingTrivia()
                .Where(x => !x.IsKind(SyntaxKind.EndOfLineTrivia)));

            remainingStatements[0] = remainingStatements[0].WithLeadingTrivia(trivia);
        }

        guardStatements[guardStatements.Count - 1] = guardStatements.Last()
            .WithTrailingTrivia(SyntaxFactory.TriviaList(
                SyntaxFactory.CarriageReturnLineFeed,
                SyntaxFactory.CarriageReturnLineFeed
            ));
    }

    private static SyntaxNode RewriteConstructorBody(SyntaxNode root, ConstructorDeclarationSyntax constructor, List<ExpressionStatementSyntax> guardStatements, List<StatementSyntax> remainingStatements)
    {
        var newBodyStatements = SyntaxFactory.List(guardStatements.Concat(remainingStatements));

        var newConstructor = constructor.WithBody(constructor.Body.WithStatements(newBodyStatements));

        return root.ReplaceNode(constructor, newConstructor);
    }
}
