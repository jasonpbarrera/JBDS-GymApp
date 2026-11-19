namespace JBDS.GymApp.Shared.Enums;

public enum MembershipStatus
{
    Active = 1,
    Trial = 2,
    PastDue = 3,
    Paused = 4,
    Cancelled = 5,
    Expired = 6
}

public enum BillingInterval
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Annual = 4,
    DayPass = 5
}

public enum CheckInMethod
{
    QrCode = 1,
    Barcode = 2,
    ManualDesk = 3,
    MobileApp = 4
}

public enum BookingStatus
{
    Confirmed = 1,
    Waitlisted = 2,
    CheckedIn = 3,
    Cancelled = 4,
    NoShow = 5
}

public enum InvoiceStatus
{
    Draft = 1,
    Open = 2,
    Paid = 3,
    PastDue = 4,
    Void = 5
}

public enum PaymentMethod
{
    StripeCard = 1,
    Cash = 2,
    CardTerminal = 3,
    BankTransfer = 4
}
