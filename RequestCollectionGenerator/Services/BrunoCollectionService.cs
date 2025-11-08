using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RequestCollectionGenerator.Models;
using CsvHelper;
using System.Formats.Asn1;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RequestCollectionGenerator.Services;

public class BrunoCollectionService
{
    public void GenerateCollection(string csvPath, string outputPath)
    {
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<ApiLogEntry>().ToList();

        // Group by TraceId (one root folder per trace)
        var groupedByTrace = records
            .GroupBy(r => r.TraceId)
            .OrderBy(g => g.First().TimeStamp);

        foreach (var traceGroup in groupedByTrace)
        {
            string traceFolder = Path.Combine(outputPath, SanitizeFolderName(traceGroup.Key));
            Directory.CreateDirectory(traceFolder);

            var orderedEntries = traceGroup.OrderBy(r => r.TimeStamp).ToList();
            var previousCallee = string.Empty;

            foreach (var entry in orderedEntries)
            {
                var callee = GetCalleeFolder(entry.RequestedUrl);

                // Nest callee under the previous one (to simulate trace chain)
                string fullPath;
                if (!string.IsNullOrEmpty(previousCallee) && !callee.Equals(previousCallee, StringComparison.OrdinalIgnoreCase))
                    fullPath = Path.Combine(traceFolder, previousCallee, callee);
                else
                    fullPath = Path.Combine(traceFolder, callee);

                Directory.CreateDirectory(fullPath);

                string fileName = $"{entry.Verb}_{ShortenUrl(entry.RequestedUrl)}.bru";
                string bruFilePath = Path.Combine(fullPath, SanitizeFileName(fileName));

                var request = new
                {
                    type = "http-request",
                    meta = new
                    {
                        name = $"{entry.Verb} {entry.RequestedUrl}",
                        created = entry.TimeStamp.ToString("O"),
                        traceId = entry.TraceId
                    },
                    request = new
                    {
                        url = entry.RequestedUrl,
                        method = entry.Verb.ToUpper(),
                        headers = ParseHeaders(entry.HeaderContent),
                        body = entry.BodyContent
                    }
                };

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(bruFilePath, json);

                previousCallee = callee;
            }
        }
    }

    private string GetCalleeFolder(string url)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            return segments.Length > 0 ? SanitizeFolderName(segments[0]) : "root";
        }
        catch
        {
            return "unknown";
        }
    }

    private Dictionary<string, string> ParseHeaders(string headerContent)
    {
        var headers = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(headerContent)) return headers;

        foreach (var line in headerContent.Split(';', '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
                headers[parts[0].Trim()] = parts[1].Trim();
        }

        return headers;
    }

    private string SanitizeFolderName(string name)
        => Regex.Replace(name, @"[^\w\d_-]+", "_");

    private string SanitizeFileName(string name)
        => Regex.Replace(name, @"[^\w\d_-]+", "_");

    private string ShortenUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return string.Join("_", uri.AbsolutePath.Trim('/').Split('/').TakeLast(2));
        }
        catch
        {
            return "request";
        }
    }
}
