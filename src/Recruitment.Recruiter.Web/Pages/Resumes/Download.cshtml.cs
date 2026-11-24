using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recruitment.Recruiter.Web.Data;
using Recruitment.Recruiter.Web.Storage;

namespace Recruitment.Recruiter.Web.Pages.Resumes;

public class DownloadModel(RecruiterDbContext db, IResumeStorage storage) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var doc = await db.ResumeDocuments.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (doc is null) return NotFound();

        var stream = await storage.OpenReadAsync(doc.StoredPath, ct);
        if (stream is null) return NotFound();

        return File(stream, doc.ContentType, doc.OriginalFileName);
    }
}
