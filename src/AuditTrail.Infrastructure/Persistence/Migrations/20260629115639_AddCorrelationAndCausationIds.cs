using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditTrail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationAndCausationIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CausationId",
                table: "AuditEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditEvents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CorrelationId",
                table: "AuditEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType",
                table: "AuditEvents",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredBy",
                table: "AuditEvents",
                column: "OccurredBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_CorrelationId",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_EntityType",
                table: "AuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditEvents_OccurredBy",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "CausationId",
                table: "AuditEvents");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditEvents");
        }
    }
}
