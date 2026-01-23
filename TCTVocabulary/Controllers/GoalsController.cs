using Microsoft.AspNetCore.Mvc;

namespace TCTVocabulary.Controllers
{
    public class GoalsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
