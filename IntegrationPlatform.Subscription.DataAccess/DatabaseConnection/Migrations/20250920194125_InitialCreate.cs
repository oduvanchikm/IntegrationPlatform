using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntegrationPlatform.Subscription.API.DatabaseConnection.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "subscription");

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "subscription",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameProduct = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProductType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataInterfaces",
                schema: "subscription",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    InterfaceType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                    ProductId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataInterfaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataInterfaces_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "subscription",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiInterface",
                schema: "subscription",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Host = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Port = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Token = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiInterface", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiInterface_DataInterfaces_Id",
                        column: x => x.Id,
                        principalSchema: "subscription",
                        principalTable: "DataInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DatabaseInterface",
                schema: "subscription",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Host = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Port = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Username = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Password = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DatabaseName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Scheme = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseInterface", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DatabaseInterface_DataInterfaces_Id",
                        column: x => x.Id,
                        principalSchema: "subscription",
                        principalTable: "DataInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KafkaInterface",
                schema: "subscription",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    BootstrapServers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Password = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TopicName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KafkaInterface", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KafkaInterface_DataInterfaces_Id",
                        column: x => x.Id,
                        principalSchema: "subscription",
                        principalTable: "DataInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrchestrationConfig",
                schema: "subscription",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DataInterfaceId = table.Column<int>(type: "integer", nullable: false),
                    ScheduleCron = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "*/5 * * * *"),
                    MaxRetryAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    RetryDelaySeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    ExecutionTimeoutSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrchestrationConfig", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrchestrationConfig_DataInterfaces_DataInterfaceId",
                        column: x => x.DataInterfaceId,
                        principalSchema: "subscription",
                        principalTable: "DataInterfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataInterfaces_ProductId",
                schema: "subscription",
                table: "DataInterfaces",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrchestrationConfig_DataInterfaceId",
                schema: "subscription",
                table: "OrchestrationConfig",
                column: "DataInterfaceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiInterface",
                schema: "subscription");

            migrationBuilder.DropTable(
                name: "DatabaseInterface",
                schema: "subscription");

            migrationBuilder.DropTable(
                name: "KafkaInterface",
                schema: "subscription");

            migrationBuilder.DropTable(
                name: "OrchestrationConfig",
                schema: "subscription");

            migrationBuilder.DropTable(
                name: "DataInterfaces",
                schema: "subscription");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "subscription");
        }
    }
}
