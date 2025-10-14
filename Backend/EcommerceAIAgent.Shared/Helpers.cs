using Newtonsoft.Json.Schema.Generation;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EcommerceAIAgent.Shared
{
    public class Helpers
    {
        private static readonly JSchemaGenerator _generator = new();

        public static async Task<T> GetStructuredLlmResponse<T>(ChatClient chatClient, List<ChatMessage> chat, int temperature) where T : class
        {
            string responseJsonSchema = _generator.Generate(typeof(T)).ToString();

            ChatCompletion completion = await chatClient.CompleteChatAsync(
                new List<ChatMessage>(chat) { new SystemChatMessage("Treba da odredis da li je korisnik prosledio tacan SKU i pitao za odredjeni proizvod") },
                new ChatCompletionOptions
                {
                    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                        typeof(T).Name,
                        BinaryData.FromString(responseJsonSchema)
                    ),
                    Temperature=temperature
                }
            );

            return JsonSerializer.Deserialize<T>(completion.Content[0].Text);
        }
    }
}
