using System.Diagnostics;
using System.Text;
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

        static SqliteDictionary Store = SqliteDictionary.Open ( "appstore" );
        static List<ChatMessage> CurrentChatSession = new List<ChatMessage> ();
        static ChatModel? CurrentModel;
        static AppConfiguration? AppConfiguration;

        static string AppDirectory = Path.Combine ( Environment.GetFolderPath ( Environment.SpecialFolder.CommonApplicationData ), "Copilot4" );
        static string AppConfigPath = Path.Combine ( AppDirectory, "app.json5" );
        static string AppPromptHistoryPath = Path.Combine ( AppDirectory, "history" );

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

            CurrentModel = AppConfiguration.Models [ num - 1 ];
            ResetContext ();

            Store [ "LastModel" ] = CurrentModel.Name;

            AnsiConsole.MarkupLine ( $"""
                You're talking with [aquamarine3]{CurrentModel.Name}[/] ({CurrentModel.Model.Name}). Type [bold]/help[/] to get help.

                """ );
        }

        static void ResetContext () {
            if (CurrentModel is null)
                return;

            CurrentChatSession.Clear ();
            if (string.IsNullOrEmpty ( CurrentModel.SystemMessage ) == false)
                CurrentChatSession.Add ( new ChatMessage ( "system", CurrentModel.SystemMessage ) );
        }

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

            bool isSendingMessage = false;
            CancellationTokenSource cancellation = new CancellationTokenSource ();

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

            if (Store [ "LastModel" ] is string lastModelName) {
                CurrentModel = AppConfiguration.Models
                    .FirstOrDefault ( m => m.Name == lastModelName );

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
                if (isSendingMessage) {
                    AnsiConsole.MarkupLine ( "\n\n[gray]Operation cancelled by the user.[/]" );
                    cancellation.Cancel ();
                    cancellation = new CancellationTokenSource ();
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

            AnsiConsole.MarkupLine ( $"""
                Welcome to [bold]Copilot4[/].
                You're talking with [aquamarine3]{CurrentModel.Name}[/] ({CurrentModel.Model.Name}). Type [bold]/help[/] to get help.

                """ );

            ResetContext ();

            while (true) {
                var question = await prompt.ReadLineAsync ();
                string questionText = question.Text;

                if (string.IsNullOrEmpty ( questionText )) {
                    continue;
                }
                if (questionText.StartsWith ( '/' )) {
                    string command = questionText [ 1.. ];

                    switch (command) {
                        case "help":
                            AnsiConsole.MarkupLine ( """
                                Current chat and model:
                                    [white]/switch[/]      Open the switch menu to switch the current model.
                                    [white]/clear[/]       Clears ONLY the console window.
                                    [white]/reset[/]       Clears both the console window and context.

                                Configuration:
                                    [white]/sysprompt[/]   Prints the current model system prompt.
                                    [white]/config[/]      Opens the configuration file to edit.
                                    [white]/reload[/]      Reloads the current configuration. This also resets the context
                                                 and window.

                                Other:
                                    [white]/exit[/]        Closes the chat window.

                                """ );
                            break;

                        case "config":
                            Process.Start ( new ProcessStartInfo () { FileName = AppConfigPath, UseShellExecute = true } )?.Start ();
                            break;

                        case "reload":
                            AnsiConsole.Clear ();
                            AnsiConsole.Reset ();
                            return await Main ( args );

                        case "switch":
                            SwitchModel ();
                            continue;

                        case "clear":
                            AnsiConsole.Clear ();
                            break;

                        case "reset":
                            ResetContext ();
                            AnsiConsole.Clear ();
                            break;

                        case "exit":
                            Environment.Exit ( 0 );
                            break;

                        case "sysprompt":
                            AnsiConsole.MarkupLineInterpolated ( $"""
                                Current system prompt for [aquamarine3]{CurrentModel.Name}[/] ({CurrentModel.Model.Name}):
                                {CurrentModel.SystemMessage ?? "<empty>"}

                                """ );
                            break;

                        default:
                            AnsiConsole.MarkupLineInterpolated ( $"""
                                Invalid command.

                                """ );
                            break;
                    }

                    continue;
                }

                StringBuilder assistantMessageBuilder = new StringBuilder ();
                CurrentChatSession.Add ( new ChatMessage ( "user", questionText ) );

                isSendingMessage = true;
                try {
                    using ISyntaxHighlighter syntaxHighlighter = CurrentModel.GetSyntaxHighlighter ();

                    await foreach (var chunk in Inference.GetCompletionsStreamAsync ( CurrentModel, CurrentChatSession, cancellation.Token )) {
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
                        [indianred]Error:[/] exception raised from the HTTP client:
                            [yellow]{ex.Message}[/]

                        """ );
                }
                finally {
                    isSendingMessage = false;

                    if (assistantMessageBuilder.Length > 0) {
                        CurrentChatSession.Add ( new ChatMessage ( "Assistant", assistantMessageBuilder.ToString () ) );
                    }

                    AnsiConsole.Write ( "\n\n" );
                }
            }
        }
    }
}
