using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperEmbeddingVectorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.Sql(
                @"CREATE INDEX ""IX_paper_embeddings_vector_hnsw"" 
                  ON ""paper_embeddings"" 
                  USING hnsw (""Vector"" vector_cosine_ops)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_paper_embeddings_vector_hnsw\";");
        }
    }
}