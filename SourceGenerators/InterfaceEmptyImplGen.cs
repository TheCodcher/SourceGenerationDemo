using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;

namespace CodeSourceGenerationDemo.SourceGenerators
{
    [Generator]
    public class InterfaceEmptyImplGen : ISourceGenerator
    {
        const string AttrebuteDiscription = @"
using System;
namespace CodeSourceGenerationDemo.GenerateAttrebutes
{
    public sealed class EmptyImplementationAttribute : Attribute { }
}";

        ImmutableHashSet<INamedTypeSymbol> targetTypes;
        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("EmptyImplementationAttribute", AttrebuteDiscription);

            //берем всю сборку
            var compilation = context.Compilation;

            //находим в сборке информацию о нашем атребуте
            var attributeSymbol = compilation.GetTypeByMetadataName("CodeSourceGenerationDemo.GenerateAttrebutes.EmptyImplementationAttribute");

            //перебираем все файлы проекта
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                //переходим от файла к типам, которые он содержит
                var semanticModel = compilation.GetSemanticModel(syntaxTree);

                //перебираем все типы в поисках нужных
                targetTypes = syntaxTree.GetRoot().DescendantNodesAndSelf()

                    //интересует только описание интерфейсов
                    .OfType<InterfaceDeclarationSyntax>()

                    //проверяем partial ли тип
                    .Where(x => x.Modifiers.Any(SyntaxKind.PartialKeyword))

                    //переходим от "текста" к семантики, к описанию типа
                    .Select(x => semanticModel.GetDeclaredSymbol(x))
                     
                    //проверяем наличие нужного атребута
                    .Where(x => x.GetAttributes().Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))

                    //выполняем все вышеперечисленное
                    .ToImmutableHashSet();
                foreach (var targetType in targetTypes)
                {
                    var source = GenerateEmptyImplType(targetType);
                    context.AddSource($"{targetType.Name}.EmptyImplementation", source);
                }
            }
        }

        private string GenerateEmptyImplType(INamedTypeSymbol typeSymbol)
        {
            return $@"
                using System;

                namespace {typeSymbol.ContainingNamespace.Name}
                {{
                    partial interface {typeSymbol.Name}
                    {{
                        public static {typeSymbol.Name} Empty = new {typeSymbol.Name}EmptyImplementation();
                        private class {typeSymbol.Name}EmptyImplementation : {typeSymbol.Name}
                        {{
                            {GenerateProperties(typeSymbol)}
                            {GenerateMethods(typeSymbol)}
                        }}
                    }}
                }}";
        }

        private string GenerateProperties(INamedTypeSymbol typeSymbol)
        {
            var builder = new StringBuilder();
            foreach (var symb in typeSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsAbstract))
            {
                var getter = symb.GetMethod != null ? "get;" : "";
                var setter = symb.SetMethod != null ? "set;" : "";
                var init = targetTypes.Contains(symb.Type) ? $" = {symb.Type.Name}.Empty;" : "";

                var prop = $"public {symb.Type} {symb.Name} {{{getter}{setter}}}{init}";

                builder.AppendLine(prop);
            }
            return builder.ToString();
        }
        private string GenerateMethods(INamedTypeSymbol typeSymbol)
        {
            var builder = new StringBuilder();

            string ParamString(IMethodSymbol methodSymbol)
            {
                if (methodSymbol.Parameters.Length == 0)
                    return "";
                var build = new StringBuilder();
                foreach (var param in methodSymbol.Parameters)
                {
                    var refK = param.RefKind == RefKind.None ? "" : $"{param.RefKind.ToString().ToLower()} ";
                    var paramsA = param.IsParams ? "params " : "";
                    var opt = param.IsOptional ? $" = {param.ExplicitDefaultValue}" : "";
                    var text = $"{refK}{paramsA}{param.Type} {param.Name}{opt}, ";
                    build.Append(text);
                }
                return build.Remove(build.Length - 2, 2).ToString();
            }

            foreach (var symb in typeSymbol.GetMembers().OfType<IMethodSymbol>().Where(x => x.MethodKind == MethodKind.Ordinary).Where(x => x.IsAbstract))
            {
                var refMod = symb.ReturnsByRef ? "ref " : "";

                var returnText = symb.ReturnsVoid ? "" : targetTypes.Contains(symb.ReturnType) ? $"return {refMod}{symb.ReturnType.Name}.Empty; " : $"return {refMod}default; ";

                var returnSignature = symb.ReturnsVoid ? "void" : symb.ReturnType.ToString();

                var methd = $"public {refMod}{returnSignature} {symb.Name}({ParamString(symb)}) {{ {returnText}}}";
                builder.AppendLine(methd);
            }
            return builder.ToString();
        }
        public void Initialize(GeneratorInitializationContext context)
        {
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
            //пока ничего
        }
    }
}

