   M S B A M L   `   `   `  ���� F  BDicingBlade, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null9!clr-namespace:DicingBlade.ClassesDicingBlade.Classes  ?$clr-namespace:DicingBlade.ConvertersDicingBlade.Converters  ?$clr-namespace:DicingBlade.ViewModelsDicingBlade.ViewModels  b ^GalaSoft.MvvmLight.Platform, Version=5.4.1.0, Culture=neutral, PublicKeyToken=5f873c45e98af8a1lMclr-namespace:GalaSoft.MvvmLight.Command;assembly=GalaSoft.MvvmLight.PlatformGalaSoft.MvvmLight.Command O Kmscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e0891&clr-namespace:System;assembly=mscorlibSystem U QSystem.Windows, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3aG4clr-namespace:System.Windows;assembly=System.WindowsSystem.Windows &     DicingBlade.Views.MainWindowView   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     _ [Microsoft.Xaml.Behaviors, Version=1.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a3i+http://schemas.microsoft.com/xaml/behaviors  -local!clr-namespace:DicingBlade.Classes  5
converters$clr-namespace:DicingBlade.Converters  0model$clr-namespace:DicingBlade.ViewModels  WcmdMclr-namespace:GalaSoft.MvvmLight.Command;assembly=GalaSoft.MvvmLight.Platform  3System&clr-namespace:System;assembly=mscorlib  Asyswin4clr-namespace:System.Windows;assembly=System.Windows  
�������  � Title$  MainWindowView��$
��1024��$
��1280�� � WindowStartupLocation$ CenterScreen=� � 	Resources (  "DicingBlade.Classes.ScaleConverter   ScaleCoverterScheme,  &DicingBlade.Classes.TranslateConverter  TranslateConverterScheme3  -DicingBlade.Classes.TranslateConverterInverse # TranslateConverterSchemeInverse,  &DicingBlade.Classes.ConditionConverter  ConditionConvert-  'DicingBlade.Classes.VisibilityConverter  
VisConvert0  *DicingBlade.Classes.CrossPositionConverter  CrossConverter*  $DicingBlade.Classes.ToPointConverter  ToPointConvert1  +DicingBlade.Classes.TransformPointConverter  TransformPoint/	  )DicingBlade.Classes.CursorToPathConverter  	GetCursor0
  *DicingBlade.Converters.ZIndicatorConverter 	 ZIndConverter9  3DicingBlade.Converters.MatchEnumVisibilityConverter 
 MatchEnumConverter+  %DicingBlade.Converters.TraceConverter  TraceConverter0  *DicingBlade.Converters.TraceAngleConverter  TraceAngleConvereter.  (DicingBlade.Converters.GetRatioConverter  GetRatioConverter6  0DicingBlade.Converters.DoubleToDurationConverter  DoubleToDurationConverter5  /DicingBlade.Converters.WaferViewToPathConverter  WaferViewToPathConverter4  .DicingBlade.Converters.DisableBindingConverter  DisableBindingConverter-  'DicingBlade.Converters.MulZeroConverter  MulZeroConverter �� Color  SignalGreen  	SignalRed  Zero 	 Ycoor  PathAnimation �� TargetProperty �� Target �� ElementName	 W� By g� Duration	 �� 	Converter
 `� 	Converter �� Path��%�  &	        &	      &	 
     &	      &	      &	      &	      &	 #     &	 (     &		 -     &	
 2     &	 7     &	 <     &	 A     &	 F     &	 K     &	 P     &	 U     &	 Z     &	 w     &	 �     &	 �     &	 �     7  7  7                  	  
                  �� $ MediumSpringGreen���� $ 	IndianRed��X�0Q�X�
