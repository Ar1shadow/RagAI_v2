#define module_3

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Prompts;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2;
using RagAI_v2.Test;
using RagAI_v2.Extensions;
using Spectre.Console;
using RagAI_v2.Utils;



#pragma warning disable SKEXP0070


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

#if module_6
await Test_IO.Run();
#endif

#if module_7
await TestDeleteDB.Run();
#endif

#if module_8
await TestSearch.Run();
#endif

#if module_9
await TestCommand.Run();
#endif

#if module_10
await TestHistory.Run();
#endif

#if module_11
await PythonServiceTest.Run();
#endif

#if module_12
await TestHandler.Run();
#endif

#if module_13
await Evaluator.run();
#endif
