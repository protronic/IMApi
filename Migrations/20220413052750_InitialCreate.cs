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
                    Artikelnummer = table.Column<string>(type: "TEXT", nullable: true),
                    lang = table.Column<int>(type: "INTEGER", nullable: false),
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
                name: "Conversions",
                columns: table => new
                {
                    ConveretedFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ConversionName = table.Column<string>(type: "TEXT", nullable: true),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFileFileName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversions", x => x.ConveretedFilePath);
                    table.ForeignKey(
                        name: "FK_Conversions_OriginalFiles_OriginalFileFileName",
                        column: x => x.OriginalFileFileName,
                        principalTable: "OriginalFiles",
                        principalColumn: "FileName");
                });

            migrationBuilder.CreateTable(
                name: "ConvertedFiles",
                columns: table => new
                {
                    WebURL = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: true),
                    ConversionConveretedFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    FileType = table.Column<string>(type: "TEXT", nullable: true),
                    FileCrc = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false),
                    OriginalFileFileName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvertedFiles", x => x.WebURL);
                    table.ForeignKey(
                        name: "FK_ConvertedFiles_Conversions_ConversionConveretedFilePath",
                        column: x => x.ConversionConveretedFilePath,
                        principalTable: "Conversions",
                        principalColumn: "ConveretedFilePath");
                    table.ForeignKey(
                        name: "FK_ConvertedFiles_OriginalFiles_OriginalFileFileName",
                        column: x => x.OriginalFileFileName,
                        principalTable: "OriginalFiles",
                        principalColumn: "FileName");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversions_OriginalFileFileName",
                table: "Conversions",
                column: "OriginalFileFileName");

            migrationBuilder.CreateIndex(
                name: "IX_ConvertedFiles_ConversionConveretedFilePath",
                table: "ConvertedFiles",
                column: "ConversionConveretedFilePath");

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
                name: "Conversions");

            migrationBuilder.DropTable(
                name: "OriginalFiles");
        }
    }
}
