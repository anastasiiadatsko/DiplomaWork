using Microsoft.AspNetCore.Mvc;

namespace HabitFlow.Web.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            if (this.HttpContext.Session.GetString("UserId") == null)
            {
                return this.RedirectToAction("Login", "Auth");
            }

            this.ViewBag.UserName = this.HttpContext.Session.GetString("UserName");
            return this.View();
        }
    }
}