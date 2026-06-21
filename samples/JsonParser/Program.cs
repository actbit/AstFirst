using SampleJson;

Console.WriteLine("=== AstFirst JSON Parser Sample ===\n");

var r1 = JsonParser.Parse("null");
Console.WriteLine($"null → {r1.Ast?.GetType().Name} (errors: {r1.Errors.Count})");

var r2 = JsonParser.Parse("true");
Console.WriteLine($"true → {(r2.Ast as JsonBool)?.Value} (errors: {r2.Errors.Count})");

var r3 = JsonParser.Parse("false");
Console.WriteLine($"false → {(r3.Ast as JsonBool)?.Value} (errors: {r3.Errors.Count})");

var r4 = JsonParser.Parse("42");
Console.WriteLine($"42 → {(r4.Ast as JsonNumber)?.Value} (errors: {r4.Errors.Count})");

var r5 = JsonParser.Parse("-3.14e2");
Console.WriteLine($"-3.14e2 → {(r5.Ast as JsonNumber)?.Value} (errors: {r5.Errors.Count})");

var r6 = JsonParser.Parse("\"hello\"");
Console.WriteLine($"\"hello\" → {(r6.Ast as JsonString)?.Value} (errors: {r6.Errors.Count})");

var r7 = JsonParser.Parse("\"escaped\\nstring\"");
Console.WriteLine($"\"escaped\\\\nstring\" → {(r7.Ast as JsonString)?.Value} (errors: {r7.Errors.Count})");

var r8 = JsonParser.Parse("   42   ");
Console.WriteLine($"   42   → {(r8.Ast as JsonNumber)?.Value} (errors: {r8.Errors.Count})");

try
{
    JsonParser.Parse("@@@");
    Console.WriteLine("@@@ → no error (unexpected)");
}
catch (AstFirst.Core.Lexing.LexException)
{
    Console.WriteLine("@@@ → LexException (未認識文字、期待通り)");
}

Console.WriteLine("\n=== Done ===");
