using Bogus;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Xunit;

namespace SourceGeneratorSamples.Tests
{
    public class EnumDescriptionGeneratorTests
    {
        [Theory]
        [InlineData("public", Accessibility.Public)]
        [InlineData("internal", Accessibility.Internal)]
        public void SimpleEnum(string outerClassVisibility, Accessibility expectedAccessibility)
        {
            const string NAMESPACE = "My.Composite.Namespace";
            const string ENUM_NAME = "SimpleEnum";
            var faker = new Faker();

            var description1 = faker.Lorem.Sentence();
            var description2 = faker.Lorem.Sentence();
            var tree = CSharpSyntaxTree.ParseText($@"
using System.ComponentModel;

namespace {NAMESPACE} {{
    {outerClassVisibility} enum {ENUM_NAME} {{
        [Description(""{description1}"")]
        Member1 = 1,
        [Description(""{description2}"")]
        Member2 = 0,
    }}
}}");
            Execute(tree, NAMESPACE, ENUM_NAME, expectedAccessibility);
        }

        [Theory]
        [InlineData("public", "public", Accessibility.Public)]
        [InlineData("internal", "public", Accessibility.Internal)]
        [InlineData("public", "internal", Accessibility.Internal)]

        public void NestedEnum(string visibility, string enumVisibility, Accessibility expectedAccessibility)
        {
            const string NAMESPACE = "Another.Arbitrary.Ns";
            const string ENUM_NAME = "EnumNestedInsideClass";
            var faker = new Faker();

            var description1 = faker.Lorem.Sentence();
            var description2 = faker.Lorem.Sentence();

            var tree = CSharpSyntaxTree.ParseText($@"
using System.ComponentModel;

namespace {NAMESPACE}
{{
    {visibility} class MyClass {{
        {enumVisibility} {ENUM_NAME} Value {{ get; set; }}

        {enumVisibility} enum {ENUM_NAME} {{
            [Description(""{description1}"")]
            Member1 = 1,
            [Description(""{description2}"")]
            Member2 = 0,
        }}
    }}
}}");
            Execute(tree, NAMESPACE, ENUM_NAME, expectedAccessibility);
        }
        private void Execute(SyntaxTree inputSyntaxTree, string NAMESPACE, string ENUM_NAME, Accessibility expectedAccessibility)
        {
            var sut = new EnumDescriptionGenerator();
            var enumSyntaxDeclaration = inputSyntaxTree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().First();

            // Semantic level
            var compilation = CSharpCompilation.Create("MyAssembly", new[] {
                inputSyntaxTree,
                //outputSyntaxTree,
              })
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(DescriptionAttribute).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));

            {
                var tempSemanticModel = compilation.GetSemanticModel(inputSyntaxTree);
                var tempSymbol = tempSemanticModel.GetDeclaredSymbol(enumSyntaxDeclaration);
                var outputSyntaxTree = sut.GenerateExtensionsClass(tempSymbol, NAMESPACE);

                compilation = compilation.AddSyntaxTrees(outputSyntaxTree);
                var outputSemanticModel = compilation.GetSemanticModel(outputSyntaxTree);
                var inputSemanticModel = compilation.GetSemanticModel(inputSyntaxTree);
                var enumSymbol = inputSemanticModel.GetDeclaredSymbol(enumSyntaxDeclaration);

                enumSymbol.TypeKind.Should().Be(TypeKind.Enum);
                enumSymbol.Name.Should().Be(ENUM_NAME);
                enumSymbol.ContainingNamespace.ToString().Should().Be(NAMESPACE);

                var extensionClassSyntax = outputSyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
                var extensionClass = outputSemanticModel.GetDeclaredSymbol(extensionClassSyntax);
                extensionClass.TypeKind.Should().Be(TypeKind.Class);
                extensionClass.Name.Should().Be(ENUM_NAME + "Extensions");
                extensionClass.ContainingNamespace.ToString().Should().Be(NAMESPACE);
                extensionClass.IsStatic.Should().BeTrue();
                extensionClass.DeclaredAccessibility.Should().Be(expectedAccessibility);

                var extensionMethod = extensionClass.GetMembers().OfType<IMethodSymbol>().Where(x => x.Name == "GetDescription" && x.Kind == SymbolKind.Method).Single();
                extensionMethod.IsStatic.Should().BeTrue();
                extensionMethod.IsExtensionMethod.Should().BeTrue();

                var param = extensionMethod.Parameters.First();
                param.Type.Should().Be(enumSymbol);
                param.IsOptional.Should().BeFalse();
            }

            // Assembly level
            {
                var assembly = compilation.BuildAssembly("MyCodeAssembly");

                var enumType = assembly.DefinedTypes
                    .Where(x => x.Namespace == NAMESPACE)
                    .Where(x => x.Name.EndsWith(ENUM_NAME))
                    .Single();
                var enumValues = Enum.GetValues(enumType);

                var extensionsType = assembly.GetType($"{NAMESPACE}.{ENUM_NAME}Extensions", true);
                var extensionMethod = extensionsType.GetMethod("GetDescription");
                var extensionMethodParameter = extensionMethod.GetParameters().First();

                foreach (var enumValue in enumValues)
                {
                    var invocationResult = extensionMethod.Invoke(null, new object[] { enumValue });
                    var expectedDescription = enumType.GetField(enumValue.ToString()).GetCustomAttributes<DescriptionAttribute>().Single().Description;

                    invocationResult.Should().NotBeNull();
                    invocationResult.Should().Be(expectedDescription);
                }
            }
        }
    }
}
