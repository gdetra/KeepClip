using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace KeepClip.CodeStyle;

internal static class Program
{
    public static int Main(string[] arguments)
    {
        var check = arguments.Contains("--check");
        var different = 0;
        foreach (var path in arguments.Where(argument => argument != "--check"))
        {
            var original = File.ReadAllText(path);
            SourceFileRules.Validate(path, original);
            var formatted = original;
            var maximumPasses = CSharpSyntaxTree.ParseText(original).GetRoot().DescendantNodes().Count() + 1;
            for (var iteration = 0; iteration < maximumPasses; iteration++)
            {
                var next = SignatureFormatter.Format(formatted);
                if (next == formatted)
                {
                    break;
                }

                formatted = next;
                if (iteration == maximumPasses - 1)
                {
                    throw new InvalidOperationException("Nested argument indentation did not converge.");
                }
            }
            if (original == formatted)
            {
                continue;
            }

            different++;
            if (check)
            {
                Console.Error.WriteLine($"Method signature formatting differs: {path}");
            }
            else
            {
                File.WriteAllText(path, formatted, new UTF8Encoding(false));
            }
        }

        Console.WriteLine($"Method signatures: {different} {(check ? "differences" : "formatted files")}.");
        return check && different > 0 ? 1 : 0;
    }

}
