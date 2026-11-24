# Guia do WebApp Recrutador: como funciona e passo a passo

Guia para retomar o projeto: o que cada peça faz, **onde a app lê a configuração**, onde mudar as connection
strings, como correr localmente e no Azure, e os erros que já apareceram e como se resolvem.

> Regra de ouro: **nunca** colocar passwords ou chaves em ficheiros do repositório (`appsettings*.json`, código,
> Dockerfile). Localmente vão para **user-secrets**; no Azure para **variáveis de ambiente** da Web App / Container App.

---

## 1. Visão geral

```
Browser ──► Recruitment.Recruiter.Web (ASP.NET Core Razor Pages)
                 │
                 ├── Azure SQL Database  (ConnectionStrings:recruiterdb)
                 │     tabelas: JobOpportunities, RequiredSkills, Candidates,
                 │              ResumeDocuments, ResumeSubmissions
                 │
                 └── Azure Blob Storage  (ConnectionStrings:resumes)
                       container privado "resumes", ficheiros em yyyy/MM/<guid>.pdf

Recruitment.AppHost (Aspire) ── só orquestra: arranca a Web localmente e passa-lhe
                                 as connection strings; em "publish" descreve os recursos Azure.
```

| Projeto | Para quê |
|---|---|
| `src/Recruitment.AppHost` | Orquestração Aspire. **É o projeto de arranque (F5).** Guarda as connection strings locais. |
| `src/Recruitment.ServiceDefaults` | OpenTelemetry, health checks, resiliência (partilhado). |
| `src/Recruitment.Recruiter.Web` | A aplicação: páginas, acesso a dados, armazenamento de CVs. |

---

## 2. Onde a app lê a configuração

### 2.1 As chaves que existem

| Chave | Obrigatória | Para quê | Lida em |
|---|---|---|---|
| `ConnectionStrings:recruiterdb` | Sim | Base de dados (Azure SQL, SQL Server ou LocalDB) | `Program.cs` → `AddSqlServerDbContext<RecruiterDbContext>("recruiterdb")` e `Data/DatabaseInitializer.cs` |
| `ConnectionStrings:resumes` | Sim fora de Development | Blob Storage dos CVs: connection string da Storage Account **ou** URL `https://<conta>.blob.core.windows.net/` | `Storage/ResumeStorageSetup.cs` → `AddResumeStorage()` |
| `ResumeStorage:ContainerName` | Não (`resumes`) | Nome do container de blobs | `Storage/ResumeStorageSetup.cs` |
| `ResumeStorage:AllowLocal` | Não (`true` só em Development) | Permite gravar CVs numa pasta local quando não há Blob | `Storage/ResumeStorageSetup.cs` |
| `ResumeStorage:LocalPath` | Não (`App_Data/resumes`) | Pasta local dos CVs | `Storage/ResumeStorage.cs` (`FileSystemResumeStorage`) |
| `Database:ApplySchemaOnStartup` | Não (`true`) | Criar BD / tabelas no arranque se não existirem | `Data/DatabaseInitializer.cs` |

Nas **variáveis de ambiente** os `:` passam a `__` (dois underscores):
`ConnectionStrings__recruiterdb`, `ConnectionStrings__resumes`, `ResumeStorage__ContainerName`, ...

### 2.2 De onde vêm os valores (ordem do ASP.NET Core)

Cada fonte sobrepõe a anterior; **a última ganha**:

1. `appsettings.json`
2. `appsettings.{Ambiente}.json` (ex.: `appsettings.Development.json`)
3. **user-secrets** (só quando o ambiente é `Development`)
4. **variáveis de ambiente**
5. argumentos da linha de comando

### 2.3 O caminho com Aspire (F5 no AppHost)

Ao correr pelo AppHost **há dois programas**, e as connection strings passam de um para o outro:

