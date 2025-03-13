using System.Diagnostics;
using System.Text;
using CommandLine;
using Copilot4.Entity;
using Copilot4.SyntaxHighlighter;
using CypherPotato.SqliteCollections;
using LightJson;
using LightJson.Serialization;
using PrettyPrompt;
using PrettyPrompt.Highlighting;
using Spectre.Console;
using Spectre.Console.Json;

namespace Copilot4 {

    public record struct ChatMessage ( string Role, string Message );

    internal class Program {

        const string APP_VERSION = "v0.2";

        static volatile bool IsWaitingResponse = false;
        static CancellationTokenSource ConsoleCancellation = new CancellationTokenSource ();
        static ChatContext CurrentChat = null!;
        static ChatModel CurrentModel = null!;
        static AppConfiguration AppConfiguration = null!;

        static string AppDirectory = Path.Combine ( Environment.GetFolderPath ( Environment.SpecialFolder.CommonApplicationData ), "Copilot4" );
        static string AppConfigPath = Path.Combine ( AppDirectory, "app.json5" );
        static string AppPromptHistoryPath = Path.Combine ( AppDirectory, "history" );
        public static string AppDatabase = Path.Combine ( AppDirectory, "appstore.db" );

        static SqliteDictionary ConfigurationStore = SqliteDictionary.Open ( AppDatabase, tableName: "base" );
        static DbRepository<ChatContext> Chats = new DbRepository<ChatContext> ();

        static async Task<int> Main ( string [] args ) {

            if (!Directory.Exists ( AppDirectory ))
                Directory.CreateDirectory ( AppDirectory );

            if (!File.Exists ( AppConfigPath )) {
                AnsiConsole.MarkupLineInterpolated ( $"""
                    Welcome to [bold]Copilot4[/]!

                    To get started, setup your [aquamarine3]app.json5[/] configuration file located at:
                        
                        [link]{AppConfigPath}[/]
                    """ );

                File.WriteAllText ( AppConfigPath, """
                    {
                        models: [
                            {
                                name: "My first model",
                                endpointUrl: "https://api.groq.com/openai/v1/chat/completions",
                                apiKey: "gsk_",
                                systemMessage: "You are an friendly AI assistant.",
                                model: {
                                    name: "llama-3.3-70b-versatile",
                                    maxTokens: 4096,
                                    topp: 1,
                                    temperature: 1.0
                                }
                            }
                        ]
                    }
                    """ );

                return 1;
            }

            JsonOptions.Default.SerializationFlags = JsonSerializationFlags.Json5;
            JsonOptions.Default.PropertyNameComparer = new JsonSanitizedComparer ();

            string appJson = File.ReadAllText ( AppConfigPath );
            if (!JsonOptions.Default.TryDeserialize ( appJson, out JsonValue appJsonData )) {
                AnsiConsole.MarkupLine ( "Your [bold]app.json5[/] looks corrupted. Delete it and run Copilot4 again." );
                Environment.Exit ( 1 );
            }

            AppConfiguration = appJsonData.Get<AppConfiguration> ();
            if (AppConfiguration.EnableChatDecorations == false) {
                AnsiConsole.Console.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
                AnsiConsole.Console.Profile.Capabilities.AlternateBuffer = false;
                AnsiConsole.Console.Profile.Capabilities.Ansi = false;
                AnsiConsole.Console.Profile.Capabilities.Unicode = false;
                AnsiConsole.Console.Profile.Capabilities.Legacy = true;
            }
            if (AppConfiguration.Models.Length == 0) {
                AnsiConsole.MarkupLine ( "You haven't added any models to your app.json5. Try adding models then run Copilot4 again." );
                Environment.Exit ( 1 );
            }

            if (ConfigurationStore [ "LastModel" ] is string lastModelName) {
                CurrentModel = AppConfiguration.Models
                    .FirstOrDefault ( m => m.Name == lastModelName )!;

                if (CurrentModel is null) {
                    AnsiConsole.MarkupLineInterpolated ( $"[lightgoldenrod3]Warning:[/] The last used model (\"{lastModelName}\") is not available.\n" );
                    CurrentModel = AppConfiguration.Models [ 0 ];
                }
            }
            else {
                CurrentModel = AppConfiguration.Models [ 0 ];
            }

            Console.TreatControlCAsInput = true;
            Console.CancelKeyPress += ( object? sender, ConsoleCancelEventArgs e ) => {
                if (IsWaitingResponse) {
                    AnsiConsole.MarkupLine ( "\n\n[gray]Operation cancelled by the user.[/]" );
                    ConsoleCancellation.Cancel ();
                    ConsoleCancellation = new CancellationTokenSource ();
                }
                else {
                    AnsiConsole.MarkupLine ( "Bye!" );
                    Environment.Exit ( 0 );
                }
            };

            await using var prompt = new Prompt (
                persistentHistoryFilepath: AppPromptHistoryPath,
                configuration: new PromptConfiguration (
                    prompt: new FormattedString ( $">>> ", new ConsoleFormat ( Bold: true ) ) ) );

            AnsiConsole.MarkupLine ( $"Welcome to [bold]Copilot4[/]. [grey]Version {APP_VERSION}[/]" );

            ReloadContext ( CurrentModel );

            while (true) {
                var question = await prompt.ReadLineAsync ();
                string questionText = question.Text;

                if (string.IsNullOrEmpty ( questionText )) {
                    continue;
                }
                if (questionText.StartsWith ( '/' )) {
                    string command = questionText [ 1.. ];
                    int commandResult = await HandleCommand ( command );

                    if (commandResult == 1) {
                        return await Main ( args );
                    }

                    continue;
                }

                StringBuilder assistantMessageBuilder = new StringBuilder ();
                CurrentChat.Messages.Add ( new ChatMessage ( "User", questionText ) );

                try {
                    using ISyntaxHighlighter syntaxHighlighter = CurrentModel.GetSyntaxHighlighter ();

                    IsWaitingResponse = true;
                    await foreach (var chunk in Inference.GetCompletionsStreamAsync ( CurrentModel, CurrentChat.Messages, ConsoleCancellation.Token )) {
                        assistantMessageBuilder.Append ( chunk );
                        syntaxHighlighter.Write ( chunk );
                    }
                }
                catch (OperationCanceledException) {
                    ;
                }
                catch (Inference.InferenceException iex) {
                    AnsiConsole.MarkupLineInterpolated ( $"""
                        [indianred]Error:[/] the completions endpoint returned an [bold]{(int) iex.StatusCode} {iex.StatusReason}[/]:
                        
                        """ );

                    if (JsonOptions.Default.TryDeserialize ( iex.ResponseContents ?? string.Empty, out JsonValue responseJson )) {
                        AnsiConsole.Write ( new JsonText ( responseJson.ToString () ) );
                    }
                    else {
                        AnsiConsole.MarkupLineInterpolated ( $"{iex.ResponseContents}" );
                    }
                }
                catch (Exception ex) {
                    AnsiConsole.MarkupLineInterpolated ( $"""
                        [indianred]Error:[/] exception raised from the AI client:
                            [yellow]{ex.Message}[/]

                        """ );
                }
                finally {
                    IsWaitingResponse = false;

                    if (assistantMessageBuilder.Length > 0) {
                        CurrentChat.Messages.Add ( new ChatMessage ( "Assistant", assistantMessageBuilder.ToString () ) );
                    }

                    if (CurrentChat.ModelName != null)
                        Chats.AddOrUpdate ( CurrentChat );

                    AnsiConsole.Write ( "\n\n" );
                }
            }
        }

