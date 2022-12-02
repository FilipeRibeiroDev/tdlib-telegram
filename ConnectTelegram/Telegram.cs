using TdLib;
using TdLib.Bindings;

namespace ConnectTelegram
{
    public class Telegram
    {
        public int ApiId { get; private set; }
        public string ApiHash { get; private set; }
        public string PhoneNumber { get; private set; }
        
        private const string ApplicationVersion = "1.0.0";
        private TdClient _client;
        private readonly ManualResetEventSlim ReadyToAuthenticate = new();

        private bool _authNeeded;
        private bool _passwordNeeded;

        public Telegram(int apiId, string apiHash, string phoneNumber)
        {
            ApiId = apiId;
            ApiHash = apiHash;
            PhoneNumber = phoneNumber;
        }

        public async Task Connect()
        {
            _client = new TdClient();
            _client.Bindings.SetLogVerbosityLevel(TdLogLevel.Fatal);

            _client.UpdateReceived += async (_, update) => { await ProcessUpdates(update); };

            ReadyToAuthenticate.Wait();

            if (_authNeeded) await HandleAuthentication();
        }

        private async Task HandleAuthentication()
        {
            // Setting phone number
            await _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber
            {
                PhoneNumber = PhoneNumber
            });

            // Telegram servers will send code to us
            Console.Write("Insert the login code: ");
            var code = Console.ReadLine();

            await _client.ExecuteAsync(new TdApi.CheckAuthenticationCode
            {
                Code = code
            });

            if (!_passwordNeeded) { return; }

            // 2FA may be enabled. Cloud password is required in that case.
            Console.Write("Insert the password: ");
            var password = Console.ReadLine();

            await _client.ExecuteAsync(new TdApi.CheckAuthenticationPassword
            {
                Password = password
            });
        }

        private async Task ProcessUpdates(TdApi.Update update)
        {

            switch (update)
            {
                case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters }:
                     var filesLocation = Path.Combine(AppContext.BaseDirectory, "db");
                    await _client.ExecuteAsync(new TdApi.SetTdlibParameters
                    {
                        Parameters = new TdApi.TdlibParameters
                        {
                            ApiId = ApiId,
                            ApiHash = ApiHash,
                            DeviceModel = "PC",
                            SystemLanguageCode = "en",
                            ApplicationVersion = ApplicationVersion,
                            DatabaseDirectory = filesLocation,
                            FilesDirectory = filesLocation,
                        }
                    });
                    break;

                case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitEncryptionKey }:
                    await _client.ExecuteAsync(new TdApi.CheckDatabaseEncryptionKey());
                    break;

                case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber }:
                case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitCode }:
                    _authNeeded = true;
                    ReadyToAuthenticate.Set();
                    break;

                case TdApi.Update.UpdateAuthorizationState { AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPassword }:
                    _authNeeded = true;
                    _passwordNeeded = true;
                    ReadyToAuthenticate.Set();
                    break;

                case TdApi.Update.UpdateUser:
                    ReadyToAuthenticate.Set();
                    break;

                case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateReady }:
                    break;

                case TdApi.Update.UpdateNewMessage:
                    await ListingMessage(update);
                    break;

                default:
                    break;
            }
        }

        public async Task SendMessage(long chatId, string message)
        {
            var content = new TdApi.InputMessageContent.InputMessageText
            {
                Text = new TdApi.FormattedText
                {
                    Text = message
                }
            };

            await _client.ExecuteAsync(new TdApi.SendMessage
            {
                ChatId = chatId,
                InputMessageContent = content,
            });
        }

        public async Task<TdApi.Chat> CreateContact(string number)
        {
            var contact = new TdApi.Contact[]
                {
                    new TdApi.Contact
                    {
                        PhoneNumber = number,
                    }
                };

            var contacts = await _client.ExecuteAsync(new TdApi.ImportContacts
            {
                Contacts = contact
            });

            var chat = await _client.ExecuteAsync(new TdApi.CreatePrivateChat
            {
                UserId = contacts.UserIds[0],
            });

            return chat;
        }

        public async Task ListingMessage(TdApi.Update update)
        {
          
        }

        public async Task<TdApi.User> GetCurrentUser()
        {
            return await _client.ExecuteAsync(new TdApi.GetMe());
        }

        public async IAsyncEnumerable<TdApi.Chat> GetChannels(int limit)
        {
            var chats = await _client.ExecuteAsync(new TdApi.GetChats
            {
                Limit = limit
            });

            foreach (var chatId in chats.ChatIds)
            {
                var chat = await _client.ExecuteAsync(new TdApi.GetChat
                {
                    ChatId = chatId
                });

                if (chat.Type is TdApi.ChatType.ChatTypeSupergroup or TdApi.ChatType.ChatTypeBasicGroup or TdApi.ChatType.ChatTypePrivate)
                {
                    yield return chat;
                }
            }
        }
    }
}
