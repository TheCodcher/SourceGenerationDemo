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
    public class ObjectiveEnumGen : ISourceGenerator
    {
        //генерация атрибута, которым следует пометить интерфейсы базовой реализации
        const string AttributeDiscription = @"
        using System;
        namespace System.ObjectiveEnum
        {
            [AttributeUsage(AttributeTargets.Class)]
            public class ObjectiveEnumAttribute : Attribute { }
        }";

        const string ObjectiveEnumExtentionsDiscription = @"
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Reflection;
        namespace System.ObjectiveEnum
        {
            public interface IObjectiveEnum
            {
                string Name { get; }
                int Ordinal { get; }
            }
            public static class ObjectiveEnum
            {
                private static Type GetEnum(Type type)
                {
                    var attribute = type.GetCustomAttribute<System.ObjectiveEnum.ObjectiveEnumAttribute>();
                    var interfaceDecl = type.GetInterface(nameof(IObjectiveEnum));
                    var declaredmembers = type.GetTypeInfo().DeclaredNestedTypes;
                    var nested = declaredmembers.SingleOrDefault(t => t.Name == ""Enum"").AsType();
                    var has = attribute != null && nested != null && interfaceDecl != null;
                    if (has)
                    {
                        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                        return nested;
                    }
                    else
                    {
                        throw new InvalidOperationException($""type {type.Name} is not an enum"");
                    }
                }
                public static Array GetValues<T>() where T : class => GetValues(typeof(T));
                public static Array GetValues(Type type)
                {
                    var nested = GetEnum(type);
                    return nested.GetMethod(""GetValues"").Invoke(null, null) as Array;
                }
                public static Array GetNames<T>() where T : class => GetValues(typeof(T));
                public static Array GetNames(Type type)
                {
                    var nested = GetEnum(type);
                    return nested.GetMethod(""GetNames"").Invoke(null, null) as Array;
                }
                public static T GetValue<T>(int ordinal) where T : class => GetValue(typeof(T), ordinal) as T;
                public static object GetValue(Type type, int ordinal)
                {
                    var nested = GetEnum(type);
                    var obj = nested.GetMethod(""GetByOrdinal"").Invoke(null, new object[] { ordinal });
                    return obj;
                }
                public static T GetValue<T>(string name) where T : class => GetValue(typeof(T), name) as T;
                public static object GetValue(Type type, string name)
                {
                    var nested = GetEnum(type);
                    var obj = nested.GetMethod(""GetByName"").Invoke(null, new object[] { name });
                    return obj;
                }
                public static bool IsDefined<T>() => IsDefined(typeof(T));
                public static bool IsDefined(Type type)
                {
                    var attribute = type.GetCustomAttribute<System.ObjectiveEnum.ObjectiveEnumAttribute>();
                    var nested = type.GetTypeInfo().DeclaredMembers.SingleOrDefault(m => m.MemberType == MemberTypes.NestedType) as Type;
                    return attribute != null && nested != null;
                }
                public static bool TryGetValue(Type type, string name, out object value)
                {
                    try
                    {
                        value = GetValue(type, name);
                        return true;
                    }
                    catch
                    {
                        value = null;
                        return false;
                    }
                }
                public static bool TryGetValue(Type type, int ordinal, out object value)
                {
                    try
                    {
                        value = GetValue(type, ordinal);
                        return true;
                    }
                    catch
                    {
                        value = null;
                        return false;
                    }
                }
                public static bool TryGetValue<T>(string name, out T value) where T : class
                {
                    if (TryGetValue(typeof(T), name, out var val))
                    {
                        value = val as T;
                        return true;
                    }
                    else
                    {
                        value = null;
                        return false;
                    }
                }
                public static bool TryGetValue<T>(int ordinal, out T value) where T : class
                {
                    if (TryGetValue(typeof(T), ordinal, out var val))
                    {
                        value = val as T;
                        return true;
                    }
                    else
                    {
                        value = null;
                        return false;
                    }
                }
                public static bool Exists(Type type, string name)
                {
                    return TryGetValue(type, name, out _);
                }
                public static bool Exists(Type type, int ordinal)
                {
                    return TryGetValue(type, ordinal, out _);
                }
                public static bool Exists<T>(string name) where T : class => Exists(typeof(T), name);
                public static bool Exists<T>(int ordinal) where T : class => Exists(typeof(T), ordinal);
                public static bool HasFlag(int flags, int flag)
                {
                    return (flag & flags) == flag;
                }
                public static T[] GetFlags<T>(int flags) where T: class
                {
                    return GetFlagsByEnumerable(typeof(T), flags).OfType<T>().ToArray();
                }
                public static Array GetFlags(Type type, int flags)
                {
                    return GetFlagsByEnumerable(type, flags).ToArray();
                }
                private static IEnumerable<IObjectiveEnum> GetFlagsByEnumerable(Type type, int flags)
                {
                    return ObjectiveEnum.GetValues(type).OfType<IObjectiveEnum>().Where(e => HasFlag(flags, e.Ordinal));
                }
            }
        }";

        const string ObjectiveEnumBuilderDiscription = @"
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Reflection;
        namespace System.ObjectiveEnum
        {
            internal static class EnumBuilder
            {
                public static Action<int> SetField<T>(Dictionary<string, T> byname, Dictionary<int, T> byord, Type type, ref int lastOrd, FieldInfo lastOrdMember, 
                    string name, params object[] ctorParam) where T : class
                {
                    var value = Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, ctorParam, null) as T;
                    if (!CheckFree(byname, name))
                    {
                        throw new InvalidOperationException($""object with name \""{name}\"" has already been added"");
                    }
                    var ord = GetFreeOrdinal(byord, ref lastOrd);
                    type.GetRuntimeField(""Ordinal"").SetValue(value, ord);
                    type.GetRuntimeField(""Name"").SetValue(value, name);
                    byname.Add(name, value);
                    byord.Add(ord, value);
                    return SetOrdinal(byord, lastOrdMember, value, type);
                }
                private static void SetLastOrdinal(FieldInfo lastOrd, int value)
                {
                    lastOrd.SetValue(null, value);
                }
                private static bool CheckFree<T>(Dictionary<int, T> byord, int ordinal)
                {
                    return !byord.ContainsKey(ordinal);
                }
                private static bool CheckFree<T>(Dictionary<string, T> byname, string name)
                {
                    return !byname.ContainsKey(name);
                }
                private static int GetFreeOrdinal<T>(Dictionary<int, T> byord, ref int lastOrd)
                {
                    for (int i = lastOrd; i < int.MaxValue; i++)
                    {
                        if (CheckFree(byord, i))
                        {
                            lastOrd = i + 1;
                            return i;
                        }
                    }
                    throw new IndexOutOfRangeException();
                }
                private static Action<int> SetOrdinal<T>(Dictionary<int, T> byord, FieldInfo lastOrdProp, T value, Type type)
                {
                    return i =>
                    {
                        if (i < 0) throw new ArgumentException(""ordinal cant be less then zero"");
                        if (CheckFree(byord, i))
                        {
                            var prevOrd = (int)type.GetRuntimeField(""Ordinal"").GetValue(value);
                            var lastOrd = i == int.MaxValue ? int.MaxValue : i + 1;
                            SetLastOrdinal(lastOrdProp, lastOrd);
                            byord.Remove(prevOrd);
                            byord.Add(i, value);
                            type.GetRuntimeField(""Ordinal"").SetValue(value, i);
                        }
                        else
                        {
                            if (byord[i].GetHashCode() == value.GetHashCode()) return;
                            throw new InvalidOperationException($""object with ordinal {i} has already been added"");
                        }
                    };
                }
            }
        }";

        ImmutableHashSet<ActualSemanticModel> targetTypes;
        public void Execute(GeneratorExecutionContext context)
        {
            var reciever = context.SyntaxReceiver as SyntaxReciver;
            if (reciever is null) return;

            //закидываем атрибут и интерфейс
            context.AddSource("ObjectiveEnumAttribute.cs", AttributeDiscription);
            context.AddSource("ObjectiveEnum.cs", ObjectiveEnumExtentionsDiscription);
            context.AddSource("ObjectiveEnumBuilder.cs", ObjectiveEnumBuilderDiscription);

            //подгружаем сборку с новым атрибутом
            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(AttributeDiscription, options));

            //находим определение атрибута в сборке
            var attributeSymbol = compilation.GetTypeByMetadataName("System.ObjectiveEnum.ObjectiveEnumAttribute");

            //првоеряем, нужно ли продолжать работать перед длительной опирацией
            context.CancellationToken.ThrowIfCancellationRequested();

            //получаем все подходящие типы из ресивера
            var temptargetTypes = reciever.TargetTypes

                //переходим к их семантической модели
                .Select(x => new ActualSemanticModel(compilation, x))

                //проверяем наличие необходимого атрибута
                .Where(x => x.Semantic.GetAttributes().Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))

                //проверяем наличие статического конструктора
                .Where(x => x.StaticCtor != null);

            //выполняем все действия выше
            targetTypes = temptargetTypes.ToImmutableHashSet();


            foreach (var targetType in targetTypes)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                //генерируем код для каждого выбранного типа
                var source = GenerateEnumForType(targetType);

                //добавляем сгенерированный код в сборку
                context.AddSource($"{targetType.Semantic.Name}.ObjectiveEnum.cs", source);
            }
        }



        private string GenerateEnumForType(ActualSemanticModel typeSymbol)
        {
            var semanticSymb = typeSymbol.Semantic;
            var typeName = typeSymbol.Semantic.Name;

            string typeSymbolDeclarationType = semanticSymb.IsRecord ? "record" : semanticSymb.TypeKind.ToString().ToLower();

            //находим необходимые конструкции
            var prepared = PrepareType(typeSymbol);

            //создаем по шаблону соответствующий тип
            return $@"
            using System;
            using System.Reflection;
            using System.Collections.Generic;
            using System.Linq;
            using System.ObjectiveEnum;
            namespace {semanticSymb.ContainingNamespace}
            {{
                sealed partial {typeSymbolDeclarationType} {typeName} : IObjectiveEnum
                {{
                    {GenerateFields(typeName, prepared.EnumNames)}
                    
                    {GenTypeFlow(typeName)}
                    string IObjectiveEnum.Name => Name;
                    int IObjectiveEnum.Ordinal => Ordinal;

                    {GenToStringMeth(typeSymbol.Semantic)}

                    private static class Enum
                    {{
                        private static Dictionary<string, {typeName}> byname = new Dictionary<string, {typeName}>({prepared.EnumNames.Length});
                        private static Dictionary<int, {typeName}> byord = new Dictionary<int, {typeName}>({prepared.EnumNames.Length});

                        {GenEnumFlow(typeName)}

                        {GenerateEnumMethods(prepared.EnumNames, typeName, prepared.CtorParamStrings)}
                    }}
                }}
            }}";
        }

        private string GenTypeFlow(string typeName)
        {
            return $@"
            public readonly int Ordinal;
            public readonly string Name;

            public override int GetHashCode()
            {{
                return Ordinal;
            }}
            public override bool Equals(object obj)
            {{
                return (obj is {typeName} val) ? val.GetHashCode() == GetHashCode() : false;
            }}
            public static {typeName} Parse(string name) => ObjectiveEnum.GetValue<{typeName}>(name);
            public static bool TryParse(string name, out {typeName} value) => ObjectiveEnum.TryGetValue(name, out value);
            public static explicit operator {typeName}(int ordinal) => Enum.GetByOrdinal(ordinal);
            public static implicit operator int({typeName} value) => value.Ordinal;
            public static int operator |({typeName} value1, {typeName} value2) => value1.Ordinal | value2.Ordinal;
            public static int operator &({typeName} value1, {typeName} value2) => value1.Ordinal & value2.Ordinal;
            public static int operator |({typeName} value1, int value2) => value1.Ordinal | value2;
            public static int operator &({typeName} value1, int value2) => value1.Ordinal & value2;
            public static bool operator ==({typeName} value1, {typeName} value2) => value1.GetHashCode() == value2.GetHashCode();
            public static bool operator !=({typeName} value1, {typeName} value2) => !(value1 == value2);
            ";
        }
        private string GenEnumFlow(string typeName)
        {
            return $@"
            private static int lastOrd = 0;
           
            public static {typeName} GetByName(string name)
            {{
                return byname[name];
            }}
            public static {typeName} GetByOrdinal(int ordinal)
            {{
                return byord[ordinal];
            }}
            public static {typeName}[] GetValues() => byname.Values.ToArray();
            public static string[] GetNames() => byname.Keys.ToArray();
            ";
        }

        private string GenToStringMeth(INamedTypeSymbol typeSymbol)
        {
            var hasMethod = typeSymbol.MemberNames.Contains("ToString");
            if (hasMethod)
            {
                return "";
            }
            else
            {
                return $@"
                public override string ToString()
                {{
                    return Name;
                }}";
            }
        }
        private (MethodParamString[] CtorParamStrings, string[] EnumNames) PrepareType(ActualSemanticModel typeSymbol)
        {
            var ctors = typeSymbol.GetMemberSyntax<ConstructorDeclarationSyntax>(m => !m.Modifiers.Any(SyntaxKind.StaticKeyword))
                .Select(c => new MethodParamString(c)).ToArray();

            return (ctors, typeSymbol.GetCtorInvocation());
        }


        private string GenerateFields(string typeName, string[] names)
        {
            const string format = "public static {0} {1} => Enum.GetByName(\"{2}\");";

            //просто так, хотя стоило бы использовать везде
            var charCount = names.Sum(n => n.Length + typeName.Length + format.Length);
            var builder = new StringBuilder(charCount);

            foreach (var name in names)
            {
                builder.AppendFormat(format, typeName, name, name);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        //обходим все параметры метода и генерируем соответствующую строку c# кода

        private string GenerateEnumMethod(string name, string typeName, MethodParamString[] paramStrings)
        {
            const string format = "public static Action<int> {0}({1}) => EnumBuilder.SetField(byname, byord, typeof({2}), ref lastOrd, typeof({3}.Enum).GetField(\"lastOrd\", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static), \"{4}\", {5});";

            var builder = new StringBuilder();
            foreach (var paramString in paramStrings)
            {
                var param = paramString.ToStringOnlyValue();
                string actualParam = string.IsNullOrEmpty(param) ? "null" : param;
                builder.AppendFormat(format, name, paramString.ToString(), typeName, typeName, name, actualParam);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private string GenerateEnumMethods(string[] methodNames, string typeName, MethodParamString[] CtorParamStrings)
        {
            var builder = new StringBuilder();

            foreach (var methname in methodNames)
            {
                var method = GenerateEnumMethod(methname, typeName, CtorParamStrings);
                builder.AppendLine(method);
            }

            return builder.ToString();
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            //В режиме дебага можно поставить точку останова и продебажить анализатор кода
#if DEBUG
            //if (!Debugger.IsAttached)
            //{
            //    Debugger.Launch();
            //}
#endif
            context.RegisterForSyntaxNotifications(() => new SyntaxReciver());
            //пока ничего
        }

        class SyntaxReciver : ISyntaxReceiver
        {
            public readonly List<TypeDeclarationSyntax> TargetTypes = new List<TypeDeclarationSyntax>();
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                var node = syntaxNode as TypeDeclarationSyntax;
                if (node != null && node is not InterfaceDeclarationSyntax)
                {
                    if (node.Modifiers.Any(SyntaxKind.PartialKeyword) && node.AttributeLists.Any())
                    {
                        TargetTypes.Add(node);
                    }
                };
            }
        }

        class ActualSemanticModel
        {
            public readonly INamedTypeSymbol Semantic;
            public readonly MemberDeclarationSyntax StaticCtor;
            public readonly TypeDeclarationSyntax Syntax;

            const string CLASS_NAME = "Enum";
            public ActualSemanticModel(Compilation compilation, TypeDeclarationSyntax syntax)
            {
                Syntax = syntax;

                Semantic = compilation.GetSemanticModel(syntax.SyntaxTree).GetDeclaredSymbol(syntax);

                StaticCtor = syntax.Members.Where(m => m is ConstructorDeclarationSyntax).SingleOrDefault(c => c.Modifiers.Any(SyntaxKind.StaticKeyword));
            }

            public string[] GetCtorInvocation()
            {
                //можно использовать для проверки при дебаге
                //var e1 = StaticCtor.ChildNodes().SingleOrDefault(n=>n.IsKind(SyntaxKind.Block));
                //var e2 = e1.ChildNodes().Where(n => n.IsKind(SyntaxKind.ExpressionStatement)).ToArray();
                //var e3 = e2.Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.InvocationExpression))).ToArray();
                //var e4 = e3.Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression))).ToArray();
                //var e5 = e4.Select(n => n as MemberAccessExpressionSyntax).Where(n => n != null).Select(n => (n.Expression.ToString(), n.Name.ToString())).ToArray();

                //получаем содержимое статического конструктора
                var block = StaticCtor.ChildNodes().SingleOrDefault(n => n.IsKind(SyntaxKind.Block));

                var exprs = block

                    //переходим к выражениям
                    .ChildNodes().Where(n => n.IsKind(SyntaxKind.ExpressionStatement))

                    //переходим к выражениям-вызова
                    .Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.InvocationExpression)))

                    //переходим к названию вызываемых членов
                    .Select(n => 
                    {
                        var nodes = n.ChildNodes();
                        var member = nodes.SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression));
                        if (member == null)
                        {
                            var newNodes = nodes.SingleOrDefault(c => c.IsKind(SyntaxKind.InvocationExpression));
                            return newNodes.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression));
                        }
                        else
                        {
                            return member;
                        }
                    })

                    //проверям предыдущий переход + каст
                    .Select(n => n as MemberAccessExpressionSyntax).Where(n => n != null)

                    //проверям необходимый нам вызов
                    .Where(n => n.Expression.ToString() == CLASS_NAME)

                    //возвращаем имя вызываемого члена
                    .Select(n => n.Name.ToString());

                //var local = block

                //    //переходим к выражениям
                //    .ChildNodes().Where(n => n.IsKind(SyntaxKind.ExpressionStatement))

                //    //переходим к выражениям-вызова
                //    .Select(n => n.ChildNodes().SingleOrDefault(c => c.IsKind(SyntaxKind.SimpleMemberAccessExpression)))

                //    //проверям предыдущий переход + каст
                //    .Select(n => n as MemberAccessExpressionSyntax).Where(n => n != null)

                //    //проверям необходимый нам вызов
                //    .Where(n => n.Expression.ToString() == CLASS_NAME)

                //    //возвращаем имя вызываемого члена
                //    .Select(n => n.Name.ToString());

                //return exprs.Concat(local).ToArray();
                return exprs.ToArray();
            }

            public IEnumerable<T> GetMemberSyntax<T>(Func<T,bool> predicate) where T : MemberDeclarationSyntax
            {
                return Syntax.Members.Where(m => m is T).Select(m => m as T).Where(predicate);
            }

        }

        class MethodParamString
        {
            BaseMethodDeclarationSyntax symb;
            public MethodParamString(BaseMethodDeclarationSyntax methodSymbol)
            {
                symb = methodSymbol;
            }

            public override string ToString()
            {
                return symb.ParameterList.Parameters.ToFullString();
            }
            public string ToStringOnlyValue()
            {
                return string.Join(", ", symb.ParameterList.Parameters.Select(p => p.Identifier));
            }
            public string ToStringValuesAndKeywords()
            {
                return string.Join(", ", symb.ParameterList.Parameters.Select(p => $"{p.Modifiers} {p.Identifier}"));
            }
        }
    }
}

