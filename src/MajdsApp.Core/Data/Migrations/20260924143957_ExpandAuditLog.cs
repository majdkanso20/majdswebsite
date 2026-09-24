using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MajdsApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientBrowser",
                table: "AuditLogEntries",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientIp",
                table: "AuditLogEntries",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMs",
                table: "AuditLogEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: "AuditLogEntries",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                table: "AuditLogEntries",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "AuditLogEntries",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Success");

            migrationBuilder.AddColumn<string>(
                name: "Parameters",
                table: "AuditLogEntries",
                type: "TEXT",
                maxLength: 4100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Succeeded",
                table: "AuditLogEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "AuditLogEntries",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditEntityChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AuditLogEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ChangeType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntityChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEntityChanges_AuditLogEntries_AuditLogEntryId",
                        column: x => x.AuditLogEntryId,
                        principalTable: "AuditLogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditPropertyChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityChangeId = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OriginalValue = table.Column<string>(type: "TEXT", maxLength: 510, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 510, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditPropertyChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditPropertyChanges_AuditEntityChanges_EntityChangeId",
                        column: x => x.EntityChangeId,
                        principalTable: "AuditEntityChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_CreatedAt",
                table: "AuditLogEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntityChanges_AuditLogEntryId",
                table: "AuditEntityChanges",
                column: "AuditLogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditPropertyChanges_EntityChangeId",
                table: "AuditPropertyChanges",
                column: "EntityChangeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditPropertyChanges");

            migrationBuilder.DropTable(
                name: "AuditEntityChanges");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogEntries_CreatedAt",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "ClientBrowser",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "ClientIp",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "Error",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "HttpMethod",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "Parameters",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "Succeeded",
                table: "AuditLogEntries");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "AuditLogEntries");
        }
    }
}
