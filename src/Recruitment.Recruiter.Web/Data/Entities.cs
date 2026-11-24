namespace Recruitment.Recruiter.Web.Data;

public class Candidate
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<ResumeDocument> Resumes { get; set; } = [];
}

public class JobOpportunity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Department { get; set; } = "";
    public string Location { get; set; } = "";
    public string EmploymentType { get; set; } = "Full-time";
    public string Description { get; set; } = "";
    public string Requirements { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<RequiredSkill> RequiredSkills { get; set; } = [];
    public List<ResumeDocument> Resumes { get; set; } = [];
}

public class RequiredSkill
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public string Name { get; set; } = "";
    public bool IsMandatory { get; set; } = true;

    public JobOpportunity? Opportunity { get; set; }
}

public class ResumeDocument
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public int OpportunityId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string ContentType { get; set; } = "application/pdf";
    public long FileSize { get; set; }
    public string StoredPath { get; set; } = "";
    public DateTime SubmittedAt { get; set; }

    public Candidate? Candidate { get; set; }
    public JobOpportunity? Opportunity { get; set; }
}

public class ResumeSubmission
{
    public int Id { get; set; }
    public string CandidateName { get; set; } = "";
    public string Email { get; set; } = "";
    public int OpportunityId { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public DateTime SubmittedAt { get; set; }

    public JobOpportunity? Opportunity { get; set; }
}
