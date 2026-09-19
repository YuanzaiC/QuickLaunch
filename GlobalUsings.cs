global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// UseWindowsForms 会隐式引入 System.Drawing / System.Windows.Forms，
// 其中若干部件与 WPF 同名，这里统一指向 WPF 版本，避免到处写完整命名空间。
global using Point = System.Windows.Point;
global using Rect = System.Windows.Rect;
global using Color = System.Windows.Media.Color;
global using Brush = System.Windows.Media.Brush;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;