using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Orders.Dal.Migrations
{
    /// <inheritdoc />
    public partial class SeedSeedReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ProductRefs",
                columns: new[] { "ProductId", "IsActive", "Name", "SourceVersion", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-4000-8000-000000000001"), true, "Медовик", 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a0000000-0000-4000-8000-000000000002"), true, "Наполеон", 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a0000000-0000-4000-8000-000000000003"), true, "Круасан класичний", 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a0000000-0000-4000-8000-000000000004"), true, "Хліб на заквасці", 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a0000000-0000-4000-8000-000000000005"), true, "Брауні безглютенове", 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "VariantRefs",
                columns: new[] { "VariantId", "IsActive", "Name", "Price", "ProductId", "SourceVersion", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("d0000000-0000-4000-8000-000000000001"), true, "1 кг", 650.00m, new Guid("a0000000-0000-4000-8000-000000000001"), 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d0000000-0000-4000-8000-000000000002"), true, "1,5 кг", 950.00m, new Guid("a0000000-0000-4000-8000-000000000001"), 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d0000000-0000-4000-8000-000000000003"), true, "1 кг", 600.00m, new Guid("a0000000-0000-4000-8000-000000000002"), 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d0000000-0000-4000-8000-000000000004"), true, "1 шт", 55.00m, new Guid("a0000000-0000-4000-8000-000000000003"), 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d0000000-0000-4000-8000-000000000005"), true, "6 шт", 300.00m, new Guid("a0000000-0000-4000-8000-000000000003"), 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d0000000-0000-4000-8000-000000000006"), true, "500 г", 90.00m, new Guid("a0000000-0000-4000-8000-000000000004"), 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d0000000-0000-4000-8000-000000000007"), true, "6 порцій", 420.00m, new Guid("a0000000-0000-4000-8000-000000000005"), 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "VariantRefs",
                keyColumn: "VariantId",
                keyValue: new Guid("d0000000-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "VariantRefs",
                keyColumn: "VariantId",
                keyValue: new Guid("d0000000-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "VariantRefs",
                keyColumn: "VariantId",
                keyValue: new Guid("d0000000-0000-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "VariantRefs",
                keyColumn: "VariantId",
                keyValue: new Guid("d0000000-0000-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "VariantRefs",
                keyColumn: "VariantId",
                keyValue: new Guid("d0000000-0000-4000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "VariantRefs",
                keyColumn: "VariantId",
                keyValue: new Guid("d0000000-0000-4000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "VariantRefs",
                keyColumn: "VariantId",
                keyValue: new Guid("d0000000-0000-4000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "ProductRefs",
                keyColumn: "ProductId",
                keyValue: new Guid("a0000000-0000-4000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "ProductRefs",
                keyColumn: "ProductId",
                keyValue: new Guid("a0000000-0000-4000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "ProductRefs",
                keyColumn: "ProductId",
                keyValue: new Guid("a0000000-0000-4000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "ProductRefs",
                keyColumn: "ProductId",
                keyValue: new Guid("a0000000-0000-4000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "ProductRefs",
                keyColumn: "ProductId",
                keyValue: new Guid("a0000000-0000-4000-8000-000000000005"));
        }
    }
}
