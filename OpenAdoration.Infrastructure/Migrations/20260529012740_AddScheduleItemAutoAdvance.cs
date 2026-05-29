using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleItemAutoAdvance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoAdvanceSeconds",
                table: "ScheduleItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoAdvanceSeconds",
                table: "ScheduleItems");
        }
    }
}
