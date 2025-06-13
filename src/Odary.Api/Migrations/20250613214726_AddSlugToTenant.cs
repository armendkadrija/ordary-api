using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the slug column as nullable first
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Update existing tenants with slugs based on their names
            migrationBuilder.Sql(@"
                UPDATE tenants 
                SET slug = LOWER(REGEXP_REPLACE(REGEXP_REPLACE(name, '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g'))
                WHERE slug IS NULL;
            ");

            // Make the column non-nullable
            migrationBuilder.AlterColumn<string>(
                name: "slug",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            // Create unique index
            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tenants_slug",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "tenants");
        }
    }
}
