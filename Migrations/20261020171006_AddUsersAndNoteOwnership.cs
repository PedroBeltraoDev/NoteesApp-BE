using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndNoteOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ATENÇÃO - DESTRUTIVO: as notas existentes não têm dono e, por decisão do projeto, são
            // APAGADAS aqui, antes de "Notes"."UserId" (NOT NULL) ser criada. Não há como desfazer
            // pelo Down. Se precisar preservá-las, faça backup (ou use uma branch do Neon) antes de aplicar.
            migrationBuilder.Sql(@"DELETE FROM ""Notes"";");

            migrationBuilder.DropIndex(
                name: "IX_Notes_Folder",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_IsPinned_CreatedAt_Id",
                table: "Notes");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Notes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UserId_Folder",
                table: "Notes",
                columns: new[] { "UserId", "Folder" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UserId_IsPinned_CreatedAt_Id",
                table: "Notes",
                columns: new[] { "UserId", "IsPinned", "CreatedAt", "Id" },
                descending: new[] { false, true, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Users_UserId",
                table: "Notes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Users_UserId",
                table: "Notes");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Notes_UserId_Folder",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_UserId_IsPinned_CreatedAt_Id",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Notes");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_Folder",
                table: "Notes",
                column: "Folder");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_IsPinned_CreatedAt_Id",
                table: "Notes",
                columns: new[] { "IsPinned", "CreatedAt", "Id" },
                descending: new bool[0]);
        }
    }
}
