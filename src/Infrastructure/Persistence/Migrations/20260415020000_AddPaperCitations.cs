using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.CreateTable(
                name: "paper_citations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetPaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitationContext = table.Column<string>(type: "text", nullable: true),
                    IngestedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_citations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paper_citations_papers_SourcePaperId",
                        column: x => x.SourcePaperId,
                        principalTable: "papers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_paper_citations_papers_TargetPaperId",
                        column: x => x.TargetPaperId,
                        principalTable: "papers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paper_citations_SourcePaperId",
                table: "paper_citations",
                column: "SourcePaperId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_citations_TargetPaperId",
                table: "paper_citations",
                column: "TargetPaperId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_citations_SourcePaperId_TargetPaperId",
                table: "paper_citations",
                columns: new[] { "SourcePaperId", "TargetPaperId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropTable(
                name: "paper_citations");
        }
    }
}
