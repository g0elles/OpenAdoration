using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSongCopyrightAndCcli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CcliNumber",
                table: "Songs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Copyright",
                table: "Songs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CcliNumber",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Copyright",
                table: "Songs");
        }
    }
}
