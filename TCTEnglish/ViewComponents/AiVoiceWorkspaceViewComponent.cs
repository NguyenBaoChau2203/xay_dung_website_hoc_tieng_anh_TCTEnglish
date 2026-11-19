using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.AI.Voice;
using TCTEnglish.ViewModels.AI.Voice;

namespace TCTEnglish.ViewComponents;

public sealed class AiVoiceWorkspaceViewComponent(IRealtimeVoiceClient provider) : ViewComponent
{
    public IViewComponentResult Invoke(bool inline = false) =>
        View("~/Views/AiVoice/_Workspace.cshtml", new VoicePageVm(provider.IsConfigured, inline));
}
