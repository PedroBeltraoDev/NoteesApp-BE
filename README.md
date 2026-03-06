# 📝 NotisApp API - Backend

API RESTful para o aplicativo de notas NoteesApp, desenvolvida com .NET e PostgreSQL.

## 🚀 Tecnologias

- **.NET 8.0** - Framework principal
- **Entity Framework Core** - ORM
- **PostgreSQL** - Banco de dados (Neon)
- **Npgsql** - Provider PostgreSQL
- **Swagger** - Documentação da API

## 📋 Pré-requisitos

- .NET 8.0 ou superior
- PostgreSQL (recomendado: Neon - https://neon.tech)
- Git

## 🔧 Instalação

1. Clone o repositório:
```bash
git clone https://github.com/PedroBeltraoDev/NotisApp-BE.git
cd NotisApp-BE
```

2. Configure a conexão com o banco em appsettings.json:
 ```bash
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=SEU_HOST;Database=notisdb;Username=SEU_USER;Password=SUA_SENHA"
  }
}
```

3.Execute as migrations:
```bash
dotnet ef database update
```

4.Inicie a API:
```bash
dotnet run
```

A API estará disponível em: http://localhost:5216
📚 Endpoints Principais
Notes
```bash
GET      |    /api/notes                |     Listar todas as notas
GET      |    /api/notes/{id}           |    Buscar nota por ID
POST     |    /api/notes                |     Criar nova nota
PUT      |    /api/notes/{id}           |     Atualizar nota
DELETE   |    /api/notes/{id}           |     Excluir nota
GET      |    /api/notes/search?query=   |    Buscar notas por texto
GET      |    /api/notes/folders        |     Listar pastas disponíveis
GET      |    /api/notes/tags           |     Listar tags disponíveis
```

🗂️ Estrutura do Projeto
```bash
NotesApp.Api/
├── Controllers/        # Controllers da API
├── Data/               # DbContext e configurações
├── DTOs/               # Data Transfer Objects
├── Middleware/         # Middlewares customizados
├── Migrations/         # Migrations do EF Core
├── Models/             # Entidades do banco
├── Repositories/       # Repositórios
└── Services/           # Regras de negócio
```

🔐 Variáveis de Ambiente
Crie um arquivo appsettings.local.json para configurações sensíveis (não versionado):
```bash
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=...;Username=...;Password=..."
  }
}
```

📦 Comandos Úteis
```bash
# Build
dotnet build

# Executar
dotnet run

# Testes
dotnet test

# Migrations
dotnet ef migrations add NomeDaMigration
dotnet ef database update
```
👨‍💻 Autor
Pedro Beltrão
GitHub: @PedroBeltraoDev
