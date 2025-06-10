using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odary.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePasswordHistoryToArray : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert existing semicolon-separated strings to PostgreSQL arrays
            migrationBuilder.Sql(@"
                ALTER TABLE users 
                ALTER COLUMN password_history 
                TYPE text[] 
                USING string_to_array(password_history, ';');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert arrays back to semicolon-separated strings
            migrationBuilder.Sql(@"
                ALTER TABLE users 
                ALTER COLUMN password_history 
                TYPE text 
                USING array_to_string(password_history, ';');
            ");
        }
    }
}
