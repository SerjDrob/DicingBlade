namespace DicingBlade.Classes
{
    class TracePath
    {
        public TracePath(double Y, double X, double Xend, double InitAngle)
        {
            this.Y = Y;
            this.X = X;
            this.Xend = Xend;
            this.InitAngle = InitAngle;
        }
        public double Y { get; set; }
        public double X { get; set; }
        public double Xend { get; set; }
        public double InitAngle { get; set; }
    }
}