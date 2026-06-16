using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoPsalmMigrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceArchivePath",
                table: "WorshipServices",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceGuid",
                table: "WorshipServices",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceGuid",
                table: "Songs",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "MediaFiles",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorshipServices_SourceGuid",
                table: "WorshipServices",
                column: "SourceGuid");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_SourceGuid",
                table: "Songs",
                column: "SourceGuid");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_ContentHash",
                table: "MediaFiles",
                column: "ContentHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorshipServices_SourceGuid",
                table: "WorshipServices");

            migrationBuilder.DropIndex(
                name: "IX_Songs_SourceGuid",
                table: "Songs");

            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_ContentHash",
                table: "MediaFiles");

            migrationBuilder.DropColumn(
                name: "SourceArchivePath",
                table: "WorshipServices");

            migrationBuilder.DropColumn(
                name: "SourceGuid",
                table: "WorshipServices");

            migrationBuilder.DropColumn(
                name: "SourceGuid",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "MediaFiles");
        }
    }
}
