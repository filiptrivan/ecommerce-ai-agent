using Qdrant.Client;
using Qdrant.Client.Grpc;
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
using Microsoft.Extensions.Logging;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Numeric;
using System;
using Newtonsoft.Json.Schema.Generation;
using EcommerceAIAgent.Shared;
using EcommerceAIAgent.Business.LlmResponses;
using Google.Protobuf;
using Microsoft.Extensions.Options;

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

        private const string _systemPromptTemplate = """
Ti si profesionalni web shop prodavac na sajtu www.prodavnicaalata.rs.
Imaš dva maloprodajna objekta:
- Vojislava Ilića 141g
- Na Altini - Ugrinovačka 212

VAŽNA PRAVILA:
- Odgovaraj direktno i profesionalno
- Nikad ne prikazuj ID proizvoda
- Ako korisnik nije precizan, postavi jedno ili dva dodatna pitanja
- Koristi funkcije za pretraživanje proizvoda kada je potrebno, ako ih ne nađeš iz prvog pokušaja, nema potrebe da ponavljaš pretragu sa sličnim upitom više puta, samo obavesti korisnika da nisi uspeo da pronađeš.
- Ne spominji tehničke detalje (vektorska baza, ID-jeve, itd.)
- Ako korisnik pita neko opšte pitanje o proizvodu iskoristi svoj osnovni model za odgovor.
""";

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
            List<QdrantProductDTO> products = LoadProducts(@"C:\Users\user\Documents\Projects\RecommenderSystems\data\products.csv"); // TODO FT: In the production we should switch to the azure blob storage.
            await SaveProductsToVectorDb(products);
        }

        public async Task<string> SendMessage(List<ChatMessage> chatHistory, string userPrompt)
        {
            List<ChatMessage> chat = PrepareChat(chatHistory, userPrompt);
            ChatCompletionOptions options = new()
            {
                Tools =
                {
                    searchProductsVectorizedTool,
                    searchProductsByIdTool,
                }
            };

            bool requiresAction;
            do
            {
                requiresAction = false;
                ChatCompletion completion = await _openAIChatClient.CompleteChatAsync(chat, options);

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        {
                            chat.Add(new AssistantChatMessage(completion));
                            break;
                        }

                    case ChatFinishReason.ToolCalls:
                        {
                            chat.Add(new AssistantChatMessage(completion));

                            foreach (ChatToolCall toolCall in completion.ToolCalls)
                            {
                                switch (toolCall.FunctionName)
                                {
                                    case nameof(SearchProductsVectorized):
                                        {
                                            using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                            bool hasQuery = argumentsJson.RootElement.TryGetProperty("query", out JsonElement query);
                                            bool hasLimit = argumentsJson.RootElement.TryGetProperty("limit", out JsonElement limit);
                                            bool hasPriceLowerLimit = argumentsJson.RootElement.TryGetProperty("priceLowerLimit", out JsonElement priceLowerLimit);
                                            bool hasPriceUpperLimit = argumentsJson.RootElement.TryGetProperty("priceUpperLimit", out JsonElement priceUpperLimit);

                                            if (!hasQuery)
                                                throw new ArgumentNullException(nameof(query), $"The {nameof(query)} argument is required.");

                                            if (!hasLimit)
                                                throw new ArgumentNullException(nameof(limit), $"The {nameof(limit)} argument is required.");

                                            List<string> toolResult = await SearchProductsVectorized(
                                                query.GetString(),
                                                limit.GetUInt64(),
                                                hasPriceLowerLimit ? priceLowerLimit.GetInt32() : null,
                                                hasPriceUpperLimit ? priceUpperLimit.GetInt32() : null
                                            );

                                            chat.Add(new ToolChatMessage(toolCall.Id, string.Join(", ", toolResult)));
                                            break;
                                        }

                                    case nameof(SearchProductsById):
                                        {
                                            using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                            bool hasIds = argumentsJson.RootElement.TryGetProperty("productIds", out JsonElement productIds);

                                            if (!hasIds)
                                                throw new ArgumentNullException(nameof(productIds), $"The {nameof(productIds)} argument is required.");

                                            string toolResult = await GetMarkdownFormattedProducts(productIds.EnumerateArray()
                                                .Select(x => x.GetString())
                                                .ToList()
                                            );

                                            chat.Add(new ToolChatMessage(toolCall.Id, toolResult));
                                            break;
                                        }

                                    default:
                                        {
                                            throw new NotImplementedException();
                                        }
                                }
                            }

                            requiresAction = true;
                            break;
                        }

                    default:
                        throw new NotImplementedException(completion.FinishReason.ToString());
                }
            } while (requiresAction);

            ChatCompletion completionResult = await _openAIChatClient.CompleteChatAsync(chat);
            return completionResult.Content[0].Text;
        }

        #region Helpers

        #region Tools

        private async Task<List<string>> SearchProductsVectorized(
            string query,
            ulong limit,
            int? priceLowerLimit = null,
            int? priceUpperLimit = null
        )
        {
            ReadOnlyMemory<float> embedding = await GetEmbeddingAsync(query);

            Filter filter = null;

            if (priceLowerLimit.HasValue || priceUpperLimit.HasValue)
            {
                var range = new Qdrant.Client.Grpc.Range();

                if (priceLowerLimit.HasValue)
                    range.Gte = priceLowerLimit.Value;

                if (priceUpperLimit.HasValue)
                    range.Lte = priceUpperLimit.Value;

                filter = new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "price",
                                Range = range
                            }
                        }
                    }
                };
            }

            IReadOnlyList<ScoredPoint> searchResult = await _qdrantClient.SearchAsync(
                collectionName: SettingsProvider.Current.QdrantProductsTableName,
                vector: embedding,
                filter: filter,
                limit: limit
            );

            return searchResult.Select(r => r.Id.ToString()).ToList();
        }

        private static readonly ChatTool searchProductsVectorizedTool = ChatTool.CreateFunctionTool(
            functionName: nameof(SearchProductsVectorized),
            functionDescription: "Pretrazi i dohvati top {limit} proizvoda, tj. njihove id-jeve, iz vektorske baze podataka, po opisu koji je korisnik prosledio i koji je LLM potencijalno dodatno obradio.",
            functionParameters: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "query": {
                        "type": "string",
                        "description": "Pretraživački upit koji opisuje proizvode koje korisnik traži."
                    },
                    "limit": {
                        "type": "integer",
                        "minimum": 1,
                        "maximum": 10,
                        "description": "Maksimalan broj proizvoda za prikaz (1-10). Koristi manji broj (1-5) za specifične upite, veći (5-10) za opšte kategorije."
                    },
                    "priceLowerLimit": {
                        "type": "integer",
                        "description": "Minimalna cena u dinarima (opciono)"
                    },
                    "priceUpperLimit": {
                        "type": "integer",
                        "description": "Maksimalna cena u dinarima (opciono)"
                    }
                },
                "required": [ "query", "limit" ]
            }
            """u8.ToArray())
        );

        private async Task<List<ExternalProductDTO>> SearchProductsById(List<string> productIds)
        {
            List<ExternalProductDTO> products = new();

            foreach (string productId in productIds)
            {
                ExternalProductDTO product = await SearchProductById(productId);
                products.Add(product);
            }

            return products;
        }

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

        private static readonly ChatTool searchProductsByIdTool = ChatTool.CreateFunctionTool(
            functionName: nameof(SearchProductsById),
            functionDescription: "Pretraži i dohvati proizvode po njegovom Id-ju, ovaj tool treba da se koristi samo ako nakon što smo pretragom vektorske baze podataka dobili listu id-jeva.",
            functionParameters: BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "productIds": {
                        "type": "array",
                        "items": {
                            "type": "string"
                        },
                        "description": "Lista ID-jeva proizvoda dobijenih pretragom vektorske baze podataka."
                    }
                },
                "required": [ "productIds" ]
            }
            """u8.ToArray())
        );

        #endregion

        private List<ChatMessage> PrepareChat(List<ChatMessage> chatHistory, string userPrompt)
        {
            List<ChatMessage> chat = new();

            if (chatHistory == null || !chatHistory.Any(m => m is SystemChatMessage))
            {
                chat.Add(new SystemChatMessage(_systemPromptTemplate));
            }

            if (chatHistory?.Count > 0)
            {
                List<ChatMessage> relevantHistory = chatHistory
                    .Where(m => !(m is SystemChatMessage))
                    .ToList();

                if (relevantHistory.Count > 20)
                {
                    _logger.LogInformation("Skraćivanje istorije sa {Count} na 20 poruka", relevantHistory.Count);
                    relevantHistory = relevantHistory.Skip(relevantHistory.Count - 20).ToList();
                }

                chat.AddRange(relevantHistory);
            }

            chat.Add(new UserChatMessage(userPrompt));
            return chat;
        }

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
            {
                await _qdrantClient.CreateCollectionAsync(SettingsProvider.Current.QdrantProductsTableName, new VectorParams { Size = 3072, Distance = Distance.Cosine });

                await _qdrantClient.CreatePayloadIndexAsync(
                    collectionName: SettingsProvider.Current.QdrantProductsTableName,
                    fieldName: "price",
                    schemaType: PayloadSchemaType.Integer
                );
            }
            int batchSize = 100;
            for (int i = 0; i < products.Count; i += batchSize)
            {
                if (i < 3100)
                    continue;
                List<QdrantProductDTO> batch = products.Skip(i).Take(batchSize).ToList();
                List<PointStruct> points = new();

                foreach (QdrantProductDTO product in batch)
                {
                    if (string.IsNullOrEmpty(product.Text))
                        continue;

                    ReadOnlyMemory<float> embedding = await GetEmbeddingAsync(product.Text);

                    if (embedding.IsEmpty)
                        continue;

                    points.Add(new PointStruct
                    {
                        Id = ulong.Parse(product.Id),
                        Vectors = embedding.ToArray(),
                        Payload = { ["price"] = product.Price }
                    });
                }

                await _qdrantClient.UpsertAsync(SettingsProvider.Current.QdrantProductsTableName, points);

                _logger.LogInformation($"Uploaded {i + batch.Count} / {products.Count}");
            }
        }

        private async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ReadOnlyMemory<float>.Empty;

            List<string> chunks = LlmHtmlProcessor.HtmlToLlmMarkdown(text);

            if (chunks.Count == 0)
                return ReadOnlyMemory<float>.Empty;

            List<ReadOnlyMemory<float>> vectors = new();

            foreach (string chunk in chunks)
            {
                OpenAIEmbedding embedding = await _openAIEmbeddingClient.GenerateEmbeddingAsync(input: chunk);
                vectors.Add(embedding.ToFloats());
            }

            // Average vectors
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

        private async Task<string> GetMarkdownFormattedProducts(List<string> ids)
        {
            List<string> result = new();

            foreach (string id in ids)
            {
                ExternalProductDTO externalProductDTO = await SearchProductById(id);
                result.Add(GetMarkdownFormattedProduct(externalProductDTO));
            }

            return string.Join(", ", result);
        }

        private string GetMarkdownFormattedProduct(ExternalProductDTO productDTO)
        {
            return $$"""
({{productDTO.url}})[{{productDTO.title}}]
""";
        }

        #endregion

        #endregion

    }
}
