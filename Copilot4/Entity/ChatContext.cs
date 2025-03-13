namespace Copilot4.Entity;

public class ChatContext : IEquatable<ChatContext> {
    public string? ModelName { get; set; }
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage> ();

    public string? GetSystemMessage () {
        return Messages.FirstOrDefault ( m => m.Role.Equals ( "System", StringComparison.OrdinalIgnoreCase ) ).Message;
    }

    public int GetTokenUsage () {
        return Messages.Sum ( s => Inference.GetTokenCount ( s.Message ) );
    }

    public bool Equals ( ChatContext? other ) {
        return ModelName == other?.ModelName;
    }
}
