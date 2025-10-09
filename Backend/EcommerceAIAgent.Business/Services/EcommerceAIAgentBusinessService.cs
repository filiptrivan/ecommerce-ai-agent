using Qdrant.Client;
using EcommerceAIAgent.Business.Entities;
using EcommerceAIAgent.Business.DTO;
using EcommerceAIAgent.Business.Enums;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Extensions;
using Spiderly.Security.Services;
using Spiderly.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Spiderly.Shared.Emailing;
using OpenAI;
using OpenAI.Chat;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper;
using OpenAI.Embeddings;
using Qdrant.Client.Grpc;
using Microsoft.Extensions.Logging;

namespace EcommerceAIAgent.Business.Services
{
    public class EcommerceAIAgentBusinessService : EcommerceAIAgent.Business.Services.BusinessServiceGenerated
    {
        private readonly IApplicationDbContext _context;
        private readonly EcommerceAIAgent.Business.Services.AuthorizationBusinessService _authorizationService;
        private readonly AuthenticationService _authenticationService;
        private readonly SecurityBusinessService<User> _securityBusinessService;
        private readonly EmailingService _emailingService;
        private readonly ILogger<EcommerceAIAgentBusinessService> _logger;

        private readonly HttpClient _httpClient;

        private readonly ChatClient _openAIChatClient;
        private readonly EmbeddingClient _openAIEmbeddingClient;
        private readonly QdrantClient _qdrantClient;

        private readonly string _externalApiBaseURL = "https://api.readycms.io/GET/products/short/?namespace=prodavnicaalata";

        public EcommerceAIAgentBusinessService(
            IApplicationDbContext context,
            ExcelService excelService,
            EcommerceAIAgent.Business.Services.AuthorizationBusinessService authorizationService,
            SecurityBusinessService<User> securityBusinessService,
            AuthenticationService authenticationService,
            EmailingService emailingService,
            IFileManager fileManager,
            ILogger<EcommerceAIAgentBusinessService> logger
        )
            : base(context, excelService, authorizationService, fileManager)
        {
            _context = context;
            _authorizationService = authorizationService;
            _securityBusinessService = securityBusinessService;
            _authenticationService = authenticationService;
            _emailingService = emailingService;
            _logger = logger;

            _httpClient = new HttpClient();

            OpenAIClient openAIClient = new OpenAIClient(SettingsProvider.Current.OpenAIApiKey);
            _openAIChatClient = openAIClient.GetChatClient("gpt-5-nano");
            _openAIEmbeddingClient = openAIClient.GetEmbeddingClient("text-embedding-3-large");
            _qdrantClient = new QdrantClient(
              host: "e1af4fc9-8280-49ad-b76c-8e2e2f5849ae.europe-west3-0.gcp.cloud.qdrant.io",
              https: true,
              apiKey: SettingsProvider.Current.QdrantApiKey
            );
        }

        #region User

