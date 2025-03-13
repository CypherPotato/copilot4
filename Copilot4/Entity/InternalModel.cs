namespace Copilot4.Entity;

public sealed class InternalModel {
    public required string EndpointUrl { get; set; }
    public required string ModelName { get; set; }
    public string? ApiKey { get; set; }

    public ChatModel GetChatModel ( in InternalModeProgram program ) {
        return new ChatModel () {
            EndpointUrl = EndpointUrl,
            ApiKey = ApiKey,
            SystemMessage = program switch {
                InternalModeProgram.SummarizeConversation =>
                   ChatModel.SanitizePrompt ( """
                        Can you summarize the main relevant points of this conversation between \
                        the user and a assistant in a concise and short manner?
                        """ ),
                _ => null
            },
            Model = new ChatModelParameters () {
                Name = ModelName,
                MaxTokens = 4096
            }
        };
    }

    public enum InternalModeProgram {
        SummarizeConversation
    }
}
