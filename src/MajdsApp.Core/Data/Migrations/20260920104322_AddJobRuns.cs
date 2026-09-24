using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MajdsApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobRuns_JobName_StartedAt",
                table: "JobRuns",
                columns: new[] { "JobName", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRuns");
        }
    }
}
