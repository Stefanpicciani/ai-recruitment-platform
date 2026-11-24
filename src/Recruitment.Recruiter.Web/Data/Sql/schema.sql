-- =============================================
-- Recruiter Database
-- Azure SQL Database
-- =============================================

CREATE TABLE dbo.Candidates
(
    Id INT IDENTITY(1,1) NOT NULL,
    FullName NVARCHAR(160) NOT NULL,
    Email NVARCHAR(254) NOT NULL,
    Phone NVARCHAR(40) NULL,
    CreatedAt DATETIME2(7) NOT NULL
        CONSTRAINT DF_Candidates_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2(7) NULL,

    CONSTRAINT PK_Candidates PRIMARY KEY (Id)
);
GO

CREATE INDEX IX_Candidates_Email
    ON dbo.Candidates (Email);
GO

-- =============================================
-- Job Opportunities
-- =============================================

CREATE TABLE dbo.JobOpportunities
(
    Id INT IDENTITY(1,1) NOT NULL,
    Title NVARCHAR(160) NOT NULL,
    Department NVARCHAR(100) NOT NULL DEFAULT '',
    Location NVARCHAR(160) NOT NULL DEFAULT '',
    EmploymentType NVARCHAR(40) NOT NULL DEFAULT 'Full-time',
    Description NVARCHAR(MAX) NOT NULL,
    Requirements NVARCHAR(MAX) NOT NULL DEFAULT '',
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2(7) NOT NULL
        CONSTRAINT DF_JobOpportunities_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2(7) NULL,

    CONSTRAINT PK_JobOpportunities PRIMARY KEY (Id)
);
GO

CREATE INDEX IX_JobOpportunities_IsActive
    ON dbo.JobOpportunities (IsActive);
GO

-- =============================================
-- Required Skills
-- =============================================

CREATE TABLE dbo.RequiredSkills
(
    Id INT IDENTITY(1,1) NOT NULL,
    OpportunityId INT NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    IsMandatory BIT NOT NULL DEFAULT 1,

    CONSTRAINT PK_RequiredSkills PRIMARY KEY (Id),

    CONSTRAINT FK_RequiredSkills_JobOpportunities
        FOREIGN KEY (OpportunityId)
        REFERENCES dbo.JobOpportunities(Id)
        ON DELETE CASCADE
);
GO

CREATE INDEX IX_RequiredSkills_OpportunityId
    ON dbo.RequiredSkills (OpportunityId);
GO

-- =============================================
-- Resume Documents
-- =============================================

CREATE TABLE dbo.ResumeDocuments
(
    Id INT IDENTITY(1,1) NOT NULL,
    CandidateId INT NOT NULL,
    OpportunityId INT NOT NULL,
    OriginalFileName NVARCHAR(255) NOT NULL,
    StoredFileName NVARCHAR(255) NOT NULL,
    ContentType NVARCHAR(100) NOT NULL DEFAULT 'application/pdf',
    FileSize BIGINT NOT NULL,
    StoredPath NVARCHAR(1024) NOT NULL,
    SubmittedAt DATETIME2(7) NOT NULL
        CONSTRAINT DF_ResumeDocuments_SubmittedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_ResumeDocuments PRIMARY KEY (Id),

    CONSTRAINT FK_ResumeDocuments_Candidates
        FOREIGN KEY (CandidateId)
        REFERENCES dbo.Candidates(Id),

    CONSTRAINT FK_ResumeDocuments_JobOpportunities
        FOREIGN KEY (OpportunityId)
        REFERENCES dbo.JobOpportunities(Id),

    CONSTRAINT CK_ResumeDocuments_FileSize
        CHECK (FileSize >= 0)
);
GO

CREATE INDEX IX_ResumeDocuments_CandidateId
    ON dbo.ResumeDocuments (CandidateId);
GO

CREATE INDEX IX_ResumeDocuments_OpportunityId
    ON dbo.ResumeDocuments (OpportunityId);
GO

CREATE INDEX IX_ResumeDocuments_SubmittedAt
    ON dbo.ResumeDocuments (SubmittedAt DESC);
GO

-- =============================================
-- Resume Submissions
-- =============================================

CREATE TABLE dbo.ResumeSubmissions
(
    Id INT IDENTITY(1,1) NOT NULL,
    CandidateName NVARCHAR(160) NOT NULL,
    Email NVARCHAR(254) NOT NULL,
    OpportunityId INT NOT NULL,
    OriginalFileName NVARCHAR(255) NOT NULL,
    StoredPath NVARCHAR(1024) NOT NULL,
    SubmittedAt DATETIME2(7) NOT NULL
        CONSTRAINT DF_ResumeSubmissions_SubmittedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_ResumeSubmissions PRIMARY KEY (Id),

    CONSTRAINT FK_ResumeSubmissions_JobOpportunities
        FOREIGN KEY (OpportunityId)
        REFERENCES dbo.JobOpportunities(Id)
);
GO

CREATE INDEX IX_ResumeSubmissions_OpportunityId
    ON dbo.ResumeSubmissions (OpportunityId);
GO

CREATE INDEX IX_ResumeSubmissions_Email
    ON dbo.ResumeSubmissions (Email);
GO

CREATE INDEX IX_ResumeSubmissions_SubmittedAt
    ON dbo.ResumeSubmissions (SubmittedAt DESC);
GO
