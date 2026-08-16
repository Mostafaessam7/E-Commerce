namespace Inventory.Domain;

public enum StockTransactionType
{
    Received = 0,
    Reserved = 1,
    ReservationReleased = 2,
    ReservationConfirmed = 3,
    Adjusted = 4,
}
