using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperEmbeddingVectorHnswIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentChunkId",
                table: "paper_embeddings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    TextLength = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_chunks_paper_documents_PaperDocumentId",
                        column: x => x.PaperDocumentId,
                        principalTable: "paper_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paper_embeddings_DocumentChunkId",
                table: "paper_embeddings",
                column: "DocumentChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_PaperDocumentId",
                table: "document_chunks",
                column: "PaperDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_PaperDocumentId_ChunkIndex",
                table: "document_chunks",
                columns: new[] { "PaperDocumentId", "ChunkIndex" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_paper_embeddings_document_chunks_DocumentChunkId",
                table: "paper_embeddings",
                column: "DocumentChunkId",
                principalTable: "document_chunks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.Sql(@"
                CREATE INDEX ix_paper_embeddings_vector_hnsw
                ON paper_embeddings
                USING hnsw (vector vector_l2_ops)
                WITH (m = 16, ef_construction = 64);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropForeignKey(
                name: "FK_paper_embeddings_document_chunks_DocumentChunkId",
                table: "paper_embeddings");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropIndex(
                name: "IX_paper_embeddings_DocumentChunkId",
                table: "paper_embeddings");

            migrationBuilder.DropColumn(
                name: "DocumentChunkId",
                table: "paper_embeddings");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_paper_embeddings_vector_hnsw;");
        }
    }
}
