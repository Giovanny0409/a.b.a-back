using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackendAba.Migrations
{
    /// <inheritdoc />
    public partial class AddProductKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Los productos que ya existen toman el tipo de sus lotes: Unit (1) si solo tienen lotes de
            // piezas, Bag (0) en otro caso.
            migrationBuilder.Sql(
                "UPDATE Products SET Kind = 1 WHERE Id IN (SELECT ProductId FROM UnitLots) AND Id NOT IN (SELECT ProductId FROM BagLots)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Products");
        }
    }
}
