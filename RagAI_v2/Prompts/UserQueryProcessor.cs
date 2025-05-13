using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAI_v2.Prompts;

internal static class UserQueryProcessor
{
    public static async Task<string> ReformulerUserInput(
        string userInput,
        Kernel kernel,
        string? prompt = CustomTemplate.Rag.QueryRefinementPrompt)
    {
        // remplacer la variable dans le prompt
        var arg = new KernelArguments()
        {
            ["question"] = userInput,
        };

        var queryRefined = await kernel.InvokePromptAsync(prompt!, arg);
        var query = queryRefined.GetValue<string>()!;
        (var think, var answer) = SplitReasoning(query);
        if (!string.IsNullOrEmpty(think))
            ConsoleIO.WriteSystem($"Think par LLM : {think}\n\n");
        return answer;
    }
    private static (string Think, string Answer) SplitReasoning(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (string.Empty, string.Empty);
        }
        // Split reasoning and answer 
        var think = Regex.Match(text, @"(?<=<think>)([\s\S]*?)(?=</think>)", RegexOptions.IgnoreCase);
        var thinkText = think.Success ? think.Value.Trim() : string.Empty;
        var answer = Regex.Match(text, @"(?<=</think>)([\s\S]*)", RegexOptions.IgnoreCase);
        var answerText = string.Empty;
        if (think.Success)
        {
            answerText = answer.Success ? answer.Value.Trim() : string.Empty;
        }
        else
        {
            thinkText = string.Empty;
            answerText = text;
        }



            return (thinkText, answerText);
    }

}
