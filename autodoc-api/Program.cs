using System.Text;
using System.Text.Json;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

// AutoDoc REST API
// Phase 2: Docker Backend Service
// Receives controller code via HTTP POST
// Returns enriched OpenAPI YAML as HTTP response

app.MapPost("/generate-openapi", async (HttpRequest request, IHttpClientFactory httpClientFactory) =>
{
    // Read request body
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    string controllerCode;
    string controllerName;

    try
    {
        using var doc = JsonDocument.Parse(body);
        controllerCode = doc.RootElement.GetProperty("controllerCode").GetString() ?? string.Empty;
        controllerName = doc.RootElement.GetProperty("controllerName").GetString() ?? "Controller";
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid request. Provide controllerCode and controllerName." });
    }

    if (string.IsNullOrEmpty(controllerCode))
        return Results.BadRequest(new { error = "controllerCode cannot be empty." });

    Console.WriteLine($"Processing: {controllerName}");

    var sw = Stopwatch.StartNew();

    // Call Ollama API
    var systemPrompt = "You are an OpenAPI 3.0.3 YAML generator. Generate only valid OpenAPI 3.0.3 YAML from C# ASP.NET Core controllers. Infer all error responses from code logic. Never add explanations, markdown, or any text outside the YAML. Always group all HTTP methods under the same path key. Never duplicate a path. Always include tags on every endpoint using the controller name. Always include operationId on every endpoint using verb plus resource naming. Every operationId must be unique. Always include 500 Internal Server Error on every endpoint. Always name the generic error schema ErrorResponse.";

    var userPrompt = $"Generate complete valid OpenAPI 3.0.3 YAML for this C# ASP.NET Core controller. Infer all error responses from code logic. Each HTTP path must appear ONLY ONCE. Return only YAML starting with 'openapi: 3.0.3'. No markdown. No explanations.\n\nCONTROLLER:\n{controllerCode}";

    var httpClient = httpClientFactory.CreateClient();
    httpClient.Timeout = TimeSpan.FromMinutes(5);

    var requestBody = new
    {
        model  = "llama3.2",
        prompt = $"{systemPrompt}\n\n{userPrompt}",
        stream = false
    };

    var json     = JsonSerializer.Serialize(requestBody);
    var content  = new StringContent(json, Encoding.UTF8, "application/json");
   var ollamaHost = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://host.docker.internal:11434";
var response = await httpClient.PostAsync($"{ollamaHost}/api/generate", content);
    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem($"Ollama API call failed: {responseBody}");
    }

    using var ollamaDoc = JsonDocument.Parse(responseBody);
    var rawResponse = ollamaDoc.RootElement
        .GetProperty("response")
        .GetString() ?? string.Empty;

    sw.Stop();

    // Extract and Clean
    var lines     = rawResponse.Split('\n').ToList();
    var yamlStart = lines.FindIndex(l => l.TrimStart().StartsWith("openapi:"));
    var cleanYaml = yamlStart >= 0
        ? string.Join('\n', lines.Skip(yamlStart)).Replace("```yaml", "").Replace("```", "").Trim()
        : rawResponse;

    cleanYaml = MergeDuplicatePaths(cleanYaml);
    cleanYaml = NormaliseOutput(cleanYaml);

    // Validation Gate
    if (!cleanYaml.StartsWith("openapi:") || !cleanYaml.Contains("paths:"))
    {
        return Results.Problem("Invalid OpenAPI output generated. Please try again.");
    }

    Console.WriteLine($"Generated {cleanYaml.Split('\n').Length} lines in {sw.ElapsedMilliseconds / 1000}s");

    return Results.Ok(new
    {
        controllerName = controllerName,
        yaml           = cleanYaml,
        lines          = cleanYaml.Split('\n').Length,
        generationTime = $"{sw.ElapsedMilliseconds / 1000} seconds"
    });
});

app.MapGet("/health", () => Results.Ok(new { status = "AutoDoc API is running" }));

app.Run();

// Helper functions
static string NormaliseOutput(string yaml)
{
    yaml = yaml.Replace("\n    Error:\n", "\n    ErrorResponse:\n")
               .Replace("$ref: '#/components/schemas/Error'",
                        "$ref: '#/components/schemas/ErrorResponse'")
               .Replace("\"$ref\": \"#/components/schemas/Error\"",
                        "\"$ref\": \"#/components/schemas/ErrorResponse\"");
    return yaml;
}

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