        static async ValueTask<int> HandleCommand ( string command ) {

            if (string.IsNullOrEmpty ( command ))
                return -1;

            var cmdValues = CommandLineParser.Split ( command );

            switch (cmdValues [ 0 ]) {
                case "help":
                    AnsiConsole.MarkupLine ( """
                        Current chat and model:
                            [white]/switch[/]
                                Open the switch menu to switch the current model.
                            [white]/clear[/]
                                Clears ONLY the console window.
                            [white]/reset[/]
                                Clears both the console window and context.
                            [white]/summarize[/]
                                Summarizes and compresses the current chat history using the
                                base model.
                            [white]/tokens[/]
                                Gets an approximate number of tokens used in the current
                                conversation.

                        Configuration:
                            [white]/sysprompt[/]
                                Prints the current model system prompt.
                            [white]/config[/]
                                Opens the configuration file to edit.
                            [white]/reload[/]
                                Reloads the current configuration. This also resets the context
                                and window.

                        Other:
                            [white]/exit[/]
                                Closes the chat window.

                        """ );
                    break;

                case "config":
                    Process.Start ( new ProcessStartInfo () { FileName = AppConfigPath, UseShellExecute = true } )?.Start ();
                    break;

                case "reload":
                    AnsiConsole.Clear ();
                    AnsiConsole.Reset ();
                    return 1;

                case "switch":
                    if (cmdValues.Length == 1) {
                        SwitchModel ();
                    }
                    else {
                        var index = cmdValues [ 1 ];
                        if (!int.TryParse ( index, out int iindex ) || iindex < 0 || iindex > AppConfiguration.Models.Length) {
                            AnsiConsole.MarkupLineInterpolated ( $"""
                                Please, insert an number from 0-{AppConfiguration.Models.Length}.

                                """ );

                            return -1;
                        }

                        SetSelectedModel ( AppConfiguration.Models [ iindex - 1 ] );
                    }
                    break;

                case "clear":
                    AnsiConsole.Clear ();
                    break;

                case "tokens":
                    AnsiConsole.MarkupLineInterpolated ( $"""
                        Current conversation usage: [white]~{CurrentChat.GetTokenUsage ():N0} tokens[/]

                        """ );
                    break;

                case "reset":
                    Chats.Remove ( CurrentChat );
                    ReloadContext ( CurrentModel );
                    AnsiConsole.Clear ();
                    break;

                case "summarize":

                    if (AppConfiguration.BaseModel is null) {
                        AnsiConsole.MarkupLineInterpolated ( $"""
                            No base model was defined in the app configuration.

                            """ );
                        return -1;
                    }
                    else {
                        var chatTranscript = Inference.SerializeContext ( CurrentChat.Messages );
                        var summarizeModel = AppConfiguration.BaseModel.GetChatModel ( InternalModel.InternalModeProgram.SummarizeConversation );

                        double fromTokens = CurrentChat.GetTokenUsage ();

                        try {
                            IsWaitingResponse = true;

                            List<ChatMessage> summarizeChat = new List<ChatMessage> ();
                            summarizeChat.Add ( new ChatMessage ( "System", summarizeModel.GetFormattedSystemMessage () ?? string.Empty ) );
                            summarizeChat.Add ( new ChatMessage ( "User", chatTranscript ) );

                            var summary = await Inference.GetNextMessageAsync ( summarizeModel, summarizeChat, ConsoleCancellation.Token );

                            CurrentChat.Messages.Clear ();
                            if (CurrentModel.GetFormattedSystemMessage () is { Length: > 0 } modelSystemMessage) {
                                CurrentChat.Messages.Add ( new ChatMessage ( "System", modelSystemMessage ) );
                            }

                            CurrentChat.Messages.Add ( new ChatMessage ( "User", $"Context from last conversation:\n\n{summary}" ) );

                            Chats.AddOrUpdate ( CurrentChat );

                            double toTokens = Inference.GetTokenCount ( summary );
                            double reducedAmount = (fromTokens - toTokens) / fromTokens * 100;
                            AnsiConsole.MarkupLineInterpolated ( $"""
                                Chat shortened and summarized. From [white]~{fromTokens:N0}[/] to [white]~{toTokens:N0}[/] tokens (-{reducedAmount:N2}%)

                                """ );
                        }
                        catch {
                            ;
                        }
                        finally {
                            IsWaitingResponse = false;
                        }
                    }

                    break;

                case "exit":
                    Environment.Exit ( 0 );
                    break;

                case "sysprompt":
                    AnsiConsole.MarkupLineInterpolated ( $"""
                        Current system prompt for [aquamarine3]{CurrentModel.Name}[/] ({CurrentModel.Model.Name}):
                        {CurrentChat.GetSystemMessage () ?? "<empty>"}

                        """ );
                    break;

                default:
                    AnsiConsole.MarkupLineInterpolated ( $"""
                        Invalid command.

                        """ );
                    break;
            }

            return 0;
        }

