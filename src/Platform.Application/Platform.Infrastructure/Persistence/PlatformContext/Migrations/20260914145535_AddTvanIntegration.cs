using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.PlatformContext.Migrations
{
    /// <inheritdoc />
    public partial class AddTvanIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TvanProviderCode",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TvanProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    AuthSchemeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResilienceConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    CallbackMapJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvanProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TvanTenantBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FallbackProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvanTenantBindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TvanTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestXml = table.Column<string>(type: "text", nullable: false),
                    FieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResultCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ResultMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvanTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TvanCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProtectedSecretsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvanCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvanCredentials_TvanProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "TvanProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TvanEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PathTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SoapAction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BodyTemplate = table.Column<string>(type: "text", nullable: false),
                    RequestTransformsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResponseMapJson = table.Column<string>(type: "jsonb", nullable: false),
                    ArgsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvanEndpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvanEndpoints_TvanProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "TvanProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TvanHeaderTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValueTemplate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvanHeaderTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvanHeaderTemplates_TvanProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "TvanProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TvanTransactionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNo = table.Column<int>(type: "integer", nullable: false),
                    RequestUri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequestHeaders = table.Column<string>(type: "text", nullable: true),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    ResponseSnapshot = table.Column<string>(type: "text", nullable: true),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvanTransactionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TvanTransactionAttempts_TvanTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "TvanTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TvanCredentials_ProviderId_TaxCode",
                table: "TvanCredentials",
                columns: new[] { "ProviderId", "TaxCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanEndpoints_ProviderId_OperationCode",
                table: "TvanEndpoints",
                columns: new[] { "ProviderId", "OperationCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanHeaderTemplates_ProviderId_EndpointId_Name",
                table: "TvanHeaderTemplates",
                columns: new[] { "ProviderId", "EndpointId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanProviders_Code_Environment",
                table: "TvanProviders",
                columns: new[] { "Code", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanTenantBindings_TaxCode",
                table: "TvanTenantBindings",
                column: "TaxCode",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_TvanTransactionAttempts_TransactionId",
                table: "TvanTransactionAttempts",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TvanTransactions_ProviderCode_CorrelationKey",
                table: "TvanTransactions",
                columns: new[] { "ProviderCode", "CorrelationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TvanTransactions_State_NextAttemptAt",
                table: "TvanTransactions",
                columns: new[] { "State", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TvanCredentials");

            migrationBuilder.DropTable(
                name: "TvanEndpoints");

            migrationBuilder.DropTable(
                name: "TvanHeaderTemplates");

            migrationBuilder.DropTable(
                name: "TvanTenantBindings");

            migrationBuilder.DropTable(
                name: "TvanTransactionAttempts");

            migrationBuilder.DropTable(
                name: "TvanProviders");

            migrationBuilder.DropTable(
                name: "TvanTransactions");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TvanProviderCode",
                table: "Users");
        }
    }
}
