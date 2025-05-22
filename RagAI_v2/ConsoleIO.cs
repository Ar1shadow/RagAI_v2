using RagAI_v2.Interface;
using Spectre.Console;
namespace RagAI_v2;

public static class ConsoleIO
{

    /// <summary>
    /// Write a assistant message to console
    /// </summary>
    /// <param name="text"></param>
    public static void WriteAssistant(object? text)
    {
        AnsiConsole.Markup($"[yellow]{Markup.Escape(text?.ToString() ?? "")}[/]");
    }
    
    /// <summary>
    /// Write a assistant prompt to console
    /// </summary>
    public static void WriteAssistant()
    {
        AnsiConsole.Markup("\n[yellow]Assistant > [/]");
    }
    
    /// <summary>
    /// Write a assistant message to console and a new line
    /// </summary>
    /// <param name="text"></param>
    public static void WriteLineAssistant(object? text)
    {
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(text?.ToString() ?? "")}[/]");
    }
    
    /// <summary>
    /// Write a system message to console
    /// </summary>
    /// <param name="text"></param>
    public static void WriteSystem(object? text)
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(text?.ToString() ?? "")}[/]");
    }
    
    /// <summary>
    /// Write a user input prompt
    /// </summary>
    public static void WriteUser()
    {
        AnsiConsole.Markup("\n[green]User > [/]");
    }
    
    /// <summary>
    /// Write a user input to the console
    /// </summary>
    /// <param name="text"></param>
    public static void WriteUser(object? text)
    {
        AnsiConsole.Markup($"[green]{Markup.Escape(text?.ToString() ?? "")}[/]");
    }
    
    /// <summary>
    /// Write a user input to the console and a new line
    /// </summary>
    /// <param name="text"></param>
    public static void WriteLineUser(object? text)
    {
        AnsiConsole.MarkupLine($"[green]{Markup.Escape(text?.ToString() ?? "")}[/]");
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
    public static string Ask(string prompt) =>
        AnsiConsole.Prompt(new TextPrompt<string>($"[bold grey]{Markup.Escape(prompt)}[/]").AllowEmpty());

    /// <summary>
    /// Write a prompt to the console and return the input
    /// </summary>
    /// <param name="exception"></param>
    public static void Warning(string exception) =>
        AnsiConsole.MarkupLine("[bold red]WARNING:[/] " + Markup.Escape(exception));

    /// <summary>
    /// Write a error message to the console
    /// </summary>
    /// <param name="exception"></param>
    public static void Error(string exception) =>
        AnsiConsole.MarkupLine("[bold blue]Error:[/] " + Markup.Escape(exception));

    /// <summary>
    /// Write a confirmation prompt to the console and return the input
    /// </summary>
    /// <param name="prompt"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static bool Confirm(string prompt, bool defaultValue = true)
    {
        return AnsiConsole.Confirm($"[bold grey]{Markup.Escape(prompt)}[/]", defaultValue);
    }
    
    
    public static void ShowProgress(Action<ProgressContext> action) =>
        AnsiConsole.Progress().Start(action);
    
}