using RagAI_v2.Interface;
using Spectre.Console;
namespace RagAI_v2;

public static class IOmanager
{

    private static readonly System.ConsoleColor UserColor = ConsoleColor.Green;
    private static readonly System.ConsoleColor AssistantColor = ConsoleColor.Yellow;
    private static readonly System.ConsoleColor SystemColor = ConsoleColor.DarkGray;
    
    /// <summary>
    /// Write a assistant message to console
    /// </summary>
    /// <param name="text"></param>
    public static void WriteAssistant(object? text)
    {
        Console.ForegroundColor = AssistantColor;
        Console.Write(text);
    }
    /// <summary>
    /// Write a assistant prompt to console
    /// </summary>
    public static void WriteAssistant()
    {
        Console.ForegroundColor = AssistantColor;
        Console.Write("\nAssistant > ");
    }
    /// <summary>
    /// Write a assistant message to console and a new line
    /// </summary>
    /// <param name="text"></param>
    public static void WriteLineAssistant(object? text)
    {
        Console.ForegroundColor = AssistantColor;
        Console.WriteLine(text);
    }
    /// <summary>
    /// Write a system message to console
    /// </summary>
    /// <param name="text"></param>
    public static void WriteSystem(object? text)
    {
        Console.ForegroundColor = SystemColor;
        Console.WriteLine(text);
    }
    /// <summary>
    /// Write a user input prompt
    /// </summary>
    public static void WriteUser()
    {
        Console.ForegroundColor = UserColor;
        Console.Write("\nUser > ");
    }
    /// <summary>
    /// Write a user input to the console
    /// </summary>
    /// <param name="text"></param>
    public static void WriteUser(object? text)
    {
        Console.ForegroundColor = UserColor;
        Console.Write(text);
    }
    /// <summary>
    /// Write a user input to the console and a new line
    /// </summary>
    /// <param name="text"></param>
    public static void WriteLineUser(object? text)
    {
        Console.ForegroundColor = UserColor;
        Console.WriteLine(text);
    }

    /// <summary>
    /// Write a selection to the console and return the input
    /// </summary>
    /// <param name="title"></param>
    /// <param name="choices"></param>
    /// <returns></returns>
    public static string WriteSelection(string? title, List<string> choices)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .AddChoices(choices));
    }
    /// <summary>
    /// Write a title to the console
    /// </summary>
    /// <param name="title"></param>
    public static void WriteTitre(string? title)
    {
        title ??= "#####";
        AnsiConsole.Write(new Rule(title));
    }
    /// <summary>
    /// Write a prompt to the console and return the input
    /// </summary>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public static string? WriteInput(string? prompt)
    {
        Console.ForegroundColor = SystemColor;
        AnsiConsole.Write(prompt);
        return Console.ReadLine();
    }
    
}