        /// <summary>
        /// IsDisabled is handled inside authorization service
        /// </summary>
        protected override async Task OnBeforeSaveUserAndReturnSaveBodyDTO(UserSaveBodyDTO userSaveBodyDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                if (userSaveBodyDTO.UserDTO.Id <= 0)
                    throw new HackerException("You can't add new user.");

                User user = await GetInstanceAsync<User, long>(userSaveBodyDTO.UserDTO.Id, userSaveBodyDTO.UserDTO.Version);

                if (userSaveBodyDTO.UserDTO.Email != user.Email ||
                    userSaveBodyDTO.UserDTO.HasLoggedInWithExternalProvider != user.HasLoggedInWithExternalProvider
                //userSaveBodyDTO.UserDTO.AccessedTheSystem != user.AccessedTheSystem
                )
                {
                    throw new HackerException("You can't change Email, HasLoggedInWithExternalProvider nor AccessedTheSystem from the main UI form.");
                }
            });
        }

        #endregion

        #region Notification

        public async Task SendNotificationEmail(long notificationId, int notificationVersion)
        {
            await _context.WithTransactionAsync(async () =>
            {
                await _authorizationService.AuthorizeAndThrowAsync<User>(BusinessPermissionCodes.UpdateNotification);

                // Checking version because if the user didn't save and some other user changed the version, he will send emails to wrong users
                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                List<string> recipients = notification.Recipients.Select(x => x.Email).ToList();

                await _emailingService.SendEmailAsync(recipients, notification.Title, notification.EmailBody);
            });
        }

        /// <summary>
        /// Don't need authorization because user can do whatever he wants with his notifications
        /// </summary>
        public async Task DeleteNotificationForCurrentUser(long notificationId, int notificationVersion)
        {
            await _context.WithTransactionAsync(async () =>
            {
                long currentUserId = _authenticationService.GetCurrentUserId();

                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                await _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId && x.Notification.Id == notification.Id)
                    .ExecuteDeleteAsync();
            });
        }

        /// <summary>
        /// Don't need authorization because user can do whatever he wants with his notifications
        /// </summary>
        public async Task MarkNotificationAsReadForCurrentUser(long notificationId, int notificationVersion)
        {
            await _context.WithTransactionAsync(async () =>
            {
                long currentUserId = _authenticationService.GetCurrentUserId();

                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                await _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId && x.Notification.Id == notification.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsMarkedAsRead, true));
            });
        }

        /// <summary>
        /// Don't need authorization because user can do whatever he wants with his notifications
        /// </summary>
        public async Task MarkNotificationAsUnreadForCurrentUser(long notificationId, int notificationVersion)
        {
            await _context.WithTransactionAsync(async () =>
            {
                long currentUserId = _authenticationService.GetCurrentUserId();

                Notification notification = await GetInstanceAsync<Notification, long>(notificationId, notificationVersion);

                await _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId && x.Notification.Id == notification.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsMarkedAsRead, false));
            });
        }

        public async Task<int> GetUnreadNotificationsCountForCurrentUser()
        {
            long currentUserId = _authenticationService.GetCurrentUserId();

            return await _context.WithTransactionAsync(async () =>
            {
                var notificationUsersQuery = _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId && x.IsMarkedAsRead == false);

                int count = await notificationUsersQuery.CountAsync();

                return count;
            });
        }

        public async Task<PaginatedResultDTO<NotificationDTO>> GetNotificationsForCurrentUser(FilterDTO filterDTO)
        {
            PaginatedResultDTO<NotificationDTO> result = new();
            long currentUserId = _authenticationService.GetCurrentUserId(); // Not doing user.Notifications, because he could have a lot of them.

            await _context.WithTransactionAsync(async () =>
            {
                var notificationUsersQuery = _context.DbSet<UserNotification>()
                    .Where(x => x.User.Id == currentUserId)
                    .Select(x => new
                    {
                        UserId = x.User.Id,
                        NotificationId = x.Notification.Id,
                        IsMarkedAsRead = x.IsMarkedAsRead,
                    });

                int count = await notificationUsersQuery.CountAsync();

                var notificationUsers = await notificationUsersQuery
                    .Skip(filterDTO.First)
                    .Take(filterDTO.Rows)
                    .ToListAsync();

                List<NotificationDTO> notificationsDTO = new();

                foreach (var item in notificationUsers)
                {
                    NotificationDTO notificationDTO = new();

                    Notification notification = await GetInstanceAsync<Notification, long>(item.NotificationId, null);
                    notificationDTO.Id = notification.Id;
                    notificationDTO.Version = notification.Version;
                    notificationDTO.Title = notification.Title;
                    notificationDTO.Description = notification.Description;
                    notificationDTO.CreatedAt = notification.CreatedAt;

                    notificationDTO.IsMarkedAsRead = item.IsMarkedAsRead;

                    notificationsDTO.Add(notificationDTO);
                }

                notificationsDTO = notificationsDTO.OrderByDescending(x => x.CreatedAt).ToList();

                result.Data = notificationsDTO;
                result.TotalRecords = count;
            });

            return result;
        }

        #endregion

        #region Agent

        public async Task SaveProductsToVectorDb()
        {
            List<QdrantProductDTO> products = LoadProducts(@"C:\Users\user\Documents\Projects\EcommerceAIAgent\data\products.csv"); // TODO FT: In the production we should switch to the azure blob storage.
            await SaveProductsToVectorDb(products);
        }

        public async Task<string> SendMessage(string prompt)
        {
            List<ChatMessage> messages = new List<ChatMessage>
            {
                new SystemChatMessage("""
Ti si profesionalni web shop prodavac. 
Samo ako ti korisnik trazi da pronadjes proizvod po SKU pozovi tool sa SKU, ako ne postoji to treba da kazes korisniku. 
Ako ti korisnik opisno trazi neki proizvod ili skup proizvoda treba da pozoves tool koji pretrazuje uz pomoc vektorske baze podataka, ako ti je korisnik poslao los query za to, poboljsaj ga. 
Probaj da one shot-ujes odgovor u vecini slucajeva, ako je korisnik bas bio neodredjen onda ga pitaj dodatna pitanja.
"""),
                new UserChatMessage(prompt),
            };

            ChatCompletionOptions options = new()
            {
                Tools = { 
                    searchProductBySKUTool, 
                    searchProductsVectorized,
                    searchProductByIdTool, 
                },
            };

            bool requiresAction;

            do
            {
                requiresAction = false;
                ChatCompletion completion = await _openAIChatClient.CompleteChatAsync(messages, options);

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        {
                            // Add the assistant message to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));
                            break;
                        }

                    case ChatFinishReason.ToolCalls:
                        {
                            // First, add the assistant message with tool calls to the conversation history.
                            messages.Add(new AssistantChatMessage(completion));

                            // Then, add a new tool message for each tool call that is resolved.
                            foreach (ChatToolCall toolCall in completion.ToolCalls)
                            {
                                switch (toolCall.FunctionName)
                                {
                                    case nameof(SearchProductBySKU):
                                        {
                                            using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                            bool hasProductSKU = argumentsJson.RootElement.TryGetProperty("productSKU", out JsonElement productSKU);

                                            if (!hasProductSKU)
                                                throw new ArgumentNullException(nameof(productSKU), $"The {nameof(productSKU)} argument is required.");

                                            string toolResult = PrepareProductForContext(await SearchProductBySKU(productSKU.GetString()));

                                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                            break;
                                        }

                                    case nameof(SearchProductById):
                                        {
                                            using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                            bool hasId = argumentsJson.RootElement.TryGetProperty("productId", out JsonElement productId);

                                            if (!hasId)
                                                throw new ArgumentNullException(nameof(productId), $"The {nameof(productId)} argument is required.");

                                            string toolResult = PrepareProductForContext(await SearchProductById(productId.GetString()));

                                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                            break;
                                        }

                                    case nameof(SearchProductsVectorized):
                                        {
                                            using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                            bool hasQuery = argumentsJson.RootElement.TryGetProperty("query", out JsonElement query);

                                            if (!hasQuery)
                                                throw new ArgumentNullException(nameof(query), $"The {nameof(query)} argument is required.");

                                            List<string> toolResult = await SearchProductsVectorized(query.GetString());

                                            messages.Add(new ToolChatMessage(toolCall.Id, $"Id-jevi proizvoda: {string.Join(", ", toolResult)}"));
                                            break;
                                        }

                                    default:
                                        {
                                            // Handle other unexpected calls.
                                            throw new NotImplementedException();
                                        }
                                }
                            }

                            requiresAction = true;
                            break;
                        }

                    case ChatFinishReason.Length:
                        throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                    case ChatFinishReason.ContentFilter:
                        throw new NotImplementedException("Omitted content due to a content filter flag.");

                    case ChatFinishReason.FunctionCall:
                        throw new NotImplementedException("Deprecated in favor of tool calls.");

                    default:
                        throw new NotImplementedException(completion.FinishReason.ToString());
                }
            } while (requiresAction);

            ChatCompletion completionResult = await _openAIChatClient.CompleteChatAsync(messages);
            return completionResult.Content[0].Text;
        }

        #region Helpers

        #region Tools

        private async Task<List<string>> SearchProductsVectorized(string query)
        {
            ReadOnlyMemory<float> embedding = await GetEmbeddingAsync(query);

            IReadOnlyList<ScoredPoint> searchResult = await _qdrantClient.SearchAsync(
                SettingsProvider.Current.QdrantProductsTableName,
                embedding,
                limit: 5
            );

            return searchResult.Select(r => r.Id.ToString()).ToList();
        }

        private static readonly ChatTool searchProductsVectorized = ChatTool.CreateFunctionTool(
            functionName: nameof(SearchProductsVectorized),
            functionDescription: "Pretrazi i dohvati top 5 proizvoda, tj. njihove id-jeve, iz vektorske baze podataka, po opisu koji je korisnik prosledio i koji je LLM potencijalno dodatno obradio.",
            functionParameters: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "Opis koji je korisnik prosledio i koji je LLM potencijalno dodatno obradio"
                    }
                },
                "required": [ "query" ]
            }
            """u8.ToArray())
        );

        private async Task<ExternalProductDTO> SearchProductBySKU(string productSKU)
        {
            string url = $"{_externalApiBaseURL}&sku={productSKU}";

            // Configure headers
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SettingsProvider.Current.ExternalApiBearerToken);

            // Execute request
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            ExternalProductDTO product = JsonSerializer.Deserialize<ExternalProductDTO>(json);

            return product;
        }

        private static readonly ChatTool searchProductBySKUTool = ChatTool.CreateFunctionTool(
            functionName: nameof(SearchProductBySKU),
            functionDescription: "Pretrazi i dohvati proizvod po njegovom SKU, ovaj tool treba da se koristi samo ako nam korisnik prosledi SKU proizvoda.",
            functionParameters: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "productSKU": {
                        "type": "string",
                        "description": "SKU proizvoda"
                    }
                },
                "required": [ "productSKU" ]
            }
            """u8.ToArray())
        );

        private async Task<ExternalProductDTO> SearchProductById(string productId)
        {
            string url = $"{_externalApiBaseURL}&id={productId}";

            // Configure headers
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SettingsProvider.Current.ExternalApiBearerToken);

            // Execute request
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            ExternalProductDTO product = JsonSerializer.Deserialize<ExternalProductDTO>(json);

            return product;
        }

        private static readonly ChatTool searchProductByIdTool = ChatTool.CreateFunctionTool(
            functionName: nameof(SearchProductById),
            functionDescription: "Pretrazi i dohvati proizvod po njegovom Id, ovaj tool treba da se koristi samo ako nakon sto smo pretragom vektorske baze podataka dobili listu id-jeva.",
            functionParameters: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "productId": {
                        "type": "string",
                        "description": "Id proizvoda"
                    }
                },
                "required": [ "productId" ]
            }
            """u8.ToArray())
        );

        #endregion

        private static List<QdrantProductDTO> LoadProducts(string path)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            using StreamReader reader = new StreamReader(path);
            using CsvReader csv = new CsvReader(reader, config);
            return csv.GetRecords<QdrantProductDTO>().ToList();
        }

        private async Task SaveProductsToVectorDb(List<QdrantProductDTO> products)
        {
            if (!await _qdrantClient.CollectionExistsAsync(SettingsProvider.Current.QdrantProductsTableName))
                await _qdrantClient.CreateCollectionAsync(SettingsProvider.Current.QdrantProductsTableName, new VectorParams { Size = 3072, Distance = Distance.Cosine });

            int batchSize = 100;
            for (int i = 0; i < products.Count; i += batchSize)
            {
                List<QdrantProductDTO> batch = products.Skip(i).Take(batchSize).ToList();
                List<PointStruct> points = new();

                foreach (QdrantProductDTO product in batch)
                {
                    string text = product.ToSearchableText();
                    ReadOnlyMemory<float> embedding = await GetEmbeddingAsync(text);

                    points.Add(new PointStruct
                    {
                        Id = ulong.Parse(product.Id),
                        Vectors = embedding.ToArray(),
                        Payload = { ["title"] = product.Title }
                    });
                }

                await _qdrantClient.UpsertAsync(SettingsProvider.Current.QdrantProductsTableName, points);

                _logger.LogInformation($"Uploaded {i + batch.Count} / {products.Count}");
            }
        }

        /// <summary>
        /// TODO: If it's needed avarage only the html field, also we should clean up the html and make it in markdown
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text)
        {
            const int chunkSize = 25000;

            if (string.IsNullOrWhiteSpace(text))
                return ReadOnlyMemory<float>.Empty;

            if (text.Length <= chunkSize)
            {
                OpenAIEmbedding embedding = await _openAIEmbeddingClient.GenerateEmbeddingAsync(input: text);
                return embedding.ToFloats();
            }

            // Split into chunks if too long
            List<ReadOnlyMemory<float>> vectors = new();
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                string chunk = text.Substring(i, Math.Min(chunkSize, text.Length - i));
                OpenAIEmbedding embedding = await _openAIEmbeddingClient.GenerateEmbeddingAsync(input: chunk);
                vectors.Add(embedding.ToFloats());
            }

            // Average them
            int dim = vectors[0].Length;
            float[] avg = new float[dim];
            foreach (ReadOnlyMemory<float> vec in vectors)
            {
                ReadOnlySpan<float> span = vec.Span;
                for (int i = 0; i < dim; i++)
                    avg[i] += span[i];
            }
            for (int i = 0; i < dim; i++)
                avg[i] /= vectors.Count;

            return new ReadOnlyMemory<float>(avg);
        }

        private string PrepareProductForContext(ExternalProductDTO productDTO)
        {
            return $$"""
Product name: {{productDTO.title}}
Product url: {{productDTO.url}}
""";
        }

        #endregion

        #endregion

    }
}