```
src/Recruitment.AppHost
  appsettings.json            ConnectionStrings:recruiterdb = LocalDB (valor por omissão, sem segredos)
  + user-secrets do AppHost   ConnectionStrings:recruiterdb / resumes = Azure (sobrepõem o LocalDB)
        │
        │  AppHost.cs: AddConnectionString("recruiterdb"/"resumes") + WithReference(...)
        ▼
  variáveis de ambiente injetadas no processo da Web:
        ConnectionStrings__recruiterdb=...   ConnectionStrings__resumes=...
        ▼
src/Recruitment.Recruiter.Web  lê-as como ConnectionStrings:recruiterdb / :resumes
```

Por isso, **localmente as connection strings mudam-se nos user-secrets do AppHost**, não no projeto Web.
No dashboard do Aspire (recurso `recruiter-web` → *Environment variables*) vês os valores que a Web recebeu.

Detalhes em `src/Recruitment.AppHost/AppHost.cs`:

- `recruiterdb` é sempre passada (vem do `appsettings.json` do AppHost ou dos user-secrets).
- `resumes` só é passada **se estiver definida**. Sem ela, em Development os CVs vão para
  `src/Recruitment.Recruiter.Web/App_Data/resumes`.
- `ResumeStorage:ContainerName`, se definida no AppHost, é passada como `ResumeStorage__ContainerName`.

---

## 3. Onde mudar as connection strings

| Cenário | Onde | Como |
|---|---|---|
| Local com LocalDB (por omissão) | `src/Recruitment.AppHost/appsettings.json` | Já configurado, não tem segredos. |
| Local contra Azure SQL / Blob | **user-secrets do AppHost** | Comandos abaixo (secção 3.1). |
| F5 direto no projeto Web (sem Aspire) | user-secrets do projeto Web | `dotnet user-secrets init --project src/Recruitment.Recruiter.Web` uma vez, depois os mesmos `set` com `--project src/Recruitment.Recruiter.Web`. |
| Azure (Web App, Container App, container) | Variáveis de ambiente do recurso | Secção 5. |
| Azure via `azd up` | Automático | O Aspire cria os recursos e define as variáveis. |

### 3.1 User-secrets do AppHost

Na raiz do repositório, trocando o texto entre `< >` pelos valores reais:

```powershell
dotnet user-secrets set "ConnectionStrings:recruiterdb" "<connection string Azure SQL>" --project src/Recruitment.AppHost
```

```powershell
dotnet user-secrets set "ConnectionStrings:resumes" "<connection string ou URL da Storage Account>" --project src/Recruitment.AppHost
```

Ver, editar e apagar:

```powershell
dotnet user-secrets list --project src/Recruitment.AppHost
```

```powershell
notepad "$env:APPDATA\Microsoft\UserSecrets\7389f34e-e410-4710-8199-44beb5f8f6c2\secrets.json"
```

```powershell
dotnet user-secrets remove "ConnectionStrings:resumes" --project src/Recruitment.AppHost
```

- O ficheiro fica em `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, **fora do repositório**,
  e é só deste PC (noutro PC é preciso voltar a defini-los). O `UserSecretsId` está no `Recruitment.AppHost.csproj`.
- O Visual Studio pode não mostrar "Manage User Secrets" no AppHost; usar os comandos acima.
- Para voltar ao LocalDB e à pasta local: apagar as duas chaves (`remove`).
- Depois de mudar: parar e voltar a correr a app.

### 3.2 Onde encontrar os valores no portal Azure

**Azure SQL** (`recruiterdb`): *SQL database → Settings → Connection strings → ADO.NET*. Substituir
`{your_password}` pela password real (sem as chavetas).

```text
# SQL authentication
Server=tcp:<servidor>.database.windows.net,1433;Initial Catalog=<base>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;

# Entra ID (sem password; localmente usa o login do Visual Studio / az login)
Server=tcp:<servidor>.database.windows.net,1433;Initial Catalog=<base>;Authentication=Active Directory Default;Encrypt=True;
```

Em *SQL server → Security → Networking*: adicionar o **IP do teu PC** (para correr localmente) e ativar
**Allow Azure services and resources to access this server** (para a Web App).

**Storage Account** (`resumes`):

- Connection string: *Security + networking → Access keys → key1 → Connection string → Show → copiar*.
  Copiar a **Connection string** completa, não só a *Key*.
  ```text
  DefaultEndpointsProtocol=https;AccountName=<conta>;AccountKey=<chave>;EndpointSuffix=core.windows.net
  ```
- Ou só o URL (sem chave): *Settings → Endpoints → Blob service*, `https://<conta>.blob.core.windows.net/`.
  Requer o role **Storage Blob Data Contributor** (*Access Control (IAM) → Add role assignment*) para o teu
  utilizador (local) ou para a Managed Identity da app (Azure). Ser Owner/Contributor **não chega**.

