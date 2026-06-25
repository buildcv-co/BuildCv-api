using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildCv.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddIterationResults : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "iteration_requests",
            columns: table => new
            {
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                cv_text = table.Column<string>(type: "text", nullable: false),
                vacancy_text = table.Column<string>(type: "text", nullable: false),
                iteration_count = table.Column<int>(type: "integer", nullable: false),
                probability_threshold = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_iteration_requests", x => x.request_id);
                table.ForeignKey(
                    name: "FK_iteration_requests_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "iteration_results",
            columns: table => new
            {
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                best_step = table.Column<string>(type: "jsonb", nullable: true),
                all_steps = table.Column<string>(type: "jsonb", nullable: false),
                probability_warning = table.Column<string>(type: "text", nullable: true),
                credits_consumed = table.Column<int>(type: "integer", nullable: false),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_iteration_results", x => x.request_id);
                table.ForeignKey(
                    name: "FK_iteration_results_iteration_requests_request_id",
                    column: x => x.request_id,
                    principalTable: "iteration_requests",
                    principalColumn: "request_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_iteration_requests_status_created_at",
            table: "iteration_requests",
            columns: new[] { "status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_iteration_requests_user_created_at",
            table: "iteration_requests",
            columns: new[] { "user_id", "created_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_iteration_results_expires_at",
            table: "iteration_results",
            column: "expires_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "iteration_results");

        migrationBuilder.DropTable(
            name: "iteration_requests");
    }
}
