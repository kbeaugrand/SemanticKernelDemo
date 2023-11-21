// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextEmbedding;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using SemanticKernel.Connectors.Memory.MongoDB;
using Spectre.Console;
using System;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.development.json", optional: true)
    .Build();

using var loggerFactory = LoggerFactory.Create(logging =>
{
    logging
        .AddConsole(opts =>
        {
            opts.FormatterName = "simple";
        })
        .AddConfiguration(configuration.GetSection("Logging"));
});

AnsiConsole.Write(new FigletText($"Wizard-Copilot").Color(Color.Chartreuse1));
AnsiConsole.WriteLine("");

AnsiConsole.Status().Start("Initializing...", ctx =>
{
    string azureOpenAIEndpoint = configuration["AzureOpenAIEndpoint"]!;
    string azureOpenAIKey = configuration["AzureOpenAIAPIKey"]!;
});

const string clear = "1.\tClear";
const string encode = "2.\tEncode";
const string find = "3.\tFind history";
const string group = "4.\tGroup history";
const string help = "5.\tHelp me";

ISemanticTextMemory CreateSemanticMemory(IConfiguration configuration)
{
    var embeddingGenerator = new AzureTextEmbeddingGeneration(
               modelId: configuration["EmbeddingModelId"]!,
               endpoint: configuration["AzureOpenAIEndpoint"]!,
               apiKey: configuration["AzureOpenAIKey"]!);

    return new SemanticTextMemory(MongoDBMemoryStore.Connect(connectionString: configuration["MongoDbConnectionString"]!, database: configuration["MongoDbVectorDB"]!), embeddingGenerator);
}

IKernel CreateKernel(IConfiguration configuration)
{
    var kernel = Kernel.Builder
            .WithAzureChatCompletionService(
                deploymentName: configuration["ChatCompletionModelId"]!,
                endpoint: configuration["AzureOpenAIEndpoint"]!,
                apiKey: configuration["AzureOpenAIKey"]!)
            .WithAzureTextEmbeddingGenerationService(
                deploymentName: configuration["EmbeddingModelId"]!,
                endpoint: configuration["AzureOpenAIEndpoint"]!,
                apiKey: configuration["AzureOpenAIKey"]!)
    .Build();

    var memoryPlugin = new TextMemoryPlugin(CreateSemanticMemory(configuration));

    kernel.ImportSemanticFunctionsFromDirectory(Path.Combine(Directory.GetCurrentDirectory(), "SKPrompts"), "Encoder");
    kernel.ImportSemanticFunctionsFromDirectory(Path.Combine(Directory.GetCurrentDirectory(), "SKPrompts"), "Resolver");

    kernel.ImportFunctions(memoryPlugin);

    return kernel;
}

while (true)
{
    try
    {
        var option = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select an option")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                .AddChoices(clear, encode, find, group, help)
        );

        switch (option)
        {
            case clear: 
                await Clear().ConfigureAwait(true);
                break;
            case encode:
                await Encode().ConfigureAwait(true);
                break;
            case find:
                await FindHistory().ConfigureAwait(true);
                break;
            case group:
                await GroupHistory().ConfigureAwait(true);
                break;
            case help:
                await FindSolution().ConfigureAwait(true);
                break;
        }
    }
    catch(Exception ex)
    {
        AnsiConsole.Write(new Rule("[red][/]") { Justification = Justify.Center });
        AnsiConsole.WriteLine($"Error: {ex.Message}");
        AnsiConsole.Write(new Rule("[red][/]") { Justification = Justify.Center });
    }
}

async Task FindHistory()
{
    IKernel kernel = null;
    ISemanticTextMemory memory = null;

    AnsiConsole.Status().Start("Initializing...", ctx =>
    {
        ctx.Spinner(Spinner.Known.Star);
        ctx.SpinnerStyle(Style.Parse("green"));

        kernel = CreateKernel(configuration);
        memory = CreateSemanticMemory(configuration);

        ctx.Status("Initialized");
    });

    var prompt = AnsiConsole.Prompt(new TextPrompt<string>("Enter the problem you are facing: \n").PromptStyle("teal"));

    string result = null;

    await AnsiConsole.Status().StartAsync("Processing...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Star);
        ctx.SpinnerStyle(Style.Parse("green"));

        ctx.Status($"Searching into the memory");
        var relatedIssues = memory.SearchAsync("maintenance", prompt, 10);
        result = "";

        await foreach (var item in relatedIssues)
        {
            result += item.Metadata.Text + "\n";
            result += "--------------------------\n";
        }
    });

    AnsiConsole.Write(new Rule("[cyan][/]") { Justification = Justify.Center });
    AnsiConsole.WriteLine(result!);
    AnsiConsole.Write(new Rule("[cyan][/]") { Justification = Justify.Center });
}