O container `resumes` **não precisa de ser criado**: a app cria-o como privado no primeiro upload.

---

## 4. Correr localmente: passo a passo

Pré-requisitos: .NET SDK 10, Visual Studio 2026 (ou `dotnet` CLI), SQL Server LocalDB (vem com o Visual Studio).
Não precisa de Docker.

1. Clonar o repositório e abrir `Recruitment.slnx`.
2. (Opcional, para usar Azure) Definir os user-secrets do AppHost (secção 3.1).
3. Projeto de arranque: **Recruitment.AppHost** → F5. Ou na linha de comandos:
   ```powershell
   dotnet run --project src/Recruitment.AppHost
   ```
4. Abre o dashboard do Aspire; clicar no endpoint do `recruiter-web`.
5. Confirmar no log do `recruiter-web` (dashboard → *Console*):
   - `CV storage: Azure Blob Storage, account '<conta>', container 'resumes'` (ou `local folder ...` sem Blob)
   - `Database '...' already exists.` e `Recruiter schema already present.` (ou `Applying Recruiter schema` na 1.ª vez)
6. Testar: **Oportunidades → Nova**, criar uma oportunidade ativa com competências; **Submeter CV** com um PDF.
   - Na BD: linhas novas em `Candidates`, `ResumeDocuments` e `ResumeSubmissions`.
   - No portal: *Storage Account → Containers → resumes → yyyy/MM/<guid>.pdf*.
   - Na página do candidato o CV descarrega-se pela app (o container é privado).

A pasta `test-files/` (ignorada pelo git) serve para guardar CVs de teste no PC.

---

## 5. Azure: como a app lê a configuração depois do deploy

O `secrets.json` **nunca vai para o Azure**. No Azure a app lê **as mesmas chaves** como variáveis de ambiente:

| Variável | Valor |
|---|---|
| `ConnectionStrings__recruiterdb` | connection string da Azure SQL |
| `ConnectionStrings__resumes` | connection string ou URL da Storage Account |
| `ResumeStorage__ContainerName` | (opcional) nome do container |

Onde defini-las, conforme o tipo de deploy:

| Deploy | Onde |
|---|---|
| **Web App publish** (Visual Studio, zip deploy, GitHub Actions) | *Web App → Settings → Environment variables* |
| **Container Registry + Web App for Containers** | *Web App → Settings → Environment variables*; adicionar também `WEBSITES_PORT=8080` |
| **Container Registry + Container App** | *Container App → Containers → Environment variables* (chaves em *Secrets* e referenciadas) |
| **`azd up` / `aspire deploy`** (Aspire publish mode) | Automático, com Managed Identity |

**Nunca** colocar as chaves dentro da imagem do container.

Gerar a imagem sem Dockerfile (.NET 10):

```powershell
dotnet publish src/Recruitment.Recruiter.Web -c Release /t:PublishContainer
```

No Azure o ambiente é `Production`: **sem `ConnectionStrings__resumes` a app não arranca** (de propósito, para
nunca gravar CVs no disco do servidor). É a primeira coisa a verificar se o deploy falhar no arranque.

### 5.1 Com Managed Identity (sem passwords)

1. Na Web App / Container App: *Identity → System assigned → On*.
2. Na base de dados (Query editor, com um admin Entra):
   ```sql
   CREATE USER [nome-da-app] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_owner ADD MEMBER [nome-da-app];
   ```
   (`db_owner` porque a app cria as tabelas no arranque; ver secção 6 para restringir.)
3. Na Storage Account: role **Storage Blob Data Contributor** para a identidade da app.
4. Connection strings: `Authentication=Active Directory Default` na SQL e só o URL no Blob.

