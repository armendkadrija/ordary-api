using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    first_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    street = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    insurance_provider = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    insurance_policy_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    allergies = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    medical_conditions = table.Column<string>(type: "text", nullable: true),
                    current_medications = table.Column<string>(type: "text", nullable: true),
                    emergency_contact_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    emergency_contact_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    emergency_contact_relationship = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archive_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patients", x => x.id);
                    table.ForeignKey(
                        name: "fk_patients_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_patients_email",
                table: "patients",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_patients_first_name_last_name",
                table: "patients",
                columns: new[] { "first_name", "last_name" });

            migrationBuilder.CreateIndex(
                name: "ix_patients_is_archived",
                table: "patients",
                column: "is_archived");

            migrationBuilder.CreateIndex(
                name: "ix_patients_phone_number",
                table: "patients",
                column: "phone_number");

            migrationBuilder.CreateIndex(
                name: "ix_patients_tenant_id",
                table: "patients",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patients");
        }
    }
}
