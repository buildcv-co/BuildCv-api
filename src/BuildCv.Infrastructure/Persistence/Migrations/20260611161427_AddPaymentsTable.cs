using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildCv.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPaymentsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Invoices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentType = table.Column<int>(type: "integer", maxLength: 50, nullable: false),
                ReferenceCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Cufe = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Uuid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                AmountInCents = table.Column<long>(type: "bigint", nullable: false),
                Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Status = table.Column<int>(type: "integer", maxLength: 50, nullable: false),
                CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CustomerIdentification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CustomerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CustomerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CustomerAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CustomerCompany = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CustomerTradeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CustomerLegalOrganizationCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                CustomerTributeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                CustomerMunicipalityCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                CustomerIdentificationDocumentCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                ItemsJson = table.Column<string>(type: "text", nullable: false),
                ItemsDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                PaymentDetailsJson = table.Column<string>(type: "text", nullable: false),
                PaymentMethodCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                ProviderRaw = table.Column<string>(type: "text", nullable: true),
                ProviderId = table.Column<string>(type: "text", nullable: true),
                FactusResponseJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                ErrorJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "NumberingRanges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderId = table.Column<int>(type: "integer", nullable: false),
                Prefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                From = table.Column<int>(type: "integer", nullable: false),
                To = table.Column<int>(type: "integer", nullable: false),
                Current = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NumberingRanges", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                package_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                credits = table.Column<int>(type: "integer", nullable: false),
                amount_in_cents = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                wompi_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                wompi_payment_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                provider_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payments", x => x.id);
                table.CheckConstraint("CK_payments_amount_positive", "amount_in_cents > 0");
                table.CheckConstraint("CK_payments_credits_positive", "credits > 0");
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                provider_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "consent_records",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                policy_version = table.Column<int>(type: "integer", nullable: false),
                consent_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_consent_records", x => x.id);
                table.ForeignKey(
                    name: "FK_consent_records_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "data_treatment_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                data_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_data_treatment_logs", x => x.id);
                table.ForeignKey(
                    name: "FK_data_treatment_logs_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            columns: table => new
            {
                token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refresh_tokens", x => x.token);
                table.ForeignKey(
                    name: "FK_refresh_tokens_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_consent_records_user_id_purpose",
            table: "consent_records",
            columns: new[] { "user_id", "purpose" });

        migrationBuilder.CreateIndex(
            name: "IX_data_treatment_logs_user_id",
            table: "data_treatment_logs",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_Number",
            table: "Invoices",
            column: "Number");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_ReferenceCode",
            table: "Invoices",
            column: "ReferenceCode",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_UserId",
            table: "Invoices",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_NumberingRanges_ProviderId",
            table: "NumberingRanges",
            column: "ProviderId");

        migrationBuilder.CreateIndex(
            name: "IX_payments_user_id_created_at",
            table: "payments",
            columns: new[] { "user_id", "created_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "UX_payments_idempotency_key",
            table: "payments",
            column: "idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_payments_wompi_transaction_id",
            table: "payments",
            column: "wompi_transaction_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_user_id",
            table: "refresh_tokens",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_users_provider_provider_id",
            table: "users",
            columns: new[] { "provider", "provider_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "consent_records");

        migrationBuilder.DropTable(
            name: "data_treatment_logs");

        migrationBuilder.DropTable(
            name: "Invoices");

        migrationBuilder.DropTable(
            name: "NumberingRanges");

        migrationBuilder.DropTable(
            name: "payments");

        migrationBuilder.DropTable(
            name: "refresh_tokens");

        migrationBuilder.DropTable(
            name: "users");
    }
}
