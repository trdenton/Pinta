// 
// ToolManager.cs
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
using System.Collections.Generic;
using System.Linq;
using Gtk;

namespace Pinta.Core
{
	public interface IToolService
	{
		/// <summary>
		/// Gets the currently selected tool.
		/// </summary>
		BaseTool? CurrentTool { get; }

		/// <summary>
		/// Performs the mouse down event for the currently selected tool.
		/// </summary>
		void DoMouseDown (Document document, ToolMouseEventArgs e);

		/// <summary>
		/// Gets the previously selected tool.
		/// </summary>
		BaseTool? PreviousTool { get; }

		/// <summary>
		/// Sets the current tool to the specified tool.
		/// </summary>
		void SetCurrentTool (BaseTool tool);

		/// <summary>
		/// Sets the current tool to the first tool with the specified tool type name, like
		/// 'PencilTool'. Returns a value indicating if tool was successfully changed.
		/// </summary>
		bool SetCurrentTool (string tool);
	}

	public class ToolManager : IEnumerable<BaseTool>, IToolService
	{
		int index = -1;
		int prev_index = -1;
		
		private List<BaseTool> Tools;
		private Dictionary<Gdk.Key, List<BaseTool>> groupedTools;
		private Gdk.Key LastUsedKey;
		private int PressedShortcutCounter;

		public event EventHandler<ToolEventArgs>? ToolAdded;
		public event EventHandler<ToolEventArgs>? ToolRemoved;

		public ToolManager ()
		{
			Tools = new List<BaseTool> ();
			groupedTools = new Dictionary<Gdk.Key, List<BaseTool>>();
			PressedShortcutCounter = 0;
		}

		public void AddTool (BaseTool tool)
		{
			tool.ToolItem.Clicked += HandlePbToolItemClicked;
			
			Tools.Add (tool);
			Tools.Sort (new ToolSorter ());

			OnToolAdded (tool);

			if (CurrentTool == null)
				SetCurrentTool (tool);

			if (!groupedTools.ContainsKey(tool.ShortcutKey))
				groupedTools.Add(tool.ShortcutKey, new List<BaseTool>());

			groupedTools[tool.ShortcutKey].Add(tool);
		}

		public void RemoveInstanceOfTool (System.Type tool_type)
		{
			foreach (BaseTool tool in Tools) {
				if (tool.GetType () == tool_type) {
					tool.ToolItem.Clicked -= HandlePbToolItemClicked;
					tool.ToolItem.Active = false;
					tool.ToolItem.Sensitive = false;

					Tools.Remove (tool);
					Tools.Sort (new ToolSorter ());

					// Are we trying to remove the current tool?
					if (CurrentTool == tool) {
						// Can we set it back to the previous tool?
						if (PreviousTool is not null && PreviousTool != CurrentTool)
							SetCurrentTool (PreviousTool);
						else if (Tools.Any ())  // Any tool?
							SetCurrentTool (Tools.First ());
						else {
							// There are no tools left.
							DeactivateTool (tool, null);
							prev_index = -1;
							index = -1;
						}
					}

					OnToolRemoved (tool);

					if (groupedTools[tool.ShortcutKey].Count > 1)
						groupedTools[tool.ShortcutKey].Remove(tool);
					else
						groupedTools.Remove(tool.ShortcutKey);

					return;
				}
			}
		}

		void HandlePbToolItemClicked (object? sender, EventArgs e)
		{
			if (sender is not ToolBoxButton tb)
				return;

			var new_tool = tb.Tool;

			// Don't let the user unselect the current tool	
			if (CurrentTool != null && new_tool.GetType ().Name == CurrentTool.GetType ().Name) {
				if (prev_index != index)
					tb.Active = true;

				return;
			}

			SetCurrentTool (new_tool);
		}

		private BaseTool? FindTool (string name)
		{
			return Tools.FirstOrDefault (t => string.Compare (name, t.GetType ().Name, true) == 0);
		}

		public BaseTool? CurrentTool => index >= 0 ? Tools[index] : null;

		public BaseTool? PreviousTool => prev_index >= 0 ? Tools[prev_index] : null;

		public void Commit ()
		{
			if (CurrentTool != null)
				CurrentTool.DoCommit (PintaCore.Workspace.HasOpenDocuments ? PintaCore.Workspace.ActiveDocument : null);
		}

		public void SetCurrentTool(BaseTool tool)
		{
			int i = Tools.IndexOf (tool);
			
			if (index == i)
				return;

			// Unload previous tool if needed
			if (index >= 0) {
				prev_index = index;

				var prev_tool = Tools[index];

				DeactivateTool (prev_tool, tool);
			}
			
			// Load new tool
			index = i;
			tool.ToolItem.Active = true;
			tool.DoActivated(PintaCore.Workspace.HasOpenDocuments ? PintaCore.Workspace.ActiveDocument : null);

			ToolImage.Set (tool.Icon);

			PintaCore.Chrome.ToolToolBar.AppendItem (ToolLabel);
			PintaCore.Chrome.ToolToolBar.AppendItem (ToolImage);
			PintaCore.Chrome.ToolToolBar.AppendItem (ToolSeparator);

			tool.DoBuildToolBar (PintaCore.Chrome.ToolToolBar);
			
			PintaCore.Workspace.Invalidate ();
			PintaCore.Chrome.SetStatusBarText (string.Format (" {0}: {1}", tool.Name, tool.StatusBarText));
		}

