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
                name: "FileMeta",
                columns: table => new
                {
                    WebURL = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Artikelnummer = table.Column<string>(type: "TEXT", nullable: false),
                    lang = table.Column<int>(type: "INTEGER", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    FileCrc = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileLength = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMeta", x => x.WebURL);
                });

            migrationBuilder.CreateTable(
                name: "OriginalFiles",
                columns: table => new
                {
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileMetaDataWebURL = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OriginalFiles", x => x.FilePath);
                    table.ForeignKey(
                        name: "FK_OriginalFiles_FileMeta_FileMetaDataWebURL",
                        column: x => x.FileMetaDataWebURL,
                        principalTable: "FileMeta",
                        principalColumn: "WebURL");
                });

            migrationBuilder.CreateTable(
                name: "Conversions",
                columns: table => new
                {
                    ConveretedFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    ConversionName = table.Column<string>(type: "TEXT", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    BackgroundColor = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalFileFilePath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversions", x => x.ConveretedFilePath);
                    table.ForeignKey(
                        name: "FK_Conversions_OriginalFiles_OriginalFileFilePath",
                        column: x => x.OriginalFileFilePath,
                        principalTable: "OriginalFiles",
                        principalColumn: "FilePath");
                });

            migrationBuilder.CreateTable(
                name: "ConvertedFiles",
                columns: table => new
                {
                    ConveretedFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileMetaDataWebURL = table.Column<string>(type: "TEXT", nullable: false),
                    ConversionConveretedFilePath = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalFileFilePath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvertedFiles", x => x.ConveretedFilePath);
                    table.ForeignKey(
                        name: "FK_ConvertedFiles_Conversions_ConversionConveretedFilePath",
                        column: x => x.ConversionConveretedFilePath,
                        principalTable: "Conversions",
                        principalColumn: "ConveretedFilePath");
                    table.ForeignKey(
                        name: "FK_ConvertedFiles_FileMeta_FileMetaDataWebURL",
                        column: x => x.FileMetaDataWebURL,
                        principalTable: "FileMeta",
                        principalColumn: "WebURL",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConvertedFiles_OriginalFiles_OriginalFileFilePath",
                        column: x => x.OriginalFileFilePath,
                        principalTable: "OriginalFiles",
                        principalColumn: "FilePath");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversions_OriginalFileFilePath",
                table: "Conversions",
                column: "OriginalFileFilePath");

            migrationBuilder.CreateIndex(
                name: "IX_ConvertedFiles_ConversionConveretedFilePath",
                table: "ConvertedFiles",
                column: "ConversionConveretedFilePath");

            migrationBuilder.CreateIndex(
                name: "IX_ConvertedFiles_FileMetaDataWebURL",
                table: "ConvertedFiles",
                column: "FileMetaDataWebURL");

            migrationBuilder.CreateIndex(
                name: "IX_ConvertedFiles_OriginalFileFilePath",
                table: "ConvertedFiles",
                column: "OriginalFileFilePath");

            migrationBuilder.CreateIndex(
                name: "IX_OriginalFiles_FileMetaDataWebURL",
                table: "OriginalFiles",
                column: "FileMetaDataWebURL");
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

            migrationBuilder.DropTable(
                name: "FileMeta");
        }
    }
}
