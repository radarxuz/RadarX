using TestX.Data.Contexts;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestX.Core.Enums;
using TestX.Data.Wrapper.Helpers;

namespace TestX.Data.Wrapper.Runners
{
    public class Receiver
    {
        private static readonly HttpClient client = new HttpClient();
        private const string ApiBaseEndpoint = "http://192.168.20.43/Api/ImportGaiDocument?day=";
        private const string TokenEndpoint = "http://192.168.20.43/token";
        private const string ClientId = "7ea1279dceef49d68054827a0bca2c409998a1aa2d29426494";
        private const string ClientSecret = "8827a81ba0bd43d3b3cee989b5d82ce9ce077fc7925247d58eee80f3ebfab851e75b4abffd594c098f4a8729cd4bce5c20c8";
        private string _token;
        private static Timer _dailyTimer;
        
        private string GetApiEndpointForToday()
        {
            string yesterday = DateTime.Now.AddDays(-1).ToString("dd.MM.yyyy");
            return ApiBaseEndpoint + yesterday;
        }

        public async Task InitializeDailyTask()
        {
            await Start();
            Console.WriteLine(DateTime.Now + "\tDaily task completed.");

            while (true)
            {
                try
                {
                    var now = DateTime.Now;
                    var midnight = DateTime.Today.AddDays(1);
                    var timeUntilMidnight = (midnight - now);

                    Console.WriteLine(DateTime.Now + $"\tTime until to start: {timeUntilMidnight}");
                    Thread.Sleep(timeUntilMidnight);
                    await Start();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public async Task Start()
        {
            try
            {
                _token = await GetTokenAsync();
                var uids = await GetUidsFromApiAsync();
                Console.WriteLine(DateTime.Now + $"\tUids count : {uids.Count}");
                foreach (var uid in uids)
                {
                    await ProcessMessage(uid);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
        static string RunCommand(string command)
        {
            var process = new ProcessStartInfo("cmd", $"/c {command}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var cmd = Process.Start(process);
            var reader = cmd.StandardOutput;
            return reader.ReadToEnd();
        }


        // Retrieve authentication token
        private async Task<string> GetTokenAsync()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret)
            });

            var response = await client.PostAsync(TokenEndpoint, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenObj = JObject.Parse(responseContent);
            return tokenObj["access_token"].ToString();
        }

        // Retrieve UIDs from the API
        private async Task<List<string>> GetUidsFromApiAsync()
        {
            while (true)
            {
                try
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                    var apiUrl = GetApiEndpointForToday();

                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        Thread.Sleep(TimeSpan.FromHours(1));
                        continue;
                    }

                    var uids = await response.Content.ReadAsStringAsync();
                    return JArray.Parse(uids).Select(uid => uid.ToString()).ToList();
                }
                catch (Exception e)
                {
                    // ignored
                }
            }
        }
        
        private async Task<List<string>> GetUidsFromDatabaseAsync()
        {
            string connectionString = "Server=DB-02-REPORTS;Database=HybridMail;User Id=sa;Password=IevzS9ylSeofkJOdrqg3;Encrypt=True;TrustServerCertificate=True;";
    
            // Define the start and end of yesterday
            var start = DateTime.Today.AddDays(-1);
            var end = DateTime.Today;
    
            var uids = new List<string?>();
    
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Use a parameterized query with @start and @end
                using (var command = new SqlCommand("SELECT Uid FROM Mail WHERE CreatedOn >= @start AND CreatedOn < @end AND ClientType = 1", connection))
                {
                    // Add parameters for start and end
                    command.Parameters.AddWithValue("@start", start);
                    command.Parameters.AddWithValue("@end", end);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Load data directly into a DataTable
                        var dataTable = new DataTable();
                        dataTable.Load(reader);

                        // Convert DataTable rows to a list of strings
                        uids = dataTable.AsEnumerable()
                            .Select(row => row["Uid"].ToString())
                            .ToList();
                    }
                }
            }

            return uids;
        }


        // Process each UID
        // Optimized ProcessMessage with Scoped Context and Batching
        private async Task<bool> ProcessMessage(string uid)
        {
            try
            {
                var pdfBytes = await GetPdfAsByteArrayAsync(uid);

                using (var stream = new MemoryStream(pdfBytes))
                using (PdfReader pdfReader = new PdfReader(stream))
                using (PdfDocument pdfDoc = new PdfDocument(pdfReader))
                {
                    StringBuilder textBuilder = new StringBuilder();

                    // Extract text from each page
                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var page = pdfDoc.GetPage(i);
                        var text = PdfTextExtractor.GetTextFromPage(page);
                        textBuilder.Append(text);
                    }

                    string extractedText = textBuilder.ToString();
                    string urlPattern = @"(https:\/\/cloud\.yhxx\.uz\/[a-zA-Z0-9-]{36}|https:\/\/video\.yhxx\.uz\/r\/[0-9]+|https:\/\/slink\.ksubdd\.uz\/[a-zA-Z0-9]+)";
                    var matches = Regex.Matches(extractedText.Replace(" ", "").Replace("\n", ""), urlPattern);

                    Entities.Data data = null;

                    foreach (Match match in matches)
                    {
                        if (match.Value.StartsWith("https://cloud.yhxx.uz/"))
                        {
                            data = new Entities.Data() { Link = match.Value, Uid = uid, UrlType = UrlType.Cloud };
                        }
                        else if (match.Value.StartsWith("https://slink.ksubdd.uz/"))
                        {
                            data = new Entities.Data() { Link = match.Value, Uid = uid, UrlType = UrlType.Slink };
                        }
                        else if (match.Value.StartsWith("https://video.yhxx.uz"))
                        {
                            data = new Entities.Data() { Link = match.Value, Uid = uid, UrlType = UrlType.Video };
                        }
                    }

                    if (data is not null)
                    {
                        int counter = 0;
                        Exception exception = null;
                        while (counter < 10)
                        {
                            try
                            {
                                var optionsBuilder = new DbContextOptionsBuilder<DataBaseContext>();
                                optionsBuilder.UseSqlServer("data source=(local);initial catalog=DataX;persist security info=True;user id=sa;password=eso8Yv0#;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True");
                                using (var context = new DataBaseContext(optionsBuilder.Options))
                                {
                                    context.Datas.Add(data);
                                    await context.SaveChangesAsync();
                                }
                                break;
                            }
                            catch (Exception e)
                            {
                                exception = e;
                                counter++;
                                Thread.Sleep(TimeSpan.FromMinutes(1));
                            }
                        }

                        if (counter == 10)
                            throw exception;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                var data = new Entities.Data() { Link = "", Uid = uid, UrlType = UrlType.Video };
                var optionsBuilder = new DbContextOptionsBuilder<DataBaseContext>();
                optionsBuilder.UseSqlServer("data source=(local);initial catalog=DataX;persist security info=True;user id=sa;password=eso8Yv0#;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True");
                using (var context = new DataBaseContext(optionsBuilder.Options))
                {
                    context.Datas.Add(data);
                    await context.SaveChangesAsync();
                }

                return true;
            }
        }


        public async Task<byte[]?> GetPdfAsByteArrayAsync(string uid)
        {
            string url = $"http://192.168.20.43/api/MailPdf/{uid}";

            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    byte[] pdfData = await response.Content.ReadAsByteArrayAsync();
                    return pdfData;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving PDF for UID: {uid}. Exception: {ex.Message}");
            }

            return null;
        }
    }
}
