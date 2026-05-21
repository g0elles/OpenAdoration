using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBibleVersesFts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FTS5 virtual table — not tracked by EF Core; managed via raw SQL.
            // unicode61 tokenizer: Unicode-aware word splitting + case folding.
            // BibleVersionId is UNINDEXED: stored per-row for version scoping but
            // not included in the full-text inverted index.
            migrationBuilder.Sql("""
                CREATE VIRTUAL TABLE BibleVersesFts USING fts5(
                    Text,
                    BibleVersionId UNINDEXED,
                    tokenize='unicode61'
                );
                """);

            // Populate from any Bible versions already in the database (upgrade path).
            migrationBuilder.Sql("""
                INSERT INTO BibleVersesFts(rowid, Text, BibleVersionId)
                SELECT Id, Text, BibleVersionId FROM BibleVerses;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS BibleVersesFts;");
        }
    }
}
