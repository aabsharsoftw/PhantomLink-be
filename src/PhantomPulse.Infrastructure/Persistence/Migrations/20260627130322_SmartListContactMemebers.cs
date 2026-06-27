using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhantomPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SmartListContactMemebers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contact_smart_list_members",
                columns: table => new
                {
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    smart_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_smart_list_members", x => new { x.contact_id, x.smart_list_id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_smart_list_members");
        }
    }
}
