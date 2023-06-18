namespace Copilot.Pages
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.CoreSkills;

    public partial class Index
    {
        [Inject]
        private IKernel _kernel { get; set; }

        private string _result;

        private string _userPrompt;

        private async Task FindSolution()
        {
            var context = _kernel.CreateNewContext();

            context["INPUT"] = _userPrompt;
            context[TextMemorySkill.CollectionParam] = "maintenance";
            context[TextMemorySkill.LimitParam] = "10";

            var solution = await _kernel.Func("Resolver", "HelpMe")
                                .InvokeAsync(context);

            _result = solution.Result;
        }
    }
}
