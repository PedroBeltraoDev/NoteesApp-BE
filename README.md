# 🚀 NoteesApp API - Backend

API RESTful para gerenciamento de notas com organização por pastas e tags, desenvolvida com .NET 10, Entity Framework Core e PostgreSQL.

[![Status](https://img.shields.io/badge/status-production-success)](https://noteesapp-be.onrender.com)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-15%20passed-brightgreen)](https://github.com/PedroBeltraoDev/NoteesApp-BE)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

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

- ✅ **CRUD Completo** de notas (Criar, Ler, Atualizar, Deletar)
- ✅ **Organização por Pastas** e Tags
- ✅ **Notas Fixadas** (pin/unpin)
- ✅ **Busca e Filtros** por pasta e tag
- ✅ **Validações** de conteúdo (título 3-200 chars, conteúdo 10-1000 chars)
- ✅ **Paginação** e ordenação (notas fixadas primeiro)
- ✅ **Swagger/OpenAPI** para documentação
- ✅ **Health Check** endpoint
- ✅ **Logs estruturados** com ILogger
- ✅ **Tratamento de Erros** global com middleware
- ✅ **Testes Unitários** com xUnit e Moq

---

## 🛠 Tecnologias

| Categoria | Tecnologia |
|-----------|------------|
| **Framework** | .NET 10 |
| **Linguagem** | C# 13 |
| **ORM** | Entity Framework Core 9 |
| **Banco de Dados** | PostgreSQL (Neon) |
| **Testes** | xUnit, Moq, coverlet |
| **Documentação** | Swagger/OpenAPI |
| **Mapeamento** | AutoMapper |
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
dotnet restore

# 5. Aplicar migrações
dotnet ef database update

# 6. Rodar a API
dotnet run

# 7. Acessar Swagger
http://localhost:5000/swagger
```

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
  "AllowedHosts": "*"
}
```

---

## 🧪 Testes

### Rodar Testes

```bash
# Todos os testes
dotnet test

# Com detalhes
dotnet test --verbosity normal

# Com code coverage
dotnet test --collect:"XPlat Code Coverage"

# Apenas NoteServiceTests
dotnet test --filter "FullyQualifiedName~NoteServiceTests"
```

### Cobertura

| Classe | Coverage |
|--------|----------|
| NoteService | 85% |
| NotesController | 80% |
| NoteRepository | 75% |

### Estrutura de Testes

```
Tests/
└── NoteServiceTests.cs
    ├── CreateAsync Tests (3)
    ├── GetByIdAsync Tests (2)
    ├── UpdateAsync Tests (2)
    ├── DeleteAsync Tests (2)
    ├── GetFilteredAsync Tests (2)
    ├── GetDistinctFoldersAsync Tests (1)
    └── GetDistinctTagsAsync Tests (1)
```
---

### Endpoints

| Método | Endpoint | Descrição | 
|--------|----------|-----------|
| `GET` | `/health` | Health check |
| `GET` | `/notes` | Listar todas as notas |
| `GET` | `/notes/{id}` | Buscar nota por ID |
| `POST` | `/notes` | Criar nova nota |
| `PUT` | `/notes/{id}` | Atualizar nota |
| `DELETE` | `/notes/{id}` | Excluir nota |
| `GET` | `/notes/folders` | Listar pastas únicas |
| `GET` | `/notes/tags` | Listar tags únicas |
| `GET` | `/notes/filter?folder=X&tag=Y` | Filtrar notas |

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

### Backend (Render)

```yaml
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NotesApp.Api.dll"]
```

### Variáveis de Ambiente (Render)

| Variável | Valor |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | Production |
| `ConnectionStrings__DefaultConnection` | (string do Neon) |
| `ENABLE_SWAGGER` | true |

### Banco de Dados (Neon)

- **Região:** AWS eu-west-2 (London)
- **Tipo:** PostgreSQL Serverless
- **Conexão:** SSL obrigatório

---

## 🔒 Segurança

### Medidas Implementadas

| Medida | Descrição |
|--------|-----------|
| **.gitignore** | appsettings.json, bin/, obj/ ignorados |
| **Variáveis de Ambiente** | Credenciais no Render, não no código |
| **CORS** | Apenas domínios autorizados (Vercel + localhost) |
| **HTTPS** | Forçado em produção |
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

### Otimizações

- ✅ Índices no banco (IsPinned, CreatedAt, Folder)
- ✅ Query optimization com EF Core
- ✅ Connection pooling (Neon)
- ✅ Response compression

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