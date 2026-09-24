using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MajdsApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    PermissionName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsGranted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionName });
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PermissionName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsGranted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => new { x.UserId, x.PermissionName });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserPermissions");
        }
    }
}
