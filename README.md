# 📝 NotisApp API - Backend

> API RESTful para o aplicativo de notas NoteesApp, desenvolvida com .NET 8 e PostgreSQL.

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET">
  <img src="https://img.shields.io/badge/Entity_Framework-Core-6DB33F?logo=entityframework&logoColor=white" alt="EF Core">
  <img src="https://img.shields.io/badge/PostgreSQL-15.x-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL">
  <img src="https://img.shields.io/badge/Swagger-OpenAPI-85EA2D?logo=swagger&logoColor=black" alt="Swagger">
  <img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License">
</p>

---

## 📖 Sobre

O **NotisApp API** é o backend do sistema de gerenciamento de notas NoteesApp. Desenvolvido com .NET 8 e Entity Framework Core, oferece uma API RESTful robusta, segura e documentada para gerenciar notas, pastas e tags com alta performance e escalabilidade.

### ✨ Destaques

| Recurso | Descrição |
|---------|-----------|
| 🔐 **Segurança** | Validação de entrada, CORS configurado, boas práticas de API |
| 📚 **Documentação** | Swagger/OpenAPI integrado para teste e documentação |
| 🗄️ **Banco de Dados** | PostgreSQL com migrations via Entity Framework Core |
| ⚡ **Performance** | Arquitetura em camadas, injeção de dependência, async/await |
| 🔄 **Versionamento** | API versionada e preparada para evolução |
| 🧪 **Testável** | Estrutura preparada para testes unitários e de integração |

---

## 🚀 Tecnologias

- **.NET 8.0** - Framework principal
- **Entity Framework Core 8** - ORM para acesso a dados
- **PostgreSQL** - Banco de dados relacional
- **Npgsql** - Provider PostgreSQL para EF Core
- **Swagger/OpenAPI** - Documentação automática da API
- **ASP.NET Core Web API** - Framework para APIs REST
- **Minimal APIs / Controllers** - Padrão de rotas

---

## 📋 Pré-requisitos

- **.NET 8.0 SDK** ou superior
- **PostgreSQL 15+** (recomendado: [Neon](https://neon.tech) para cloud)
- **Git** instalado

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
