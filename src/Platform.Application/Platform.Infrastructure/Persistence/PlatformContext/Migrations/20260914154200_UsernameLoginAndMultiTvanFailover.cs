using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.PlatformContext.Migrations
{
    /// <inheritdoc />
    public partial class UsernameLoginAndMultiTvanFailover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TvanTransactions_ProviderCode_CorrelationKey",
                table: "TvanTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TvanTenantBindings_TaxCode",
                table: "TvanTenantBindings");

            migrationBuilder.DropColumn(
                name: "TvanProviderCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "TvanTransactions");

            migrationBuilder.DropColumn(
                name: "FallbackProviderId",
                table: "TvanTenantBindings");

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderPlan",
                table: "TvanTransactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RouteSource",
                table: "TvanTransactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderCode",
                table: "TvanTransactionAttempts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "TvanTenantBindings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserTvanProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTvanProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTvanProviders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill trước khi dựng unique index, nếu không index sẽ đụng hàng loạt chuỗi rỗng.
            // Tài khoản cũ đăng nhập bằng email nên lấy phần trước @ làm username.
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "UserName" = lower(split_part("Email", '@', 1))
                WHERE "UserName" = '';

                UPDATE "Users" u
                SET "UserName" = u."UserName" || '.' || substr(u."Id"::text, 1, 8)
                WHERE EXISTS (
                    SELECT 1 FROM "Users" d
                    WHERE d."UserName" = u."UserName" AND d."Id" <> u."Id");
                """);

            // Giao dịch cũ chỉ biết một nhà truyền nhận — đó chính là kế hoạch định tuyến của nó.
            migrationBuilder.Sql(
                """
                UPDATE "TvanTransactions" SET "ProviderPlan" = "ProviderCode" WHERE "ProviderPlan" = '';

                UPDATE "TvanTransactionAttempts" a
                SET "ProviderCode" = t."ProviderCode"
                FROM "TvanTransactions" t
                WHERE a."TransactionId" = t."Id" AND a."ProviderCode" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanTransactions_CorrelationKey",
                table: "TvanTransactions",
                column: "CorrelationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanTenantBindings_TaxCode_Priority",
                table: "TvanTenantBindings",
                columns: new[] { "TaxCode", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_TvanTenantBindings_TaxCode_ProviderId",
                table: "TvanTenantBindings",
                columns: new[] { "TaxCode", "ProviderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTvanProviders_UserId_Priority",
                table: "UserTvanProviders",
                columns: new[] { "UserId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTvanProviders_UserId_ProviderCode",
                table: "UserTvanProviders",
                columns: new[] { "UserId", "ProviderCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTvanProviders");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserName",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TvanTransactions_CorrelationKey",
                table: "TvanTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TvanTenantBindings_TaxCode_Priority",
                table: "TvanTenantBindings");

            migrationBuilder.DropIndex(
                name: "IX_TvanTenantBindings_TaxCode_ProviderId",
                table: "TvanTenantBindings");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProviderPlan",
                table: "TvanTransactions");

            migrationBuilder.DropColumn(
                name: "RouteSource",
                table: "TvanTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderCode",
                table: "TvanTransactionAttempts");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "TvanTenantBindings");

            migrationBuilder.AddColumn<string>(
                name: "TvanProviderCode",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderId",
                table: "TvanTransactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FallbackProviderId",
                table: "TvanTenantBindings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanTransactions_ProviderCode_CorrelationKey",
                table: "TvanTransactions",
                columns: new[] { "ProviderCode", "CorrelationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanTenantBindings_TaxCode",
                table: "TvanTenantBindings",
                column: "TaxCode",
                unique: true,
                filter: "\"IsActive\" = true");
        }
    }
}
