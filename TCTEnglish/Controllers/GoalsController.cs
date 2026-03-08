using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TCTVocabulary.Controllers
{
    [Authorize]
    public class GoalsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
