using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AttributeNetworkWrapperV2.Generator;

[Generator]
public class RpcGenerator : IIncrementalGenerator
{
    public static readonly SymbolDisplayFormat FullNameQualification = new (
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType, 
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes, 
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
    
    
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var allExtensions = context.CompilationProvider.Select((x, _) =>
        {
            var builder = ImmutableArray.CreateBuilder<IMethodSymbol?>();
            
            foreach (var namespaceMember in GetAllNamespacesRecursive(x.GlobalNamespace))
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
                            builder.Add(methodSymbol);
                        }
                    }
                }
            }
            
            return builder.ToImmutable();
        });

        var writers = allExtensions.Select((x, _) =>
        {
            var builder = ImmutableArray.CreateBuilder<ExtensionSyntax>();
            foreach (var methodSymbol in x)
            {
                if (methodSymbol is not null &&
                    methodSymbol.Parameters.Length == 2 && 
                    methodSymbol.ReturnsVoid &&
                    methodSymbol.Parameters[0].Type.ToDisplayString(FullNameQualification) == "AttributeNetworkWrapperV2.NetworkWriter")
                {
                    builder.Add(new ExtensionSyntax(methodSymbol.ToDisplayString(FullNameQualification), methodSymbol.Parameters[1].Type));
                }
            }
            
            return builder.ToImmutable();
        });
        
        var readers = allExtensions.Select((x, _) =>
        {
            var builder = ImmutableArray.CreateBuilder<ExtensionSyntax>();
           
            foreach (var methodSymbol in x)
            {
                if (methodSymbol is not null &&
                    methodSymbol.Parameters.Length == 1 && 
                    !methodSymbol.ReturnsVoid &&
                    methodSymbol.Parameters[0].Type.ToDisplayString(FullNameQualification) == "AttributeNetworkWrapperV2.NetworkReader")
                {
                    builder.Add(new ExtensionSyntax(methodSymbol.ToDisplayString(FullNameQualification), methodSymbol.ReturnType));
                }
            }

            return builder.ToImmutable();
        });
        
       var serverRpcs = context.SyntaxProvider.ForAttributeWithMetadataName("AttributeNetworkWrapperV2.ServerRpcAttribute",
            predicate: static (node, _) => true,
            transform: static (syntaxContext, _) =>
            {
                var methodDeclarationSyntax = syntaxContext.TargetNode;
                return RpcSyntax.Create(syntaxContext.SemanticModel.GetDeclaredSymbol(methodDeclarationSyntax));
                
            }).Where(static x => x is not null);
        
        var clientRpcs = context.SyntaxProvider.ForAttributeWithMetadataName("AttributeNetworkWrapperV2.ClientRpcAttribute",
            predicate: static (node, _) => true,
            transform: static (syntaxContext, _) =>
            {
                var methodDeclarationSyntax = syntaxContext.TargetNode;
                return RpcSyntax.Create(syntaxContext.SemanticModel.GetDeclaredSymbol(methodDeclarationSyntax));
            }).Where(static x => x is not null);
        
        var multiRpcs = context.SyntaxProvider.ForAttributeWithMetadataName("AttributeNetworkWrapperV2.MultiRpcAttribute",
            predicate: static (node, _) => true,
            transform: static (syntaxContext, _) =>
            {
                var methodDeclarationSyntax = syntaxContext.TargetNode;
                return RpcSyntax.Create(syntaxContext.SemanticModel.GetDeclaredSymbol(methodDeclarationSyntax));
            }).Where(static x => x is not null);

        var readerWriter = writers.Combine(readers);
        
        var serverRpcWriterReader = serverRpcs.Combine(readerWriter);
        var clientRpcWriterReader = clientRpcs.Combine(readerWriter);
        var multiRpcWriterReader = multiRpcs.Combine(readerWriter);
        
        context.RegisterSourceOutput(serverRpcWriterReader, static (productionContext, syntax) => GenerateServerRpc(productionContext, syntax.Left!.Value, syntax.Right.Left!, syntax.Right.Right!));
        context.RegisterSourceOutput(clientRpcWriterReader, static (productionContext, syntax) => GenerateClientRpc(productionContext, syntax.Left!.Value, syntax.Right.Left!, syntax.Right.Right!));
        context.RegisterSourceOutput(multiRpcWriterReader, static (productionContext, syntax) => GenerateMultiRpc(productionContext, syntax.Left!.Value, syntax.Right.Left!, syntax.Right.Right!));
        
        var allRpcs = serverRpcs.Collect().Combine(clientRpcs.Collect()).Combine(multiRpcs.Collect());
        var projectName = context.CompilationProvider.Select((x, _) => x.AssemblyName);
        var allRpcAndName = allRpcs.Combine(projectName);
        
        context.RegisterSourceOutput(allRpcAndName, static (productionContext, syntax) => GenerateRpcInvoker(productionContext, syntax.Right, syntax.Left.Left.Left, syntax.Left.Left.Right, syntax.Left.Right));
        
    }
    
    
    static void GenerateServerRpc(SourceProductionContext context, RpcSyntax mtd, ImmutableArray<ExtensionSyntax> writers, ImmutableArray<ExtensionSyntax> readers)
    {
        if (!ValidRpc(context, mtd)) return;
        
        StringBuilder source = new StringBuilder();
        
        StartGeneratingClass(source, mtd.ContainingType);
        GenerateFunctionDeclaration(context, source, mtd, "CallRpc_");
        GenerateWriter(context, source, mtd, writers);
            
        source.AppendLine($"         AttributeNetworkWrapperV2.NetworkManager.Instance.SendToServer(writer.GetData(), (SendType){mtd.SendType});");
        source.AppendLine("        }\n");
        
        GenerateDeserializeFunction(context, source, mtd, readers);
        
        source.AppendLine("   }\n}");
        context.AddSource($"{mtd.FullName}.g.cs", source.ToString());
        
    }
    
    static void GenerateClientRpc(SourceProductionContext context, RpcSyntax mtd, ImmutableArray<ExtensionSyntax> writers, ImmutableArray<ExtensionSyntax> readers)
    {
        if (!ValidRpc(context, mtd)) return;
        
        StringBuilder source = new StringBuilder();

        string conn = "";
        bool found = false;
        foreach (var parameterSymbol in mtd.Parameters)
        {
            if (parameterSymbol.Type.ToDisplayString(FullNameQualification) == "AttributeNetworkWrapperV2.ClientNetworkConnection")
            {
                if (found)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "SG0001",
                            "duplicate conn",
                            $"method {mtd.Name} contains more than one ClientNetworkConnection parameter",
                            "error",
                            DiagnosticSeverity.Error,
                            true), mtd.Location)); 
                    return;
                }
                
                conn = parameterSymbol.Name;
                found = true;
            }
        }
        
        if (!found)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "SG0001",
                    "no writer",
                    $"client rpc {mtd.Name} should contain one ClientNetworkConnection parameter",
                    "error",
                    DiagnosticSeverity.Error,
                    true), mtd.Location));
            return;
        }
        
        StartGeneratingClass(source, mtd.ContainingType);
        GenerateFunctionDeclaration(context, source, mtd, "CallRpc_");
        GenerateWriter(context, source, mtd, writers);
            
        source.AppendLine($"         AttributeNetworkWrapperV2.NetworkManager.Instance.SendToClient({conn}, writer.GetData(), (SendType){mtd.SendType});");
        source.AppendLine("        }\n");
        
        GenerateDeserializeFunction(context, source, mtd, readers);
        
        source.AppendLine("   }\n}");
        context.AddSource($"{mtd.FullName}.g.cs", source.ToString());
        
    }

    static void GenerateMultiRpc(SourceProductionContext context, RpcSyntax mtd, ImmutableArray<ExtensionSyntax> writers, ImmutableArray<ExtensionSyntax> readers)
    {
        if (!ValidRpc(context, mtd)) return;
        
        foreach (var parameterSymbol in mtd.Parameters)
        {
            if (parameterSymbol.Type.ToDisplayString(FullNameQualification) == "AttributeNetworkWrapperV2.ClientNetworkConnection")
            {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "SG0001",
                            "duplicate conn",
                            $"Multi rpc {mtd.Name} cannot contain a ClientNetworkConnection parameter",
                            "error",
                            DiagnosticSeverity.Error,
                            true), mtd.Location)); 
                    return;
            }
        }
        
        StringBuilder source = new StringBuilder();
        
        StartGeneratingClass(source, mtd.ContainingType);
        GenerateFunctionDeclaration(context, source, mtd, "CallRpc_");
        GenerateWriter(context, source, mtd, writers);
            
        source.AppendLine($"         AttributeNetworkWrapperV2.NetworkManager.Instance.SendToAllClients(writer.GetData(), (SendType){mtd.SendType});");
        source.AppendLine("        }\n");
        
        GenerateDeserializeFunction(context, source, mtd, readers);
        
        source.AppendLine("   }\n}");
        context.AddSource($"{mtd.FullName}.g.cs", source.ToString());
        
    }
    static bool ValidRpc(SourceProductionContext context, RpcSyntax mtd)
    {
        switch (mtd.Error)
        {
            case RpcSyntax.RpcError.Partial:
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SG0001",
                        "bad ref",
                        $"type {mtd.ContainingType.Name} containing rpc {mtd.Name} must be a partial class",
                        "error",
                        DiagnosticSeverity.Error,
                        true), mtd.ContainingType.Locations.FirstOrDefault()));
                return false;
            case RpcSyntax.RpcError.VoidStatic:
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SG0001",
                        "static void",
                        $"rpc {mtd.Name} has to be [static void]",
                        "error",
                        DiagnosticSeverity.Error,
                        true), mtd.Location));
                return false;
            default:
                return true;
        }
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

        source.Append($"static void {prefix}{mtd.Name} (");
        
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
                        true), mtd.Location));
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
        source.AppendLine("         if (AttributeNetworkWrapperV2.NetworkManager.Instance == null) \n            return;\n");
        source.AppendLine("         using NetworkWriter writer = new NetworkWriter();");
        source.AppendLine($"         writer.Write({mtd.Hash});");
        
        bool found = false;
        foreach (var parameterSymbol in mtd.Parameters)
        {
            found= false;

            if (parameterSymbol.Type.Name == "ClientNetworkConnection")
            {
                continue;
            }
            
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
                        true), mtd.Location));
                return;
            }
        }
    }

    static void GenerateDeserializeFunction(SourceProductionContext context, StringBuilder source, RpcSyntax mtd, ImmutableArray<ExtensionSyntax> readers)
    {
        source.AppendLine($"        public static void Deserialize_{mtd.Name}_{mtd.Hash} (ClientNetworkConnection senderConn, NetworkReader reader)");

        source.AppendLine("        {");
        source.Append($"            {mtd.Name}(");
        
        bool found = false;
        for (int i = 0; i <  mtd.Parameters.Length; i++)
        {
            
          
            IParameterSymbol parameter = mtd.Parameters[i];
            
            
            if (parameter.Type.Name == "ClientNetworkConnection")
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
                        true), mtd.Location));
                return;
            }

            if (i != mtd.Parameters.Length - 1)
            {
                source.Append(", ");
            }
        }
        
        
        source.AppendLine(");\n        }");
    }

    static IEnumerable<INamespaceSymbol> GetAllNamespacesRecursive(INamespaceSymbol symbol)
    {
        yield return symbol;

        foreach (var namespaceMember in symbol.GetNamespaceMembers())
        {
            foreach (var namespaceSymbol in GetAllNamespacesRecursive(namespaceMember))
            {
                yield return namespaceSymbol;
            }
        }
    }

    static void GenerateRpcInvoker(SourceProductionContext context, string? assemblyName, ImmutableArray<RpcSyntax?> server, ImmutableArray<RpcSyntax?> client, ImmutableArray<RpcSyntax?> multi)
    {
        StringBuilder builder = new StringBuilder();

        Dictionary<ushort, RpcSyntax> hashes = new();
        
        void RegisterRpc( RpcSyntax? rpcSyntax, int callType)
        {
            if (rpcSyntax is { } rpc)
            {
                if (hashes.TryGetValue(rpc.Hash, out var existing))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "SG0001",
                            "repeat hash",
                            $"method {rpc.Name} clashed hash with {existing.Name}, consider renaming one.",
                            "error",
                            DiagnosticSeverity.Error,
                            true), rpc.Location));
                }
                else
                {
                    hashes.Add(rpc.Hash, rpc);
                    builder.AppendLine($"           RpcHandler.RegisterRpc({rpc.Hash}, new RpcHandler.RpcDelegate({rpc.FullName.Remove(rpc.FullName.Length - rpc.Name.Length)}Deserialize_{rpc.Name}_{rpc.Hash}), (RpcHandler.CallType){callType});");
                }
                
            }
        }
        
        builder.AppendLine("using AttributeNetworkWrapperV2;\n");

        builder.AppendLine(@"
namespace AttributeNetworkWrapperV2
{
    internal static class RpcFuncRegisterGenerated
    {
        static RpcFuncRegisterGenerated()
        {");

       
        
        foreach (var rpcSyntax in server)
        {
            
            RegisterRpc(rpcSyntax, 0);
        }
        foreach (var rpcSyntax in client)
        {
            RegisterRpc(rpcSyntax, 1);
        }
        foreach (var rpcSyntax in multi)
        {
            RegisterRpc(rpcSyntax, 2);
        }

        builder.AppendLine(@"
        }
    }
}");
        
        context.AddSource($"{assemblyName}.RpcRegister.g.cs", builder.ToString());
    }
}