using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DicingBlade.Classes
{
    //public struct Point 
    //{
    //    public double X;
    //    public double Y;
    //}
    //public struct Line
    //{
    //    public Point Start;
    //    public Point End;
    //}
    interface IWafer2
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double IndexW { get; set; }
        public double IndexH { get; set; }
        public double Thickness { get; set; }
     //   public Line this[int angle, int lineNum] { get; set; }


        //#region ???
        //public bool CurrentCutIsDone(int currentLine)
        //{
        //    return !GetCurrentCut(currentLine).Status;
        //}
        //#endregion

        //public int DirectionLinesCount => Grid.Lines[Directions[CurrentAngleNum].angle].Count;

        //public int DirectionsCount => Directions.Count;
        //public int CurrentAngleNum { get; private set; }
        //private Grid Grid { get; set; }
       
        //private List<(double angle, double actualAngle, double indexShift)> Directions { get; set; }
        //public double GetCurrentDiretionAngle => Directions[CurrentAngleNum].angle;

        //public double GetCurrentDiretionActualAngle => Directions[CurrentAngleNum].actualAngle;

        //public double GetPrevDiretionAngle => CurrentAngleNum != 0 ? Directions[CurrentAngleNum - 1].angle : Directions.Last().angle;

        //public double GetPrevDiretionActualAngle => CurrentAngleNum != 0 ? Directions[CurrentAngleNum - 1].actualAngle : Directions.Last().actualAngle;

        //public double SetCurrentDirectionAngle
        //{
        //    set => Directions[CurrentAngleNum] = (Directions[CurrentAngleNum].angle, value, Directions[CurrentAngleNum].indexShift);
        //}
        //public double AddToCurrentDirectionIndexShift
        //{
        //    set
        //    {
        //        var shift = Directions[CurrentAngleNum].indexShift;
        //        Directions[CurrentAngleNum] = (Directions[CurrentAngleNum].angle, Directions[CurrentAngleNum].actualAngle, shift + value);
        //        GetCurrentDirectionIndexShift = Directions[CurrentAngleNum].indexShift;
        //    }
        //}

        //private double _currentShift;
        //public double GetCurrentDirectionIndexShift //{ get; set; }
        //{
        //    set => _currentShift = value;
        //    get => -Directions[CurrentAngleNum].indexShift;
        //}
        //public Wafer() { }
        //public void ResetWafer()
        //{
        //    foreach (var cut in Grid.Lines.SelectMany(s => s.Value))
        //    {
        //        cut.ResetCut();
        //    }

        //    CurrentAngleNum = 0;
        //}
        //public void SetPassCount(int passes)
        //{
        //    foreach (var cut in Grid.Lines.SelectMany(s => s.Value))
        //    {
        //        cut.CutCount = passes;
        //    }
        //}
        //public Wafer(double thickness, DxfDocument dxf, string layer)
        //{
        //    Thickness = thickness;
        //    Grid = new Grid(dxf.Lines.Where(l => l.Layer.Name == layer));
        //    MakeDirections(new List<double>(Grid.Lines.Keys));
        //    CurrentAngleNum = 0;
        //}
        //private void MakeDirections(List<double> list)
        //{
        //    Directions = new List<(double, double, double)>();
        //    foreach (var item in list)
        //    {
        //        Directions.Add((item, item, 0));
        //    }
        //}
        //public Wafer(Vector2 origin, double thickness, params (double degree, double length, double side, double index)[] directions)
        //{
        //    Thickness = thickness;
        //    Grid = new Grid(origin, directions);
        //    MakeDirections(new List<double>(Grid.Lines.Keys));
        //    CurrentAngleNum = 0;
        //    IsRound = false;
        //}
        //public Wafer(Vector2 origin, double thickness, double diameter, params (double degree, double index)[] directions)
        //{
        //    Thickness = thickness;
        //    Grid = new Grid(origin, diameter, directions);
        //    MakeDirections(new List<double>(Grid.Lines.Keys));
        //    CurrentAngleNum = 0;
        //    IsRound = true;
        //}
        //public WaferView MakeWaferView()
        //{
        //    var tempView = Grid.MakeGridView();
        //    tempView.IsRound = IsRound;
        //    return tempView;
        //}
        //public bool NextDir(bool reset = false)
        //{
        //    if (Directions.Count - 1 == CurrentAngleNum)
        //    {
        //        if (reset) CurrentAngleNum = 0;
        //        return false;
        //    }

        //    CurrentAngleNum++;
        //    return true;
        //}
        //public bool PrevDir()
        //{
        //    if (CurrentAngleNum == 0)
        //    {
        //        CurrentAngleNum = Directions.Count - 1;
        //        return false;
        //    }

        //    CurrentAngleNum--;
        //    return true;
        //}
        ////private void RotateWafer(double angle, Vector2 origin) => Grid.RotateRawLines(angle);
        //public Cut GetNearestCut(double y)
        //{
        //    int index = 0;
        //    var angle = Directions[CurrentAngleNum].angle;
        //    var gridLine = Grid.Lines[angle];

        //    double diff = Math.Abs(gridLine[index].StartPoint.Y - y);

        //    for (int i = 0; i < gridLine.Count; i++)
        //    {
        //        if (Math.Abs(gridLine[i].StartPoint.Y - y) < diff)
        //        {
        //            diff = Math.Abs(gridLine[i].StartPoint.Y - y);
        //            index = i;
        //        }
        //    }
        //    return gridLine[index];
        //}
        //private Cut GetCurrentCut(int currentLine)
        //{
        //    return Grid.Lines[Directions[CurrentAngleNum].angle][currentLine];
        //}
        //public (Vector2 start, Vector2 end) GetCurrentLine(int currentLine)
        //{
        //    var (angle, _, indexShift) = Directions[CurrentAngleNum];

        //    var (s, e) = Grid.GetCenteredLine(angle, currentLine);
        //    s.Y += indexShift;
        //    e.Y += indexShift;
        //    return (s, e);
        //}
        //public double GetCurrentCutZ(int currentLine)
        //{
        //    double res = GetCurrentCut(currentLine).CutRatio;
        //    return (1 - res) * Thickness;
        //}
        //public void Test()
        //{
        //    //  List<Cut> cuts = Grid.Lines[CurrentAngleNum];
        //}
        //public bool CurrentCutIncrement(int currentLine)
        //{
        //    return Grid.Lines.GetItemByIndex(CurrentAngleNum)[currentLine].NextCut();
        //}
    }
}
