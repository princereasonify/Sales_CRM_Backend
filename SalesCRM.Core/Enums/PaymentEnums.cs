namespace SalesCRM.Core.Enums;

public enum PaymentMethod
{
    UPI,
    BankTransfer,
    Cheque,
    Cash,
    CreditCard,
    DebitCard,
    Other
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Refunded
}
