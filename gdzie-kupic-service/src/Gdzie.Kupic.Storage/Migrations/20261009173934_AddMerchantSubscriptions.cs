using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gdzie.Kupic.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantSubscriptions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MerchantSubscriptions_Merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "Merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MerchantSubscriptions_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_CategoryId",
                table: "MerchantSubscriptions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_MerchantId",
                table: "MerchantSubscriptions",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_MerchantId_CategoryId_NoTag",
                table: "MerchantSubscriptions",
                columns: new[] { "MerchantId", "CategoryId" },
                unique: true,
                filter: "\"TagId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_MerchantId_CategoryId_TagId",
                table: "MerchantSubscriptions",
                columns: new[] { "MerchantId", "CategoryId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_TagId",
                table: "MerchantSubscriptions",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantSubscriptions");
        }
    }
}
