using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagAI_v2.Prompts;

public static class UserQueryProcessor
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
        return queryRefined.GetValue<string>()!;
    }
}