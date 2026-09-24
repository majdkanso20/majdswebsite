using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MajdsApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureOverrides",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureOverrides", x => x.Name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureOverrides");
        }
    }
}