69.515Q��� .��W� $' !RenderTransform.Children[0].Angle� �� $ CutsView�� �� *	WVAngle+ �� *RotatingTime+8	   W� $' !RenderTransform.Children[0].Angle� �� $ 
TracesView�� �� *RotatingTime+8	   `� 8
  .1��� $ WVAngle��� $ Process.CutsRotate�*  $Microsoft.Xaml.Behaviors.Interaction   Triggers +  %Microsoft.Xaml.Behaviors.EventTrigger     	EventName$ KeyDown��*  $Microsoft.Xaml.Behaviors.TriggerBase   Actions. 2  ,Microsoft.Xaml.Behaviors.InvokeCommandAction     PassEventArgsToCommand .    Command �� *
KeyDownCmd+ �� Mode$ OneWay=�  $ KeyUp��.    .  �� *
KeyUpCmd+$ OneWay=�:  4Microsoft.Xaml.Behaviors.Core.PropertyChangedTrigger     Binding �� *
WVRotate+. <  6Microsoft.Xaml.Behaviors.Media.ControlStoryboardAction  5  /Microsoft.Xaml.Behaviors.Media.StoryboardAction   
Storyboard# [ .���    Rec1 �� 
TargetType �� Property �� Value��%�   &	       7  �� " ��.���  � ��$ 3q���  � ��$ 10����  � ��$ 10����  � ��8   ��  �@��RadiusX$ 5����  �@��RadiusY$ 5�� � ColumnDefinitions �� $��2* ��� $��* � � RowDefinitions �� $��* ��� $��* ��� $��* �.+��� $��Параметры��$��1��$��1��$��2��.���  �� �� .+��� $��1��.��� $��3q� �� �� ��  �� �� �� .+��� $��Подложка��$��3q����� *WaferSettingsCmd+�� $��Приводы��$��1��$��3q����� *MachineSettingsCmd+�� $��Технология��$��2��$��3q����� *TechnologySettingsCmd+�� $��1��$��1��$��3q��� $��2��$��1��$��3q��� $��2��$��3q��� $��1��$��2��$��3q��� $��2��$��2��$��3q�o� $��1��$��1q�$��3q�
���iii����� $ ActualWidth� �� RelativeSource ��  �� Mode$ FindAncestor=� �� AncestorType$
 MenuL����� $ ActualHeight� �� $ FindAncestor=�$
 MenuL����� # � ��.@�m� $��Center=����� .R��  J� RenderTransformOrigin$ 0.5,0.5&����� $ ActualWidth� �� $ FindAncestor=�$
 MenuL����� $ ActualHeight� �� $ FindAncestor=�$
 MenuL�.+��� $��Обучение��$��Center=�$��Center=�.@�m� $ ��_Масштаб видео�� m� Command �� *ToTeachVideoScaleCmd+m� $ ��_Масштаб видео�� �� *ToTeachChipSizeCmd+m� $ ��_Масштаб видео��m� $ ��_Масштаб видео��m� $ ��_Масштаб видео���� $��Наладка��.��� .+��� .+��� .+���  �� Orientation$ 
Horizontal=�.+��� $��ProcessStatus���� ���� *Process.ProcessStatus+�� $ 
Horizontal=�.+��� $��SideDone���� ���� *Process.SideDone+�� $ 
Horizontal=�.+��� $��SideCounter���� ���� *Process.SideCounter+�� $ 
Horizontal=�.+��� $��CurrentLine���� ���� *Process.CurrentLine+�� $ 
Horizontal=�.+��� $��SpindleFreq���� ���� *Machine.SpindleFreq+�� $ 
Horizontal=�.+��� $��SpindleCurrent���� ���� *Machine.SpindleCurrent+�� $��2q�
���   �$��30��._��� 
�������$��30��$	��150��.+��� 
�������$��50������ *ActualWidth+ �� " ��z�V� .~���  �� ScaleY$ -1Q�T�   T� Y  �� *ActualHeight+ �� $ FindAncestor=�$ CanvasL��� 
��� E��$��3��	! �� X1$! 0��	" �� X2" �� *ActualWidth+ �� " ��	# �� Y2# �� *Y1+ �� $
 Self=�	$ �� Y1$ `� #
 [	 .1��� $ ActualHeight� �� " ��$ FindAncestor=��� $ Wafer.Thickness�% �� FallbackValue$% 1���� $ Machine.Z.ActualPosition��� $ Machine.ZBladeTouch�z�V� .~��� $ -1Q�T�   �� *ActualHeight+ �� $ FindAncestor=�$ CanvasL��� $��2��
�������$��3q��� $��1��
�������$��3q�]� ���� *	Cols[0]+���� *	Rows[0]+.+�� $	��Top=��� }�. 
�������$��3q�$��Stretch=�.+�A� 
�������& K� ZIndex$& 1��y��� $ WaferView.IsRound�#	 [ ' �� ConverterParameter$' round������ $( "DataContext.WaferView.ShapeSize[0]�$% 0�� �� $ FindAncestor=�" ����� $( "DataContext.WaferView.ShapeSize[1]�$% 0�� �� $ FindAncestor=�" �z�V� .~�T� ( T� X( `� #
 [ .1��� $( "DataContext.WaferView.ShapeSize[0]�$% 0�� �� $ FindAncestor=�" �  `� #
 [ .1��� $( "DataContext.WaferView.ShapeSize[1]�$% 0�� �� $ FindAncestor=�" ��� ) �� Angle) �� $! DataContext.WaferView.Angle� �� $ FindAncestor=�" ��� * �� ScaleX* `� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" � `� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" �T� ( `� #
 [ .1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL�  `� #
 [ .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� -   + ��Name$+ SquareWafer��
�������$& 1��y��� $ WaferView.IsRound�#	 [ $' square������ $( "DataContext.WaferView.ShapeSize[0]�$% 0�� �� $ FindAncestor=�" ����� $( "DataContext.WaferView.ShapeSize[1]�$% 0�� �� $ FindAncestor=�" �   WaferAnimation��%�   &	       7  �� .��W� $' !RenderTransform.Children[1].Angle� ��  �� $ FindAncestor=�$ 	RectangleL� �� *	WVAngle+ �� *RotatingTime+8	       �� *
WVRotate+.   # [ z�V� .~�T� ( `� #
 [ .1��� $( "DataContext.WaferView.ShapeSize[0]�$% 0�� �� $ FindAncestor=�" �  `� #
 [ .1��� $( "DataContext.WaferView.ShapeSize[1]�$% 0�� �� $ FindAncestor=�" ��� �� * `� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" � `� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" �T� ( `� #
 [ .1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL�  `� #
 [ .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL�F� -   $��CutsView��
���2͚�$& 2������ *WaferView.RawLines+#	 [ ��`� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" ��� $ DataContext.Thickness� �� $ FindAncestor=�" �z�V� .~��� �� * `� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" � `� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" �T� ( `� #
 [ .1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL�  `� #
 [ .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL�F� -   , F�Name$, BladeCursor��
��� � �$��1��$& 3����`� #
 [ .1��� $ Machine.X.ActualPosition��� $ Machine.Y.ActualPosition��� $ ActualWidth� �� $ FindAncestor=�- �� AncestorLevel$- 1��$ CanvasL��� $ ActualHeight� �� $ FindAncestor=�$- 1��$ CanvasL��� $  Machine.BladeChuckCenter.X��� $  Machine.BladeChuckCenter.Y��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" ����� " F�. �� Triggers. �� / �� Binding/ �� $ ChuckCursorChk��$ 	IsChecked�0 �� Value	0 False.a���  � y�$ Hidden=�F� -   $, CameraCursor��
��� E��$��1��$& 3����`� #
 [ .1��� $ Machine.X.ActualPosition��� $ Machine.Y.ActualPosition��� $ ActualWidth� �� $ FindAncestor=�$- 1��$ CanvasL��� $ ActualHeight� �� $ FindAncestor=�$- 1��$ CanvasL��� $! Machine.CameraChuckCenter.X��� $! Machine.CameraChuckCenter.Y��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" ����� " F�. �� / �� $ CameraCursorChk��$ 	IsChecked�	0 False.a���  � y�$ Hidden=�F� -   $��
BladeTrace��
����  �$��2��1 �� StrokeDashArray$	1 4 4R�$& 4����`� #
 [ .1��� $ Process.TracingLine.X��� $ Process.TracingLine.Y��� $ Process.TracingLine.Xend��� $ ActualWidth� �� $ FindAncestor=�$- 1��$ CanvasL��� $ ActualHeight� �� $ FindAncestor=�$- 1��$ CanvasL��� $, &DataContext.Machine.BladeChuckCenter.X� �� $ FindAncestor=�" ��� $, &DataContext.Machine.BladeChuckCenter.Y� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" �z�T�   �� *%#Wafer.GetCurrentDirectionIndexShift+F� -   $��
TracesView��
���   �$& 2������ *Process.TracesView.RawLines+#	 [ ��`� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" ��� $ DataContext.Thickness� �� $ FindAncestor=�" �z�V� .~��� �� * `� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" � `� #
 [  .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $( "DataContext.WaferView.ShapeSize[0]� �� $ FindAncestor=�" ��� $( "DataContext.WaferView.ShapeSize[1]� �� $ FindAncestor=�" �T� ( `� #
 [ .1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL�  `� #
 [ .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� -   $��AnglePosition��
���2͚�$��2��$! 0��$ 0.5,1&�" �� $ ActualWidth� �� $ FindAncestor=�$ CanvasL�$ �� $ Y2� �� $
 Self=�# `� #
 [ .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL�z��� ) �� $* $DataContext.Machine.U.ActualPosition� �� $ FindAncestor=�" ��� $��2��$	��Top=�.���  �� $��* ��� $��* ��� $��* ��� $��* � �� �� �� �� .+��� ���� *Machine.X.ActualPosition+2 �� StringFormat$2 X:{0}���� $��1������ *Machine.Y.ActualPosition+$2 Y:{0}���� $��2������ *Machine.Z.ActualPosition+$2 Z:{0}���� $��3������ *Machine.U.ActualPosition+$2 U:{0}���� $��1������ *Machine.X.CmdPosition+$2 Xcmd:{0}���� $��1��$��1������ *Machine.Y.CmdPosition+$2 Ycmd:{0}���� $��1��$��2������ *Machine.Z.CmdPosition+$2 Zcmd:{0}���� $��1��$��3������ *Machine.U.CmdPosition+$2 Ucmd:{0}���� $ 
Horizontal=�$��2��.+��� ���� " ��3 �� BasedOn#3 [ . �� 0 True/ �� $ Machine.X.LmtN�.a���  � ��# [ �� ���� " ��#3 [ . �� 0 True/ �� $ Machine.X.LmtP�.a���  � ��# [ �� ���� " ��#3 [ . �� 0 True/ �� $ Machine.X.MotionDone�.a���  � ��# [ �� $ 
Horizontal=�$��2��$��1��.+��� ���� " ��#3 [ . �� 0 True/ �� $ Machine.Y.LmtN�.a���  � ��# [ �� ���� " ��#3 [ . �� 0 True/ �� $ Machine.Y.LmtP�.a���  � ��# [ �� ���� " ��#3 [ . �� 0 True/ �� $ Machine.Y.MotionDone�.a���  � ��# [ �� $ 
Horizontal=�$��2��$��2��.+��� ���� " ��#3 [ . �� 0 True/ �� $ Machine.Z.LmtN�.a���  � ��# [ �� ���� " ��#3 [ . �� 0 True/ �� $ Machine.Z.LmtP�.a���  � ��# [ �� $ 
Horizontal=�$��2��$��3��.+��� ���� " ��#3 [ . �� 0 True/ �� $ Machine.U.LmtN�.a���  � ��# [ �� ���� " ��#3 [ . �� 0 True/ �� $ Machine.U.LmtP�.a���  � ��# [ �� $��Быстро��$��3��y��� *Machine.VelocityRegime+#	 [
 "   DicingBlade.Classes.Velocity4   Fast#' Z4 �� $��Медленно��$��3��y��� *Machine.VelocityRegime+#	 [
 5   Slow#' Z5 �� $��Шаг��$��3��y��� *Machine.VelocityRegime+#	 [
 6   Step#' Z6 �� $��2��$
��Left=�$��Bottom=�.���� $ 
Horizontal=�.+��� $��Шпиндель��.���� .+��� $��Об/мин.��.���� .+�� 7 � Minimum$7 0Q�8 � Maximum$
8 1000Q�$��Center=�$��3q�$��10��$	��150��9 � Value9 �� *Machine.SpindleFreq+�� ���� *Machine.SpindleFreq+�� $��
Ток, А��.���� .+�� $7 0Q�$8 20Q�$��Center=�$��3q�$��10��$	��150��9 �� *Machine.SpindleCurrent+�� ���� *Machine.SpindleCurrent+�� $��СОЖ��.���� .+��� $��Бок.��.���� .+�� $7 0Q�$8 50000Q�$9 32300Q�$��Center=�$��3q�$��10��$	��150���� $��0���� $��Фронт.��.���� .+�� $7 0Q�$8 50000Q�$
9 3000Q�$��Center=�$��3q�$��10��$	��150���� $��0���� .+��� 
������ $��3q�._�]� ��.  .+��� $��Охл. шпинделя��$
��Left=��� $��3q�$��20��$��20��$��Right=����� " ��.���  � ��# [ . �� 0 True/ �� $ Machine.SpindleWater�.a���  � ��# [ �� $��3q����� " ��.���  � ��
 ���� . �� 0 True/ �� $" Machine.SwitchOnCoolantWater�.a���  � ��# [ ._�]� ��.  .+��� $��Вода��$
��Left=��� $��3q�$��20��$��20��$��Right=����� " ��.���  � ��# [ . �� 0 True/ �� $ Machine.CoolantWater�.a���  � ��# [ �� 
������ $��3q�._�]� ��.  .+��� $��Воздух��$
��Left=��� $��3q�$��20��$��20��$��Right=����� " ��.���  � ��# [ . �� 0 True/ �� $ Machine.Air�.a���  � ��# [ �� $��3q����� " ��.���  � ��
 ���� . �� 0 True/ �� $! Machine.SwitchOnChuckVacuum�.a���  � ��# [ ._�]� ��.  .+��� $��Вакуум��$
��Left=��� $��3q�$��20��$��20��$��Right=����� " ��.���  � ��# [ . �� 0 True/ �� $ Machine.ChuckVacuum�.a���  � ��# [ �� $��3q����� " ��.���  � ��
 ���� . �� 0 True/ �� $ Machine.SwitchOnBlowing�.a���  � ��# [ ._�]� ��.  .+��� $��
Обдув��$
��Left=��� -   $��VideoImg��$��3q�$& 0������ *
Machine.Bi+$ OneWay=����� *	Cols[1]+���� *	Rows[1]+�� }�. $& 1������ *	Cols[1]+���� *	Rows[1]+���� $ VideoImg��$ ActualWidth����� $ VideoImg��$ ActualHeight�.+�� ���� $ ActualWidth� �� $ FindAncestor=�$ CanvasL����� $ ActualHeight� �� $ FindAncestor=�$ CanvasL�.+��� : �� TextAlignment$: Center=�$��3(�$��Normal$�$��TimesNewRoman)�
��� E��$
��Bold"�$��Center=�$	��Top=�$ 0.5,-1&����� $ Process.ProcessMessage�z�V� .~��� * `� #
 [ .1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $ 1�$	% 100�� `� #
 [ .1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $ 1�$	% 100���� $: Center=�$��3(�$��Normal$�$��TimesNewRoman)�
���/���$
��Bold"�$��Center=�$	��Top=�$ 0.5,-1&�y��� *" Process.CutWidthMarkerVisibility+���� $ Process.CutOffset�$52 /Корректировка реза, мм:  {0}��z�V� .~��� * `� #
 [ .1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $  �$	% 100�� `� #
 [ .1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $ 1�$	% 100���� 
���"�"����� $ Process.CutWidth����� $ ActualWidth� �� $ FindAncestor=�$ CanvasL�y��� *" Process.CutWidthMarkerVisibility+$% Hidden����`� #
 [ .1��� $  �$% 0.002���� $ Machine.CameraScale�z�V� .~��� $ -1Q�T�   `� #
 [ .1��� $ Process.CutWidth�T�   �� $ Process.CutOffset���  �� $ Machine.CameraScale���  �� $ ActualHeight� �� $ FindAncestor=�$ CanvasL�T�   `� #
 [ .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� -	   $��TeachScaleMarkers��
��� E��$��1������ $ ActualWidth� �� $ FindAncestor=�$ CanvasL�y��� *&$Process.TeachVScaleMarkersVisibility+$% Hidden����`� #
 [ .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $ Machine.TeachMarkersRatio�
; �� Top; `� #
 [ .1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL�z�V� .~��� $ -1Q�$	* 1.5Q�T� $	( -10Q�  `� #
 [ .1��� $ Height� �� $ FindAncestor=�$ 	RectangleL��� 
���  ��$��1��! `� #
 [ < `� ConverterParameter$	< 0,3��.1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� = �� Source$= 1��" `� #
 [ $	< 0,3��.1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL��� $= 2��$ `� #
 [ $	< 0,3��.1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL�# `� #
 [ $	< 0,3��.1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� 
���  ��$��1��$ `� #
 [ $	< 0,3��.1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $= 1��# `� #
 [ $	< 0,3��.1��� $ ActualHeight� �� $ FindAncestor=�$ CanvasL��� $= 2��! `� #
 [ $	< 0,3��.1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL�" `� #
 [ $	< 0,3��.1��� $ ActualWidth� �� $ FindAncestor=�$ CanvasL