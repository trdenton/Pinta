// 
// LineCurveTool.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Cairo;
using Pinta.Core;
using Mono.Unix;
using System.Collections.Generic;
using System.Linq;

namespace Pinta.Tools
{
	public class LineCurveTool : ShapeTool
	{
		public override string Name
		{
			get { return Catalog.GetString("Line/Curve"); }
		}
		public override string Icon {
			get { return "Tools.Line.png"; }
		}
		public override string StatusBarText {
			get { return Catalog.GetString ("Left click to draw a line with primary color." +
					"\nLeft click on a line to add control points." +
					"\nLeft click on a control point and drag to move it." +
					"\nRight click on a control point and drag to change tension." +
					"\nHold Shift to snap to angles." +
					"\nUse arrow keys to move selected control point." +
					"\nPress Ctrl + left/right arrows to navigate through (select) control points by order." +
					"\nPress Delete to delete selected control point." +
					"\nPress Space to create a new point on the outermost side of the selected control point at the mouse position." +
					"\nHold Ctrl while pressing Space to create the point at the exact same position." +
					"\nHold Ctrl while left clicking on a control point to create a new line at the exact same position." +
					"\nHold Ctrl while clicking outside of the Image bounds to create a new line starting at the edge." +
					"\nPress Enter to finalize the shape.");
			}
		}
		public override Gdk.Cursor DefaultCursor {
			get { return new Gdk.Cursor (PintaCore.Chrome.Canvas.Display, PintaCore.Resources.GetIcon ("Cursor.Line.png"), 9, 18); }
		}
		public override int Priority {
			get { return 39; }
		}

        public LineCurveTool()
        {
			editEngine = new LineCurveEditEngine(this);

            editEngine.ShowStrokeComboBox = false;
        }
	}
}
