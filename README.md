# Copilot4

Copilot4 is an experimental AI chatbot project that works in your console, similar to [Ollama](https://ollama.com/), but for cloud models like Deepseek, Groq, OpenAI, Mistral, or any other model compatible with the OpenAI API.

<p align="center">
    <img src="./.github/Presentation.png" alt="Copilot4 demo">
</p>

https://github.com/user-attachments/assets/7a990586-36a9-4f4c-9636-77b9e6036cf7

Some of the features:
- **Multi-models:** configure multiple models with their own *system prompts* from different sources that share an OpenAI-compatible API. Even Ollama works here.
- **Syntax-highlighting**: automatically colored markdown responses as the AI sends you the content. Also, supports code highlighting, thanks to [Prism.js](https://prismjs.com/) and [Jint](https://github.com/sebastienros/jint).
- **Cross-platform**: written in C# as a console application.
- **No accounts, no premium**: no extra payment for an extra feature. Everything is open-source.

And some of the features I plan to add soon:
- **Multi-modal chat**: for models that support multi-modal conversations (images, documents), allow attaching them.
- **RAG support**: add support for RAG mechanisms. I still have to figure out how to do this.

To get started, build or download the Copilot4 distributable, then make your first run. After that, open it again and type `/config`. There, you can define your own models definitions by the template:

```json5
{
    // Optional. Global option to disable the entire console coloring, decorations and
    // unicode support.
    // Defaults to true.
    enableChatDecorations: true,

    // Optional. Defines an base model used for the application tasks, such as summarizing
    // the chat conversation. You should use an smaller/cheapier model here.
    baseModel: {
        endpointUrl: "https://api.groq.com/openai/v1/chat/completions",
        apiKey: "gsk_...",
        modelName: "gemma2-9b-it"
    },
    
    // The actual model/agent list.
    models: [
        {
            // Sets the agent name (not the model name).
            name: "Default model",
            
            // Sets the OpenAI API completions endpoint URL. This is not the base address, but
            // the entire endpoint itself.
            endpointUrl: "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            
            // Optional. The API key to use for authentication.
            apiKey: "Your api key",
            
            // Optional. The agent system message to use. New lines can be added in this string
            // since it is JSON5.
            systemMessage: "You're an helpful assistant. \
                Multiline text is supported here too.",
            
            // Optional. Defines the transport interface for the API. Currently, only OpenAi is
            // supported.
            apiInterface: "OpenAi",
            
            // Optional. Defines how the console should highlight the output.
            // Defaults to markdown.
            syntaxHighlighting: "Markdown" | "None",
            
            // Actual model configuration.
            model: {
                // The model name to use.
                name: "gemini-2.0-flash",
                
                // Max completion tokens.
                maxTokens: 4096,
                
                // Top_p.
                topp: 1,
                
                // Temperature.
                temperature: 1.0
            }
        }
    ]
}
```
