namespace FruitInventoryAPI.Models
{
    public class Inventory
    {
        public int InventoryID { get; set; }
        public int FruitID { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }   
}