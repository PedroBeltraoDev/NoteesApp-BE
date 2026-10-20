using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotesApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnLimitsAndNoteIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reduzir o tamanho das colunas nunca trunca dados em silêncio: se existir nota
            // acima do limite, a migration aborta (e a transação é desfeita) com instruções.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    long_folders integer;
    long_contents integer;
BEGIN
    SELECT count(*) INTO long_folders FROM ""Notes"" WHERE char_length(""Folder"") > 100;
    SELECT count(*) INTO long_contents FROM ""Notes"" WHERE char_length(""Content"") > 1000;

    IF long_folders > 0 OR long_contents > 0 THEN
        RAISE EXCEPTION 'Migration abortada: % nota(s) com Folder > 100 caracteres e % com Content > 1000. Ajuste esses registros antes de aplicar.', long_folders, long_contents;
    END IF;
END $$;");

            migrationBuilder.AlterColumn<string>(
                name: "Folder",
                table: "Notes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Notes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notes_Folder",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_IsPinned_CreatedAt_Id",
                table: "Notes");

            migrationBuilder.AlterColumn<string>(
                name: "Folder",
                table: "Notes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Notes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);
        }
    }
}
