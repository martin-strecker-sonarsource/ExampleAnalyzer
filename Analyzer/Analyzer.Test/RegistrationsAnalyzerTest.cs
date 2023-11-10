using Microsoft;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Verifyer = Microsoft.CodeAnalysis.CSharp.Testing.MSTest.AnalyzerVerifier<Analyzer.RegistrationsAnalyzer>;
namespace Analyzer.Test
{
    [TestClass]
    public class RegistrationsAnalyzerTest
    {
        [TestMethod]
        public async Task DoesNotFail()
        {
            await Verifyer.VerifyAnalyzerAsync("");
        }

        [TestMethod] // https://github.com/dotnet/roslyn-sdk/blob/main/src/Microsoft.CodeAnalysis.Testing/README.md
        public async Task StringBuilderExtension_OtherSymbolReturned()
        {
            await Verifyer.VerifyAnalyzerAsync("""
            using System.Text;

            public static class StringBuilderExtensions
            {
                public static StringBuilder MyExtension(this StringBuilder sb)
                {
                    var result = new StringBuilder();
                    [|return result;|]
                }
            }
            """);
        }

        [TestMethod]
        public async Task StringBuilderExtension_AssignedSymbolReturned()
        {
            await Verifyer.VerifyAnalyzerAsync("""
            using System.Text;

            public static class StringBuilderExtensions
            {
                public static StringBuilder MyExtension(this StringBuilder sb)
                {
                    StringBuilder result;
                    result = sb;
                    return result;
                }
            }
            """);
        }
    }
}
