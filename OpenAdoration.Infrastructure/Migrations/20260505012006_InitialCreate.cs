using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenAdoration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BibleVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Songs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Themes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FontFamily = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FontSize = table.Column<int>(type: "INTEGER", nullable: false),
                    FontColor = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    BackgroundColor = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    BackgroundImagePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorshipServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorshipServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BibleBooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BibleVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Abbreviation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Testament = table.Column<string>(type: "TEXT", nullable: false),
                    BookNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ChapterCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleBooks_BibleVersions_BibleVersionId",
                        column: x => x.BibleVersionId,
                        principalTable: "BibleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BibleVerses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BibleVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Book = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Chapter = table.Column<int>(type: "INTEGER", nullable: false),
                    Verse = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BibleVerses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BibleVerses_BibleVersions_BibleVersionId",
                        column: x => x.BibleVersionId,
                        principalTable: "BibleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SongSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SongId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    SectionNumber = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    Lyrics = table.Column<string>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SongSections_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    ThemeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    Book = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Chapter = table.Column<int>(type: "INTEGER", nullable: true),
                    VerseStart = table.Column<int>(type: "INTEGER", nullable: true),
                    VerseEnd = table.Column<int>(type: "INTEGER", nullable: true),
                    BibleVersionId = table.Column<int>(type: "INTEGER", nullable: true),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: true),
                    SongId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleItems_BibleVersions_BibleVersionId",
                        column: x => x.BibleVersionId,
                        principalTable: "BibleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleItems_MediaFiles_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleItems_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleItems_Themes_ThemeId",
                        column: x => x.ThemeId,
                        principalTable: "Themes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduleItems_WorshipServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "WorshipServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Themes",
                columns: new[] { "Id", "BackgroundColor", "BackgroundImagePath", "CreatedAt", "FontColor", "FontFamily", "FontSize", "IsDefault", "Name", "UpdatedAt" },
                values: new object[] { 1, "#000000", null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "#FFFFFF", "Arial", 48, true, "Default", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_BibleBooks_BibleVersionId_BookNumber",
                table: "BibleBooks",
                columns: new[] { "BibleVersionId", "BookNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_BibleVerses_BibleVersionId_Book_Chapter",
                table: "BibleVerses",
                columns: new[] { "BibleVersionId", "Book", "Chapter" });

            migrationBuilder.CreateIndex(
                name: "IX_BibleVerses_BibleVersionId_Book_Chapter_Verse",
                table: "BibleVerses",
                columns: new[] { "BibleVersionId", "Book", "Chapter", "Verse" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BibleVersions_Abbreviation",
                table: "BibleVersions",
                column: "Abbreviation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_BibleVersionId",
                table: "ScheduleItems",
                column: "BibleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_MediaFileId",
                table: "ScheduleItems",
                column: "MediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_ServiceId_Order",
                table: "ScheduleItems",
                columns: new[] { "ServiceId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_SongId",
                table: "ScheduleItems",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleItems_ThemeId",
                table: "ScheduleItems",
                column: "ThemeId");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_Title",
                table: "Songs",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_SongSections_SongId_Order",
                table: "SongSections",
                columns: new[] { "SongId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_WorshipServices_Date",
                table: "WorshipServices",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BibleBooks");

            migrationBuilder.DropTable(
                name: "BibleVerses");

            migrationBuilder.DropTable(
                name: "ScheduleItems");

            migrationBuilder.DropTable(
                name: "SongSections");

            migrationBuilder.DropTable(
                name: "BibleVersions");

            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "Themes");

            migrationBuilder.DropTable(
                name: "WorshipServices");

            migrationBuilder.DropTable(
                name: "Songs");
        }
    }
}
