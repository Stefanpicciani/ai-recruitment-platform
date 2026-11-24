# AI-powered Recruitment Platform — WebApp Recrutador

ASP.NET Core (.NET 10, Razor Pages) + **Aspire 17** + **Azure SQL Database** + **Azure Blob Storage**, alojado em **Azure App Service**.

```
Recruitment.slnx
src/
  Recruitment.AppHost/          Orquestração Aspire (local + deploy para Azure)
  Recruitment.ServiceDefaults/  OpenTelemetry, health checks, resiliência
  Recruitment.Recruiter.Web/    WebApp Recrutador
    Data/                       Entidades, DbContext, DatabaseInitializer
    Data/Sql/schema.sql         Modelo da base de dados (fonte de verdade)
    Storage/                    Armazenamento de CVs (Blob no Azure / disco local)
    Pages/                      Oportunidades, Candidatos, Submeter CV
```

## Funcionalidades (Fluxo A — Submissão de Currículos)

- Criar / editar / desativar oportunidades, com competências (obrigatórias ou desejáveis) → `JobOpportunities`, `RequiredSkills`
- Submeter currículos (PDF/DOC/DOCX, máx. 10 MB) para uma oportunidade ativa
  - Candidato reutilizado por email (upsert) → `Candidates`
  - Documento → `ResumeDocuments` (normalizado, com `CandidateId`)
  - Registo plano → `ResumeSubmissions` (gravado na mesma transação)
- Consultar oportunidades, candidatos e respetivos CVs; download dos CVs pela app (o container de Blob é privado)

## Base de dados

O schema é aplicado automaticamente no arranque **se a tabela `dbo.JobOpportunities` não existir**,
executando `Data/Sql/schema.sql` (mantém os nomes exatos de constraints e índices).
Desligar com `Database__ApplySchemaOnStartup=false` e correr o script manualmente
(ex.: Query editor do Azure SQL no portal).

## Correr localmente

Não precisa de Docker. O AppHost usa a connection string `recruiterdb` de
`src/Recruitment.AppHost/appsettings.json` (por omissão SQL Server LocalDB). Os CVs ficam em
`src/Recruitment.Recruiter.Web/App_Data/resumes`.

```powershell
dotnet run --project src/Recruitment.AppHost
```

Para apontar para outra base (ex.: a Azure SQL), usar user-secrets no AppHost:

```powershell
dotnet user-secrets set "ConnectionStrings:recruiterdb" "Server=tcp:<server>.database.windows.net,1433;Database=recruiterdb;Authentication=Active Directory Default;Encrypt=True" --project src/Recruitment.AppHost
```

## Deploy para Azure

Em modo publish o AppHost cria:

| Recurso Aspire | Azure |
|---|---|
| `appservice` | App Service Plan + Web App (Linux) com Managed Identity |
| `sql` / `recruiterdb` | Azure SQL Server (Entra ID only) + Database |
| `storage` / `resumes` | Storage Account + Blob container privado |

Com o Azure Developer CLI:

```powershell
winget install microsoft.azd
azd auth login
azd init      # deteta o AppHost
azd up        # escolher subscrição e região
```

Ou com o Aspire CLI: `aspire deploy`.

A Web App liga-se à SQL e ao Storage com **Managed Identity** (sem passwords em configuração).
O Aspire dá à identidade `db_owner` na base (por isso o schema é criado no primeiro arranque) e
`Storage Blob Data Contributor` na conta de Storage. Para restringir permissões mais tarde:
correr `schema.sql` à mão, definir `Database__ApplySchemaOnStartup=false` e baixar o role.

## Próximas sessões

- WebApp Análise Técnica (Agente Verificar Adequação, Agente Parecer Técnico) + BD Análises e Pareceres
- Agente Comparador (scheduled trigger)
