using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Concurrent;
using System.Xml;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RegistrationsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MS002";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId,
            "The string builder passed in the this parameter should be returned",
            "The returned value is not the parameter passed via the this parameter",
            category: "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true, description: "Description.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(CompilationStart);
        }

        private static void CompilationStart(CompilationStartAnalysisContext context)
        {
            if (context.Compilation.GetTypeByMetadataName("System.Text.StringBuilder") is INamedTypeSymbol stringBuilderTypeSymbol)
            {
                context.RegisterSymbolStartAction((SymbolStartAnalysisContext symbolStartContext) =>
                {
                    if (symbolStartContext.Symbol.IsStatic)
                    {
                        symbolStartContext.RegisterCodeBlockStartAction((CodeBlockStartAnalysisContext<SyntaxKind> codeBlockContext) =>
                        {
                            if (codeBlockContext.OwningSymbol is IMethodSymbol { IsExtensionMethod: true, Parameters.Length: >= 1 } methodSymbol
                                && methodSymbol.Parameters[0] is { } thisParameter
                                && SymbolEqualityComparer.Default.Equals(thisParameter.Type, stringBuilderTypeSymbol)
                                && SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, stringBuilderTypeSymbol))
                            {
                                var thisStringBuilderReferences = new ConcurrentDictionary<ISymbol, object>(SymbolEqualityComparer.Default);
                                var stringBuilderReturns = new ConcurrentDictionary<ISymbol, SyntaxNode>(SymbolEqualityComparer.Default);
                                thisStringBuilderReferences.TryAdd(thisParameter, null);

                                codeBlockContext.RegisterSyntaxNodeAction((SyntaxNodeAnalysisContext assignmentContext) =>
                                {
                                    var assignment = (AssignmentExpressionSyntax)assignmentContext.Node;
                                    if (assignmentContext.SemanticModel.GetSymbolInfo(assignment.Right).Symbol is { } assignedSymbol
                                        && SymbolEqualityComparer.Default.Equals(assignedSymbol, thisParameter)
                                        && assignmentContext.SemanticModel.GetSymbolInfo(assignment.Left).Symbol is { } assignedToSymbol)
                                    {
                                        thisStringBuilderReferences.TryAdd(assignedToSymbol, null);
                                    }
                                }, SyntaxKind.SimpleAssignmentExpression);

                                codeBlockContext.RegisterSyntaxNodeAction((SyntaxNodeAnalysisContext returnContext) =>
                                {
                                    var returnStatement = (ReturnStatementSyntax)returnContext.Node;
                                    if (returnContext.SemanticModel.GetSymbolInfo(returnStatement.Expression).Symbol is { } returnSymbol)
                                    {
                                        stringBuilderReturns.TryAdd(returnSymbol, returnStatement);
                                    }
                                }, SyntaxKind.ReturnStatement);

                                codeBlockContext.RegisterCodeBlockEndAction((CodeBlockAnalysisContext endContext) =>
                                {
                                    foreach (var stringBuilderReturn in stringBuilderReturns.Where(kvp => !thisStringBuilderReferences.ContainsKey(kvp.Key)))
                                    {
                                        endContext.ReportDiagnostic(Diagnostic.Create(Rule, stringBuilderReturn.Value.GetLocation()));
                                    }
                                });
                            }
                        });
                    }
                }, SymbolKind.NamedType);
            }
        }
    }
}

