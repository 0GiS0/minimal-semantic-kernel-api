global using Microsoft.SemanticKernel;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel.Planners;
using minimal_semantic_kernel_api.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .Build();

// Get model, apiKey, endpoint and openaiKey from environment variables or appsettings.json
var model = Environment.GetEnvironmentVariable("model") ?? config.GetSection("Values").GetValue<string>("model");
var apiKey = Environment.GetEnvironmentVariable("apiKey") ?? config.GetSection("Values").GetValue<string>("apiKey");
var qdrant = Environment.GetEnvironmentVariable("qdrant") ?? config.GetSection("Values").GetValue<string>("qdrant");

// Check if model, apiKey, endpoint and openaiKey are set
if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("Please set model, apiKey, endpoint and openaiKey in appsettings.json");
    return;
}


// Build a kernel
var kernel = new KernelBuilder()
              .WithOpenAIChatCompletionService(model, apiKey)
              .Build();


// Get plugins directory path
var pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Plugins");

app.MapGet("/", () => "Welcome to Semantic Kernel!");


/*****************************************************************************************
*********************************** Semantic Functions ***********************************
*****************************************************************************************/
app.MapPost("plugins/{pluginName}/invoke/{functionName}", async (HttpContext context, string pluginName, string functionName, [FromBody] UserAsk userAsk) =>
{
    try
    {
        var funPluginFunctions = kernel.ImportSemanticFunctionsFromDirectory(pluginsDirectory, pluginName);

        var result = await kernel.RunAsync(userAsk.Ask, funPluginFunctions[functionName]);

        return Results.Json(new { answer = result.GetValue<string>() });


    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        throw;
    }

});


/*****************************************************************************************
*********************************** Planner *********************************************
*****************************************************************************************/
app.MapGet("planner", async (HttpContext context, string query) =>
{
    var planner = new SequentialPlanner(kernel);

    kernel.ImportSemanticFunctionsFromDirectory(pluginsDirectory, "FunPlugin");

    var plan = await planner.CreatePlanAsync(query);

    Console.WriteLine("Plan:\n");
    Console.WriteLine(JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));

    var result = await kernel.RunAsync(plan);

    Console.WriteLine("Result:\n");
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

    return Results.Json(new { answer = result.GetValue<string>() });


});


/*****************************************************************************************
*********************************** Kernel Memory ****************************************
*****************************************************************************************/

// First, load some documents... about Minecraft! 😙
Plugins.MemoryPlugin.MemoryKernel.Init(apiKey, qdrant);

app.MapGet("memory", async (HttpContext context, string query) =>
{
    kernel.ImportSemanticFunctionsFromDirectory(pluginsDirectory, "FunPlugin");
    var memoryPlugin = kernel.ImportFunctions(new Plugins.MemoryPlugin.MemoryKernel(), "MemoryPlugin");

    var planner = new SequentialPlanner(kernel);

    var plan = await planner.CreatePlanAsync(query);

    Console.WriteLine("Plan:\n");
    Console.WriteLine(JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));

    var result = await kernel.RunAsync(plan);

    Console.WriteLine(result);

    try
    {
        return Results.Json(JsonSerializer.Deserialize<Answer>(result.GetValue<string>()));
    }
    catch (System.Exception)
    {
        return Results.Json(new { answer = result.GetValue<string>() });
    }

});


app.Run();