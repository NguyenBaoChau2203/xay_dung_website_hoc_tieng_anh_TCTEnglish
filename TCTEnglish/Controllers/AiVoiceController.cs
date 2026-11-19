using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TCTEnglish.Services.AI.Voice;
using TCTEnglish.ViewModels.AI.Voice;

namespace TCTEnglish.Controllers;

[Authorize]
public sealed class AiVoiceController(IRealtimeVoiceClient provider) : Controller
{
    [HttpGet("AI/Voice")]
    public IActionResult Index() => View(new VoicePageVm(provider.IsConfigured));
}
