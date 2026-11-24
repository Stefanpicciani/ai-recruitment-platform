using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recruitment.Recruiter.Web.Data;

namespace Recruitment.Recruiter.Web.Pages.Opportunities;

public class IndexModel(RecruiterDbContext db) : PageModel
{
    public record Row(int Id, string Title, string Department, string Location, string EmploymentType, bool IsActive, int Skills, int Resumes, DateTime CreatedAt);

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public bool ShowInactive { get; set; }

    public List<Row> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = db.JobOpportunities.AsNoTracking();
        if (!ShowInactive) query = query.Where(o => o.IsActive);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            query = query.Where(o => o.Title.Contains(q) || o.Department.Contains(q) || o.Location.Contains(q));
        }

        Items = await query.OrderByDescending(o => o.CreatedAt)
            .Select(o => new Row(o.Id, o.Title, o.Department, o.Location, o.EmploymentType, o.IsActive,
                o.RequiredSkills.Count, o.Resumes.Count, o.CreatedAt))
            .ToListAsync();
    }
}
