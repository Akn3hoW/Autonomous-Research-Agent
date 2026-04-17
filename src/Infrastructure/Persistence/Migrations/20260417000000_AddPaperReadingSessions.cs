using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperReadingSessionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.CreateTable(
                name: "paper_reading_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_reading_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paper_reading_sessions_UserId",
                table: "paper_reading_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_reading_sessions_PaperId",
                table: "paper_reading_sessions",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_reading_sessions_Status",
                table: "paper_reading_sessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_paper_reading_sessions_UserId_Status",
                table: "paper_reading_sessions",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_paper_reading_sessions_papers_PaperId",
                table: "paper_reading_sessions",
                column: "PaperId",
                principalTable: "papers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_paper_reading_sessions_users_UserId",
                table: "paper_reading_sessions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropTable(
                name: "paper_reading_sessions");
        }
    }
}