		public bool SetCurrentTool (string tool)
		{
			var t = FindTool (tool);
			
			if (t != null) {
				SetCurrentTool (t);
				return true;
			}
			
			return false;
		}
		
		public void SetCurrentTool (Gdk.Key shortcut)
		{
			BaseTool? tool = FindNextTool (shortcut);
			
			if (tool != null)
				SetCurrentTool(tool);
		}

		private BaseTool? FindNextTool (Gdk.Key shortcut)
		{
			shortcut = shortcut.ToUpper();

			if (groupedTools.ContainsKey(shortcut))
			{
				for (int i = 0; i < groupedTools[shortcut].Count; i++)
				{
					if (LastUsedKey != shortcut)
					{
						// Return first tool in group.
						PressedShortcutCounter = (1 % groupedTools[shortcut].Count);
						LastUsedKey = shortcut;
						return groupedTools[shortcut][0];
					}
					else if(i == PressedShortcutCounter)
					{
						var tool = groupedTools[shortcut][PressedShortcutCounter];
						PressedShortcutCounter = (i + 1) % groupedTools[shortcut].Count;
						return tool;
					}
				}
			}

            return null;
		}

		private void DeactivateTool (BaseTool tool, BaseTool? newTool)
		{
			var toolbar = PintaCore.Chrome.ToolToolBar;

			while (toolbar.NItems > 0)
				toolbar.Remove (toolbar.Children[toolbar.NItems - 1]);

			tool.DoDeactivated (PintaCore.Workspace.HasOpenDocuments ? PintaCore.Workspace.ActiveDocument : null, newTool);
			tool.ToolItem.Active = false;
		}

		public void DoMouseDown (Document document, ToolMouseEventArgs e) => CurrentTool?.DoMouseDown (document, e);
		public void DoMouseDown (Document document, ButtonPressEventArgs args) => CurrentTool?.DoMouseDown (document, args);
		public void DoMouseUp (Document document, ButtonReleaseEventArgs args) => CurrentTool?.DoMouseUp (document, args);
		public void DoMouseMove (Document document, MotionNotifyEventArgs args) => CurrentTool?.DoMouseMove (document, args);
		public void DoKeyDown (Document document, KeyPressEventArgs args) => CurrentTool?.DoKeyDown (document, args);
		public void DoKeyUp (Document document, KeyReleaseEventArgs args) => CurrentTool?.DoKeyUp (document, args);
		public void DoAfterSave (Document document) => CurrentTool?.DoAfterSave (document);
		public bool DoHandlePaste (Document document, Clipboard clipboard) => CurrentTool?.DoHandlePaste (document, clipboard) ?? false;

		private void OnToolAdded (BaseTool tool)
		{
			if (ToolAdded != null)
				ToolAdded (this, new ToolEventArgs (tool));
		}

		private void OnToolRemoved (BaseTool tool)
		{
			if (ToolRemoved != null)
				ToolRemoved (this, new ToolEventArgs (tool));
		}

		#region IEnumerable<BaseTool> implementation
		public IEnumerator<BaseTool> GetEnumerator ()
		{
			return Tools.GetEnumerator ();
		}
		#endregion

		#region IEnumerable implementation
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return Tools.GetEnumerator ();
		}
		#endregion

		class ToolSorter : Comparer<BaseTool>
		{
			public override int Compare (BaseTool? x, BaseTool? y)
			{

				return (x?.Priority ?? 0) - (y?.Priority ?? 0);
			}
		}

		protected ToolBarLabel? tool_label;
		protected ToolBarImage? tool_image;
		protected SeparatorToolItem? tool_sep;

		private ToolBarLabel ToolLabel => tool_label ??= new ToolBarLabel ($" {Translations.GetString ("Tool")}:  ");
		private ToolBarImage ToolImage => tool_image ??= new ToolBarImage (string.Empty);
		private SeparatorToolItem ToolSeparator => tool_sep ??= new SeparatorToolItem ();

	}

	//Key extensions for more convenient usage of Gdk key enumerator
	public static class KeyExtensions
    {
		public static Gdk.Key ToUpper(this Gdk.Key k1)
		{
            try
            {
				return (Gdk.Key)Enum.Parse(typeof(Gdk.Key), k1.ToString().ToUpperInvariant());
			}
            catch (ArgumentException)
            {
				//there is a need to catch argument exception because some buttons have not its UpperCase variant
				return k1;
            }
		}
	}
}
