# 🚀 NoteesApp API - Backend

API RESTful para gerenciamento de notas com organização por pastas e tags, desenvolvida com .NET 10, Entity Framework Core e PostgreSQL.

[![Status](https://img.shields.io/badge/status-production-success)](https://noteesapp-be.onrender.com)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

## 📋 Índice

- [Funcionalidades](#-funcionalidades)
- [Tecnologias](#-tecnologias)
- [Arquitetura](#-arquitetura)
- [Como Rodar Localmente](#-como-rodar-localmente)
- [Testes](#-testes)
- [Deploy](#-deploy)
- [Segurança](#-segurança)

---

## ✨ Funcionalidades

- ✅ **Autenticação própria com JWT**: cadastro, login, refresh token com rotação e logout
- ✅ **Notas por usuário**: cada pessoa só enxerga e altera as próprias notas
- ✅ **CRUD Completo** de notas (Criar, Ler, Atualizar, Deletar)
- ✅ **Organização por Pastas** e Tags
- ✅ **Notas Fixadas** (pin/unpin)
- ✅ **Busca e Filtros** por pasta e tag
- ✅ **Validações** de conteúdo (título 3-200 chars, conteúdo 10-1000 chars)
- ✅ **Paginação opcional** (`page`/`pageSize`) e ordenação (fixadas primeiro, depois mais recentes)
- ✅ **Swagger/OpenAPI** para documentação
- ✅ **Health checks**: liveness (`/api/health`) e readiness com banco (`/api/health/ready`)
- ✅ **Logs estruturados** com ILogger
- ✅ **Tratamento de Erros** global com middleware
- ✅ **Testes Unitários** com xUnit e Moq

---

## 🛠 Tecnologias

| Categoria | Tecnologia |
|-----------|------------|
| **Framework** | .NET 10 |
| **Linguagem** | C# 13 |
| **ORM** | Entity Framework Core 10 |
| **Banco de Dados** | PostgreSQL (Neon) |
| **Testes** | xUnit, Moq, coverlet |
| **Documentação** | Swagger/OpenAPI |
| **Mapeamento** | Manual (extension methods em `Mappers/NoteMapper.cs`) |
| **Deploy** | Render (Docker) |
| **Monitoramento** | Health Checks |

---

## 🏗 Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                    NoteesApp API                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────────┐    │
│  │ Controllers │ → │  Services   │ → │   Repositories  │    │
│  │  (HTTP)     │   │ (Business)  │   │   (Data Access) │    │
│  └─────────────┘   └─────────────┘   └────────┬────────┘    │
│                                               │             │
│                                        ┌──────▼──────┐      │
│                                        │ PostgreSQL  │      │
│                                        │   (Neon)    │      │
│                                        └─────────────┘      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Camadas

| Camada | Responsabilidade |
|--------|-----------------|
| **Controllers** | Receber requisições HTTP, validar ModelState, retornar respostas padronizadas |
| **Services** | Regras de negócio, validações, logging, orquestração |
| **Repositories** | Acesso ao banco de dados, queries EF Core |
| **Mappers** | Conversão entre DTOs e entidade (`NoteMapper`) |
| **Models** | Entidades do domínio (Note) |
| **DTOs** | Objetos de transferência de dados (CreateNoteDto, UpdateNoteDto, etc.) |

---

## 🚀 Como Rodar Localmente

### Pré-requisitos

- .NET 10 SDK
- PostgreSQL (local ou Neon)
- Git

### Passos

```bash
# 1. Clonar repositório
git clone https://github.com/PedroBeltraoDev/NoteesApp-BE.git
cd NoteesApp-BE

# 2. Copiar arquivo de exemplo
cp appsettings.json.example appsettings.json

# 3. Configurar connection string no appsettings.json
# Editar: ConnectionStrings:DefaultConnection

# 4. Instalar dependências
dotnet restore NotesApp.sln

# 5. Aplicar migrações
dotnet ef database update

# 6. Rodar a API
dotnet run

# 7. Acessar Swagger
http://localhost:5216/swagger
```

> **Migration `AddColumnLimitsAndNoteIndexes`**: limita `Folder` a 100 e `Content` a 1000 caracteres
> (`varchar`) e cria índices para a listagem. Antes de alterar as colunas ela confere se existe nota acima
> desses limites; se existir, **aborta com uma mensagem** (nada é truncado) e você precisa ajustar esses
> registros antes de rodar `dotnet ef database update` de novo. Em produção o banco não é migrado
> automaticamente: aplique a migration como passo do deploy.

### Variáveis de Ambiente

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=seu-host.neon.tech;Database=seu-db;Username=seu-user;Password=sua-senha;SSL Mode=Require"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Jwt": {
    "Key": "uma-chave-aleatoria-com-no-minimo-32-caracteres",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 14
  },
  "AllowedHosts": "*"
}
```

> **`Jwt:Key` é obrigatória**: sem ela (ou com menos de 32 caracteres) a API **não sobe**. Gere uma com
> `openssl rand -base64 48` (ou `python -c "import secrets; print(secrets.token_urlsafe(48))"`).
> Em produção ela vem da variável `Jwt__Key`, nunca do repositório.

> **Migration `AddUsersAndNoteOwnership` apaga as notas existentes.** As notas antigas não têm dono,
> então a migration executa `DELETE FROM "Notes"` antes de criar `Notes.UserId` (NOT NULL). Não há como
> desfazer. Aplique primeiro em uma *branch* do Neon (ou faça backup) se houver algo a preservar.

---

## 🧪 Testes

### Rodar Testes

```bash
# Todos os testes (a raiz tem a solution e o projeto da API, então informe a solution)
dotnet test NotesApp.sln

# Com detalhes
dotnet test NotesApp.sln --verbosity normal

# Com code coverage
dotnet test NotesApp.sln --collect:"XPlat Code Coverage"

# Apenas NoteServiceTests
dotnet test NotesApp.sln --filter "FullyQualifiedName~NoteServiceTests"
```

### Estrutura de Testes

Os testes ficam em um projeto separado, `NotesApp.Api.Tests`:

```
NotesApp.Api.Tests/
├── NoteServiceTests.cs                 # regras de negócio e isolamento entre usuários (repositório mockado)
├── AuthServiceTests.cs                 # registro, login, refresh (rotação/reuso) e logout (repositório mockado)
├── TokenServiceTests.cs                # JWT (claims, expiração, algoritmo) e hash do refresh token
├── NoteMapperTests.cs                  # mapeamento DTO <-> entidade
├── ExceptionHandlingMiddlewareTests.cs # tradução de exceções em HTTP
├── NoteRepositoryQueryTests.cs         # SQL gerado para o Npgsql (filtro por dono, tags, paginação)
├── ApiIntegrationTests.cs              # API real: 401 sem/ com token inválido, paginação, health
├── AuthFlowTests.cs                    # API real + banco em memória: fluxo de auth e isolamento de notas
└── TestHosts.cs                        # fábricas de host de teste
```

---

### Endpoints

Todas as rotas de `/api/notes` e `/api/auth/me` exigem o header `Authorization: Bearer <accessToken>`.

| Método | Endpoint | Auth | Descrição |
|--------|----------|------|-----------|
| `POST` | `/api/auth/register` | não | Cria a conta e já devolve os tokens (`201`; `409` se o e-mail já existe) |
| `POST` | `/api/auth/login` | não | Devolve os tokens (`401` para e-mail/senha inválidos) |
| `POST` | `/api/auth/refresh` | não | Troca o refresh token por um novo par de tokens |
| `POST` | `/api/auth/logout` | não | Revoga o refresh token informado |
| `GET` | `/api/auth/me` | sim | Dados do usuário autenticado |
| `GET` | `/api/health` | não | Liveness: o processo está de pé (não acessa o banco) |
| `GET` | `/api/health/ready` | não | Readiness: verifica o banco; responde `503` se estiver inacessível |
| `GET` | `/api/notes` | sim | Listar notas (filtros opcionais: `?folder=X&tag=Y`; paginação opcional: `?page=1&pageSize=20`) |
| `GET` | `/api/notes/{id}` | sim | Buscar nota por ID |
| `POST` | `/api/notes` | sim | Criar nova nota |
| `PUT` | `/api/notes/{id}` | sim | Atualizar nota (o `id` vem só da rota; não enviar no body) |
| `DELETE` | `/api/notes/{id}` | sim | Excluir nota |
| `GET` | `/api/notes/folders` | sim | Listar pastas únicas (só do usuário) |
| `GET` | `/api/notes/tags` | sim | Listar tags únicas (só do usuário) |

Fixar/desafixar uma nota é feito alterando `isPinned` no `PUT`.

### Autenticação

Fluxo para o frontend:

1. `POST /api/auth/register` ou `POST /api/auth/login` com `{ "email", "password" }` (senha de 8 a 128 caracteres).
   A resposta traz `data` com:
   ```json
   {
     "accessToken": "eyJ...",              // JWT, vale 15 min; vai em Authorization: Bearer
     "accessTokenExpiresAt": "2026-10-20T12:15:00Z",
     "refreshToken": "…",                  // opaco, vale 14 dias, uso único
     "refreshTokenExpiresAt": "2026-11-03T12:00:00Z",
     "user": { "id": "…", "email": "pessoa@exemplo.com" }
   }
   ```
2. Envie `Authorization: Bearer <accessToken>` em toda chamada a `/api/notes/*`.
3. Ao receber `401` (mensagem `Token expirado`), chame `POST /api/auth/refresh` com `{ "refreshToken" }`.
   A resposta é um **novo par de tokens**: guarde o novo `refreshToken`, o anterior deixa de valer.
4. Se o refresh também falhar com `401`, a sessão acabou: volte para a tela de login.
5. No logout, chame `POST /api/auth/logout` com `{ "refreshToken" }` e apague os tokens locais.

Regras importantes:

- **Dono das notas**: vem sempre do token. Qualquer `userId` enviado no corpo é ignorado. Nota de outro usuário
  responde `404` (não `403`), como se não existisse.
- **Reuso de refresh token**: apresentar de novo um refresh token já usado é tratado como roubo e **revoga todas
  as sessões** do usuário. Por isso o frontend deve evitar dois refreshes simultâneos com o mesmo token
  (ex.: duas abas): serialize a renovação.
- **Senhas** são guardadas com PBKDF2 (`PasswordHasher` do ASP.NET Core); refresh tokens ficam no banco só como hash SHA-256.
- **Limite de tentativas**: `register`, `login`, `refresh` e `logout` aceitam 10 requisições por minuto por IP
  (`RateLimiting:AuthPermitLimit`); acima disso a resposta é `429`.
- **Erros** seguem o formato `{ "success": false, "message": "…", "errors": { campo: [mensagens] } }`.

### Paginação

- Sem `page` nem `pageSize`, `GET /api/notes` devolve **todas** as notas (comportamento anterior).
- Com qualquer um dos dois, devolve só a página pedida e preenche `pagination` na resposta
  (`page` ≥ 1, `pageSize` de 1 a 100, padrão 20). Valores fora desses limites retornam `400`.
- A ordem é estável entre páginas: fixadas primeiro, depois mais recentes, com o `id` como desempate.
- O filtro por `tag` ignora maiúsculas/minúsculas e `GET /api/notes/tags` agrupa tags que só diferem por isso.

```json
{
  "success": true,
  "data": [ /* notas da página */ ],
  "pagination": { "page": 2, "pageSize": 5, "totalItems": 11, "totalPages": 3 }
}
```

> Use `/api/health` para pings frequentes (ex.: manter o Render acordado). `/api/health/ready`
> abre conexão com o banco e, num Neon em *scale-to-zero*, acorda o banco a cada chamada.

### Exemplo de Request/Response

**POST /api/notes**
```json
// Request
{
  "title": "Minha Nota",
  "content": "Conteúdo da nota com mais de 10 caracteres",
  "folder": "Trabalho",
  "tags": ["importante", "urgente"],
  "isPinned": true
}

// Response (201 Created)
{
  "success": true,
  "message": "Nota criada com sucesso",
  "data": {
    "id": 1,
    "title": "Minha Nota",
    "content": "Conteúdo da nota...",
    "folder": "Trabalho",
    "tags": ["importante", "urgente"],
    "isPinned": true,
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:30:00Z"
  }
}
```

---

## 🌐 Deploy

### Backend (Vercel)

A Vercel não tem runtime .NET nativo: a API roda como **Function com imagem de contêiner**. A Vercel detecta o
`Dockerfile.vercel` na raiz (mesmo build do `Dockerfile`: imagens `dotnet/sdk:10.0` e `dotnet/aspnet:10.0`) e roteia o
tráfego para o contêiner, que escuta na porta da variável `PORT`. O `.dockerignore` mantém testes, `bin/obj` e arquivos
de configuração local fora da imagem.

1. Crie o projeto na Vercel ligado a este repositório, com o preset **Container**.
2. Em **Settings > Deployment Protection**, desative a *Vercel Authentication* (a API é pública; quem protege as rotas é o JWT).
3. Defina as variáveis da tabela abaixo (Settings > Environment Variables) e faça um *redeploy*: variáveis novas só valem
   para deploys novos.
4. Aplique as migrations no banco (veja abaixo) e confira `GET /api/health/ready` (200 = banco acessível).

O `Dockerfile` original continua válido para rodar localmente (`docker build .`) ou em outros provedores. Mantenha o
`Dockerfile` e o `Dockerfile.vercel` em sincronia.

> Funções na Vercel são sem estado e escalam a zero: a primeira requisição depois de um tempo parado pode demorar
> (partida a frio do .NET). `/api/health` não acessa o banco e é a melhor rota para checagens simples.

### Variáveis de Ambiente (Vercel)

| Variável | Valor |
|----------|-------|
| `ConnectionStrings__DefaultConnection` | (string do Neon; marque como *sensitive*) |
| `Jwt__Key` | **Obrigatória.** Chave aleatória de no mínimo 32 caracteres (*sensitive*). A API não sobe sem ela. Trocá-la derruba todas as sessões |
| `Cors__AllowedOrigins__0` | URL do frontend (ex.: `https://noteesapp-ui.vercel.app`). Sem isso, só `http://localhost:5173` é aceito |
| `ENABLE_SWAGGER` | true (opcional; Swagger só fica ativo em Development ou com esta variável) |

`ASPNETCORE_ENVIRONMENT` já vem como `Production` na imagem.

### Migrations em produção

O contêiner não tem terminal, então aplique as migrations da sua máquina, apontando para o banco de produção:

```bash
ConnectionStrings__DefaultConnection="<string do Neon>" dotnet ef database update
```

(no PowerShell: `$env:ConnectionStrings__DefaultConnection="<string do Neon>"; dotnet ef database update`).
Lembre que `AddUsersAndNoteOwnership` **apaga as notas existentes**. Teste antes numa *branch* do Neon.

### Banco de Dados (Neon)

- **Região:** AWS eu-west-2 (London)
- **Tipo:** PostgreSQL Serverless
- **Conexão:** SSL obrigatório

---

## 🔒 Segurança

### Medidas Implementadas

| Medida | Descrição |
|--------|-----------|
| **Autenticação** | JWT HS256 de curta duração (15 min), validando emissor, audiência, assinatura e expiração; refresh token com rotação e detecção de reuso |
| **Senhas** | PBKDF2 via `PasswordHasher`; mensagem única para e-mail/senha inválidos e verificação de hash também para e-mail inexistente (sem diferença de tempo) |
| **Isolamento** | Toda consulta filtra por `UserId` vindo do token; nota alheia responde `404` |
| **Rate limit** | Endpoints de `/api/auth` limitados por IP (429), usando o IP real via `X-Forwarded-For` do Render |
| **.gitignore** | appsettings.json, bin/, obj/ ignorados |
| **Variáveis de Ambiente** | Credenciais no Render, não no código |
| **CORS** | Apenas origens listadas em `Cors:AllowedOrigins` (padrão: localhost:5173) |
| **HTTPS** | Terminado pelo Render em produção; o redirecionamento da aplicação só roda em Development |
| **Validações** | DataAnnotations + validação manual no Service |
| **SQL Injection** | Previno com EF Core (parameterized queries) |

### Arquivos Sensíveis

```
✅ .gitignore configurado
✅ appsettings.json.example (sem credenciais)
✅ Variáveis no Render Dashboard
✅ Connection string rotacionada periodicamente
```

---

---

## 🤝 Contribuindo

1. Fork o projeto
2. Crie uma branch (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

---

## 👨‍💻 Autor

**Pedro Beltrão**

- GitHub: [@PedroBeltraoDev](https://github.com/PedroBeltraoDev)
- LinkedIn: [pedro-beltrao](https://www.linkedin.com/in/pedro-beltr%C3%A3o123/)
- Email: pedrobeltraodev@gmail.com

---

<div align="center">

**Feito por Pedro Beltrão**

[⬆ Voltar ao topo](#-noteesapp-api---backend)

</div>
