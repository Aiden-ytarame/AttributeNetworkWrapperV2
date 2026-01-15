using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AttributeNetworkWrapperV2.Generator;

public readonly record struct RpcSyntax(string Name, string FullName, ushort Hash, INamedTypeSymbol ContainingType, Accessibility Accessibility, ImmutableArray<IParameterSymbol> Parameters, ushort SendType, Location? Location, RpcSyntax.RpcError Error = RpcSyntax.RpcError.None, short? ImplOptions = null)
{
    public enum RpcError
    {
        None,
        Partial,
        VoidStatic,
    }
    
    public static RpcSyntax? Create(ISymbol? symbol)
    {
        
        if (symbol is not IMethodSymbol method)
        {
            return null;
        }

        if (!method.ReturnsVoid || !method.IsStatic)
        {
            return new RpcSyntax(method.Name, method.ToDisplayString(RpcGenerator.FullNameQualification), 0, method.ContainingType, method.DeclaredAccessibility, method.Parameters, 0, method.Locations.FirstOrDefault(), RpcError.VoidStatic);
        }
        
        if (!method.ContainingType.DeclaringSyntaxReferences.Any(syntax =>
                syntax.GetSyntax() is BaseTypeDeclarationSyntax declaration
                && declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword))))
        {
            return new RpcSyntax(method.Name, method.ToDisplayString(RpcGenerator.FullNameQualification), 0, method.ContainingType, method.DeclaredAccessibility, method.Parameters, 0, method.Locations.FirstOrDefault(), RpcError.Partial);
        }

        short? options = null;
        
        foreach (var attributeData in method.GetAttributes())
        {
            if (attributeData.ConstructorArguments.Length > 0 &&
                attributeData.AttributeClass!.ContainingNamespace.Name == "CompilerServices" &&
                attributeData.AttributeClass.Name == "MethodImplAttribute")
            {
                if (attributeData.ConstructorArguments[0].Type!.Name == "Int16")
                {
                    options = (short)attributeData.ConstructorArguments[0].Value!; //cast fails otherwise
                }
                else
                {
                    options = (short)(int)attributeData.ConstructorArguments[0].Value!;
                }
            }
        }

        return new RpcSyntax(method.Name, 
            method.ToDisplayString(RpcGenerator.FullNameQualification), 
            method.ToDisplayString(RpcGenerator.FullNameQualification).GetStableHashCode(), 
            method.ContainingType, 
            method.DeclaredAccessibility, 
            method.Parameters, 
            (ushort)method.GetAttributes().First(x => x.AttributeClass!.ContainingNamespace.Name == "AttributeNetworkWrapperV2").ConstructorArguments[0].Value!, 
            method.Locations.FirstOrDefault(),
            RpcError.None,
            options);
    }

    public readonly string Name = Name;
    public readonly string FullName = FullName;
    public readonly ushort Hash =  Hash;
    public readonly INamedTypeSymbol ContainingType = ContainingType;
    public readonly Accessibility Accessibility = Accessibility;
    public readonly ImmutableArray<IParameterSymbol> Parameters = Parameters;
    public readonly ushort SendType = SendType;
    public readonly Location? Location = Location;
    public readonly RpcError Error = Error;
    public readonly short? ImplOptions = ImplOptions;

}

public readonly record struct ExtensionSyntax(string FullName, ITypeSymbol ExtensionType)
{
    public readonly string FullName = FullName;
    public readonly ITypeSymbol ExtensionType = ExtensionType;
    
}