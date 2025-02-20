using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using LightJson;
using Spectre.Console;

namespace Copilot4;

public static class Inference {

    public sealed class InferenceException : Exception {
        public HttpStatusCode StatusCode { get; init; }
        public string? StatusReason { get; init; }
        public string? ResponseContents { get; init; }

        public InferenceException ( string message ) : base ( message ) { }
    }

    public static async IAsyncEnumerable<string> GetCompletionsStreamAsync ( ChatModel model, IList<ChatMessage> chatSession, [EnumeratorCancellation] CancellationToken cancellationToken ) {
        using (var client = new HttpClient ()) {
            var reqMsg = new HttpRequestMessage ( HttpMethod.Post, model.EndpointUrl );
            if (model.ApiKey is not null)
                reqMsg.Headers.TryAddWithoutValidation ( "Authorization", $"Bearer {model.ApiKey}" );

            reqMsg.Content = JsonContent.Create ( new {
                messages = chatSession.Select ( m => new {
                    role = m.Role.ToLower (),
                    content = m.Message
                } ),
                model = model.Model.Name,
                temperature = model.Model.Temperature,
                max_tokens = model.Model.MaxTokens,
                top_p = model.Model.TopP,
                stream = true
            } );

            var response = await client.SendAsync ( reqMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken );
            if (!response.IsSuccessStatusCode) {
                string restext = await response.Content.ReadAsStringAsync ();
                throw new InferenceException ( "The inference endpoint returned an bad response." ) {
                    ResponseContents = restext,
                    StatusCode = response.StatusCode,
                    StatusReason = response.ReasonPhrase
                };
            }

            using (var rs = await response.Content.ReadAsStreamAsync ( cancellationToken ))
            using (var sr = new StreamReader ( rs )) {
                string? line;
                while ((line = await sr.ReadLineAsync ()) != null && !cancellationToken.IsCancellationRequested) {

                    if (line.StartsWith ( "data: {" )) {
                        string lineData = line.Substring ( line.IndexOf ( ':' ) + 2 );
                        JsonValue lineJson = JsonOptions.Default.Deserialize ( lineData );

                        var delta = lineJson [ "choices" ] [ 0 ] [ "delta" ];
                        if (delta [ "content" ].IsString) {
                            string chunk = delta [ "content" ].GetString ();
                            yield return chunk;
                        }
                    }
                }
            }
        }
    }
}
