using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "papers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SemanticScholarId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Doi = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Abstract = table.Column<string>(type: "text", nullable: true),
                    Authors = table.Column<List<string>>(type: "text[]", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Venue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CitationCount = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_papers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analysis_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnalysisType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InputSetJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_analysis_results_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "paper_summaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SummaryJson = table.Column<string>(type: "jsonb", nullable: true),
                    SearchText = table.Column<string>(type: "text", nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_summaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paper_summaries_papers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "papers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paper_embeddings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperId = table.Column<Guid>(type: "uuid", nullable: true),
                    SummaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmbeddingType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Vector = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    ModelName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_embeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paper_embeddings_paper_summaries_SummaryId",
                        column: x => x.SummaryId,
                        principalTable: "paper_summaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_paper_embeddings_papers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "papers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_results_AnalysisType",
                table: "analysis_results",
                column: "AnalysisType");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_results_JobId",
                table: "analysis_results",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jobs_Status",
                table: "jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_TargetEntityId",
                table: "jobs",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_Type",
                table: "jobs",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_paper_embeddings_EmbeddingType",
                table: "paper_embeddings",
                column: "EmbeddingType");

            migrationBuilder.CreateIndex(
                name: "IX_paper_embeddings_PaperId",
                table: "paper_embeddings",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_embeddings_SummaryId",
                table: "paper_embeddings",
                column: "SummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_summaries_PaperId",
                table: "paper_summaries",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_summaries_Status",
                table: "paper_summaries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_papers_Doi",
                table: "papers",
                column: "Doi");

            migrationBuilder.CreateIndex(
                name: "IX_papers_SemanticScholarId",
                table: "papers",
                column: "SemanticScholarId");

            migrationBuilder.CreateIndex(
                name: "IX_papers_Source",
                table: "papers",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_papers_Status",
                table: "papers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_papers_Title",
                table: "papers",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_papers_Year",
                table: "papers",
                column: "Year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropTable(
                name: "analysis_results");

            migrationBuilder.DropTable(
                name: "paper_embeddings");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "paper_summaries");

            migrationBuilder.DropTable(
                name: "papers");
        }
    }
}
