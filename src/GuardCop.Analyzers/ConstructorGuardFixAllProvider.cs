using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace GuardCop.Analyzers;

public class ConstructorGuardFixAllProvider : FixAllProvider
{
    private ConstructorGuardFixAllProvider() { }

    public static readonly ConstructorGuardFixAllProvider Instance = new();

    public override IEnumerable<FixAllScope> GetSupportedFixAllScopes() => [FixAllScope.Document];

    public override Task<CodeAction> GetFixAsync(FixAllContext fixAllContext) =>
        fixAllContext.Scope switch
        {
            FixAllScope.Document => FixDocumentAsync(fixAllContext),
            _ => Task.FromResult<CodeAction>(null),
        };

    private async Task<CodeAction> FixDocumentAsync(FixAllContext context)
    {
        var document = context.Document;

        foreach (var diagnostic in await context.GetDocumentDiagnosticsAsync(context.Document))
        {
            document = await ConstructorGuardFixer.AddGuardForParameter(document, diagnostic, context.CancellationToken);
        }

        return CodeAction.Create("Fix all in document", c => Task.FromResult(document));
    }
}
