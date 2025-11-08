using RequestCollectionGenerator.Services;

Console.WriteLine("=== Bruno Api Client Request Collection Generator ===");
string csvPath = @"c:\temp\RequestCollectionGenerator\input.csv";

string outputPath = @"c:\temp\RequestCollectionGenerator\Output";

Console.WriteLine($"Generating Bruno collection from CSV: {csvPath}, into {outputPath}");

if (string.IsNullOrWhiteSpace(csvPath) || string.IsNullOrWhiteSpace(outputPath))
{
    Console.WriteLine("CSV path and output folder are required.");
    return;
}

var service = new BrunoCollectionService();
service.GenerateCollection(csvPath!, outputPath!);

Console.WriteLine($"✅ Bruno collection generated successfully at: {outputPath}");
