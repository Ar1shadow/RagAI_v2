
using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2.Extensions;
using RagAI_v2.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using OllamaSharp.Models.Chat;
using RagAI_v2.Prompts;
using Microsoft.SemanticKernel;

namespace RagAI_v2.Cmd;

/// <summary>
/// Utiliser Dependency Injection pour instancier les commandes. 
/// Les commandes héritent de l'interface ICommand. 
/// Router est un contenant de Commands, utiliser la méthode RegisterCommand pour enregistrer les commandes. 
/// </summary>

public class LoadCommand : ICommand
{
    private readonly Microsoft.SemanticKernel.ChatCompletion.ChatHistory _history;
    private readonly string _historyDirectory;
    //ctor
    public LoadCommand(ChatHistory history, string historyDirectory)
    {
        _history = history;
        _historyDirectory = historyDirectory;
    }

    public string Name => "load";
    public IEnumerable<string> Aliases => new [] { "ld" };

    public string Description => "Charger une historique";
    public string Usage => "/load (sans paramètres)";

    public Task Execute(string[] args)
    {
        if (args.Length > 0)
        {
            ConsoleIO.Warning("Trop de arguments pour la command");
            return Task.CompletedTask;
        }

        if (Path.Exists(_historyDirectory))
        {
            _history.LoadHistory(_historyDirectory);
        }
        else
        {
            ConsoleIO.Warning("Vérifier le chemin du répertoire ou fichier.");
        }
        return Task.CompletedTask;
    }
}

public class SaveCommand : ICommand
{
    private readonly ChatHistory _history;
    private readonly string _savePath;
    public SaveCommand(ChatHistory history, string savePath)
    {
        _history = history;
        _savePath = savePath;
    }
    
    public string Name => "save";
    public string Description => "Sauvegarder l'historique avec nom optionnel";
    public string Usage => "/save [SaveName]";

    public Task Execute(string[] args)
    {
        if (args.Length == 0)
        {
            if (Path.Exists(_savePath))
            {
                _history.SaveHistory(_savePath);
            }
            else
            {
                ConsoleIO.Warning("Vérifier le chemin du répertoire ou fichier.");
            }

        }else
            ConsoleIO.Warning("Nombre de arguments inapproprié pour la command");
        return Task.CompletedTask;
    }
}



public class DeleteCommand : ICommand
{
    private readonly ChatHistory _history;
    private readonly string _savePath;
    
    public DeleteCommand(ChatHistory history, string savePath)
    {
        _history = history;
        _savePath = savePath;
    }
    
    public string Name => "delete";
    public IEnumerable<string> Aliases => new[] { "del" , "rm"};
    public string Description => "Supprimer un fichier ou un répertoire";
    public string Usage => "/delete (sans paramètre)";

