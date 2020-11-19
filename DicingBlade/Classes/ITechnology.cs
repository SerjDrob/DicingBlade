namespace DicingBlade.Classes
{
    //[AddINotifyPropertyChangedInterface]
    public interface ITechnology
    {
        string FileName { get; set; }
        int SpindleFreq { get; set; }
        double FeedSpeed { get; set; }
        double WaferBladeGap { get; set; }
        double FilmThickness { get; set; }
        double UnterCut { get; set; }
        int PassCount { get; set; }
        Directions PassType { get; set; }
        int StartControlNum { get; set; }
        int ControlPeriod { get; set; }
    }
}
