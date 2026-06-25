using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildCv.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSubscriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "subscriptions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                plan = table.Column<int>(type: "integer", nullable: false),
                payment_source_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                last_charge_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                next_charge_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_subscriptions", x => x.id);
                table.CheckConstraint("ck_subscriptions_plan", "plan IN (1,2)");
                table.CheckConstraint("ck_subscriptions_retry_count", "retry_count >= 0 AND retry_count <= 3");
                table.CheckConstraint("ck_subscriptions_status", "status IN (1,2,3)");
                table.ForeignKey(
                    name: "FK_subscriptions_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_subscriptions_status_next_charge",
            table: "subscriptions",
            columns: new[] { "status", "next_charge_at" });

        migrationBuilder.CreateIndex(
            name: "ux_subscriptions_user_active",
            table: "subscriptions",
            column: "user_id",
            unique: true,
            filter: "status != 3");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "subscriptions");
    }
}
