using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GuardCop.Analyzers;

public class GuardSyntax
{
    public static bool IsParameterGuarded(ConstructorDeclarationSyntax constructor, ParameterSyntax parameter) =>
        constructor.Body.Statements
            .OfType<ExpressionStatementSyntax>()
            .Select(x => x.Expression)
            .OfType<InvocationExpressionSyntax>()
            .Any(x => IsGuardForParameter(x, parameter));

    private static bool IsGuardForParameter(InvocationExpressionSyntax invocation, ParameterSyntax parameter) =>
        IsContractRequiresMethod(invocation) && GetParameterNameFromGuard(invocation) == parameter.Identifier.Text;

    private static bool IsContractRequiresMethod(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Expression is not IdentifierNameSyntax className) return false;

        return className.Identifier.Text == "Contract" && memberAccess.Name.Identifier.Text.StartsWith("Requires");
    }

    public static List<ExpressionStatementSyntax> GetGuardStatements(ConstructorDeclarationSyntax constructor) =>
        constructor.Body.Statements
            .OfType<ExpressionStatementSyntax>()
            .Where(x => x.Expression is InvocationExpressionSyntax invocation && IsContractRequiresMethod(invocation))
            .ToList();

    public static string GetParameterNameFromGuard(ExpressionStatementSyntax statement) =>
        GetParameterNameFromGuard((InvocationExpressionSyntax)statement.Expression);

    private static string GetParameterNameFromGuard(InvocationExpressionSyntax invocation)
    {
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault();

        if (argument?.Expression is not InvocationExpressionSyntax nameofInvocation) return null;
        if (nameofInvocation.Expression is not IdentifierNameSyntax nameofIdentifier) return null;
        if (nameofIdentifier.Identifier.Text != "nameof") return null;
        if (nameofInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not IdentifierNameSyntax paramName) return null;

        return paramName.Identifier.Text;
    }

    public static ExpressionStatementSyntax CreateGuardStatement(ParameterSyntax parameter)
    {
        var requiresApi = parameter.Type is PredefinedTypeSyntax t && t.Keyword.IsKind(SyntaxKind.StringKeyword)
            ? "RequiresNotNullNotEmpty"
            : "RequiresNotNull";

        var guardInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Contract"),
                    SyntaxFactory.IdentifierName(requiresApi)))
            .WithArgumentList(SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.IdentifierName("nameof"))
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Identifier.Text)))))))));

        return SyntaxFactory.ExpressionStatement(guardInvocation);
    }
}
