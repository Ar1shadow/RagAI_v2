#define module_5

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Prompts;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2.Test;
using RagAI_v2.Extensions;
using Spectre.Console;



#pragma warning disable SKEXP0070
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .UpdateChatModelConfig("appsettings.json")
    .Build();



#if module_1
#pragma warning disable KMEXP00
var reader = new EmbeddedPromptProvider();
Console.WriteLine(reader.ReadPrompt(Constants.PromptNamesSummarize));
#endif

#if module_2
await TestDB.Run();
#endif

#if module_3
await Test_SK_KM_ChatCompletion.Run();
#endif

#if module_4
await TestLoadHistory.Run();
#endif

#if module_5
await TestSaveHistory.Run();
#endif
