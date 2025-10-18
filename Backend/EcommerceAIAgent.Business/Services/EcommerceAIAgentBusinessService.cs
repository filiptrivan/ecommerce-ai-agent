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
using EcommerceAIAgent.Business.ExternalDTO;

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

        private readonly string _externalApiBaseURL = "https://api.readycms.io";
        private readonly string _externalApiNamespace = "prodavnicaalata";

        private const string _systemPromptTemplate = $$"""
Ti si profesionalni web shop prodavac na sajtu www.prodavnicaalata.rs.
Kupci se uživo dopisuju sa tobom, moraš brzo da odgovaraš.
Prodaješ mašine, alate, usisivace, elektricni, akumulatorski alat, kosilice, brusilice, pribor...
Imaš dva maloprodajna objekta:
- Vojislava Ilića 141g
- Na Altini - Ugrinovačka 212

Važna pravila:
- Odgovaraj direktno i profesionalno, kad god mozes se potrudi da ne postavljas dodatna pitanja
- Krajnji odgovor korisniku lepo upakuj u markdown
- Samo ako korisnik nije precizan postavi dodatno pitanje
- Ako korisnik pita za nešto čime si sto posto siguran da se ne bavimo i nemamo u asortimanu, mu se izvini i reci da se ne bavimo sa tim npr. ako te neko pita za tastature/sminku/haljine očigledno je da se ne bavimo time i možeš odmah da mu odgovoriš, ali npr. ako pita za nešto što nisi siguran, prvo proveri pretraživanjem putem {{nameof(SearchProductsVectorized)}} tool-a.
- Ne spominji tehničke detalje (vektorska baza, ID-jeve, itd.)
- Ako korisnik pita neko opšte pitanje o proizvodu iskoristi svoj osnovni model za odgovor.
- Kad korisniku vracas neki proizvod uvek navedi barem ime, a kad je potrebno i ostale detalje proizvoda
- Ako ti korisnik trazi da uporedis neke proizvode poredjenje prikazi u tabelarnom prikazu
- Ako proizvod ima sniženu cenu ({sale_price}), prikaži samo tu cenu. Cenu bez popusta prikaži samo ako korisnik to izričito zatraži (npr. pita za „punu cenu” ili „cenu pre popusta”).

Primeri: 
Primer 1:
Korisnik: Tražim štapni usisivač po povoljnoj ceni
Asistent: Da li želite bežični ili električni?
Korisnik: Bežični
Asistent: Najpovoljniji bežični štapni usisivač je: (https://www.prodavnicaalata.rs/proizvodi/einhell-te-sv-18-li-solo-akumulatorski-stapni-usisivac-bez-baterije-i-punjaca/)[Einhell TE-SV 18 Li-Solo Akumulatorski štapni usisivač, bez baterije i punjača - 12.320 RSD].
Korisnik: Daj još neki
Asistent: Naravno, evo još povoljnih proizvoda u našoj ponudi:
1. (https://www.prodavnicaalata.rs/proizvodi/makita-dcl284fz-akumulatorski-stapni-usisivac-18-v-lxt-bez-baterije-i-punjaca/)[Makita DCL284FZ akumulatorski štapni usisivač, 18 V LXT, bez baterije i punjača - 19.170 RSD]
2. (https://www.prodavnicaalata.rs/proizvodi/karcher-1198-7300-stapni-usisivac-450w/)[Kärcher 1.198-730.0 štapni usisivač, 450W - 21.849 RSD]
3. (https://www.prodavnicaalata.rs/proizvodi/karcher-1198-6300-bezicni-stapni-usisivac-sa-posudom-650ml/)[Kärcher 1.198-630.0 bežični štapni usisivač sa posudom, 650ml - 22.799 RSD]

Primer 2:
Korisnik: Koje su vam najpovoljnije wc šolje?
Sistem: Pretražuje wc šolje u vektorskoj bazi podataka, i vidi da je rezultat prazan.
Asistent: Nažalost mi ne prodajemo WC šolje, ako ste zainteresovani za nešto drugo poput alata, mašina ili pribora mogu da vam pomognem :)

Primer 3:
Korisnik: Za šta se koristi hilti bušilica?
Sistem: Shvata da je ovo opšte pitanje i koristi svoj osnovni model da bi vratio odgovor.
Asistent: Hilti bušilice se koriste za bušenje čvrstih materijala poput betona, cigle i kamena, a zahvaljujući vibracionoj i udarnoj funkciji, mogu i da razbijaju materijal. Većina ljudi kad kaže "hilti" — misli na bilo koju udarnu bušilicu ili štemericu, bez obzira na marku.
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
            _openAIChatClient = openAIClient.GetChatClient("gpt-4.1-nano");
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

        public async Task<string> SendMessage(MessageDTO messageDTO)
        {
            List<ChatMessage> chat = PrepareChat(messageDTO.ChatHistory, messageDTO.Content);
            ChatCompletionOptions options = new()
            {
                Tools =
                {
                    searchProductsVectorizedTool,
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
                                            bool hasShouldSortAscendingByPrice = argumentsJson.RootElement.TryGetProperty("shouldSortAscendingByPrice", out JsonElement shouldSortAscendingByPrice);
                                            bool hasPriceLowerLimit = argumentsJson.RootElement.TryGetProperty("priceLowerLimit", out JsonElement priceLowerLimit);
                                            bool hasPriceUpperLimit = argumentsJson.RootElement.TryGetProperty("priceUpperLimit", out JsonElement priceUpperLimit);

                                            if (!hasQuery)
                                                throw new ArgumentNullException(nameof(query), $"The {nameof(query)} argument is required.");

                                            if (!hasLimit)
                                                throw new ArgumentNullException(nameof(limit), $"The {nameof(limit)} argument is required.");

                                            if (!hasShouldSortAscendingByPrice)
                                                throw new ArgumentNullException(nameof(shouldSortAscendingByPrice), $"The {nameof(shouldSortAscendingByPrice)} argument is required.");

                                            string toolResult = await SearchProductsVectorized(
                                                query.GetString(),
                                                limit.GetUInt64(),
                                                shouldSortAscendingByPrice.GetBoolean(),
                                                hasPriceLowerLimit ? priceLowerLimit.GetInt32() : null,
                                                hasPriceUpperLimit ? priceUpperLimit.GetInt32() : null
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

        private async Task<string> SearchProductsVectorized(
            string query,
            ulong limit,
            bool shouldSortAscendingByPrice,
            int? priceLowerLimit = null,
            int? priceUpperLimit = null
        )
        {
            ReadOnlyMemory<float> embedding = await GetEmbeddingAsync(query);

            Filter filter = BuildPriceFilter(priceLowerLimit, priceUpperLimit);

            IReadOnlyList<ScoredPoint> scoredPoints = await _qdrantClient.SearchAsync(
                collectionName: SettingsProvider.Current.QdrantProductsTableName,
                vector: embedding,
                filter: filter,
                limit: limit,
                scoreThreshold: query.Length > 20 ? 0.5f : 0.4f
            );

            List<ExternalProductDTO> externalProductDTOList = await GetProducts(
                scoredPoints.Select(x => x.Id.Num.ToString()).ToList()
            );

            if (shouldSortAscendingByPrice)
            {
                externalProductDTOList = externalProductDTOList
                    .OrderBy(x => x.sale_price ?? x.price)
                    .ToList();
            }

            List<object> results = MapProductsToSearchResults(externalProductDTOList, scoredPoints);

            return JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private static readonly ChatTool searchProductsVectorizedTool = ChatTool.CreateFunctionTool(
            functionName: nameof(SearchProductsVectorized),
            functionDescription: """
