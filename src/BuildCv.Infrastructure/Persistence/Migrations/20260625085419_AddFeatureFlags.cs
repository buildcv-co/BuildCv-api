using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildCv.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_flag_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flag_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<bool>(type: "boolean", nullable: true),
                    new_value = table.Column<bool>(type: "boolean", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flag_audit_log", x => x.id);
                    table.CheckConstraint("ck_feature_flag_audit_log_new_value_not_null", "new_value IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "feature_flags",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_value = table.Column<bool>(type: "boolean", nullable: false),
                    current_value = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flags", x => x.name);
                    table.CheckConstraint("ck_feature_flags_current_value_not_null", "current_value IS NOT NULL");
                });

            migrationBuilder.CreateIndex(
                name: "ix_feature_flag_audit_log_flag_name_changed_at",
                table: "feature_flag_audit_log",
                columns: new[] { "flag_name", "changed_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_flag_audit_log");

            migrationBuilder.DropTable(
                name: "feature_flags");
        }
    }
}
