using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SourceGeneratorSamples
{
    [Generator]
    public class EnumDescriptionGenerator : ISourceGenerator
    {
        internal SyntaxTree GenerateExtensionsClass(ITypeSymbol enumSymbol, string @namespace)
        {
            var switchSections = List<SwitchSectionSyntax>();
            foreach (var enumField in enumSymbol.GetMembers().OfType<IFieldSymbol>())
            {
                var descriptionAttribute = enumField.GetAttributes().Where(x => x.AttributeClass.Name == "DescriptionAttribute").FirstOrDefault();
                ExpressionSyntax valueExpression;
                if (descriptionAttribute == null)
                {
                    valueExpression = LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(enumField.Name));
                }
                else
                {
                    var description = descriptionAttribute.ConstructorArguments.Select(x => x.Value).OfType<string>().First();
                    valueExpression = LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(description));
                }

                switchSections = switchSections.Add(SwitchSection()
                    .WithLabels(
                        SingletonList(
                            (SwitchLabelSyntax)CaseSwitchLabel(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(GetEnumNameConsideringNesting(enumSymbol)),
                                    IdentifierName(enumField.Name)
                                )
                            )
                        )
                    )
                    .WithStatements(SingletonList((StatementSyntax)ReturnStatement(valueExpression)))
                );
            }

            const string PARAM_NAME = "value";

            switchSections = switchSections.Add(SwitchSection()
                .WithLabels(SingletonList<SwitchLabelSyntax>(DefaultSwitchLabel()))
                .WithStatements(
                    SingletonList<StatementSyntax>(
                        ReturnStatement(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(PARAM_NAME),
                                    IdentifierName("ToString")
                                )
                            )
                        )
                    )
                )
            );

            var visibility = AccessibilityAsSyntaxKind(enumSymbol);
            var unit = CompilationUnit()
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(
                        NamespaceDeclaration(ParseName(@namespace))
                        .WithMembers(
                            SingletonList<MemberDeclarationSyntax>(
                                ClassDeclaration($"{enumSymbol.Name}Extensions")
                                .WithModifiers(TokenList(new[]{
                                    Token(SyntaxKind.StaticKeyword),
                                    Token(visibility),
                                }))
                                .WithMembers(
                                    SingletonList<MemberDeclarationSyntax>(
                                        MethodDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)), Identifier("GetDescription"))
                                        .WithModifiers(
                                            TokenList(new[]{
                                                Token(SyntaxKind.PublicKeyword),
                                                Token(SyntaxKind.StaticKeyword)
                                            })
                                        )
                                        .WithParameterList(
                                            ParameterList(
                                                SingletonSeparatedList(
                                                    Parameter(Identifier(PARAM_NAME))
                                                    .WithModifiers(TokenList(Token(SyntaxKind.ThisKeyword)))
                                                    .WithType(IdentifierName(GetEnumNameConsideringNesting(enumSymbol))))
                                            )
                                        )
                                    .WithBody(Block(
                                        SwitchStatement(IdentifierName(PARAM_NAME))
                                        .WithSections(switchSections)
                                    ))
                                )
                            )
                        )
                    )
                )
            )
            .NormalizeWhitespace();

            var sourceText = unit.ToString();
            return CSharpSyntaxTree.ParseText(sourceText);
        }

        private static SyntaxKind AccessibilityAsSyntaxKind(ITypeSymbol type)
        {
            var combinedAccessibility = type.DeclaredAccessibility;
            while ((type = type.ContainingType) is not null)
            {
                combinedAccessibility = (Accessibility)Math.Min((byte)combinedAccessibility, (byte)type.DeclaredAccessibility);
            }

            return combinedAccessibility switch
            {
                Accessibility.Private => SyntaxKind.PrivateKeyword,
                Accessibility.ProtectedAndInternal => SyntaxKind.ProtectedKeyword,
                Accessibility.Protected => SyntaxKind.ProtectedKeyword,
                Accessibility.Internal => SyntaxKind.InternalKeyword,
                Accessibility.ProtectedOrInternal => SyntaxKind.InternalKeyword,
                Accessibility.Public => SyntaxKind.PublicKeyword,
                _ => SyntaxKind.None,
            };
        }

        private static string GetEnumNameConsideringNesting(ITypeSymbol enumSymbol)
        {
            var result = enumSymbol.ToDisplayString(new SymbolDisplayFormat(
               SymbolDisplayGlobalNamespaceStyle.Omitted,
               SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
               SymbolDisplayGenericsOptions.IncludeTypeParameters,
               miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
            ));
            return result;
        }

        public void Execute(GeneratorExecutionContext context)
        {
#if ATTACH
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
#endif
            var receiver = context.SyntaxReceiver as SyntaxReceiver;
            foreach (var enumSyntax in receiver.Enums)
            {
                var enumSymbol = context.Compilation.GetSemanticModel(enumSyntax.SyntaxTree).GetDeclaredSymbol(enumSyntax);
                var @namespace = enumSymbol.ContainingNamespace.ToString();
                var extensionsClass = GenerateExtensionsClass(enumSymbol, @namespace);
                context.AddSource(enumSyntax.Identifier.ToString() + "_Extensions_Class", SourceText.From(extensionsClass.ToString(), Encoding.UTF8));
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
#if ATTACH
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
#endif
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        internal class SyntaxReceiver : ISyntaxReceiver
        {
            public System.Collections.Generic.List<EnumDeclarationSyntax> Enums { get; } = new System.Collections.Generic.List<EnumDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is EnumDeclarationSyntax eds && IsTarget(eds))
                {
                    Enums.Add(eds);
                }
            }

            private bool IsTarget(EnumDeclarationSyntax eds)
            {
                return eds.Members.All(m => m.AttributeLists.Any(al => al.Attributes.Any(a =>
                a.Name.ToString() == "Description" &&
                a.ArgumentList.Arguments.Count == 1)));
            }
        }
    }
}
