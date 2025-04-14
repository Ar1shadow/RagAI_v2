﻿#define module_6

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

var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromMinutes(5) // Increase the timeout to 5 minutes
};

CancellationTokenSource cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromMinutes(5));

#if module_1
#pragma warning disable KMEXP00
var reader = new EmbeddedPromptProvider();
Console.WriteLine(reader.ReadPrompt(Constants.PromptNamesSummarize));
#endif

#if module_2
await TestDB.Run();
#endif

#if module_3
await Test_SK_KM_ChatCompletion.Run(cts.Token);
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