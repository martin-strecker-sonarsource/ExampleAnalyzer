using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CsprojAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MS003";

        private static readonly DiagnosticDescriptor Rule = new(DiagnosticId,
            "A Nuget package is referenced",
            "Nuget package '{0}' with version {1} is referenced",
            category: "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Just a PoC.",
            customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterAdditionalFileAction(c =>
            {
                if (c.AdditionalFile is { } csproj
                    && csproj.Path?.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) is true
                    && csproj.GetText(c.CancellationToken) is { } text)
                {
                    XDocument xml;
                    using (var buffer = new MemoryStream())
                    using (var writer = new StreamWriter(buffer))
                    {
                        text.Write(writer);
                        writer.Flush();
                        buffer.Seek(0, SeekOrigin.Begin);
                        xml = XDocument.Load(buffer, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
                    }
                    var packageReferences = from ig in xml.Root.Nodes().OfType<XElement>()
                                            where ig.Name == "ItemGroup"
                                            from pr in ig.Nodes().OfType<XElement>()
                                            where pr.Name == "PackageReference"
                                            select pr;
                    foreach (var packageReference in packageReferences)
                    {
                        if (packageReference is IXmlLineInfo { } lineInfo
                            && lineInfo.HasLineInfo()
                            && packageReference.Attribute("Include") is { Value: { } package }
                            && packageReference.Attribute("Version") is { Value: { } version })
                        {
                            int tagLength = packageReference.Name.LocalName.Length;
                            var linePositionStart = new LinePosition(lineInfo.LineNumber - 1, lineInfo.LinePosition - 1);
                            var linePositionEnd = new LinePosition(linePositionStart.Line, linePositionStart.Character + tagLength);
                            var linePositionSpan = new LinePositionSpan(linePositionStart, linePositionEnd);
                            var span = text.Lines.GetTextSpan(linePositionSpan);
                            var location = Location.Create(csproj.Path, span, linePositionSpan);
                            c.ReportDiagnostic(Diagnostic.Create(Rule, location, package, version));
                        }
                    }
                }
            });
        }
    }
}
