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
        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            Mode = CsvMode.RFC4180
        };
        using var csv = new CsvReader(reader, config);
        var records = csv.GetRecords<ApiLogEntry>().ToList();

        // Group by TraceId (one root folder per trace)
        var groupedByTrace = records
            .GroupBy(r => r.TraceId)
            .OrderBy(g => g.First().TimeStamp);

        // ✅ Create the requests root folder expected by Bruno
        string requestsRoot = Path.Combine(outputPath, "requests");
        Directory.CreateDirectory(requestsRoot);

        foreach (var traceGroup in groupedByTrace)
        {
            // ✅ Each trace lives inside the requests folder
            string traceFolder = Path.Combine(requestsRoot, SanitizeFolderName(traceGroup.Key));
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
        GenerateBrunoJsonFile(outputPath);
    }


    private void GenerateBrunoJsonFile(string outputPath)
    {
        var brunoManifest = new
        {
            version = "1",
            name = "Generated API Collection",
            type = "collection",
            ignore = new[] { "node_modules", ".git" },
            description = "Generated from Azure logs"
        };

        File.WriteAllText(Path.Combine(outputPath, "bruno.json"),
            JsonSerializer.Serialize(brunoManifest, new JsonSerializerOptions { WriteIndented = true }));

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
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Preserve the extension (e.g. ".bru") and sanitize only the filename part.
        var extension = Path.GetExtension(name) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(name) ?? string.Empty;

        var cleanedBase = Regex.Replace(baseName, @"[^\w\d_-]+", "_");

        if (string.IsNullOrEmpty(extension))
            return cleanedBase;

        // Keep a safe extension (remove any unexpected chars from extension, ensure leading dot)
        var cleanedExt = Regex.Replace(extension, @"[^.\w\d_-]+", string.Empty);
        if (!cleanedExt.StartsWith("."))
            cleanedExt = "." + cleanedExt;

        return cleanedBase + cleanedExt;
    }

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
