using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClusterMapAndConcepts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.AddColumn<double>(
                name: "cluster_x",
                table: "papers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "cluster_y",
                table: "papers",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "paper_concepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_concepts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paper_concepts_papers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "papers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paper_concepts_PaperId",
                table: "paper_concepts",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_concepts_ConceptType",
                table: "paper_concepts",
                column: "ConceptType");

            migrationBuilder.CreateIndex(
                name: "IX_paper_concepts_Name",
                table: "paper_concepts",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropTable(
                name: "paper_concepts");

            migrationBuilder.DropColumn(
                name: "cluster_x",
                table: "papers");

            migrationBuilder.DropColumn(
                name: "cluster_y",
                table: "papers");
        }
    }
}
