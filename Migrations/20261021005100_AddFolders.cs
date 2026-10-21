using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NotesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Folders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Folders_UserId",
                table: "Folders",
                column: "UserId");

            // O nome da pasta é único por usuário sem diferenciar maiúsculas ("Trabalho" = "trabalho").
            // O EF não modela índice de expressão, por isso ele fica só aqui na migration.
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX ""UX_Folders_UserId_LowerName"" ON ""Folders"" (""UserId"", lower(""Name""));");

            migrationBuilder.AddColumn<int>(
                name: "FolderId",
                table: "Notes",
                type: "integer",
                nullable: true);

            // PRESERVA os dados: cada nome de pasta que existia nas notas vira uma linha em "Folders" (por usuário,
            // juntando variações de maiúsculas/minúsculas e espaços nas pontas) e as notas passam a apontar para ela.
            // A pasta nasce com a data da nota mais antiga que a usa.
            migrationBuilder.Sql(@"
INSERT INTO ""Folders"" (""UserId"", ""Name"", ""CreatedAt"")
SELECT n.""UserId"", min(btrim(n.""Folder"")), min(n.""CreatedAt"")
FROM ""Notes"" n
WHERE n.""Folder"" IS NOT NULL AND btrim(n.""Folder"") <> ''
GROUP BY n.""UserId"", lower(btrim(n.""Folder""));");

            migrationBuilder.Sql(@"
UPDATE ""Notes"" n
SET ""FolderId"" = f.""Id""
FROM ""Folders"" f
WHERE n.""Folder"" IS NOT NULL
  AND f.""UserId"" = n.""UserId""
  AND lower(f.""Name"") = lower(btrim(n.""Folder""));");

            migrationBuilder.DropIndex(
                name: "IX_Notes_UserId_Folder",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "Folder",
                table: "Notes");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_FolderId",
                table: "Notes",
                column: "FolderId");

            // Excluir a pasta não apaga as notas: o banco zera FolderId
            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Folders_FolderId",
                table: "Notes",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Folder",
                table: "Notes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Devolve o nome da pasta para dentro de cada nota. Pastas vazias não têm onde ficar e se perdem.
            migrationBuilder.Sql(@"
UPDATE ""Notes"" n
SET ""Folder"" = f.""Name""
FROM ""Folders"" f
WHERE n.""FolderId"" = f.""Id"";");

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Folders_FolderId",
                table: "Notes");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Notes_FolderId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Notes");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UserId_Folder",
                table: "Notes",
                columns: new[] { "UserId", "Folder" });
        }
    }
}
