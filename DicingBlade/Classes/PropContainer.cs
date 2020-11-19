namespace DicingBlade.Classes
{
    public static class PropContainer
    {
        public static int Counter { get; set; }
        public static bool IsRound { get; set; }
        public static Wafer Wafer { get; set; }
        public static ITechnology Technology { get; set; }
        public static IWafer WaferTemp { get; set; }
    }
}
