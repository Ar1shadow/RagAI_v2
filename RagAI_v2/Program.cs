using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
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

TestLoadHistory test = new();
await test.Run();
