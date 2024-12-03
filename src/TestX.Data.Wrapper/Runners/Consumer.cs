using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf.Canvas.Parser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TestX.Core.Enums;
using TestX.Data.Contexts;
using PdfDocument = iText.Kernel.Pdf.PdfDocument;
using PdfReader = iText.Kernel.Pdf.PdfReader;

namespace TestX.Data.Wrapper.Runners
{
    public class Consumer
    {

        private readonly DataBaseContext _dataBaseContext;

        public Consumer(DataBaseContext dataBaseContext)
        {
            _dataBaseContext = dataBaseContext;
        }

        public async Task Start()
        {
            try
            {
                // Setup RabbitMQ connection
                var factory = new ConnectionFactory()
                {
                    HostName = "194.135.36.43",
                    Port = 5672,
                    UserName = "admin", // Change as necessary
                    Password = "9a55f70a841f18b97c3a7db939b7adc9e34a0f1d",
                    VirtualHost = "/",
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(30)
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                // Declare the queue
                channel.QueueDeclare(queue: "mailcreated",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                // Create a consumer to listen for messages
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += async (model, ea) =>
                {
                    // Get the message body
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // Deserialize the message to the appropriate type
                    var mailMessage = JsonConvert.DeserializeObject<JObject>(message);
                    var data = mailMessage["data"]?.ToString();
                    var uid = mailMessage["uid"]?.ToString();
                    // Process the message
                    var response = ProcessMessage(data, uid);

                    if (response)
                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };

                channel.BasicConsume(queue: "mailcreated", autoAck: false, consumer: consumer);

                Console.WriteLine(" [*] Waiting for messages. Press [enter] to exit.");
                Console.ReadLine(); 
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private bool ProcessMessage(string data, string uid)
        {
            byte[] pdfBytes = Convert.FromBase64String(data);
            if (pdfBytes is null)
            {
                Console.WriteLine("Pdf is null");
                return false;
            }
            
            using (var stream = new MemoryStream(pdfBytes))
            {
                using (PdfReader pdfReader = new PdfReader(stream))
                using (PdfDocument pdfDoc = new PdfDocument(pdfReader))
                {
                    StringBuilder textBuilder = new StringBuilder();

                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var page = pdfDoc.GetPage(i);
                        var text = PdfTextExtractor.GetTextFromPage(page);
                        textBuilder.Append(text);
                    }

                    string extractedText = textBuilder.ToString();
                    string urlPattern = @"(https:\/\/cloud\.yhxx\.uz\/[a-zA-Z0-9-]{36}|https:\/\/video\.yhxx\.uz\/r\/[0-9]+|https:\/\/slink\.ksubdd\.uz\/[a-zA-Z0-9]+)";
                    
                    var matches = Regex.Matches(extractedText.Replace(" ", "").Replace("\n", ""), urlPattern);
                    foreach (Match match in matches)
                    {
                        bool result = false;

                        if (match.Value.StartsWith("https://cloud.yhxx.uz/"))
                        {
                            _dataBaseContext.Datas.Add(new Entities.Data()
                            {
                                Link = match.Value,
                                Uid = uid,
                                UrlType = UrlType.Cloud
                            });
                            _dataBaseContext.SaveChanges();
                            result = true;
                        }
                        else if (match.Value.StartsWith("https://slink.ksubdd.uz/"))
                        {
                            _dataBaseContext.Datas.Add(new Entities.Data()
                            {
                                Link = match.Value,
                                Uid = uid,
                                UrlType = UrlType.Slink
                            });
                            _dataBaseContext.SaveChanges();
                            result = true;
                        }
                        else if (match.Value.StartsWith("https://video.yhxx.uz"))
                        {
                            _dataBaseContext.Datas.Add(new Entities.Data()
                            {
                                Link = match.Value,
                                Uid = uid,
                                UrlType = UrlType.Video
                            });
                            _dataBaseContext.SaveChanges();
                            result = true;
                        }
                        
                        Console.WriteLine(uid + "\t" + result);

                        return result;
                    }
                }
            }

            return false;
        }
    }
}
