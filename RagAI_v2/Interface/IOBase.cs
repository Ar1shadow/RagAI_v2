using System.IO;

namespace RagAI_v2.Interface;

public interface IOBase
{
    /// <summary>
    ///  Afficher les entrees de l'utilisateur
    /// </summary>
    public void WriteUser();

    /// <summary>
    /// Afficher les entrees de LLM
    /// </summary>
    /// <param name="text">Le texte à afficher par l'assistant.</param>
    public void WriteAssistant(object? text);
    
    /// <summary>
    /// Afficher les entrees de system
    /// </summary>
    /// <param name="text">Le texte à afficher par system</param>
    public void WriteSystem(object? text);
}