        static void SwitchModel () {

            if (AppConfiguration is null)
                return;

            AnsiConsole.WriteLine ( "Please, insert the number of the desired model to use:" );
            for (int i = 0; i < AppConfiguration.Models.Length; i++) {
                ChatModel? model = AppConfiguration.Models [ i ];
                string nums = (i + 1).ToString ().PadLeft ( 3 );
                AnsiConsole.MarkupLineInterpolated ( $"- [aquamarine3]{nums}[/]: {model.Name ?? "<unnamed>"} [gray]{model.Model.Name}[/]" );
            }

            AnsiConsole.WriteLine ();
readNum:
            AnsiConsole.Markup ( $"Model (1-{AppConfiguration.Models.Length + 1}): " );
            string? result = Console.ReadLine ();

            if (string.IsNullOrEmpty ( result ))
                return;

            if (!int.TryParse ( result, out int num )) {
                goto readNum;
            }
            if (num < 1 || num > AppConfiguration.Models.Length + 1) {
                goto readNum;
            }

            SetSelectedModel ( AppConfiguration.Models [ num - 1 ] );
        }

        static void SetSelectedModel ( ChatModel model ) {
            CurrentModel = model;
            ConfigurationStore [ "LastModel" ] = CurrentModel.Name;
            ReloadContext ( model );
        }

        static void ReloadContext ( ChatModel model ) {

            bool isNewChat = true;

            var loadedChat = Chats.FirstOrDefault ( c => c.ModelName == model.Name );
            if (loadedChat is null) {
                loadedChat = new ChatContext () { ModelName = model.Name };
                var systemMessage = model.GetFormattedSystemMessage ();
                if (systemMessage != null)
                    loadedChat.Messages.Add ( new ChatMessage ( "System", systemMessage ) );
            }
            else {
                isNewChat = false;
            }

            CurrentChat = loadedChat;

            AnsiConsole.MarkupLine ( $"You're talking with [aquamarine3]{CurrentModel.Name}[/] ({CurrentModel.Model.Name}). Type [bold]/help[/] to get help." );
            if (!isNewChat) {
                AnsiConsole.MarkupLineInterpolated ( $"[darkkhaki]Important: [/]you are continuing a previous conversation with [white]{loadedChat.Messages.Count} messages[/] [gray](~ {loadedChat.GetTokenUsage ():N0} tokens)[/]." );
                AnsiConsole.MarkupLineInterpolated ( $"Type [bold]/reset[/] to start a fresh new conversation." );
            }

            AnsiConsole.WriteLine ();
        }
    }
}
