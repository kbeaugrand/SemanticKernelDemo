namespace Copilot.Pages
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.Memory;
    using Microsoft.SemanticKernel.Plugins.Memory;

    public partial class Index
    {
        [Inject]
        private IKernel _kernel { get; set; }

        [Inject]
        private ISemanticTextMemory _memory { get; set; }

        private string _result;

        private string _userPrompt;

        private IEnumerable<string> _languages = new List<string> { "Anglais", "Espagnol", "Français", "Allemand", "Italien" }; 

        private string _language = "Français";

        private async Task FindHistory()
        {
            _result = "Loading...";

            var relatedIssues = _memory.SearchAsync("maintenance", _userPrompt, 10);
            var result = "";

            await foreach (var item in relatedIssues)
            {
                result += item.Metadata.Text + "\n";
                result += "--------------------------\n";
            }

            _result = result;
        }

        private async Task Encode()
        {
            var context = _kernel.CreateNewContext();

            context.Variables["INPUT"] = _userPrompt;

            _result = "Loading...";

            var solution = await _kernel.RunAsync(_kernel.Functions.GetFunction("Encoder", "AnomalyEncode"), context.Variables);

            _result = solution.GetValue<string>();
        }

        private async Task GroupHistory()
        {
            var context = _kernel.CreateNewContext();

            context.Variables["INPUT"] = _userPrompt;
            context.Variables[TextMemoryPlugin.CollectionParam] = "maintenance";
            context.Variables[TextMemoryPlugin.LimitParam] = "5";

            _result = "Loading...";

            var solution = await _kernel.RunAsync(_kernel.Functions.GetFunction("Resolver", "GroupHistory"), context.Variables);

            _result = solution.GetValue<string>();
        }

        private async Task FindSolution()
        {
            var context = _kernel.CreateNewContext();

            context.Variables["INPUT"] = _userPrompt;
            context.Variables[TextMemoryPlugin.CollectionParam] = "maintenance";
            context.Variables[TextMemoryPlugin.LimitParam] = "5";
            context.Variables["LANGUAGE"] = _language;

            _result = "Loading...";

            var solution = await _kernel.RunAsync(_kernel.Functions.GetFunction("Resolver", "HelpMe"), context.Variables);

            _result = solution.GetValue<string>();
        }


        private void Clear()
        {
            _result = "";
        }
    }
}