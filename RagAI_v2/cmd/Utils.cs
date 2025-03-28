using Microsoft.KernelMemory.Pipeline;

namespace RagAI_v2.cmd;

public static class Utils
{
    /// <summary>
    /// Check if the input is a command
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    static bool IsCommand(string? input)
    {
        if (input is null) { return false;}

        return input.StartsWith('/');
    }
    
    
    /// <summary>
    /// Show help menu
    /// </summary>
    static void ShowHelp()
    {
        
    }
    
    
}