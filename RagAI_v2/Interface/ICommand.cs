namespace RagAI_v2.Interface;

public interface ICommand
{
    string Name { get; } 
    
    IEnumerable<string> Aliases => Enumerable.Empty<string>();
        
    string Description { get; }
    string Usage { get; }
    Task Execute(string[] args);
}