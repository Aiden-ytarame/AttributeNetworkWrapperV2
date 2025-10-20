using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AttributeNetworkWrapperV2.Generator;

[Generator]
public class RpcGenerator : IIncrementalGenerator
{
    public static readonly SymbolDisplayFormat FullNameQualification = new (
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType, 
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
    
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var allExtensions = context.CompilationProvider.Select((x, _) =>
        {
            List<IMethodSymbol?> writers = new();

            foreach (var namedTypeSymbol in x.GlobalNamespace.GetTypeMembers())
            {
                if (namedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                {
                     continue;
                }

                foreach (var methodSymbol in namedTypeSymbol.GetMembers().OfType<IMethodSymbol>())
                {
                    if (methodSymbol.IsExtensionMethod && methodSymbol.DeclaredAccessibility == Accessibility.Public)
                    {
                        writers.Add(methodSymbol);
                    }
                }
            }

            foreach (var namespaceMember in x.GlobalNamespace.GetNamespaceMembers())
            {
                foreach (var namedTypeSymbol in namespaceMember.GetTypeMembers())
                {
                    if (namedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                    {
                        continue;
                    }

                    foreach (var methodSymbol in namedTypeSymbol.GetMembers().OfType<IMethodSymbol>())
                    {
                        if (methodSymbol.IsExtensionMethod && methodSymbol.DeclaredAccessibility == Accessibility.Public)
                        {
                            writers.Add(methodSymbol);
                        }
                    }
                }
            }
            return writers.ToImmutableArray();
        });

        var writers = allExtensions.Select((x, _) =>
        {
            List<ExtensionSyntax> writers = new();
            foreach (var methodSymbol in x)
            {
                if (methodSymbol is not null &&
                    methodSymbol.Parameters.Length == 2 && 
                    methodSymbol.ReturnsVoid &&
                    methodSymbol.Parameters[0].Type.ToDisplayString(FullNameQualification) == "AttributeNetworkWrapperV2.NetworkWriter")
                {
                    writers.Add(new ExtensionSyntax(methodSymbol.ToDisplayString(FullNameQualification), methodSymbol.Parameters[1].Type));
                }
            }
            
            return writers.ToImmutableArray();
        });
        
        var readers = allExtensions.Select((x, _) =>
        {
            List<ExtensionSyntax> readers = new();
           
            foreach (var methodSymbol in x)
            {
                if (methodSymbol is not null &&
                    methodSymbol.Parameters.Length == 1 && 
                    !methodSymbol.ReturnsVoid &&
                    methodSymbol.Parameters[0].Type.ToDisplayString(FullNameQualification) == "AttributeNetworkWrapperV2.NetworkReader")
                {
                    readers.Add(new ExtensionSyntax(methodSymbol.ToDisplayString(FullNameQualification), methodSymbol.ReturnType));
                }
            }
            
            return readers.ToImmutableArray();
        });
        
        var serverRpcs = context.SyntaxProvider.ForAttributeWithMetadataName("AttributeNetworkWrapperV2.ServerRpcAttribute",
            predicate: static (node, _) => true,
            transform: static (syntaxContext, _) =>
            {
                var methodDeclarationSyntax = syntaxContext.TargetNode;
                return RpcSyntax.Create(syntaxContext.SemanticModel.GetDeclaredSymbol(methodDeclarationSyntax)!);
            }).Where(static x => x is not null);
        
        var serverRpcWriterReader = serverRpcs.Combine(writers.Combine(readers));
        context.RegisterSourceOutput(serverRpcWriterReader, static (productionContext, syntax) => GenerateServerRpc(productionContext, syntax.Left!.Value, syntax.Right.Left!, syntax.Right.Right!));
    }
    
    static void GenerateServerRpc(SourceProductionContext context, RpcSyntax mtd, ImmutableArray<ExtensionSyntax> writers, ImmutableArray<ExtensionSyntax> readers)
    {
        StringBuilder source = new StringBuilder();
        
        context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                "SG0001",
                "fuck",
                $"Returned {writers.Length} writers, {readers.Length} readers",
                "yeet",
                DiagnosticSeverity.Info,
                true), Location.None));

        
        StartGeneratingClass(source, mtd.ContainingType);
        GenerateFunctionDeclaration(context, source, mtd, "CallRpc_");
        GenerateWriter(context, source, mtd, writers);
            
        source.AppendLine($"         NetworkManager.Instance.SendToServer(writer.GetData(), (SendType){mtd.SendType});");
        source.AppendLine("        }\n");
        
        GenerateDeserializeFunction(context, source, mtd, readers);
        
