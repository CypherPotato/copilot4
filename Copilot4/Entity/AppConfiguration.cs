namespace Copilot4.Entity;

public sealed class AppConfiguration {
    public ChatModel [] Models { get; set; } = Array.Empty<ChatModel> ();
    public bool EnableChatDecorations { get; set; } = true;
    public InternalModel? BaseModel { get; set; }
}
