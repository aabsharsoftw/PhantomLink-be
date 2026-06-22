using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhantomPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "import_batch_id",
                table: "contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    imported = table.Column<int>(type: "integer", nullable: false),
                    skipped = table.Column<int>(type: "integer", nullable: false),
                    failed = table.Column<int>(type: "integer", nullable: false),
                    errors_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_batches", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contacts_import_batch_id",
                table: "contacts",
                column: "import_batch_id");

            migrationBuilder.AddForeignKey(
                name: "fk_contacts_import_batches_import_batch_id",
                table: "contacts",
                column: "import_batch_id",
                principalTable: "import_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_contacts_import_batches_import_batch_id",
                table: "contacts");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropIndex(
                name: "ix_contacts_import_batch_id",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "import_batch_id",
                table: "contacts");
        }
    }
}