        source.AppendLine("   }\n}");
        context.AddSource($"{mtd.FullName}.g.cs", source.ToString());
        
    }

    static void StartGeneratingClass(StringBuilder source, INamedTypeSymbol type)
    {
        source.AppendLine("using System; \nusing AttributeNetworkWrapperV2;");
        
        if (type.ContainingNamespace is not null)
        {
            source.AppendLine($"namespace {type.ContainingNamespace.ToDisplayString()} {{ ");
        }
        
        source.Append("    ");
        
        switch (type.DeclaredAccessibility)
        {
            case Accessibility.Private:
                source.Append("private ");
                break;
            case Accessibility.Internal:
                source.Append("internal ");
                break;
            case Accessibility.Public:
                source.Append("public ");
                break;
        }

        if (type.IsStatic)
            source.Append("static ");
        if (type.IsAbstract)
            source.Append("abstract ");
        if (type.IsSealed)
            source.Append("sealed ");
        if (type.IsRecord)
            source.Append("record ");
        
        source.Append($"partial class {type.Name}");

        if (type.BaseType is not null)
            source.Append($" : {type.BaseType.ToDisplayString()} \n    {{\n");
        else
            source.Append("\n    {\n");

    }

    static void GenerateFunctionDeclaration(SourceProductionContext context, StringBuilder source, RpcSyntax mtd, string prefix)
    {
        source.Append("        ");
        switch (mtd.Accessibility)
        {
            case Accessibility.Private:
                source.Append("private ");
                break;
            case Accessibility.Protected:
                source.Append("protected ");
                break;
            case Accessibility.Internal:
                source.Append("internal ");
                break;
            case Accessibility.Public:
                source.Append("public ");
                break;
        }

        source.Append($"void {prefix}{mtd.Name} (");
        
        for (int i = 0; i <  mtd.Parameters.Length; i++)
        {
            IParameterSymbol parameter = mtd.Parameters[i];
            if (parameter.RefKind != RefKind.None)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SG0001",
                        "bad ref",
                        $"method {mtd.Name} cannot have {parameter.RefKind} parameter",
                        "error",
                        DiagnosticSeverity.Error,
                        true), Location.None));
                return;
            }
            
            source.Append($"{parameter.Type.ToDisplayString(FullNameQualification)} {parameter.Name}");

            if (parameter.HasExplicitDefaultValue)
            {
                if (parameter.ExplicitDefaultValue is not null)
                {
                    source.Append($" = {parameter.ExplicitDefaultValue}");
                }
                else
                {
                    source.Append($" = new()");
                }

                if (parameter.ExplicitDefaultValue is float)
                {
                    source.Append("f");
                }
            }
            
            if (i != mtd.Parameters.Length - 1)
            {
                source.Append(", ");
            }
            
        }

        source.AppendLine(")\n        {");
    }

    static void GenerateWriter(SourceProductionContext context, StringBuilder source, RpcSyntax mtd, ImmutableArray<ExtensionSyntax> writers)
    {
        source.AppendLine("         if (NetworkManager.Instance == null) \n            return;");
        source.AppendLine("         using NetworkWriter writer = new NetworkWriter();");
        source.AppendLine($"         writer.Write({mtd.Hash});");
        
        bool found = false;
        foreach (var parameterSymbol in mtd.Parameters)
        {
            found= false;
            
            foreach (var methodSymbol in writers)
            {
                if (parameterSymbol.Type.Equals(methodSymbol.ExtensionType, SymbolEqualityComparer.Default))
                {
                    source.AppendLine($"         {methodSymbol.FullName}(writer, {parameterSymbol.Name});");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SG0001",
                        "no writer",
                        $"method {mtd.Name} contains parameter of type {parameterSymbol.Type.Name}, which has no writer. Consider making an extension method.",
                        "error",
                        DiagnosticSeverity.Error,
                        true), Location.None));
                return;
            }
        }
    }

    static void GenerateDeserializeFunction(SourceProductionContext context, StringBuilder source, RpcSyntax mtd, ImmutableArray<ExtensionSyntax> readers)
    {
        source.Append("        ");
        switch (mtd.Accessibility)
        {
            case Accessibility.Private:
                source.Append("private ");
                break;
            case Accessibility.Protected:
                source.Append("protected ");
                break;
            case Accessibility.Internal:
                source.Append("internal ");
                break;
            case Accessibility.Public:
                source.Append("public ");
                break;
        }

        source.AppendLine($"void Deserialize_{mtd.Name}_{mtd.Hash} (ClientNetworkConnection senderConn, NetworkReader reader)");

        source.AppendLine("        {");
        source.Append($"            {mtd.Name}(");
        
        bool found = false;
        for (int i = 0; i <  mtd.Parameters.Length; i++)
        {
            
          
            IParameterSymbol parameter = mtd.Parameters[i];
            
            if (parameter.ToDisplayString(FullNameQualification) ==
                "AttributeNetworkWrapperV2.ClientNetworkConnection")
            {
                source.Append("senderConn");
                
                if (i != mtd.Parameters.Length - 1)
                {
                    source.Append(", ");
                }
                continue;
            }

            found= false;
            
            foreach (var methodSymbol in readers)
            {
                if (parameter.Type.Equals(methodSymbol.ExtensionType, SymbolEqualityComparer.Default))
                {
                    source.Append($"{methodSymbol.FullName}(reader)");
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SG0001",
                        "no writer",
                        $"method {mtd.Name} contains parameter of type {parameter.Type.Name}, which has no reader. Consider making an extension method.",
                        "error",
                        DiagnosticSeverity.Error,
                        true), Location.None));
                return;
            }

            if (i != mtd.Parameters.Length - 1)
            {
                source.Append(", ");
            }
        }
        
        
        source.AppendLine(");\n        }");
    }
}