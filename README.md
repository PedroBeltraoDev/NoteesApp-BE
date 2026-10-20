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

- ✅ **CRUD Completo** de notas (Criar, Ler, Atualizar, Deletar)
- ✅ **Organização por Pastas** e Tags
- ✅ **Notas Fixadas** (pin/unpin)
- ✅ **Busca e Filtros** por pasta e tag
- ✅ **Validações** de conteúdo (título 3-200 chars, conteúdo 10-1000 chars)
- ✅ **Ordenação** (notas fixadas primeiro, depois mais recentes)
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
├── NoteServiceTests.cs                 # regras de negócio (repositório mockado)
├── NoteMapperTests.cs                  # mapeamento DTO <-> entidade
├── ExceptionHandlingMiddlewareTests.cs # tradução de exceções em HTTP
└── NoteRepositoryQueryTests.cs         # traduzibilidade das queries para SQL do Npgsql
```

---

### Endpoints

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `GET` | `/api/health` | Health check |
| `GET` | `/api/notes` | Listar notas (filtros opcionais: `?folder=X&tag=Y`) |
| `GET` | `/api/notes/{id}` | Buscar nota por ID |
| `POST` | `/api/notes` | Criar nova nota |
| `PUT` | `/api/notes/{id}` | Atualizar nota (o `id` vem só da rota; não enviar no body) |
| `DELETE` | `/api/notes/{id}` | Excluir nota |
| `GET` | `/api/notes/folders` | Listar pastas únicas |
| `GET` | `/api/notes/tags` | Listar tags únicas |

Fixar/desafixar uma nota é feito alterando `isPinned` no `PUT`.

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

O deploy usa o `Dockerfile` da raiz do repositório (build multi-stage com as imagens `dotnet/sdk:10.0` e `dotnet/aspnet:10.0`).
O `.dockerignore` mantém testes, `bin/obj` e arquivos de configuração local fora da imagem.

### Variáveis de Ambiente (Render)

| Variável | Valor |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | Production |
| `ConnectionStrings__DefaultConnection` | (string do Neon) |
| `ENABLE_SWAGGER` | true (opcional; Swagger só fica ativo em Development ou com esta variável) |
| `Cors__AllowedOrigins__0` | URL do frontend (ex.: `https://seu-app.vercel.app`). Sem isso, só `http://localhost:5173` é aceito |

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
