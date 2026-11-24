# AI-powered Recruitment Platform — WebApp Recrutador

ASP.NET Core (.NET 10, Razor Pages) + **Aspire 17** + **Azure SQL Database** + **Azure Blob Storage**, alojado em **Azure App Service**.

> **Para retomar o projeto, ler primeiro o [guia passo a passo](docs/GUIA.md)**: como funciona, onde a app lê a
> configuração, onde mudar as connection strings (local e Azure), deploy e problemas conhecidos.

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

## Configuração (Azure SQL + Azure Blob Storage)

A app só precisa de duas connection strings:

| Chave | Para quê | Valores aceites |
|---|---|---|
| `ConnectionStrings:recruiterdb` | Base de dados | Azure SQL ou SQL Server/LocalDB |
| `ConnectionStrings:resumes` | Ficheiros dos CVs (Blob Storage) | Connection string da Storage Account **ou** URL `https://<conta>.blob.core.windows.net/` |
| `ResumeStorage:ContainerName` | Nome do container (opcional) | por omissão `resumes`; criado como **privado** se não existir |

Regras de segurança:

- Sem `ConnectionStrings:resumes`, os CVs só vão para a pasta local `App_Data/resumes` em **Development**.
  Em qualquer outro ambiente a app **recusa arrancar**, para nunca gravar CVs no disco do servidor.
- Nunca colocar chaves/passwords em `appsettings.json` (o repositório é público):
  localmente usar **user-secrets**, no Azure usar as **App Settings / Connection strings** da Web App
  ou, melhor, Managed Identity (sem chaves).

### Azure SQL

```text
# Entra ID (recomendado; localmente usa o teu login do Visual Studio / az login)
Server=tcp:<servidor>.database.windows.net,1433;Database=<base>;Authentication=Active Directory Default;Encrypt=True;

# SQL authentication
Server=tcp:<servidor>.database.windows.net,1433;Database=<base>;User ID=<user>;Password=<password>;Encrypt=True;
```

No portal, em *SQL server → Networking*, permitir o IP do teu PC (para correr localmente) e
"Allow Azure services" (para a Web App). As tabelas são criadas no primeiro arranque se não existirem.

### Azure Blob Storage

```text
# Connection string (Storage Account → Access keys)
DefaultEndpointsProtocol=https;AccountName=<conta>;AccountKey=<chave>;EndpointSuffix=core.windows.net

# Ou só o URL (Managed Identity no Azure / o teu login localmente) — requer o role
# "Storage Blob Data Contributor" na Storage Account para essa identidade
https://<conta>.blob.core.windows.net/
```

No arranque a app escreve no log onde vai guardar os CVs, por exemplo
`CV storage: Azure Blob Storage, account '<conta>', container 'resumes'`.

## Correr localmente

Não precisa de Docker. Por omissão usa LocalDB e a pasta `App_Data/resumes`.

```powershell
dotnet run --project src/Recruitment.AppHost
```

### User-secrets (Azure SQL + Blob Storage a partir do PC)

Para usar os recursos do Azure a partir do PC, guardar as connection strings em user-secrets do AppHost.
Correr na raiz do repositório, **substituindo o texto entre `< >` pelos valores reais**:

```powershell
dotnet user-secrets set "ConnectionStrings:recruiterdb" "<connection string Azure SQL>" --project src/Recruitment.AppHost
```

```powershell
dotnet user-secrets set "ConnectionStrings:resumes" "<connection string ou URL da Storage Account>" --project src/Recruitment.AppHost
```

Onde encontrar os valores no portal:

- **Azure SQL**: *SQL database → Settings → Connection strings → ADO.NET* (substituir `{your_password}`).
- **Storage Account**: *Security + networking → Access keys → key1 → Connection string* (Show + copiar),
  ou o URL em *Settings → Endpoints → Blob service* (`https://<conta>.blob.core.windows.net/`, requer o role
  "Storage Blob Data Contributor" para o teu utilizador).

Ver, editar ou apagar os valores guardados:

```powershell
dotnet user-secrets list --project src/Recruitment.AppHost
```

```powershell
notepad "$env:APPDATA\Microsoft\UserSecrets\7389f34e-e410-4710-8199-44beb5f8f6c2\secrets.json"
```

```powershell
dotnet user-secrets remove "ConnectionStrings:resumes" --project src/Recruitment.AppHost
```

Os user-secrets ficam fora do repositório (`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`) e
nunca são enviados para o Azure. Sem estas chaves a app volta a usar LocalDB e a pasta `App_Data/resumes`.
Depois de alterar, reiniciar a app; no log de arranque aparece `CV storage: Azure Blob Storage, account '...'`.

Sem Aspire (ex.: F5 direto no projeto Web), as mesmas chaves funcionam em user-secrets do projeto Web
(`--project src/Recruitment.Recruiter.Web`).

### Web App criada à mão no portal (sem azd)

Em *Web App → Settings → Environment variables*:

| Nome | Valor |
|---|---|
| `ConnectionStrings__recruiterdb` | connection string da Azure SQL |
| `ConnectionStrings__resumes` | connection string ou URL da Storage Account |
| `ResumeStorage__ContainerName` | (opcional) nome do container |

## Deploy para Azure

Em modo publish o AppHost cria:

| Recurso Aspire | Azure |
|---|---|
| `appservice` | App Service Plan + Web App (Linux) com Managed Identity |
| `sql` / `recruiterdb` | Azure SQL Server (Entra ID only) + Database |
| `storage` / `resumes` | Storage Account (o container privado `resumes` é criado pela app) |

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
