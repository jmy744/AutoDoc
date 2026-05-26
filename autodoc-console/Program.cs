using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// AutoDoc — Automatic OpenAPI Documentation Generator
// Phase 1: Console Application
// Reads a C# controller file and generates enriched OpenAPI 3.0.3 YAML
// Powered by Ollama (llama3.2) — runs locally, free, no API key needed

// 1 Validate controller path argument
if (args.Length == 0)
{
    Console.WriteLine("ERROR: No controller file path provided.");
    Console.WriteLine("Usage: dotnet run -- <path-to-controller.cs>");
    Environment.Exit(1);
    return;
}

var controllerPath = args[0];
var controllerName = Path.GetFileNameWithoutExtension(controllerPath);
var outputDir      = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "output");
var yamlFile       = Path.Combine(outputDir, $"{controllerName}.yaml");

// 2 Validate file exists
if (!File.Exists(controllerPath))
{
    Console.WriteLine($"ERROR: Controller not found at {Path.GetFullPath(controllerPath)}");
    Environment.Exit(1);
    return;
}

Directory.CreateDirectory(outputDir);
Console.WriteLine($"Controller : {controllerName}.cs");

// 3 Read controller and model files
var controllerCode = File.ReadAllText(controllerPath);
var serviceFolder  = Path.GetDirectoryName(Path.GetDirectoryName(controllerPath)) ?? "";
var modelFiles     = new List<string>();

foreach (var folder in new[] { "Models", "DTOs", "Dto", "Dtos" })
{
    var fullFolder = Path.Combine(serviceFolder, folder);
    if (Directory.Exists(fullFolder))
        modelFiles.AddRange(Directory.GetFiles(fullFolder, "*.cs", SearchOption.AllDirectories));
}

Console.WriteLine($"Model files: {modelFiles.Count} found");

var modelCode = modelFiles.Count > 0
    ? string.Join("\n\n", modelFiles.Select(f => $"// {Path.GetFileName(f)}\n{File.ReadAllText(f)}"))
    : string.Empty;

Console.WriteLine("Generating OpenAPI documentation...");
var sw = Stopwatch.StartNew();

// 4 Call Ollama API locally
var modelSection = modelCode.Length > 0
    ? $"\n\nMODELS AND DTOS:\n{modelCode}"
    : string.Empty;

var systemPrompt = "You are an OpenAPI 3.0.3 YAML generator. Generate only valid OpenAPI 3.0.3 YAML from C# ASP.NET Core controllers. Infer all error responses from code logic. Never add explanations, markdown, or any text outside the YAML. Always group all HTTP methods under the same path key. Never duplicate a path. Always include tags on every endpoint using the controller name. Always include operationId on every endpoint using verb plus resource naming. Every operationId must be unique. Always include 500 Internal Server Error on every endpoint. Always name the generic error schema ErrorResponse.";

var userPrompt = $"Generate complete valid OpenAPI 3.0.3 YAML for this C# ASP.NET Core controller. Infer all error responses from code logic. Each HTTP path must appear ONLY ONCE. Return only YAML starting with 'openapi: 3.0.3'. No markdown. No explanations.\n\nCONTROLLER:\n{controllerCode}{modelSection}";

using var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromMinutes(5);

var requestBody = new
{
    model  = "llama3.2",
    prompt = $"{systemPrompt}\n\n{userPrompt}",
    stream = false
};

var json     = JsonSerializer.Serialize(requestBody);
var content  = new StringContent(json, Encoding.UTF8, "application/json");
var response = await httpClient.PostAsync("http://localhost:11434/api/generate", content);
var body     = await response.Content.ReadAsStringAsync();

if (!response.IsSuccessStatusCode)
{
    Console.WriteLine($"ERROR: Ollama API call failed — {body}");
    Environment.Exit(1);
    return;
}

using var doc   = JsonDocument.Parse(body);
var rawResponse = doc.RootElement
    .GetProperty("response")
    .GetString() ?? string.Empty;

sw.Stop();

// 5 Extract and Clean
// Step 1: Find where YAML starts
var lines     = rawResponse.Split('\n').ToList();
var yamlStart = lines.FindIndex(l => l.TrimStart().StartsWith("openapi:"));
var cleanYaml = yamlStart >= 0
    ? string.Join('\n', lines.Skip(yamlStart)).Replace("```yaml", "").Replace("```", "").Trim()
    : rawResponse;

// Step 2: Merge duplicate paths
cleanYaml = MergeDuplicatePaths(cleanYaml);

// Step 3: Normalise schema names to ErrorResponse
cleanYaml = NormaliseOutput(cleanYaml);

// 6 Validation Gate
if (!cleanYaml.StartsWith("openapi:") || !cleanYaml.Contains("paths:"))
{
    Console.WriteLine("ERROR: Invalid output — nothing saved.");
    Console.WriteLine("Raw response:");
    Console.WriteLine(rawResponse);
    Environment.Exit(1);
    return;
}

// 7 Save YAML
File.WriteAllText(yamlFile, cleanYaml);

Console.WriteLine();
Console.WriteLine("SUCCESS");
Console.WriteLine($"Controller  : {controllerName}");
Console.WriteLine($"Output      : {Path.GetFullPath(yamlFile)}");
Console.WriteLine($"Lines       : {cleanYaml.Split('\n').Length}");
Console.WriteLine($"Time        : {sw.ElapsedMilliseconds / 1000} seconds");
Console.WriteLine();
Console.WriteLine("--- Generated YAML ---");
Console.WriteLine(cleanYaml);

// Normalise error schema name to ErrorResponse
static string NormaliseOutput(string yaml)
{
    yaml = yaml.Replace("\n    Error:\n", "\n    ErrorResponse:\n")
               .Replace("$ref: '#/components/schemas/Error'",
                        "$ref: '#/components/schemas/ErrorResponse'")
               .Replace("\"$ref\": \"#/components/schemas/Error\"",
                        "\"$ref\": \"#/components/schemas/ErrorResponse\"");
    return yaml;
}

// Merge duplicate path entries
static string MergeDuplicatePaths(string yaml)
{
    var lines  = yaml.Split('\n').ToList();
    var result = new List<string>();
    var seen   = new Dictionary<string, int>();

    for (int i = 0; i < lines.Count; i++)
    {
        var line    = lines[i];
        var trimmed = line.TrimEnd();

        if (trimmed.StartsWith("  /") && trimmed.EndsWith(":"))
        {
            var pathKey = trimmed.Trim();
            if (seen.ContainsKey(pathKey))
            {
                var insertAt = seen[pathKey];
                i++;
                while (i < lines.Count && (lines[i].StartsWith("    ") || string.IsNullOrWhiteSpace(lines[i])))
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                        result.Insert(insertAt++, lines[i]);
                    i++;
                }
                i--;
                continue;
            }
            else
            {
                seen[pathKey] = result.Count + 1;
            }
        }

        result.Add(line);
    }

    return string.Join('\n', result);
}