Koristi ovaj tool kada korisnik postavi pitanje ili upit vezan za naše proizvode.
Tool pretražuje vektorsku bazu podataka koja sadrži detaljne opise proizvoda sa našeg sajta (u Markdown formatu), pa pre poziva treba da prilagodiš {query}.
Vraća do {limit} proizvoda, sortirane po sličnosti sa korisnikovim upitom (od najrelevantnijeg do najmanje relevantnog).
Ako nema rezultata, jednostavno obavesti korisnika, bez ponovnog pokušaja pretrage.
""",
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
                        "maximum": 15,
                        "description": "Maksimalan broj proizvoda za pretragu (1-15). Koristi manji broj (1-10) za specifične upite, veći (10-15) za opšte kategorije."
                    },
                    "shouldSortAscendingByPrice": {
                        "type": "boolean",
                        "description": "Ako je true, proizvodi se sortiraju po ceni od najniže do najviše. Po defaultu (false) proizvodi se sortiraju po sličnosti."
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
                "required": [ "query", "limit", "shouldSortAscendingByPrice" ]
            }
            """u8.ToArray())
        );

        #endregion

        private List<ChatMessage> PrepareChat(List<MessageDTO> chatHistory, string userPrompt)
        {
            List<ChatMessage> chat = [new SystemChatMessage(_systemPromptTemplate)];

            if (chatHistory?.Count > 0)
            {
                if (chatHistory.Count > 20)
                {
                    _logger.LogInformation("Skraćivanje istorije sa {Count} na 20 poruka", chatHistory.Count);
                    chatHistory = chatHistory.Skip(chatHistory.Count - 20).ToList();
                }

                foreach (MessageDTO messageDTO in chatHistory)
                {
                    if (messageDTO.Role == MessageRoleCodes.Agent)
                        chat.Add(new AssistantChatMessage(messageDTO.Content));
                    else
                        chat.Add(new UserChatMessage(messageDTO.Content));
                }
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

        private Filter BuildPriceFilter(int? priceLowerLimit, int? priceUpperLimit)
        {
            if (!priceLowerLimit.HasValue && !priceUpperLimit.HasValue)
                return null;

            Qdrant.Client.Grpc.Range range = new();

            if (priceLowerLimit.HasValue)
                range.Gte = priceLowerLimit.Value;

            if (priceUpperLimit.HasValue)
                range.Lte = priceUpperLimit.Value;

            return new Filter
            {
                Must = {
                    new Condition {
                        Field = new FieldCondition {
                            Key = "price", Range = range
                        }
                    }
                }
            };
        }

        private async Task<List<ExternalProductDTO>> GetProducts(List<string> productIds)
        {
            IEnumerable<Task<ExternalProductDTO>> tasks = productIds.Select(GetProductById);

            ExternalProductDTO[] products = await Task.WhenAll(tasks);

            return products.Where(p => p != null).ToList();
        }

        private async Task<ExternalProductDTO> GetProductById(string productId)
        {
            string url = $"{_externalApiBaseURL}/GET/products/" +
                $"?namespace={_externalApiNamespace}" +
                $"&id={productId}" +
                $"&status=Published" +
                $"&hide_categories=true" +
                $"&hide_seo=true" +
                $"&hide_tags=true" +
                $"&hide_manufacturer=true" +
                $"&hide_items=true" +
                $"&hide_attributes=true" +
                $"&hide_locations=true" +
                $"&hide_variations=true";

            // Configure headers
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SettingsProvider.Current.ExternalApiBearerToken);

            // Execute request
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            ExternalProductsResponseDTO externalProductsResponseDTO = JsonSerializer.Deserialize<ExternalProductsResponseDTO>(json);

            if (externalProductsResponseDTO == null)
                return null;

            if (externalProductsResponseDTO.data == null)
                return null;

            if (
                externalProductsResponseDTO.data.products == null ||
                externalProductsResponseDTO.data.products.Count == 0
            )
            {
                return null;
            }

            return externalProductsResponseDTO.data.products[0];
        }

        private List<object> MapProductsToSearchResults(
            List<ExternalProductDTO> externalProductDTOList,
            IReadOnlyList<ScoredPoint> scoredPoints
        )
        {
            List<object> results = new();

            for (int i = 0; i < externalProductDTOList.Count; i++)
            {
                ExternalProductDTO externalProductDTO = externalProductDTOList[i];

                ScoredPoint searchResult = scoredPoints.Where(x => x.Id.Num == (ulong)externalProductDTO.id).Single();

                results.Add(new
                {
                    index = i + 1,
                    productSimilarityScore = searchResult.Score,
                    productName = externalProductDTO.title,
                    productPrice = externalProductDTO.price,
                    saleProductPrice = externalProductDTO.sale_price,
                    productUrl = externalProductDTO.url,
                    productStock = externalProductDTO.stock,
                });
            }

            return results;
        }

        #endregion

        #endregion

    }
}
