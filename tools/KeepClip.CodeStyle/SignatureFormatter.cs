using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace KeepClip.CodeStyle;

internal static class SignatureFormatter
{
    private const int MaximumSignatureWidth = 120;

    public static string Format(string original)
    {
        var source = SourceText.From(original);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var changes = new List<TextChange>();
        foreach (var parameters in root.DescendantNodes().OfType<ParameterListSyntax>())
        {
            if (parameters.Parameters.Count == 0 || parameters.Parent is not
                (BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or DelegateDeclarationSyntax or TypeDeclarationSyntax))
            {
                continue;
            }

            var firstLine = source.Lines.GetLineFromPosition(parameters.OpenParenToken.SpanStart);
            var lastLine = source.Lines.GetLineFromPosition(parameters.CloseParenToken.SpanStart);
            if (firstLine.LineNumber == lastLine.LineNumber &&
                parameters.CloseParenToken.Span.End - firstLine.Start <= MaximumSignatureWidth)
            {
                continue;
            }

            var indentation = new string(' ', firstLine.ToString().TakeWhile(char.IsWhiteSpace).Count() + 4);
            var separators = parameters.Parameters.GetSeparators().ToArray();
            for (var index = 0; index < parameters.Parameters.Count; index++)
            {
                var parameter = parameters.Parameters[index];
                var start = index == 0 ? parameters.OpenParenToken.Span.End : separators[index - 1].Span.End;
                var whitespace = TextSpan.FromBounds(start, parameter.SpanStart);
                if (source.ToString(whitespace).All(char.IsWhiteSpace))
                {
                    AddChange(whitespace, "\r\n" + indentation);
                }
                else
                {
                    // Preserve comments/directives; adjust only an existing whitespace-only line prefix.
                    var line = source.Lines.GetLineFromPosition(parameter.SpanStart);
                    var prefix = TextSpan.FromBounds(line.Start, parameter.SpanStart);
                    if (source.ToString(prefix).All(char.IsWhiteSpace))
                    {
                        AddChange(prefix, indentation);
                    }
                }
            }

            var closingWhitespace = TextSpan.FromBounds(parameters.Parameters.Last().Span.End,
                parameters.CloseParenToken.SpanStart);
            if (source.ToString(closingWhitespace).All(char.IsWhiteSpace))
            {
                AddChange(closingWhitespace, "");
            }
        }

        foreach (var arguments in root.DescendantNodes().OfType<ArgumentListSyntax>())
        {
            if (arguments.Arguments.Count == 0)
            {
                continue;
            }

            var firstLine = source.Lines.GetLineFromPosition(arguments.OpenParenToken.SpanStart);
            var argumentLine = source.Lines.GetLineFromPosition(arguments.Arguments[0].SpanStart);
            if (firstLine.LineNumber == argumentLine.LineNumber)
            {
                continue;
            }

            var indentation = new string(' ', firstLine.ToString().TakeWhile(char.IsWhiteSpace).Count() + 4);
            var separators = arguments.Arguments.GetSeparators().ToArray();
            for (var index = 0; index < arguments.Arguments.Count; index++)
            {
                var argument = arguments.Arguments[index];
                var start = index == 0 ? arguments.OpenParenToken.Span.End : separators[index - 1].Span.End;
                var whitespace = TextSpan.FromBounds(start, argument.SpanStart);
                if (source.ToString(whitespace).All(char.IsWhiteSpace))
                {
                    AddChange(whitespace, "\r\n" + indentation);
                }
            }
        }

        if (changes.Count == 0)
        {
            changes.AddRange(LambdaBlockAlignment.GetChanges(root, source));
        }

        var formatted = source.WithChanges(changes).ToString();
        var originalTokens = root.DescendantTokens().Select(token => (token.RawKind, token.Text));
        var formattedTokens = CSharpSyntaxTree.ParseText(formatted).GetRoot().DescendantTokens()
            .Select(token => (token.RawKind, token.Text));
        if (!originalTokens.SequenceEqual(formattedTokens))
        {
            throw new InvalidOperationException("Signature formatting must preserve every C# token.");
        }

        var originalTrivia = root.DescendantTrivia().Where(trivia => trivia.RawKind is not
            ((int)SyntaxKind.WhitespaceTrivia) and not ((int)SyntaxKind.EndOfLineTrivia))
            .Select(trivia => (trivia.RawKind, trivia.ToFullString()));
        var formattedTrivia = CSharpSyntaxTree.ParseText(formatted).GetRoot().DescendantTrivia()
            .Where(trivia => trivia.RawKind is not ((int)SyntaxKind.WhitespaceTrivia) and not ((int)SyntaxKind.EndOfLineTrivia))
            .Select(trivia => (trivia.RawKind, trivia.ToFullString()));
        if (!originalTrivia.SequenceEqual(formattedTrivia))
        {
            throw new InvalidOperationException("Formatting must preserve comments and directives.");
        }
        return formatted;

        void AddChange(TextSpan span, string replacement)
        {
            if (source.ToString(span) != replacement)
            {
                changes.Add(new TextChange(span, replacement));
            }
        }
    }
}
