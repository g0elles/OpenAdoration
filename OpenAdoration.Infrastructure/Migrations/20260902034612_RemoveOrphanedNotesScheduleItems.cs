using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only cleanup: an earlier F9 dev build (Title/Content on <c>NotesScheduleItem</c>,
    /// superseded by the real Notes library before release) could leave a "Notes" schedule item
    /// with a null NoteId once the newer <c>AddNotesLibrary</c> migration ran on top of it --
    /// <c>WorshipServiceRepository.GetWithItemsAsync</c> throws trying to hydrate it, blocking the
    /// owning service from opening at all. Removes any such orphaned row; harmless no-op on a
    /// database that never had one.
    /// </summary>
    public partial class RemoveOrphanedNotesScheduleItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM ScheduleItems WHERE ItemType = 'Notes' AND NoteId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data cleanup is not reversible -- the deleted row(s) carried no recoverable content.
        }
    }
}
