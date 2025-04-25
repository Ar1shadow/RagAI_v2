using System.Security.Cryptography;
using Microsoft.KernelMemory.Pipeline;

namespace RagAI_v2.Utils;

public static class Outils
{
    /// <summary>
    /// Check if the input is a command
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static bool IsCommand(string? input)
    {
        if (input is null) { return false;}

        return input.StartsWith('/');
    }
    
    public static string CalculateSHA256(this BinaryData binaryData)
    {
        byte[] byteArray = SHA256.HashData(binaryData.ToMemory().Span);
        return Convert.ToHexString(byteArray).ToLowerInvariant();
    }
    
}