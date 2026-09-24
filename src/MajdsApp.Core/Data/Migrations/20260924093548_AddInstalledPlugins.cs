using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MajdsApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstalledPlugins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstalledPlugins",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AssemblyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstalledPlugins", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstalledPlugins");
        }
    }
}