### 5.2 `IsPublishMode` no AppHost

`builder.ExecutionContext.IsPublishMode` é `true` só quando o AppHost é executado por `azd up`/`azd deploy`/
`aspire publish`/`aspire deploy` (o CLI passa-lhe argumentos de publish). Nesse modo o AppHost não arranca nada:
descreve os recursos (App Service, Azure SQL, Storage) e o `azd` cria-os. Com F5 / `dotnet run` é sempre
Run mode. O *Publish* do Visual Studio para uma Web App **não usa o AppHost**.

`aspire publish -o ./publish-output` gera os ficheiros Bicep sem tocar no Azure, para ver o que seria criado.

---

## 6. O que acontece no arranque da Web

1. **Armazenamento de CVs** (`ResumeStorageSetup.AddResumeStorage`):
   `resumes` definido → Blob; senão pasta local se Development / `AllowLocal`; senão **erro e a app pára**.
2. **Base de dados** (`DatabaseInitializer.EnsureDatabaseAsync`), nunca apaga nada:
   - Abre a BD. Se a Azure SQL **serverless estiver em pausa** demora até ~1 min a acordar: a app usa um timeout
     mínimo de 60 s e tenta até 5 vezes (log `Database not reachable yet (attempt n/5 ...)`).
   - BD não existe (erro 4060): Azure SQL → `CREATE DATABASE`; LocalDB/SQL Server → cria, ou volta a anexar os
     ficheiros `.mdf/.ldf` se já existirem no disco.
3. **Tabelas** (`EnsureSchemaAsync`): se `dbo.JobOpportunities` não existe, executa `Data/Sql/schema.sql`
   numa transação. Desligar com `Database:ApplySchemaOnStartup=false` e correr o script à mão.

## 7. Fluxo de submissão de um CV

`Pages/Resumes/Submit.cshtml.cs`:

1. Valida o formulário e o ficheiro **antes de gravar seja o que for** (`Storage/ResumeRules.cs`):
   obrigatório, não vazio, máx. 10 MB, `.pdf/.doc/.docx`, e o conteúdo tem de ser mesmo PDF/DOC/DOCX
   (assinatura binária, não só a extensão).
2. Grava o ficheiro no Blob (`yyyy/MM/<guid>.ext`, content-type correto).
3. Candidato: reutilizado pelo email (minúsculas) ou criado.
4. Grava `ResumeDocuments` + `ResumeSubmissions` num único `SaveChanges`.
5. Se a BD falhar, **apaga o blob** acabado de gravar (sem ficheiros órfãos).

O `StoredPath` na BD é `resumes/yyyy/MM/<guid>.pdf` (Blob) ou `local/<guid>.pdf` (pasta local).

---

## 8. Problemas conhecidos e soluções

| Sintoma | Causa | Solução |
|---|---|---|
| `Connection Timeout Expired ... post-login phase` | Azure SQL serverless em pausa a acordar | Já tratado (60 s + 5 tentativas). Se persistir, `Connection Timeout=60` na connection string. |
| `Login failed for user '...'` (18456) | Utilizador/password errados (ex.: ficou `{your_password}`) | Corrigir no `secrets.json`; *SQL server → Reset password* se for o admin. |
| `Cannot open server ... client IP address ... not allowed` | Firewall da SQL | *SQL server → Networking* → adicionar o IP. |
| `Armazenamento de CVs não configurado` | Falta `ConnectionStrings:resumes` fora de Development | Definir a variável / user-secret. |
| `AuthorizationPermissionMismatch` (403) no Blob | Usando URL sem o role | Dar **Storage Blob Data Contributor** à identidade. |
| `Cannot create file '...RecruiterDb.mdf' because it already exists` | LocalDB perdeu o registo da BD | Já tratado: a app volta a anexar os ficheiros. |
| Build falha com ficheiros bloqueados em `bin/` | A app ainda está a correr | Parar a app (Shift+F5) e voltar a compilar. |
| Os valores de user-secrets não são usados | Definidos no projeto errado ou com o texto `<...>` | `dotnet user-secrets list --project src/Recruitment.AppHost`. |
