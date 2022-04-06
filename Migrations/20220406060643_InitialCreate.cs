using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OriginalFiles",
                columns: table => new
                {
                    Artikelnummer = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    FileCrc = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OriginalFiles", x => x.Artikelnummer);
                });

            migrationBuilder.CreateTable(
                name: "ConvertedFiles",
                columns: table => new
                {
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FileCrc = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false),
                    OriginalFileArtikelnummer = table.Column<uint>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvertedFiles", x => x.FileName);
                    table.ForeignKey(
                        name: "FK_ConvertedFiles_OriginalFiles_OriginalFileArtikelnummer",
                        column: x => x.OriginalFileArtikelnummer,
                        principalTable: "OriginalFiles",
                        principalColumn: "Artikelnummer");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConvertedFiles_OriginalFileArtikelnummer",
                table: "ConvertedFiles",
                column: "OriginalFileArtikelnummer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConvertedFiles");

            migrationBuilder.DropTable(
                name: "OriginalFiles");
        }
    }
}
