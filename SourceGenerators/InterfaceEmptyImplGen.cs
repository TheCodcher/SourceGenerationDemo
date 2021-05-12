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
        //генерация атрибута, которым следует пометить интерфейсы базовой реализации
        const string AttributeDiscription = @"
        using System;
        namespace GeneratedAttributes
        {
            [AttributeUsage(AttributeTargets.Interface)]
            public class EmptyImplementationAttribute : Attribute { }
        }";

        ImmutableHashSet<INamedTypeSymbol> targetTypes;
        public void Execute(GeneratorExecutionContext context)
        {
            var reciever = context.SyntaxReceiver as SyntaxReciver;
            if (reciever is null) return;

            //закидываем атрибут
            context.AddSource("EmptyImplementationAttribute.cs", AttributeDiscription);

            //подгружаем сборку с новым атрибутом
            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(AttributeDiscription, options));

            //находим определение атрибута в сборке
            var attributeSymbol = compilation.GetTypeByMetadataName("GeneratedAttributes.EmptyImplementationAttribute");

            //првоеряем, нужно ли продолжать работать перед длительной опирацией
            context.CancellationToken.ThrowIfCancellationRequested();

            //получаем все подходящие интерфейсы из ресивера
            targetTypes = reciever.TargetInterfaces

                //переходим к их семантической модели
                .Select(x => compilation.GetSemanticModel(x.SyntaxTree).GetDeclaredSymbol(x))

                //проверяем наличие необходимого атрибута
                //.Where(x => x.GetAttributes().Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))

                //выполняем все действия выше
                .ToImmutableHashSet();

            foreach (var targetType in targetTypes)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                //генерируем код для каждого выбранного интерфейса
                var source = GenerateEmptyImplType(targetType);

                //добавляем сгенерированный код в сборку
                context.AddSource($"{targetType.Name}.EmptyImplementation.cs", source);
            }
        }

        private string GenerateEmptyImplType(INamedTypeSymbol typeSymbol)
        {
            //создаем по шаблону соответствующий тип
            return $@"
            using System;
            namespace {typeSymbol.ContainingNamespace}
            {{
                public partial interface {typeSymbol.Name}
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

            //получаем список членов типа
            var propEnumer = typeSymbol.GetMembers()
                
                //интересуют только свойства
                .OfType<IPropertySymbol>()
                
                //только не реализованные свойства
                .Where(p => p.IsAbstract);

            //обходим все необходимые для реализации свойства
            foreach (var symb in propEnumer)
            {
                //добавляем getter если он есть
                var getter = symb.GetMethod != null ? "get;" : "";

                //добавляем setter если он есть
                var setter = symb.SetMethod != null ? "set;" : "";

                //определяем инициализацию свойства
                var init = targetTypes.Contains(symb.Type) ? $" = {symb.Type}.Empty;" : "";

                //соединяем все в c# код
                var prop = $"public {symb.Type} {symb.Name} {{{getter}{setter}}}{init}";

                builder.AppendLine(prop);
            }
            return builder.ToString();
        }

        //аналогично генерации свойств: выясняем по описанию типа, какой c# код должен быть сгенерирован
        //и генерируем соответствующий код.
        private string GenerateMethods(INamedTypeSymbol typeSymbol)
        {
            var builder = new StringBuilder();

            //обходим все параметры метода и генерируем соответствующую строку c# кода
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

            //получаем список все членов, описанных в типе
            var methodEnumer = typeSymbol.GetMembers()
                
                //интересую только методы
                .OfType<IMethodSymbol>()
                
                //интересуют только методы, а не геттеры, сеттеры, методы управления событиями и т.д.
                .Where(x => x.MethodKind == MethodKind.Ordinary)
                
                //интересуют только не реализованные методы
                .Where(x => x.IsAbstract);

            //генерируем c# код для каждого метода
            foreach (var symb in methodEnumer)
            {
                var refMod = symb.ReturnsByRef ? "ref " : "";
                var returnText = symb.ReturnsVoid ? "" : targetTypes.Contains(symb.ReturnType) ? $"return {refMod}{symb.ReturnType}.Empty; " : $"return {refMod}default; ";
                var returnSignature = symb.ReturnsVoid ? "void" : symb.ReturnType.ToString();
                var methd = $"public {refMod}{returnSignature} {symb.Name}({ParamString(symb)}) {{ {returnText}}}";
                builder.AppendLine(methd);
            }

            return builder.ToString();
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            //В режиме дебага можно поставить точку останова и продебажить анализатор кода
#if DEBUG
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
#endif
            context.RegisterForSyntaxNotifications(() => new SyntaxReciver());
            //пока ничего
        }

        class SyntaxReciver : ISyntaxReceiver
        {
            public readonly List<InterfaceDeclarationSyntax> TargetInterfaces = new List<InterfaceDeclarationSyntax>();
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                var node = syntaxNode as InterfaceDeclarationSyntax;
                if (node != null)
                {
                    if (node.Modifiers.Any(SyntaxKind.PartialKeyword) && node.AttributeLists.Any())
                    {
                        TargetInterfaces.Add(node);
                    }
                };
            }
        }

    }
}

