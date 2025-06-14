using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odary.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePatientMedicalInfoToPostgreSQLArrays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<string>>(
                name: "medical_conditions",
                table: "patients",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<List<string>>(
                name: "current_medications",
                table: "patients",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<List<string>>(
                name: "allergies",
                table: "patients",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "medical_conditions",
                table: "patients",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string>(
                name: "current_medications",
                table: "patients",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string>(
                name: "allergies",
                table: "patients",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "text[]");
        }
    }
}
