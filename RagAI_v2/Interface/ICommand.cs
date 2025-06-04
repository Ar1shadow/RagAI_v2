namespace RagAI_v2.Interface;

/// <summary>
/// interface de commande
/// </summary>
public interface ICommand
{

    string Name { get; }
    /// <summary>
    /// Liste optionel des alias de la commande 
    /// </summary>
    IEnumerable<string> Aliases => Enumerable.Empty<string>();

    /// <summary>
    /// Description de la commande
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Usage de la commande
    /// </summary>
    string Usage { get; }

    /// <summary>
    /// les op�rations � effectuer lors de l'ex�cution de la commande
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    Task Execute(string[] args);
}