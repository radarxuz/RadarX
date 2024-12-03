using System;
using System.IO;
using System.Text.RegularExpressions;
using MetadataExtractor;

public static class ImageGpsExtractor
{
    public static (double Latitude, double Longitude)? GetGpsCoordinates(byte[] imageBytes)
    {
        try
        {
            // Create a stream from the byte array
            using (var stream = new MemoryStream(imageBytes))
            {
                // Extract metadata from the image
                var directories = ImageMetadataReader.ReadMetadata(stream);

                // Define regex patterns for GPS coordinates
                var regexPatterns = new[]
                {
                    @"([+-]?\d{1,3}\.\d{5,6})\s*,?\s*([+-]?\d{1,3}\.\d{5,6})", // Common GPS format
                    @"([+-]?\d{2,3}\.\d{4,6})\s*,?\s*([+-]?\d{2,3}\.\d{4,6})"  // Alternate GPS format
                };

                // Iterate through each metadata directory
                foreach (var directory in directories)
                {
                    // Check if the directory contains GPS information or similar tags
                    foreach (var tag in directory.Tags)
                    {
                        var description = tag.Description;
                        if (!string.IsNullOrEmpty(description))
                        {
                            // Test each regex pattern
                            foreach (var pattern in regexPatterns)
                            {
                                var match = Regex.Match(description, pattern);
                                if (match.Success)
                                {
                                    // Try to parse the latitude and longitude
                                    if (double.TryParse(match.Groups[1].Value, out double latitude) &&
                                        double.TryParse(match.Groups[2].Value, out double longitude))
                                    {
                                        return (latitude, longitude);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting GPS coordinates: {ex.Message}");
        }

        // Return null if no GPS coordinates are found
        return null;
    }
}
