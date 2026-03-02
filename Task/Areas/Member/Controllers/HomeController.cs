using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Task.Areas.Member.Controllers
{
    [Area("Member")]
    [Authorize(AuthenticationSchemes = "MemberCookie")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}