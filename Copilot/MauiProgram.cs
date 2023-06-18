using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.Sqlite;
using Microsoft.SemanticKernel.CoreSkills;
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
                                EncodeDatabase();
                            });
                    });
#endif
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
            _ = builder.Services.AddSingleton<IKernel>((_) => CreateKernel().Result);

            return builder.Build();
        }

        private static async Task CopySKPrompts()
        {
            await CopySKSkillToAppData("Encoder", "AnomalyEncode");
            await CopySKSkillToAppData("Resolver", "HelpMe");
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
                var lineNumber = 0;

                var kernel = await CreateKernel();
                var encodingFunc = kernel.Func(skillName: "Encoder", functionName: "AnomalyEncode");

                foreach (var line in lines)
                {
                    string userPrompt = null;

                    try
                    {
                        userPrompt = line.Split(";", StringSplitOptions.TrimEntries)[1];
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine($"Failed to parse line {lineNumber}");
                        continue;
                    }

                    lineNumber++;

                    var ctx = kernel.CreateNewContext();

                    if ((await ctx.Memory.GetAsync(collection: "maintenance", key: lineNumber.ToString())) != null)
                    {
                        continue;
                    }

                    ctx["INPUT"] = userPrompt;

                    var encoding = await encodingFunc.InvokeAsync(ctx);

                    if (encoding.ErrorOccurred)
                    {
                        continue;
                    }

                    Debug.WriteLine($"Saving line {lineNumber}...");

                    await ctx.Memory.SaveInformationAsync(
                            collection: "maintenance",
                            text: encoding.Result,
                            id: lineNumber.ToString());
                }

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

        private static async Task<IKernel> CreateKernel()
        {
            var azureIdentity = new VisualStudioCredential();

            var memoryStore = await SqliteMemoryStore.ConnectAsync("memory.db");

            var kernel = Kernel.Builder
                    .WithAzureTextCompletionService(
                        deploymentName: "text-davinci-003",
                        endpoint: "https://weu-jarvis-ai.openai.azure.com/",
                        credentials: azureIdentity)
                    .WithAzureTextEmbeddingGenerationService(
                        deploymentName: "text-embedding-ada-002",
                        endpoint: "https://weu-jarvis-ai.openai.azure.com/",
                        credential: azureIdentity)
                .WithMemoryStorage(memoryStore)
            .Build();

            kernel.ImportSemanticSkillFromDirectory(Path.Combine(FileSystem.Current.AppDataDirectory, "SKPrompts"), "Encoder");
            kernel.ImportSemanticSkillFromDirectory(Path.Combine(FileSystem.Current.AppDataDirectory, "SKPrompts"), "Resolver");

            kernel.ImportSkill(new TextMemorySkill(), "TextMemory");

            return kernel;
        }
    }
}