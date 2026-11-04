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
                name: "ImageTexts",
                columns: table => new
                {
                    Artikelnummer = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageTexts", x => new { x.Artikelnummer, x.Language });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageTexts");
        }
    }
}
