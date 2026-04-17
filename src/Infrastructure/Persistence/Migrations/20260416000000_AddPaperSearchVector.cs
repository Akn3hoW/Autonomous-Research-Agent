using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.AddColumn<string>(
                name: "SearchVector",
                table: "papers",
                type: "tsvector",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_papers_SearchVector",
                table: "papers",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.Sql(
                "UPDATE \"papers\" SET \"SearchVector\" = " +
                "setweight(to_tsvector('english', COALESCE(\"Title\", '')), 'A') || " +
                "setweight(to_tsvector('english', COALESCE(\"Abstract\", '')), 'B') || " +
                "setweight(to_tsvector('english', COALESCE(array_to_string(\"Authors\"::text[], ''), '')), 'C');");

            migrationBuilder.Sql(
                "CREATE OR REPLACE FUNCTION update_paper_search_vector() RETURNS TRIGGER AS $$\n" +
                "BEGIN\n" +
                "    NEW.\"SearchVector\" :=\n" +
                "        setweight(to_tsvector('english', COALESCE(NEW.\"Title\", '')), 'A') ||\n" +
                "        setweight(to_tsvector('english', COALESCE(NEW.\"Abstract\", '')), 'B') ||\n" +
                "        setweight(to_tsvector('english', COALESCE(array_to_string(NEW.\"Authors\"::text[], ''), '')), 'C');\n" +
                "    RETURN NEW;\n" +
                "END;\n" +
                "$$ LANGUAGE plpgsql;");

            migrationBuilder.Sql(
                "CREATE TRIGGER trg_update_paper_search_vector " +
                "BEFORE INSERT OR UPDATE OF \"Title\", \"Abstract\", \"Authors\" " +
                "ON \"papers\" FOR EACH ROW " +
                "EXECUTE FUNCTION update_paper_search_vector();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_update_paper_search_vector ON \"papers\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_paper_search_vector();");
            migrationBuilder.DropIndex(name: "IX_papers_SearchVector", table: "papers");
            migrationBuilder.DropColumn(name: "SearchVector", table: "papers");
        }
    }
}