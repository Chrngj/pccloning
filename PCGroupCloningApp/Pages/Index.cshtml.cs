// Pages/Index.cshtml.cs
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PCGroupCloningApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            // Simple page load
        }
    }
}