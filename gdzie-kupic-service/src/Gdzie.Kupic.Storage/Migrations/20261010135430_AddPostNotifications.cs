using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gdzie.Kupic.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddPostNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MerchantSubscriptions_CategoryId",
                table: "MerchantSubscriptions");

            migrationBuilder.CreateTable(
                name: "PostNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostNotifications_Merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "Merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostNotifications_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_CategoryId_TagId",
                table: "MerchantSubscriptions",
                columns: new[] { "CategoryId", "TagId" });

            migrationBuilder.CreateIndex(
                name: "IX_PostNotifications_MerchantId",
                table: "PostNotifications",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_PostNotifications_PostId_MerchantId",
                table: "PostNotifications",
                columns: new[] { "PostId", "MerchantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostNotifications");

            migrationBuilder.DropIndex(
                name: "IX_MerchantSubscriptions_CategoryId_TagId",
                table: "MerchantSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_CategoryId",
                table: "MerchantSubscriptions",
                column: "CategoryId");
        }
    }
}
