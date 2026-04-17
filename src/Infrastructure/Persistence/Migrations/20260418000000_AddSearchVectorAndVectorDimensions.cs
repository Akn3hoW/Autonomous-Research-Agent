using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

public partial class AddSearchVectorAndVectorDimensions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql(@"
            ALTER TABLE ""papers"" ADD COLUMN IF NOT EXISTS ""SearchVector"" tsvector
            GENERATED ALWAYS AS (setweight(to_tsvector('english', coalesce(""Title"", '')), 'A') ||
                                setweight(to_tsvector('english', coalesce(""Abstract"", '')), 'B')) STORED;
        ");

        migrationBuilder.Sql(@"
            ALTER TABLE ""paper_summaries"" ADD COLUMN IF NOT EXISTS ""SearchVector"" tsvector
            GENERATED ALWAYS AS (to_tsvector('english', coalesce(""SearchText"", ''))) STORED;
        ");

        migrationBuilder.Sql(@"
            ALTER TABLE ""paper_documents"" ADD COLUMN IF NOT EXISTS ""SearchVector"" tsvector
            GENERATED ALWAYS AS (to_tsvector('english', coalesce(""ExtractedText"", ''))) STORED;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_papers_SearchVector"" ON ""papers"" USING gin (""SearchVector"");
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_paper_summaries_SearchVector"" ON ""paper_summaries"" USING gin (""SearchVector"");
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_paper_documents_SearchVector"" ON ""paper_documents"" USING gin (""SearchVector"");
        ");

        migrationBuilder.Sql(@"
            ALTER TABLE ""paper_embeddings"" ADD COLUMN IF NOT EXISTS ""VectorDimensions"" integer;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_papers_SearchVector\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_paper_summaries_SearchVector\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_paper_documents_SearchVector\";");

        migrationBuilder.Sql("ALTER TABLE \"papers\" DROP COLUMN IF EXISTS \"SearchVector\";");
        migrationBuilder.Sql("ALTER TABLE \"paper_summaries\" DROP COLUMN IF EXISTS \"SearchVector\";");
        migrationBuilder.Sql("ALTER TABLE \"paper_documents\" DROP COLUMN IF EXISTS \"SearchVector\";");

        migrationBuilder.Sql("ALTER TABLE \"paper_embeddings\" DROP COLUMN IF EXISTS \"VectorDimensions\";");
    }
}
