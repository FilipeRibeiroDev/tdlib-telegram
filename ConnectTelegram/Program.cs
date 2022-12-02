using ConnectTelegram;

int ApiId = 0;
string ApiHash = "";
string PhoneNumber = "";

var telegram = new Telegram(ApiId, ApiHash, PhoneNumber);
await telegram.Connect();

var contact = await telegram.CreateContact("+55");

await telegram.SendMessage(contact.Id, "Hello World!");

Console.ReadLine();

var channels = telegram.GetChannels(5);

await foreach (var channel in channels)
{
    await telegram.SendMessage(channel.Id, "Hello World!");
}