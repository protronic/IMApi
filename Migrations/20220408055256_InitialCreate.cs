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
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Artikelnummer = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: true),
                    FileCrc = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false),
                    WebURL = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OriginalFiles", x => x.FileName);
                });

            migrationBuilder.CreateTable(
                name: "ConvertedFiles",
                columns: table => new
                {
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    ConversionType = table.Column<string>(type: "TEXT", nullable: true),
                    FileCrc = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false),
                    WebURL = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalFileFileName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvertedFiles", x => x.FileName);
                    table.ForeignKey(
                        name: "FK_ConvertedFiles_OriginalFiles_OriginalFileFileName",
                        column: x => x.OriginalFileFileName,
                        principalTable: "OriginalFiles",
                        principalColumn: "FileName");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConvertedFiles_OriginalFileFileName",
                table: "ConvertedFiles",
                column: "OriginalFileFileName");
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
