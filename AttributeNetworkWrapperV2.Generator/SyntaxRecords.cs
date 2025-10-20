using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AttributeNetworkWrapperV2.Generator;

public readonly record struct RpcSyntax(string Name, string FullName, ushort Hash, INamedTypeSymbol ContainingType, Accessibility Accessibility, ImmutableArray<IParameterSymbol> Parameters, ushort SendType)
{
    public static RpcSyntax? Create(ISymbol symbol)
    {
        
        if (symbol is not IMethodSymbol method)
        {
            return null;
        }

        if (!method.ReturnsVoid)
        {
            return null;
        }
        
        if (!method.ContainingType.DeclaringSyntaxReferences.Any(syntax =>
                syntax.GetSyntax() is BaseTypeDeclarationSyntax declaration
                && declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword))))
        {
            return null; // not partial
        }

        return new RpcSyntax(method.Name, method.ToDisplayString(RpcGenerator.FullNameQualification), method.ToDisplayString(RpcGenerator.FullNameQualification).GetStableHashCode(), method.ContainingType, method.DeclaredAccessibility, method.Parameters, (ushort)method.GetAttributes()[0].ConstructorArguments[0].Value!);
    }

    public readonly string Name = Name;
    public readonly string FullName = FullName;
    public readonly ushort Hash =  Hash;
    public readonly INamedTypeSymbol ContainingType = ContainingType;
    public readonly Accessibility Accessibility = Accessibility;
    public readonly ImmutableArray<IParameterSymbol> Parameters = Parameters;
    public readonly ushort SendType = SendType;

}

public readonly record struct ExtensionSyntax(string FullName, ITypeSymbol ExtensionType)
{
    public readonly string FullName = FullName;
    public readonly ITypeSymbol ExtensionType = ExtensionType;
    
}