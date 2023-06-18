// See https://aka.ms/new-console-template for more information

using Azure.Identity;
using Microsoft.SemanticKernel;

var azureIdentity = new VisualStudioCredential();

var kernel = Kernel.Builder
    .WithAzureTextCompletionService(
            deploymentName: "text-davinci-003",
            endpoint: "https://weu-jarvis-ai.openai.azure.com/",
            credentials: azureIdentity)
                .Build();

kernel.ImportSemanticSkillFromDirectory(Path.Combine(Directory.GetCurrentDirectory(), "SKPrompts"), "Author");

var context = kernel.CreateNewContext();

Console.Write("Factory kind: ");
var factory = await kernel.Func("Author", "Factory")
                    .InvokeAsync();

var factoryKind = factory.Result.Trim();
context["FactoryKind"] = factoryKind;
Console.WriteLine(factoryKind);

var description = await kernel.Func("Author", "ProductDescriptionAndFabricationProcess")
                                .InvokeAsync(context);

Console.WriteLine(description.Result);

Console.ReadLine();

context["NumberOfMachines"] = "7";
context["Description"] = description.Result;

Console.Write("Imaginaing machines: ");
var machines = await kernel.Func("Author", "Machine")
                    .InvokeAsync(context);

var machineNames = machines.Result.Trim().Split("\n");
Console.WriteLine(string.Join(", ", machineNames));

var potentialIssues = new Dictionary<string, Dictionary<string, List<string>>>();

using var writer = File.AppendText($"{factoryKind}.csv");

foreach (var machineName in machineNames)
{
    context = kernel.CreateNewContext();

    context["FactoryKind"] = factoryKind;
    context["MachineName"] = machineName;

    var issues = await kernel.Func("Author", "IssueCause")
                    .InvokeAsync(context);

    foreach (var issue in issues.Result.Trim().Split("\n"))
    {
        context["IssueCause"] = issue;
        context["Description"] = description.Result;

        var symptoms = await kernel.Func("Author", "IssueSymptom")
                                    .InvokeAsync(context);

        foreach (var item in symptoms.Result.Trim().Split("\n"))
        {
            context["Symptom"] = item;

            for (int i = 0; i < 5; i++)
            {
                var ticket = await kernel.Func("Author", "Ticket")
                                        .InvokeAsync(context);

                writer.WriteLine($"{machineName};{ticket.Result.Trim()}");
            }
        }
    }
}