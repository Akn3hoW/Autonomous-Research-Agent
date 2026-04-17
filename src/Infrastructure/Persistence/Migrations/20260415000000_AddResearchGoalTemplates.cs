using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutonomousResearchAgent.Infrastructure.Persistence.Migrations
{
    public partial class AddResearchGoalTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.CreateTable(
                name: "research_goal_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GoalType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PromptTemplate = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Parameters = table.Column<string>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_goal_templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_research_goal_templates_IsActive",
                table: "research_goal_templates",
                column: "IsActive");

            var templates = new[]
            {
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Systematic Review",
                    Description = "Conduct a comprehensive systematic review following PRISMA guidelines",
                    GoalType = "SystematicReview",
                    PromptTemplate = "Conduct a systematic review on {{topic}}. Focus on {{papers}} papers. Research questions: {{objective}}",
                    Parameters = JsonSerializer.Serialize(new[] { "topic", "papers", "objective" }),
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Competitive Analysis",
                    Description = "Analyze the competitive landscape and compare different approaches",
                    GoalType = "CompetitiveAnalysis",
                    PromptTemplate = "Analyze competitive landscape for {{topic}}. Compare approaches: {{comparison_baseline}}. Papers to analyze: {{papers}}",
                    Parameters = JsonSerializer.Serialize(new[] { "topic", "comparison_baseline", "papers" }),
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Name = "Replication Check",
                    Description = "Verify and replicate findings from original studies",
                    GoalType = "ReplicationCheck",
                    PromptTemplate = "Check replication of {{original_study}} using {{papers}}. Method comparison: {{method_details}}",
                    Parameters = JsonSerializer.Serialize(new[] { "original_study", "papers", "method_details" }),
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Name = "Meta-Analysis",
                    Description = "Perform statistical meta-analysis across multiple studies",
                    GoalType = "MetaAnalysis",
                    PromptTemplate = "Perform meta-analysis on {{topic}} across {{papers}} studies. Effect sizes to compare: {{effect_sizes}}",
                    Parameters = JsonSerializer.Serialize(new[] { "topic", "papers", "effect_sizes" }),
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };

            foreach (var template in templates)
            {
                migrationBuilder.InsertData(
                    table: "research_goal_templates",
                    columns: new[] { "Id", "Name", "Description", "GoalType", "PromptTemplate", "Parameters", "IsActive", "CreatedAt", "UpdatedAt" },
                    values: new object[] { template.Id, template.Name, template.Description, template.GoalType, template.PromptTemplate, template.Parameters, template.IsActive, template.CreatedAt, template.UpdatedAt });
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropTable(
                name: "research_goal_templates");
        }
    }
}