using System.Text;
using Newtonsoft.Json;
using TestX.Data.Entities;
using TestX.Data.IRepositories;

namespace TestX.Data.Wrapper.Helpers;

public class TelegramBot
{
    private static readonly HttpClient httpClient = new HttpClient();

    private const string TelegramBotToken = "7860646546:AAEgZjIOU3Zjl0iQj7FKUpAKfJdsCUg_yZY";
    private const string TelegramChatId = "1052097431";

    // public async Task SendCamerasDataToTelegramAsync()
    // {
    //     // Step 1: Retrieve data
    //     var cameras = await RetrieveAsync();
    //
    //     // Step 2: Convert data to JSON
    //     string jsonData = JsonConvert.SerializeObject(cameras, Formatting.Indented);
    //
    //     // Step 3: Send data to Telegram as a text message
    //     await SendMessageToTelegramAsync(jsonData);
    // }

    public async Task SendMessageToTelegramAsync(string message)
    {
        var telegramUrl = $"https://api.telegram.org/bot{TelegramBotToken}/sendMessage";

        // Prepare content
        var content = new StringContent(JsonConvert.SerializeObject(new
        {
            chat_id = TelegramChatId,
            text = message,
            parse_mode = "Markdown"
        }), Encoding.UTF8, "application/json");

        // Send the message
        var response = await httpClient.PostAsync(telegramUrl, content);
        response.EnsureSuccessStatusCode();
        Console.WriteLine("Message sent to Telegram successfully.");
    }
}