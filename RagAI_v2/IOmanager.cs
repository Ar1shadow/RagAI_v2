using RagAI_v2.Interface;
using Spectre.Console;
namespace RagAI_v2;

public static class IOmanager
{
    public static void WriteAssistant(object? text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(text);
    }

    public static void WriteAssistant()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("\nAssistant > ");
    }
    public static void WriteSystem(object? text)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(text);
    }

    public static void WriteUser()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("\nUser > ");
    }

    public static string WriteSelection(string? title, List<string> choices)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .AddChoices(choices));
    }

    public static void WriteTitre(string? title)
    {
        title ??= "#####";
        AnsiConsole.Write(new Rule(title));
    }
    
}