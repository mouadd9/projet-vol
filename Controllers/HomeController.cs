using Microsoft.AspNetCore.Mvc;

namespace MoteurDeRechercheDeVol.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Search", "Flight");
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
