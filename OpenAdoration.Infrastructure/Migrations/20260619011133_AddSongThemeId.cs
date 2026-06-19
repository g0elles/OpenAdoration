using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSongThemeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ThemeId",
                table: "Songs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_ThemeId",
                table: "Songs",
                column: "ThemeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Songs_Themes_ThemeId",
                table: "Songs",
                column: "ThemeId",
                principalTable: "Themes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Songs_Themes_ThemeId",
                table: "Songs");

            migrationBuilder.DropIndex(
                name: "IX_Songs_ThemeId",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "ThemeId",
                table: "Songs");
        }
    }
}
