using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextEmbedding;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using SemanticKernel.Connectors.Memory.MongoDB;
using System.Diagnostics;

namespace Copilot
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                })
                .ConfigureLifecycleEvents((events) =>
                {
#if WINDOWS
                    events.AddWindows(windows =>
                    {
                        windows
                            .OnLaunched(async (window, args) => {
                                await CopySKPrompts();
                                //EncodeDatabase();
                            });
                    });
#endif
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
            _ = builder.Services.AddSingleton((_) => CreateKernel());
            _ = builder.Services.AddSingleton((_) => CreateSemantiMemory());

            return builder.Build();
        }

        private static async Task CopySKPrompts()
        {
            await CopySKSkillToAppData("Encoder", "AnomalyEncode");
            await CopySKSkillToAppData("Resolver", "HelpMe");
            await CopySKSkillToAppData("Resolver", "GroupHistory");
        }

        private static void EncodeDatabase()
        {
            Task.Factory.StartNew(async () =>
            {
                var dataFilePath = Path.GetFullPath(
                                    Path.Combine(Directory.GetCurrentDirectory(), "C:\\Users\\KevinBEAUGRAND\\source\\repos\\kbeaugrand\\MagiGemm-SemanticKernel\\ScenarioAuthor\\bin\\Debug\\net7.0\\Magi-Gemmes_shufled.csv"));

                if (!File.Exists(dataFilePath))
                {
                    return;
                }

                var lines = await File.ReadAllLinesAsync(dataFilePath);

                var kernel = CreateKernel();

                var memory = CreateSemantiMemory();

                var encodingFunc = kernel.Functions.GetFunction(pluginName: "Encoder", functionName: "AnomalyEncode");

                await Parallel.ForEachAsync(lines, new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4

                }, async (line, cancellationToken) =>
                {
                    string userPrompt = null;
                    var lineNumber = lines.ToList().IndexOf(line);

                    try
                    {
                        userPrompt = line.Split(";", StringSplitOptions.TrimEntries)[1];
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine($"Failed to parse line {lineNumber}");
                        return;
                    }

                    var ctx = kernel.CreateNewContext();

                    //try
                    //{
                    //    if ((await memory.GetAsync(collection: "maintenance", key: lineNumber.ToString()).ConfigureAwait(false)) != null)
                    //    {
                    //        return;
                    //    }
                    //}
                    //catch(Exception e)
                    //{
                    //    Debug.WriteLine("Failed to check if line was already encoded. Exception: " + e.Message);
                    //    return;
                    //}

                    ctx.Variables["INPUT"] = userPrompt;

                    try
                    {
                        var encoding = await kernel.RunAsync(encodingFunc, ctx.Variables);

                        Debug.WriteLine($"Saving line {lineNumber}...");

                        var savedToMemory = $"Equipement: {line.Split(";", StringSplitOptions.TrimEntries)[0]}\n";
                        savedToMemory += encoding.GetValue<string>();

                        await memory.SaveInformationAsync(
                            collection: "maintenance",
                            text: savedToMemory,
                            id: lineNumber.ToString())
                                .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to encode line {lineNumber}, exception: {ex}");
                        return;
                    }
                }).ConfigureAwait(false);

                Debug.WriteLine("All entries encoded");
            });
        }

        private static async Task CopySKSkillToAppData(string skillName, string functionName)
        {
            var fileNames = new[] { "skprompt.txt", "config.json" };

            CreateDirectoryIfNotExists(Path.Combine(FileSystem.Current.AppDataDirectory, "SKPrompts"));
            CreateDirectoryIfNotExists(Path.Combine(FileSystem.Current.AppDataDirectory, "SKPrompts", skillName));
            CreateDirectoryIfNotExists(Path.Combine(FileSystem.Current.AppDataDirectory, "SKPrompts", skillName, functionName));

            foreach (var file in fileNames)
            {
                using var stream = await FileSystem.Current.OpenAppPackageFileAsync(Path.Combine("SKPrompts", skillName, functionName, file));
                using StreamReader reader = new StreamReader(stream);

                string targetFile = Path.Combine(FileSystem.Current.AppDataDirectory, "SKPrompts", skillName, functionName, file);

                using var outputStream = File.OpenWrite(targetFile);
                using var streamWriter = new StreamWriter(outputStream);

                await streamWriter.WriteAsync(await reader.ReadToEndAsync());
            }
        }

        private static void CreateDirectoryIfNotExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private static ISemanticTextMemory CreateSemantiMemory()
        {
            var embeddingGenerator = new AzureTextEmbeddingGeneration(
                       modelId: "text-embedding-ada-002",
                       endpoint: "https://copichat-yuwt7vgwiz3ug.openai.azure.com/",
                       apiKey: "");

            return new SemanticTextMemory(MongoDBMemoryStore.Connect(connectionString: "", database: "vector-cluster"), embeddingGenerator);
        }

        private static IKernel CreateKernel()
        {
            var kernel = Kernel.Builder
                    .WithAzureChatCompletionService(
                        deploymentName: "gpt-35-turbo",
                        endpoint: "https://copichat-yuwt7vgwiz3ug.openai.azure.com/",
                       apiKey: "")
                    .WithAzureTextEmbeddingGenerationService(
                        deploymentName: "text-embedding-ada-002",
                        endpoint: "https://copichat-yuwt7vgwiz3ug.openai.azure.com/",
                       apiKey: "")
            .Build();

            var memoryPlugin = new TextMemoryPlugin(CreateSemantiMemory());

            kernel.ImportSemanticFunctionsFromDirectory(Path.Combine(FileSystem.Current.AppDataDirectory, "SKPrompts"), "Encoder");
            kernel.ImportSemanticFunctionsFromDirectory(Path.Combine(FileSystem.Current.AppDataDirectory, "SKPrompts"), "Resolver");

            kernel.ImportFunctions(memoryPlugin);

            return kernel;
        }
    }
}