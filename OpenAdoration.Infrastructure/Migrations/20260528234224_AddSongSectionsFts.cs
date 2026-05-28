using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSongSectionsFts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FTS5 virtual table over SongSections.Lyrics.
            // rowid=SongSections.Id so the join in SearchByLyricsAsync is a PK lookup.
            // SongId is UNINDEXED: stored per-row for filtering but not full-text indexed.
            migrationBuilder.Sql("""
                CREATE VIRTUAL TABLE SongSectionsFts USING fts5(
                    Lyrics,
                    SongId UNINDEXED,
                    tokenize='unicode61'
                );
                """);

            // Populate from any sections already in the database (upgrade path).
            migrationBuilder.Sql("""
                INSERT INTO SongSectionsFts(rowid, Lyrics, SongId)
                SELECT Id, Lyrics, SongId FROM SongSections;
                """);

            // Keep FTS in sync with SongSections via triggers.
            migrationBuilder.Sql("""
                CREATE TRIGGER SongSectionsFts_Insert AFTER INSERT ON SongSections BEGIN
                    INSERT INTO SongSectionsFts(rowid, Lyrics, SongId) VALUES (new.Id, new.Lyrics, new.SongId);
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER SongSectionsFts_Update AFTER UPDATE ON SongSections BEGIN
                    UPDATE SongSectionsFts SET Lyrics=new.Lyrics, SongId=new.SongId WHERE rowid=old.Id;
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER SongSectionsFts_Delete AFTER DELETE ON SongSections BEGIN
                    DELETE FROM SongSectionsFts WHERE rowid=old.Id;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SongSectionsFts_Delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SongSectionsFts_Update;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SongSectionsFts_Insert;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS SongSectionsFts;");
        }
    }
}
