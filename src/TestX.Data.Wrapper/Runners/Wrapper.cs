using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using AForge.Imaging.Filters;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using MetadataExtractor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using TestX.Data.Contexts;
using TestX.Data.Entities;
using Tesseract;
using TestX.Core.Enums;
using TestX.Data.Wrapper.Helpers;

namespace TestX.Data.Wrapper.Runners;     

public class Wrapper
{
    private readonly string _tessDataPath = "E:\\datax\\WRAPPER\\TessData";
    private static readonly HttpClient httpClient = new HttpClient();
    public async Task Start()
    {
        while (true)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var optionsBuilder = new DbContextOptionsBuilder<DataBaseContext>();
                optionsBuilder.UseSqlServer("data source=(local);initial catalog=DataX;persist security info=True;user id=sa;password=eso8Yv0#;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True");

                using (var context = new DataBaseContext(optionsBuilder.Options))
                {
                    var allData = await context.Datas
                        .Where(d => !d.IsCompleted)
                        .OrderByDescending(d => d.CreatedOn)
                        .Take(1000)
                        .ToListAsync();

                    var batchSize = 100;
                    var batches = allData
                        .Select((data, index) => new { data, index })
                        .GroupBy(x => x.index / batchSize)
                        .Select(group => group.Select(x => x.data).ToList())
                        .ToList();

                    var tasks = batches
                        .Select(batch => Task.Run(() => ProcessUidsInThread(batch, context)))
                        .ToList();

                    await Task.WhenAll(tasks);
                }

                stopwatch.Stop();
                Console.WriteLine($"{DateTime.UtcNow}\t Processed 1000 items in {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occurred: {e.Message}");
                throw;
            }
        }
    }
        
    private async Task ProcessUidsInThread(List<Entities.Data> datas, DataBaseContext context)
    {
        int falseResultCount = 0;
        int sleepCount = 0;
        
        Console.WriteLine($"Processing {datas.Count} items in thread.");
        
        foreach (var data in datas)
        {
            if (falseResultCount > 10)
            {
                sleepCount++;
                Console.WriteLine($"Sleeping for {60 * sleepCount} secund...");
                await Task.Delay(TimeSpan.FromSeconds(60 * sleepCount));
                Console.WriteLine("Waking up...");
            }
            
            var result = await Resolve(data, context);
            if (!result)
                falseResultCount++;
            else
                falseResultCount = 0;

            context.Attach(data);
            data.IsCompleted = true;
            await context.SaveChangesAsync();
        }

        Console.WriteLine("Sleeping for 10 secund...");
        await Task.Delay(TimeSpan.FromSeconds(10));
        Console.WriteLine("Waking up...");
    }


    private async Task<bool> Resolve(Entities.Data data, DataBaseContext _dataBaseContext)
    {
        bool result = false;
        var stopwatch = new Stopwatch();
        
        switch (data.UrlType)
        {
            case UrlType.Cloud:
                stopwatch.Start();
                result = await CloudTypeHandler(data, _dataBaseContext);
                stopwatch.Stop();
                if(result)
                    Console.ForegroundColor = ConsoleColor.Green;
                else 
                    Console.ForegroundColor = ConsoleColor.Red;
                
                Console.WriteLine(DateTime.UtcNow + $"\t CloudTypeHandler took {stopwatch.ElapsedMilliseconds} ms " + data.Uid + " - completed with: " + result);
                Console.ResetColor();
                break;

            case UrlType.Slink:
                stopwatch.Start();
                result = await SlinkTypeHandler(data, _dataBaseContext);
                stopwatch.Stop();
                if(result)
                    Console.ForegroundColor = ConsoleColor.Green;
                else 
                    Console.ForegroundColor = ConsoleColor.Red;
                
                Console.WriteLine(DateTime.UtcNow + $"\t SlinkTypeHandler took {stopwatch.ElapsedMilliseconds} ms " + data.Uid + " - completed with: " + result);
                Console.ResetColor();
                break;

            case UrlType.Video:
                stopwatch.Start();
                result = await VideoTypeHandler(data, _dataBaseContext);
                stopwatch.Stop();
                if(result)
                    Console.ForegroundColor = ConsoleColor.Green;
                else 
                    Console.ForegroundColor = ConsoleColor.Red;
                
                Console.WriteLine(DateTime.UtcNow + $"\t VideoTypeHandler took {stopwatch.ElapsedMilliseconds} ms " + data.Uid + " - completed with: " + result);
                Console.ResetColor();
                break;

            default:
                return false;
        }

        return result;
    }

    private static async Task<(DateTime, CameraType)?> GetLastPunishmentDateAndEventType(string link)
    {
        try
        {
            JObject response = await GetWithContentAsync(link.Replace("https://cloud.yhxx.uz/video/raw/car_photo/", "https://cloud.yhxx.uz/video/meta/"));

            string titleUz = response["data"]["rules"]["title_uz"].ToString();
            CameraType cameraType = CameraType.TrafficControl;
            if (titleUz.Contains("Тезлик"))
                cameraType = CameraType.Speed;

            var dateString = response["data"]["the_date"].ToString();
            DateTime theDate = DateTime.Parse(dateString).ToUniversalTime();

            return (theDate, cameraType);
        }
        catch (Exception e)
        {
            return null;
        }
    }

    public static Bitmap PreprocessImage(byte[] imageBytes)
    {
        try
        {
            using MemoryStream ms = new MemoryStream(imageBytes);
            using Bitmap originalImage = new Bitmap(ms);

            int newWidth = originalImage.Width * 3;
            int newHeight = originalImage.Height * 3;
            Bitmap resizedImage = ResizeImage(originalImage, newWidth, newHeight);

            Bitmap grayImage = ConvertToGrayscale(resizedImage);

            Bitmap binaryImage = AdjustContrastAndThreshold(grayImage, 1.4, 150);

            ApplyDilation(binaryImage);

            return binaryImage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing image: {ex.Message}");
            throw;
        }
    }

    private static Bitmap ResizeImage(Bitmap input, int width, int height)
    {
        Bitmap resizedImage = new Bitmap(width, height);
        using (Graphics g = Graphics.FromImage(resizedImage))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(input, 0, 0, width, height);
        }
        return resizedImage;
    }

    private static Bitmap ConvertToGrayscale(Bitmap input)
    {
        Bitmap grayImage = new Bitmap(input.Width, input.Height, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(grayImage))
        {
            ColorMatrix colorMatrix = new ColorMatrix(new float[][]
            {
                new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });

            ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(colorMatrix);

            g.DrawImage(input, new Rectangle(0, 0, input.Width, input.Height),
                        0, 0, input.Width, input.Height, GraphicsUnit.Pixel, attributes);
        }
        return grayImage;
    }

    private static Bitmap AdjustContrastAndThreshold(Bitmap input, double contrastFactor, int threshold)
    {
        BitmapData bmpData = input.LockBits(
            new Rectangle(0, 0, input.Width, input.Height),
            ImageLockMode.ReadWrite,
            PixelFormat.Format24bppRgb);

        unsafe
        {
            byte* ptr = (byte*)bmpData.Scan0;
            int height = input.Height, width = input.Width;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * bmpData.Stride + x * 3;

                    byte gray = ptr[index];
                    int adjusted = Math.Min(255, (int)(gray * contrastFactor));
                    ptr[index] = ptr[index + 1] = ptr[index + 2] = (byte)(adjusted < threshold ? 0 : 255);
                }
            }
        }
        input.UnlockBits(bmpData);
        return input;
    }

    public static void ApplyDilation(Bitmap image)
    {
        try
        {
            AForge.Imaging.Filters.Dilatation dilationFilter = new AForge.Imaging.Filters.Dilatation();
            Bitmap processedImage = dilationFilter.Apply(image);

            // Copy back the processed image to the original
            using (Graphics g = Graphics.FromImage(image))
            {
                g.DrawImageUnscaled(processedImage, 0, 0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying dilation: {ex.Message}");
        }
    }

    static string ExtractText(Bitmap image, string tessdataPath)
    {
        using var pix = ConvertBitmapToPix(image);
        using var engine = new TesseractEngine(tessdataPath, "eng+uzb+uzb_cyrl+rus+equ+osd", EngineMode.Default);
        using var page = engine.Process(pix, " --psm 6");
        return page.GetText();
    }

    static Pix ConvertBitmapToPix(Bitmap bitmap)
    {
        Pix pix = PixConverter.ToPix(bitmap);
        return pix;
    }
    
    public static (double Latitude, double Longitude)? GetGpsCoordinates(byte[] imageData)
    {
        try
        {
            // Create an image stream from the byte array
            using (var stream = new MemoryStream(imageData))
            {
                var directories = ImageMetadataReader.ReadMetadata(stream);
                var regexPatterns = new[]
                {
                    @"([+-]?\d{1,3}\.\d{5,6})\s*,?\s*([+-]?\d{1,3}\.\d{5,6})", // Pattern for GPS coordinates with + and - signs
                    @"([+-]?\d{2,3}\.\d{4,6})\s*,?\s*([+-]?\d{2,3}\.\d{4,6})"   // Another format for GPS coordinates
                };
                foreach (var directory in directories)
                {
                    if (directory.Name == "JpegComment")
                    {
                        var text = directory.Tags[0].Description;
                        // Iterate through the patterns and try to match
                        foreach (var pattern in regexPatterns)
                        {
                            var match = Regex.Match(text, pattern);
                            if (match.Success)
                            {
                                if (double.TryParse(match.Groups[1].Value, out double latitude) &&
                                    double.TryParse(match.Groups[2].Value, out double longitude))
                                {
                                    return (latitude, longitude);
                                }
                            }
                        }
                    }
                }

                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading image metadata: " + ex.Message);
            return null;
        }
    }
        
    private async Task<bool> CloudTypeHandler(Entities.Data data, DataBaseContext _dataBaseContext)
    {
        var link = data.Link.Replace("https://cloud.yhxx.uz/", "https://cloud.yhxx.uz/video/raw/car_photo/");
        byte[]? firstImageData = null;
        byte[]? secondImageData = null;
        (double, double)? coordinates = null;
        for (int i = 0; i < 3; i++)
        {
            firstImageData = await GetImage(link);
            if(firstImageData is not null)
                break;
        }

        if (firstImageData is not null)
        {
            coordinates = GetGpsCoordinates(firstImageData);
        }

        if (coordinates is null)
        {
            for (int i = 0; i < 3; i++)
            {
                secondImageData = await GetImage(link.Replace("https://cloud.yhxx.uz/video/raw/car_photo/", "https://cloud.yhxx.uz/video/raw/full_photo/"));
                if(secondImageData is not null)
                    break;
            }
            if (secondImageData is not null)
            {
                coordinates = GetGpsCoordinates(secondImageData);
            }

        }

        if (coordinates is null)
        {
            coordinates = await ExtractCoordinates(data, firstImageData, secondImageData);
            if (coordinates == null)
            {
                Console.WriteLine($"coordinates is null");
                return false;
            }
        }
        
        var response = await GetLastPunishmentDateAndEventType(link);
        if (response == null)
        {
            Console.WriteLine("response from GetLastPunishmentDateAndEventType is null");
            return false;
        }
        
        bool isValid = await OpenStreetMapGeospatialService.IsValid(coordinates.Value.Item1,coordinates.Value.Item2);
        if (isValid)
        {
            var camera = new Camera
            {
                Latitude = coordinates.Value.Item1,
                Longitude = coordinates.Value.Item2,
                CameraType = response.Value.Item2
            };
        
            var cameraEntity = await _dataBaseContext.Cameras.FirstOrDefaultAsync(a => a.Longitude == camera.Longitude && a.Latitude == camera.Latitude);
        
            if (cameraEntity is null)
            {
                var entityEntry = await _dataBaseContext.Cameras.AddAsync(camera);
                await _dataBaseContext.SaveChangesAsync();
                var punishmentEntity = await _dataBaseContext.Punishments.FirstOrDefaultAsync(p => p.Uid == data.Uid);
                if (punishmentEntity is null)
                {
                    var punishment = new Punishment()
                    {
                        Uid = data.Uid,
                        Link = data.Link,
                        Date = response.Value.Item1,
                        UrlType = UrlType.Cloud,
                        CameraId = entityEntry.Entity.Id
                    };
        
                    // Start measuring time for adding punishment
                    await _dataBaseContext.Punishments.AddAsync(punishment);
                    await _dataBaseContext.SaveChangesAsync();
                }
                return true;
            }
            else
            {
                if (cameraEntity.CameraType != response.Value.Item2)
                {
                    cameraEntity.CameraType = CameraType.All;
                    await _dataBaseContext.SaveChangesAsync();
                }
        
                var punishmentEntity = await _dataBaseContext.Punishments.FirstOrDefaultAsync(p => p.Uid == data.Uid);
                if (punishmentEntity is null)
                {
                    var punishment = new Punishment()
                    {
                        Uid = data.Uid,
                        Link = data.Link,
                        Date = response.Value.Item1,
                        UrlType = UrlType.Cloud,
                        CameraId = cameraEntity.Id
                    };
        
                    // Start measuring time for adding punishment
                    await _dataBaseContext.Punishments.AddAsync(punishment);
                    await _dataBaseContext.SaveChangesAsync();
                }
                
                return true;
            }
        }
        
        return false;
    }

    public async Task<string[]> ExtractCoordinatesSlink(byte[] imageByte, string uid)
    {
        var regex = new Regex(@"[^\d\s\n\t.,]");
        
        var regexPatterns = new[]
        {
            @"(\d{1,3}\.\d{5,6})\s*,?\s*(\d{1,3}\.\d{5,6})",
            @"(\d{2,3}\.\d{4,6})\s*,?\s*(\d{2,3}\.\d{4,6})"
        };


        Bitmap processedImage = PreprocessImage(imageByte);
        string text = ExtractText(processedImage, _tessDataPath);
        var firstText = regex.Replace(text, "").Replace("\t", " ").Replace("\n", " ");
        if (firstText.Contains("movin9"))
        {
            return null;
        }
        foreach (var pattern in regexPatterns)
        {
            var match = Regex.Match(firstText, pattern);
            if (match.Success) return new[] {  match.Groups[1].Value,  match.Groups[2].Value };
            
        }

        return null;
    }

    public async Task<(double Latitude, double Longitude)?> ExtractCoordinates(Entities.Data data, byte[] firstImageData, byte[] secondImageData)
    {
        // Regex to clean non-numeric and unwanted characters
        var regex = new Regex(@"[^\d\s.,-]");
        var regexPatterns = new[]
        {
            @"(\d{1,3}\.\d{5,6})\s*,?\s*(\d{1,3}\.\d{5,6})", // Pattern for typical GPS coordinates
            @"(\d{2,3}\.\d{4,6})\s*,?\s*(\d{2,3}\.\d{4,6})"  // Pattern for alternate format
        };

        (double Latitude, double Longitude)? ExtractFromImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) 
                return null;

            // Preprocess the image (your custom implementation)
            var processedImage = PreprocessImage(imageData);

            // Extract text from the image (e.g., OCR)
            var text = ExtractText(processedImage, _tessDataPath);
            if (text.Contains("movin9", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            // Clean the extracted text
            var cleanedText = regex.Replace(text, "").Replace("\t", " ").Replace("\n", " ");

            // Check for specific unwanted keywords
            

            // Try matching GPS patterns
            foreach (var pattern in regexPatterns)
            {
                var match = Regex.Match(cleanedText, pattern);
                if (match.Success &&
                    double.TryParse(match.Groups[1].Value, out double latitude) &&
                    double.TryParse(match.Groups[2].Value, out double longitude))
                {
                    return (latitude, longitude);
                }
            }

            return null;
        }

        // Update the link to the first image
        // Process the first image
        if (firstImageData != null)
        {
            var result = ExtractFromImage(firstImageData);
            if (result.HasValue)
            {
                return result;
            }
        }

        return ExtractFromImage(secondImageData);
    }


    
    public static async Task<byte[]?> GetImage(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        try
        {
            var response = await httpClient.GetAsync(link, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
        catch (HttpRequestException httpEx)
        {
            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    
    public static async Task<HttpResponseMessage> GetAsync(string link)
    {
        try
        {
            var response = await httpClient.GetAsync(link);
            response.EnsureSuccessStatusCode();

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching image: {ex.Message}");
            return null;
        }
    }
    
    
    public static async Task<JObject> GetWithContentAsync(string link)
    {
        try
        {
            var response = await httpClient.GetAsync(link);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject data = JObject.Parse(responseBody);
            
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching image: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> VideoTypeHandler(Entities.Data data, DataBaseContext _dataBaseContext)
    {
        try
        {
            if (data == null || string.IsNullOrEmpty(data.Link))
            {
                Console.WriteLine("Error: Invalid data or data.Link is null or empty.");
                return false;
            }

            // Attempt to get the response from the provided link
            var response = await GetAsync(data.Link);
            
            // Ensure the request was successful
            response.EnsureSuccessStatusCode();
            
            // Parse the response content into an HTML document
            var htmlDoc = new HtmlDocument();
            var htmlContent = await response.Content.ReadAsStringAsync();
            htmlDoc.LoadHtml(htmlContent);

            // Check if the content is available and parse the date and coordinates
            var theDateNode = htmlDoc.DocumentNode.SelectSingleNode("//h4[normalize-space()='Вақт']/following-sibling::div");
            var coordinatesNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='map']");

            if (theDateNode == null || coordinatesNode == null)
            {
                Console.WriteLine("Error: Missing necessary nodes (date or coordinates).");
                return false;
            }

            string theDate = theDateNode.InnerText.Trim();
            string coordinates = coordinatesNode.GetAttributeValue("data-center", "");

            if (string.IsNullOrEmpty(coordinates))
            {
                Console.WriteLine("Error: Coordinates are null or empty.");
                return false;
            }

            // Remove the word "года" from the date string
            var russianCulture = new CultureInfo("ru-RU");
            theDate = Regex.Replace(theDate, @"\sгода", "");

            // Split the coordinates and parse them
            var coordinatesArray = coordinates.Split(',');
            if (coordinatesArray.Length != 2)
            {
                Console.WriteLine("Error: Invalid coordinates format.");
                return false;
            }

            double latitude, longitude;
            if (!double.TryParse(coordinatesArray[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
                !double.TryParse(coordinatesArray[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
            {
                Console.WriteLine("Error: Invalid coordinates value.");
                return false;
            }

            // Parse the date and convert to universal time
            DateTime dateTime;
            try
            {
                dateTime = DateTime.ParseExact(theDate, "d MMMM yyyy, HH:mm", russianCulture, DateTimeStyles.None);
            }
            catch (FormatException ex)
            {
                Console.WriteLine("Error: Date format exception - " + ex.Message);
                return false;
            }

            var universalDateTime = dateTime.ToUniversalTime();

            // Validate coordinates using OpenStreetMap service
            if (!await OpenStreetMapGeospatialService.IsValid(latitude, longitude))
            {
                Console.WriteLine("Error: Invalid coordinates.");
                return false;
            }

            // Determine the camera type based on the page content
            var cameraType = CameraType.TrafficControl;
            var speedViolationNode = htmlDoc.DocumentNode.SelectSingleNode("//div[a[contains(text(), '128-1')]]");

            if (speedViolationNode != null && speedViolationNode.InnerText.Contains("тезлик", StringComparison.OrdinalIgnoreCase))
            {
                cameraType = CameraType.Speed;
            }

            // Create the camera object
            var camera = new Camera
            {
                Latitude = latitude,
                Longitude = longitude,
                CameraType = cameraType
            };

            // Check if the camera already exists in the database
            var cameraEntity = await _dataBaseContext.Cameras.FirstOrDefaultAsync(a =>
                a.Longitude == longitude && a.Latitude == latitude);

            // If camera doesn't exist, add a new camera and punishment record
            if (cameraEntity == null)
            {
                var entityEntry = await _dataBaseContext.Cameras.AddAsync(camera);
                await _dataBaseContext.SaveChangesAsync();

                var punishmentEntity = await _dataBaseContext.Punishments.FirstOrDefaultAsync(p => p.Uid == data.Uid);
                if (punishmentEntity is null)
                {
                    var punishment = new Punishment()
                    {
                        Uid = data.Uid,
                        Link = data.Link,
                        Date = universalDateTime,
                        UrlType = UrlType.Video,
                        CameraId = entityEntry.Entity.Id
                    };

                    await _dataBaseContext.Punishments.AddAsync(punishment);
                    await _dataBaseContext.SaveChangesAsync();
                }
                
                return true;
            }
            else
            {
                // If camera exists and type is different, update camera type
                if (cameraEntity.CameraType != cameraType)
                {
                    cameraEntity.CameraType = CameraType.All;
                }

                var punishmentEntity = await _dataBaseContext.Punishments.FirstOrDefaultAsync(p => p.Uid == data.Uid);
                if (punishmentEntity is null)
                {
                    var punishment = new Punishment()
                    {
                        Uid = data.Uid,
                        Link = data.Link,
                        Date = universalDateTime,
                        UrlType = UrlType.Video,
                        CameraId = cameraEntity.Id
                    };

                    await _dataBaseContext.Punishments.AddAsync(punishment);
                    await _dataBaseContext.SaveChangesAsync();
                }
                
                return true;
            }
        }
        catch (Exception ex)
        {
            // Log the exception details for debugging
            Console.WriteLine("Exception occurred in VideoTypeHandler: " + ex.Message);
            Console.WriteLine("Stack Trace: " + ex.StackTrace);

            // Optionally log inner exceptions if present
            if (ex.InnerException != null)
            {
                Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                Console.WriteLine("Inner Exception Stack Trace: " + ex.InnerException.StackTrace);
            }

            return false;
        }
    }

    public async Task<string> GetFinalRedirectedUrl(string url)
    {
        // Set the 'AllowAutoRedirect' property to true (which is default in HttpClient)
        httpClient.DefaultRequestHeaders.Clear();

        try
        {
            // Make a GET request and allow redirection
            var response = await httpClient.GetAsync(url);

            // Ensure we get a successful response
            response.EnsureSuccessStatusCode();

            // Return the final URL after redirection
            return response.RequestMessage.RequestUri.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching URL: {ex.Message}");
            return string.Empty;
        }
    }
    

    private async Task<bool> SlinkTypeHandler(Entities.Data data, DataBaseContext _dataBaseContext)
    {
        try
        {
            string finalUrl = await GetFinalRedirectedUrl(data.Link);
            string apiUrl = finalUrl.Replace("https://jarima.ksubdd.uz/", "https://jarima.ksubdd.uz/api/");
            var response = await GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();

            if (!jsonResponse.StartsWith("{") && !jsonResponse.StartsWith("["))
            {
                Console.WriteLine("Received non-JSON response: " + jsonResponse);
                return false;
            }
            
            JObject json = JObject.Parse(jsonResponse);

            var locationToken = json["data"]?["location"];
            string[] coordinates = null;
            if (locationToken == null)
            {
                var filesArray = json["data"]?["files"] as JArray;
                if (filesArray != null)
                {
                    var imageCloseUpPath =
                        filesArray.FirstOrDefault(file => file["type"]?.ToString() == "ImageCloseUp")?["path"]
                            ?.ToString();

                    if (!string.IsNullOrEmpty(imageCloseUpPath))
                    {
                        byte[] imageData = await GetImage(imageCloseUpPath);
                        if (imageData is null)
                            return false;
                        coordinates = await ExtractCoordinatesSlink(imageData, data.Uid);
                        if (!coordinates.Any())
                        {
                            Console.WriteLine($"coordinates is null");
                            return false;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                string location = locationToken.ToString();
                coordinates = location.Split(',');
            }

            CameraType cameraType = CameraType.TrafficControl;
            if (json["data"]?["violationType"]?["en"]?.ToString().Contains("km/h") == true ||
                json["data"]?["violationType"]?["en"]?.ToString().Contains("Speed") == true)
                cameraType = CameraType.Speed;

            var dateString = json["data"]?["eventDate"]?.ToString();
            if (string.IsNullOrEmpty(dateString))
            {
                throw new InvalidOperationException("Invalid event date in JSON data");
            }

            DateTime theDate = DateTime.Parse(dateString).ToUniversalTime();

            if (await OpenStreetMapGeospatialService.IsValid(double.Parse(coordinates[0], CultureInfo.InvariantCulture),
                    double.Parse(coordinates[1], CultureInfo.InvariantCulture)))
            {
                var camera = new Camera
                {
                    Latitude = double.Parse(coordinates[0], CultureInfo.InvariantCulture),
                    Longitude = double.Parse(coordinates[1], CultureInfo.InvariantCulture),
                    CameraType = cameraType
                };

                var cameraEntity = await _dataBaseContext.Cameras.FirstOrDefaultAsync(a =>
                    a.Longitude == camera.Longitude && a.Latitude == camera.Latitude);
                if (cameraEntity is null)
                {
                    var entityEntry = await _dataBaseContext.Cameras.AddAsync(camera);
                    await _dataBaseContext.SaveChangesAsync();

                    var punishmentEntity = await _dataBaseContext.Punishments.FirstOrDefaultAsync(p => p.Uid == data.Uid);
                    if (punishmentEntity is null)
                    {
                        var punishment = new Punishment()
                        {
                            Uid = data.Uid,
                            Link = data.Link,
                            Date = theDate,
                            UrlType = UrlType.Slink,
                            CameraId = entityEntry.Entity.Id
                        };

                        await _dataBaseContext.Punishments.AddAsync(punishment);
                        await _dataBaseContext.SaveChangesAsync();
                    }
                    
                    return true;
                }
                else
                {
                    if (cameraEntity.CameraType != cameraType)
                    {
                        cameraEntity.CameraType = CameraType.All;
                    }
                    
                    var punishmentEntity = await _dataBaseContext.Punishments.FirstOrDefaultAsync(p => p.Uid == data.Uid);
                    if (punishmentEntity is null)
                    {
                        var punishment = new Punishment()
                        {
                            Uid = data.Uid,
                            Link = data.Link,
                            Date = theDate,
                            UrlType = UrlType.Slink,
                            CameraId = cameraEntity.Id
                        };

                        await _dataBaseContext.Punishments.AddAsync(punishment);
                        await _dataBaseContext.SaveChangesAsync();
                    }
                    
                    return true;
                }
            }

            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }
}