    public Task Execute(string[] args)
    {
        _history.DeleteHistory(_savePath);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Command qui permet de fermer le programme
/// </summary>
public class ExitCommand : ICommand
{
    public string Name => "exit";
    public IEnumerable<string> Aliases => new[] { "quit", "q" };
    public string Description => "Quitter le programme";
    public string Usage => "/exit";

    public Task Execute(string[] args)
    {
        if(ConsoleIO.Confirm("Souhaitez-vous sauvegarder l'historique avant de quitter?"))
            return Task.CompletedTask;
        ConsoleIO.WriteSystem("Fermeture du programme...");
        Environment.Exit(0);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Répondre la question en fonction du RAG
/// </summary>
public class QueryCommand : ICommand
{
    private readonly ChatHistory _history;
    private readonly IKernelMemory _memory;
    private readonly IConfigurationRoot _config;
    private readonly IChatCompletionService _chatService;
    private readonly Kernel _kernel;
    /// <summary>
    /// constructeur pour Commande Query
    /// </summary>
    /// <param name="history"></param> L'historique de conversation
    /// <param name="memory"></param> Orchestrateur du RAG
    /// <param name="config"></param> Configuration de l'application
    /// <param name="chatService"></param> 
    public QueryCommand(
        ChatHistory history, 
        IKernelMemory memory, 
        IConfigurationRoot config, 
        IChatCompletionService chatService,
        Kernel kernel)
    {
        _history = history;
        _memory = memory;
        _config = config;
        _chatService = chatService;
        _kernel = kernel;
    }
    
    public string Name => "query";
    public IEnumerable<string> Aliases => new[] { "rag", "ask" };
    public string Description => "Poser une question en utilisant la fonction de RAG";
    public string Usage => "/query <question>";
    
    public async Task Execute(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleIO.Warning("Question est Vide");
            ConsoleIO.WriteSystem("(Utilisez /help <query> pour plus d'info)");
            return;
        }
        
        
        var question = string.Join(" ", args);
        question = await UserQueryProcessor.ReformulerUserInput(question, _kernel);
        ConsoleIO.WriteSystem(question);
        var searchAnswer = await _memory.SearchAsync(question);
        var prompt = SearchResultProcessor.FormatSearchResultPrompt(searchAnswer, question);
        //Pour Tester
        ConsoleIO.WriteSystem($"prompt: {prompt}");
        _history.AddUserMessage(prompt);
        ConsoleIO.WriteAssistant();
        var response = new StringBuilder();

        await foreach (var text in
                       _chatService.GetStreamingChatMessageContentsAsync(_history))
        {
            ConsoleIO.WriteAssistant(text);
            response.Append(text);
        }
        _history.AddAssistantMessage(response.ToString());
    }
}

/// <summary>
/// Command qui affiche la liste des commandes disponibles
/// </summary>
public class HelpCommand : ICommand
{
    private Dictionary<string, ICommand> _commandList;
    public HelpCommand(Dictionary<string, ICommand> commandList)
    {
        _commandList = commandList;
    }
    public string Name => "help";
    public IEnumerable<string> Aliases => new[] { "h", "?", "aide" };
    public string Description => "Afficher la liste des commandes disponibles. ";
    public string Usage => "/help [command]. Utilisez \"/help <command>\" pour plus d’informations sur une commande.";

    public Task Execute(string[] args)
    {
        
        if (args.Length == 0)
        {
            var printList = new HashSet<string>();
            ConsoleIO.WriteTitre("Available Commands:");
            foreach (var cmd in _commandList.Values)
            {
                // éviter les doublons
                if (printList.Contains(cmd.Name)) continue;
                printList.Add(cmd.Name);
                
                if (cmd.Aliases.Any())
                {
                    var aliasInfo = string.Join(",", cmd.Aliases.Select(a => "/" + a));
                    Console.WriteLine($"  /{cmd.Name.PadRight(12)}{cmd.Description} (Alias: {aliasInfo})");
                }else
                    Console.WriteLine($"  /{cmd.Name.PadRight(12)}{cmd.Description}");
               
            }
            Console.WriteLine();
            Console.WriteLine("Utilisez \"/help <command>\" pour plus d’informations sur une commande.");
            Console.WriteLine();
        }
        else if (args.Length == 1)
        {
            var name = args[0];
            if (_commandList.TryGetValue(name, out var cmd))
            {
                var aliasInfo = string.Join(",", cmd.Aliases.Select(a => "/" + a));
                Console.WriteLine();
                ConsoleIO.WriteTitre($"Help: /{cmd.Name}");
                if (cmd.Aliases.Any()) Console.WriteLine($"Commandes Equivalentes : {aliasInfo}");
                Console.WriteLine($"Description : {cmd.Description}");
                Console.WriteLine($"Usage       : {cmd.Usage}");
                Console.WriteLine();
            }
            else
            {
                ConsoleIO.Error($"Commande inconnue : {name}");
            }
        }
        else
        {
            ConsoleIO.Error("Trop de paramètres pour la commande help.");
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Routeur de commande : analyse la ligne de commande et exécute la commande correspondante. Maintenir le dictionnaire de commandes.
/// </summary>
public class CommandRouter
{
    private static Dictionary<string, ICommand> _commands = new();
    //ctor
    public CommandRouter(
        IConfigurationRoot config, 
        ChatHistory history, 
        IKernelMemory memory,
        IChatCompletionService chatService,
        Kernel kernel
        )
    {
        // Enregistrer les commandes par défaut
        var commandList = new List<ICommand>
        {
            new LoadCommand(history,config["ChatHistory:Directory"]??String.Empty),
            new SaveCommand(history,config["ChatHistory:Directory"]??String.Empty),
            new ExitCommand(),
            new DeleteCommand(history,config["ChatHistory:Directory"]??String.Empty),
            new QueryCommand(history:history,memory:memory,config:config, chatService:chatService, kernel)
            
        };
        _commands = commandList.ToDictionary(cmd => cmd.Name);
        
        // Enregistrer les alias de commandes
        foreach (var command in commandList)
        {
            foreach (var alias in command.Aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;
                if (!_commands.TryAdd(alias, _commands[command.Name]))
                    ConsoleIO.Warning($"Conflit d'alias détecté : \"{alias}\" est déjà associé à une autre commande.");
                
            }
        }
        // Enregistrer la commande help après les autres
        _commands["help"] = new HelpCommand(_commands);
        foreach (var alias in _commands["help"].Aliases )
            if (!_commands.TryAdd(alias, _commands["help"]))
                ConsoleIO.Warning($"Conflit d'alias détecté : \"{alias}\" est déjà associé à une autre commande.");
        
    }
    
    /// <summary>
    /// Traite une ligne de commande entrée par l'utilisateur
    /// </summary>
    /// <param name="input"></param>
    public async Task HandleCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var commandName = parts[0].TrimStart('/');
        var args = parts.Skip(1).ToArray();

        if (_commands.TryGetValue(commandName, out var command))
        { 
            await command.Execute(args);
        }
        else
        {
            Console.WriteLine($"Commande inconnue : {commandName}");
            await _commands["help"].Execute(args);
        }
    }
}