async Task Encode()
{
    IKernel kernel = null;

    AnsiConsole.Status().Start("Initializing...", ctx =>
    {
        ctx.Spinner(Spinner.Known.Star);
        ctx.SpinnerStyle(Style.Parse("green"));

        kernel = CreateKernel(configuration);

        ctx.Status("Initialized");
    });

    var prompt = AnsiConsole.Prompt(new TextPrompt<string>("Enter the input you want to encode: \n").PromptStyle("teal"));

    string result = null;

    await AnsiConsole.Status().StartAsync("Processing...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Star);
        ctx.SpinnerStyle(Style.Parse("green"));

        var context = kernel.CreateNewContext();
        context.Variables["INPUT"] = prompt;

        ctx.Status($"Encoding your prompt");
        var solution = await kernel.RunAsync(kernel.Functions.GetFunction("Encoder", "AnomalyEncode"), context.Variables);

        result = solution.GetValue<string>()!;
    });

    AnsiConsole.Write(new Rule("[cyan][/]") { Justification = Justify.Center });
    AnsiConsole.WriteLine(result!);
    AnsiConsole.Write(new Rule("[cyan][/]") { Justification = Justify.Center });
}

async Task GroupHistory()
{
    IKernel kernel = null;

    AnsiConsole.Status().Start("Initializing...", ctx =>
    {
        ctx.Spinner(Spinner.Known.Star);
        ctx.SpinnerStyle(Style.Parse("green"));

        kernel = CreateKernel(configuration);

        ctx.Status("Initialized");
    });

    var prompt = AnsiConsole.Prompt(new TextPrompt<string>("Enter the problem you are facing: \n").PromptStyle("teal"));

    string result = null;

    await AnsiConsole.Status().StartAsync("Processing...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Star);
        ctx.SpinnerStyle(Style.Parse("green"));

        var context = kernel.CreateNewContext();

        context.Variables["INPUT"] = prompt;
        context.Variables[TextMemoryPlugin.CollectionParam] = "maintenance";
        context.Variables[TextMemoryPlugin.LimitParam] = "10";

        ctx.Status($"Grouping related issues");
        var solution = await kernel.RunAsync(kernel.Functions.GetFunction("Resolver", "GroupHistory"), context.Variables);

        result = solution.GetValue<string>()!;
    });

    AnsiConsole.Write(new Rule("[cyan][/]") { Justification = Justify.Center });
    AnsiConsole.WriteLine(result!);
    AnsiConsole.Write(new Rule("[cyan][/]") { Justification = Justify.Center });
}

async Task FindSolution()
{
    IKernel kernel = null;

    AnsiConsole.Status().Start("Initializing...", ctx =>
    {
        ctx.Spinner(Spinner.Known.Star);
        ctx.SpinnerStyle(Style.Parse("green"));

        kernel = CreateKernel(configuration);

        ctx.Status("Initialized");
    });

    var prompt = AnsiConsole.Prompt(new TextPrompt<string>("Enter the problem you are facing: \n").PromptStyle("teal"));

    string result = null;

    await AnsiConsole.Status().StartAsync("Processing...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Star);
        ctx.SpinnerStyle(Style.Parse("green"));

        var context = kernel.CreateNewContext();

        context.Variables["INPUT"] = prompt;
        context.Variables[TextMemoryPlugin.CollectionParam] = "maintenance";
        context.Variables[TextMemoryPlugin.LimitParam] = "5";
        //context.Variables["LANGUAGE"] = _language;

        ctx.Status($"Finding resolution suggestions");
        var solution = await kernel.RunAsync(kernel.Functions.GetFunction("Resolver", "HelpMe"), context.Variables);

        result = solution.GetValue<string>()!;
    });

    AnsiConsole.Write(new Rule("[cyan][/]") { Justification = Justify.Center });
    AnsiConsole.WriteLine(result!);
    AnsiConsole.Write(new Rule("[cyan][/]") { Justification = Justify.Center });
}

async Task Clear()
{
    AnsiConsole.Clear();
    AnsiConsole.Write(new FigletText($"Wizard-Copilot").Color(Color.Chartreuse1));
    AnsiConsole.WriteLine("");
}