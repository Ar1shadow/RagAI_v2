using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel.ChatCompletion;
using RagAI_v2.Extensions;
using RagAI_v2.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using RagAI_v2.Prompts;
using Microsoft.SemanticKernel;
using System.Diagnostics;
using RagAI_v2.Utils;

namespace RagAI_v2.Cmd;

/// <summary>
/// Utiliser Dependency Injection pour instancier les commandes. 
/// Les commandes héritent de l'interface ICommand. 
/// Router est un contenant de Commands, utiliser la méthode RegisterCommand pour enregistrer les commandes. 
/// </summary>


///<summary>
///Charger l'historique de conversation depuis un fichier ou un répertoire spécifié.
///</summary>

public class LoadCommand : ICommand
{
    private readonly ChatHistory _history;
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


/// <summary>
/// Command qui permet de sauvegarder l'historique de conversation dans un fichier spécifié.
/// </summary>
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


/// <summary>
/// Command qui permet de supprimer l'historique de conversation ou un fichier spécifique.
/// </summary>
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
/// Command qui permet de répondre la question en utilisant RAG
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
        var sw_search = Stopwatch.StartNew();
        var userInputRefined = await UserQueryProcessor.ReformulerUserInput(question, _kernel);
        ConsoleIO.WriteSystem(userInputRefined);
        var searchAnswer = await _memory.SearchAsync(userInputRefined);
        var prompt = SearchResultProcessor.FormatSearchResultPrompt(searchAnswer, question);
        //Pour Tester
        ConsoleIO.WriteSystem($"prompt: {prompt}");
        ConsoleIO.WriteSystem($"--Recherche est fini à {sw_search.Elapsed} s");

        _history.AddUserMessage(prompt);
        ConsoleIO.WriteAssistant();
        var response = new StringBuilder();

        var sw_answer = Stopwatch.StartNew();
        await foreach (var text in
                       _chatService.GetStreamingChatMessageContentsAsync(_history))
        {
            ConsoleIO.WriteAssistant(text);
            response.Append(text);
        }
        ConsoleIO.WriteSystem($"--Reponse est générée à {sw_answer.Elapsed} s");
        _history.AddAssistantMessage(response.ToString());
    }
}

/// <summary>
/// Command qui permet de modifier la configuration de l'application (fichier appsetting.json).
/// </summary>
public class  ModifyConfigSettingsCommand : ICommand
{
    private readonly string _appSettingsPath;
    /// <summary>
    /// Constructeur pour la commande de modification de la configuration
    /// </summary>
    /// <param name="FileName">le nom du fichier de configuration est appsettings obligatoirement</param>
    public ModifyConfigSettingsCommand(string FileName)
    {
        _appSettingsPath = Path.Combine(AppPaths.Root, FileName);
    }

    public string Name => "config";
    public string Description => "Modifier la configuration de l'application";
    public string Usage => "/config";

    
    private void UpdateAppSettings()
    {
        if (!File.Exists(_appSettingsPath))
        {
            ConsoleIO.Error($"Le fichier de configuration {_appSettingsPath} n'existe pas !");
            return;
        } 
        string json = File.ReadAllText(_appSettingsPath);
        var root = JsonNode.Parse(json) ?? new JsonObject();

        List <string> menu = new()
        {
            { "le chemin de l'historique" },
            { "le chemin de stockage des fichiers" },
            { "la chaîne de connexion à la base de données" },
            { "Quitter" }
        };
        while (true)
        {
            var champ = ConsoleIO.WriteSelection("Selectionnez le champ à modifier", menu);
            if (champ == "Quitter") break;

            string? newValue = null;
            switch (champ)
            {
                case "le chemin de l'historique":
                    newValue = ConsoleIO.Ask("Entrez le nouveau chemin pour le stockage de l'historique : ");
                    if (string.IsNullOrWhiteSpace(newValue))
                    {
                        ConsoleIO.Error("Valeur vide, veuillez réessayer.");
                        continue;
                    }
                    if (!Path.Exists(newValue))
                    {
                        ConsoleIO.Error($"Le chemin {newValue} n'existe pas. Veuillez entrer un chemin valide.");
                        continue;
                    }
                    root["ChatHistory"] ??= new JsonObject();
                    root["ChatHistory"]!["Directory"] = newValue;
                    break;

                case "le chemin de stockage des fichiers":
                    newValue = ConsoleIO.Ask("Entrez le nouveau chemin vers le dossier contenant les fichiers à importer dans la base de données : ");

                    if (string.IsNullOrWhiteSpace(newValue))
                    {
                        ConsoleIO.Error("Valeur vide, veuillez réessayer.");
                        continue;
                    }
                    if (!File.Exists(newValue))
                    {
                        ConsoleIO.Error($"Le chemin {newValue} n'existe pas. Veuillez entrer un chemin valide.");
                        continue;
                    }

                    root["MemoryDB"] ??= new JsonObject();
                    root["MemoryDB"]!["LocalFileStorage"] = newValue;   
                    break;

                case "la chaîne de connexion à la base de données":
                    string? Host = ConsoleIO.Ask("Entrez le nom d'hôte de la base de données [Espace pour utiliser le défaut : localhost]: ");
                    string? Port = ConsoleIO.Ask("Entrez le port de la base de données [Espace pour utiliser le défaut : 5432]: ");
                    string? User = ConsoleIO.Ask("Entrez le nom d'utilisateur de la base de données [Espace pour utiliser le défaut : postgres]: ");
                    string? Password = ConsoleIO.Ask("Entrez le mot de passe de la base de données [Espace pour utiliser le défaut : null]: ");
                    string? Database = ConsoleIO.Ask("Entrez le nom de la base de données [Espace pour utiliser le défaut : postgres]: ");
                    Host = string.IsNullOrWhiteSpace(Host) ? Host = "localhost" : Host.Trim();
                    Port = string.IsNullOrWhiteSpace(Port) ? Port = "5432" : Port.Trim();
                    User = string.IsNullOrWhiteSpace(User) ? User = "postgres" : User.Trim();
                    Password = string.IsNullOrWhiteSpace(Password) ? Password = "" : Password.Trim();
                    Database = string.IsNullOrWhiteSpace(Database) ? Database = "postgres" : Database.Trim();

                    string? ConnectionString = $"Host={Host};Port={Port};Username={User};Password={Password};Database={Database}";
                    root["MemoryDB"] ??= new JsonObject();
                    root["MemoryDB"]!["Postgres"] ??= new JsonObject();
                    root["MemoryDB"]!["Postgres"]!["ConnectString"] = ConnectionString;
                    break;

                default:
                    ConsoleIO.Error("Champ non reconnu.");
                    break;
            }

           
        }
        File.WriteAllText(_appSettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        ConsoleIO.WriteSystem($"Le fichier de configuration a été mis à jour avec succès : {_appSettingsPath}");
    }


    public Task Execute(string[] args)
    {
        if (args.Length > 0)
        {
            ConsoleIO.Warning("Trop de paramètres pour la commande config.");

        }
        else
        {
            ConsoleIO.WriteSystem("Configuration de l'application :");
            UpdateAppSettings();
        }

            return Task.CompletedTask;
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
    public string Usage => "/help [command].\n Utilisez \"/help <command>\" pour plus d’informations sur une commande.";

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
            new QueryCommand(history:history,memory:memory,config:config, chatService:chatService, kernel),
            new ModifyConfigSettingsCommand("appsettings.json")
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
