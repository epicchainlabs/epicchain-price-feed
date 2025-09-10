using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PriceFeed.R3E.Analytics.Pages
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
            _logger.LogInformation("Analytics dashboard accessed at {Time}", DateTime.UtcNow);
        }
    }
}