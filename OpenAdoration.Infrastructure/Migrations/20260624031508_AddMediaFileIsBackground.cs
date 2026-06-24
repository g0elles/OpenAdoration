using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaFileIsBackground : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_ContentHash",
                table: "MediaFiles");

            migrationBuilder.AddColumn<bool>(
                name: "IsBackground",
                table: "MediaFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_ContentHash_IsBackground",
                table: "MediaFiles",
                columns: new[] { "ContentHash", "IsBackground" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_ContentHash_IsBackground",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "IsBackground",
                table: "MediaFiles");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_ContentHash",
                table: "MediaFiles",
                column: "ContentHash");
        }
    }
}
