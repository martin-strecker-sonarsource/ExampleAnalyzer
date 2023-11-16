using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Analyzer.Test
{
    [TestClass]
    public class CsprojAnalyzerTest
    {
        [TestMethod]
        public async Task ReportOnCsProj()
        {
            var v = new CSharpAnalyzerTest<CsprojAnalyzer, MSTestVerifier>();
            v.SolutionTransforms.Add((s, p) => s.AddAdditionalDocument(DocumentId.CreateNewId(p), "Test.csproj", SourceText.From(
                """
                <Project Sdk="Microsoft.NET.Sdk">
                	<PropertyGroup>
                		<TargetFramework>net8.0</TargetFramework>
                		<ImplicitUsings>enable</ImplicitUsings>
                		<Nullable>enable</Nullable>
                	</PropertyGroup>

                	<ItemGroup>
                		<PackageReference Include="Analyzer" Version="1.17.0">
                			<PrivateAssets>all</PrivateAssets>
                			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                		</PackageReference>
                	</ItemGroup>
                </Project>
                """)));
            v.TestCode = "";
            v.ExpectedDiagnostics.Add(new DiagnosticResult(CsprojAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithSpan("Test.csproj", 9, 4, 9, 20)
                .WithArguments("Analyzer", "1.17.0"));
            await v.RunAsync();
        }
    }
}
