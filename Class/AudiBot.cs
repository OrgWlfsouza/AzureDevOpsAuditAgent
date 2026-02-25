using AzureDevOpsAuditAgent.Class;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

public class AuditBot : ActivityHandler
{
    private readonly AzureDevOpsService _service;

    public AuditBot(AzureDevOpsService service)
    {
        _service = service;
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var text = turnContext.Activity.Text?.ToLower();

        if (text.Contains("quantos projetos"))
        {
            var count = await _service.GetProjectCountAsync();
            await turnContext.SendActivityAsync($"Existem {count} projetos na organização.");
        }
        else if (text.Contains("quantos usuários"))
        {
            var count = await _service.GetUserCountAsync();
            await turnContext.SendActivityAsync($"Existem {count} usuários cadastrados.");
        }
        else
        {
            await turnContext.SendActivityAsync("Desculpe, não entendi. Pergunte sobre projetos, usuários ou licenças.");
        }
    }
}
