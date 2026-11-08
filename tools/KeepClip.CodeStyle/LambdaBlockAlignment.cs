using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace KeepClip.CodeStyle;

internal static class LambdaBlockAlignment
{
    public static IReadOnlyList<TextChange> GetChanges(SyntaxNode root, SourceText source)
    {
        var changes = new List<TextChange>();
        var protectedTrivia = root.DescendantTrivia()
            .Where(trivia => trivia.RawKind != (int)SyntaxKind.WhitespaceTrivia &&
                trivia.RawKind != (int)SyntaxKind.EndOfLineTrivia)
            .Select(trivia => trivia.Span).ToArray();
        foreach (var lambda in root.DescendantNodes().OfType<LambdaExpressionSyntax>())
        {
            if (lambda.Body is not BlockSyntax block)
            {
                continue;
            }

            var lambdaLine = source.Lines.GetLineFromPosition(lambda.SpanStart);
            var blockLine = source.Lines.GetLineFromPosition(block.OpenBraceToken.SpanStart);
            if (lambdaLine.LineNumber == blockLine.LineNumber)
            {
                continue;
            }

            var expected = lambdaLine.ToString().TakeWhile(char.IsWhiteSpace).Count();
            var actual = blockLine.ToString().TakeWhile(char.IsWhiteSpace).Count();
            if (expected == actual)
            {
                continue;
            }

            var lastLine = source.Lines.GetLineFromPosition(block.CloseBraceToken.SpanStart).LineNumber;
            for (var index = blockLine.LineNumber; index <= lastLine; index++)
            {
                var line = source.Lines[index];
                var token = root.FindToken(line.Start);
                if (string.IsNullOrWhiteSpace(line.ToString()) ||
                    (token.Span.Start < line.Start && token.Span.End > line.Start) ||
                    protectedTrivia.Any(span => span.Start < line.Start && span.End > line.Start))
                {
                    continue;
                }

                var length = line.ToString().TakeWhile(char.IsWhiteSpace).Count();
                changes.Add(new TextChange(new TextSpan(line.Start, length),
                    new string(' ', Math.Max(0, length + expected - actual))));
            }

            // Reparse before nested blocks are considered so text edits remain disjoint.
            return changes;
        }

        return changes;
    }
}
