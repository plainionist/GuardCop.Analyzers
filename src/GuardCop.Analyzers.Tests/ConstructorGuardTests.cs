using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace GuardCop.Analyzers.Tests;

public class ConstructorGuardTests
{
    [Test]
    public async Task NoParameters_NoDiagnostic()
    {
        var testCode = @"
public class Person
{
    public Person()
    {
    }
}";

        await VerifyAsync(testCode, []);
    }

    [Test]
    public async Task AllParametersGuarded_NoDiagnostic()
    {
        var testCode = @"
public class Person
{
    public Person(string firstName, string lastName)
    {
        Contract.RequiresNotNull(nameof(firstName));
        Contract.RequiresNotNull(nameof(lastName));
    }
}";

        await VerifyAsync(testCode, []);
    }

    [Test]
    public async Task SomeParametersGuarded_TriggerDiagnosticForUnguarded()
    {
        var testCode = @"
public class Person
{
    public Person(string firstName, string lastName)
    {
        Contract.RequiresNotNull(nameof(firstName));
    }
}";

        var expectedDiagnostic = new DiagnosticResult(ConstructorGuardAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithMessage(string.Format(ConstructorGuardAnalyzer.MessageFormat, "lastName"))
            .WithSpan(4, 37, 4, 52);

        await VerifyAsync(testCode, [expectedDiagnostic]);
    }

    [Test]
    public async Task NoParametersGuarded_TriggerDiagnosticForAll()
    {
        var testCode = @"
public class Person
{
    public Person(string firstName, string lastName)
    {
    }
}";

        var expectedDiagnostic1 = new DiagnosticResult(ConstructorGuardAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithMessage(string.Format(ConstructorGuardAnalyzer.MessageFormat, "firstName"))
            .WithSpan(4, 19, 4, 35);

        var expectedDiagnostic2 = new DiagnosticResult(ConstructorGuardAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithMessage(string.Format(ConstructorGuardAnalyzer.MessageFormat, "lastName"))
            .WithSpan(4, 37, 4, 52);

        await VerifyAsync(testCode, [expectedDiagnostic1, expectedDiagnostic2]);
    }

    [Test]
    public async Task CodeFixForSingleParameter()
    {
        var testCode = @"
public class Person
{
    public Person(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public string FirstName { get; }
    public string LastName { get; }
}";

        var fixedCode = @"
public class Person
{
    public Person(string firstName, string lastName)
    {
        Contract.RequiresNotNullNotEmpty(nameof(firstName));
        Contract.RequiresNotNullNotEmpty(nameof(lastName));

        FirstName = firstName;
        LastName = lastName;
    }

    public string FirstName { get; }
    public string LastName { get; }
}";

        var expectedDiagnostic1 = new DiagnosticResult(ConstructorGuardAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithMessage(string.Format(ConstructorGuardAnalyzer.MessageFormat, "firstName"))
            .WithSpan(4, 19, 4, 35);

        var expectedDiagnostic2 = new DiagnosticResult(ConstructorGuardAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
            .WithMessage(string.Format(ConstructorGuardAnalyzer.MessageFormat, "lastName"))
            .WithSpan(4, 37, 4, 52);

        await VerifyAsync(testCode, [expectedDiagnostic1, expectedDiagnostic2], fixedCode);
    }

    private static async Task VerifyAsync(string source, DiagnosticResult[] expectedDiagnostics, string fixedSource = null)
    {
        var test = new CSharpCodeFixTest<ConstructorGuardAnalyzer, ConstructorGuardFixer, DefaultVerifier>
        {
            // FixAllProvider only supports document scope
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipFixAllInProjectCheck | CodeFixTestBehaviors.SkipFixAllInSolutionCheck,
        };

        test.TestState.Sources.Add(source);

        const string contractClass = @"
    public static class Contract
    {
        public static void RequiresNotNull(string paramName) {}
        public static void RequiresNotNullNotEmpty(string paramName) {}
    }";
        test.TestState.Sources.Add(("Contract.cs", contractClass));

        if (fixedSource != null)
        {
            test.FixedState.Sources.Add(fixedSource);
            test.FixedState.Sources.Add(("Contract.cs", contractClass));
        }

        test.ExpectedDiagnostics.AddRange(expectedDiagnostics);

        await test.RunAsync(CancellationToken.None);
    }
}
