// See https://aka.ms/new-console-template for more information

using Azure.Identity;
using Microsoft.SemanticKernel;
using static System.Runtime.InteropServices.JavaScript.JSType;

var azureIdentity = new VisualStudioCredential();

var kernel = Kernel.Builder
    .WithAzureTextCompletionService(
            deploymentName: "text-davinci-003",
            endpoint: "https://weu-jarvis-ai.openai.azure.com/",
            credentials: azureIdentity)
                .Build();

kernel.ImportSemanticFunctionsFromDirectory(Path.Combine(Directory.GetCurrentDirectory(), "SKPrompts"), "Author");

var context = kernel.CreateNewContext();

Console.Write("Factory kind: ");
var factory = await kernel.RunAsync(kernel.Functions.GetFunction("Author", "Factory"));

var factoryKind = factory.GetValue<string>()!;
context.Variables["FactoryKind"] = factoryKind;
Console.WriteLine(factoryKind);

var description = await kernel.RunAsync(kernel.Functions.GetFunction("Author", "ProductDescriptionAndFabricationProcess"), context.Variables);

Console.WriteLine(description.GetValue<string>());

Console.ReadLine();

context.Variables["NumberOfMachines"] = "7";
context.Variables["Description"] = description.GetValue<string>()!;

Console.Write("Imaginaing machines: ");
var machines = await kernel.RunAsync(kernel.Functions.GetFunction("Author", "Machine"), context.Variables);
  
var machineNames = machines.GetValue<string>()!.Trim().Split("\n");
Console.WriteLine(string.Join(", ", machineNames));

var potentialIssues = new Dictionary<string, Dictionary<string, List<string>>>();

using var writer = File.AppendText($"{factoryKind}.csv");

foreach (var machineName in machineNames)
{
    context = kernel.CreateNewContext();

    context.Variables["FactoryKind"] = factoryKind;
    context.Variables["MachineName"] = machineName;

    var issues = await kernel.RunAsync(kernel.Functions.GetFunction("Author", "IssueCause"), context.Variables);

    foreach (var issue in issues.GetValue<string>()!.Trim().Split("\n"))
    {
        context.Variables["IssueCause"] = issue;
        context.Variables["Description"] = description.GetValue<string>()!;

        var symptoms = await kernel.RunAsync(kernel.Functions.GetFunction("Author", "IssueSymptom"), context.Variables);

        foreach (var item in symptoms.GetValue<string>()!.Trim().Split("\n"))
        {
            context.Variables["Symptom"] = item;

            for (int i = 0; i < 5; i++)
            {
                var ticket = await kernel.RunAsync(kernel.Functions.GetFunction("Author", "Ticket"), context.Variables);

                writer.WriteLine($"{machineName};{ticket.GetValue<string>()!.Trim()}");
            }
        }
    }
}